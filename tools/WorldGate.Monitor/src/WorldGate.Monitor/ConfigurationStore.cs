using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorldGate.Monitor;

internal sealed record ConfigurationLoadResult(
    MonitorConfiguration Configuration,
    string ConfigurationPath,
    string? Error);

internal sealed record ConfigurationSaveResult(bool Succeeded, string? Error)
{
    public static ConfigurationSaveResult Success() => new(true, null);

    public static ConfigurationSaveResult Failure(string error) => new(false, error);
}

internal sealed class ConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public ConfigurationSaveResult Save(MonitorConfiguration configuration, string configurationPath)
    {
        string? validationError = configuration.Validate();
        if (validationError is not null)
        {
            return ConfigurationSaveResult.Failure(validationError);
        }

        try
        {
            string? directory = Path.GetDirectoryName(configurationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(configuration, SerializerOptions);
            File.WriteAllText(configurationPath, json);
            return ConfigurationSaveResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ConfigurationSaveResult.Failure($"Unable to save config.json: {exception.Message}");
        }
    }

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
