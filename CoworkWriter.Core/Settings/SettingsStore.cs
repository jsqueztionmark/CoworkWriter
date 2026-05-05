using System.Text.Json;
using Anthropic.SDK.Constants;

namespace CoworkWriter.Core.Settings;

public class SettingsStore
{
    public static readonly string[] AllowedModels =
    [
        AnthropicModels.Claude46Sonnet,
        AnthropicModels.Claude46Opus,
        AnthropicModels.Claude45Sonnet,
        AnthropicModels.Claude45Haiku,
    ];

    public static string SettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "CoworkWriter", "settings.json");

    public void Save(AppSettings settings)
    {
        var path = SettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public AppSettings Load()
    {
        var path = SettingsPath();
        AppSettings settings;
        if (!File.Exists(path))
            settings = new AppSettings();
        else
        {
            try
            {
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
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
