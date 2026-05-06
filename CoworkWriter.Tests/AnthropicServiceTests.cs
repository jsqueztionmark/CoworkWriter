using Anthropic.SDK.Messaging;
using CoworkWriter.Core;

namespace CoworkWriter.Tests;

public class AnthropicServiceTests
{
    private static AnthropicService MakeService(
        Func<MessageParameters, CancellationToken, IAsyncEnumerable<MessageResponse>>? streamFunc = null)
    {
        streamFunc ??= (_, _) => EmptyStream();
        return new AnthropicService(streamFunc);
    }

    private static async IAsyncEnumerable<MessageResponse> EmptyStream()
    {
        await Task.CompletedTask;
        yield break;
    }

    [Fact]
    public async Task StreamMessageAsync_AddsUserMessageToHistoryBeforeSending()
    {
        MessageParameters? captured = null;

        async IAsyncEnumerable<MessageResponse> Capture(MessageParameters p, CancellationToken _)
        {
            captured = p;
            await Task.CompletedTask;
            yield break;
        }

        var service = MakeService(Capture);
        await foreach (var _ in service.StreamMessageAsync("Hello")) { }

        Assert.NotNull(captured);
        Assert.Single(captured!.Messages);
        Assert.Equal(RoleType.User, captured.Messages[0].Role);
    }

    [Fact]
    public async Task StreamMessageAsync_YieldsDeltaTextFromStream()
    {
        async IAsyncEnumerable<MessageResponse> FakeStream(MessageParameters _, CancellationToken __)
        {
            yield return new MessageResponse { Delta = new Delta { Text = "Hello" } };
            yield return new MessageResponse { Delta = new Delta { Text = " world" } };
            await Task.CompletedTask;
        }

        var service = MakeService(FakeStream);
        var chunks = new List<string>();
        await foreach (var chunk in service.StreamMessageAsync("Hi"))
            chunks.Add(chunk);

        Assert.Equal(["Hello", " world"], chunks);
    }

    [Fact]
    public async Task StreamMessageAsync_AccumulatesHistoryAcrossTurns()
    {
        var callCount = 0;
        MessageParameters? lastCapture = null;

        async IAsyncEnumerable<MessageResponse> Capture(MessageParameters p, CancellationToken _)
        {
            callCount++;
            lastCapture = p;
            yield return new MessageResponse { Delta = new Delta { Text = "reply" } };
            await Task.CompletedTask;
        }

        var service = MakeService(Capture);

        await foreach (var _ in service.StreamMessageAsync("First")) { }
        await foreach (var _ in service.StreamMessageAsync("Second")) { }

        Assert.Equal(2, callCount);
        // History should contain: user1, assistant1, user2
        Assert.Equal(3, lastCapture!.Messages.Count);
        Assert.Equal(RoleType.User, lastCapture.Messages[0].Role);
        Assert.Equal(RoleType.Assistant, lastCapture.Messages[1].Role);
        Assert.Equal(RoleType.User, lastCapture.Messages[2].Role);
    }

    [Fact]
    public async Task ClearHistory_ResetsHistoryToEmpty()
    {
        MessageParameters? lastCapture = null;

        async IAsyncEnumerable<MessageResponse> Capture(MessageParameters p, CancellationToken _)
        {
            lastCapture = p;
            await Task.CompletedTask;
            yield break;
        }

        var service = MakeService(Capture);

        await foreach (var _ in service.StreamMessageAsync("First")) { }
        service.ClearHistory();
        await foreach (var _ in service.StreamMessageAsync("After clear")) { }

        Assert.Single(lastCapture!.Messages);
    }

    [Fact]
    public async Task StreamMessageAsync_SkipsNullDeltaText()
    {
        async IAsyncEnumerable<MessageResponse> FakeStream(MessageParameters _, CancellationToken __)
        {
            yield return new MessageResponse { Delta = null };
            yield return new MessageResponse { Delta = new Delta { Text = null } };
            yield return new MessageResponse { Delta = new Delta { Text = "visible" } };
            await Task.CompletedTask;
        }

        var service = MakeService(FakeStream);
        var chunks = new List<string>();
        await foreach (var chunk in service.StreamMessageAsync("Hi"))
            chunks.Add(chunk);

        Assert.Single(chunks);
        Assert.Equal("visible", chunks[0]);
    }

    [Fact]
    public async Task StreamMessageAsync_SetsPromptCachingToFineGrained()
    {
        MessageParameters? captured = null;

        async IAsyncEnumerable<MessageResponse> Capture(MessageParameters p, CancellationToken _)
        {
            captured = p;
            await Task.CompletedTask;
            yield break;
        }

        var service = MakeService(Capture);
        await foreach (var _ in service.StreamMessageAsync("Hello")) { }

        Assert.Equal(PromptCacheType.FineGrained, captured!.PromptCaching);
    }

    [Fact]
    public async Task StreamMessageAsync_SystemPromptUsesOneHourTTL()
    {
        MessageParameters? captured = null;

        async IAsyncEnumerable<MessageResponse> Capture(MessageParameters p, CancellationToken _)
        {
            captured = p;
            await Task.CompletedTask;
            yield break;
        }

        var service = MakeService(Capture);
        service.SystemPrompt = "You are a writing assistant.";
        await foreach (var _ in service.StreamMessageAsync("Hello")) { }

        Assert.NotNull(captured!.System);
        var cache = captured.System![0].CacheControl;
        Assert.NotNull(cache);
        Assert.Equal(CacheControlType.ephemeral, cache!.Type);
        Assert.Equal(CacheDuration.OneHour, cache.TTL);
    }

    [Fact]
    public void SetCacheBreakpoints_MarksLastAssistantMessage()
    {
        var service = MakeService();

        var history = new ConversationHistory();
        history.AddUserMessage("Hello");
        history.AddAssistantMessage("Hi there");
        history.AddUserMessage("Follow up");
        service.LoadHistory(history);

        service.SetCacheBreakpoints();

        var messages = service.History;
        Assert.Null(messages[0].Content[^1].CacheControl);
        Assert.NotNull(messages[1].Content[^1].CacheControl);
        Assert.Equal(CacheControlType.ephemeral, messages[1].Content[^1].CacheControl!.Type);
        Assert.Null(messages[2].Content[^1].CacheControl);
    }

    [Fact]
    public void SetCacheBreakpoints_ClearsPreviousBreakpoints()
    {
        var service = MakeService();

        var history = new ConversationHistory();
        history.AddUserMessage("Q1");
        history.AddAssistantMessage("A1");
        history.AddUserMessage("Q2");
        history.AddAssistantMessage("A2");
        service.LoadHistory(history);

        service.SetCacheBreakpoints();

        Assert.Null(service.History[1].Content[^1].CacheControl);
        Assert.NotNull(service.History[3].Content[^1].CacheControl);
    }

    [Fact]
    public void SetCacheBreakpoints_NoAssistantMessages_NoCacheSet()
    {
        var service = MakeService();

        var history = new ConversationHistory();
        history.AddUserMessage("Hello");
        service.LoadHistory(history);

        service.SetCacheBreakpoints();

        Assert.Null(service.History[0].Content[^1].CacheControl);
    }

    [Fact]
    public async Task StreamMessageAsync_TracksCacheStats()
    {
        async IAsyncEnumerable<MessageResponse> FakeStream(MessageParameters _, CancellationToken __)
        {
            yield return new MessageResponse { Delta = new Delta { Text = "reply" } };
            yield return new MessageResponse
            {
                Usage = new Usage { CacheCreationInputTokens = 500, CacheReadInputTokens = 1200 }
            };
            await Task.CompletedTask;
        }

        var service = MakeService(FakeStream);
        await foreach (var _ in service.StreamMessageAsync("Hi")) { }

        Assert.NotNull(service.LastCacheStats);
        Assert.Equal(500, service.LastCacheStats!.CacheCreationTokens);
        Assert.Equal(1200, service.LastCacheStats.CacheReadTokens);
    }
}
