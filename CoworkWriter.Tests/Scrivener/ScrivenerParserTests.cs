using CoworkWriter.Core.Scrivener;

namespace CoworkWriter.Tests.Scrivener;

public class ScrivenerParserTests : IDisposable
{
    private readonly string _tempDir;

    public ScrivenerParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateProject(string scrivxContent)
    {
        var projectDir = Path.Combine(_tempDir, "Test.scriv");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "Test.scrivx"), scrivxContent);
        return projectDir;
    }

    private const string MinimalScrivx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <ScrivenerProject Version="2.0">
          <Binder>
            <BinderItem UUID="AAA-111" Type="DraftFolder">
              <Title>Manuscript</Title>
              <Children>
                <BinderItem UUID="BBB-222" Type="Text">
                  <Title>Chapter One</Title>
                  <Children/>
                </BinderItem>
              </Children>
            </BinderItem>
            <BinderItem UUID="CCC-333" Type="ResearchFolder">
              <Title>Research</Title>
              <Children/>
            </BinderItem>
          </Binder>
        </ScrivenerProject>
        """;

    [Fact]
    public void Parse_ValidScrivx_ReturnsBinder()
    {
        var path = CreateProject(MinimalScrivx);
        var result = ScrivenerParser.Parse(path);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(2, result.Project!.Binder.Count);
    }

    [Fact]
    public void Parse_ValidScrivx_BinderItemsHaveCorrectTitles()
    {
        var path = CreateProject(MinimalScrivx);
        var result = ScrivenerParser.Parse(path);

        var manuscript = result.Project!.Binder[0];
        Assert.Equal("Manuscript", manuscript.Title);
        Assert.Equal("AAA-111", manuscript.Id);
        Assert.Equal("DraftFolder", manuscript.Type);
    }

    [Fact]
    public void Parse_ValidScrivx_NestedChildrenAreParsed()
    {
        var path = CreateProject(MinimalScrivx);
        var result = ScrivenerParser.Parse(path);

        var chapter = result.Project!.Binder[0].Children[0];
        Assert.Equal("Chapter One", chapter.Title);
        Assert.Equal("BBB-222", chapter.Id);
        Assert.Empty(chapter.Children);
    }

    [Fact]
    public void Parse_ValidScrivx_AllItemsFlattensTree()
    {
        var path = CreateProject(MinimalScrivx);
        var result = ScrivenerParser.Parse(path);

        var all = result.Project!.AllItems().ToList();
        Assert.Equal(3, all.Count);
        Assert.Contains(all, i => i.Id == "BBB-222");
    }

    [Fact]
    public void Parse_DirectoryNotFound_ReturnsFail()
    {
        var result = ScrivenerParser.Parse("/nonexistent/path/Test.scriv");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_NoScrivxFile_ReturnsFail()
    {
        var emptyDir = Path.Combine(_tempDir, "Empty.scriv");
        Directory.CreateDirectory(emptyDir);

        var result = ScrivenerParser.Parse(emptyDir);

        Assert.False(result.Success);
        Assert.Contains(".scrivx", result.Error);
    }

    [Fact]
    public void Parse_MalformedXml_ReturnsFail()
    {
        var path = CreateProject("<this is not valid xml<<<");
        var result = ScrivenerParser.Parse(path);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_MissingBinderElement_ReturnsFail()
    {
        var path = CreateProject("""
            <?xml version="1.0" encoding="UTF-8"?>
            <ScrivenerProject Version="2.0">
            </ScrivenerProject>
            """);

        var result = ScrivenerParser.Parse(path);

        Assert.False(result.Success);
        Assert.Contains("Binder", result.Error);
    }

    [Fact]
    public void Parse_SupportsLegacyIdAttribute()
    {
        var path = CreateProject("""
            <?xml version="1.0" encoding="UTF-8"?>
            <ScrivenerProject>
              <Binder>
                <BinderItem ID="42" Type="Text">
                  <Title>Scene</Title>
                  <Children/>
                </BinderItem>
              </Binder>
            </ScrivenerProject>
            """);

        var result = ScrivenerParser.Parse(path);

        Assert.True(result.Success);
        Assert.Equal("42", result.Project!.Binder[0].Id);
    }
}
