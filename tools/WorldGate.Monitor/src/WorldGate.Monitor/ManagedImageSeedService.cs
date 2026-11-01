using WorldGate.Monitor.Core.ManagedImages;
using WorldGate.Monitor.Core.WslRuntime;

namespace WorldGate.Monitor;

internal sealed class ManagedImageSeedService
{
    private readonly ConfigurationStore configurationStore;
    private readonly TimeSpan commandTimeout;

    public ManagedImageSeedService(ConfigurationStore configurationStore, TimeSpan commandTimeout)
    {
        this.configurationStore = configurationStore;
        this.commandTimeout = commandTimeout;
    }

    public async Task<ManagedImageSeedResult> TrySeedIfEmptyAsync(
        MonitorConfiguration configuration,
        string configurationPath,
        CancellationToken cancellationToken = default)
    {
        if (configuration.ManagedImages.Count > 0)
        {
            return ManagedImageSeedResult.Skipped("managedImages already populated");
        }

        IReadOnlyList<ManagedImageDiscoveryCandidate> discovered = await DiscoverAsync(configuration, cancellationToken).ConfigureAwait(false);
        if (discovered.Count == 0)
        {
            return ManagedImageSeedResult.Skipped("no images discovered");
        }

        IReadOnlyList<ManagedImageEntry> merged = ManagedImageSeedMerger.Merge(
            configuration.ManagedImages,
            discovered,
            configuration.RunnerScaleSetName);

        MonitorConfiguration next = configuration with { ManagedImages = merged.ToArray() };
        ConfigurationSaveResult save = configurationStore.Save(next, configurationPath);
        if (!save.Succeeded)
        {
            return ManagedImageSeedResult.Failed(save.Error ?? "save failed");
        }

        return ManagedImageSeedResult.Succeeded(merged, discovered);
    }

    internal async Task<IReadOnlyList<ManagedImageDiscoveryCandidate>> DiscoverAsync(
        MonitorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var candidates = new List<ManagedImageDiscoveryCandidate>();

        WslRuntimeSnapshot runtime = WslRuntimeProbe.Detect();
        if (runtime.State == WslRuntimeState.Running)
        {
            var runner = new WslCommandRunner(configuration.WslDistribution, commandTimeout);
            await AddDockerCandidatesAsync(runner, candidates, cancellationToken).ConfigureAwait(false);
            await AddKubernetesCandidatesAsync(runner, configuration, candidates, cancellationToken).ConfigureAwait(false);
        }

        return DeduplicateCandidates(candidates);
    }

    private static async Task AddDockerCandidatesAsync(
        WslCommandRunner runner,
        List<ManagedImageDiscoveryCandidate> candidates,
        CancellationToken cancellationToken)
    {
        WslCommandResult result = await runner.RunAsync(
            ["bash", "-lc", "docker images --format '{{.Repository}}:{{.Tag}}'"],
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            return;
        }

        foreach (string reference in DockerImageListParser.ParseRepositoryTags(result.StandardOutput))
        {
            if (reference.EndsWith("<none>:<none>", StringComparison.Ordinal))
            {
                continue;
            }

            string? context = ManagedImageBuildContextResolver.ResolveForImageReference(reference);

            candidates.Add(new ManagedImageDiscoveryCandidate(
                reference,
                ManagedImageProbeBackend.Docker,
                null,
                context,
                PreferMonitor: false));
        }
    }

    private static async Task AddKubernetesCandidatesAsync(
        WslCommandRunner runner,
        MonitorConfiguration configuration,
        List<ManagedImageDiscoveryCandidate> candidates,
        CancellationToken cancellationToken)
    {
        WslCommandResult result = await runner.RunAsync(
            ["bash", "-lc", "kubectl get pods -A -o json"],
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            return;
        }

        foreach (DiscoveredPodImage podImage in KubectlPodImageDiscovery.ParsePodsJson(result.StandardOutput))
        {
            if (!IsRelevantNamespace(podImage.Namespace, configuration))
            {
                continue;
            }

            candidates.Add(new ManagedImageDiscoveryCandidate(
                podImage.ImageReference,
                ManagedImageProbeBackend.Kubernetes,
                podImage.Namespace,
                null,
                PreferMonitor: false));
        }
    }

    private static bool IsRelevantNamespace(string ns, MonitorConfiguration configuration) =>
        string.Equals(ns, configuration.RunnerNamespace, StringComparison.OrdinalIgnoreCase)
        || string.Equals(ns, configuration.ControllerNamespace, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<ManagedImageDiscoveryCandidate> DeduplicateCandidates(
        IReadOnlyList<ManagedImageDiscoveryCandidate> candidates)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<ManagedImageDiscoveryCandidate>();
        foreach (ManagedImageDiscoveryCandidate candidate in candidates)
        {
            string key = ManagedImageSeedMerger.NormalizeRef(candidate.ImageReference);
            if (!seen.Add(key))
            {
                continue;
            }

            unique.Add(candidate);
        }

        return unique;
    }

}

internal sealed record ManagedImageSeedResult(
    bool Applied,
    string? Message,
    IReadOnlyList<ManagedImageEntry> Entries,
    IReadOnlyList<ManagedImageDiscoveryCandidate> Discovered)
{
    public static ManagedImageSeedResult Skipped(string message) =>
        new(false, message, [], []);

    public static ManagedImageSeedResult Failed(string message) =>
        new(false, message, [], []);

    public static ManagedImageSeedResult Succeeded(
        IReadOnlyList<ManagedImageEntry> entries,
        IReadOnlyList<ManagedImageDiscoveryCandidate> discovered) =>
        new(true, null, entries, discovered);
}
