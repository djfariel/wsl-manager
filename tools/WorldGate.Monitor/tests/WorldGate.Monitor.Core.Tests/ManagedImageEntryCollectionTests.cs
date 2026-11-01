using WorldGate.Monitor.Core.ManagedImages;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class ManagedImageEntryCollectionTests
{
    [Fact]
    public void ReplaceById_updates_matching_entry_only()
    {
        var first = new ManagedImageEntry { Id = "1", DisplayName = "a", ImageReference = "a:1" };
        var second = new ManagedImageEntry { Id = "2", DisplayName = "b", ImageReference = "b:1" };
        var updated = second with { DisplayName = "b-edited" };

        IReadOnlyList<ManagedImageEntry> result = ManagedImageEntryCollection.ReplaceById([first, second], updated);

        Assert.Equal("a", result[0].DisplayName);
        Assert.Equal("b-edited", result[1].DisplayName);
    }

    [Fact]
    public void ReplaceById_toggles_monitorStatus_for_one_entry_when_refs_differ_only_by_id()
    {
        var first = new ManagedImageEntry
        {
            Id = "wg-2",
            DisplayName = "worldgate-runner:2026.09.04-2",
            ImageReference = "ghcr.io/djfariel/worldgate-runner:2026.09.04-2",
            MonitorStatus = true,
        };
        var second = new ManagedImageEntry
        {
            Id = "wg-1",
            DisplayName = "worldgate-runner:2026.09.04-1",
            ImageReference = "ghcr.io/djfariel/worldgate-runner:2026.09.04-1",
            MonitorStatus = false,
        };
        var factorio = new ManagedImageEntry
        {
            Id = "factorio",
            DisplayName = "factorio-runner:2026.09.11-1",
            ImageReference = "ghcr.io/djfariel/factorio-runner:2026.09.11-1",
            MonitorStatus = false,
        };

        IReadOnlyList<ManagedImageEntry> toggledFactorio = ManagedImageEntryCollection.ReplaceById(
            [first, second, factorio],
            factorio with { MonitorStatus = true });
        IReadOnlyList<ManagedImageEntry> toggledSecond = ManagedImageEntryCollection.ReplaceById(
            toggledFactorio,
            second with { MonitorStatus = true });

        Assert.True(toggledSecond[2].MonitorStatus);
        Assert.True(toggledSecond[1].MonitorStatus);
        Assert.True(toggledSecond[0].MonitorStatus);
    }

    [Fact]
    public void GetMonitoredEntries_returns_every_monitor_flag_unique_by_id()
    {
        var first = new ManagedImageEntry
        {
            Id = "wg-2",
            DisplayName = "worldgate-runner:2026.09.04-2",
            ImageReference = "ghcr.io/djfariel/worldgate-runner:2026.09.04-2",
            MonitorStatus = true,
        };
        var second = new ManagedImageEntry
        {
            Id = "wg-1",
            DisplayName = "worldgate-runner:2026.09.04-1",
            ImageReference = "ghcr.io/djfariel/worldgate-runner:2026.09.04-1",
            MonitorStatus = true,
        };
        var factorio = new ManagedImageEntry
        {
            Id = "factorio",
            DisplayName = "factorio-runner:2026.09.11-1",
            ImageReference = "ghcr.io/djfariel/factorio-runner:2026.09.11-1",
            MonitorStatus = true,
        };
        var idle = new ManagedImageEntry
        {
            Id = "idle",
            DisplayName = "idle",
            ImageReference = "ghcr.io/example/idle:1",
            MonitorStatus = false,
        };
        var duplicateId = first with { DisplayName = "duplicate id row" };

        IReadOnlyList<ManagedImageEntry> monitored = ManagedImageEntryCollection.GetMonitoredEntries(
            [first, second, factorio, idle, duplicateId]);

        Assert.Equal(3, monitored.Count);
        Assert.Equal(["wg-2", "wg-1", "factorio"], monitored.Select(entry => entry.Id));
    }

    [Fact]
    public void FindById_returns_entry_or_null()
    {
        var entry = new ManagedImageEntry { Id = "abc", DisplayName = "x", ImageReference = "x:1" };
        Assert.Same(entry, ManagedImageEntryCollection.FindById([entry], "abc"));
        Assert.Null(ManagedImageEntryCollection.FindById([entry], "missing"));
        Assert.Null(ManagedImageEntryCollection.FindById([entry], ""));
        Assert.Null(ManagedImageEntryCollection.FindById([entry], "   "));
    }
}
