namespace CoworkWriter.Core.Scrivener;

public class ScrivenerProject
{
    private readonly Func<BinderItem, ScrivenerDocument?> _documentLoader;

    public string FolderPath { get; }
    public IReadOnlyList<BinderItem> Binder { get; }

    public ScrivenerProject(string folderPath, IReadOnlyList<BinderItem> binder)
    {
        FolderPath = folderPath;
        Binder = binder;
        _documentLoader = LoadFromFilesystem;
    }

    internal ScrivenerProject(IReadOnlyList<BinderItem> binder, Func<BinderItem, ScrivenerDocument?> loader)
    {
        FolderPath = string.Empty;
        Binder = binder;
        _documentLoader = loader;
    }

    public ScrivenerDocument? LoadDocument(BinderItem item) => _documentLoader(item);

    public IEnumerable<BinderItem> AllItems() => Flatten(Binder);

    private ScrivenerDocument? LoadFromFilesystem(BinderItem item)
    {
        var rtfPath = Path.Combine(FolderPath, "Files", "Data", item.Id, "content.rtf");
        var txtPath = Path.Combine(FolderPath, "Files", "Data", item.Id, "content.txt");

        if (File.Exists(rtfPath))
            return new ScrivenerDocument(item.Id, item.Title, RtfStripper.ToPlainText(File.ReadAllText(rtfPath)));
        if (File.Exists(txtPath))
            return new ScrivenerDocument(item.Id, item.Title, File.ReadAllText(txtPath));

        return null;
    }

    private static IEnumerable<BinderItem> Flatten(IEnumerable<BinderItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Flatten(item.Children))
                yield return child;
        }
    }
}
