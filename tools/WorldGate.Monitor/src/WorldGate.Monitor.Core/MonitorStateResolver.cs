namespace WorldGate.Monitor.Core;

public static class MonitorStateResolver
{
    public static MonitorSnapshot Resolve(GitHubObservation github, ArcObservation arc)
    {
        MonitorDisplayState state;

        if (!github.Available || !arc.Available)
        {
            state = MonitorDisplayState.Unavailable;
        }
        else if (!arc.ControllerReady || !arc.ListenerReady)
        {
            state = MonitorDisplayState.Degraded;
        }
        else if (github.ActiveJob?.IsRunning == true || arc.ActiveRunnerCount > 0)
        {
            state = MonitorDisplayState.Running;
        }
        else if (github.ActiveJob?.IsQueued == true)
        {
            state = MonitorDisplayState.Queued;
        }
        else if (IsFailureConclusion(github.LatestCompletedJob?.Conclusion))
        {
            state = MonitorDisplayState.Degraded;
        }
        else
        {
            state = MonitorDisplayState.Healthy;
        }

        return new MonitorSnapshot(state, github, arc);
    }

    private static bool IsFailureConclusion(string? conclusion)
    {
        if (string.IsNullOrWhiteSpace(conclusion))
        {
            return false;
        }

        return conclusion.Equals("failure", StringComparison.OrdinalIgnoreCase) ||
               conclusion.Equals("cancelled", StringComparison.OrdinalIgnoreCase) ||
               conclusion.Equals("timed_out", StringComparison.OrdinalIgnoreCase) ||
               conclusion.Equals("action_required", StringComparison.OrdinalIgnoreCase) ||
               conclusion.Equals("startup_failure", StringComparison.OrdinalIgnoreCase);
    }
}
