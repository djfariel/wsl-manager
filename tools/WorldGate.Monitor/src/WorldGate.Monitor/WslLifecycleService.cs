using System.Diagnostics;

namespace WorldGate.Monitor;

internal sealed class WslLifecycleService
{
    public WslShutdownResult TryShutdown()
    {
        try
        {
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = "--shutdown",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }) ?? throw new InvalidOperationException("Failed to start wsl.exe.");

            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(TimeSpan.FromSeconds(30));
            if (process.ExitCode != 0)
            {
                string message = string.IsNullOrWhiteSpace(stderr)
                    ? $"wsl --shutdown exited with code {process.ExitCode}."
                    : stderr.Trim();
                return WslShutdownResult.Failure(message);
            }

            return WslShutdownResult.Success();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return WslShutdownResult.Failure(exception.Message);
        }
    }
}

internal readonly struct WslShutdownResult
{
    public string? Error { get; }

    private WslShutdownResult(string? error) => Error = error;

    public bool Succeeded => Error is null;

    public static WslShutdownResult Success() => new(null);

    public static WslShutdownResult Failure(string error) => new(error);
}
