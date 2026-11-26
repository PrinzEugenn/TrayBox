using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Startup
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static bool Enabled
    {
        get { using (var key = Registry.CurrentUser.OpenSubKey(KeyPath)) return key != null && key.GetValue("TrayBox") != null; }
        set
        {
            using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                if (value) key.SetValue("TrayBox", "\"" + Application.ExecutablePath + "\"");
                else key.DeleteValue("TrayBox", false);
            }
        }
    }
}

internal static class Resources
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    internal static Icon CreateIcon()
    {
        using (var bmp = new Bitmap(32, 32))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Color.FromArgb(70, 115, 216))) FillRoundedRectangle(g, brush, new RectangleF(2, 3, 28, 26), 7);
            using (var pen = new Pen(Color.White, 2.2f))
            {
                g.DrawLine(pen, 8, 12, 24, 12); g.DrawLine(pen, 11, 18, 21, 18); g.DrawLine(pen, 13, 23, 19, 23);
            }
            IntPtr handle = bmp.GetHicon();
            try { using (var nativeIcon = Icon.FromHandle(handle)) return (Icon)nativeIcon.Clone(); }
            finally { DestroyIcon(handle); }
        }
    }
    private static void FillRoundedRectangle(Graphics g, Brush b, RectangleF r, float radius)
    {
        using (var path = new GraphicsPath())
        {
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); g.FillPath(b, path);
        }
    }
}
