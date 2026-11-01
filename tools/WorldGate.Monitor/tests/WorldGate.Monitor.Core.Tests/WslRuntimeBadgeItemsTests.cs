using WorldGate.Monitor.Core.WslRuntime;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class WslRuntimeBadgeItemsTests
{
    [Fact]
    public void FromSnapshot_single_badge_when_not_multi_distro()
    {
        WslRuntimeSnapshot snapshot = WslRuntimeSnapshot.Running(["Ubuntu"]);

        IReadOnlyList<WslRuntimeBadgeItem> items = WslRuntimeBadgeItems.FromSnapshot(snapshot);

        Assert.Single(items);
        Assert.Equal(WslRuntimeBadgeItems.SingleBadgeKey, items[0].Key);
        Assert.Equal("WSL runtime", items[0].Caption);
    }

    [Fact]
    public void FromSnapshot_one_badge_per_distro_when_multiple_running()
    {
        WslRuntimeSnapshot snapshot = WslRuntimeSnapshot.Running(["Ubuntu", "Debian"]);

        IReadOnlyList<WslRuntimeBadgeItem> items = WslRuntimeBadgeItems.FromSnapshot(snapshot);

        Assert.Equal(2, items.Count);
        Assert.Equal(WslRuntimeBadgeItems.DistroBadgeKey("Ubuntu"), items[0].Key);
        Assert.Equal(WslRuntimeBadgeItems.DistroBadgeKey("Debian"), items[1].Key);
        Assert.Equal(["Ubuntu"], items[0].Snapshot.RunningDistributions);
        Assert.Equal(["Debian"], items[1].Snapshot.RunningDistributions);
    }
}
