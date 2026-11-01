namespace WorldGate.Monitor.Core.WslConfig;

/// <summary>
/// Parsed representation of a user <c>.wslconfig</c> file focused on the <c>[wsl2]</c> section.
/// Unknown keys in <c>[wsl2]</c> are preserved in <see cref="ExtraWsl2Settings"/>.
/// </summary>
public sealed class WslConfigDocument
{
    public static readonly IReadOnlyList<string> KnownWsl2Keys =
    [
        "memory",
        "processors",
        "swap",
        "localhostForwarding",
        "nestedVirtualization",
        "vmIdleTimeout",
        "debugConsole",
        "swapFile",
        "kernel",
        "kernelCommandLine",
        "safeMode",
        "guiApplications",
        "pageReporting",
        "defaultVhdSize",
        "sparseVhd",
        "dnsTunneling",
        "firewall",
        "autoProxy",
        "autoMemoryReclaim",
    ];

    public string? Memory { get; set; }
    public string? Processors { get; set; }
    public string? Swap { get; set; }
    public string? LocalhostForwarding { get; set; }
    public string? NestedVirtualization { get; set; }
    public string? VmIdleTimeout { get; set; }
    public string? DebugConsole { get; set; }
    public string? SwapFile { get; set; }
    public string? Kernel { get; set; }
    public string? KernelCommandLine { get; set; }
    public string? SafeMode { get; set; }
    public string? GuiApplications { get; set; }
    public string? PageReporting { get; set; }
    public string? DefaultVhdSize { get; set; }
    public string? SparseVhd { get; set; }
    public string? DnsTunneling { get; set; }
    public string? Firewall { get; set; }
    public string? AutoProxy { get; set; }
    public string? AutoMemoryReclaim { get; set; }

    public Dictionary<string, string> ExtraWsl2Settings { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? GetKnown(string key) => key.ToLowerInvariant() switch
    {
        "memory" => Memory,
        "processors" => Processors,
        "swap" => Swap,
        "localhostforwarding" => LocalhostForwarding,
        "nestedvirtualization" => NestedVirtualization,
        "vmidletimeout" => VmIdleTimeout,
        "debugconsole" => DebugConsole,
        "swapfile" => SwapFile,
        "kernel" => Kernel,
        "kernelcommandline" => KernelCommandLine,
        "safemode" => SafeMode,
        "guiapplications" => GuiApplications,
        "pagereporting" => PageReporting,
        "defaultvhdsize" => DefaultVhdSize,
        "sparsevhd" => SparseVhd,
        "dnstunneling" => DnsTunneling,
        "firewall" => Firewall,
        "autoproxy" => AutoProxy,
        "automemoryreclaim" => AutoMemoryReclaim,
        _ => null,
    };

    public void SetKnown(string key, string? value)
    {
        switch (key.ToLowerInvariant())
        {
            case "memory": Memory = value; break;
            case "processors": Processors = value; break;
            case "swap": Swap = value; break;
            case "localhostforwarding": LocalhostForwarding = value; break;
            case "nestedvirtualization": NestedVirtualization = value; break;
            case "vmidletimeout": VmIdleTimeout = value; break;
            case "debugconsole": DebugConsole = value; break;
            case "swapfile": SwapFile = value; break;
            case "kernel": Kernel = value; break;
            case "kernelcommandline": KernelCommandLine = value; break;
            case "safemode": SafeMode = value; break;
            case "guiapplications": GuiApplications = value; break;
            case "pagereporting": PageReporting = value; break;
            case "defaultvhdsize": DefaultVhdSize = value; break;
            case "sparsevhd": SparseVhd = value; break;
            case "dnstunneling": DnsTunneling = value; break;
            case "firewall": Firewall = value; break;
            case "autoproxy": AutoProxy = value; break;
            case "automemoryreclaim": AutoMemoryReclaim = value; break;
            default:
                if (string.IsNullOrWhiteSpace(value))
                {
                    ExtraWsl2Settings.Remove(key);
                }
                else
                {
                    ExtraWsl2Settings[key] = value.Trim();
                }

                break;
        }
    }

    public static WslConfigDocument CreateDefault() => new()
    {
        Memory = "8GB",
        Processors = "4",
        Swap = "4GB",
        LocalhostForwarding = "true",
    };

    public WslConfigDocument Clone()
    {
        var clone = new WslConfigDocument
        {
            Memory = Memory,
            Processors = Processors,
            Swap = Swap,
            LocalhostForwarding = LocalhostForwarding,
            NestedVirtualization = NestedVirtualization,
            VmIdleTimeout = VmIdleTimeout,
            DebugConsole = DebugConsole,
            SwapFile = SwapFile,
            Kernel = Kernel,
            KernelCommandLine = KernelCommandLine,
            SafeMode = SafeMode,
            GuiApplications = GuiApplications,
            PageReporting = PageReporting,
            DefaultVhdSize = DefaultVhdSize,
            SparseVhd = SparseVhd,
            DnsTunneling = DnsTunneling,
            Firewall = Firewall,
            AutoProxy = AutoProxy,
            AutoMemoryReclaim = AutoMemoryReclaim,
        };

        foreach (KeyValuePair<string, string> pair in ExtraWsl2Settings)
        {
            clone.ExtraWsl2Settings[pair.Key] = pair.Value;
        }

        return clone;
    }
}
