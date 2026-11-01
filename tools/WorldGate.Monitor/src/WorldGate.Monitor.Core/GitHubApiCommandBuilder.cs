namespace WorldGate.Monitor.Core;

public static class GitHubApiCommandBuilder
{
    public static IReadOnlyList<string> BuildJobsQuery(string repository, long runId) =>
        new[]
        {
            "gh",
            "api",
            "--method",
            "GET",
            $"repos/{repository}/actions/runs/{runId}/jobs",
            "-f",
            "filter=all",
            "-F",
            "per_page=100",
        };
}
