using WorldGate.Monitor.Core.ManagedImages;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class KubernetesPodImageParserTests
{
    [Fact]
    public void TrySummarizePodsUsingImage_counts_running_pods()
    {
        const string json = """
        {
          "items": [
            {
              "spec": { "containers": [ { "image": "ghcr.io/djfariel/worldgate-runner:1.2.3" } ] },
              "status": { "phase": "Running" }
            },
            {
              "spec": { "containers": [ { "image": "ghcr.io/djfariel/worldgate-runner:1.2.3" } ] },
              "status": { "phase": "Pending" }
            },
            {
              "spec": { "containers": [ { "image": "docker.io/library/nginx:latest" } ] },
              "status": { "phase": "Running" }
            }
          ]
        }
        """;

        Assert.True(
            KubernetesPodImageParser.TrySummarizePodsUsingImage(
                json,
                "ghcr.io/djfariel/worldgate-runner:1.2.3",
                out int running,
                out int total,
                out string? error),
            error);

        Assert.Equal(1, running);
        Assert.Equal(2, total);
    }
}
