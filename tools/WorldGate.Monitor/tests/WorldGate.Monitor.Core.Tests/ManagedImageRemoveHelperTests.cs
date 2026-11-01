using WorldGate.Monitor.Core.ManagedImages;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class ManagedImageRemoveHelperTests
{
    [Fact]
    public void DescribeRemovePlan_listOnly_mentions_no_system_changes()
    {
        var entry = SampleDockerEntry();
        string plan = ManagedImageRemoveHelper.DescribeRemovePlan(entry, removeFromListOnly: true);
        Assert.Contains("from the managed list only", plan, StringComparison.Ordinal);
        Assert.Contains("will be changed", plan, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeRemovePlan_docker_mentions_stop_rmi_and_wsl()
    {
        var entry = SampleDockerEntry();
        string plan = ManagedImageRemoveHelper.DescribeRemovePlan(entry, removeFromListOnly: false);
        Assert.Contains("docker rmi", plan, StringComparison.Ordinal);
        Assert.Contains("Stop and remove Docker containers", plan, StringComparison.Ordinal);
        Assert.Contains("WSL", plan, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeRemovePlan_kubernetes_clarifies_no_deployment_delete()
    {
        var entry = SampleDockerEntry() with
        {
            ProbeBackend = ManagedImageProbeBackend.Kubernetes,
            KubernetesNamespace = "arc-systems",
        };
        string plan = ManagedImageRemoveHelper.DescribeRemovePlan(entry, removeFromListOnly: false);
        Assert.Contains("Deployments", plan, StringComparison.Ordinal);
        Assert.Contains("arc-systems", plan, StringComparison.Ordinal);
        Assert.Contains("docker rmi", plan, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDiscoverySteps_docker_lists_containers_only()
    {
        var entry = SampleDockerEntry();
        IReadOnlyList<ManagedImageRemoveCommandStep> steps =
            ManagedImageRemoveHelper.BuildDiscoverySteps(entry, removeFromListOnly: false);
        Assert.Single(steps);
        Assert.Equal(DockerCliOutputParser.ListAllContainersIdAndImageArgs, steps[0].Arguments);
    }

    [Fact]
    public void BuildDiscoverySteps_kubernetes_includes_kubectl_and_docker_ps()
    {
        var entry = SampleDockerEntry() with
        {
            ProbeBackend = ManagedImageProbeBackend.Kubernetes,
            KubernetesNamespace = "arc-runners",
        };
        IReadOnlyList<ManagedImageRemoveCommandStep> steps =
            ManagedImageRemoveHelper.BuildDiscoverySteps(entry, removeFromListOnly: false);
        Assert.Equal(2, steps.Count);
        Assert.Equal("kubectl", steps[0].Arguments[0]);
        Assert.Equal(DockerCliOutputParser.ListAllContainersIdAndImageArgs, steps[1].Arguments);
    }

    [Fact]
    public void BuildCleanupSteps_docker_stops_matching_containers_then_rmi()
    {
        var entry = SampleDockerEntry();
        const string reference = "ghcr.io/djfariel/worldgate-runner:2026.09.04-2";
        const string psStdout = """
            abc123|ghcr.io/djfariel/worldgate-runner:2026.09.04-2
            def456|ghcr.io/other:1
            """;

        IReadOnlyList<ManagedImageRemoveCommandStep> steps = ManagedImageRemoveHelper.BuildCleanupSteps(
            entry,
            removeFromListOnly: false,
            kubernetesPodsJson: null,
            dockerPsStdout: psStdout);

        Assert.Contains(steps, step => step.Label == "Stop containers using image" && step.Arguments.Contains("abc123"));
        Assert.Contains(steps, step => step.Label == "Remove containers using image" && step.Arguments.Contains("abc123"));
        Assert.DoesNotContain(steps, step => step.Arguments.Contains("def456"));
        Assert.Contains(steps, step => step.Label == "Remove local Docker image" && step.Arguments[^1] == reference);
    }

    [Fact]
    public void ParseContainerIdsForImageReference_returns_ids_for_matching_images()
    {
        const string reference = "ghcr.io/djfariel/worldgate-runner:2026.09.04-2";
        const string stdout = """
            abc123|ghcr.io/djfariel/worldgate-runner:2026.09.04-2
            def456|ghcr.io/djfariel/worldgate-runner:2026.09.04-2@sha256:abc
            """;

        IReadOnlyList<string> ids = DockerCliOutputParser.ParseContainerIdsForImageReference(stdout, reference);
        Assert.Equal(["abc123", "def456"], ids);
    }

    private static ManagedImageEntry SampleDockerEntry() =>
        new()
        {
            DisplayName = "worldgate runner",
            ImageReference = "ghcr.io/djfariel/worldgate-runner:2026.09.04-2",
            ProbeBackend = ManagedImageProbeBackend.Docker,
        };
}
