using System;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace TrayBoxFluent;

public sealed partial class MainWindow
{
    private readonly UISettings systemUi=new UISettings();
    private readonly AccessibilitySettings accessibility=new AccessibilitySettings();
    private Storyboard pageTransition;


    private void SetUpTitleBar()
    {
        if(!AppWindowTitleBar.IsCustomizationSupported()) return;
        CaptionRegion.Visibility=Visibility.Visible;
        ExtendsContentIntoTitleBar=true;
        SetTitleBar(CaptionRegion);
        if(File.Exists(App.IconPath))
            CaptionIcon.Source=new BitmapImage(new Uri(App.IconPath)) { DecodePixelWidth=16 };
        WindowSurface.SizeChanged+=delegate { AlignTitleBar(); };
        WindowSurface.Loaded+=delegate { AlignTitleBar(); };
        Activated+=delegate(object sender,WindowActivatedEventArgs e)
        {
            CaptionIdentity.Opacity=e.WindowActivationState==WindowActivationState.Deactivated ? 0.65 : 1;
        };
    }

    private void AlignTitleBar()
    {
        var scale=WindowSurface.XamlRoot?.RasterizationScale ?? 1;
        CaptionInset.Width=new GridLength(AppWindow.TitleBar.LeftInset/scale);
        CaptionRegion.Height=Math.Max(32,AppWindow.TitleBar.Height/scale);
    }

    private void StyleCaptionButtons(bool dark)
    {
        if(!ExtendsContentIntoTitleBar) return;
        var titleBar=AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor=Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor=Colors.Transparent;
        if(accessibility.HighContrast)
        {
            titleBar.ButtonForegroundColor=null;
            titleBar.ButtonInactiveForegroundColor=null;
            titleBar.ButtonHoverForegroundColor=null;
            titleBar.ButtonPressedForegroundColor=null;
            titleBar.ButtonHoverBackgroundColor=null;
            titleBar.ButtonPressedBackgroundColor=null;
            return;
        }
        byte onGlass=dark ? (byte)255 : (byte)28;
        byte dimmed=dark ? (byte)177 : (byte)105;
        byte highlight=dark ? (byte)255 : (byte)0;
        titleBar.ButtonForegroundColor=Color.FromArgb(255,onGlass,onGlass,onGlass);
        titleBar.ButtonHoverForegroundColor=titleBar.ButtonForegroundColor;
        titleBar.ButtonPressedForegroundColor=titleBar.ButtonForegroundColor;
        titleBar.ButtonInactiveForegroundColor=Color.FromArgb(255,dimmed,dimmed,dimmed);
        titleBar.ButtonHoverBackgroundColor=Color.FromArgb(24,highlight,highlight,highlight);
        titleBar.ButtonPressedBackgroundColor=Color.FromArgb(42,highlight,highlight,highlight);
    }

    private void AnimatePage(bool animateHeading=true)
    {
        pageTransition?.Stop();
        pageTransition=null;
        if(!showing || !systemUi.AnimationsEnabled) return;
        pageTransition=new Storyboard { FillBehavior=FillBehavior.Stop };
        AddPageAnimation(PageBody,"Opacity",0.45,1);
        if(animateHeading)
        {
            AddPageAnimation(PageHeading,"Opacity",0.58,1);
            AddPageAnimation(HeadingOffset,"Y",5,0,true);
        }
        pageTransition.Begin();
    }

    private void AddPageAnimation(DependencyObject target,string property,double from,double to,bool dependent=false)
    {
        var animation=new DoubleAnimation
        {
            From=from, To=to, Duration=new Duration(TimeSpan.FromMilliseconds(170)),
            EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut },
            EnableDependentAnimation=dependent
        };
        Storyboard.SetTarget(animation,target);
        Storyboard.SetTargetProperty(animation,property);
        pageTransition.Children.Add(animation);
    }
}
