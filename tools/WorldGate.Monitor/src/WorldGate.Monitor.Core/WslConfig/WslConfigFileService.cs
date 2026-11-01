namespace WorldGate.Monitor.Core.WslConfig;

public sealed class WslConfigFileService
{
    public string DefaultPath { get; }

    public WslConfigFileService(string? path = null)
    {
        DefaultPath = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".wslconfig");
    }

    public WslConfigLoadResult LoadOrCreateDefault()
    {
        if (!File.Exists(DefaultPath))
        {
            return WslConfigLoadResult.CreatedDefault(WslConfigDocument.CreateDefault(), DefaultPath, savedBaseline: null);
        }

        try
        {
            string content = File.ReadAllText(DefaultPath);
            WslConfigParseResult parsed = WslConfigIniSerializer.TryParse(content);
            if (!parsed.Succeeded || parsed.Document is null)
            {
                return WslConfigLoadResult.Failure(parsed.Error ?? "Unable to parse .wslconfig.");
            }

            string baseline = WslConfigIniSerializer.Serialize(parsed.Document);
            return WslConfigLoadResult.Loaded(parsed.Document, DefaultPath, baseline);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return WslConfigLoadResult.Failure(exception.Message);
        }
    }

    public WslConfigSaveResult Save(WslConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        IReadOnlyList<string> validationErrors = WslConfigValidator.Validate(document);
        if (validationErrors.Count > 0)
        {
            return WslConfigSaveResult.ValidationFailure(validationErrors);
        }

        string serialized = WslConfigIniSerializer.Serialize(document);
        string? directory = Path.GetDirectoryName(DefaultPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = DefaultPath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, serialized);
            if (File.Exists(DefaultPath))
            {
                File.Replace(tempPath, DefaultPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, DefaultPath);
            }

            return WslConfigSaveResult.Success(serialized);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(tempPath);
            return WslConfigSaveResult.Failure(exception.Message);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public readonly struct WslConfigLoadResult
{
    public WslConfigDocument? Document { get; }
    public string Path { get; }
    public string? SavedBaseline { get; }
    public string? Error { get; }
    public bool CreatedFromDefault { get; }

    private WslConfigLoadResult(
        WslConfigDocument? document,
        string path,
        string? savedBaseline,
        string? error,
        bool createdFromDefault)
    {
        Document = document;
        Path = path;
        SavedBaseline = savedBaseline;
        Error = error;
        CreatedFromDefault = createdFromDefault;
    }

    public bool Succeeded => Error is null && Document is not null;

    public static WslConfigLoadResult Loaded(WslConfigDocument document, string path, string baseline) =>
        new(document, path, baseline, null, false);

    public static WslConfigLoadResult CreatedDefault(WslConfigDocument document, string path, string? savedBaseline) =>
        new(document, path, savedBaseline, null, true);

    public static WslConfigLoadResult Failure(string error) =>
        new(null, string.Empty, null, error, false);
}

public readonly struct WslConfigSaveResult
{
    public string? SavedBaseline { get; }
    public IReadOnlyList<string>? ValidationErrors { get; }
    public string? Error { get; }

    private WslConfigSaveResult(string? savedBaseline, IReadOnlyList<string>? validationErrors, string? error)
    {
        SavedBaseline = savedBaseline;
        ValidationErrors = validationErrors;
        Error = error;
    }

    public bool Succeeded => Error is null && ValidationErrors is null && SavedBaseline is not null;

    public static WslConfigSaveResult Success(string savedBaseline) => new(savedBaseline, null, null);

    public static WslConfigSaveResult ValidationFailure(IReadOnlyList<string> errors) => new(null, errors, null);

    public static WslConfigSaveResult Failure(string error) => new(null, null, error);
}
