using System;
using static TrayText;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace TrayBoxFluent;

internal sealed class QuickMenu : Window
{
    private bool wasActivated;
    private bool closing;
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int GetWindowLongW(IntPtr hwnd,int index);
    [DllImport("user32.dll")] private static extern int SetWindowLongW(IntPtr hwnd,int index,int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    internal QuickMenu(System.Drawing.Point point, ElementTheme theme, bool acrylic, Action open, Action settings, Action refresh, Action quit)
    {
        Title="TrayBox";
        if(acrylic) SystemBackdrop=new DesktopAcrylicBackdrop();
        var list=new StackPanel { Spacing=1, Padding=new Thickness(4), RequestedTheme=theme };
        Add(list,"\uE80F",TextFor(TextKey.OpenTrayBox),open);
        Add(list,"\uE713",TextFor(TextKey.Settings),settings);
        Add(list,"\uE72C",TextFor(TextKey.RefreshTrayIcons),refresh);
        list.Children.Add(new Border {Height=1,Margin=new Thickness(8,3,8,3),Style=(Style)Application.Current.Resources["TrayBoxDividerStyle"]});
        Add(list,"\uE711",TextFor(TextKey.ExitAndRestore),quit);
        var scroll=new ScrollViewer { Content=list,HorizontalScrollMode=ScrollMode.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        var surface=new Border {RequestedTheme=theme,Child=scroll};
        if(!acrylic) surface.Background=new SolidColorBrush(theme==ElementTheme.Dark ? Windows.UI.Color.FromArgb(255,38,38,38) : Windows.UI.Color.FromArgb(255,248,248,248));
        Content=surface;
        AppWindow.IsShownInSwitchers=false;
        list.KeyDown+=delegate(object sender,Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) { if(e.Key==Windows.System.VirtualKey.Escape) { e.Handled=true; Close(); } };
        var presenter=AppWindow.Presenter as OverlappedPresenter;
        if(presenter!=null) { presenter.SetBorderAndTitleBar(false,false); presenter.IsResizable=false; presenter.IsAlwaysOnTop=true; }
        var handle=WindowNative.GetWindowHandle(this);
        RemoveDialogFrame(handle);
        int rounded=2,dark=theme==ElementTheme.Dark ? 1 : 0;
        int noBorder=unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        DwmSetWindowAttribute(handle,33,ref rounded,sizeof(int));
        DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));
        DwmSetWindowAttribute(handle,34,ref noBorder,sizeof(int));
        var area=System.Windows.Forms.Screen.FromPoint(point).WorkingArea;
        AppWindow.Move(new PointInt32(point.X,point.Y));
        int dpi=(int)GetDpiForWindow(handle); if(dpi==0) dpi=96;
        double scale=dpi/96.0;
        list.Measure(new Windows.Foundation.Size(area.Width/scale,double.PositiveInfinity));
        int clientWidth=Math.Min(area.Width,(int)Math.Ceiling(list.DesiredSize.Width*scale));
        int clientHeight=Math.Min(area.Height,(int)Math.Ceiling(list.DesiredSize.Height*scale));
        AppWindow.ResizeClient(new SizeInt32(clientWidth,clientHeight));
        int width=AppWindow.Size.Width,height=AppWindow.Size.Height;
        int x=Math.Max(area.Left,Math.Min(area.Right-width,point.X-width/2));
        int y=Math.Max(area.Top,Math.Min(area.Bottom-height,point.Y-height));
        AppWindow.Move(new PointInt32(x,y));
        Activated += delegate(object sender,WindowActivatedEventArgs e) {
            if(e.WindowActivationState!=WindowActivationState.Deactivated) wasActivated=true;
            else if(wasActivated && !closing) { closing=true; Close(); }
        };
    }
    private static void RemoveDialogFrame(IntPtr handle)
    {
        const int styleIndex=-16,extendedStyleIndex=-20;
        const int dialogFrame=0x00400000,windowEdge=0x00000100;
        const uint applyFrameWithoutMoving=0x0037;
        // OverlappedPresenter leaves a dialog frame around the titleless window.
        SetWindowLongW(handle,styleIndex,GetWindowLongW(handle,styleIndex)&~dialogFrame);
        SetWindowLongW(handle,extendedStyleIndex,GetWindowLongW(handle,extendedStyleIndex)&~windowEdge);
        SetWindowPos(handle,IntPtr.Zero,0,0,0,0,applyFrameWithoutMoving);
    }
    private void Add(StackPanel panel, string glyph, string title, Action action)
    {
        var content=new StackPanel { Orientation=Orientation.Horizontal, Spacing=8 };
        var iconSlot=new Grid { Width=24, Height=24, VerticalAlignment=VerticalAlignment.Center };
        iconSlot.Children.Add(new FontIcon { Glyph=glyph, FontSize=16,
            HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center });
        content.Children.Add(iconSlot);
        content.Children.Add(new TextBlock { Text=title, VerticalAlignment=VerticalAlignment.Center });
        var button=new Button { Content=content, HorizontalAlignment=HorizontalAlignment.Stretch, HorizontalContentAlignment=HorizontalAlignment.Left, MinHeight=32, Padding=new Thickness(8,4,8,4),Style=(Style)Application.Current.Resources["SubtleButtonStyle"] };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button,title);
        button.Click += delegate {
            if(closing) return;
            closing=true;
            // Finish the current routed input/automation request before closing its window.
            DispatcherQueue.TryEnqueue(delegate { Close(); action(); });
        };
        panel.Children.Add(button);
    }
}
