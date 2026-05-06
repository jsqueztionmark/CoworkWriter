using Anthropic.SDK.Messaging;
using CoworkWriter.Core;
using CoworkWriter.Core.Scrivener;
using CoworkWriter.Core.Writing;

namespace CoworkWriter.Tests.Writing;

public class BatchProcessorTests
{
    private static (AnthropicService service, List<string> capturedPrompts) MakeCapturingService(string response = "edited")
    {
        var prompts = new List<string>();

        async IAsyncEnumerable<MessageResponse> FakeStream(MessageParameters p, CancellationToken _)
        {
            var userMsg = p.Messages.Last(m => m.Role == RoleType.User);
            var text = string.Concat(userMsg.Content.OfType<TextContent>().Select(c => c.Text));
            prompts.Add(text);
            yield return new MessageResponse { Delta = new Delta { Text = response } };
            await Task.CompletedTask;
        }

        return (new AnthropicService(FakeStream), prompts);
    }

    private static ScrivenerProject MakeTestProject(params (string id, string title, string text)[] docs)
    {
        var items = docs.Select(d => new BinderItem(d.id, d.title, "Text", [])).ToList();
        var docMap = docs.ToDictionary(d => d.id, d => new ScrivenerDocument(d.id, d.title, d.text));
        return new ScrivenerProject(items, item => docMap.GetValueOrDefault(item.Id));
    }

    [Fact]
    public async Task ProcessAsync_AppliesCommandToEachDocument()
    {
        var (service, prompts) = MakeCapturingService();
        var project = MakeTestProject(
            ("doc1", "Scene 1", "The cat sat."),
            ("doc2", "Scene 2", "The dog ran."));

        var processor = new BatchProcessor(service);
        var results = new List<BatchResult>();
        await foreach (var r in processor.ProcessAsync(project, ["doc1", "doc2"], "Fix grammar"))
            results.Add(r);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(2, prompts.Count);
        Assert.All(prompts, p => Assert.Contains("Fix grammar", p));
    }

    [Fact]
    public async Task ProcessAsync_ReturnsContentFromEachDocument()
    {
        var callIndex = 0;
        async IAsyncEnumerable<MessageResponse> FakeStream(MessageParameters _, CancellationToken __)
        {
            var text = callIndex == 0 ? "result-1" : "result-2";
            callIndex++;
            yield return new MessageResponse { Delta = new Delta { Text = text } };
            await Task.CompletedTask;
        }

        var service = new AnthropicService(FakeStream);
        var project = MakeTestProject(
            ("doc1", "S1", "text1"),
            ("doc2", "S2", "text2"));

        var processor = new BatchProcessor(service);
        var results = new List<BatchResult>();
        await foreach (var r in processor.ProcessAsync(project, ["doc1", "doc2"], "edit"))
            results.Add(r);

        Assert.Equal("result-1", results[0].Content);
        Assert.Equal("result-2", results[1].Content);
    }

    [Fact]
    public async Task ProcessAsync_NonexistentDocument_ReturnsError()
    {
        var (service, _) = MakeCapturingService();
        var project = MakeTestProject(("doc1", "S1", "text"));

        var processor = new BatchProcessor(service);
        var results = new List<BatchResult>();
        await foreach (var r in processor.ProcessAsync(project, ["missing-id"], "edit"))
            results.Add(r);

        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Contains("not found", results[0].Error!.ToLower());
    }

    [Fact]
    public async Task ProcessAsync_EmptyDocument_ReturnsError()
    {
        var items = new List<BinderItem> { new("doc1", "Empty", "Text", []) };
        var project = new ScrivenerProject(items, _ => new ScrivenerDocument("doc1", "Empty", ""));

        var (service, _) = MakeCapturingService();
        var processor = new BatchProcessor(service);
        var results = new List<BatchResult>();
        await foreach (var r in processor.ProcessAsync(project, ["doc1"], "edit"))
            results.Add(r);

        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Contains("no content", results[0].Error!.ToLower());
    }

    [Fact]
    public async Task ProcessAsync_HandlesPartialFailureGracefully()
    {
        var callIndex = 0;
        async IAsyncEnumerable<MessageResponse> FakeStream(MessageParameters _, CancellationToken __)
        {
            callIndex++;
            if (callIndex == 1)
                throw new InvalidOperationException("API error");
            yield return new MessageResponse { Delta = new Delta { Text = "success" } };
            await Task.CompletedTask;
        }

        var service = new AnthropicService(FakeStream);
        var project = MakeTestProject(
            ("doc1", "S1", "text1"),
            ("doc2", "S2", "text2"));

        var processor = new BatchProcessor(service);
        var results = new List<BatchResult>();
        await foreach (var r in processor.ProcessAsync(project, ["doc1", "doc2"], "edit"))
            results.Add(r);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].Success);
        Assert.Contains("API error", results[0].Error!);
        Assert.True(results[1].Success);
        Assert.Equal("success", results[1].Content);
    }

    [Fact]
    public async Task ProcessAsync_IncludesDocumentTextInPrompt()
    {
        var (service, prompts) = MakeCapturingService();
        var project = MakeTestProject(("doc1", "S1", "The specific content here."));

        var processor = new BatchProcessor(service);
        await foreach (var _ in processor.ProcessAsync(project, ["doc1"], "edit")) { }

        Assert.Single(prompts);
        Assert.Contains("The specific content here.", prompts[0]);
    }

    [Fact]
    public async Task ProcessAsync_ClearsHistoryBetweenDocuments()
    {
        var messageCounts = new List<int>();

        async IAsyncEnumerable<MessageResponse> FakeStream(MessageParameters p, CancellationToken _)
        {
            messageCounts.Add(p.Messages.Count);
            yield return new MessageResponse { Delta = new Delta { Text = "ok" } };
            await Task.CompletedTask;
        }

        var service = new AnthropicService(FakeStream);
        var project = MakeTestProject(
            ("doc1", "S1", "text1"),
            ("doc2", "S2", "text2"));

        var processor = new BatchProcessor(service);
        await foreach (var _ in processor.ProcessAsync(project, ["doc1", "doc2"], "edit")) { }

        Assert.Equal(2, messageCounts.Count);
        Assert.Equal(1, messageCounts[0]);
        Assert.Equal(1, messageCounts[1]);
    }
}
