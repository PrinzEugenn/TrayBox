using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace TrayBoxFluent;
public sealed partial class MainWindow
{
    private ComboBox appearanceTheme;
    private ComboBox appearanceIconSize;
    private ToggleSwitch appearanceAcrylic;
    private Slider appearanceTint;
    private void AddSettingsLink(string title,string description,string glyph,System.Action open)
    {
        var grid=new Grid {ColumnSpacing=12};
        grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto}); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        grid.Children.Add(new FontIcon {Glyph=glyph,FontSize=20,VerticalAlignment=VerticalAlignment.Center});
        var labels=new StackPanel {Spacing=3}; labels.Children.Add(new TextBlock {Text=title}); labels.Children.Add(new TextBlock {Text=description,FontSize=12,Opacity=0.65,TextWrapping=TextWrapping.Wrap});
        Grid.SetColumn(labels,1);grid.Children.Add(labels);
        var arrow=new FontIcon {Glyph="\uE76C",FontSize=12,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(arrow,2);grid.Children.Add(arrow);
        var link=new Button {Content=grid, HorizontalAlignment=HorizontalAlignment.Stretch, HorizontalContentAlignment=HorizontalAlignment.Stretch, Padding=new Thickness(14,12,14,12),CornerRadius=new CornerRadius(8)};
        AutomationProperties.SetName(link,title);
        link.Click+=delegate {open();}; ListItems.Children.Add(link);
    }
    private void SettingsSection(string title)
    {
        ListItems.Children.Add(new TextBlock {Text=title,FontSize=14,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Margin=new Thickness(0,12,0,4)});
    }
    private void SettingCard(string title,string description,FrameworkElement control)
    {
        var grid=new Grid {ColumnSpacing=16,RowSpacing=8};
        grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
        var labels=new StackPanel {Spacing=3,VerticalAlignment=VerticalAlignment.Center};
        labels.Children.Add(new TextBlock {Text=title,TextWrapping=TextWrapping.Wrap});
        if(description!=null) labels.Children.Add(new TextBlock {Text=description,FontSize=12,TextWrapping=TextWrapping.Wrap,Opacity=0.65});
        grid.Children.Add(labels);Grid.SetColumn(control,1);control.VerticalAlignment=VerticalAlignment.Center;control.HorizontalAlignment=HorizontalAlignment.Right;grid.Children.Add(control);AutomationProperties.SetName(control,title);
        grid.SizeChanged+=delegate {
            bool narrow=grid.ActualWidth<360;
            Grid.SetColumnSpan(labels,narrow ? 2 : 1);Grid.SetRow(control,narrow ? 1 : 0);Grid.SetColumn(control,narrow ? 0 : 1);Grid.SetColumnSpan(control,narrow ? 2 : 1);
            control.HorizontalAlignment=narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        };
        ListItems.Children.Add(new Border {Child=grid,Padding=new Thickness(14,12,14,12),Style=(Style)Application.Current.Resources["TrayBoxCardStyle"]});
    }
    private void RenderAppearance()
    {
        SwitchView(PanelView.Appearance);
        BackButton.Visibility=Visibility.Visible; PageTitle.Text="外观与启动"; PageSubtitle.Text="按你的习惯设置 TrayBox";
        Tiles.Visibility=Visibility.Collapsed; EmptyState.Visibility=Visibility.Collapsed; SectionHeader.Visibility=Visibility.Collapsed; ListItems.Visibility=Visibility.Visible;
        ListItems.Children.Clear(); ManageButton.Visibility=Visibility.Collapsed;
        SettingsSection("外观");
        appearanceTheme=new ComboBox {MinWidth=136}; appearanceTheme.Items.Add("跟随系统");appearanceTheme.Items.Add("浅色");appearanceTheme.Items.Add("深色");
        appearanceTheme.SelectedIndex=appearance.Theme=="light" ? 1 : appearance.Theme=="dark" ? 2 : 0;
        appearanceTheme.SelectionChanged+=delegate {appearance.Theme=appearanceTheme.SelectedIndex==1 ? "light" : appearanceTheme.SelectedIndex==2 ? "dark" : "system";SaveAppearance();};
        SettingCard("应用主题",null,appearanceTheme);
        AddBoxIconSizeSetting();
        var names=new ToggleSwitch {IsOn=appearance.ShowBoxIconNames,OnContent="",OffContent="",MinWidth=40};
        names.Toggled+=delegate {appearance.ShowBoxIconNames=names.IsOn;SaveAppearance();};
        SettingCard("显示收纳图标名称",null,names);
        appearanceAcrylic=new ToggleSwitch {IsOn=appearance.Acrylic,OnContent="",OffContent="",MinWidth=40};
        appearanceAcrylic.Toggled+=delegate {appearance.Acrylic=appearanceAcrylic.IsOn; if(appearanceTint!=null) appearanceTint.IsEnabled=appearance.Acrylic; SaveAppearance();};
        SettingCard("系统毛玻璃","使用 Windows Acrylic 材质",appearanceAcrylic);
        appearanceTint=new Slider {Minimum=80,Maximum=100,StepFrequency=1,Value=appearance.Opacity,Width=156,IsEnabled=appearance.Acrylic};
        appearanceTint.ValueChanged+=delegate {appearance.Opacity=(int)appearanceTint.Value;SaveAppearance();};
        SettingCard("背景色强度","调整材质上方的明暗遮罩",appearanceTint);
        SettingsSection("启动");
        var startup=new ToggleSwitch {IsOn=Startup.Enabled,OnContent="",OffContent="",MinWidth=40};
        startup.Toggled+=delegate {try {Startup.Enabled=startup.IsOn;} catch {Status.Text="无法更改启动设置";}};
        SettingCard("登录时自动启动","启动后驻留通知区域",startup);
        Status.Text="设置即时生效";
    }

    private void AddBoxIconSizeSetting()
    {
        appearanceIconSize=new ComboBox { MinWidth=104 };
        foreach(int size in AppearanceSettings.BoxIconSizes)
            appearanceIconSize.Items.Add(size==AppearanceSettings.DefaultBoxIconSize ? $"{size}（默认）" : size.ToString());
        appearanceIconSize.SelectedIndex=System.Array.IndexOf(AppearanceSettings.BoxIconSizes,appearance.BoxIconSize);
        var preview=new FontIcon
        {
            Glyph="\uE8B7",FontSize=appearance.BoxIconSize,Width=28,Height=28,
            VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false
        };
        var control=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
        control.Children.Add(preview);
        control.Children.Add(appearanceIconSize);
        appearanceIconSize.SelectionChanged+=delegate
        {
            if(appearanceIconSize.SelectedIndex<0) return;
            appearance.BoxIconSize=AppearanceSettings.BoxIconSizes[appearanceIconSize.SelectedIndex];
            preview.FontSize=appearance.BoxIconSize;
            SaveAppearance();
        };
        AutomationProperties.SetName(appearanceIconSize,"收纳图标尺寸");
        SettingCard("收纳图标尺寸","调整收纳盒里的图标边长",control);
    }

}
