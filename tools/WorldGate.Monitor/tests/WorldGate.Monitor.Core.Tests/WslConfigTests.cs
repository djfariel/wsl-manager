using WorldGate.Monitor.Core.WslConfig;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class WslConfigTests
{
    [Fact]
    public void ParseAndSerializeRoundTripPreservesKnownAndExtraKeys()
    {
        const string input = """
        ; comment
        [wsl2]
        memory=48GB
        processors=32
        swap=8GB
        localhostForwarding=true
        customExperimental=value
        """;

        WslConfigParseResult parsed = WslConfigIniSerializer.TryParse(input);
        Assert.True(parsed.Succeeded, parsed.Error);
        WslConfigDocument document = parsed.Document!;
        Assert.Equal("48GB", document.Memory);
        Assert.Equal("32", document.Processors);
        Assert.Equal("8GB", document.Swap);
        Assert.Equal("true", document.LocalhostForwarding);
        Assert.Equal("value", document.ExtraWsl2Settings["customExperimental"]);

        string output = WslConfigIniSerializer.Serialize(document);
        WslConfigParseResult reparsed = WslConfigIniSerializer.TryParse(output);
        Assert.True(reparsed.Succeeded, reparsed.Error);
        Assert.Equal(document.Memory, reparsed.Document!.Memory);
        Assert.Equal(document.Processors, reparsed.Document.Processors);
        Assert.Equal(document.Swap, reparsed.Document.Swap);
        Assert.Equal(document.LocalhostForwarding, reparsed.Document.LocalhostForwarding);
        Assert.Equal("value", reparsed.Document.ExtraWsl2Settings["customExperimental"]);
    }

    [Fact]
    public void ValidatorRejectsInvalidProcessorAndBooleanValues()
    {
        var document = new WslConfigDocument
        {
            Processors = "0",
            LocalhostForwarding = "maybe",
            Memory = "lots",
        };

        IReadOnlyList<string> errors = WslConfigValidator.Validate(document);

        Assert.Contains(errors, e => e.Contains("processors", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("localhostForwarding", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("memory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParseNormalizesBooleanCasingForStableBaseline()
    {
        const string input = """
        [wsl2]
        localhostForwarding=True
        nestedVirtualization=FALSE
        """;

        WslConfigParseResult parsed = WslConfigIniSerializer.TryParse(input);
        Assert.True(parsed.Succeeded, parsed.Error);
        Assert.Equal("true", parsed.Document!.LocalhostForwarding);
        Assert.Equal("false", parsed.Document.NestedVirtualization);

        string baseline = WslConfigIniSerializer.Serialize(parsed.Document);
        WslConfigParseResult reparsed = WslConfigIniSerializer.TryParse(baseline);
        Assert.True(reparsed.Succeeded, reparsed.Error);
        Assert.Equal("true", reparsed.Document!.LocalhostForwarding);
        Assert.Equal("false", reparsed.Document.NestedVirtualization);
    }

    [Fact]
    public void ParseNormalizesAutoMemoryReclaimCasing()
    {
        const string input = """
        [wsl2]
        autoMemoryReclaim=GRADUAL
        """;

        WslConfigParseResult parsed = WslConfigIniSerializer.TryParse(input);
        Assert.True(parsed.Succeeded, parsed.Error);
        Assert.Equal("gradual", parsed.Document!.AutoMemoryReclaim);
    }

    [Fact]
    public void FileServiceSavesAtomicallyAndReloads()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "wsl-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string path = Path.Combine(tempDir, ".wslconfig");
        var service = new WslConfigFileService(path);

        try
        {
            WslConfigLoadResult initial = service.LoadOrCreateDefault();
            Assert.True(initial.Succeeded);
            Assert.True(initial.CreatedFromDefault);

            initial.Document!.Memory = "16GB";
            initial.Document.Processors = "8";
            WslConfigSaveResult saved = service.Save(initial.Document);
            Assert.True(saved.Succeeded, saved.Error);

            WslConfigLoadResult reloaded = service.LoadOrCreateDefault();
            Assert.True(reloaded.Succeeded);
            Assert.False(reloaded.CreatedFromDefault);
            Assert.Equal("16GB", reloaded.Document!.Memory);
            Assert.Equal("8", reloaded.Document.Processors);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
