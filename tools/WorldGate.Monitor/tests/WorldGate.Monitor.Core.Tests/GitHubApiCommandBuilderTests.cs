using WorldGate.Monitor.Core;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class GitHubApiCommandBuilderTests
{
    [Fact]
    public void JobsQueryKeepsShellControlCharactersOutOfWslArguments()
    {
        IReadOnlyList<string> arguments = GitHubApiCommandBuilder.BuildJobsQuery(
            "djfariel/WorldGate",
            33922936595);

        Assert.Equal(
            new[]
            {
                "gh",
                "api",
                "--method",
                "GET",
                "repos/djfariel/WorldGate/actions/runs/33922936595/jobs",
                "-f",
                "filter=all",
                "-F",
                "per_page=100",
            },
            arguments);
        Assert.DoesNotContain(arguments, argument => argument.Contains('&'));
    }
}
