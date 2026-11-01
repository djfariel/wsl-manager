namespace WorldGate.Monitor.Core.ManagedImages;

public static class DockerImageListParser
{
    public static IReadOnlyList<string> ParseRepositoryTags(string? stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return [];
        }

        var references = new List<string>();
        foreach (string line in stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.Length == 0 || line.StartsWith('<'))
            {
                continue;
            }

            references.Add(line);
        }

        return references;
    }
}
