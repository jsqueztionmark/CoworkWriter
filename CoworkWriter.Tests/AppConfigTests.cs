using Anthropic.SDK.Constants;
using CoworkWriter.Core;

namespace CoworkWriter.Tests;

public class AppConfigTests
{
    [Fact]
    public void AppConfig_StoresApiKey()
    {
        var config = new AppConfig("test-key");
        Assert.Equal("test-key", config.ApiKey);
    }

    [Fact]
    public void AppConfig_DefaultsToSonnet()
    {
        var config = new AppConfig("test-key");
        Assert.Equal(AnthropicModels.Claude46Sonnet, config.Model);
    }

    [Fact]
    public void AppConfig_AcceptsCustomModel()
    {
        var config = new AppConfig("test-key", AnthropicModels.Claude46Opus);
        Assert.Equal(AnthropicModels.Claude46Opus, config.Model);
    }
}
