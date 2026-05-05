using CoworkWriter.Core;
using CoworkWriter.Core.Writing;
using CoworkWriter.Gtk.Dialogs;
using Gtk;

namespace CoworkWriter.Gtk.Panels;

public class ChatPanel : Box
{
    private AnthropicService? _service;

    private readonly TextView _historyView;
    private readonly TextBuffer _historyBuffer;
    private readonly TextTag _userTag;
    private readonly TextTag _assistantTag;
    private readonly TextView _inputView;
    private readonly Button _sendButton;
    private bool _streaming;

    public ChatPanel() : base(Orientation.Vertical, 4)
    {
        MarginStart = 4; MarginEnd = 4; MarginTop = 4;

        // History
        _historyView = new TextView
        {
            Editable = false,
            WrapMode = WrapMode.WordChar,
            LeftMargin = 8, RightMargin = 8, TopMargin = 4
        };
        _historyBuffer = _historyView.Buffer;

        _userTag = new TextTag("user");
        _userTag.Foreground = "#0055cc";
        _userTag.Weight = Pango.Weight.Bold;
        _historyBuffer.TagTable.Add(_userTag);

        _assistantTag = new TextTag("assistant");
        _assistantTag.Foreground = "#226622";
        _historyBuffer.TagTable.Add(_assistantTag);

        var historyScroll = new ScrolledWindow { ShadowType = ShadowType.In };
        historyScroll.Add(_historyView);
        PackStart(historyScroll, true, true, 0);

        // Writing command buttons
        PackStart(BuildCommandBar(), false, false, 0);

        // Input row
        _inputView = new TextView { WrapMode = WrapMode.WordChar, LeftMargin = 6 };
        _inputView.SetSizeRequest(-1, 60);
        _inputView.KeyPressEvent += OnInputKeyPress;

        _sendButton = new Button("Send");
        _sendButton.Clicked += OnSendClicked;

        var inputScroll = new ScrolledWindow { ShadowType = ShadowType.In };
        inputScroll.Add(_inputView);

        var inputRow = new Box(Orientation.Horizontal, 4) { MarginBottom = 4 };
        inputRow.PackStart(inputScroll, true, true, 0);
        inputRow.PackEnd(_sendButton, false, false, 0);
        PackEnd(inputRow, false, false, 0);
    }

    private Box BuildCommandBar()
    {
        var bar = new Box(Orientation.Horizontal, 4) { MarginTop = 2, MarginBottom = 2 };

        AddCmdButton(bar, "/continue",   () => SendPrompt(WritingPrompts.Continue()));
        AddCmdButton(bar, "/summarize",  () => SendPrompt(WritingPrompts.Summarize()));
        AddCmdButton(bar, "/critique",   () => SendPrompt(WritingPrompts.Critique()));
        AddCmdButton(bar, "/brainstorm…", ShowBrainstormDialog);
        AddCmdButton(bar, "/edit…",       ShowEditDialog);

        return bar;
    }

    private void AddCmdButton(Box bar, string label, System.Action action)
    {
        var btn = new Button(label);
        btn.Clicked += (_, _) => action();
        bar.PackStart(btn, false, false, 0);
    }

    public void SetService(AnthropicService service) => _service = service;

    [GLib.ConnectBefore]
    private void OnInputKeyPress(object o, KeyPressEventArgs args)
    {
        if (args.Event.Key == Gdk.Key.Return &&
            (args.Event.State & Gdk.ModifierType.ShiftMask) == 0)
        {
            args.RetVal = true;
            DoSend();
        }
    }

    private void OnSendClicked(object? sender, EventArgs e) => DoSend();

    private void DoSend()
    {
        var text = _inputView.Buffer.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        _inputView.Buffer.Text = string.Empty;
        SendPrompt(text);
    }

    public void SendPrompt(string prompt)
    {
        if (_service is null || _streaming) return;

        AppendSpeaker("You", _userTag);
        var end = _historyBuffer.EndIter;
        _historyBuffer.Insert(ref end, prompt + "\n\n");

        AppendSpeaker("Claude", _assistantTag);
        ScrollToEnd();

        _streaming = true;
        _sendButton.Sensitive = false;

        Task.Run(async () =>
        {
            try
            {
                await foreach (var chunk in _service.StreamMessageAsync(prompt))
                {
                    var c = chunk;
                    GLib.Idle.Add(() => { AppendChunk(c); return false; });
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                GLib.Idle.Add(() => { AppendChunk($"\n[Error: {msg}]"); return false; });
            }
            finally
            {
                GLib.Idle.Add(() =>
                {
                    AppendChunk("\n\n");
                    ScrollToEnd();
                    _streaming = false;
                    _sendButton.Sensitive = true;
                    return false;
                });
            }
        });
    }

    private void AppendSpeaker(string name, TextTag tag)
    {
        var end = _historyBuffer.EndIter;
        _historyBuffer.InsertWithTags(ref end, $"{name}: ", tag);
    }

    private void AppendChunk(string text)
    {
        var end = _historyBuffer.EndIter;
        _historyBuffer.Insert(ref end, text);
        ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        var mark = _historyBuffer.GetMark("insert");
        if (mark != null)
            _historyView.ScrollToMark(mark, 0.0, false, 0, 0);
    }

    private void ShowBrainstormDialog()
    {
        if (_service is null) return;
        var parent = Toplevel as Window;
        var dialog = new InputDialog(parent, "Brainstorm", "Topic:");
        if (dialog.Run() == (int)ResponseType.Accept && !string.IsNullOrWhiteSpace(dialog.InputText))
            SendPrompt(WritingPrompts.Brainstorm(dialog.InputText));
        dialog.Destroy();
    }

    private void ShowEditDialog()
    {
        if (_service is null) return;
        var parent = Toplevel as Window;
        var dialog = new InputDialog(parent, "Edit", "Instruction:");
        if (dialog.Run() == (int)ResponseType.Accept && !string.IsNullOrWhiteSpace(dialog.InputText))
            SendPrompt(WritingPrompts.Edit(dialog.InputText));
        dialog.Destroy();
    }
}
