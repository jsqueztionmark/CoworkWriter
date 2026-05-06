using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using System.Runtime.CompilerServices;

namespace CoworkWriter.Core;

public record CacheStats(int CacheCreationTokens, int CacheReadTokens);

public class AnthropicService : IAnthropicService
{
    private readonly Func<MessageParameters, CancellationToken, IAsyncEnumerable<MessageResponse>> _streamFunc;
    private readonly ConversationHistory _history = new();
    private readonly string _model;

    public AnthropicService(AppConfig config)
    {
        var client = new AnthropicClient(new APIAuthentication(config.ApiKey));
        _streamFunc = (p, ct) => client.Messages.StreamClaudeMessageAsync(p, ct);
        _model = config.Model;
    }

    internal AnthropicService(
        Func<MessageParameters, CancellationToken, IAsyncEnumerable<MessageResponse>> streamFunc,
        string model = "claude-sonnet-4-6-20251001")
    {
        _streamFunc = streamFunc;
        _model = model;
    }

    public IReadOnlyList<Message> History => _history.Messages;
    public string? SystemPrompt { get; set; }
    public CacheStats? LastCacheStats { get; private set; }

    public async IAsyncEnumerable<string> StreamMessageAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _history.AddUserMessage(userMessage);

        SetCacheBreakpoints();

        var parameters = new MessageParameters
        {
            Messages = _history.Messages.ToList(),
            MaxTokens = 4096,
            Model = _model,
            Stream = true,
            Temperature = 1.0m,
            PromptCaching = PromptCacheType.FineGrained,
            System = SystemPrompt is not null
                ? [new SystemMessage(SystemPrompt) { CacheControl = new CacheControl { Type = CacheControlType.ephemeral, TTL = CacheDuration.OneHour } }]
                : null
        };

        var responses = new List<MessageResponse>();

        await foreach (var res in _streamFunc(parameters, ct).WithCancellation(ct))
        {
            if (res.Delta?.Text != null)
                yield return res.Delta.Text;
            responses.Add(res);
        }

        if (responses.Count > 0)
            _history.AddStreamingResponse(responses);

        var usage = responses.LastOrDefault(r => r.Usage != null)?.Usage;
        if (usage != null)
            LastCacheStats = new CacheStats(usage.CacheCreationInputTokens, usage.CacheReadInputTokens);
    }

    internal void SetCacheBreakpoints()
    {
        foreach (var msg in _history.Messages)
            if (msg.Content is { Count: > 0 })
                msg.Content[^1].CacheControl = null;

        var lastAssistant = _history.Messages.LastOrDefault(m => m.Role == RoleType.Assistant);
        if (lastAssistant?.Content is { Count: > 0 })
            lastAssistant.Content[^1].CacheControl = new CacheControl { Type = CacheControlType.ephemeral };
    }

    public void ClearHistory() => _history.Clear();
    public void LoadHistory(ConversationHistory history) => _history.LoadFrom(history);
}
