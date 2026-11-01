using System.Globalization;
using System.Text.RegularExpressions;

namespace WorldGate.Monitor.Core.WslConfig;

public static partial class WslConfigValidator
{
    private static readonly HashSet<string> BooleanKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhostForwarding",
        "nestedVirtualization",
        "debugConsole",
        "safeMode",
        "guiApplications",
        "pageReporting",
        "sparseVhd",
        "dnsTunneling",
        "firewall",
        "autoProxy",
    };

    private static readonly HashSet<string> SizeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "memory",
        "swap",
        "defaultVhdSize",
    };

    public static IReadOnlyList<string> Validate(WslConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<string>();
        foreach (string key in WslConfigDocument.KnownWsl2Keys)
        {
            string? value = document.GetKnown(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            ValidateKey(key, value.Trim(), errors);
        }

        return errors;
    }

    private static void ValidateKey(string key, string value, List<string> errors)
    {
        if (key.Equals("processors", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int processors) || processors < 1)
            {
                errors.Add("processors must be a positive integer.");
            }

            return;
        }

        if (key.Equals("vmIdleTimeout", StringComparison.OrdinalIgnoreCase))
        {
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long timeout) || timeout < -1)
            {
                errors.Add("vmIdleTimeout must be -1 (disabled) or a non-negative number of milliseconds.");
            }

            return;
        }

        if (key.Equals("autoMemoryReclaim", StringComparison.OrdinalIgnoreCase))
        {
            if (!value.Equals("disabled", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("gradual", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("autoMemoryReclaim must be 'disabled' or 'gradual'.");
            }

            return;
        }

        if (SizeKeys.Contains(key))
        {
            if (!SizePattern().IsMatch(value))
            {
                errors.Add($"{key} must be a size such as 8GB, 512MB, or a byte count.");
            }

            return;
        }

        if (BooleanKeys.Contains(key))
        {
            if (!value.Equals("true", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{key} must be true or false.");
            }
        }
    }

    [GeneratedRegex(@"^\d+(\.\d+)?(B|KB|MB|GB|TB|K|M|G|T)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SizePattern();
}
