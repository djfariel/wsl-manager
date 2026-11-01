using WorldGate.Monitor.Core.ManagedImages;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class ManagedImageBuildContextResolverTests
{
    [Fact]
    public void ResolveForImageReference_returns_null_for_unrelated_image()
    {
        Assert.Null(ManagedImageBuildContextResolver.ResolveForImageReference("hello-world:latest"));
    }

    [Fact]
    public void ApplyDiscoveredBuildContext_keeps_existing_path()
    {
        var entry = new ManagedImageEntry
        {
            Id = "a",
            DisplayName = "test",
            ImageReference = "ghcr.io/djfariel/factorio-runner:1",
            BuildContextPath = @"C:\existing",
        };

        ManagedImageEntry result = ManagedImageBuildContextResolver.ApplyDiscoveredBuildContext(entry);
        Assert.Equal(@"C:\existing", result.BuildContextPath);
    }

    [Fact]
    public void ApplyDiscoveredBuildContext_leaves_null_when_no_directory_on_disk()
    {
        var entry = new ManagedImageEntry
        {
            Id = "a",
            DisplayName = "test",
            ImageReference = "ghcr.io/djfariel/factorio-runner:2026.09.11-1",
        };

        ManagedImageEntry result = ManagedImageBuildContextResolver.ApplyDiscoveredBuildContext(entry);
        Assert.Null(result.BuildContextPath);
    }
}
