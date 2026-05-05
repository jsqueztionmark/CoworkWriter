namespace CoworkWriter.Core.Scrivener;

public record BinderItem(
    string Id,
    string Title,
    string Type,
    IReadOnlyList<BinderItem> Children);
