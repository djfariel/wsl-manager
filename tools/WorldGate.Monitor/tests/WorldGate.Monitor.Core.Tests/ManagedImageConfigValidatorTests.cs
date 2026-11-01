using WorldGate.Monitor.Core.ManagedImages;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class ManagedImageConfigValidatorTests
{
    [Fact]
    public void ValidateCollection_rejects_duplicate_ids()
    {
        var entries = new[]
        {
            new ManagedImageEntry { Id = "a", DisplayName = "One", ImageReference = "img:1" },
            new ManagedImageEntry { Id = "a", DisplayName = "Two", ImageReference = "img:2" },
        };

        string? error = ManagedImageConfigValidator.ValidateCollection(entries);
        Assert.Contains("duplicate", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateEntry_requires_kubernetes_namespace_for_k8s_backend()
    {
        var entry = new ManagedImageEntry
        {
            Id = "x",
            DisplayName = "Pod",
            ImageReference = "ghcr.io/org/app:1",
            ProbeBackend = ManagedImageProbeBackend.Kubernetes,
        };

        Assert.NotNull(ManagedImageConfigValidator.ValidateEntry(entry));
    }
}
