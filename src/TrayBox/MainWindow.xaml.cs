using System;
using static TrayText;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.Storage.Streams;
using WinRT.Interop;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Image = Microsoft.UI.Xaml.Controls.Image;
namespace TrayBoxFluent;
public sealed partial class MainWindow : Microsoft.UI.Xaml.Window, IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct MouseData { public int X, Y; public uint Data, Flags, Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeMouseInput { public int X,Y; public uint Data,Flags,Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeKeyboardInput { public ushort Key,Scan; public uint Flags,Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Explicit)] private struct NativeInputUnion { [FieldOffset(0)] public NativeMouseInput Mouse; [FieldOffset(0)] public NativeKeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public NativeInputUnion Data; }
    private static readonly IntPtr CancelDragMarker = new IntPtr(0x54524258);
    private delegate IntPtr MouseProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int id, MouseProc proc, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr window, ref Point point);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassNameW(IntPtr hwnd, System.Text.StringBuilder name, int max);
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    private readonly TrayEngine engine;
    private readonly string appearancePath;
    private readonly AppearanceSettings appearance;
    private readonly string sizesPath;
    private readonly WindowSizes sizes;
    private readonly BoxLayout layout;
    private enum PanelView { Box, Menu, Manage, Icons, Appearance }
    private PanelView view;
    private readonly Dictionary<string, Rectangle> sourceBounds = new Dictionary<string, Rectangle>();
    private readonly Dictionary<string, Rectangle> accessibleBounds = new Dictionary<string, Rectangle>();
    private DateTime accessibleBoundsAt, nextBoundsInspection;
    private DateTime sourceBoundsAt;
    private readonly Dictionary<string,string> discoveryNotes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    private bool boundsInspectionRunning;
    private int boundsGeneration;
    private List<VisibleTrayIcon> visibleTrayIcons;
    private bool inspectionRunning;
    private bool inspectionFailed;
    private int inspectionGeneration;
    private bool targetedRecoveryRunning;
    private readonly HashSet<string> targetedAttempts = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string,DateTime> targetedNameRetry = new Dictionary<string,DateTime>(StringComparer.OrdinalIgnoreCase);
    private readonly MouseProc mouseProc;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer boundsTimer;
    private IntPtr hook;
    private bool showing, tracking, dragCancelled, navigating, managing, itemPressed, disposed;
    private string sourceKey;
    private Point dragStart;
    private TrayItem pressedItem;
    private bool pressedRight;
    private bool itemDragging;
    private Point itemPressPoint;
    private Point? itemReleasePoint;
    private string lastClickKey;
    private DateTime lastClickTime;
    private readonly IntPtr hwnd;
    [System.Diagnostics.Conditional("TRAYBOX_TEST")]
    private static void Trace(string line) { }
    internal MainWindow(TrayEngine value, Action quitAction, string settingsPath, AppearanceSettings settings = null)
    {
        InitializeComponent(); engine = value; appearancePath = settingsPath; appearance = settings ?? AppearanceSettings.LoadFrom(settingsPath);
        TrayText.Use(appearance.Language);
        ApplyLanguageText();
        sizesPath = Path.Combine(Path.GetDirectoryName(settingsPath), "window-sizes.ini"); sizes = WindowSizes.Load(sizesPath);
        layout = BoxLayout.Load(Path.Combine(Path.GetDirectoryName(settingsPath), "box-layout.ini"));
        hwnd = WindowNative.GetWindowHandle(this);
        if (File.Exists(App.IconPath)) AppWindow.SetIcon(App.IconPath);
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.SetBorderAndTitleBar(true, true); presenter.IsResizable = true; presenter.IsMinimizable = true; presenter.IsMaximizable = true; presenter.IsAlwaysOnTop = false;
            try { presenter.PreferredMinimumWidth = 320; presenter.PreferredMinimumHeight = 260; } catch (NotImplementedException) { }
        }
        SetUpTitleBar();
        AppWindow.Closing += delegate(AppWindow sender, AppWindowClosingEventArgs args) { if (!disposed) { args.Cancel = true; Hide(); } };
        previewTimer=DispatcherQueue.CreateTimer();
        previewTimer.Interval=TimeSpan.FromMilliseconds(16);
        previewTimer.Tick+=delegate { DrawDragFrame(); };
        InitializeGroupDrag();
        mouseProc = OnMouse;
        boundsTimer = DispatcherQueue.CreateTimer();
        boundsTimer.Interval=TimeSpan.FromMilliseconds(250);
        boundsTimer.Tick += delegate {
            if (!showing || IsMinimized() || tracking || itemPressed || navigating) return;
            RefreshBounds();
            if (DateTime.UtcNow>=nextBoundsInspection) StartBoundsInspection();
        };
        engine.Changed += EngineChanged;
        Root.ActualThemeChanged += delegate { ApplyAppearance(); };
        ApplyAppearance();
        Root.AllowDrop = true;
        Root.KeyDown += delegate(object sender, KeyRoutedEventArgs e) {
            if(e.Key!=Windows.System.VirtualKey.Escape) return;
            if(groupDrag?.Released==true) { FinishGroupDrag(); e.Handled=true; }
            else if(CancelGroupDrag()) e.Handled=true;
            else if(organizing) { OrganizeClicked(this,e); e.Handled=true; }
            else if(view!=PanelView.Box) { NavigateBack(); e.Handled=true; }
        };
    }
    private void EngineChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if(disposed || !showing || tracking || itemPressed || groupDrag!=null) return;
            if(managing) RenderIconSettings();
            else if(!navigating && BoxPresentationChanged()) Render();
        });
    }
    internal bool AcrylicEnabled => appearance.Acrylic;
    internal ElementTheme CurrentTheme { get { return Root.ActualTheme; } }
    internal void ShowBox(Point point) => OpenPanel(point,false);
    internal void OpenSettings(Point point) => OpenPanel(point,true);

    private void OpenPanel(Point point, bool settings)
    {
        navigating=false; managing=false;
        if (showing)
        {
            var currentPresenter=AppWindow.Presenter as OverlappedPresenter;
            if (currentPresenter!=null && currentPresenter.State==OverlappedPresenterState.Minimized) currentPresenter.Restore();
            if (settings) ManageClicked(this,null); else Render();
            AppWindow.Show(); Activate(); return;
        }
        showing = true; view = PanelView.Box; boundsGeneration++;
        if (settings) ManageClicked(this,null); else Render();
        RefreshBounds();
        int dpi = (int)GetDpiForWindow(hwnd); if (dpi < 96) dpi = 96;
        int width = sizes.BoxWidth * dpi / 96, height = sizes.BoxHeight * dpi / 96;
        var target = sizes.HasPosition ? new Point(sizes.X,sizes.Y) : point;
        var area = System.Windows.Forms.Screen.FromPoint(target).WorkingArea;
        width=Math.Min(width,area.Width); height=Math.Min(height,area.Height);
        var position=WindowSizes.Place(area,width,height,sizes.HasPosition ? new Point?(new Point(sizes.X,sizes.Y)) : null);
        AppWindow.MoveAndResize(new RectInt32(position.X,position.Y,width,height));
        AppWindow.Show(); Activate();
        AnimatePage();
        boundsTimer.Start();
        StartBoundsInspection();
        dragPreview.RunOnWindowThread(() => {
            if(hook==IntPtr.Zero) hook=SetWindowsHookEx(14,mouseProc,IntPtr.Zero,0);
            if(hook==IntPtr.Zero) DispatcherQueue.TryEnqueue(() => Status.Text=TextFor(TextKey.DragUnavailable));
        });
    }
    private void Hide()
    {
        if (!showing) return;
        SaveViewSize();
        showing = false; tracking = false; dragCancelled = false; sourceKey = null; boundsGeneration++;
        engine.DragInProgress=false;
        CancelGroupDrag();
        EndDragVisual();
        accessibleBounds.Clear(); accessibleBoundsAt=DateTime.MinValue;
        itemPressed = false; itemDragging = false; pressedItem = null; itemReleasePoint=null;
        boundsTimer.Stop();
        dragPreview.RunOnWindowThread(() => {
            if(hook==IntPtr.Zero) return;
            UnhookWindowsHookEx(hook);
            hook=IntPtr.Zero;
        });
        AppWindow.Hide();
    }
    private void RefreshBounds()
    {
        if(engine.IsCapturing) { sourceBounds.Clear(); sourceBoundsAt=DateTime.MinValue; return; }
        sourceBounds.Clear();
        foreach (var item in engine.Snapshot())
        {
            Rectangle rect;
            if (!item.Boxed && item.Callback != 0 && !TrayEngine.IsOwnIcon(item))
            {
                bool found=engine.TryGetBounds(item,out rect);
                Rectangle accessible;
                if (!found && (DateTime.UtcNow-accessibleBoundsAt).TotalMilliseconds<400 && accessibleBounds.TryGetValue(item.Key,out accessible)) { rect=accessible; found=true; }
                if (found) sourceBounds[item.Key] = rect;
            }
        }
        sourceBoundsAt=DateTime.UtcNow;
    }
    private void StartBoundsInspection()
    {
        nextBoundsInspection=DateTime.UtcNow.AddSeconds(1);
        if (boundsInspectionRunning) return;
        boundsInspectionRunning=true; IntPtr trayWindow=engine.NativeTrayWindow; int generation=boundsGeneration;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate {
            List<VisibleTrayIcon> icons;
            try { icons=VisibleTrayInspector.Read(trayWindow); } catch { icons=null; }
            DispatcherQueue.TryEnqueue(delegate {
                boundsInspectionRunning=false;
                if (disposed || !showing || navigating || generation!=boundsGeneration || icons==null) return;
                Trace("bounds scan count=" + icons.Count);
                var mapped=new Dictionary<string,Rectangle>(); var ambiguous=new HashSet<string>(); var captured=engine.Snapshot();
                foreach (var icon in icons)
                {
                    if (icon.Bounds.Width>80 || icon.Bounds.Height>80) continue;
                    var match=VisibleTrayInspector.MatchUniqueName(icon,captured);
                    if (match==null || ambiguous.Contains(match.Key)) continue;
                    if (mapped.ContainsKey(match.Key)) { mapped.Remove(match.Key); ambiguous.Add(match.Key); }
                    else mapped[match.Key]=icon.Bounds;
                }
                accessibleBounds.Clear(); foreach (var entry in mapped) accessibleBounds[entry.Key]=entry.Value;
                accessibleBoundsAt=DateTime.UtcNow;
                if (!tracking && !itemPressed) RefreshBounds();
                StartTargetedRecovery(icons,generation,false,false);
            });
        });
    }
    private string FindSource(Point p)
    {
        if(engine.IsCapturing || (DateTime.UtcNow-sourceBoundsAt).TotalMilliseconds>300) return null;
        string key = null;
        foreach (var pair in sourceBounds) if (pair.Value.Contains(p)) { if (key != null) return null; key = pair.Key; }
        return key;
    }
    private TrayItem ResolveSource()
    {
        if (sourceKey == null) return null;
        foreach (var item in engine.Snapshot()) if (!item.Boxed && item.Callback != 0 && item.Key == sourceKey && !TrayEngine.IsOwnIcon(item)) return item;
        return null;
    }
    private bool Inside(Point p) { NativeRect r; return GetWindowRect(hwnd, out r) && Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom).Contains(p); }
    private bool IsDropTarget(Point p)
    {
        if (!Inside(p)) return false;
        IntPtr hit=WindowFromPoint(p);
        return hit==hwnd || (hit!=IntPtr.Zero && GetAncestor(hit,2)==hwnd);
    }
    private bool IsExplorerTrayAt(Point p)
    {
        IntPtr hit=WindowFromPoint(p); if (hit==IntPtr.Zero) return false;
        IntPtr root=GetAncestor(hit,2); if (root==IntPtr.Zero) root=hit;
        uint pid, trayPid; GetWindowThreadProcessId(root,out pid); GetWindowThreadProcessId(engine.NativeTrayWindow,out trayPid);
        if (pid==0 || pid!=trayPid) return false;
        var name=new System.Text.StringBuilder(128); GetClassNameW(root,name,name.Capacity);
        string cls=name.ToString();
        return cls=="TopLevelWindowForOverflowXamlIsland" || cls=="Shell_TrayWnd" || cls=="Shell_SecondaryTrayWnd";
    }
    // Explorer's tray drag is not exposed as a normal XAML DragEnter/DragOver.
    // End only that native drag once it is over our actual window; our hook
    // keeps tracking the user's physical release. Never target other foreground
    // applications, unknown icons, or a point covered by another window.
    private bool EndExplorerPreview()
    {
        if ((GetAsyncKeyState(1) & 0x8000)==0) return false;
        IntPtr foreground=GetForegroundWindow(); uint foregroundPid, trayPid;
        GetWindowThreadProcessId(foreground,out foregroundPid);
        GetWindowThreadProcessId(engine.NativeTrayWindow,out trayPid);
        if (foreground==IntPtr.Zero || trayPid==0 || foregroundPid!=trayPid) return false;
        var cls=new System.Text.StringBuilder(128); GetClassNameW(foreground,cls,cls.Capacity);
        string name=cls.ToString();
        if (foreground!=engine.NativeTrayWindow && name!="Shell_SecondaryTrayWnd" && name!="TopLevelWindowForOverflowXamlIsland") return false;
        var input=new NativeInput { Type=0, Data=new NativeInputUnion { Mouse=new NativeMouseInput { Flags=0x0004, Extra=CancelDragMarker } } };
        return SendInput(1,new[] { input },Marshal.SizeOf(typeof(NativeInput)))==1;
    }
    private bool IsMinimized() { var presenter=AppWindow.Presenter as OverlappedPresenter; return presenter!=null && presenter.State==OverlappedPresenterState.Minimized; }
    private static int Distance(Point a, Point b) { return Math.Abs(a.X-b.X) + Math.Abs(a.Y-b.Y); }
    private IntPtr OnMouse(int code, IntPtr wparam, IntPtr lparam)
    {
        uint msg=unchecked((uint)wparam.ToInt64());
        if(code>=0 && (msg==0x201 || msg==0x202 || msg==0x205))
        {
            var data=Marshal.PtrToStructure<MouseData>(lparam);
            if(msg==0x202 && (data.Flags&1)!=0 && data.Extra==CancelDragMarker)
                DispatcherQueue.TryEnqueue(() => Trace("ignored our native-preview release"));
            else
                DispatcherQueue.TryEnqueue(() => ProcessGlobalButton(msg,new Point(data.X,data.Y)));
        }
        return CallNextHookEx(hook,code,wparam,lparam);
    }

    private void ProcessGlobalButton(uint message,Point point)
    {
        if(!showing || IsMinimized()) return;
        Trace("global "+message.ToString("X")+" pressed="+itemPressed);
        if(itemPressed && (message==0x202 && !pressedRight || message==0x205 && pressedRight))
        {
            itemReleasePoint=point;
            CompleteClick();
        }
        if(message==0x202 && tracking)
        {
            FinishDrag(IsDropTarget(point),Distance(point,dragStart)>=12,point);
            return;
        }
        if(message!=0x201 || navigating || !IsExplorerTrayAt(point)) return;

        tracking=true;
        dragCancelled=false;
        dragStart=point;
        sourceKey=engine.FindVisibleIconAt(point)?.Key ?? FindSource(point);
        var source=ResolveSource();
        Trace("source cached="+(sourceKey ?? "null")+" point="+point);
        engine.DragInProgress=source!=null;
        StartDragFrames(source);
    }
    private void FinishDrag(bool inside, bool moved, Point release)
    {
        Trace("finish inside=" + inside + " moved=" + moved + " source=" + (sourceKey ?? "null"));
        string before=null; GroupTarget group=null;
        if(inside && moved) {
            group=DropSlot(release,out before);
            Trace("release="+release+" group="+group?.Name+" before="+tileTargets.Find(t=>t.Key==before)?.Item.Tip);
        }
        tracking = false; dragCancelled = false;
        EndDragVisual();
        try
        {
            if (inside && moved)
            {
                var item = ResolveSource();
                if (item == null) Status.Text = TextFor(TextKey.DragUnknown);
                else {
                    if(ChangeIcon(item,true,group?.Id,before)) Activate();
                }
            }
        }
        finally { sourceKey = null; engine.DragInProgress=false; }
    }
    private void Render()
    {
        SwitchView(PanelView.Box);
        BackButton.Visibility=Visibility.Collapsed; PageTitle.Text="TrayBox"; PageSubtitle.Text=TextFor(TextKey.TrayIcons);
        Tiles.Visibility=Visibility.Visible; ListItems.Visibility=Visibility.Collapsed;
        SectionHeader.Visibility=Visibility.Visible; ManageButton.Visibility=Visibility.Visible;
        OrganizeButton.Visibility=Visibility.Visible;
        OrganizeButton.Content=organizing ? TextFor(TextKey.Done) : TextFor(TextKey.Organize);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(OrganizeButton,organizing ? TextFor(TextKey.DoneOrganizing) : TextFor(TextKey.OrganizeIcons));
        NewGroupButton.Visibility=organizing ? Visibility.Visible : Visibility.Collapsed;
        RenderBoxContents(); RefreshBounds();
    }
    private void CompleteClick()
    {
        if (!itemPressed || pressedItem == null) return;
        Trace("tile release right=" + pressedRight + " moved=" + itemDragging + " at=" + System.Windows.Forms.Cursor.Position);
        var point=itemReleasePoint ?? System.Windows.Forms.Cursor.Position;
        var item=pressedItem; bool right=pressedRight, moved=itemDragging || Distance(point,itemPressPoint)>=12; itemPressed=false; itemDragging=false; pressedItem=null; itemReleasePoint=null;
        string before=null;
        var group=!right && moved && IsDropTarget(point) ? DropSlot(point,out before) : null;
        EndDragVisual();
        if(organizing) {
            lastClickKey=null;
            if(!right && moved) CompleteOrganizeMove(item,group,before);
            else if(!right) Status.Text=TextFor(TextKey.TileHint);
            return;
        }
        if (!right && moved)
        {
            lastClickKey=null;
            if (!Inside(point))
            {
                ChangeIcon(item,false);
            }
            else CompleteOrganizeMove(item,group,before);
            return;
        }
        bool doubleClick=!right && lastClickKey==item.Key && (DateTime.UtcNow-lastClickTime).TotalMilliseconds <= GetDoubleClickTime();
        lastClickKey=right || doubleClick ? null : item.Key; lastClickTime=DateTime.UtcNow;
        bool delivered=true; string failure="";
        if (!right && !engine.Send(item,doubleClick ? 0x203u : 0x201u,point)) { delivered=false; failure=engine.LastOperationError; }
        if (!engine.Send(item,right ? 0x205u : 0x202u,point)) { delivered=false; failure=engine.LastOperationError; }
        if (item.Version>=3 && !engine.Send(item,right ? 0x7bu : 0x400u,point)) { delivered=false; failure=engine.LastOperationError; }
        if (!delivered) Status.Text=failure;
    }
    private static async void LoadIcon(Image image, TrayItem item)
    {
        if (item.Image == null) return;
        try {
            byte[] bytes; using (var bitmap=item.Image.ToBitmap()) using (var stream=new MemoryStream()) { bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png); bytes=stream.ToArray(); }
            using (var random=new InMemoryRandomAccessStream()) { using (var writer=new DataWriter(random)) { writer.WriteBytes(bytes); await writer.StoreAsync(); await writer.FlushAsync(); writer.DetachStream(); } random.Seek(0); var source=new BitmapImage(); await source.SetSourceAsync(random); image.Source=source; }
        } catch { }
    }
    private void ManageClicked(object sender, RoutedEventArgs e)
    {
        navigating=true; managing=false; inspectionGeneration++;
        SwitchView(PanelView.Manage);
        BackButton.Visibility=Visibility.Visible; PageTitle.Text=TextFor(TextKey.Settings); PageSubtitle.Text="TrayBox";
        Tiles.Visibility=Visibility.Collapsed; EmptyState.Visibility=Visibility.Collapsed; SectionHeader.Visibility=Visibility.Collapsed;
        ListItems.Visibility=Visibility.Visible; ManageButton.Visibility=Visibility.Collapsed; ListItems.Children.Clear();
        AddSettingsLink(TextFor(TextKey.AppearanceAndStartup),TextFor(TextKey.AppearanceLinkHint),"\uE790",RenderAppearance);
        AddSettingsLink(TextFor(TextKey.TrayIcons),TextFor(TextKey.TrayIconsLinkHint),"\uE8B7",OpenIconSettings);
        AddLanguageSetting();
        Status.Text="";
    }
    internal void OpenIconSettings()
    {
        navigating=true; managing=true; visibleTrayIcons=null; inspectionGeneration++;
        SwitchView(PanelView.Icons);
        BackButton.Visibility=Visibility.Visible; PageTitle.Text=TextFor(TextKey.TrayIcons); PageSubtitle.Text=TextFor(TextKey.TrayIconsHint);
        Tiles.Visibility=Visibility.Collapsed; EmptyState.Visibility=Visibility.Collapsed; SectionHeader.Visibility=Visibility.Collapsed;
        ListItems.Visibility=Visibility.Visible; ManageButton.Visibility=Visibility.Collapsed; ListItems.Children.Clear();
        var tools=new Grid { ColumnSpacing=8, Margin=new Thickness(0,0,0,8) };
        tools.ColumnDefinitions.Add(new ColumnDefinition());
        tools.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
        iconSearch=new TextBox { PlaceholderText=TextFor(TextKey.SearchIcons), VerticalAlignment=VerticalAlignment.Center };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(iconSearch,TextFor(TextKey.SearchIcons));
        tools.Children.Add(iconSearch);
        var refresh=new Button { Content=TextFor(TextKey.Refresh), VerticalAlignment=VerticalAlignment.Center };
        refresh.Click += delegate { visibleTrayIcons=null; inspectionGeneration++; diagnosticRevision++; targetedAttempts.Clear(); targetedNameRetry.Clear(); discoveryNotes.Clear(); engine.Refresh(); RenderIconSettings(); Status.Text=TextFor(TextKey.Refreshing); };
        Grid.SetColumn(refresh,1);
        tools.Children.Add(refresh);
        ListItems.Children.Add(tools);
        iconResults=new StackPanel { Spacing=4 };
        ListItems.Children.Add(iconResults);
        iconSearch.TextChanged+=delegate { RenderIconSettings(); };
        RenderIconSettings();
    }
    private TextBox iconSearch;
    private StackPanel iconResults;
    private int diagnosticRevision;
    private void RenderIconSettings()
    {
        iconResults.Children.Clear();
        var query=iconSearch.Text.Trim();
        var selected=new StackPanel { Spacing=4 };
        var available=new StackPanel { Spacing=4 };
        var unavailable=new StackPanel { Spacing=4 };
        var captured=engine.Snapshot();
        captured.Sort((a,b)=>StringComparer.CurrentCultureIgnoreCase.Compare(a.Tip,b.Tip));
        foreach (var item in captured)
        {
            if (TrayEngine.IsOwnIcon(item)) continue;
            string label=String.IsNullOrWhiteSpace(item.Tip) ? TextFor(TextKey.AppIcon) : item.Tip.Replace('\n',' ');
            if(!label.Contains(query,StringComparison.OrdinalIgnoreCase)) continue;
            var issue=item.Boxed ? null : TrayEngine.BoxingIssue(item);
            if(issue!=null)
                unavailable.Children.Add(DiagnosisRow(label,issue,TextFor(TextKey.PartialRegistration)));
            else
                (item.Boxed ? selected : available).Children.Add(AvailableIconRow(item,label));
        }
        AddIconSection(TextFor(TextKey.Collected),selected);
        AddIconSection(TextFor(TextKey.Available),available);
        if (visibleTrayIcons == null)
        {
            iconResults.Children.Add(new TextBlock { Text=TextFor(TextKey.Inspecting), TextWrapping=TextWrapping.Wrap, Margin=new Thickness(3,10,3,0) });
            StartVisibleInspection();
        }
        else
        {
            if (inspectionFailed) iconResults.Children.Add(new TextBlock { Text=TextFor(TextKey.InspectFailed), TextWrapping=TextWrapping.Wrap, Opacity=0.7, Margin=new Thickness(3,10,3,2) });
            foreach (var icon in visibleTrayIcons)
            {
                if (!icon.Name.Contains(query,StringComparison.OrdinalIgnoreCase) || VisibleTrayInspector.IsKnown(icon,captured,sourceBounds)) continue;
                if(!discoveryNotes.TryGetValue(icon.Name,out var note)) note=TextFor(TextKey.RegistrationMissing);
                unavailable.Children.Add(DiagnosisRow(icon.Name.Replace('\n',' '),note,TextFor(TextKey.RegistrationNeeded)));
            }
            if(selected.Children.Count+available.Children.Count+unavailable.Children.Count==0 && query.Length>0)
                iconResults.Children.Add(new TextBlock { Text=TextFor(TextKey.NoMatchingIcons), Opacity=0.7, Margin=new Thickness(3,8,3,0) });
        }
        AddIconSection(TextFor(TextKey.Unavailable),unavailable);
        UpdateUndo(engine.SelectionPersistencePending ? TextFor(TextKey.PendingSave) : "");
    }
    private void StartVisibleInspection()
    {
        if (inspectionRunning) return;
        inspectionRunning=true; IntPtr trayWindow=engine.NativeTrayWindow; int generation=inspectionGeneration;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate {
            List<VisibleTrayIcon> icons;
            try { icons=VisibleTrayInspector.Read(trayWindow); } catch { icons=null; }
            DispatcherQueue.TryEnqueue(delegate {
                inspectionRunning=false;
                if (disposed) return;
                if (generation != inspectionGeneration) { if (showing && managing) StartVisibleInspection(); return; }
                inspectionFailed=icons==null; visibleTrayIcons=icons ?? new List<VisibleTrayIcon>();
                if (showing && managing) { RefreshBounds(); RenderIconSettings(); if (icons!=null) StartTargetedRecovery(icons,generation); }
            });
        });
    }
    private void StartTargetedRecovery(List<VisibleTrayIcon> icons, int generation, bool simulateVisibleForTest=false, bool forManagement=true)
    {
        if (targetedRecoveryRunning) return;
        var names=new List<string>(); var captured=engine.Snapshot();
        foreach (var icon in icons)
        {
            DateTime retry;
            if (VisibleTrayInspector.MatchUniqueName(icon,captured)==null && (!targetedNameRetry.TryGetValue(icon.Name,out retry) || DateTime.UtcNow>=retry)) names.Add(icon.Name);
        }
        if (names.Count==0) return;
        targetedRecoveryRunning=true; IntPtr trayWindow=engine.NativeTrayWindow;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate {
            List<VisibleTrayIcon> current; List<TargetedTrayProcess> targets;
            var identityNotes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            try {
                    current=VisibleTrayInspector.Read(trayWindow);
                var stillVisible=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (current!=null) foreach(var icon in current) stillVisible.Add(icon.Name);
                var namesToResolve=new List<string>(); foreach(var name in names) if(stillVisible.Contains(name)) namesToResolve.Add(name);
                targets=TargetedTrayDiscovery.Resolve(namesToResolve,identityNotes);
            } catch { current=null; targets=new List<TargetedTrayProcess>(); }
            DispatcherQueue.TryEnqueue(delegate {
                targetedRecoveryRunning=false;
                if (disposed || !showing) return;
                if (forManagement ? (!managing || generation!=inspectionGeneration) : (navigating || generation!=boundsGeneration))
                {
                    if(managing && visibleTrayIcons!=null) StartTargetedRecovery(visibleTrayIcons,inspectionGeneration);
                    return;
                }
                if(current==null)
                {
                    foreach(var name in names) discoveryNotes[name]=TextFor(TextKey.UnableToRecheck);
                    if(forManagement) RenderIconSettings();
                    return;
                }
                foreach(var name in names) targetedNameRetry[name]=DateTime.UtcNow.AddSeconds(30);
                foreach(var note in identityNotes) discoveryNotes[note.Key]=note.Value;
                if (tracking || itemPressed || IsMinimized() || TrayEngine.AnyMouseButtonDown())
                {
                    foreach(var name in names) targetedNameRetry[name]=DateTime.UtcNow.AddSeconds(1);
                    return;
                }
                var ids=new List<int>(); var latest=engine.Snapshot();
                foreach(var target in targets)
                {
                    if(targetedAttempts.Contains(target.Key) || !TargetedTrayDiscovery.StillMatches(target)) continue;
                    bool stillMissing=false;
                    foreach(var icon in current)
                    {
                        if(!target.Names.Exists(name=>String.Equals(name,icon.Name,StringComparison.OrdinalIgnoreCase))) continue;
                        if(VisibleTrayInspector.MatchUniqueName(icon,latest)==null) { stillMissing=true; break; }
                    }
                    if(stillMissing) ids.Add(target.Id);
                }
                if(ids.Count>0)
                {
                    bool sent=engine.RefreshProcesses(ids);
                    foreach(var target in targets)
                        if(engine.DiscoveryNotified.Contains(target.Id)) targetedAttempts.Add(target.Key);
                    string waiting=TextFor(TextKey.WaitingForRegistration);
                    var pending=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                    foreach(var name in names)
                    {
                        var candidates=targets.FindAll(target=>ids.Contains(target.Id) && target.Names.Contains(name));
                        if(candidates.Count==0) continue;
                        bool accepted=candidates.Exists(target=>engine.DiscoveryNotified.Contains(target.Id));
                        bool denied=candidates.Exists(target=>engine.DiscoveryDenied.Contains(target.Id));
                        identityNotes.TryGetValue(name,out var identityNote);
                        string context=identityNote!=null ? identityNote+"\n": "";
                        if(denied && accepted) context+=TextFor(TextKey.RegistrationPartlyDenied)+"\n";
                        discoveryNotes[name]=context+(accepted ? waiting : denied ? TextFor(TextKey.RegistrationDenied) : TextFor(TextKey.RegistrationNotDelivered));
                        if(accepted) pending[name]=context;
                    }
                    if (forManagement && managing) RenderIconSettings();
                    if(sent) {
                        var timer=DispatcherQueue.CreateTimer(); timer.Interval=TimeSpan.FromSeconds(2); timer.IsRepeating=false;
                        int attemptRevision=diagnosticRevision;
                        timer.Tick+=delegate
                        {
                            timer.Stop();
                            if(disposed || attemptRevision!=diagnosticRevision) return;
                            foreach(var pair in pending)
                                if(discoveryNotes.TryGetValue(pair.Key,out var note) && note==pair.Value+waiting)
                                    discoveryNotes[pair.Key]=pair.Value+TextFor(TextKey.RegistrationStillIncomplete);
                            if(showing && managing) RenderIconSettings();
                        };
                        timer.Start();
                    }
                }
                else if(forManagement && identityNotes.Count>0) RenderIconSettings();
            });
        });
    }
    private void ApplyAppearance()
    {
        var theme = appearance.Theme == "dark" ? ElementTheme.Dark : appearance.Theme == "light" ? ElementTheme.Light : ElementTheme.Default;
        if (WindowSurface.RequestedTheme != theme) WindowSurface.RequestedTheme=theme;
        bool dark=Root.ActualTheme==ElementTheme.Dark;
        StyleCaptionButtons(dark);
        if(accessibility.HighContrast)
        {
            SystemBackdrop=null;
            WindowSurface.Background=new SolidColorBrush(systemUi.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background));
        }
        else if (appearance.Acrylic)
        {
            if (!(SystemBackdrop is Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop)) SystemBackdrop=new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
            byte shade=(byte)(dark ? 0 : 255), alpha=(byte)((appearance.Opacity-80)*4);
            WindowSurface.Background=new SolidColorBrush(Windows.UI.Color.FromArgb(alpha,shade,shade,shade));
        }
        else
        {
            SystemBackdrop=null;
            byte shade=(byte)(dark ? 38 : 248);
            WindowSurface.Background=new SolidColorBrush(Windows.UI.Color.FromArgb(255,shade,shade,shade));
        }
    }
    private void SaveAppearance()
    {
        ApplyAppearance();
        try { appearance.SaveTo(appearancePath); Status.Text=TextFor(TextKey.SettingsSaved); }
        catch { Status.Text=TextFor(TextKey.SettingsSaveFailed); }
    }
    private void BackClicked(object sender, RoutedEventArgs args) { NavigateBack(); }
    private void NavigateBack()
    {
        if (view == PanelView.Appearance || view == PanelView.Icons) ManageClicked(this,null);
        else { navigating=false; managing=false; Render(); }
    }
    private void SaveViewSize()
    {
        if (!showing) return;
        var presenter=AppWindow.Presenter as OverlappedPresenter;
        if (presenter!=null && presenter.State!=OverlappedPresenterState.Restored) return;
        int dpi=(int)GetDpiForWindow(hwnd); if (dpi<96) dpi=96;
        var current=AppWindow.Size;
        sizes.Update(false, current.Width*96/dpi, current.Height*96/dpi);
        var position=AppWindow.Position; sizes.X=position.X; sizes.Y=position.Y; sizes.HasPosition=true;
        try { sizes.Save(sizesPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    private void SwitchView(PanelView next)
    {
        if(next!=PanelView.Box) CancelGroupDrag();
        if(next!=PanelView.Box && next!=PanelView.Icons) UndoButton.Visibility=Visibility.Collapsed;
        OrganizeButton.Visibility=next==PanelView.Box ? Visibility.Visible : Visibility.Collapsed;
        if(next!=PanelView.Box) organizing=false;
        if (view == next) return;
        SaveViewSize(); view=next;
        AnimatePage();
    }
    public void Dispose() { if (disposed) return; Hide(); EndDragVisual(); dragPreview.Dispose(); disposed=true; engine.Changed-=EngineChanged; Close(); }
}
