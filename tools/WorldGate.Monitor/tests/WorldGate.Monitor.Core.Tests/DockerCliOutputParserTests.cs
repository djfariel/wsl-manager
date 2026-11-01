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
}
