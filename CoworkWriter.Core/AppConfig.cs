using Anthropic.SDK.Constants;

namespace CoworkWriter.Core;

public record AppConfig(string ApiKey, string Model = AnthropicModels.Claude46Sonnet);
