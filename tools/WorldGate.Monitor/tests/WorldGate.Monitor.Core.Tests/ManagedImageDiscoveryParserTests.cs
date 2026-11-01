using WorldGate.Monitor.Core.ManagedImages;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class ManagedImageDiscoveryParserTests
{
    [Fact]
    public void DockerImageListParser_parses_repository_tags()
    {
        const string stdout = """
            ghcr.io/djfariel/worldgate-runner:2026.09.04-2
            hello-world:latest

            """;

        IReadOnlyList<string> images = DockerImageListParser.ParseRepositoryTags(stdout);
        Assert.Equal(2, images.Count);
        Assert.Contains("ghcr.io/djfariel/worldgate-runner:2026.09.04-2", images);
    }

    [Fact]
    public void CategoryHeuristics_classifies_runner_images()
    {
        Assert.Equal(
            ManagedImageCategory.GitHubRunner,
            ManagedImageCategoryHeuristics.Classify("ghcr.io/djfariel/worldgate-runner:1"));
        Assert.Equal(
            ManagedImageCategory.OtherWorkload,
            ManagedImageCategoryHeuristics.Classify("hello-world:latest"));
    }

    [Fact]
    public void KubectlPodImageDiscovery_reads_container_images()
    {
        const string json = """
            {
              "items": [
                {
                  "metadata": { "namespace": "arc-systems" },
                  "spec": { "containers": [ { "image": "ghcr.io/actions/gha-runner-scale-set-controller:0.14.2" } ] }
                }
              ]
            }
            """;

        IReadOnlyList<DiscoveredPodImage> images = KubectlPodImageDiscovery.ParsePodsJson(json);
        Assert.Single(images);
        Assert.Equal("arc-systems", images[0].Namespace);
    }
}
