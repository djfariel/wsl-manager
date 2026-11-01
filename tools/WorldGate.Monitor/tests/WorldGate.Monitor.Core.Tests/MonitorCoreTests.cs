using WorldGate.Monitor.Core;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class MonitorCoreTests
{
    [Theory]
    [InlineData(MonitorNotificationKind.Started, MonitorNotificationSeverity.Information)]
    [InlineData(MonitorNotificationKind.Passed, MonitorNotificationSeverity.Information)]
    [InlineData(MonitorNotificationKind.RunnerDelayed, MonitorNotificationSeverity.Warning)]
    [InlineData(MonitorNotificationKind.Failed, MonitorNotificationSeverity.Error)]
    [InlineData(MonitorNotificationKind.Cancelled, MonitorNotificationSeverity.Error)]
    public void NotificationPresentationMapsKindToNativeTraySeverity(
        MonitorNotificationKind kind,
        MonitorNotificationSeverity expectedSeverity)
    {
        var notification = new MonitorNotification(kind, "title", "message", null);

        NotificationPresentation presentation = NotificationPresentation.From(notification);

        Assert.Equal(expectedSeverity, presentation.Severity);
        Assert.Equal("title", presentation.Title);
        Assert.Equal("message", presentation.Message);
        Assert.Equal(5000, presentation.DurationMilliseconds);
    }

    [Fact]
    public void GitHubParserFiltersRunnerLabelsAndKeepsPrMetadata()
    {
        const string runsJson = """
        {
          "workflow_runs": [
            {
              "id": 100,
              "name": "Verify",
              "html_url": "https://github.com/djfariel/WorldGate/actions/runs/100",
              "head_branch": "feature/test",
              "created_at": "2026-09-04T20:00:00Z",
              "pull_requests": [{ "number": 16 }]
            }
          ]
        }
        """;
        const string jobsJson = """
        {
          "jobs": [
            {
              "id": 200,
              "name": "Verify / Integration Tests",
              "status": "in_progress",
              "conclusion": null,
              "created_at": "2026-09-04T20:00:01Z",
              "started_at": "2026-09-04T20:00:10Z",
              "completed_at": null,
              "labels": ["self-hosted", "worldgate-linux"]
            },
            {
              "id": 201,
              "name": "Hosted helper",
              "status": "in_progress",
              "conclusion": null,
              "created_at": "2026-09-04T20:00:01Z",
              "started_at": "2026-09-04T20:00:10Z",
              "completed_at": null,
              "labels": ["ubuntu-latest"]
            }
          ]
        }
        """;

        Assert.True(GitHubObservationParser.TryParseRuns(runsJson, out IReadOnlyList<WorkflowRunSummary> runs, out string? runError), runError);
        WorkflowRunSummary run = Assert.Single(runs);
        Assert.True(GitHubObservationParser.TryParseJobs(jobsJson, run, "worldgate-linux", out IReadOnlyList<GitHubJobObservation> jobs, out string? jobError), jobError);

        GitHubJobObservation job = Assert.Single(jobs);
        Assert.Equal(200, job.JobId);
        Assert.Equal(16, job.PullRequestNumber);
        Assert.Equal("Verify", job.RunName);
        Assert.Equal("Verify / Integration Tests", job.JobName);
        Assert.True(job.IsRunning);
    }

    [Fact]
    public void ArcParserUsesScaleSetLabelsAndReportsOneRunner()
    {
        const string controllerJson = """
        {"items":[{"metadata":{"name":"gha-runner-scale-set-controller-1"},"status":{"phase":"Running","conditions":[{"type":"Ready","status":"True"}]}}]}
        """;
        const string runnerJson = """
        {
          "items": [
            {
              "metadata":{"name":"worldgate-linux-abc-listener","labels":{"actions.github.com/scale-set-name":"worldgate-linux"}},
              "spec":{"containers":[{"name":"listener"}]},
              "status":{"phase":"Running","conditions":[{"type":"Ready","status":"True"}]}
            },
            {
              "metadata":{"name":"worldgate-linux-x7c2p","labels":{"actions.github.com/scale-set-name":"worldgate-linux"}},
              "spec":{"containers":[{"name":"runner"}]},
              "status":{"phase":"Running","conditions":[{"type":"Ready","status":"True"}]}
            }
          ]
        }
        """;

        Assert.True(ArcObservationParser.TryParse(controllerJson, runnerJson, "worldgate-linux", out ArcObservation arc, out string? error), error);
        Assert.True(arc.ControllerReady);
        Assert.True(arc.ListenerReady);
        Assert.Equal(1, arc.ActiveRunnerCount);
        Assert.Equal("worldgate-linux-x7c2p", arc.RunnerPodName);
    }

    [Fact]
    public void ArcParserFallsBackToNamesButDoesNotGuessAmongMultipleRunners()
    {
        const string controllerJson = """
        {"items":[{"metadata":{"name":"controller"},"status":{"conditions":[{"type":"Ready","status":"True"}]}}]}
        """;
        const string runnerJson = """
        {
          "items": [
            {"metadata":{"name":"worldgate-linux-listener"},"spec":{"containers":[{"name":"listener"}]},"status":{"phase":"Running","conditions":[{"type":"Ready","status":"True"}]}},
            {"metadata":{"name":"worldgate-linux-runner-a"},"spec":{"containers":[{"name":"runner"}]},"status":{"phase":"Running","conditions":[{"type":"Ready","status":"True"}]}},
            {"metadata":{"name":"worldgate-linux-runner-b"},"spec":{"containers":[{"name":"runner"}]},"status":{"phase":"Pending","conditions":[]}}
          ]
        }
        """;

        Assert.True(ArcObservationParser.TryParse(controllerJson, runnerJson, "worldgate-linux", out ArcObservation arc, out string? error), error);
        Assert.Equal(2, arc.ActiveRunnerCount);
        Assert.Null(arc.RunnerPodName);
    }

    [Fact]
    public void StateResolverPrioritizesActiveWorkOverPreviousFailure()
    {
        GitHubJobObservation active = Job(2, "in_progress", null, createdAt: Instant(5), startedAt: Instant(6));
        GitHubJobObservation failed = Job(1, "completed", "failure", createdAt: Instant(1), startedAt: Instant(2), completedAt: Instant(4));
        var github = new GitHubObservation(true, null, active, failed);
        var arc = new ArcObservation(true, null, true, true, new[] { new RunnerPodObservation("worldgate-linux-x", "Running", true) });

        MonitorSnapshot snapshot = MonitorStateResolver.Resolve(github, arc);

        Assert.Equal(MonitorDisplayState.Running, snapshot.DisplayState);
    }

    [Fact]
    public void StateResolverDistinguishesUnavailableQueuedAndFailedIdle()
    {
        ArcObservation healthyArc = new(true, null, true, true, Array.Empty<RunnerPodObservation>());
        Assert.Equal(
            MonitorDisplayState.Unavailable,
            MonitorStateResolver.Resolve(GitHubObservation.Unavailable("gh failed"), healthyArc).DisplayState);

        GitHubJobObservation queued = Job(2, "queued", null, createdAt: Instant(5));
        Assert.Equal(
            MonitorDisplayState.Queued,
            MonitorStateResolver.Resolve(new GitHubObservation(true, null, queued, null), healthyArc).DisplayState);

        GitHubJobObservation failed = Job(3, "completed", "failure", createdAt: Instant(1), completedAt: Instant(4));
        Assert.Equal(
            MonitorDisplayState.Degraded,
            MonitorStateResolver.Resolve(new GitHubObservation(true, null, null, failed), healthyArc).DisplayState);
    }

    [Fact]
    public void NotificationsSuppressBaselineThenEmitStartAndCompletionOnce()
    {
        var planner = new NotificationPlanner(TimeSpan.FromSeconds(90));
        ArcObservation arc = new(true, null, true, true, new[] { new RunnerPodObservation("worldgate-linux-x7c2p", "Running", true) });
        GitHubJobObservation queued = Job(10, "queued", null, createdAt: Instant(10));

        Assert.Empty(planner.Evaluate(
            MonitorStateResolver.Resolve(new GitHubObservation(true, null, queued, null), arc with { RunnerPods = Array.Empty<RunnerPodObservation>() }),
            Instant(20)));

        GitHubJobObservation running = queued with { Status = "in_progress", StartedAt = Instant(21) };
        IReadOnlyList<MonitorNotification> started = planner.Evaluate(
            MonitorStateResolver.Resolve(new GitHubObservation(true, null, running, null), arc),
            Instant(22));
        MonitorNotification start = Assert.Single(started);
        Assert.Equal(MonitorNotificationKind.Started, start.Kind);
        Assert.Contains("PR #16 — Verify", start.Message);
        Assert.Contains("Runner pod: worldgate-linux-x7c2p", start.Message);
        Assert.Empty(planner.Evaluate(
            MonitorStateResolver.Resolve(new GitHubObservation(true, null, running, null), arc),
            Instant(23)));

        GitHubJobObservation passed = running with { Status = "completed", Conclusion = "success", CompletedAt = Instant(26) };
        IReadOnlyList<MonitorNotification> completed = planner.Evaluate(
            MonitorStateResolver.Resolve(new GitHubObservation(true, null, null, passed), arc with { RunnerPods = Array.Empty<RunnerPodObservation>() }),
            Instant(27));
        MonitorNotification completion = Assert.Single(completed);
        Assert.Equal(MonitorNotificationKind.Passed, completion.Kind);
        Assert.Contains("Duration: 5m 0s", completion.Message);
        Assert.Empty(planner.Evaluate(
            MonitorStateResolver.Resolve(new GitHubObservation(true, null, null, passed), arc with { RunnerPods = Array.Empty<RunnerPodObservation>() }),
            Instant(28)));
    }

    [Theory]
    [InlineData("failure", MonitorNotificationKind.Failed, "WorldGate CI failed")]
    [InlineData("cancelled", MonitorNotificationKind.Cancelled, "WorldGate CI cancelled")]
    public void NotificationsDistinguishFailureAndCancellation(string conclusion, MonitorNotificationKind expectedKind, string expectedTitle)
    {
        var planner = new NotificationPlanner(TimeSpan.FromSeconds(90));
        ArcObservation arc = new(true, null, true, true, Array.Empty<RunnerPodObservation>());
        Assert.Empty(planner.Evaluate(MonitorStateResolver.Resolve(GitHubObservation.Empty, arc), Instant(1)));

        GitHubJobObservation completed = Job(42, "completed", conclusion, createdAt: Instant(2), completedAt: Instant(3));
        MonitorNotification notification = Assert.Single(planner.Evaluate(
            MonitorStateResolver.Resolve(new GitHubObservation(true, null, null, completed), arc),
            Instant(4)));

        Assert.Equal(expectedKind, notification.Kind);
        Assert.Equal(expectedTitle, notification.Title);
    }

    [Fact]
    public void QueueDelayWarningIsOneShotAndRequiresNoRunner()
    {
        var planner = new NotificationPlanner(TimeSpan.FromSeconds(90));
        GitHubJobObservation queued = Job(90, "queued", null, createdAt: Instant(0));
        ArcObservation arc = new(true, null, true, true, Array.Empty<RunnerPodObservation>());
        MonitorSnapshot snapshot = MonitorStateResolver.Resolve(new GitHubObservation(true, null, queued, null), arc);

        Assert.Empty(planner.Evaluate(snapshot, Instant(1)));
        MonitorNotification warning = Assert.Single(planner.Evaluate(snapshot, Instant(2)));
        Assert.Equal(MonitorNotificationKind.RunnerDelayed, warning.Kind);
        Assert.Empty(planner.Evaluate(snapshot, Instant(3)));
    }

    [Fact]
    public void TooltipNeverExceedsNotifyIconLimit()
    {
        GitHubJobObservation job = Job(5, "in_progress", null, createdAt: Instant(1), startedAt: Instant(2)) with
        {
            RunName = "A very long verify workflow name that would overflow the native tray tooltip",
        };
        ArcObservation arc = new(true, null, true, true, new[] { new RunnerPodObservation("worldgate-linux-extremely-long-runner-pod-name", "Running", true) });
        MonitorSnapshot snapshot = MonitorStateResolver.Resolve(new GitHubObservation(true, null, job, null), arc);

        string tooltip = TooltipFormatter.Format(snapshot, Instant(3));

        Assert.True(tooltip.Length <= 63);
        Assert.StartsWith("WorldGate: RUNNING", tooltip);
    }

    [Fact]
    public void MalformedProviderJsonReturnsStructuredParseFailure()
    {
        Assert.False(GitHubObservationParser.TryParseRuns("{not-json", out _, out string? githubError));
        Assert.Contains("Unable to parse GitHub workflow runs", githubError);

        Assert.False(ArcObservationParser.TryParse("{}", "{}", "worldgate-linux", out ArcObservation arc, out string? arcError));
        Assert.False(arc.Available);
        Assert.Contains("Unable to parse ARC pod state", arcError);
    }

    private static GitHubJobObservation Job(
        long id,
        string status,
        string? conclusion,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null) =>
        new(
            id,
            100,
            "Verify",
            "https://github.com/djfariel/WorldGate/actions/runs/100",
            16,
            "feature/test",
            "Integration Tests",
            status,
            conclusion,
            createdAt,
            startedAt,
            completedAt,
            new[] { "self-hosted", "worldgate-linux" });

    private static DateTimeOffset Instant(int minutes) =>
        new DateTimeOffset(2026, 9, 4, 20, 0, 0, TimeSpan.Zero).AddMinutes(minutes);
}
