namespace WorldGate.Monitor.Core.ManagedImages;

public sealed record ManagedImageDiscoveryCandidate(
    string ImageReference,
    ManagedImageProbeBackend ProbeBackend,
    string? KubernetesNamespace,
    string? BuildContextPath,
    bool PreferMonitor);

public static class ManagedImageSeedMerger
{
    public static IReadOnlyList<ManagedImageEntry> Merge(
        IReadOnlyList<ManagedImageEntry> existing,
        IReadOnlyList<ManagedImageDiscoveryCandidate> discovered,
        string? runnerScaleSetName = null)
    {
        var merged = existing.ToList();
        var knownRefs = new HashSet<string>(merged.Select(entry => NormalizeRef(entry.ImageReference)), StringComparer.OrdinalIgnoreCase);

        foreach (ManagedImageDiscoveryCandidate candidate in discovered)
        {
            string normalized = NormalizeRef(candidate.ImageReference);
            if (normalized.Length == 0 || knownRefs.Contains(normalized))
            {
                continue;
            }

            knownRefs.Add(normalized);
            merged.Add(ToEntry(candidate));
        }

        return ApplyPrimaryMonitor(merged, runnerScaleSetName);
    }

    public static string? PickPrimaryMonitorImageReference(
        IReadOnlyList<ManagedImageEntry> entries,
        string? runnerScaleSetName)
    {
        IReadOnlyList<ManagedImageEntry> runners = entries
            .Where(entry => entry.Category == ManagedImageCategory.GitHubRunner)
            .ToArray();

        if (runners.Count == 0)
        {
            return null;
        }

        ManagedImageEntry? worldgate = runners
            .Where(entry => entry.ImageReference.Contains("worldgate-runner", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.ImageReference, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (worldgate is not null)
        {
            return worldgate.ImageReference;
        }

        if (!string.IsNullOrWhiteSpace(runnerScaleSetName))
        {
            ManagedImageEntry? scaleSetMatch = runners
                .FirstOrDefault(entry => entry.DisplayName.Contains(runnerScaleSetName, StringComparison.OrdinalIgnoreCase)
                    || entry.ImageReference.Contains(runnerScaleSetName, StringComparison.OrdinalIgnoreCase));
            if (scaleSetMatch is not null)
            {
                return scaleSetMatch.ImageReference;
            }
        }

        return runners
            .OrderByDescending(entry => entry.ImageReference, StringComparer.OrdinalIgnoreCase)
            .First()
            .ImageReference;
    }

    private static IReadOnlyList<ManagedImageEntry> ApplyPrimaryMonitor(
        IReadOnlyList<ManagedImageEntry> entries,
        string? runnerScaleSetName)
    {
        if (entries.Any(entry => entry.MonitorStatus))
        {
            return entries;
        }

        string? primaryRef = PickPrimaryMonitorImageReference(entries, runnerScaleSetName);
        if (primaryRef is null)
        {
            return entries;
        }

        return entries
            .Select(entry => entry with
            {
                MonitorStatus = string.Equals(
                    NormalizeRef(entry.ImageReference),
                    NormalizeRef(primaryRef),
                    StringComparison.OrdinalIgnoreCase),
            })
            .ToArray();
    }

    private static ManagedImageEntry ToEntry(ManagedImageDiscoveryCandidate candidate)
    {
        var category = ManagedImageCategoryHeuristics.Classify(candidate.ImageReference);
        if (ManagedImageCategoryHeuristics.IsInfrastructureControllerImage(candidate.ImageReference))
        {
            category = ManagedImageCategory.OtherWorkload;
        }

        return new ManagedImageEntry
        {
            DisplayName = BuildDisplayName(candidate.ImageReference),
            ImageReference = candidate.ImageReference.Trim(),
            BuildContextPath = candidate.BuildContextPath,
            Category = category,
            ProbeBackend = candidate.ProbeBackend,
            KubernetesNamespace = candidate.KubernetesNamespace,
            MonitorStatus = candidate.PreferMonitor,
        };
    }

    public static string BuildDisplayName(string imageReference)
    {
        string trimmed = imageReference.Trim();
        int slash = trimmed.LastIndexOf('/');
        string tail = slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
        int at = tail.IndexOf('@');
        if (at > 0)
        {
            tail = tail[..at];
        }

        return tail;
    }

    public static string NormalizeRef(string reference)
    {
        string trimmed = reference.Trim();
        int at = trimmed.IndexOf('@');
        if (at > 0)
        {
            trimmed = trimmed[..at];
        }

        return trimmed;
    }
}
