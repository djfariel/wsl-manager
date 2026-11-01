using WorldGate.Monitor.Core;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class ArcObservationParserTests
{
    [Fact]
    public void ListenerInControllerNamespaceIsReportedReady()
    {
        const string controllerJson = """
        {
          "items": [
            {
              "metadata":{"name":"arc-gha-rs-controller-1"},
              "spec":{"containers":[{"name":"manager"}]},
              "status":{"phase":"Running","conditions":[{"type":"Ready","status":"True"}]}
            },
            {
              "metadata":{"name":"worldgate-linux-abc-listener","labels":{"actions.github.com/scale-set-name":"worldgate-linux"}},
              "spec":{"containers":[{"name":"listener"}]},
              "status":{"phase":"Running","conditions":[{"type":"Ready","status":"True"}]}
            }
          ]
        }
        """;
        const string runnerJson = """
        {"items":[]}
        """;

        Assert.True(
            ArcObservationParser.TryParse(
                controllerJson,
                runnerJson,
                "worldgate-linux",
                out ArcObservation observation,
                out string? error),
            error);

        Assert.True(observation.ControllerReady);
        Assert.True(observation.ListenerReady);
        Assert.Empty(observation.RunnerPods);
    }

    [Fact]
    public void ReadyListenerDoesNotMaskUnreadyController()
    {
        const string controllerJson = """
        {
          "items": [
            {
              "metadata":{"name":"arc-gha-rs-controller-1"},
              "spec":{"containers":[{"name":"manager"}]},
              "status":{"phase":"Running","conditions":[{"type":"Ready","status":"False"}]}
            },
            {
              "metadata":{"name":"worldgate-linux-abc-listener","labels":{"actions.github.com/scale-set-name":"worldgate-linux"}},
              "spec":{"containers":[{"name":"listener"}]},
              "status":{"phase":"Running","conditions":[{"type":"Ready","status":"True"}]}
            }
          ]
        }
        """;
        const string runnerJson = """
        {"items":[]}
        """;

        Assert.True(
            ArcObservationParser.TryParse(
                controllerJson,
                runnerJson,
                "worldgate-linux",
                out ArcObservation observation,
                out string? error),
            error);

        Assert.False(observation.ControllerReady);
        Assert.True(observation.ListenerReady);
    }

    [Fact]
    public void MissingPodMetadataBecomesStructuredProviderFailure()
    {
        const string controllerJson = """
        {"items":[{}]}
        """;
        const string runnerJson = """
        {"items":[]}
        """;

        bool parsed = ArcObservationParser.TryParse(
            controllerJson,
            runnerJson,
            "worldgate-linux",
            out ArcObservation observation,
            out string? error);

        Assert.False(parsed);
        Assert.False(observation.Available);
        Assert.Contains("Unable to parse ARC pod state", error);
    }

    [Fact]
    public void ScaleSetLabelOverridesNameFallbackWhenLabelNamesAnotherScaleSet()
    {
        const string controllerJson = """
        {"items":[{"metadata":{"name":"controller"},"status":{"conditions":[{"type":"Ready","status":"True"}]}}]}
        """;
        const string runnerJson = """
        {
          "items": [
            {
              "metadata":{"name":"worldgate-linux-listener","labels":{"actions.github.com/scale-set-name":"worldgate-linux"}},
              "spec":{"containers":[{"name":"listener"}]},
              "status":{"phase":"Running","conditions":[{"type":"Ready","status":"True"}]}
            },
            {
              "metadata":{"name":"worldgate-linux-runner-from-other-set","labels":{"actions.github.com/scale-set-name":"other-scale-set"}},
              "spec":{"containers":[{"name":"runner"}]},
              "status":{"phase":"Running","conditions":[{"type":"Ready","status":"True"}]}
            }
          ]
        }
        """;

        Assert.True(
            ArcObservationParser.TryParse(
                controllerJson,
                runnerJson,
                "worldgate-linux",
                out ArcObservation observation,
                out string? error),
            error);

        Assert.True(observation.ListenerReady);
        Assert.Equal(0, observation.ActiveRunnerCount);
        Assert.Null(observation.RunnerPodName);
    }
}
