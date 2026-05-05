using Gtk;

namespace CoworkWriter.Gtk.Panels;

public class DocumentPanel : Box
{
    private readonly Label _titleLabel;
    private readonly TextBuffer _buffer;

    public DocumentPanel() : base(Orientation.Vertical, 4)
    {
        MarginStart = 4; MarginEnd = 4; MarginTop = 4;

        _titleLabel = new Label { Xalign = 0, UseMarkup = true };
        _titleLabel.Markup = "<b>(No document selected)</b>";
        PackStart(_titleLabel, false, false, 4);

        var textView = new TextView
        {
            Editable = false,
            WrapMode = WrapMode.WordChar,
            LeftMargin = 8,
            RightMargin = 8
        };
        _buffer = textView.Buffer;

        var scroll = new ScrolledWindow { ShadowType = ShadowType.In };
        scroll.Add(textView);
        PackStart(scroll, true, true, 0);

        SetSizeRequest(280, -1);
    }

    public void ShowDocument(string title, string content)
    {
        _titleLabel.Markup = $"<b>{GLib.Markup.EscapeText(title)}</b>";
        _buffer.Text = content;
    }

    public void Clear()
    {
        _titleLabel.Markup = "<b>(No document selected)</b>";
        _buffer.Text = string.Empty;
    }
}
