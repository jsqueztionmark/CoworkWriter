using Anthropic.SDK.Constants;

namespace CoworkWriter.Core.Settings;

public record AppSettings(
    string ApiKey = "",
    string Model = AnthropicModels.Claude46Sonnet,
    string DefaultSystemPrompt = "You are a helpful writing assistant.");
