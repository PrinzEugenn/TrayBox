using System;
using System.Collections.Generic;
using System.Windows.Automation;
using System.Runtime.InteropServices;
using Rectangle = System.Drawing.Rectangle;
namespace TrayBoxFluent;
internal sealed class VisibleTrayIcon
{
    internal string Name;
    internal Rectangle Bounds;
}
internal static class VisibleTrayInspector
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr FindWindowW(string cls,string title);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    // Read-only: enumerates the visible notification area without opening or
    // changing the Windows hidden-icons panel.
    internal static List<VisibleTrayIcon> Read(IntPtr tray)
    {
        var result=new List<VisibleTrayIcon>();
        if (tray==IntPtr.Zero) return null;
        try
        {
            AddVisibleIcons(AutomationElement.FromHandle(tray),result);
            // Inspect the overflow only when the user has already opened it.
            IntPtr overflow=FindWindowW("TopLevelWindowForOverflowXamlIsland",null);
            uint trayPid, overflowPid; GetWindowThreadProcessId(tray,out trayPid); GetWindowThreadProcessId(overflow,out overflowPid);
            if (overflow!=IntPtr.Zero && trayPid!=0 && trayPid==overflowPid && IsWindowVisible(overflow)) AddVisibleIcons(AutomationElement.FromHandle(overflow),result);
        }
        catch (ElementNotAvailableException) { return null; } catch (System.Runtime.InteropServices.COMException) { return null; } catch (InvalidOperationException) { return null; }
        return result;
    }
    private static void AddVisibleIcons(AutomationElement root,List<VisibleTrayIcon> result)
    {
            var icons=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"NotifyItemIcon"));
            foreach (AutomationElement icon in icons)
            {
                try
                {
                    var name=icon.Current.Name; var bounds=icon.Current.BoundingRectangle;
                    if (String.IsNullOrWhiteSpace(name) || String.Equals(name,"TrayBox",StringComparison.OrdinalIgnoreCase) || bounds.IsEmpty || bounds.Width<=0 || bounds.Height<=0) continue;
                    result.Add(new VisibleTrayIcon { Name=name, Bounds=Rectangle.FromLTRB((int)bounds.Left,(int)bounds.Top,(int)bounds.Right,(int)bounds.Bottom) });
                }
                catch (ElementNotAvailableException) { }
            }
    }
    internal static bool IsKnown(VisibleTrayIcon icon, IEnumerable<TrayItem> captured, IDictionary<string,Rectangle> bounds)
    {
        string name=Normalize(icon.Name);
        foreach (var item in captured)
        {
            if (item.Boxed || TrayEngine.IsOwnIcon(item)) continue;
            string tip=Normalize(item.Tip);
            if (name.Length>0 && name==tip) return true;
            Rectangle known;
            if (bounds.TryGetValue(item.Key,out known) && icon.Bounds.Width<=80 && icon.Bounds.Height<=80 && known.Contains(icon.Bounds.Left+icon.Bounds.Width/2,icon.Bounds.Top+icon.Bounds.Height/2)) return true;
        }
        return false;
    }
    internal static TrayItem MatchUniqueName(VisibleTrayIcon icon, IEnumerable<TrayItem> captured)
    {
        string name=Normalize(icon.Name);
        if (name.Length==0) return null;
        TrayItem exact=null, partial=null; int exactCount=0, partialCount=0;
        foreach (var item in captured)
        {
            if (item.Boxed || item.Callback==0 || TrayEngine.IsOwnIcon(item)) continue;
            string tip=Normalize(item.Tip);
            if (tip.Length==0) continue;
            if (name==tip) { exact=item; exactCount++; }
            else if (name.Length>=3 && tip.Length>=3 && (name.StartsWith(tip+" ",StringComparison.Ordinal) || tip.StartsWith(name+" ",StringComparison.Ordinal))) { partial=item; partialCount++; }
        }
        if (exactCount==1) return exact;
        if (exactCount>1) return null;
        return partialCount==1 ? partial : null;
    }
    private static string Normalize(string value) { return String.Join(" ",(value??"").Split((char[])null,StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant(); }
}
