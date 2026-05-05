using Anthropic.SDK.Messaging;
using CoworkWriter.Core;

namespace CoworkWriter.Tests;

public class ConversationHistoryTests
{
    [Fact]
    public void AddUserMessage_AppendsUserRole()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("Hello");

        Assert.Single(history.Messages);
        Assert.Equal(RoleType.User, history.Messages[0].Role);
    }

    [Fact]
    public void AddAssistantMessage_AppendsAssistantRole()
    {
        var history = new ConversationHistory();
        history.AddAssistantMessage("Hi there");

        Assert.Single(history.Messages);
        Assert.Equal(RoleType.Assistant, history.Messages[0].Role);
    }

    [Fact]
    public void Messages_AreOrderedByInsertion()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("first");
        history.AddAssistantMessage("second");
        history.AddUserMessage("third");

        Assert.Equal(3, history.Messages.Count);
        Assert.Equal(RoleType.User, history.Messages[0].Role);
        Assert.Equal(RoleType.Assistant, history.Messages[1].Role);
        Assert.Equal(RoleType.User, history.Messages[2].Role);
    }

    [Fact]
    public void Clear_RemovesAllMessages()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("Hello");
        history.AddAssistantMessage("Hi");
        history.Clear();

        Assert.Empty(history.Messages);
    }

    [Fact]
    public void Messages_IsReadOnly_CannotBeModifiedExternally()
    {
        var history = new ConversationHistory();
        history.AddUserMessage("Hello");

        var messages = history.Messages;
        Assert.IsAssignableFrom<IReadOnlyList<Message>>(messages);
    }
}
