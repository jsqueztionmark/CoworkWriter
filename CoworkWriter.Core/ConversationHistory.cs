using Anthropic.SDK.Messaging;

namespace CoworkWriter.Core;

public class ConversationHistory
{
    private readonly List<Message> _messages = new();

    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();

    public void AddUserMessage(string text) =>
        _messages.Add(new Message(RoleType.User, text));

    public void AddAssistantMessage(string text) =>
        _messages.Add(new Message(RoleType.Assistant, text));

    public void AddStreamingResponse(List<MessageResponse> responses) =>
        _messages.Add(new Message(responses));

    public void Clear() => _messages.Clear();
}
