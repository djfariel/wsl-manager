namespace WorldGate.Monitor.Core.ManagedImages;

public enum ManagedImageCategory
{
    GitHubRunner,
    OtherWorkload,
}

public enum ManagedImageProbeBackend
{
    Docker,
    Kubernetes,
}

public sealed record ManagedImageEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string DisplayName { get; init; } = string.Empty;

    public string ImageReference { get; init; } = string.Empty;

    public string? BuildContextPath { get; init; }

    public ManagedImageCategory Category { get; init; } = ManagedImageCategory.GitHubRunner;

    public ManagedImageProbeBackend ProbeBackend { get; init; } = ManagedImageProbeBackend.Docker;

    public string? KubernetesNamespace { get; init; }

    public bool MonitorStatus { get; init; }
}

public enum ManagedImagePresence
{
    Unknown,
    Absent,
    Present,
}

public enum ManagedImageRuntimeState
{
    Unknown,
    Stopped,
    Running,
    Partial,
}

public sealed record ManagedImageStatusSnapshot(
    string EntryId,
    string DisplayName,
    ManagedImagePresence ImagePresence,
    ManagedImageRuntimeState RuntimeState,
    int RunningContainerCount,
    int TotalContainerCount,
    string Summary,
    string? ErrorMessage)
{
    public static ManagedImageStatusSnapshot WslOff(string entryId, string displayName) =>
        new(
            entryId,
            displayName,
            ManagedImagePresence.Unknown,
            ManagedImageRuntimeState.Unknown,
            0,
            0,
            "WSL off",
            null);

    public static ManagedImageStatusSnapshot Unavailable(string entryId, string displayName, string error) =>
        new(
            entryId,
            displayName,
            ManagedImagePresence.Unknown,
            ManagedImageRuntimeState.Unknown,
            0,
            0,
            "Unavailable",
            error);
}
