using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class AppearanceSettings
{
    internal const int DefaultBoxIconSize = 12;
    internal static readonly int[] BoxIconSizes = { 12, 16, 20, 24 };
    internal string Theme = "system";
    internal bool Acrylic = true;
    internal int Opacity = 97;
    internal int BoxIconSize = DefaultBoxIconSize;
    internal bool ShowBoxIconNames;
    internal static string FileName { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrayBox", "appearance.ini"); } }
    internal static AppearanceSettings Load() { return LoadFrom(FileName); }
    internal static AppearanceSettings LoadFrom(string fileName)
    {
        var result = new AppearanceSettings();
        try
        {
            if (!File.Exists(fileName)) return result;
            foreach (string line in File.ReadAllLines(fileName))
            {
                var p = line.Split(new char[] {'='}, 2);
                if (p.Length != 2) continue;
                if (p[0] == "theme" && (p[1] == "system" || p[1] == "dark" || p[1] == "light")) result.Theme = p[1];
                if (p[0] == "acrylic") result.Acrylic = p[1] == "1";
                int opacity;
                if (p[0] == "opacity" && Int32.TryParse(p[1], out opacity)) result.Opacity = Math.Max(80, Math.Min(100, opacity));
                int iconSize;
                if (p[0] == "box_icon_size" && Int32.TryParse(p[1], out iconSize) && Array.IndexOf(BoxIconSizes, iconSize) >= 0)
                    result.BoxIconSize = iconSize;
                if (p[0] == "show_box_icon_names") result.ShowBoxIconNames = p[1] == "1";
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return result;
    }
    internal void Save() { SaveTo(FileName); }
    internal void SaveTo(string fileName)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fileName));
        string temp = fileName + ".new";
        File.WriteAllLines(temp, new string[] {
            "theme=" + Theme, "acrylic=" + (Acrylic ? "1" : "0"), "opacity=" + Opacity,
            "box_icon_size=" + BoxIconSize, "show_box_icon_names=" + (ShowBoxIconNames ? "1" : "0")
        });
        if (File.Exists(fileName)) File.Replace(temp, fileName, null); else File.Move(temp, fileName);
    }
    internal bool Dark
    {
        get
        {
            if (Theme == "dark") return true;
            if (Theme == "light") return false;
            try { using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme", 1)) == 0; }
            catch { return false; }
        }
    }
}

