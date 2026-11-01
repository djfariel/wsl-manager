using System.Diagnostics;

namespace WorldGate.Monitor;

internal sealed record WslCommandResult(
    bool Success,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    string? Error);

internal sealed class WslCommandRunner
{
    private readonly string? distribution;
    private readonly TimeSpan commandTimeout;

    public WslCommandRunner(string? distribution, TimeSpan commandTimeout)
    {
        this.distribution = string.IsNullOrWhiteSpace(distribution) ? null : distribution;
        this.commandTimeout = commandTimeout;
    }

    public async Task<WslCommandResult> RunAsync(IReadOnlyList<string> commandArguments, CancellationToken cancellationToken)
    {
        if (commandArguments.Count == 0)
        {
            return new WslCommandResult(false, null, string.Empty, string.Empty, "No WSL command was specified.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        if (distribution is not null)
        {
            startInfo.ArgumentList.Add("-d");
            startInfo.ArgumentList.Add(distribution);
        }

        // --exec runs the Linux binary directly; default wsl invocation uses a shell and
        // splits metacharacters in docker --format templates (e.g. "|" in {{.Image}}|{{.State}}).
        startInfo.ArgumentList.Add("--exec");
        foreach (string argument in commandArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return new WslCommandResult(false, null, string.Empty, string.Empty, "wsl.exe did not start.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new WslCommandResult(false, null, string.Empty, string.Empty, $"Unable to start wsl.exe: {exception.Message}");
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(commandTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await WaitAfterKillAsync(process).ConfigureAwait(false);
            return new WslCommandResult(
                false,
                process.HasExited ? process.ExitCode : null,
                await stdoutTask.ConfigureAwait(false),
                await stderrTask.ConfigureAwait(false),
                $"WSL command exceeded the {commandTimeout.TotalSeconds:0}-second timeout.");
        }

        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);
        return process.ExitCode == 0
            ? new WslCommandResult(true, process.ExitCode, stdout, stderr, null)
            : new WslCommandResult(false, process.ExitCode, stdout, stderr, $"WSL command exited with code {process.ExitCode}.");
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task WaitAfterKillAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
