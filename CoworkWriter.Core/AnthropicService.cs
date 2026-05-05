using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using System.Runtime.CompilerServices;

namespace CoworkWriter.Core;

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

    public async IAsyncEnumerable<string> StreamMessageAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _history.AddUserMessage(userMessage);

        var parameters = new MessageParameters
        {
            Messages = _history.Messages.ToList(),
            MaxTokens = 4096,
            Model = _model,
            Stream = true,
            Temperature = 1.0m
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
    }

    public void ClearHistory() => _history.Clear();
}
