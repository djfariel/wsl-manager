namespace WorldGate.Monitor.Core.ManagedImages;

public static class DockerCliOutputParser
{
    /// <summary>
    /// Parses <c>docker image inspect -f '{{.Id}}'</c> stdout (non-empty means present).
    /// </summary>
    public static ManagedImagePresence ParseImageInspect(string? stdout, int exitCode)
    {
        if (exitCode != 0)
        {
            return ManagedImagePresence.Absent;
        }

        return string.IsNullOrWhiteSpace(stdout) ? ManagedImagePresence.Absent : ManagedImagePresence.Present;
    }

    /// <summary>
    /// Parses lines from <c>docker ps -a --filter reference=REF --format '{{.State}}'</c>.
    /// </summary>
    public static (int Running, int Total) ParseContainerStates(string? stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return (0, 0);
        }

        int running = 0;
        int total = 0;
        foreach (string rawLine in stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string state = rawLine.Trim();
            if (state.Length == 0)
            {
                continue;
            }

            total++;
            if (state.Equals("running", StringComparison.OrdinalIgnoreCase))
            {
                running++;
            }
        }

        return (running, total);
    }

    public static ManagedImageRuntimeState ResolveRuntime(int running, int total)
    {
        if (total == 0)
        {
            return ManagedImageRuntimeState.Stopped;
        }

        if (running == total)
        {
            return ManagedImageRuntimeState.Running;
        }

        if (running == 0)
        {
            return ManagedImageRuntimeState.Stopped;
        }

        return ManagedImageRuntimeState.Partial;
    }

    public static string FormatSummary(
        ManagedImagePresence presence,
        ManagedImageRuntimeState runtime,
        int running,
        int total)
    {
        if (presence == ManagedImagePresence.Absent)
        {
            return "Image not installed";
        }

        return runtime switch
        {
            ManagedImageRuntimeState.Running => total == 1 ? "Running" : $"Running ({running}/{total} containers)",
            ManagedImageRuntimeState.Partial => $"Partial ({running}/{total} running)",
            ManagedImageRuntimeState.Stopped => total > 0 ? $"Stopped ({total} container(s))" : "Installed, no containers",
            _ => presence == ManagedImagePresence.Present ? "Installed" : "Unknown",
        };
    }
}
