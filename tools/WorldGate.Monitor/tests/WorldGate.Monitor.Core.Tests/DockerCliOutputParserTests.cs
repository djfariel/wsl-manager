using WorldGate.Monitor.Core.ManagedImages;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class DockerCliOutputParserTests
{
    [Theory]
    [InlineData("sha256:abc", 0, ManagedImagePresence.Present)]
    [InlineData("  sha256:abc  \n", 0, ManagedImagePresence.Present)]
    [InlineData("", 1, ManagedImagePresence.Absent)]
    [InlineData(null, 1, ManagedImagePresence.Absent)]
    public void ParseImageInspect_respects_exit_code_and_stdout(string? stdout, int exitCode, ManagedImagePresence expected)
    {
        Assert.Equal(expected, DockerCliOutputParser.ParseImageInspect(stdout, exitCode));
    }

    [Fact]
    public void ParseContainerStates_counts_running_and_total()
    {
        const string stdout = """
            running
            exited
            running

            """;

        (int running, int total) = DockerCliOutputParser.ParseContainerStates(stdout);
        Assert.Equal(2, running);
        Assert.Equal(3, total);
    }

    [Theory]
    [InlineData(0, 0, ManagedImageRuntimeState.Stopped)]
    [InlineData(1, 1, ManagedImageRuntimeState.Running)]
    [InlineData(1, 2, ManagedImageRuntimeState.Partial)]
    [InlineData(0, 2, ManagedImageRuntimeState.Stopped)]
    public void ResolveRuntime_maps_counts(int running, int total, ManagedImageRuntimeState expected)
    {
        Assert.Equal(expected, DockerCliOutputParser.ResolveRuntime(running, total));
    }

    [Fact]
    public void FormatSummary_describes_absent_image()
    {
        string summary = DockerCliOutputParser.FormatSummary(
            ManagedImagePresence.Absent,
            ManagedImageRuntimeState.Stopped,
            0,
            0);
        Assert.Equal("Image not installed", summary);
    }

    [Theory]
    [InlineData("ghcr.io/djfariel/worldgate-runner:2026.09.04-2", "ghcr.io/djfariel/worldgate-runner:2026.09.04-2", true)]
    [InlineData(
        "ghcr.io/djfariel/worldgate-runner:2026.09.04-2@sha256:deadbeef",
        "ghcr.io/djfariel/worldgate-runner:2026.09.04-2",
        true)]
    [InlineData("ghcr.io/other/app:1", "ghcr.io/djfariel/worldgate-runner:2026.09.04-2", false)]
    public void ImageReferenceMatches_compares_tag_and_digest_suffix(string containerImage, string reference, bool expected)
    {
        Assert.Equal(expected, DockerCliOutputParser.ImageReferenceMatches(containerImage, reference));
    }

    [Fact]
    public void ParseContainerStatesForImageReference_filters_by_image_and_counts_states()
    {
        const string reference = "ghcr.io/djfariel/worldgate-runner:2026.09.04-2";
        const string stdout = """
            ghcr.io/djfariel/worldgate-runner:2026.09.04-2|running
            ghcr.io/djfariel/worldgate-runner:2026.09.04-2@sha256:abc|exited
            ghcr.io/other/workload:1|running
            malformed-line-without-separator
            """;

        (int running, int total) = DockerCliOutputParser.ParseContainerStatesForImageReference(stdout, reference);
        Assert.Equal(1, running);
        Assert.Equal(2, total);
    }

    [Fact]
    public void ListAllContainersImageAndStateArgs_avoids_reference_filter()
    {
        string joined = string.Join(' ', DockerCliOutputParser.ListAllContainersImageAndStateArgs);
        Assert.Contains("docker ps -a", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("reference=", joined, StringComparison.Ordinal);
        Assert.Contains("|", joined, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseContainerStatesForImageReference_handles_empty_ps_stdout()
    {
        const string reference = "ghcr.io/djfariel/worldgate-runner:2026.09.04-2";
        (int running, int total) = DockerCliOutputParser.ParseContainerStatesForImageReference(string.Empty, reference);
        Assert.Equal(0, running);
        Assert.Equal(0, total);
    }
}
