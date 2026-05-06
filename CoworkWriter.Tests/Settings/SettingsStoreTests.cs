using CoworkWriter.Core.Settings;

namespace CoworkWriter.Tests.Settings;

public class SettingsStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsStore _store;

    public SettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _store = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Load_NonExistentFile_ReturnsDefaults()
    {
        var saved = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
            var loaded = _store.Load();
            Assert.Equal(string.Empty, loaded.ApiKey);
            Assert.False(string.IsNullOrWhiteSpace(loaded.Model));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", saved);
        }
    }

    [Fact]
    public void Load_NoSavedKey_FallsBackToEnvironmentVariable()
    {
        var saved = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-env-key");
            var loaded = _store.Load();
            Assert.Equal("sk-ant-env-key", loaded.ApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", saved);
        }
    }

    [Fact]
    public void Load_NoSavedKey_NoEnvVar_ReturnsEmptyApiKey()
    {
        var saved = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
            var loaded = _store.Load();
            Assert.Equal(string.Empty, loaded.ApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", saved);
        }
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllFields()
    {
        var settings = new AppSettings(
            ApiKey: "sk-ant-test-key",
            Model: SettingsStore.AllowedModels[0],
            DefaultSystemPrompt: "You are a noir fiction assistant.");

        _store.Save(settings);
        var loaded = _store.Load();

        Assert.Equal(settings.ApiKey, loaded.ApiKey);
        Assert.Equal(settings.Model, loaded.Model);
        Assert.Equal(settings.DefaultSystemPrompt, loaded.DefaultSystemPrompt);
    }

    [Fact]
    public void Load_SavedKeyPresent_IgnoresEnvironmentVariable()
    {
        var saved = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-env-key");
            _store.Save(new AppSettings(ApiKey: "sk-ant-saved-key"));
            var loaded = _store.Load();
            Assert.Equal("sk-ant-saved-key", loaded.ApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", saved);
        }
    }

    [Fact]
    public void Validate_EmptyApiKey_ReturnsError()
    {
        var settings = new AppSettings(ApiKey: "");
        var errors = SettingsStore.Validate(settings).ToList();
        Assert.Contains(errors, e => e.Contains("API key"));
    }

    [Fact]
    public void Validate_WhitespaceApiKey_ReturnsError()
    {
        var settings = new AppSettings(ApiKey: "   ");
        var errors = SettingsStore.Validate(settings).ToList();
        Assert.Contains(errors, e => e.Contains("API key"));
    }

    [Fact]
    public void Validate_UnknownModel_ReturnsError()
    {
        var settings = new AppSettings(ApiKey: "sk-ant-valid", Model: "gpt-99-turbo");
        var errors = SettingsStore.Validate(settings).ToList();
        Assert.Contains(errors, e => e.Contains("model") || e.Contains("Model"));
    }

    [Fact]
    public void Validate_ValidSettings_ReturnsNoErrors()
    {
        var settings = new AppSettings(
            ApiKey: "sk-ant-valid",
            Model: SettingsStore.AllowedModels[0]);
        var errors = SettingsStore.Validate(settings).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void AllowedModels_ContainsAtLeastOneEntry()
    {
        Assert.NotEmpty(SettingsStore.AllowedModels);
    }

    [Fact]
    public void DefaultPath_IsInsideConfigDirectory()
    {
        var path = SettingsStore.DefaultPath();
        Assert.Contains(".config", path);
        Assert.EndsWith(".json", path);
    }
}
