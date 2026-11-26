// TrayBox notification-area capture. Based in part on the MIT-licensed
// systray-util architecture: https://github.com/glzr-io/zebar (see THIRD-PARTY.md).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal sealed class TrayItem
{
    public IntPtr Window;
    public uint Id, Callback, Version;
    public Guid Guid;
    public string Tip = "";
    public Icon Image;
    public uint ImageHandle;
    public ulong ImageFingerprint;
    public bool Boxed;
    public bool Pending;
    public string SelectionIdentity;
    public Rectangle Anchor;
    public string Key { get { return Guid != Guid.Empty ? Guid.ToString("D") : Window.ToInt64().ToString("X") + ":" + Id; } }
}

internal sealed class TrayEngine : IDisposable
{
    private const uint WM_COPYDATA = 0x4A, WM_USER = 0x400, WM_ACTIVATEAPP = 0x1C, WM_COMMAND = 0x111;
    private const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4, NIF_STATE = 8, NIF_GUID = 0x20;
    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10;
    private readonly Dictionary<string, TrayItem> items = new Dictionary<string, TrayItem>();
    private readonly System.Windows.Forms.Timer topTimer = new System.Windows.Forms.Timer();
    private readonly Dictionary<TrayItem,DateTime> clickVisibility = new Dictionary<TrayItem,DateTime>();
    private readonly WndProc proc;
    private readonly MouseHookProc captureMouseProc;
    private IntPtr captureMouseHook;
    private bool refreshPending;
    internal bool DragInProgress;
    internal string LastOperationError = "";
    internal readonly HashSet<int> DiscoveryDenied = new HashSet<int>();
    internal readonly HashSet<int> DiscoveryNotified = new HashSet<int>();
    private IntPtr realTray;
    private IntPtr window;
    private ushort atom;
    private DateTime captureUntil;
    private DateTime nextPrune;
    private bool disposed;
    private bool selectionDirty;
    private readonly string className = "Shell_TrayWnd";
    private readonly string recoveryPath;
    private readonly string selectionPath;
    private readonly HashSet<string> selections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<TrayItem> unresolvedRemovals = new List<TrayItem>();
    public event Action Changed;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr WndProc(IntPtr h, uint m, IntPtr w, IntPtr l);
    private delegate IntPtr MouseHookProc(int code, IntPtr wParam, IntPtr lParam);
    private delegate bool EnumWindowProc(IntPtr hwnd, IntPtr state);
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    private struct WndClass { public uint Style; public WndProc Proc; public int ClsExtra, WndExtra; public IntPtr Instance, Icon, Cursor, Background; public string Menu, Class; }
    [StructLayout(LayoutKind.Sequential)] private struct CopyData { public IntPtr Kind; public int Size; public IntPtr Data; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    private struct WireIcon
    {
        public uint Size, Window, Id, Flags, Callback, Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public uint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    private struct NotifyData
    {
        public uint Size; public IntPtr Window; public uint Id, Flags, Callback; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier { public uint Size; public IntPtr Window; public uint Id; public Guid Guid; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern ushort RegisterClassW(ref WndClass cls);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern bool UnregisterClassW(string name, IntPtr instance);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern IntPtr CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr FindWindowW(string cls, string title);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr FindWindowExW(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
    [DllImport("user32.dll", SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int id, MouseHookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern IntPtr SendMessageW(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool PostMessageW(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SendNotifyMessageW(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern uint RegisterWindowMessageW(string name);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassNameW(IntPtr hwnd,StringBuilder name,int capacity);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowProc callback, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr state);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out NativeRect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [StructLayout(LayoutKind.Sequential)] private struct GuiThreadInfo
    {
        public int Size, Flags; public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public int Left, Top, Right, Bottom;
    }
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint pid);
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)] private static extern bool Shell_NotifyIconW(uint action, ref NotifyData data);
    [DllImport("shell32.dll")] private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier id, out NativeRect rect);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string name);

    public TrayEngine(string recoveryFile = null, string selectionFile = null)
    {
        recoveryPath = recoveryFile;
        selectionPath = selectionFile;
        if (selectionPath != null && File.Exists(selectionPath))
            foreach (var line in File.ReadAllLines(selectionPath)) if (line.Length > 0) selections.Add(line);
        realTray = FindRealTray();
        if (realTray == IntPtr.Zero) throw new InvalidOperationException("未找到 Windows 任务栏。");
        proc = HandleMessage;
        captureMouseProc = CaptureMouse;
        var wc = new WndClass { Proc = proc, Class = className, Instance = GetModuleHandleW(null) };
        atom = RegisterClassW(ref wc);
        if (atom == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        topTimer.Interval = 250;
        topTimer.Tick += delegate {
            if (window != IntPtr.Zero && DateTime.UtcNow >= captureUntil) StopCapture();
            if (refreshPending && !AnyMouseButtonDown()) Refresh();
            if (!IsWindow(realTray)) realTray = FindRealTray();
            EndClickVisibility();
            foreach (var icon in Snapshot()) if (icon.Pending && icon.Callback!=0) { icon.Pending = false; Hide(icon, true); }
            if (selectionDirty && TrySaveSelections()) Notify();
            if (DateTime.UtcNow >= nextPrune) { nextPrune = DateTime.UtcNow.AddSeconds(2); PruneClosedWindows(); }
        };
        topTimer.Start();
        StartCapture();
    }

    private static IntPtr FindRealTray()
    {
        IntPtr next = IntPtr.Zero;
        while ((next = FindWindowExW(IntPtr.Zero, next, "Shell_TrayWnd", null)) != IntPtr.Zero)
        {
            uint pid; GetWindowThreadProcessId(next, out pid);
            try { if (String.Equals(Process.GetProcessById((int)pid).ProcessName, "explorer", StringComparison.OrdinalIgnoreCase)) return next; }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
        return IntPtr.Zero;
    }
    private bool StartCapture()
    {
        if (disposed || DragInProgress || AnyMouseButtonDown()) return false;
        if (window == IntPtr.Zero)
        {
            window = CreateWindowExW(0x88, className, className, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);
            if (window == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            captureMouseHook = SetWindowsHookEx(14, captureMouseProc, IntPtr.Zero, 0);
            if (captureMouseHook == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                StopCapture();
                throw new System.ComponentModel.Win32Exception(error);
            }
        }
        SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        captureUntil = DateTime.UtcNow.AddMilliseconds(1600);
        return true;
    }
    internal static bool AnyMouseButtonDown() { return (GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0 || (GetAsyncKeyState(4) & 0x8000) != 0; }
    private IntPtr CaptureMouse(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0) OnCaptureUserInput(unchecked((uint)wParam.ToInt64()));
        return CallNextHookEx(captureMouseHook, code, wParam, lParam);
    }
    private void OnCaptureUserInput(uint message)
    {
        if (message == 0x201 || message == 0x204 || message == 0x207 || message == 0x20b) StopCapture();
    }
    private void StopCapture()
    {
        if (captureMouseHook != IntPtr.Zero) { UnhookWindowsHookEx(captureMouseHook); captureMouseHook = IntPtr.Zero; }
        if (window == IntPtr.Zero) return;
        DestroyWindow(window); window = IntPtr.Zero;
    }
    public void Refresh()
    {
        if (!StartCapture()) { refreshPending = true; return; }
        refreshPending = false;
        SendNotifyMessageW(new IntPtr(0xffff), RegisterWindowMessageW("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);
    }
    // Notify only windows belonging to explicitly identified processes, including
    // message-only owners missed by HWND_BROADCAST. Never signal Explorer or self.
    internal bool RefreshProcesses(IEnumerable<int> processIds)
    {
        DiscoveryDenied.Clear();
        DiscoveryNotified.Clear();
        var targets=new HashSet<uint>(); uint shellPid; GetWindowThreadProcessId(realTray,out shellPid);
        uint self; using(var process=Process.GetCurrentProcess()) self=(uint)process.Id;
        foreach (int processId in processIds) if (processId>0 && (uint)processId!=self && (uint)processId!=shellPid) targets.Add((uint)processId);
        if (targets.Count==0) return false;
        var windows=new List<IntPtr>(); var counts=new Dictionary<uint,int>();
        EnumWindowProc callback=delegate(IntPtr hwnd,IntPtr unused) {
            uint pid; GetWindowThreadProcessId(hwnd,out pid);
            if (!targets.Contains(pid)) return true;
            int count; counts.TryGetValue(pid,out count); if (count>=64) return true;
            counts[pid]=count+1;
            windows.Add(hwnd);
            return true;
        };
        EnumWindows(callback,IntPtr.Zero);
        IntPtr messageWindow=IntPtr.Zero; int visited=0;
        while (visited++<4096 && (messageWindow=FindWindowExW(new IntPtr(-3),messageWindow,null,null))!=IntPtr.Zero)
        {
            callback(messageWindow,IntPtr.Zero);
            EnumChildWindows(messageWindow,callback,IntPtr.Zero);
        }
        if (windows.Count==0 || !StartCapture()) return false;
        uint message=RegisterWindowMessageW("TaskbarCreated");
        if (message==0) { StopCapture(); return false; }
        int sent=0;
        foreach(var hwnd in windows)
        {
            if (window==IntPtr.Zero) break;
            uint pid; GetWindowThreadProcessId(hwnd,out pid);
            if (targets.Contains(pid)) {
                if(SendNotifyMessageW(hwnd,message,IntPtr.Zero,IntPtr.Zero)) { sent++; DiscoveryNotified.Add((int)pid); }
                else if(Marshal.GetLastWin32Error()==5) DiscoveryDenied.Add((int)pid);
            }
        }
        if (sent==0) StopCapture();
        return sent>0;
    }
    internal bool IsCapturing { get { return window != IntPtr.Zero; } }
    internal IntPtr NativeTrayWindow { get { return realTray; } }
    public List<TrayItem> Snapshot() { return new List<TrayItem>(items.Values); }
    private void PruneClosedWindows()
    {
        bool changed = false;
        foreach (var item in Snapshot())
        {
            if (item.Window == IntPtr.Zero || IsWindow(item.Window)) continue;
            items.Remove(item.Key); if (item.Image != null) item.Image.Dispose(); changed = true;
        }
        if (changed)
        {
            if (recoveryPath != null) { try { WriteRecovery(); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            Notify();
        }
    }
    internal TrayItem FindVisibleIconAt(Point point)
    {
        TrayItem match = null;
        foreach (var item in Snapshot())
        {
            if (item.Boxed || item.Callback == 0 || IsOwnIcon(item)) continue;
            Rectangle bounds;
            if (!GetIconBounds(item, out bounds) || !bounds.Contains(point)) continue;
            if (match != null) return null;
            match = item;
        }
        return match;
    }
    internal static bool GetIconBounds(TrayItem item, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        var id = new NotifyIconIdentifier { Size = (uint)Marshal.SizeOf(typeof(NotifyIconIdentifier)), Window = item.Window, Id = item.Id, Guid = item.Guid };
        NativeRect rect;
        if (Shell_NotifyIconGetRect(ref id, out rect) != 0 || rect.Right <= rect.Left || rect.Bottom <= rect.Top) return false;
        bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        return true;
    }
    internal bool TryGetBounds(TrayItem item, out Rectangle bounds)
    {
        return GetIconBounds(item, out bounds);
    }
    internal bool IsNativeTrayRegion(Point point)
    {
        NativeRect rect;
        return IsWindow(realTray) && GetWindowRect(realTray, out rect) && Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom).Contains(point);
    }
    public void CancelNativeDrag()
    {
        uint pid; uint thread = GetWindowThreadProcessId(realTray, out pid);
        var info = new GuiThreadInfo { Size = Marshal.SizeOf(typeof(GuiThreadInfo)) };
        if (thread != 0 && GetGUIThreadInfo(thread, ref info) && info.Capture != IntPtr.Zero)
            PostMessageW(info.Capture, 0x001f, IntPtr.Zero, IntPtr.Zero);
    }

    private IntPtr HandleMessage(IntPtr h, uint m, IntPtr w, IntPtr l)
    {
        try
        {
            if (m == WM_COPYDATA && l != IntPtr.Zero)
            {
                var cd = (CopyData)Marshal.PtrToStructure(l, typeof(CopyData));
                if (cd.Kind.ToInt64() == 1 && cd.Data != IntPtr.Zero && cd.Size >= 8 + 16)
                {
                    uint action = unchecked((uint)Marshal.ReadInt32(cd.Data, 4));
                    CaptureWire(action, IntPtr.Add(cd.Data, 8), cd.Size - 8);
                }
                else if (cd.Kind.ToInt64() == 3 && cd.Data != IntPtr.Zero && cd.Size >= 40)
                {
                    uint command = unchecked((uint)Marshal.ReadInt32(cd.Data, 4));
                    uint handle = unchecked((uint)Marshal.ReadInt32(cd.Data, 16));
                    uint id = unchecked((uint)Marshal.ReadInt32(cd.Data, 20));
                    Guid guid = (Guid)Marshal.PtrToStructure(IntPtr.Add(cd.Data, 24), typeof(Guid));
                    TrayItem owned = null;
                    foreach (var item in items.Values)
                        if (item.Boxed && ((guid != Guid.Empty && item.Guid == guid) || (item.Window.ToInt64() == handle && item.Id == id))) { owned = item; break; }
                    if (owned != null && (command == 1 || command == 2))
                    {
                        var pt = Cursor.Position;
                        Rectangle bounds = owned.Anchor.IsEmpty ? new Rectangle(pt.X, pt.Y, 1, 1) : owned.Anchor;
                        ushort first = unchecked((ushort)(command == 1 ? bounds.Left : bounds.Top));
                        ushort last = unchecked((ushort)(command == 1 ? bounds.Right : bounds.Bottom));
                        return new IntPtr(unchecked((int)((uint)first | ((uint)last << 16))));
                    }
                }
            }
            if ((m == WM_COPYDATA || m == WM_ACTIVATEAPP || m == WM_COMMAND) && IsWindow(realTray))
                return SendMessageW(realTray, m, w, l);
            if (m >= WM_USER && IsWindow(realTray)) { PostMessageW(realTray, m, w, l); return IntPtr.Zero; }
        }
        catch { }
        return DefWindowProcW(h, m, w, l);
    }

    // Shell messages can contain an older, shorter NOTIFYICONDATA layout.
    // Copy only the available bytes into a zero-filled full-size buffer and
    // disregard fields whose bytes are absent; never read past WM_COPYDATA.
    internal void CaptureWire(uint action, IntPtr payload, int length)
    {
        int full = Marshal.SizeOf(typeof(WireIcon));
        if (payload == IntPtr.Zero || length < 16) return;
        int count = Math.Min(length, full);
        var bytes = new byte[full];
        Marshal.Copy(payload, bytes, 0, count);
        IntPtr buffer = Marshal.AllocHGlobal(full);
        try
        {
            Marshal.Copy(bytes, 0, buffer, full);
            var data = (WireIcon)Marshal.PtrToStructure(buffer, typeof(WireIcon));
            if (count < Marshal.OffsetOf(typeof(WireIcon), "Callback").ToInt32() + 4) data.Flags &= ~NIF_MESSAGE;
            if (count < Marshal.OffsetOf(typeof(WireIcon), "Icon").ToInt32() + 4) data.Flags &= ~NIF_ICON;
            if (count < Marshal.OffsetOf(typeof(WireIcon), "Tip").ToInt32() + 2) data.Flags &= ~NIF_TIP;
            if (count < Marshal.OffsetOf(typeof(WireIcon), "Guid").ToInt32() + 16) data.Flags &= ~NIF_GUID;
            if (action == NIM_SETVERSION && count < Marshal.OffsetOf(typeof(WireIcon), "Version").ToInt32() + 4) return;
            Capture(action, data);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private void Capture(uint action, WireIcon data)
    {
        if (data.Window == 0) return;
        var candidate = new TrayItem { Window = new IntPtr((long)data.Window), Id = data.Id, Guid = (data.Flags & NIF_GUID) != 0 ? data.Guid : Guid.Empty };
        TrayItem item;
        if (!items.TryGetValue(candidate.Key, out item))
        {
            foreach (var old in items.Values) if (old.Window == candidate.Window && old.Id == candidate.Id) { item = old; break; }
        }
        if (action == NIM_DELETE)
        {
            if (item != null) { items.Remove(item.Key); if (item.Image != null) item.Image.Dispose(); Notify(); }
            return;
        }
        if (action != NIM_ADD && action != NIM_MODIFY && action != NIM_SETVERSION) return;
        bool changed = false;
        if (item == null) { item = candidate; items[item.Key] = item; changed = true; }
        if ((data.Flags & NIF_MESSAGE) != 0 && item.Callback != data.Callback) { item.Callback = data.Callback; changed = true; }
        if ((data.Flags & NIF_TIP) != 0 && item.Tip != (data.Tip ?? "")) { item.Tip = data.Tip ?? ""; changed = true; }
        if (action == NIM_SETVERSION && item.Version != data.Version) { item.Version = data.Version; changed = true; }
        if ((data.Flags & NIF_ICON) != 0 && data.Icon != 0)
        {
            Icon icon = null;
            try
            {
                icon = (Icon)Icon.FromHandle(new IntPtr((long)data.Icon)).Clone();
                ulong fingerprint;
                using (var image = icon.ToBitmap()) fingerprint = Fingerprint(image);
                if (item.Image == null || item.ImageHandle != data.Icon || item.ImageFingerprint != fingerprint)
                {
                    if (item.Image != null) item.Image.Dispose();
                    item.Image = icon; icon = null; item.ImageHandle = data.Icon; item.ImageFingerprint = fingerprint; changed = true;
                }
            }
            catch (ArgumentException) { }
            catch (System.Runtime.InteropServices.ExternalException) { }
            finally { if (icon != null) icon.Dispose(); }
        }
        if (action == NIM_ADD && !item.Boxed && selections.Count > 0)
        {
            string identity = Identity(item);
            if (identity != null && selections.Contains(identity)) { item.SelectionIdentity = identity; item.Pending = true; }
        }
        if (changed) Notify();
    }
    private void Notify() { var handler = Changed; if (handler != null) handler(); }
    private static ulong Fingerprint(Bitmap image)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            hash = (hash ^ (uint)image.Width) * 1099511628211UL;
            hash = (hash ^ (uint)image.Height) * 1099511628211UL;
            int columns = Math.Min(16, image.Width), rows = Math.Min(16, image.Height);
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
                hash = (hash ^ (uint)image.GetPixel(x * image.Width / columns, y * image.Height / rows).ToArgb()) * 1099511628211UL;
            return hash;
        }
    }

    public bool Hide(TrayItem item, bool hide)
    {
        bool wasBoxed = item.Boxed;
        if (hide && recoveryPath != null)
        {
            item.Boxed = true;
            try { WriteRecovery(); }
            catch { item.Boxed = wasBoxed; LastOperationError="无法写入图标恢复记录，请检查 TrayBox 配置目录"; return false; }
        }
        bool success = SetShellHidden(item,hide);
        if(!success) LastOperationError="Windows 未接受该图标的显示状态变更";
        if(success) clickVisibility.Remove(item);
        if (success) item.Boxed = hide;
        else item.Boxed = wasBoxed;
        if (recoveryPath != null) { try { WriteRecovery(); } catch { } }
        if (item.Boxed != wasBoxed) Notify();
        return success;
    }

    private static string Identity(TrayItem item)
    {
        int error;
        return Identity(item,out error);
    }
    internal static string SelectionKey(TrayItem item) { return item.SelectionIdentity ?? Identity(item); }
    private static string Identity(TrayItem item,out int error)
    {
        error=0;
        try
        {
            uint pid; GetWindowThreadProcessId(item.Window, out pid);
            string path; long created; int integrity;
            if (!ProcessAccess.TryRead(pid,out path,out created,out integrity,out error) || String.IsNullOrEmpty(path)) return null;
            path=path.ToLowerInvariant();
            string key = path + "|" + (item.Guid != Guid.Empty ? item.Guid.ToString("D") : item.Id.ToString());
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(key));
        }
        catch { return null; }
    }
    internal static string BoxingIssue(TrayItem item)
    {
        if(!IsWindow(item.Window)) return "图标的应用窗口已退出或失效";
        if(item.Callback==0) return "已取得图标注册，但其中缺少点击回调";
        if(item.SelectionIdentity!=null) return null;
        int error;
        if(Identity(item,out error)!=null) return null;
        return error==5 ? "Windows 拒绝读取此图标所属进程的身份" : "无法验证图标所属应用的身份";
    }
    public bool SetBoxed(TrayItem item, bool boxed)
    {
        LastOperationError="";
        if (IsOwnIcon(item)) { LastOperationError="TrayBox 自身的图标需要保留在通知区域"; return false; }
        if (boxed && item.Callback==0) { LastOperationError="未取得点击回调，暂时无法收纳"; return false; }
        if (boxed && !IsWindow(item.Window)) { LastOperationError="应用已退出或图标窗口已失效"; return false; }
        string identity = item.SelectionIdentity ?? Identity(item);
        if (boxed && identity == null) { LastOperationError=BoxingIssue(item) ?? "应用身份已变化，请刷新图标后再试"; return false; }
        bool wasSelected = identity != null && selections.Contains(identity);
        string previousIdentity = item.SelectionIdentity;
        if (!Hide(item, boxed)) return false;
        if (boxed) { selections.Add(identity); item.SelectionIdentity = identity; }
        else if (identity != null) selections.Remove(identity);
        else if (selectionPath != null && selections.Count > 0 && !unresolvedRemovals.Contains(item)) unresolvedRemovals.Add(item);
        if (selectionPath != null && !TrySaveSelections())
        {
            if (boxed)
            {
                if (!wasSelected) selections.Remove(identity);
                item.SelectionIdentity = previousIdentity;
                Hide(item, false);
                LastOperationError="收纳记录无法保存，已尝试恢复图标";
                return false;
            }
            // Restoring an icon must not be undone just because the selection
            // file is locked. Keep the removal in memory and retry the write.
            selectionDirty = true;
        }
        if (!boxed) item.SelectionIdentity = null;
        return true;
    }
    internal bool SelectionPersistencePending { get { return selectionDirty; } }
    private bool TrySaveSelections()
    {
        if (selectionPath == null) { selectionDirty = false; return true; }
        try
        {
            foreach (var item in new List<TrayItem>(unresolvedRemovals))
            {
                string identity = Identity(item);
                if (identity == null) return false;
                selections.Remove(identity); unresolvedRemovals.Remove(item);
            }
            string temp = selectionPath + ".new";
            File.WriteAllLines(temp, selections);
            if (File.Exists(selectionPath)) File.Replace(temp, selectionPath, null); else File.Move(temp, selectionPath);
            selectionDirty = false; return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private void WriteRecovery()
    {
        var lines = new List<string>();
        foreach (var item in items.Values)
        {
            if (!item.Boxed) continue;
            uint pid; GetWindowThreadProcessId(item.Window, out pid);
            lines.Add(item.Window.ToInt64() + "|" + item.Id + "|" + item.Guid.ToString("D") + "|" + pid);
        }
        string temp = recoveryPath + ".new";
        File.WriteAllLines(temp, lines.ToArray());
        if (File.Exists(recoveryPath)) File.Replace(temp, recoveryPath, null); else File.Move(temp, recoveryPath);
    }

    public static void Watch(int parentId, string file)
    {
        try { using (var parent = Process.GetProcessById(parentId)) parent.WaitForExit(); } catch (ArgumentException) { }
        if (!File.Exists(file)) return;
        try
        {
            bool failed = false;
            foreach (string line in File.ReadAllLines(file))
            {
                var fields = line.Split('|'); long handle; uint id, originalPid; Guid guid;
                if (fields.Length != 4 || !Int64.TryParse(fields[0], out handle) || !UInt32.TryParse(fields[1], out id) ||
                    !Guid.TryParse(fields[2], out guid) || !UInt32.TryParse(fields[3], out originalPid)) continue;
                var hwnd = new IntPtr(handle); uint actualPid; GetWindowThreadProcessId(hwnd, out actualPid);
                if (originalPid == 0 || actualPid != originalPid || !IsWindow(hwnd)) continue;
                var data = new NotifyData { Size = (uint)Marshal.SizeOf(typeof(NotifyData)), Window = hwnd, Id = id,
                    Flags = NIF_STATE, State = 0, StateMask = 1, Tip = "", Info = "", InfoTitle = "" };
                if (guid != Guid.Empty) { data.Flags |= NIF_GUID; data.Guid = guid; }
                if (!Shell_NotifyIconW(NIM_MODIFY, ref data)) failed = true;
            }
            if (!failed) File.Delete(file);
        }
        catch { }
    }

    public bool Send(TrayItem item, uint message, Point point)
    {
        LastOperationError="";
        if (item.Callback == 0 || !IsWindow(item.Window)) { LastOperationError="图标回调缺失或应用已退出"; return false; }
        if(item.Boxed && item.Callback==6002 && message>=0x201 && message<=0x206) {
            var cls=new StringBuilder(80);
            GetClassNameW(item.Window,cls,cls.Capacity);
            if(cls.ToString()=="tray_icon_app" && !PrepareClickRectangle(item)) return false;
        }
        uint pid; GetWindowThreadProcessId(item.Window, out pid); AllowSetForegroundWindow(pid);
        long wp = item.Version >= 4 ? (uint)((ushort)point.X | ((uint)(ushort)point.Y << 16)) : item.Id;
        long lp = item.Version >= 4 ? (uint)((ushort)message | ((uint)(ushort)item.Id << 16)) : message;
        bool sent=SendNotifyMessageW(item.Window, item.Callback, new IntPtr(wp), new IntPtr(lp));
        if(!sent) LastOperationError=Marshal.GetLastWin32Error()==5 ? "Windows 拒绝点击转发：应用权限高于 TrayBox 或消息访问受限" : "点击转发失败：应用窗口可能已变化";
        return sent;
    }

    private static bool SetShellHidden(TrayItem item,bool hidden)
    {
        var data = new NotifyData { Size = (uint)Marshal.SizeOf(typeof(NotifyData)), Window = item.Window, Id = item.Id, Flags = NIF_STATE,
            State = hidden ? 1u : 0u, StateMask = 1, Tip = "", Info = "", InfoTitle = "" };
        if (item.Guid != Guid.Empty) { data.Flags |= NIF_GUID; data.Guid = item.Guid; }
        return Shell_NotifyIconW(NIM_MODIFY,ref data);
    }

    // tray-icon 0.25 discards both click and context-menu callbacks unless
    // Explorer returns S_OK for Shell_NotifyIconGetRect. Keep the selection and
    // recovery ledger unchanged while the original owner handles the callback.
    private bool PrepareClickRectangle(TrayItem item)
    {
        Rectangle rect;
        if (!clickVisibility.ContainsKey(item) && !GetIconBounds(item,out rect)) {
            if (!SetShellHidden(item,false)) { LastOperationError="Windows 未能提供应用需要的图标位置"; return false; }
        } else if (!clickVisibility.ContainsKey(item)) return true;
        clickVisibility[item]=DateTime.UtcNow.AddMilliseconds(400);
        if (GetIconBounds(item,out rect)) return true;
        LastOperationError="应用需要系统图标的位置，Windows 暂未返回。请重试或移出后打开。";
        return false;
    }
    private void EndClickVisibility()
    {
        if (clickVisibility.Count==0) return;
        foreach (var entry in new List<KeyValuePair<TrayItem,DateTime>>(clickVisibility)) {
            if(DateTime.UtcNow<entry.Value) continue;
            var item=entry.Key; TrayItem current;
            if (!item.Boxed || !items.TryGetValue(item.Key,out current) || current!=item || !IsWindow(item.Window) || SetShellHidden(item,true)) clickVisibility.Remove(item);
            else clickVisibility[item]=DateTime.UtcNow.AddSeconds(1);
        }
    }

    public void Dispose()
    {
        disposed = true;
        topTimer.Stop();
        StopCapture();
        foreach (var item in Snapshot()) if (item.Boxed) Hide(item, false);
        if (selectionDirty) TrySaveSelections();
        if (atom != 0) { UnregisterClassW(className, GetModuleHandleW(null)); atom = 0; }
        foreach (var item in items.Values) if (item.Image != null) item.Image.Dispose();
    }
    internal static bool IsOwnIcon(TrayItem item)
    {
        if (item == null || item.Window == IntPtr.Zero) return false;
        uint pid; GetWindowThreadProcessId(item.Window, out pid);
        return pid == (uint)Process.GetCurrentProcess().Id;
    }
}
