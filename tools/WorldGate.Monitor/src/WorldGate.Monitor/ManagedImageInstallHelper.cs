using WorldGate.Monitor.Core.ManagedImages;

namespace WorldGate.Monitor;

internal static class ManagedImageInstallHelper
{
    public static string DescribeInstallPlan(ManagedImageEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.BuildContextPath))
        {
            string context = entry.BuildContextPath.Trim();
            return $"Build locally in WSL:\n  cd \"{context}\"\n  docker build -t {entry.ImageReference} .";
        }

        return $"Pull in WSL:\n  docker pull {entry.ImageReference}";
    }

    public static IReadOnlyList<string> BuildWslPullCommand(ManagedImageEntry entry) =>
        new[] { "docker", "pull", entry.ImageReference.Trim() };

    public static IReadOnlyList<string> BuildWslBuildCommand(ManagedImageEntry entry)
    {
        string context = entry.BuildContextPath!.Trim().Replace('\\', '/');
        return new[] { "bash", "-lc", $"cd \"{context}\" && docker build -t {entry.ImageReference.Trim()} ." };
    }
}
