using WorldGate.Monitor.Core.ManagedImages;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class ManagedImageSeedMergerTests
{
    [Fact]
    public void Merge_skips_duplicate_image_references()
    {
        var existing = new[]
        {
            new ManagedImageEntry
            {
                Id = "1",
                DisplayName = "existing",
                ImageReference = "ghcr.io/org/app:1",
            },
        };

        var discovered = new[]
        {
            new ManagedImageDiscoveryCandidate("ghcr.io/org/app:1", ManagedImageProbeBackend.Docker, null, null, false),
            new ManagedImageDiscoveryCandidate("ghcr.io/org/app:2", ManagedImageProbeBackend.Docker, null, null, false),
        };

        IReadOnlyList<ManagedImageEntry> merged = ManagedImageSeedMerger.Merge(existing, discovered);
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void Merge_enables_monitor_for_primary_worldgate_runner_when_none_set()
    {
        var discovered = new[]
        {
            new ManagedImageDiscoveryCandidate(
                "ghcr.io/djfariel/worldgate-runner:2026.09.04-1",
                ManagedImageProbeBackend.Docker,
                null,
                null,
                false),
            new ManagedImageDiscoveryCandidate(
                "ghcr.io/djfariel/worldgate-runner:2026.09.04-2",
                ManagedImageProbeBackend.Docker,
                null,
                null,
                false),
        };

        IReadOnlyList<ManagedImageEntry> merged = ManagedImageSeedMerger.Merge([], discovered, "worldgate-linux");
        ManagedImageEntry monitored = Assert.Single(merged, entry => entry.MonitorStatus);
        Assert.Equal("ghcr.io/djfariel/worldgate-runner:2026.09.04-2", monitored.ImageReference);
    }

    [Fact]
    public void Classify_marks_arc_controller_as_other_via_merger()
    {
        var discovered = new[]
        {
            new ManagedImageDiscoveryCandidate(
                "ghcr.io/actions/gha-runner-scale-set-controller:0.14.2",
                ManagedImageProbeBackend.Kubernetes,
                "arc-systems",
                null,
                false),
        };

        IReadOnlyList<ManagedImageEntry> merged = ManagedImageSeedMerger.Merge([], discovered);
        Assert.Equal(ManagedImageCategory.OtherWorkload, merged[0].Category);
    }
}
