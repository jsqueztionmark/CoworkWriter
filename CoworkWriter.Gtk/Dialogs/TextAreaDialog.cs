using Gtk;

namespace CoworkWriter.Gtk.Dialogs;

public class TextAreaDialog : Dialog
{
    private readonly TextView _textView;

    public string InputText => _textView.Buffer.Text;

    public TextAreaDialog(Window? parent, string title, string label)
        : base(title, parent, DialogFlags.Modal)
    {
        SetDefaultSize(500, 300);

        var box = new Box(Orientation.Vertical, 8)
        {
            MarginStart = 12, MarginEnd = 12,
            MarginTop = 12, MarginBottom = 12
        };
        box.PackStart(new Label(label) { Xalign = 0 }, false, false, 0);

        _textView = new TextView { WrapMode = WrapMode.WordChar, LeftMargin = 6, TopMargin = 4 };
        var scroll = new ScrolledWindow { ShadowType = ShadowType.In };
        scroll.Add(_textView);
        box.PackStart(scroll, true, true, 0);

        ContentArea.Add(box);
        AddButton("Cancel", ResponseType.Cancel);
        AddButton("OK", ResponseType.Accept);
        DefaultResponse = ResponseType.Accept;

        ShowAll();
        _textView.GrabFocus();
    }
}
