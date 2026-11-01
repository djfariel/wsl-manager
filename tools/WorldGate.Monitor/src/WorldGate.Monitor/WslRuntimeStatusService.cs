using System.Diagnostics;
using System.Text;
using WorldGate.Monitor.Core.WslRuntime;

namespace WorldGate.Monitor;

internal sealed class WslRuntimeStatusService
{
    public WslRuntimeSnapshot GetSnapshot()
    {
        WslRuntimeSnapshot snapshot = WslRuntimeProbe.Detect();
        if (snapshot.State != WslRuntimeState.Running)
        {
            return snapshot;
        }

        string? stdout = TryListRunningDistros();
        return WslRuntimeProbe.WithRunningDistros(snapshot, stdout);
    }

    private static string? TryListRunningDistros()
    {
        try
        {
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = "--list --running",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Unicode,
                StandardErrorEncoding = Encoding.Unicode,
            }) ?? throw new InvalidOperationException("Failed to start wsl.exe.");

            string stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit(TimeSpan.FromSeconds(10));
            return process.ExitCode == 0 ? stdout : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
