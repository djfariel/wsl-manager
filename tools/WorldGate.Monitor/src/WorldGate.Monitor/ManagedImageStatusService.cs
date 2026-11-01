using WorldGate.Monitor.Core.ManagedImages;
using WorldGate.Monitor.Core.WslRuntime;

namespace WorldGate.Monitor;

internal sealed class ManagedImageStatusService
{
    private readonly ConfigurationStore configurationStore = new();
    private readonly TimeSpan commandTimeout = TimeSpan.FromSeconds(25);

    public ManagedImageStatusService(string? _)
    {
    }

    internal ManagedImageStatusService(ConfigurationStore configurationStore, TimeSpan commandTimeout)
    {
        this.configurationStore = configurationStore;
        this.commandTimeout = commandTimeout;
    }

    private WslCommandRunner CreateRunner()
    {
        ConfigurationLoadResult loaded = configurationStore.LoadOrCreate();
        return new WslCommandRunner(loaded.Configuration.WslDistribution, commandTimeout);
    }

    public IReadOnlyList<ManagedImageEntry> LoadEntries()
    {
        ConfigurationLoadResult loaded = configurationStore.LoadOrCreate();
        return loaded.Configuration.ManagedImages;
    }

    public async Task<IReadOnlyList<ManagedImageStatusSnapshot>> ProbeMonitoredAsync(
        CancellationToken cancellationToken = default)
    {
        ConfigurationLoadResult loaded = configurationStore.LoadOrCreate();
        IReadOnlyList<ManagedImageEntry> entries = loaded.Configuration.ManagedImages;
        IReadOnlyList<ManagedImageEntry> monitored = entries.Where(entry => entry.MonitorStatus).ToArray();
        if (monitored.Count == 0)
        {
            return [];
        }

        WslRuntimeSnapshot runtime = WslRuntimeProbe.Detect();
        if (runtime.State != WslRuntimeState.Running)
        {
            return monitored
                .Select(entry => ManagedImageStatusSnapshot.WslOff(entry.Id, entry.DisplayName))
                .ToArray();
        }

        WslCommandRunner commandRunner = CreateRunner();
        var snapshots = new List<ManagedImageStatusSnapshot>(monitored.Count);
        foreach (ManagedImageEntry entry in monitored)
        {
            ManagedImageStatusSnapshot snapshot = await ProbeEntryAsync(commandRunner, entry, cancellationToken).ConfigureAwait(false);
            snapshots.Add(snapshot);
        }

        return snapshots;
    }

    public async Task<IReadOnlyList<ManagedImageStatusSnapshot>> ProbeAllAsync(
        IReadOnlyList<ManagedImageEntry> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        WslRuntimeSnapshot runtime = WslRuntimeProbe.Detect();
        if (runtime.State != WslRuntimeState.Running)
        {
            return entries
                .Select(entry => ManagedImageStatusSnapshot.WslOff(entry.Id, entry.DisplayName))
                .ToArray();
        }

        WslCommandRunner commandRunner = CreateRunner();
        var snapshots = new List<ManagedImageStatusSnapshot>(entries.Count);
        foreach (ManagedImageEntry entry in entries)
        {
            snapshots.Add(await ProbeEntryAsync(commandRunner, entry, cancellationToken).ConfigureAwait(false));
        }

        return snapshots;
    }

    private async Task<ManagedImageStatusSnapshot> ProbeEntryAsync(
        WslCommandRunner commandRunner,
        ManagedImageEntry entry,
        CancellationToken cancellationToken)
    {
        return entry.ProbeBackend switch
        {
            ManagedImageProbeBackend.Kubernetes => await ProbeKubernetesAsync(commandRunner, entry, cancellationToken).ConfigureAwait(false),
            _ => await ProbeDockerAsync(commandRunner, entry, cancellationToken).ConfigureAwait(false),
        };
    }

    private static async Task<ManagedImageStatusSnapshot> ProbeDockerAsync(
        WslCommandRunner commandRunner,
        ManagedImageEntry entry,
        CancellationToken cancellationToken)
    {
        WslCommandResult inspect = await commandRunner.RunAsync(
            new[] { "docker", "image", "inspect", "-f", "{{.Id}}", entry.ImageReference },
            cancellationToken).ConfigureAwait(false);

        ManagedImagePresence presence = DockerCliOutputParser.ParseImageInspect(
            inspect.StandardOutput,
            inspect.Success ? 0 : 1);

        if (presence == ManagedImagePresence.Absent && !inspect.Success)
        {
            string detail = !string.IsNullOrWhiteSpace(inspect.StandardError)
                ? inspect.StandardError.Trim()
                : inspect.Error ?? "docker image inspect failed";
            if (detail.Contains("No such image", StringComparison.OrdinalIgnoreCase))
            {
                return BuildSnapshot(entry, ManagedImagePresence.Absent, ManagedImageRuntimeState.Stopped, 0, 0, null);
            }

            return ManagedImageStatusSnapshot.ProbeFailed(entry.Id, entry.DisplayName, detail);
        }

        WslCommandResult ps = await commandRunner.RunAsync(
            DockerCliOutputParser.ListAllContainersImageAndStateArgs,
            cancellationToken).ConfigureAwait(false);

        if (!ps.Success)
        {
            string detail = !string.IsNullOrWhiteSpace(ps.StandardError)
                ? ps.StandardError.Trim()
                : ps.Error ?? "docker ps failed";
            return ManagedImageStatusSnapshot.ProbeFailed(entry.Id, entry.DisplayName, detail);
        }

        (int running, int total) = DockerCliOutputParser.ParseContainerStatesForImageReference(
            ps.StandardOutput,
            entry.ImageReference);
        ManagedImageRuntimeState runtime = DockerCliOutputParser.ResolveRuntime(running, total);
        return BuildSnapshot(entry, presence, runtime, running, total, null);
    }

    private static async Task<ManagedImageStatusSnapshot> ProbeKubernetesAsync(
        WslCommandRunner commandRunner,
        ManagedImageEntry entry,
        CancellationToken cancellationToken)
    {
        string ns = entry.KubernetesNamespace!.Trim();
        WslCommandResult pods = await commandRunner.RunAsync(
            new[] { "kubectl", "get", "pods", "-n", ns, "-o", "json" },
            cancellationToken).ConfigureAwait(false);

        if (!pods.Success)
        {
            string detail = !string.IsNullOrWhiteSpace(pods.StandardError)
                ? pods.StandardError.Trim()
                : pods.Error ?? "kubectl get pods failed";
            return ManagedImageStatusSnapshot.ProbeFailed(entry.Id, entry.DisplayName, detail);
        }

        if (!KubernetesPodImageParser.TrySummarizePodsUsingImage(
                pods.StandardOutput,
                entry.ImageReference,
                out int running,
                out int total,
                out string? parseError))
        {
            return ManagedImageStatusSnapshot.ProbeFailed(entry.Id, entry.DisplayName, parseError ?? "parse failed");
        }

        ManagedImagePresence presence = total > 0 ? ManagedImagePresence.Present : ManagedImagePresence.Absent;
        ManagedImageRuntimeState runtime = DockerCliOutputParser.ResolveRuntime(running, total);
        return BuildSnapshot(entry, presence, runtime, running, total, null);
    }

    private static ManagedImageStatusSnapshot BuildSnapshot(
        ManagedImageEntry entry,
        ManagedImagePresence presence,
        ManagedImageRuntimeState runtime,
        int running,
        int total,
        string? error)
    {
        string summary = DockerCliOutputParser.FormatSummary(presence, runtime, running, total);
        return new ManagedImageStatusSnapshot(
            entry.Id,
            entry.DisplayName,
            presence,
            runtime,
            running,
            total,
            summary,
            error);
    }
}
