using Anthropic.SDK.Messaging;
using CoworkWriter.Core;
using CoworkWriter.Core.Agentic;

namespace CoworkWriter.Tests.Agentic;

public class ChapterWriterTests
{
    private static AnthropicService MakeService(List<string> responses)
    {
        var callIndex = 0;

        async IAsyncEnumerable<MessageResponse> FakeStream(MessageParameters _, CancellationToken __)
        {
            var text = callIndex < responses.Count ? responses[callIndex] : "";
            callIndex++;
            yield return new MessageResponse { Delta = new Delta { Text = text } };
            await Task.CompletedTask;
        }

        return new AnthropicService(FakeStream);
    }

    [Fact]
    public async Task WriteChapterAsync_ExecutesThreeStepsInOrder()
    {
        var responses = new List<string>
        {
            "1. Scene one\n2. Scene two",
            "It was a dark and stormy night...",
            "The pacing in paragraph 2 could be tightened."
        };
        var service = MakeService(responses);
        var writer = new ChapterWriter(service);

        var results = new List<ChapterProgress>();
        await foreach (var progress in writer.WriteChapterAsync("A chapter about discovery", ""))
            results.Add(progress);

        Assert.Equal(3, results.Count);
        Assert.Equal(ChapterStep.Outline, results[0].Step);
        Assert.Equal(ChapterStep.Draft, results[1].Step);
        Assert.Equal(ChapterStep.Review, results[2].Step);
    }

    [Fact]
    public async Task WriteChapterAsync_EachStepContainsExpectedContent()
    {
        var responses = new List<string>
        {
            "outline content",
            "draft content",
            "review content"
        };
        var service = MakeService(responses);
        var writer = new ChapterWriter(service);

        var results = new List<ChapterProgress>();
        await foreach (var progress in writer.WriteChapterAsync("brief", ""))
            results.Add(progress);

        Assert.Equal("outline content", results[0].Content);
        Assert.Equal("draft content", results[1].Content);
        Assert.Equal("review content", results[2].Content);
    }

    [Fact]
    public async Task WriteChapterAsync_SendsCorrectPromptsToService()
    {
        var prompts = new List<string>();

        async IAsyncEnumerable<MessageResponse> Capture(MessageParameters p, CancellationToken _)
        {
            var userMsg = p.Messages.Last(m => m.Role == RoleType.User);
            var text = string.Concat(userMsg.Content.OfType<TextContent>().Select(c => c.Text));
            prompts.Add(text);
            yield return new MessageResponse { Delta = new Delta { Text = "ok" } };
            await Task.CompletedTask;
        }

        var service = new AnthropicService(Capture);
        var writer = new ChapterWriter(service);

        await foreach (var _ in writer.WriteChapterAsync("test brief", "")) { }

        Assert.Equal(3, prompts.Count);
        Assert.Contains("test brief", prompts[0]);
        Assert.Contains("outline", prompts[0].ToLower());
        Assert.Contains("full chapter draft", prompts[1].ToLower());
        Assert.Contains("review", prompts[2].ToLower());
    }

    [Fact]
    public async Task WriteChapterAsync_SetsSystemPromptWithManuscriptContext()
    {
        string? capturedSystem = null;

        async IAsyncEnumerable<MessageResponse> Capture(MessageParameters p, CancellationToken _)
        {
            capturedSystem ??= p.System?.FirstOrDefault()?.Text;
            yield return new MessageResponse { Delta = new Delta { Text = "ok" } };
            await Task.CompletedTask;
        }

        var service = new AnthropicService(Capture);
        var writer = new ChapterWriter(service);

        await foreach (var _ in writer.WriteChapterAsync("brief", "Chapter 1 was about rain.")) { }

        Assert.NotNull(capturedSystem);
        Assert.Contains("Chapter 1 was about rain.", capturedSystem!);
    }

    [Fact]
    public async Task WriteChapterAsync_ClearsHistoryBeforeStarting()
    {
        int messageCountOnFirstCall = -1;

        async IAsyncEnumerable<MessageResponse> Capture(MessageParameters p, CancellationToken _)
        {
            if (messageCountOnFirstCall == -1)
                messageCountOnFirstCall = p.Messages.Count;
            yield return new MessageResponse { Delta = new Delta { Text = "ok" } };
            await Task.CompletedTask;
        }

        var service = new AnthropicService(Capture);
        // Pre-fill some history
        await foreach (var _ in service.StreamMessageAsync("old message")) { }

        var writer = new ChapterWriter(service);
        await foreach (var _ in writer.WriteChapterAsync("brief", "")) { }

        Assert.Equal(1, messageCountOnFirstCall);
    }

    [Fact]
    public void BuildOutlinePrompt_ContainsChapterBrief()
    {
        var prompt = ChapterWriter.BuildOutlinePrompt("A betrayal at midnight");
        Assert.Contains("A betrayal at midnight", prompt);
        Assert.Contains("outline", prompt.ToLower());
    }

    [Fact]
    public void BuildDraftPrompt_ReferencesOutline()
    {
        var prompt = ChapterWriter.BuildDraftPrompt();
        Assert.Contains("outline", prompt.ToLower());
    }

    [Fact]
    public void BuildReviewPrompt_AsksForReview()
    {
        var prompt = ChapterWriter.BuildReviewPrompt();
        Assert.Contains("review", prompt.ToLower());
    }
}
