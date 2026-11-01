namespace WorldGate.Monitor.Core.ManagedImages;

public static class ManagedImageProbeDiagnostics
{
    public static string SummarizeCommandFailure(string? standardError, string? processError)
    {
        if (!string.IsNullOrWhiteSpace(standardError))
        {
            string line = standardError.Trim().Replace("\r\n", "\n").Split('\n')[0].Trim();
            if (line.Length > 240)
            {
                return line[..240] + "…";
            }

            return line;
        }

        if (!string.IsNullOrWhiteSpace(processError))
        {
            return processError.Trim();
        }

        return "command failed";
    }

    public static string FormatDockerProbeDetail(
        bool inspectOk,
        bool psOk,
        ManagedImagePresence presence,
        int running,
        int total,
        string? failureStep = null)
    {
        if (!string.IsNullOrWhiteSpace(failureStep))
        {
            return failureStep;
        }

        string presenceLabel = presence == ManagedImagePresence.Present ? "image present" : "image absent";
        return $"docker image inspect: ok ({presenceLabel}); docker ps: {running}/{total} running";
    }

    public static string FormatKubernetesProbeDetail(bool podsOk, int running, int total, string? failureStep = null)
    {
        if (!string.IsNullOrWhiteSpace(failureStep))
        {
            return failureStep;
        }

        return $"kubectl get pods: ok ({running}/{total} running)";
    }

    public static void AppendLogLine(string displayName, string detail)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WorldGateMonitor");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "managed-image-probe.log");
            string line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\t{displayName}\t{detail.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ')}{Environment.NewLine}";
            File.AppendAllText(path, line);
        }
        catch
        {
            // Logging must not break probes.
        }
    }
}
