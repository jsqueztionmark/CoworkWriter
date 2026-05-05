using System.Xml;
using System.Xml.Linq;

namespace CoworkWriter.Core.Scrivener;

public static class ScrivenerParser
{
    public static ParseResult Parse(string scrivPath)
    {
        if (!Directory.Exists(scrivPath))
            return ParseResult.Fail($"Directory not found: {scrivPath}");

        var scrivxFiles = Directory.GetFiles(scrivPath, "*.scrivx");
        if (scrivxFiles.Length == 0)
            return ParseResult.Fail("No .scrivx file found in the project folder.");

        try
        {
            var doc = XDocument.Load(scrivxFiles[0]);
            var binderEl = doc.Root?.Element("Binder");
            if (binderEl is null)
                return ParseResult.Fail("Invalid .scrivx: missing <Binder> element.");

            var binder = binderEl.Elements("BinderItem").Select(ParseBinderItem).ToList();
            return ParseResult.Ok(new ScrivenerProject(scrivPath, binder));
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException or IOException)
        {
            return ParseResult.Fail($"Failed to parse .scrivx: {ex.Message}");
        }
    }

    private static BinderItem ParseBinderItem(XElement el)
    {
        var id = el.Attribute("UUID")?.Value ?? el.Attribute("ID")?.Value ?? string.Empty;
        var title = el.Element("Title")?.Value ?? "(Untitled)";
        var type = el.Attribute("Type")?.Value ?? "Unknown";
        var children = el.Element("Children")?
            .Elements("BinderItem")
            .Select(ParseBinderItem)
            .ToList() ?? [];

        return new BinderItem(id, title, type, children);
    }
}
