using System.Text.Json;
using Anthropic.SDK.Constants;

namespace CoworkWriter.Core.Settings;

public class SettingsStore
{
    private readonly string _path;

    public static readonly string[] AllowedModels =
    [
        AnthropicModels.Claude46Sonnet,
        AnthropicModels.Claude46Opus,
        AnthropicModels.Claude45Sonnet,
        AnthropicModels.Claude45Haiku,
    ];

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "CoworkWriter", "settings.json");

    public SettingsStore() : this(DefaultPath()) { }

    public SettingsStore(string path) => _path = path;

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public AppSettings Load()
    {
        AppSettings settings;
        if (!File.Exists(_path))
            settings = new AppSettings();
        else
        {
            try
            {
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
            }
            catch (JsonException)
            {
                settings = new AppSettings();
            }
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            var envKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
                settings = settings with { ApiKey = envKey };
        }

        return settings;
    }

    public static IEnumerable<string> Validate(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            yield return "API key is required.";
        if (!AllowedModels.Contains(settings.Model))
            yield return $"Unknown model: {settings.Model}";
    }
}
