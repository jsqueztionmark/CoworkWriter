using CoworkWriter.Core.Scrivener;
using Gtk;

namespace CoworkWriter.Gtk.Panels;

public class BinderPanel : Box
{
    public event Action<string, bool>? SelectionChanged;
    public event Action<string, bool>? PinChanged;
    public event Action<string>? DocumentSelected;

    private readonly TreeStore _store;
    private readonly TreeView _treeView;

    private const int ColSelected = 0;
    private const int ColPinned = 1;
    private const int ColTitle = 2;
    private const int ColId = 3;

    public BinderPanel() : base(Orientation.Vertical, 4)
    {
        MarginStart = 4; MarginEnd = 4; MarginTop = 4;

        _store = new TreeStore(typeof(bool), typeof(bool), typeof(string), typeof(string));
        _treeView = new TreeView(_store) { HeadersVisible = true };

        BuildColumns();
        _treeView.Selection.Changed += OnRowSelected;

        var header = new Label { Markup = "<b>Binder</b>", Xalign = 0 };
        PackStart(header, false, false, 4);

        var scroll = new ScrolledWindow { ShadowType = ShadowType.In };
        scroll.Add(_treeView);
        PackStart(scroll, true, true, 0);

        SetSizeRequest(220, -1);
    }

    private void BuildColumns()
    {
        var selToggle = new CellRendererToggle();
        selToggle.Toggled += OnSelectedToggled;
        _treeView.AppendColumn(new TreeViewColumn("✓", selToggle, "active", ColSelected));

        var pinToggle = new CellRendererToggle();
        pinToggle.Toggled += OnPinToggled;
        _treeView.AppendColumn(new TreeViewColumn("★", pinToggle, "active", ColPinned));

        var titleCell = new CellRendererText();
        var titleCol = new TreeViewColumn("Document", titleCell, "text", ColTitle) { Expand = true };
        _treeView.AppendColumn(titleCol);
    }

    private void OnSelectedToggled(object o, ToggledArgs args)
    {
        if (!_store.GetIterFromString(out var iter, args.Path)) return;
        var current = (bool)_store.GetValue(iter, ColSelected);
        _store.SetValue(iter, ColSelected, !current);
        var id = (string)_store.GetValue(iter, ColId);
        SelectionChanged?.Invoke(id, !current);
    }

    private void OnPinToggled(object o, ToggledArgs args)
    {
        if (!_store.GetIterFromString(out var iter, args.Path)) return;
        var current = (bool)_store.GetValue(iter, ColPinned);
        _store.SetValue(iter, ColPinned, !current);
        var id = (string)_store.GetValue(iter, ColId);
        PinChanged?.Invoke(id, !current);
    }

    private void OnRowSelected(object? sender, EventArgs e)
    {
        if (!_treeView.Selection.GetSelected(out _, out var iter)) return;
        var id = (string)_store.GetValue(iter, ColId);
        DocumentSelected?.Invoke(id);
    }

    public void LoadProject(ScrivenerProject project)
    {
        _store.Clear();
        AddItems(null, project.Binder);
        _treeView.ExpandAll();
    }

    private void AddItems(TreeIter? parent, IEnumerable<BinderItem> items)
    {
        foreach (var item in items)
        {
            var iter = parent.HasValue
                ? _store.AppendValues(parent.Value, false, false, item.Title, item.Id)
                : _store.AppendValues(false, false, item.Title, item.Id);

            if (item.Children.Count > 0)
                AddItems(iter, item.Children);
        }
    }
}
