namespace WorldGate.Monitor.Core.ManagedImages;

public static class ManagedImageBuildContextResolver
{
    private static readonly string[] WorldGateRunnerContextCandidates =
    [
        @"E:\Projects\Worktrees\WorldGate\infra\arc\runner",
        @"E:\Projects\RemoteRepositories\WorldGate\infra\arc\runner",
    ];

    private static readonly string[] FactorioRunnerContextCandidates =
    [
        @"E:\Projects\Worktrees\factorio-runner\infra\arc\runner",
        @"E:\Projects\RemoteRepositories\factorio-runner\infra\arc\runner",
        @"E:\Projects\RemoteRepositories\Worktrees\factorio-runner\infra\arc\runner",
    ];

    public static string? ResolveForImageReference(string imageReference)
    {
        if (string.IsNullOrWhiteSpace(imageReference))
        {
            return null;
        }

        if (imageReference.Contains("worldgate-runner", StringComparison.OrdinalIgnoreCase))
        {
            return FirstExistingDirectory(WorldGateRunnerContextCandidates);
        }

        if (imageReference.Contains("factorio-runner", StringComparison.OrdinalIgnoreCase))
        {
            return FirstExistingDirectory(FactorioRunnerContextCandidates);
        }

        return null;
    }

    public static ManagedImageEntry ApplyDiscoveredBuildContext(ManagedImageEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.BuildContextPath))
        {
            return entry;
        }

        string? discovered = ResolveForImageReference(entry.ImageReference);
        return discovered is null ? entry : entry with { BuildContextPath = discovered };
    }

    private static string? FirstExistingDirectory(IReadOnlyList<string> candidates)
    {
        foreach (string path in candidates)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
