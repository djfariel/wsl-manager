namespace WorldGate.Monitor.Core.ManagedImages;

/// <summary>
/// Tooltip / help text for managed image dialog fields.
/// </summary>
public static class ManagedImageFieldHelp
{
    public static IReadOnlyDictionary<string, string> All { get; } = Build();

    public static string Get(string fieldKey) =>
        All.TryGetValue(fieldKey, out string? text) ? text : string.Empty;

    private static IReadOnlyDictionary<string, string> Build() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["displayName"] =
                "Short label shown in lists and monitor badges. "
                + "Example: worldgate-linux runner.",
            ["imageReference"] =
                "Full container image name and tag used by Docker or Kubernetes. "
                + "Example: ghcr.io/djfariel/worldgate-runner:2026.09.04-2",
            ["buildContextPath"] =
                "Optional Windows path to the directory containing the Dockerfile (for local builds). "
                + "Example: E:\\Projects\\Worktrees\\WorldGate\\infra\\arc\\runner",
            ["category"] =
                "GitHub Actions runner images are tracked in the first list; other Docker or Kubernetes workloads go in the second. "
                + "Runner heuristics include ghcr.io, worldgate, actions-runner, and ARC runner images.",
            ["probeBackend"] =
                "How status is checked in WSL when the utility VM is running. "
                + "Docker uses image inspect and container state; Kubernetes counts pods using this image in the namespace below. "
                + "Remove… runs matching cleanup in WSL (stop/remove containers, docker rmi; for Kubernetes, delete matching pods in the namespace and still remove the local Docker tag if present). "
                + "Use “Keep image on disk” to drop the list entry only.",
            ["kubernetesNamespace"] =
                "Namespace passed to kubectl get pods for Kubernetes probes. "
                + "WorldGate defaults: arc-runners for runner pods, arc-systems for controller/listener.",
            ["monitorStatus"] =
                "When enabled, this image appears on Home and in the Monitor column. "
                + "Turn on for your primary runner image; leave off for secondary tags or infrastructure images.",
        };
}
