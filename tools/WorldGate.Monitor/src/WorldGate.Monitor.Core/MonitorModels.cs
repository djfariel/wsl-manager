namespace WorldGate.Monitor.Core;

public enum MonitorDisplayState
{
    Unavailable,
    Degraded,
    Queued,
    Running,
    Healthy,
}

public enum MonitorNotificationKind
{
    Started,
    Passed,
    Failed,
    Cancelled,
    RunnerDelayed,
}

public sealed record WorkflowRunSummary(
    long Id,
    string Name,
    string HtmlUrl,
    int? PullRequestNumber,
    string? Branch,
    DateTimeOffset CreatedAt);

public sealed record GitHubJobObservation(
    long JobId,
    long RunId,
    string RunName,
    string RunUrl,
    int? PullRequestNumber,
    string? Branch,
    string JobName,
    string Status,
    string? Conclusion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<string> Labels)
{
    public bool IsRunning => Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase);

    public bool IsQueued =>
        Status.Equals("queued", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("waiting", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("pending", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("requested", StringComparison.OrdinalIgnoreCase);

    public bool IsCompleted =>
        Status.Equals("completed", StringComparison.OrdinalIgnoreCase) || CompletedAt is not null;

    public TimeSpan? Duration
    {
        get
        {
            if (CompletedAt is null)
            {
                return null;
            }

            DateTimeOffset start = StartedAt ?? CreatedAt;
            return CompletedAt.Value >= start ? CompletedAt.Value - start : null;
        }
    }
}

public sealed record GitHubObservation(
    bool Available,
    string? Error,
    GitHubJobObservation? ActiveJob,
    GitHubJobObservation? LatestCompletedJob)
{
    public static GitHubObservation Empty { get; } = new(true, null, null, null);

    public static GitHubObservation Unavailable(string error) => new(false, error, null, null);
}

public sealed record RunnerPodObservation(string Name, string Phase, bool Ready);

public sealed record ArcObservation(
    bool Available,
    string? Error,
    bool ControllerReady,
    bool ListenerReady,
    IReadOnlyList<RunnerPodObservation> RunnerPods)
{
    public bool Healthy => Available && ControllerReady && ListenerReady;

    public int ActiveRunnerCount => RunnerPods.Count;

    public string? RunnerPodName => RunnerPods.Count == 1 ? RunnerPods[0].Name : null;

    public static ArcObservation Unavailable(string error) =>
        new(false, error, false, false, Array.Empty<RunnerPodObservation>());
}

public sealed record MonitorSnapshot(
    MonitorDisplayState DisplayState,
    GitHubObservation GitHub,
    ArcObservation Arc);

public sealed record MonitorNotification(
    MonitorNotificationKind Kind,
    string Title,
    string Message,
    long? JobId);
