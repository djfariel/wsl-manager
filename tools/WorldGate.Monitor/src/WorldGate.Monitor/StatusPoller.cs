using WorldGate.Monitor.Core;

namespace WorldGate.Monitor;

internal sealed class StatusPoller
{
    private readonly MonitorConfiguration configuration;
    private readonly WslCommandRunner commandRunner;
    private readonly string? configurationError;

    public StatusPoller(
        MonitorConfiguration configuration,
        WslCommandRunner commandRunner,
        string? configurationError)
    {
        this.configuration = configuration;
        this.commandRunner = commandRunner;
        this.configurationError = configurationError;
    }

    public async Task<MonitorSnapshot> PollAsync(CancellationToken cancellationToken)
    {
        if (configurationError is not null)
        {
            string error = $"Configuration error: {configurationError}";
            return MonitorStateResolver.Resolve(
                GitHubObservation.Unavailable(error),
                ArcObservation.Unavailable(error));
        }

        Task<GitHubObservation> githubTask = PollGitHubAsync(cancellationToken);
        Task<ArcObservation> arcTask = PollArcAsync(cancellationToken);
        await Task.WhenAll(githubTask, arcTask).ConfigureAwait(false);
        return MonitorStateResolver.Resolve(await githubTask, await arcTask);
    }

    private async Task<GitHubObservation> PollGitHubAsync(CancellationToken cancellationToken)
    {
        WslCommandResult runsResult = await commandRunner.RunAsync(
            new[] { "gh", "api", $"repos/{configuration.Repository}/actions/runs?per_page=10" },
            cancellationToken).ConfigureAwait(false);
        if (!runsResult.Success)
        {
            return GitHubObservation.Unavailable(DescribeFailure("gh workflow run query", runsResult));
        }

        if (!GitHubObservationParser.TryParseRuns(runsResult.StandardOutput, out IReadOnlyList<WorkflowRunSummary> runs, out string? parseError))
        {
            return GitHubObservation.Unavailable(parseError ?? "Unable to parse GitHub workflow runs.");
        }

        var monitoredJobs = new List<GitHubJobObservation>();
        foreach (WorkflowRunSummary run in runs.OrderByDescending(run => run.CreatedAt).Take(5))
        {
            WslCommandResult jobsResult = await commandRunner.RunAsync(
                GitHubApiCommandBuilder.BuildJobsQuery(configuration.Repository, run.Id),
                cancellationToken).ConfigureAwait(false);
            if (!jobsResult.Success)
            {
                return GitHubObservation.Unavailable(DescribeFailure($"gh jobs query for run {run.Id}", jobsResult));
            }

            if (!GitHubObservationParser.TryParseJobs(
                    jobsResult.StandardOutput,
                    run,
                    configuration.RunnerScaleSetName,
                    out IReadOnlyList<GitHubJobObservation> jobs,
                    out string? jobsError))
            {
                return GitHubObservation.Unavailable(jobsError ?? $"Unable to parse jobs for run {run.Id}.");
            }

            monitoredJobs.AddRange(jobs);
        }

        return GitHubObservationParser.SelectObservation(monitoredJobs);
    }

    private async Task<ArcObservation> PollArcAsync(CancellationToken cancellationToken)
    {
        WslCommandResult controllerResult = await commandRunner.RunAsync(
            new[] { "kubectl", "get", "pods", "-n", configuration.ControllerNamespace, "-o", "json" },
            cancellationToken).ConfigureAwait(false);
        if (!controllerResult.Success)
        {
            return ArcObservation.Unavailable(DescribeFailure("kubectl ARC controller query", controllerResult));
        }

        WslCommandResult runnerResult = await commandRunner.RunAsync(
            new[] { "kubectl", "get", "pods", "-n", configuration.RunnerNamespace, "-o", "json" },
            cancellationToken).ConfigureAwait(false);
        if (!runnerResult.Success)
        {
            return ArcObservation.Unavailable(DescribeFailure("kubectl ARC runner query", runnerResult));
        }

        if (!ArcObservationParser.TryParse(
                controllerResult.StandardOutput,
                runnerResult.StandardOutput,
                configuration.RunnerScaleSetName,
                out ArcObservation observation,
                out string? parseError))
        {
            return ArcObservation.Unavailable(parseError ?? "Unable to parse ARC pod state.");
        }

        return observation;
    }

    private static string DescribeFailure(string operation, WslCommandResult result)
    {
        string detail = !string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardError.Trim()
            : result.Error ?? "unknown error";
        return $"{operation} failed: {detail}";
    }
}
