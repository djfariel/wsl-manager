namespace WorldGate.Monitor.Core.WslConfig;

/// <summary>
/// Tooltip / help text for .wslconfig [wsl2] fields (includes Microsoft defaults when a key is omitted).
/// </summary>
public static class WslConfigFieldHelp
{
    public static IReadOnlyDictionary<string, string> All { get; } = Build();

    public static string Get(string fieldKey) =>
        All.TryGetValue(fieldKey, out string? text) ? text : string.Empty;

    private static IReadOnlyDictionary<string, string> Build() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["memory"] =
                "Maximum RAM for the WSL 2 utility VM (e.g. 8GB, 512MB). "
                + "Default when omitted: 50% of physical memory on Windows (minimum 256 MB, maximum 512 GB). "
                + "This app uses 8GB when creating a new file.",
            ["processors"] =
                "Logical processors available to WSL 2. "
                + "Default when omitted: all logical processors on the host. "
                + "This app uses 4 when creating a new file.",
            ["swap"] =
                "Swap file size for the utility VM (e.g. 4GB). "
                + "Default when omitted: 25% of memory assigned to WSL, capped at 8 GB. "
                + "This app uses 4GB when creating a new file.",
            ["defaultVhdSize"] =
                "Maximum size for new distro virtual disks (e.g. 512GB). "
                + "Default when omitted: 1 TB maximum per Microsoft documentation.",
            ["localhostForwarding"] =
                "Whether Windows localhost can reach WSL services and vice versa. "
                + "Default when omitted: true.",
            ["nestedVirtualization"] =
                "Enables nested virtualization inside the WSL 2 VM (requires host CPU support). "
                + "Default when omitted: false.",
            ["vmIdleTimeout"] =
                "Milliseconds before the utility VM shuts down when no distros are running; -1 disables idle shutdown. "
                + "Default when omitted: 60000 (60 seconds).",
            ["debugConsole"] =
                "Shows an debug console window for the WSL 2 VM. "
                + "Default when omitted: false.",
            ["safeMode"] =
                "Starts WSL in safe mode (minimal drivers/features) for troubleshooting. "
                + "Default when omitted: false.",
            ["guiApplications"] =
                "Allows Linux GUI apps to appear on the Windows desktop (WSLg). "
                + "Default when omitted: true.",
            ["pageReporting"] =
                "Reports free memory pages from WSL to Windows for reclaim. "
                + "Default when omitted: true.",
            ["sparseVhd"] =
                "Creates distro VHDs as sparse files to save disk space. "
                + "Default when omitted: true.",
            ["dnsTunneling"] =
                "Uses a tunnel for DNS resolution between WSL and Windows. "
                + "Default when omitted: true.",
            ["firewall"] =
                "Applies Windows firewall rules to WSL network traffic. "
                + "Default when omitted: true.",
            ["autoProxy"] =
                "Propagates Windows proxy settings into WSL. "
                + "Default when omitted: true.",
            ["autoMemoryReclaim"] =
                "How Windows reclaims memory from the idle WSL VM: disabled or gradual. "
                + "Default when omitted: gradual.",
            ["swapFile"] =
                "Path to the WSL swap VHDX file. "
                + "Default when omitted: %USERPROFILE%\\AppData\\Local\\Temp\\swap.vhdx.",
            ["kernel"] =
                "Custom path to a Linux kernel binary for WSL 2. "
                + "Default when omitted: Microsoft's built-in kernel.",
            ["kernelCommandLine"] =
                "Extra parameters appended to the WSL 2 kernel command line. "
                + "Default when omitted: (empty).",
        };
}
