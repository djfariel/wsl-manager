namespace WorldGate.Monitor.Core;

public static class TooltipFormatter
{
    public static string Format(MonitorSnapshot snapshot, DateTimeOffset now, int maxLength = 63)
    {
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        var parts = new List<string>
        {
            $"WorldGate: {snapshot.DisplayState.ToString().ToUpperInvariant()}",
        };

        GitHubJobObservation? job = snapshot.GitHub.ActiveJob ?? snapshot.GitHub.LatestCompletedJob;
        if (job is not null)
        {
            parts.Add(job.PullRequestNumber is { } number ? $"PR #{number} {job.RunName}" : job.RunName);
        }

        if (snapshot.GitHub.ActiveJob is { } active)
        {
            DateTimeOffset start = active.StartedAt ?? active.CreatedAt;
            TimeSpan elapsed = now >= start ? now - start : TimeSpan.Zero;
            parts.Add(elapsed.TotalHours >= 1 ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m" : $"{Math.Max(0, (int)elapsed.TotalMinutes)}m");
        }

        if (snapshot.Arc.RunnerPodName is { } pod)
        {
            parts.Add(pod);
        }
        else if (snapshot.Arc.Available)
        {
            parts.Add(snapshot.Arc.Healthy ? "ARC healthy" : "ARC degraded");
        }

        string value = string.Join(" | ", parts);
        if (value.Length <= maxLength)
        {
            return value;
        }

        if (maxLength == 1)
        {
            return "…";
        }

        return value[..(maxLength - 1)] + "…";
    }
}
