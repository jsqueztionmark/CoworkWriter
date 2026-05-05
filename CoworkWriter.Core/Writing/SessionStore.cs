using System.Text.Json;
using Anthropic.SDK.Messaging;

namespace CoworkWriter.Core.Writing;

public class SessionStore
{
    private record StoredMessage(string Role, string Text);

    public static string SessionPath(string scrivPath) =>
        Path.Combine(scrivPath, "coworkwriter-session.json");

    public void Save(string path, IReadOnlyList<Message> messages)
    {
        var stored = messages
            .Select(m => new StoredMessage(
                m.Role == RoleType.User ? "user" : "assistant",
                string.Concat(m.Content.OfType<TextContent>().Select(c => c.Text))))
            .Where(m => !string.IsNullOrEmpty(m.Text))
            .ToList();

        File.WriteAllText(path, JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true }));
    }

    public ConversationHistory Load(string path)
    {
        var history = new ConversationHistory();
        if (!File.Exists(path)) return history;

        try
        {
            var stored = JsonSerializer.Deserialize<List<StoredMessage>>(File.ReadAllText(path));
            if (stored is null) return history;

            foreach (var msg in stored)
            {
                if (msg.Role == "user") history.AddUserMessage(msg.Text);
                else if (msg.Role == "assistant") history.AddAssistantMessage(msg.Text);
            }
        }
        catch (JsonException) { }

        return history;
    }
}
