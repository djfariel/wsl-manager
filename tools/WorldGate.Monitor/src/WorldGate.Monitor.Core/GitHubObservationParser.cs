using System.Text.Json;

namespace WorldGate.Monitor.Core;

public static class GitHubObservationParser
{
    public static bool TryParseRuns(
        string json,
        out IReadOnlyList<WorkflowRunSummary> runs,
        out string? error)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("workflow_runs", out JsonElement runsElement) ||
                runsElement.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("GitHub response does not contain workflow_runs array.");
            }

            var parsed = new List<WorkflowRunSummary>();
            foreach (JsonElement run in runsElement.EnumerateArray())
            {
                long id = run.GetProperty("id").GetInt64();
                string name = GetString(run, "name") ?? $"Run {id}";
                string htmlUrl = GetString(run, "html_url") ?? string.Empty;
                string? branch = GetString(run, "head_branch");
                DateTimeOffset createdAt = GetTimestamp(run, "created_at") ?? DateTimeOffset.MinValue;
                int? pullRequestNumber = null;

                if (run.TryGetProperty("pull_requests", out JsonElement pullRequests) &&
                    pullRequests.ValueKind == JsonValueKind.Array &&
                    pullRequests.GetArrayLength() > 0)
                {
                    JsonElement first = pullRequests[0];
                    if (first.TryGetProperty("number", out JsonElement number) && number.TryGetInt32(out int parsedNumber))
                    {
                        pullRequestNumber = parsedNumber;
                    }
                }

                parsed.Add(new WorkflowRunSummary(id, name, htmlUrl, pullRequestNumber, branch, createdAt));
            }

            runs = parsed;
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            runs = Array.Empty<WorkflowRunSummary>();
            error = $"Unable to parse GitHub workflow runs: {exception.Message}";
            return false;
        }
    }

    public static bool TryParseJobs(
        string json,
        WorkflowRunSummary run,
        string runnerScaleSetName,
        out IReadOnlyList<GitHubJobObservation> jobs,
        out string? error)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("jobs", out JsonElement jobsElement) ||
                jobsElement.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("GitHub response does not contain jobs array.");
            }

            var parsed = new List<GitHubJobObservation>();
            foreach (JsonElement job in jobsElement.EnumerateArray())
            {
                var labels = new List<string>();
                if (job.TryGetProperty("labels", out JsonElement labelsElement) &&
                    labelsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement label in labelsElement.EnumerateArray())
                    {
                        if (label.ValueKind == JsonValueKind.String && label.GetString() is { } value)
                        {
                            labels.Add(value);
                        }
                    }
                }

                if (!labels.Any(label => label.Equals(runnerScaleSetName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                long jobId = job.GetProperty("id").GetInt64();
                string jobName = GetString(job, "name") ?? $"Job {jobId}";
                string status = GetString(job, "status") ?? "unknown";
                string? conclusion = GetString(job, "conclusion");
                DateTimeOffset createdAt = GetTimestamp(job, "created_at") ?? run.CreatedAt;
                DateTimeOffset? startedAt = GetTimestamp(job, "started_at");
                DateTimeOffset? completedAt = GetTimestamp(job, "completed_at");

                parsed.Add(new GitHubJobObservation(
                    jobId,
                    run.Id,
                    run.Name,
                    run.HtmlUrl,
                    run.PullRequestNumber,
                    run.Branch,
                    jobName,
                    status,
                    conclusion,
                    createdAt,
                    startedAt,
                    completedAt,
                    labels));
            }

            jobs = parsed;
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            jobs = Array.Empty<GitHubJobObservation>();
            error = $"Unable to parse GitHub jobs for run {run.Id}: {exception.Message}";
            return false;
        }
    }

    public static GitHubObservation SelectObservation(IEnumerable<GitHubJobObservation> jobs)
    {
        GitHubJobObservation? active = jobs
            .Where(job => job.IsRunning || job.IsQueued)
            .OrderByDescending(job => job.IsRunning)
            .ThenByDescending(job => job.StartedAt ?? job.CreatedAt)
            .FirstOrDefault();

        GitHubJobObservation? latestCompleted = jobs
            .Where(job => job.IsCompleted)
            .OrderByDescending(job => job.CompletedAt ?? job.CreatedAt)
            .FirstOrDefault();

        return new GitHubObservation(true, null, active, latestCompleted);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static DateTimeOffset? GetTimestamp(JsonElement element, string propertyName)
    {
        string? value = GetString(element, propertyName);
        return DateTimeOffset.TryParse(value, out DateTimeOffset parsed) ? parsed : null;
    }
}
