using System.Text;

namespace CoworkWriter.Core.Agentic;

public class ChapterWriter
{
    private readonly IAnthropicService _service;

    public ChapterWriter(IAnthropicService service)
    {
        _service = service;
    }

    public async IAsyncEnumerable<ChapterProgress> WriteChapterAsync(
        string chapterBrief,
        string manuscriptContext,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        _service.ClearHistory();

        var systemPrompt = BuildSystemPrompt(manuscriptContext);
        _service.SystemPrompt = systemPrompt;

        var outline = await RunStepAsync(
            BuildOutlinePrompt(chapterBrief), ct);
        yield return new ChapterProgress(ChapterStep.Outline, outline);

        var draft = await RunStepAsync(
            BuildDraftPrompt(), ct);
        yield return new ChapterProgress(ChapterStep.Draft, draft);

        var review = await RunStepAsync(
            BuildReviewPrompt(), ct);
        yield return new ChapterProgress(ChapterStep.Review, review);
    }

    private async Task<string> RunStepAsync(string prompt, CancellationToken ct)
    {
        var sb = new StringBuilder();
        await foreach (var chunk in _service.StreamMessageAsync(prompt, ct))
            sb.Append(chunk);
        return sb.ToString();
    }

    private static string BuildSystemPrompt(string manuscriptContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a skilled novelist collaborating on a book. Write in the same style, voice, and tone as the existing manuscript.");
        if (!string.IsNullOrWhiteSpace(manuscriptContext))
        {
            sb.AppendLine();
            sb.AppendLine("# Existing Manuscript Context");
            sb.AppendLine(manuscriptContext);
        }
        return sb.ToString();
    }

    internal static string BuildOutlinePrompt(string chapterBrief) =>
        $"Create a detailed scene-by-scene outline for the following chapter:\n\n{chapterBrief}\n\nInclude: key events, character beats, emotional arc, and pacing notes. Format as a numbered list.";

    internal static string BuildDraftPrompt() =>
        "Now write the full chapter draft based on the outline above. Write complete prose — not a summary. Maintain consistent voice, pacing, and characterization throughout.";

    internal static string BuildReviewPrompt() =>
        "Review the draft you just wrote. Identify: pacing issues, unclear prose, dialogue that feels unnatural, continuity errors, and areas that need strengthening. Then provide a revised version of any paragraphs that need improvement.";
}
