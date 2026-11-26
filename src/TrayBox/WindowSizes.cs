using System;
using System.IO;
using System.Text;
using System.Drawing;
namespace TrayBoxFluent;
internal sealed class WindowSizes
{
    internal int BoxWidth = 390, BoxHeight = 370, SettingsWidth = 390, SettingsHeight = 510;
    internal bool HasPosition;
    internal int X, Y;
    private static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
    internal static Point Place(Rectangle area, int width, int height, Point? saved)
    {
        width=Math.Min(width,area.Width); height=Math.Min(height,area.Height);
        if (!saved.HasValue) return new Point(area.Right-width,area.Bottom-height);
        return new Point(Math.Max(area.Left,Math.Min(area.Right-width,saved.Value.X)),Math.Max(area.Top,Math.Min(area.Bottom-height,saved.Value.Y)));
    }
    internal static WindowSizes Load(string path)
    {
        var result = new WindowSizes();
        try
        {
            if (!File.Exists(path)) return result;
            foreach (var line in File.ReadAllLines(path))
            {
                var pair = line.Split(new[] { '=' }, 2); int value;
                if (pair.Length != 2 || !Int32.TryParse(pair[1], out value)) continue;
                switch (pair[0])
                {
                    case "box_width": result.BoxWidth = Clamp(value, 320, 1400); break;
                    case "box_height": result.BoxHeight = Clamp(value, 260, 1100); break;
                    case "settings_width": result.SettingsWidth = Clamp(value, 320, 1400); break;
                    case "settings_height": result.SettingsHeight = Clamp(value, 300, 1100); break;
                    case "window_x": result.X = value; break;
                    case "window_y": result.Y = value; break;
                    case "has_position": result.HasPosition = value == 1; break;
                }
            }
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
        return result;
    }
    internal void Update(bool settings, int width, int height)
    {
        if (settings) { SettingsWidth = Clamp(width, 320, 1400); SettingsHeight = Clamp(height, 300, 1100); }
        else { BoxWidth = Clamp(width, 320, 1400); BoxHeight = Clamp(height, 260, 1100); }
    }
    internal void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".new";
        File.WriteAllLines(temporary, new[] { "box_width=" + BoxWidth, "box_height=" + BoxHeight, "settings_width=" + SettingsWidth, "settings_height=" + SettingsHeight, "has_position=" + (HasPosition ? 1 : 0), "window_x=" + X, "window_y=" + Y }, Encoding.UTF8);
        if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
    }
}
