using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.UI.Xaml;
namespace TrayBoxFluent;
internal sealed class TrayHost : IDisposable
{
    private readonly App app;
    private readonly Mutex mutex;
    private readonly NotifyIcon tray;
    private readonly System.Drawing.Icon trayIcon;
    private readonly TrayEngine engine;
    private readonly MainWindow panel;
    private QuickMenu quickMenu;
    private readonly System.Windows.Forms.Timer refresh;
    private bool disposed;
    internal TrayHost(App application, string[] args)
    {
        app = application;
        bool created; mutex = new Mutex(true, @"Local\TrayBox.IndependentPreview", out created);
        if (!created) { app.Exit(); return; }
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrayBox");
        Directory.CreateDirectory(dir);
        string appearancePath = Path.Combine(dir,"appearance.ini");
        var appearance = AppearanceSettings.LoadFrom(appearancePath);
        TrayText.Use(appearance.Language);
        string recovery = Path.Combine(dir, "recovery-" + Process.GetCurrentProcess().Id + ".txt");
        File.WriteAllText(recovery, "");
        var executable = Process.GetCurrentProcess().MainModule.FileName;
        Process.Start(new ProcessStartInfo(executable, "--watch " + Process.GetCurrentProcess().Id + " \"" + recovery + "\"") { UseShellExecute = false, CreateNoWindow = true });
        trayIcon = File.Exists(App.IconPath)
            ? new System.Drawing.Icon(App.IconPath, SystemInformation.SmallIconSize)
            : System.Drawing.Icon.ExtractAssociatedIcon(executable) ?? Resources.CreateIcon();
        tray = new NotifyIcon { Icon = trayIcon, Text = "TrayBox", Visible = true };
            engine = new TrayEngine(recovery, Path.Combine(dir, "selection.txt"));
        panel = new MainWindow(engine, Dispose, appearancePath, appearance);
        tray.MouseClick += delegate(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) panel.ShowBox(Cursor.Position);
            else if (e.Button == MouseButtons.Right) ShowQuickMenu();
        };
        refresh = new System.Windows.Forms.Timer { Interval = 700 };
        refresh.Tick += delegate { refresh.Stop(); engine.Refresh(); }; refresh.Start();
    }
    private void ShowQuickMenu()
    {
        if (quickMenu!=null) { quickMenu.Close(); quickMenu=null; }
        var point=Cursor.Position;
        var menu=new QuickMenu(point,panel.CurrentTheme,panel.AcrylicEnabled,
            delegate { panel.ShowBox(point); },
            delegate { panel.OpenSettings(point); },
            delegate { engine.Refresh(); },
            Dispose);
        quickMenu=menu;
        menu.Closed += delegate { if(quickMenu==menu) quickMenu=null; };
        menu.Activate();
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        if (quickMenu != null) { quickMenu.Close(); quickMenu=null; }
        if (refresh != null) { refresh.Stop(); refresh.Dispose(); }
        if (panel != null) panel.Dispose();
        if (engine != null) engine.Dispose();
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
        trayIcon?.Dispose();
        if (mutex != null) mutex.Dispose();
        app.Exit();
    }
}
