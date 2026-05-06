using System.Text;
using System.Xml.Linq;

namespace CoworkWriter.Core.Scrivener;

public class ScrivenerWriter
{
    public WriteResult WriteDocument(ScrivenerProject project, string parentId, string title, string plainText)
    {
        if (string.IsNullOrWhiteSpace(project.FolderPath))
            return WriteResult.Fail("Cannot write to a project without a folder path.");

        var uuid = Guid.NewGuid().ToString().ToUpperInvariant();
        var dataDir = Path.Combine(project.FolderPath, "Files", "Data", uuid);

        try
        {
            Directory.CreateDirectory(dataDir);
            var rtfContent = PlainTextToRtf(plainText);
            File.WriteAllText(Path.Combine(dataDir, "content.rtf"), rtfContent, Encoding.UTF8);
            AddToScrivx(project.FolderPath, parentId, uuid, title);
            return WriteResult.Ok(uuid);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return WriteResult.Fail($"Failed to write document: {ex.Message}");
        }
    }

    public WriteResult UpdateDocument(ScrivenerProject project, string documentId, string plainText)
    {
        if (string.IsNullOrWhiteSpace(project.FolderPath))
            return WriteResult.Fail("Cannot write to a project without a folder path.");

        var dataDir = Path.Combine(project.FolderPath, "Files", "Data", documentId);
        if (!Directory.Exists(dataDir))
            return WriteResult.Fail($"Document data directory not found: {documentId}");

        try
        {
            var rtfContent = PlainTextToRtf(plainText);
            File.WriteAllText(Path.Combine(dataDir, "content.rtf"), rtfContent, Encoding.UTF8);
            return WriteResult.Ok(documentId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return WriteResult.Fail($"Failed to update document: {ex.Message}");
        }
    }

    internal static string PlainTextToRtf(string text)
    {
        var sb = new StringBuilder();
        sb.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Times New Roman;}}");
        sb.Append(@"\pard\f0\fs24 ");

        foreach (char c in text)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '{': sb.Append(@"\{"); break;
                case '}': sb.Append(@"\}"); break;
                case '\n': sb.Append(@"\par "); break;
                case '\r': break;
                default:
                    if (c > 127)
                        sb.Append($@"\u{(int)c}?");
                    else
                        sb.Append(c);
                    break;
            }
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static void AddToScrivx(string projectPath, string parentId, string newId, string title)
    {
        var scrivxFiles = Directory.GetFiles(projectPath, "*.scrivx");
        if (scrivxFiles.Length == 0) return;

        var doc = XDocument.Load(scrivxFiles[0]);
        var newElement = new XElement("BinderItem",
            new XAttribute("UUID", newId),
            new XAttribute("Type", "Text"),
            new XElement("Title", title));

        var parent = FindBinderItem(doc.Root!, parentId);
        if (parent is not null)
        {
            var children = parent.Element("Children");
            if (children is null)
            {
                children = new XElement("Children");
                parent.Add(children);
            }
            children.Add(newElement);
        }
        else
        {
            doc.Root!.Element("Binder")?.Add(newElement);
        }

        doc.Save(scrivxFiles[0]);
    }

    private static XElement? FindBinderItem(XElement root, string id)
    {
        foreach (var el in root.Descendants("BinderItem"))
        {
            var uuid = el.Attribute("UUID")?.Value ?? el.Attribute("ID")?.Value;
            if (uuid == id) return el;
        }
        return null;
    }
}

public record WriteResult(bool Success, string? DocumentId, string? Error)
{
    public static WriteResult Ok(string documentId) => new(true, documentId, null);
    public static WriteResult Fail(string error) => new(false, null, error);
}
