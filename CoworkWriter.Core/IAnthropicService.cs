using Anthropic.SDK.Messaging;

namespace CoworkWriter.Core;

public interface IAnthropicService
{
    IReadOnlyList<Message> History { get; }
    IAsyncEnumerable<string> StreamMessageAsync(string userMessage, CancellationToken ct = default);
    void ClearHistory();
}
