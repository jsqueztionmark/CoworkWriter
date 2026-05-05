using CoworkWriter.Core;
using CoworkWriter.Core.Writing;

namespace CoworkWriter.Tests.Writing;

public class SessionStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SessionStore _store = new();

    public SessionStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string TempPath() => Path.Combine(_tempDir, "session.json");

    private static ConversationHistory BuildHistory(params (string role, string text)[] messages)
    {
        var h = new ConversationHistory();
        foreach (var (role, text) in messages)
        {
            if (role == "user") h.AddUserMessage(text);
            else h.AddAssistantMessage(text);
        }
        return h;
    }

    [Fact]
    public void Save_ThenLoad_ProducesIdenticalMessageCount()
    {
        var history = BuildHistory(("user", "Hello"), ("assistant", "Hi there"), ("user", "How are you?"));
        _store.Save(TempPath(), history.Messages);

        var loaded = _store.Load(TempPath());
        Assert.Equal(history.Messages.Count, loaded.Messages.Count);
    }

    [Fact]
    public void Save_ThenLoad_PreservesRolesAndText()
    {
        var history = BuildHistory(("user", "Write me a scene"), ("assistant", "Here is a scene..."));
        _store.Save(TempPath(), history.Messages);

        var loaded = _store.Load(TempPath());
        Assert.Equal(Anthropic.SDK.Messaging.RoleType.User, loaded.Messages[0].Role);
        Assert.Equal(Anthropic.SDK.Messaging.RoleType.Assistant, loaded.Messages[1].Role);
        Assert.Contains("Write me a scene", loaded.Messages[0].Content
            .OfType<Anthropic.SDK.Messaging.TextContent>().First().Text);
    }

    [Fact]
    public void Load_NonExistentFile_ReturnsEmptyHistory()
    {
        var loaded = _store.Load(Path.Combine(_tempDir, "missing.json"));
        Assert.Empty(loaded.Messages);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsEmptyHistory()
    {
        var path = TempPath();
        File.WriteAllText(path, "this is not valid json {{{");
        var loaded = _store.Load(path);
        Assert.Empty(loaded.Messages);
    }

    [Fact]
    public void SessionPath_IsInsideScrivFolder()
    {
        var path = SessionStore.SessionPath("/home/user/Novel.scriv");
        Assert.StartsWith("/home/user/Novel.scriv", path);
        Assert.EndsWith(".json", path);
    }

    [Fact]
    public void Save_EmptyHistory_WritesValidFile()
    {
        var history = new ConversationHistory();
        _store.Save(TempPath(), history.Messages);

        var loaded = _store.Load(TempPath());
        Assert.Empty(loaded.Messages);
    }
}
