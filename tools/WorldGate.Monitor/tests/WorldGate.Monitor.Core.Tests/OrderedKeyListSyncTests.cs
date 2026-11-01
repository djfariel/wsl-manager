using WorldGate.Monitor.Core.UiSync;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class OrderedKeyListSyncTests
{
    [Fact]
    public void Compare_same_keys_same_order_is_unchanged()
    {
        OrderedKeyListSync.Plan plan = OrderedKeyListSync.Compare(
            ["a", "b", "c"],
            ["a", "b", "c"]);

        Assert.True(plan.KeysAndOrderUnchanged);
        Assert.Empty(plan.AddedKeys);
        Assert.Empty(plan.RemovedKeys);
    }

    [Fact]
    public void Compare_same_keys_different_order_is_not_unchanged()
    {
        OrderedKeyListSync.Plan plan = OrderedKeyListSync.Compare(
            ["b", "a"],
            ["a", "b"]);

        Assert.False(plan.KeysAndOrderUnchanged);
        Assert.Empty(plan.AddedKeys);
        Assert.Empty(plan.RemovedKeys);
    }

    [Fact]
    public void Compare_reports_added_and_removed_keys()
    {
        OrderedKeyListSync.Plan plan = OrderedKeyListSync.Compare(
            ["b", "c"],
            ["a", "b"]);

        Assert.False(plan.KeysAndOrderUnchanged);
        Assert.Equal(["c"], plan.AddedKeys);
        Assert.Equal(["a"], plan.RemovedKeys);
    }
}
