namespace WorldGate.Monitor.Core.ManagedImages;

/// <summary>
/// Builds WSL command plans and user-facing text for removing a managed image entry.
/// </summary>
public static class ManagedImageRemoveHelper
{
    public static string DescribeRemovePlan(ManagedImageEntry entry, bool removeFromListOnly)
    {
        if (removeFromListOnly)
        {
            return
                $"Remove “{entry.DisplayName}” from the managed list only.\n"
                + "No containers, pods, or local Docker images will be changed.";
        }

        string imageRef = entry.ImageReference.Trim();
        return entry.ProbeBackend switch
        {
            ManagedImageProbeBackend.Kubernetes => DescribeKubernetesCleanupPlan(entry, imageRef),
            _ => DescribeDockerCleanupPlan(imageRef),
        };
    }

    public static IReadOnlyList<ManagedImageRemoveCommandStep> BuildDiscoverySteps(
        ManagedImageEntry entry,
        bool removeFromListOnly)
    {
        if (removeFromListOnly)
        {
            return [];
        }

        var steps = new List<ManagedImageRemoveCommandStep>();
        if (entry.ProbeBackend == ManagedImageProbeBackend.Kubernetes)
        {
            string ns = entry.KubernetesNamespace!.Trim();
            steps.Add(new ManagedImageRemoveCommandStep(
                "List pods in namespace",
                ["kubectl", "get", "pods", "-n", ns, "-o", "json"]));
        }

        steps.Add(new ManagedImageRemoveCommandStep(
            "List Docker containers",
            DockerCliOutputParser.ListAllContainersIdAndImageArgs));
        return steps;
    }

    /// <summary>
    /// Builds cleanup commands after discovery output is available. Pass null discovery payloads when nothing was run.
    /// </summary>
    public static IReadOnlyList<ManagedImageRemoveCommandStep> BuildCleanupSteps(
        ManagedImageEntry entry,
        bool removeFromListOnly,
        string? kubernetesPodsJson,
        string? dockerPsStdout)
    {
        if (removeFromListOnly)
        {
            return [];
        }

        var steps = new List<ManagedImageRemoveCommandStep>();
        string imageRef = entry.ImageReference.Trim();

        if (entry.ProbeBackend == ManagedImageProbeBackend.Kubernetes
            && !string.IsNullOrWhiteSpace(kubernetesPodsJson))
        {
            string ns = entry.KubernetesNamespace!.Trim();
            IReadOnlyList<string> podNames = KubernetesPodImageParser.ListPodNamesUsingImage(
                kubernetesPodsJson,
                imageRef);
            foreach (string podName in podNames)
            {
                steps.Add(new ManagedImageRemoveCommandStep(
                    $"Delete pod {podName}",
                    ["kubectl", "delete", "pod", podName, "-n", ns, "--wait=false"],
                    ContinueOnFailure: true));
            }
        }

        AppendDockerTeardownSteps(steps, imageRef, dockerPsStdout);
        return steps;
    }

    private static void AppendDockerTeardownSteps(
        List<ManagedImageRemoveCommandStep> steps,
        string imageRef,
        string? dockerPsStdout)
    {
        IReadOnlyList<string> containerIds = DockerCliOutputParser.ParseContainerIdsForImageReference(
            dockerPsStdout,
            imageRef);
        if (containerIds.Count > 0)
        {
            var stopArgs = new List<string> { "docker", "stop" };
            stopArgs.AddRange(containerIds);
            steps.Add(new ManagedImageRemoveCommandStep(
                "Stop containers using image",
                stopArgs,
                ContinueOnFailure: true));

            var rmArgs = new List<string> { "docker", "rm" };
            rmArgs.AddRange(containerIds);
            steps.Add(new ManagedImageRemoveCommandStep(
                "Remove containers using image",
                rmArgs,
                ContinueOnFailure: true));
        }

        steps.Add(new ManagedImageRemoveCommandStep(
            "Remove local Docker image",
            ["docker", "rmi", imageRef],
            ContinueOnFailure: true));

        steps.Add(new ManagedImageRemoveCommandStep(
            "Force-remove local Docker image (if still present)",
            ["docker", "rmi", "-f", imageRef],
            ContinueOnFailure: true,
            Optional: true));
    }

    private static string DescribeDockerCleanupPlan(string imageRef) =>
        "This will remove the entry from your managed list and clean up in WSL:\n"
        + $"  • Stop and remove Docker containers using {imageRef}\n"
        + $"  • Run docker rmi for {imageRef} (force if needed)\n"
        + "Requires WSL to be running.";

    private static string DescribeKubernetesCleanupPlan(ManagedImageEntry entry, string imageRef)
    {
        string ns = entry.KubernetesNamespace?.Trim() ?? "(namespace not set)";
        return
            "This will remove the entry from your managed list and run conservative cleanup in WSL:\n"
            + $"  • Delete pods in namespace “{ns}” whose containers use {imageRef}\n"
            + "    (Deployments, ReplicaSets, and Helm releases are not deleted; controllers may recreate pods.)\n"
            + $"  • Stop/remove local Docker containers and run docker rmi for {imageRef} if present on disk\n"
            + "Does not delete the namespace, cluster workloads you did not list here, or remote registry tags.\n"
            + "Requires WSL to be running.";
    }
}

public sealed record ManagedImageRemoveCommandStep(
    string Label,
    IReadOnlyList<string> Arguments,
    bool ContinueOnFailure = false,
    bool Optional = false);
