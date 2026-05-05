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
}
