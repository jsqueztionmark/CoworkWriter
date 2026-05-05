using CoworkWriter.Core.Scrivener;

namespace CoworkWriter.Tests.Scrivener;

public class ContextBuilderTests
{
    private static BinderItem Item(string id, string title) =>
        new(id, title, "Text", []);

    private static ScrivenerProject ProjectWith(params (string id, string title, string text)[] docs)
    {
        var items = docs.Select(d => Item(d.id, d.title)).ToList<BinderItem>();
        var lookup = docs.ToDictionary(d => d.id, d => new ScrivenerDocument(d.id, d.title, d.text));
        return new ScrivenerProject(items, item => lookup.GetValueOrDefault(item.Id));
    }

    private readonly ContextBuilder _builder = new();

    [Fact]
    public void Build_EmptySelection_ReturnsEmpty()
    {
        var project = ProjectWith(("1", "Ch1", "Some text"));
        var result = _builder.Build(project, []);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Build_SingleDocument_ContainsTitleAndText()
    {
        var project = ProjectWith(("1", "Chapter One", "It was a dark night."));
        var result = _builder.Build(project, ["1"]);

        Assert.Contains("Chapter One", result);
        Assert.Contains("It was a dark night.", result);
    }

    [Fact]
    public void Build_MultipleDocuments_ConcatenatedInBinderOrder()
    {
        var project = ProjectWith(
            ("1", "Scene A", "First scene."),
            ("2", "Scene B", "Second scene."));

        var result = _builder.Build(project, ["1", "2"]);

        var posA = result.IndexOf("Scene A", StringComparison.Ordinal);
        var posB = result.IndexOf("Scene B", StringComparison.Ordinal);
        Assert.True(posA < posB);
    }

    [Fact]
    public void Build_SelectionWithUnknownId_SkipsUnknown()
    {
        var project = ProjectWith(("1", "Real", "Content"));
        var result = _builder.Build(project, ["1", "MISSING"]);

        Assert.Contains("Content", result);
    }

    [Fact]
    public void Build_OverLimitDocument_IsTruncated()
    {
        var longText = new string('x', 100_000);
        var project = ProjectWith(("1", "Big Doc", longText));

        var result = _builder.Build(project, ["1"]);

        Assert.Contains("[...truncated]", result);
        Assert.True(result.Length < longText.Length);
    }

    [Fact]
    public void Build_DocumentWithNoContent_IsSkipped()
    {
        var project = ProjectWith(("1", "Empty", ""));
        var result = _builder.Build(project, ["1"]);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Build_NullDocument_IsSkipped()
    {
        var items = new List<BinderItem> { Item("1", "Ghost") };
        var project = new ScrivenerProject(items, _ => null);

        var result = _builder.Build(project, ["1"]);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Build_PinnedDoc_AppearsEvenWhenNotSelected()
    {
        var project = ProjectWith(("1", "Character Notes", "Elara is brave."));
        var result = _builder.Build(project, selectedIds: [], pinnedIds: ["1"]);

        Assert.Contains("Character Notes", result);
        Assert.Contains("Elara is brave.", result);
    }

    [Fact]
    public void Build_PinnedDoc_AppearsBeforeSelectedDoc()
    {
        var project = ProjectWith(
            ("1", "Scene One", "Opening action."),
            ("2", "World Notes", "Magic system rules."));

        var result = _builder.Build(project, selectedIds: ["1"], pinnedIds: ["2"]);

        var posPin = result.IndexOf("World Notes", StringComparison.Ordinal);
        var posSel = result.IndexOf("Scene One", StringComparison.Ordinal);
        Assert.True(posPin < posSel);
    }

    [Fact]
    public void Build_PinnedAndSelected_NoDuplication()
    {
        var project = ProjectWith(("1", "Shared Doc", "Some content."));
        var result = _builder.Build(project, selectedIds: ["1"], pinnedIds: ["1"]);

        Assert.Single(result.Split("Shared Doc").Skip(1).ToArray());
    }

    [Fact]
    public void Build_EmptySelectionAndPins_ReturnsEmpty()
    {
        var project = ProjectWith(("1", "Doc", "Text."));
        var result = _builder.Build(project, selectedIds: [], pinnedIds: []);

        Assert.Equal(string.Empty, result);
    }
}
