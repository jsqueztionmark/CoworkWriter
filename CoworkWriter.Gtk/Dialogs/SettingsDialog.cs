using Anthropic.SDK.Constants;
using CoworkWriter.Core.Settings;
using Gtk;

namespace CoworkWriter.Gtk.Dialogs;

public class SettingsDialog : Dialog
{
    private readonly Entry _apiKeyEntry;
    private readonly ComboBoxText _modelCombo;
    private readonly TextView _systemPromptView;

    private static readonly (string Id, string Label)[] Models =
    [
        (AnthropicModels.Claude46Sonnet, "Claude Sonnet 4.6 (default)"),
        (AnthropicModels.Claude46Opus,   "Claude Opus 4.6"),
        (AnthropicModels.Claude45Sonnet, "Claude Sonnet 4.5"),
        (AnthropicModels.Claude45Haiku,  "Claude Haiku 4.5"),
    ];

    public SettingsDialog(Window parent, AppSettings current)
        : base("Settings", parent, DialogFlags.Modal)
    {
        SetDefaultSize(500, 380);

        var grid = new Grid
        {
            RowSpacing = 10, ColumnSpacing = 10,
            MarginStart = 16, MarginEnd = 16,
            MarginTop = 16, MarginBottom = 8
        };

        grid.Attach(new Label("API Key:") { Xalign = 1 }, 0, 0, 1, 1);
        _apiKeyEntry = new Entry { Visibility = false, Text = current.ApiKey };
        _apiKeyEntry.SetSizeRequest(320, -1);
        grid.Attach(_apiKeyEntry, 1, 0, 1, 1);

        grid.Attach(new Label("Model:") { Xalign = 1 }, 0, 1, 1, 1);
        _modelCombo = new ComboBoxText();
        foreach (var (_, label) in Models)
            _modelCombo.AppendText(label);
        _modelCombo.Active = Math.Max(0, Array.FindIndex(Models, m => m.Id == current.Model));
        grid.Attach(_modelCombo, 1, 1, 1, 1);

        grid.Attach(new Label("System Prompt:") { Xalign = 1, Valign = Align.Start }, 0, 2, 1, 1);
        _systemPromptView = new TextView { WrapMode = WrapMode.WordChar };
        _systemPromptView.Buffer.Text = current.DefaultSystemPrompt;
        var spScroll = new ScrolledWindow { ShadowType = ShadowType.In, HeightRequest = 140 };
        spScroll.Add(_systemPromptView);
        grid.Attach(spScroll, 1, 2, 1, 1);

        ContentArea.Add(grid);
        AddButton("Cancel", ResponseType.Cancel);
        AddButton("Save", ResponseType.Accept);
        DefaultResponse = ResponseType.Accept;

        ShowAll();
    }

    public AppSettings GetSettings() => new(
        ApiKey: _apiKeyEntry.Text.Trim(),
        Model: Models[Math.Max(0, _modelCombo.Active)].Id,
        DefaultSystemPrompt: _systemPromptView.Buffer.Text.Trim());
}
