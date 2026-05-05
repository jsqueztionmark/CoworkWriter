using CoworkWriter.Core;
using CoworkWriter.Core.Scrivener;
using CoworkWriter.Core.Settings;
using CoworkWriter.Core.Writing;
using CoworkWriter.Gtk.Dialogs;
using CoworkWriter.Gtk.Panels;
using Gtk;

namespace CoworkWriter.Gtk;

public class MainWindow : Window
{
    private AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private AnthropicService? _service;

    private readonly ContextBuilder _contextBuilder = new();
    private readonly SessionStore _sessionStore = new();

    private ScrivenerProject? _project;
    private readonly HashSet<string> _selectedIds = new();
    private readonly HashSet<string> _pinnedIds = new();

    private readonly BinderPanel _binderPanel;
    private readonly ChatPanel _chatPanel;
    private readonly DocumentPanel _documentPanel;
    private readonly Statusbar _statusbar;

    public MainWindow(AppSettings settings, SettingsStore settingsStore)
        : base("CoworkWriter") // Window(string title)
    {
        _settings = settings;
        _settingsStore = settingsStore;

        SetDefaultSize(1280, 720);
        DeleteEvent += (_, _) => Application.Quit();

        _binderPanel = new BinderPanel();
        _binderPanel.SelectionChanged += OnSelectionChanged;
        _binderPanel.PinChanged += OnPinChanged;
        _binderPanel.DocumentSelected += OnDocumentSelected;

        _chatPanel = new ChatPanel();
        _documentPanel = new DocumentPanel();

        InitService();

        var vbox = new Box(Orientation.Vertical, 0);
        vbox.PackStart(BuildMenuBar(), false, false, 0);

        var outerPaned = new Paned(Orientation.Horizontal);
        var innerPaned = new Paned(Orientation.Horizontal);

        outerPaned.Pack1(_binderPanel, false, false);
        outerPaned.Pack2(innerPaned, true, false);
        outerPaned.Position = 240;

        innerPaned.Pack1(_chatPanel, true, false);
        innerPaned.Pack2(_documentPanel, false, false);
        innerPaned.Position = 760;

        vbox.PackStart(outerPaned, true, true, 0);

        _statusbar = new Statusbar();
        vbox.PackEnd(_statusbar, false, false, 0);

        Add(vbox);

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            Application.Invoke((_, _) => ShowSettingsDialog());
    }

    private void InitService()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey)) return;
        var config = new AppConfig(_settings.ApiKey, _settings.Model);
        _service = new AnthropicService(config);
        _chatPanel.SetService(_service);
    }

    private MenuBar BuildMenuBar()
    {
        var menuBar = new MenuBar();

        var fileMenu = new Menu();
        var fileItem = new MenuItem("_File") { Submenu = fileMenu };

        var loadItem = new MenuItem("Load Project…");
        loadItem.Activated += OnLoadProject;
        fileMenu.Append(loadItem);

        var saveItem = new MenuItem("Save Session");
        saveItem.Activated += OnSaveSession;
        fileMenu.Append(saveItem);

        fileMenu.Append(new SeparatorMenuItem());

        var quitItem = new MenuItem("Quit");
        quitItem.Activated += (_, _) => Application.Quit();
        fileMenu.Append(quitItem);

        var editMenu = new Menu();
        var editItem = new MenuItem("_Edit") { Submenu = editMenu };

        var settingsItem = new MenuItem("Settings…");
        settingsItem.Activated += (_, _) => ShowSettingsDialog();
        editMenu.Append(settingsItem);

        menuBar.Append(fileItem);
        menuBar.Append(editItem);

        return menuBar;
    }

    private void OnLoadProject(object? sender, EventArgs e)
    {
        var dialog = new FileChooserDialog(
            "Load Scrivener Project",
            this,
            FileChooserAction.SelectFolder,
            "Cancel", ResponseType.Cancel,
            "Load", ResponseType.Accept);

        var response = (ResponseType)dialog.Run();
        var path = dialog.Filename;
        dialog.Destroy();

        if (response == ResponseType.Accept && path != null)
            LoadProject(path);
    }

    private void LoadProject(string path)
    {
        var result = ScrivenerParser.Parse(path);
        if (!result.Success)
        {
            ShowError($"Failed to load project:\n{result.Error}");
            return;
        }

        _project = result.Project;
        _selectedIds.Clear();
        _pinnedIds.Clear();

        if (_service is not null)
        {
            _service.ClearHistory();
            _service.SystemPrompt = null;
        }

        _binderPanel.LoadProject(_project!);
        _documentPanel.Clear();

        var sessionPath = SessionStore.SessionPath(path);
        if (File.Exists(sessionPath) && _service is not null)
        {
            var history = _sessionStore.Load(sessionPath);
            _service.LoadHistory(history);
            SetStatus($"Loaded: {System.IO.Path.GetFileName(path)}  (session restored, {history.Messages.Count} messages)");
        }
        else
        {
            SetStatus($"Loaded: {System.IO.Path.GetFileName(path)}");
        }
    }

    private void OnSaveSession(object? sender, EventArgs e)
    {
        if (_project is null || _service is null) { SetStatus("No project loaded."); return; }
        if (_service.History.Count == 0) { SetStatus("Nothing to save."); return; }

        var path = SessionStore.SessionPath(_project.FolderPath);
        _sessionStore.Save(path, _service.History);
        SetStatus($"Session saved  ({_service.History.Count} messages).");
    }

    private void OnSelectionChanged(string id, bool selected)
    {
        if (selected) _selectedIds.Add(id);
        else _selectedIds.Remove(id);
        RebuildSystemPrompt();
    }

    private void OnPinChanged(string id, bool pinned)
    {
        if (pinned) _pinnedIds.Add(id);
        else _pinnedIds.Remove(id);
        RebuildSystemPrompt();
    }

    private void OnDocumentSelected(string id)
    {
        if (_project is null) return;
        var item = _project.AllItems().FirstOrDefault(i => i.Id == id);
        if (item is null) return;
        var doc = _project.LoadDocument(item);
        _documentPanel.ShowDocument(item.Title, doc?.PlainText ?? "(No content)");
    }

    private void RebuildSystemPrompt()
    {
        if (_project is null || _service is null) return;
        var ctx = _contextBuilder.Build(_project, _selectedIds, _pinnedIds);
        _service.SystemPrompt = ctx.Length > 0
            ? $"{_settings.DefaultSystemPrompt}\n\nManuscript context:\n\n{ctx}"
            : null;
    }

    private void ShowSettingsDialog()
    {
        var dialog = new SettingsDialog(this, _settings);
        if (dialog.Run() == (int)ResponseType.Accept)
        {
            var next = dialog.GetSettings();
            var errors = SettingsStore.Validate(next).ToList();
            if (errors.Count > 0)
            {
                ShowError(string.Join("\n", errors));
            }
            else
            {
                _settings = next;
                _settingsStore.Save(_settings);
                InitService();
                SetStatus("Settings saved.");
            }
        }
        dialog.Destroy();
    }

    private void ShowError(string message)
    {
        var d = new MessageDialog(this, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, "%s", message);
        d.Run();
        d.Destroy();
    }

    private void SetStatus(string message)
    {
        _statusbar.Pop(0);
        _statusbar.Push(0, message);
    }
}
