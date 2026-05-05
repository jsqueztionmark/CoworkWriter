using Anthropic.SDK.Messaging;

namespace CoworkWriter.Core;

public interface IAnthropicService
{
    IReadOnlyList<Message> History { get; }
    string? SystemPrompt { get; set; }
    IAsyncEnumerable<string> StreamMessageAsync(string userMessage, CancellationToken ct = default);
    void ClearHistory();
    void LoadHistory(ConversationHistory history);
}
