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
    public void FindById_returns_entry_or_null()
    {
        var entry = new ManagedImageEntry { Id = "abc", DisplayName = "x", ImageReference = "x:1" };
        Assert.Same(entry, ManagedImageEntryCollection.FindById([entry], "abc"));
        Assert.Null(ManagedImageEntryCollection.FindById([entry], "missing"));
    }
}
