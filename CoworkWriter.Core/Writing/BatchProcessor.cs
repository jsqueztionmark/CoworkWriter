using System.Text;
using CoworkWriter.Core.Scrivener;

namespace CoworkWriter.Core.Writing;

public record BatchResult(string DocumentId, string Title, bool Success, string Content, string? Error);

public class BatchProcessor
{
    private readonly IAnthropicService _service;

    public BatchProcessor(IAnthropicService service)
    {
        _service = service;
    }

    public async IAsyncEnumerable<BatchResult> ProcessAsync(
        ScrivenerProject project,
        IEnumerable<string> documentIds,
        string instruction,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var allItems = project.AllItems().ToDictionary(i => i.Id);

        foreach (var id in documentIds)
        {
            if (ct.IsCancellationRequested) yield break;

            if (!allItems.TryGetValue(id, out var item))
            {
                yield return new BatchResult(id, "(unknown)", false, string.Empty, $"Document not found: {id}");
                continue;
            }

            var doc = project.LoadDocument(item);
            if (doc is null || string.IsNullOrWhiteSpace(doc.PlainText))
            {
                yield return new BatchResult(id, item.Title, false, string.Empty, "Document has no content.");
                continue;
            }

            yield return await ProcessSingleAsync(item, doc, instruction, ct);
        }
    }

    private async Task<BatchResult> ProcessSingleAsync(
        BinderItem item, ScrivenerDocument doc, string instruction, CancellationToken ct)
    {
        _service.ClearHistory();
        _service.SystemPrompt = "You are a skilled editor. Apply the requested edit precisely and return only the revised text.";

        var prompt = $"Apply the following edit to this text:\n\nEdit instruction: {instruction}\n\nText:\n{doc.PlainText}\n\nReturn only the revised text.";

        try
        {
            var sb = new StringBuilder();
            await foreach (var chunk in _service.StreamMessageAsync(prompt, ct))
                sb.Append(chunk);
            return new BatchResult(item.Id, item.Title, true, sb.ToString(), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new BatchResult(item.Id, item.Title, false, string.Empty, ex.Message);
        }
    }
}
