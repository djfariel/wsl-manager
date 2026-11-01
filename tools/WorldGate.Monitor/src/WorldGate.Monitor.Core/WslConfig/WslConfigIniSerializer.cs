using System.Text;

namespace WorldGate.Monitor.Core.WslConfig;

public static class WslConfigIniSerializer
{
    public static WslConfigParseResult TryParse(string content)
    {
        if (content is null)
        {
            return WslConfigParseResult.Failure("Content is null.");
        }

        var document = new WslConfigDocument();
        bool inWsl2 = false;

        using var reader = new StringReader(content);
        string? line;
        int lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                string section = trimmed[1..^1].Trim();
                inWsl2 = section.Equals("wsl2", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inWsl2)
            {
                continue;
            }

            int equals = trimmed.IndexOf('=');
            if (equals <= 0)
            {
                return WslConfigParseResult.Failure($"Line {lineNumber}: expected key=value.");
            }

            string key = trimmed[..equals].Trim();
            string value = trimmed[(equals + 1)..].Trim();
            if (string.IsNullOrEmpty(key))
            {
                return WslConfigParseResult.Failure($"Line {lineNumber}: key is empty.");
            }

            if (WslConfigDocument.KnownWsl2Keys.Any(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                document.SetKnown(key, value);
            }
            else
            {
                document.ExtraWsl2Settings[key] = value;
            }
        }

        return WslConfigParseResult.Success(document);
    }

    public static string Serialize(WslConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        builder.AppendLine("[wsl2]");

        foreach (string key in WslConfigDocument.KnownWsl2Keys)
        {
            string? value = document.GetKnown(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.Append(key).Append('=').AppendLine(value.Trim());
            }
        }

        foreach (KeyValuePair<string, string> pair in document.ExtraWsl2Settings.OrderBy(static p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(pair.Value))
            {
                builder.Append(pair.Key).Append('=').AppendLine(pair.Value.Trim());
            }
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }
}

public readonly struct WslConfigParseResult
{
    public WslConfigDocument? Document { get; }
    public string? Error { get; }

    private WslConfigParseResult(WslConfigDocument? document, string? error)
    {
        Document = document;
        Error = error;
    }

    public bool Succeeded => Error is null && Document is not null;

    public static WslConfigParseResult Success(WslConfigDocument document) => new(document, null);

    public static WslConfigParseResult Failure(string error) => new(null, error);
}
