namespace WorldGate.Monitor.Core.WslRuntime;

/// <summary>
/// Detects whether the WSL 2 lightweight utility VM (or running distros) are active.
/// </summary>
/// <remarks>
/// <para>
/// Polling must not start WSL. We therefore avoid invoking <c>wsl.exe</c> when the VM is
/// clearly off. On WSL 2, a running utility VM is reflected by a <c>vmmem</c> or
/// <c>vmmemWSL</c> process (see Microsoft WSL architecture). When those processes are
/// absent, WSL is treated as stopped without calling into the WSL CLI.
/// </para>
/// <para>
/// When the VM process is present, optional detail (running distribution names) may be
/// obtained via <c>wsl --list --running</c>, which lists already-running distros and does
/// not launch a stopped default distribution (unlike bare <c>wsl</c>).
/// </para>
/// </remarks>
public static class WslRuntimeProbe
{
    private static readonly string[] VmProcessNames = ["vmmemWSL", "vmmem"];

    public static WslRuntimeSnapshot Detect(Func<IEnumerable<string>>? enumerateProcessNames = null)
    {
        enumerateProcessNames ??= EnumerateProcessNamesDefault;

        HashSet<string> names;
        try
        {
            names = enumerateProcessNames()
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            return WslRuntimeSnapshot.Error(exception.Message);
        }

        bool vmRunning = VmProcessNames.Any(names.Contains);
        if (!vmRunning)
        {
            return WslRuntimeSnapshot.Stopped();
        }

        return WslRuntimeSnapshot.Running(runningDistros: null);
    }

    /// <summary>
    /// Parses stdout from <c>wsl --list --running</c> (locale-dependent header lines ignored).
    /// </summary>
    public static IReadOnlyList<string> ParseRunningDistributionList(string? stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return [];
        }

        var distros = new List<string>();
        foreach (string rawLine in stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Contains("Windows Subsystem", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Subsystem for Linux", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("WSL", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string name = line;
            int paren = line.IndexOf('(');
            if (paren > 0)
            {
                name = line[..paren].Trim();
            }

            if (name.Length > 0)
            {
                distros.Add(name);
            }
        }

        return distros;
    }

    public static WslRuntimeSnapshot WithRunningDistros(WslRuntimeSnapshot snapshot, string? listRunningStdout)
    {
        if (snapshot.State != WslRuntimeState.Running)
        {
            return snapshot;
        }

        IReadOnlyList<string> distros = ParseRunningDistributionList(listRunningStdout);
        return snapshot with { RunningDistributions = distros.Count > 0 ? distros : snapshot.RunningDistributions };
    }

    private static IEnumerable<string> EnumerateProcessNamesDefault()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        foreach (string name in VmProcessNames)
        {
            if (System.Diagnostics.Process.GetProcessesByName(name).Length > 0)
            {
                yield return name;
            }
        }
    }
}

public enum WslRuntimeState
{
    Stopped,
    Running,
    Error,
}

public sealed record WslRuntimeSnapshot(
    WslRuntimeState State,
    IReadOnlyList<string>? RunningDistributions,
    string? ErrorMessage)
{
    public static WslRuntimeSnapshot Stopped() => new(WslRuntimeState.Stopped, null, null);

    public static WslRuntimeSnapshot Running(IReadOnlyList<string>? runningDistros) =>
        new(WslRuntimeState.Running, runningDistros, null);

    public static WslRuntimeSnapshot Error(string message) =>
        new(WslRuntimeState.Error, null, message);
}
