using System.Text.Json;

namespace WorldGate.Monitor;

internal sealed record ConfigurationLoadResult(
    MonitorConfiguration Configuration,
    string ConfigurationPath,
    string? Error);

internal sealed class ConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public ConfigurationLoadResult LoadOrCreate()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WorldGateMonitor");
        string path = Path.Combine(directory, "config.json");
        var defaults = new MonitorConfiguration();

        try
        {
            Directory.CreateDirectory(directory);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, JsonSerializer.Serialize(defaults, SerializerOptions));
                return new ConfigurationLoadResult(defaults, path, null);
            }

            string json = File.ReadAllText(path);
            MonitorConfiguration? configuration = JsonSerializer.Deserialize<MonitorConfiguration>(json, SerializerOptions);
            if (configuration is null)
            {
                return new ConfigurationLoadResult(defaults, path, "config.json did not contain a configuration object");
            }

            string? validationError = configuration.Validate();
            return new ConfigurationLoadResult(configuration, path, validationError);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new ConfigurationLoadResult(defaults, path, $"Unable to load config.json: {exception.Message}");
        }
    }
}
