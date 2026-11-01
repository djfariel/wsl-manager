using WorldGate.Monitor.Core.WslRuntime;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class WslRuntimeProbeTests
{
    [Fact]
    public void DetectReturnsStoppedWhenNoVmProcesses()
    {
        WslRuntimeSnapshot snapshot = WslRuntimeProbe.Detect(() => []);

        Assert.Equal(WslRuntimeState.Stopped, snapshot.State);
        Assert.Null(snapshot.ErrorMessage);
    }

    [Fact]
    public void DetectReturnsRunningWhenVmmemWslPresent()
    {
        WslRuntimeSnapshot snapshot = WslRuntimeProbe.Detect(() => ["wslhost", "vmmemWSL"]);

        Assert.Equal(WslRuntimeState.Running, snapshot.State);
    }

    [Fact]
    public void ParseRunningDistributionListSkipsHeaderAndReadsNames()
    {
        const string stdout = """
            Windows Subsystem for Linux Distributions:
            Ubuntu (Default)
            Debian
            """;

        IReadOnlyList<string> names = WslRuntimeProbe.ParseRunningDistributionList(stdout);

        Assert.Equal(2, names.Count);
        Assert.Equal("Ubuntu", names[0]);
        Assert.Equal("Debian", names[1]);
    }

    [Fact]
    public void ParseRunningDistributionList_matchesActualWindowsRunningOutput()
    {
        const string stdout = "Windows Subsystem for Linux Distributions:\r\nUbuntu (Default)\r\n";

        IReadOnlyList<string> names = WslRuntimeProbe.ParseRunningDistributionList(stdout);

        Assert.Single(names);
        Assert.Equal("Ubuntu", names[0]);
    }

    [Fact]
    public void ParseRunningDistributionList_ignoresBlankLinesAndDefaultSuffixOnly()
    {
        const string stdout = """
            Windows Subsystem for Linux Distributions:

            Ubuntu (Default)

            """;

        IReadOnlyList<string> names = WslRuntimeProbe.ParseRunningDistributionList(stdout);

        Assert.Single(names);
        Assert.Equal("Ubuntu", names[0]);
    }

    [Fact]
    public void ParseRunningDistributionListReturnsEmptyForBlankOutput()
    {
        Assert.Empty(WslRuntimeProbe.ParseRunningDistributionList(null));
        Assert.Empty(WslRuntimeProbe.ParseRunningDistributionList("   "));
    }

    [Fact]
    public void WithRunningDistrosEnrichesRunningSnapshot()
    {
        WslRuntimeSnapshot baseSnapshot = WslRuntimeSnapshot.Running(null);
        WslRuntimeSnapshot enriched = WslRuntimeProbe.WithRunningDistros(
            baseSnapshot,
            "Ubuntu (Default)\r\n");

        Assert.Equal(WslRuntimeState.Running, enriched.State);
        Assert.Single(enriched.RunningDistributions!);
        Assert.Equal("Ubuntu", enriched.RunningDistributions![0]);
    }
}
