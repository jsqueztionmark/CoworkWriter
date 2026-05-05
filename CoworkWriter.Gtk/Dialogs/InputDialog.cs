using Gtk;

namespace CoworkWriter.Gtk.Dialogs;

public class InputDialog : Dialog
{
    private readonly Entry _entry;

    public string InputText => _entry.Text;

    public InputDialog(Window? parent, string title, string label)
        : base(title, parent, DialogFlags.Modal)
    {
        SetDefaultSize(400, -1);

        var box = new Box(Orientation.Vertical, 8)
        {
            MarginStart = 12, MarginEnd = 12,
            MarginTop = 12, MarginBottom = 12
        };
        box.PackStart(new Label(label) { Xalign = 0 }, false, false, 0);

        _entry = new Entry();
        _entry.Activated += (_, _) => Respond(ResponseType.Accept);
        box.PackStart(_entry, false, false, 0);

        ContentArea.Add(box);
        AddButton("Cancel", ResponseType.Cancel);
        AddButton("OK", ResponseType.Accept);
        DefaultResponse = ResponseType.Accept;

        ShowAll();
        _entry.GrabFocus();
    }
}
