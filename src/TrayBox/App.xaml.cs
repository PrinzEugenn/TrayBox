using System;
using System.IO;
using Microsoft.UI.Xaml;
namespace TrayBoxFluent;
public partial class App : Application
{
    internal static string IconPath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
    private TrayHost host;
    public App() { InitializeComponent(); }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var command = Environment.GetCommandLineArgs();
        if (command.Length == 4 && command[1] == "--watch")
        {
            int pid; if (Int32.TryParse(command[2], out pid)) TrayEngine.Watch(pid, command[3]);
            Exit(); return;
        }
        if (command.Length != 1) { Exit(); return; }
        try { host = new TrayHost(this, command); }
        catch (Exception error)
        {
                System.Windows.Forms.MessageBox.Show("TrayBox 无法启动：" + error.Message, "TrayBox", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            Exit();
        }
    }
}
