namespace WorldGate.Monitor.Core.ManagedImages;

public static class DockerCliOutputParser
{
    /// <summary>
    /// Arguments for listing all containers with image and state (no CLI filters — parsed in-process for broad Engine compatibility).
    /// </summary>
    public static readonly string[] ListAllContainersImageAndStateArgs =
        ["docker", "ps", "-a", "--format", "{{.Image}}|{{.State}}"];

    /// <summary>
    /// Arguments for listing all container IDs with image (parsed in-process).
    /// </summary>
    public static readonly string[] ListAllContainersIdAndImageArgs =
        ["docker", "ps", "-a", "--format", "{{.ID}}|{{.Image}}"];

    /// <summary>
    /// User-facing label when a probe command fails; details belong in diagnostics, not the UI.
    /// </summary>
    public const string ProbeFailedSummary = "Probe failed";
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
    /// Parses lines from <c>docker ps -a --format '{{.State}}'</c> (state-only output).
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

    /// <summary>
    /// Parses <see cref="ListAllContainersImageAndStateArgs"/> stdout and counts containers using <paramref name="imageReference"/>.
    /// </summary>
    public static (int Running, int Total) ParseContainerStatesForImageReference(string? stdout, string imageReference)
    {
        if (string.IsNullOrWhiteSpace(stdout) || string.IsNullOrWhiteSpace(imageReference))
        {
            return (0, 0);
        }

        int running = 0;
        int total = 0;
        foreach (string rawLine in stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            int separator = line.IndexOf('|');
            if (separator <= 0 || separator >= line.Length - 1)
            {
                continue;
            }

            string containerImage = line[..separator].Trim();
            string state = line[(separator + 1)..].Trim();
            if (!ImageReferenceMatches(containerImage, imageReference))
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

    /// <summary>
    /// Parses <see cref="ListAllContainersIdAndImageArgs"/> stdout and returns container IDs using <paramref name="imageReference"/>.
    /// </summary>
    public static IReadOnlyList<string> ParseContainerIdsForImageReference(string? stdout, string imageReference)
    {
        if (string.IsNullOrWhiteSpace(stdout) || string.IsNullOrWhiteSpace(imageReference))
        {
            return [];
        }

        var ids = new List<string>();
        foreach (string rawLine in stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            int separator = line.IndexOf('|');
            if (separator <= 0 || separator >= line.Length - 1)
            {
                continue;
            }

            string containerId = line[..separator].Trim();
            string containerImage = line[(separator + 1)..].Trim();
            if (containerId.Length == 0 || !ImageReferenceMatches(containerImage, imageReference))
            {
                continue;
            }

            ids.Add(containerId);
        }

        return ids;
    }

    public static bool ImageReferenceMatches(string containerImage, string imageReference)
    {
        if (string.IsNullOrWhiteSpace(containerImage) || string.IsNullOrWhiteSpace(imageReference))
        {
            return false;
        }

        string image = containerImage.Trim();
        string reference = imageReference.Trim();
        if (image.Equals(reference, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (image.StartsWith(reference + "@", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
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
