using System.Text;

namespace CoworkWriter.Core.Scrivener;

public class ContextBuilder
{
    private const int MaxContextChars = 80_000;

    public string Build(ScrivenerProject project, IEnumerable<string> selectedIds, IEnumerable<string>? pinnedIds = null)
    {
        var pinned = pinnedIds?.ToHashSet() ?? [];
        var selected = selectedIds.ToHashSet();

        if (pinned.Count == 0 && selected.Count == 0)
            return string.Empty;

        var allItems = project.AllItems().ToList();
        var selectedItems = allItems
            .Where(i => pinned.Contains(i.Id) || selected.Contains(i.Id))
            .OrderBy(i => pinned.Contains(i.Id) ? 0 : 1)
            .ToList();

        if (selectedItems.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        int remaining = MaxContextChars;

        foreach (var item in selectedItems)
        {
            if (remaining <= 0) break;

            var doc = project.LoadDocument(item);
            if (doc is null || string.IsNullOrWhiteSpace(doc.PlainText)) continue;

            var header = $"## {doc.Title}\n\n";
            var text = doc.PlainText;

            int available = remaining - header.Length;
            if (available <= 0) break;

            if (text.Length > available)
                text = text[..available] + "\n[...truncated]";

            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(header).Append(text);
            remaining -= header.Length + text.Length;
        }

        return sb.ToString().Trim();
    }
}
