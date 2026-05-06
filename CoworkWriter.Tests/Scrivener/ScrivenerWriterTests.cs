using CoworkWriter.Core.Scrivener;

namespace CoworkWriter.Tests.Scrivener;

public class ScrivenerWriterTests : IDisposable
{
    private readonly string _tempDir;

    public ScrivenerWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"coworkwriter_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateMinimalScrivProject()
    {
        var scrivPath = Path.Combine(_tempDir, "Test.scriv");
        Directory.CreateDirectory(scrivPath);
        Directory.CreateDirectory(Path.Combine(scrivPath, "Files", "Data"));

        var scrivx = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<ScrivenerProject>
  <Binder>
    <BinderItem UUID=""EXISTING-001"" Type=""Folder"">
      <Title>Manuscript</Title>
      <Children>
      </Children>
    </BinderItem>
  </Binder>
</ScrivenerProject>";

        File.WriteAllText(Path.Combine(scrivPath, "Test.scrivx"), scrivx);
        return scrivPath;
    }

    [Fact]
    public void WriteDocument_CreatesContentRtfInDataFolder()
    {
        var scrivPath = CreateMinimalScrivProject();
        var parseResult = ScrivenerParser.Parse(scrivPath);
        Assert.True(parseResult.Success);

        var writer = new ScrivenerWriter();
        var result = writer.WriteDocument(parseResult.Project!, "EXISTING-001", "New Scene", "Hello world.");

        Assert.True(result.Success);
        Assert.NotNull(result.DocumentId);

        var rtfPath = Path.Combine(scrivPath, "Files", "Data", result.DocumentId!, "content.rtf");
        Assert.True(File.Exists(rtfPath));

        var rtf = File.ReadAllText(rtfPath);
        Assert.Contains("Hello world.", rtf);
    }

    [Fact]
    public void WriteDocument_AddsBinderItemToScrivx()
    {
        var scrivPath = CreateMinimalScrivProject();
        var parseResult = ScrivenerParser.Parse(scrivPath);
        Assert.True(parseResult.Success);

        var writer = new ScrivenerWriter();
        var result = writer.WriteDocument(parseResult.Project!, "EXISTING-001", "New Scene", "Content");

        Assert.True(result.Success);

        var reparse = ScrivenerParser.Parse(scrivPath);
        Assert.True(reparse.Success);

        var allItems = reparse.Project!.AllItems().ToList();
        Assert.Contains(allItems, i => i.Title == "New Scene" && i.Id == result.DocumentId);
    }

    [Fact]
    public void WriteDocument_DoesNotCorruptExistingBinderItems()
    {
        var scrivPath = CreateMinimalScrivProject();
        var parseResult = ScrivenerParser.Parse(scrivPath);
        var originalItems = parseResult.Project!.AllItems().ToList();

        var writer = new ScrivenerWriter();
        writer.WriteDocument(parseResult.Project!, "EXISTING-001", "New Scene", "Content");

        var reparse = ScrivenerParser.Parse(scrivPath);
        var newItems = reparse.Project!.AllItems().ToList();

        foreach (var orig in originalItems)
            Assert.Contains(newItems, i => i.Id == orig.Id && i.Title == orig.Title);
    }

    [Fact]
    public void UpdateDocument_OverwritesExistingContent()
    {
        var scrivPath = CreateMinimalScrivProject();
        var parseResult = ScrivenerParser.Parse(scrivPath);

        var writer = new ScrivenerWriter();
        var createResult = writer.WriteDocument(parseResult.Project!, "EXISTING-001", "Scene", "Original text");
        Assert.True(createResult.Success);

        var updateResult = writer.UpdateDocument(parseResult.Project!, createResult.DocumentId!, "Updated text");
        Assert.True(updateResult.Success);

        var rtfPath = Path.Combine(scrivPath, "Files", "Data", createResult.DocumentId!, "content.rtf");
        var rtf = File.ReadAllText(rtfPath);
        Assert.Contains("Updated text", rtf);
        Assert.DoesNotContain("Original text", rtf);
    }

    [Fact]
    public void UpdateDocument_NonexistentId_ReturnsError()
    {
        var scrivPath = CreateMinimalScrivProject();
        var parseResult = ScrivenerParser.Parse(scrivPath);

        var writer = new ScrivenerWriter();
        var result = writer.UpdateDocument(parseResult.Project!, "NONEXISTENT-ID", "text");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!);
    }

    [Fact]
    public void WriteDocument_EmptyFolderPath_ReturnsError()
    {
        var binder = new List<BinderItem> { new("id", "Title", "Text", []) };
        var project = new ScrivenerProject(binder, _ => null);

        var writer = new ScrivenerWriter();
        var result = writer.WriteDocument(project, "id", "New", "text");

        Assert.False(result.Success);
        Assert.Contains("folder path", result.Error!);
    }

    [Fact]
    public void PlainTextToRtf_ProducesValidRtfWithContent()
    {
        var rtf = ScrivenerWriter.PlainTextToRtf("Hello\nWorld");

        Assert.StartsWith(@"{\rtf1", rtf);
        Assert.EndsWith("}", rtf);
        Assert.Contains("Hello", rtf);
        Assert.Contains(@"\par ", rtf);
        Assert.Contains("World", rtf);
    }

    [Fact]
    public void PlainTextToRtf_EscapesSpecialCharacters()
    {
        var rtf = ScrivenerWriter.PlainTextToRtf(@"A {brace} and \backslash");

        Assert.Contains(@"\{", rtf);
        Assert.Contains(@"\}", rtf);
        Assert.Contains(@"\\", rtf);
    }

    [Fact]
    public void PlainTextToRtf_HandlesUnicodeCharacters()
    {
        var rtf = ScrivenerWriter.PlainTextToRtf("café");

        Assert.Contains(@"\u233?", rtf);
    }
}
