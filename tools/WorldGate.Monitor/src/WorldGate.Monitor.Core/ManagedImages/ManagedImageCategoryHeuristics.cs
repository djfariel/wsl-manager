namespace WorldGate.Monitor.Core.ManagedImages;

public static class ManagedImageCategoryHeuristics
{
    private static readonly string[] GitHubRunnerNeedles =
    [
        "actions-runner",
        "actions/actions-runner",
        "gha-runner-scale-set",
        "worldgate-runner",
        "worldgate",
        "factorio-runner",
        "/runner:",
        "arc-runners",
    ];

    public static ManagedImageCategory Classify(string imageReference)
    {
        if (string.IsNullOrWhiteSpace(imageReference))
        {
            return ManagedImageCategory.OtherWorkload;
        }

        string normalized = imageReference.Trim();
        if (normalized.Equals("hello-world:latest", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("hello-world:", StringComparison.OrdinalIgnoreCase))
        {
            return ManagedImageCategory.OtherWorkload;
        }

        foreach (string needle in GitHubRunnerNeedles)
        {
            if (normalized.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return ManagedImageCategory.GitHubRunner;
            }
        }

        if (normalized.Contains("ghcr.io/", StringComparison.OrdinalIgnoreCase)
            && normalized.Contains("runner", StringComparison.OrdinalIgnoreCase))
        {
            return ManagedImageCategory.GitHubRunner;
        }

        return ManagedImageCategory.OtherWorkload;
    }

    public static bool IsInfrastructureControllerImage(string imageReference) =>
        !string.IsNullOrWhiteSpace(imageReference)
        && imageReference.Contains("gha-runner-scale-set-controller", StringComparison.OrdinalIgnoreCase);
}
