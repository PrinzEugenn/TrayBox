using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using Wpf=System.Windows;
using Controls=System.Windows.Controls;
using Media=System.Windows.Media;

namespace TrayBoxFluent;

internal sealed class DragPreviewWindow : IDisposable
{
    [DllImport("user32")] private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32")] private static extern int GetWindowLongW(IntPtr window,int index);
    [DllImport("user32")] private static extern int SetWindowLongW(IntPtr window,int index,int value);
    [DllImport("user32")] private static extern IntPtr MonitorFromPoint(Point point,uint fallback);
    [DllImport("shcore")] private static extern int GetDpiForMonitor(IntPtr monitor,int kind,out uint x,out uint y);

    private readonly object gate=new object();
    private readonly Queue<Action> startup=new Queue<Action>();
    private Thread thread;
    private Dispatcher dispatcher;
    private DispatcherTimer timer;
    private Wpf.Window surface;
    private IntPtr handle,monitor;
    private Controls.StackPanel content;
    private Controls.Image image;
    private Controls.TextBlock caption;
    private Point? lastCursor;
    private Rectangle screen;
    private Size size;
    private int offset;

    internal void Start(TrayItem item,int iconSize)
    {
        Media.Imaging.BitmapSource bitmap=null;
        if(item.Image!=null)
        {
            bitmap=Wpf.Interop.Imaging.CreateBitmapSourceFromHIcon(item.Image.Handle,
                Wpf.Int32Rect.Empty,Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
        }
        var name=String.IsNullOrWhiteSpace(item.Tip) ? "应用" : item.Tip.Replace('\n',' ').Replace('\r',' ').Trim();
        RunOnWindowThread(() =>
        {
            CreateSurface();
            image.Source=bitmap;
            image.Width=image.Height=iconSize;
            caption.Text=name;
            lastCursor=null;
            monitor=IntPtr.Zero;
            Follow();
            timer.Start();
        });
    }

    internal void RunOnWindowThread(Action action)
    {
        lock(gate)
        {
            if(thread==null)
            {
                thread=new Thread(Run) { IsBackground=true, Name="TrayBox drag preview" };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
            Post(action);
        }
    }

    private void Run()
    {
        var owner=Dispatcher.CurrentDispatcher;
        timer=new DispatcherTimer(DispatcherPriority.Input,owner) { Interval=TimeSpan.FromMilliseconds(8) };
        timer.Tick+=delegate { Follow(); };
        lock(gate)
        {
            dispatcher=owner;
            while(startup.Count>0) dispatcher.BeginInvoke(startup.Dequeue());
        }
        Dispatcher.Run();
    }

    // Keep start, hide and shutdown in order even while the thread starts.
    private void Post(Action action)
    {
        if(dispatcher==null) startup.Enqueue(action);
        else dispatcher.BeginInvoke(action);
    }

    private void CreateSurface()
    {
        if(surface!=null) return;
        image=new Controls.Image { Width=24, Height=24, HorizontalAlignment=Wpf.HorizontalAlignment.Center };
        Media.RenderOptions.SetBitmapScalingMode(image,Media.BitmapScalingMode.HighQuality);
        caption=new Controls.TextBlock
        {
            FontFamily=new Media.FontFamily("Segoe UI"), FontSize=12, Margin=new Wpf.Thickness(4,3,4,3),
            Foreground=Media.Brushes.White, MaxWidth=152, TextTrimming=Wpf.TextTrimming.CharacterEllipsis,
            TextAlignment=Wpf.TextAlignment.Center,
            Effect=new Media.Effects.DropShadowEffect { BlurRadius=4, ShadowDepth=1, Opacity=1 }
        };
        content=new Controls.StackPanel { MinWidth=32 };
        content.Children.Add(image);
        content.Children.Add(caption);
        surface=new Wpf.Window
        {
            Title="TrayBox Drag Preview", Background=Media.Brushes.Transparent, AllowsTransparency=true,
            WindowStyle=Wpf.WindowStyle.None, ResizeMode=Wpf.ResizeMode.NoResize, SizeToContent=Wpf.SizeToContent.WidthAndHeight,
            ShowActivated=false, ShowInTaskbar=false, Topmost=true, Focusable=false, Content=content
        };
        handle=new Wpf.Interop.WindowInteropHelper(surface).EnsureHandle();
        const int extendedStyle=-20, transparent=0x20, noActivate=0x08000000;
        SetWindowLongW(handle,extendedStyle,GetWindowLongW(handle,extendedStyle)|transparent|noActivate);
    }

    private void Follow()
    {
        var cursor=System.Windows.Forms.Cursor.Position;
        if(cursor==lastCursor) return;
        lastCursor=cursor;
        var display=MonitorFromPoint(cursor,2);
        if(monitor!=display)
        {
            monitor=display;
            double scale=GetDpiForMonitor(display,0,out uint dpi,out _)==0 ? dpi/96.0 : 1;
            content.Measure(new Wpf.Size(double.PositiveInfinity,double.PositiveInfinity));
            size=new Size((int)Math.Ceiling(content.DesiredSize.Width*scale),(int)Math.Ceiling(content.DesiredSize.Height*scale));
            offset=(int)Math.Round(16*scale);
            screen=System.Windows.Forms.Screen.FromPoint(cursor).Bounds;
        }
        int x=cursor.X+offset,y=cursor.Y+offset;
        if(x+size.Width>screen.Right) x=cursor.X-offset-size.Width;
        if(y+size.Height>screen.Bottom) y=cursor.Y-offset-size.Height;
        const uint noActivateOrResize=0x215;
        SetWindowPos(handle,IntPtr.Zero,Math.Max(screen.Left,x),Math.Max(screen.Top,y),0,0,noActivateOrResize);
        if(!surface.IsVisible) surface.Show();
    }

    internal void Hide()
    {
        lock(gate) if(thread!=null) Post(() =>
        {
            timer.Stop();
            surface?.Hide();
        });
    }

    public void Dispose()
    {
        lock(gate) if(thread!=null) Post(() =>
        {
            timer.Stop();
            surface?.Close();
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
        });
        thread?.Join(TimeSpan.FromSeconds(1));
    }
}
