namespace WorldGate.Monitor.Core;

public sealed class NotificationPlanner
{
    private readonly TimeSpan queueWarningThreshold;
    private readonly HashSet<long> startedJobs = new();
    private readonly HashSet<long> completedJobs = new();
    private readonly HashSet<long> delayedJobs = new();
    private bool initialized;

    public NotificationPlanner(TimeSpan queueWarningThreshold)
    {
        this.queueWarningThreshold = queueWarningThreshold;
    }

    public IReadOnlyList<MonitorNotification> Evaluate(MonitorSnapshot snapshot, DateTimeOffset now)
    {
        if (!snapshot.GitHub.Available)
        {
            return Array.Empty<MonitorNotification>();
        }

        GitHubJobObservation? active = snapshot.GitHub.ActiveJob;
        GitHubJobObservation? completed = snapshot.GitHub.LatestCompletedJob;

        if (!initialized)
        {
            if (active?.IsRunning == true)
            {
                startedJobs.Add(active.JobId);
            }

            if (completed is not null)
            {
                completedJobs.Add(completed.JobId);
            }

            initialized = true;
            return Array.Empty<MonitorNotification>();
        }

        var notifications = new List<MonitorNotification>();

        if (active?.IsRunning == true && startedJobs.Add(active.JobId))
        {
            notifications.Add(new MonitorNotification(
                MonitorNotificationKind.Started,
                "WorldGate CI started",
                BuildStartedMessage(active, snapshot.Arc.RunnerPodName),
                active.JobId));
        }

        if (completed is not null && completedJobs.Add(completed.JobId))
        {
            MonitorNotification? completion = BuildCompletionNotification(completed);
            if (completion is not null)
            {
                notifications.Add(completion);
            }
        }

        if (active?.IsQueued == true &&
            now - active.CreatedAt >= queueWarningThreshold &&
            snapshot.Arc.Available &&
            snapshot.Arc.ControllerReady &&
            snapshot.Arc.ListenerReady &&
            snapshot.Arc.ActiveRunnerCount == 0 &&
            delayedJobs.Add(active.JobId))
        {
            notifications.Add(new MonitorNotification(
                MonitorNotificationKind.RunnerDelayed,
                "WorldGate runner delayed",
                $"{BuildContextLine(active)}\nQueued for {FormatDuration(now - active.CreatedAt)}; no runner pod has appeared.",
                active.JobId));
        }

        return notifications;
    }

    private static MonitorNotification? BuildCompletionNotification(GitHubJobObservation job)
    {
        string? conclusion = job.Conclusion;
        if (conclusion?.Equals("success", StringComparison.OrdinalIgnoreCase) == true)
        {
            string duration = job.Duration is { } value ? $"\nDuration: {FormatDuration(value)}" : string.Empty;
            return new MonitorNotification(
                MonitorNotificationKind.Passed,
                "WorldGate CI passed",
                $"{BuildContextLine(job)}{duration}",
                job.JobId);
        }

        if (conclusion?.Equals("cancelled", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new MonitorNotification(
                MonitorNotificationKind.Cancelled,
                "WorldGate CI cancelled",
                $"{BuildContextLine(job)}\nCancelled job: {job.JobName}",
                job.JobId);
        }

        if (conclusion is not null && !conclusion.Equals("neutral", StringComparison.OrdinalIgnoreCase) && !conclusion.Equals("skipped", StringComparison.OrdinalIgnoreCase))
        {
            return new MonitorNotification(
                MonitorNotificationKind.Failed,
                "WorldGate CI failed",
                $"{BuildContextLine(job)}\nFailed job: {job.JobName}",
                job.JobId);
        }

        return null;
    }

    private static string BuildStartedMessage(GitHubJobObservation job, string? runnerPodName)
    {
        string runnerLine = runnerPodName is null ? string.Empty : $"\nRunner pod: {runnerPodName}";
        return $"{BuildContextLine(job)}{runnerLine}";
    }

    private static string BuildContextLine(GitHubJobObservation job) =>
        job.PullRequestNumber is { } number
            ? $"PR #{number} — {job.RunName}"
            : job.RunName;

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        }

        return $"{Math.Max(0, (int)duration.TotalSeconds)}s";
    }
}
