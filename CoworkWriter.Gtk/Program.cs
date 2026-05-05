using CoworkWriter.Core.Settings;
using Gtk;

Application.Init();

var settingsStore = new SettingsStore();
var settings = settingsStore.Load();

var app = new Application("com.coworkwriter.app", GLib.ApplicationFlags.None);
app.Register(GLib.Cancellable.Current);

var win = new CoworkWriter.Gtk.MainWindow(settings, settingsStore);
app.AddWindow(win);
win.ShowAll();
Application.Run();
