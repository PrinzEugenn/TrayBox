using Microsoft.UI.Xaml;
using static TrayText;
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

    private void ApplyLanguageText()
    {
        WindowSurface.Language=TrayText.LanguageTag;
        SetButtonLabel(BackButton,TextKey.Back);
        SetButtonLabel(ManageButton,TextKey.Settings);
        ToolTipService.SetToolTip(OrganizeButton,TextFor(TextKey.OrganizeHint));
        AutomationProperties.SetName(UndoButton,TextFor(TextKey.Undo));
        UndoLabel.Text=TextFor(TextKey.Undo);
        NewGroupButton.Content=TextFor(TextKey.NewGroupButton);
        EmptyTitle.Text=TextFor(TextKey.EmptyBox);
        EmptyHint.Text=TextFor(TextKey.EmptyBoxHint);
    }

    private static void SetButtonLabel(Button button,TextKey label)
    {
        string text=TextFor(label);
        AutomationProperties.SetName(button,text);
        ToolTipService.SetToolTip(button,text);
    }

    private void AddLanguageSetting()
    {
        var choices=new[] { "system", "zh-Hans", "en" };
        var language=new ComboBox {MinWidth=136};
        language.Items.Add(TextFor(TextKey.SystemDefault));
        language.Items.Add("简体中文");
        language.Items.Add("English");
        language.SelectedIndex=System.Array.IndexOf(choices,appearance.Language);
        language.SelectionChanged+=delegate
        {
            if(language.SelectedIndex<0 || !ReferenceEquals(language,languageSetting)) return;
            string selected=choices[language.SelectedIndex];
            if(selected==appearance.Language) return;
            appearance.Language=selected;
            TrayText.Use(selected);
            inspectionGeneration++; boundsGeneration++; diagnosticRevision++;
            discoveryNotes.Clear(); targetedNameRetry.Clear();
            ApplyLanguageText();
            ManageClicked(this,null);
            languageSetting.Focus(FocusState.Programmatic);
            SaveAppearance();
        };
        languageSetting=language;
        SettingCard(TextFor(TextKey.Language),null,language);
    }

    private ComboBox languageSetting;
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
        BackButton.Visibility=Visibility.Visible; PageTitle.Text=TextFor(TextKey.AppearanceAndStartup); PageSubtitle.Text=TextFor(TextKey.AppearanceHint);
        Tiles.Visibility=Visibility.Collapsed; EmptyState.Visibility=Visibility.Collapsed; SectionHeader.Visibility=Visibility.Collapsed; ListItems.Visibility=Visibility.Visible;
        ListItems.Children.Clear(); ManageButton.Visibility=Visibility.Collapsed;
        SettingsSection(TextFor(TextKey.Appearance));
        appearanceTheme=new ComboBox {MinWidth=136}; appearanceTheme.Items.Add(TextFor(TextKey.SystemDefault));appearanceTheme.Items.Add(TextFor(TextKey.Light));appearanceTheme.Items.Add(TextFor(TextKey.Dark));
        appearanceTheme.SelectedIndex=appearance.Theme=="light" ? 1 : appearance.Theme=="dark" ? 2 : 0;
        appearanceTheme.SelectionChanged+=delegate {appearance.Theme=appearanceTheme.SelectedIndex==1 ? "light" : appearanceTheme.SelectedIndex==2 ? "dark" : "system";SaveAppearance();};
        SettingCard(TextFor(TextKey.Theme),null,appearanceTheme);
        AddBoxIconSizeSetting();
        var names=new ToggleSwitch {IsOn=appearance.ShowBoxIconNames,OnContent="",OffContent="",MinWidth=40};
        names.Toggled+=delegate {appearance.ShowBoxIconNames=names.IsOn;SaveAppearance();};
        SettingCard(TextFor(TextKey.ShowNames),null,names);
        appearanceAcrylic=new ToggleSwitch {IsOn=appearance.Acrylic,OnContent="",OffContent="",MinWidth=40};
        appearanceAcrylic.Toggled+=delegate {appearance.Acrylic=appearanceAcrylic.IsOn; if(appearanceTint!=null) appearanceTint.IsEnabled=appearance.Acrylic; SaveAppearance();};
        SettingCard(TextFor(TextKey.SystemAcrylic),TextFor(TextKey.AcrylicHint),appearanceAcrylic);
        appearanceTint=new Slider {Minimum=80,Maximum=100,StepFrequency=1,Value=appearance.Opacity,Width=156,IsEnabled=appearance.Acrylic};
        appearanceTint.ValueChanged+=delegate {appearance.Opacity=(int)appearanceTint.Value;SaveAppearance();};
        SettingCard(TextFor(TextKey.Tint),TextFor(TextKey.TintHint),appearanceTint);
        SettingsSection(TextFor(TextKey.Startup));
        var startup=new ToggleSwitch {IsOn=Startup.Enabled,OnContent="",OffContent="",MinWidth=40};
        startup.Toggled+=delegate {try {Startup.Enabled=startup.IsOn;} catch {Status.Text=TextFor(TextKey.StartupChangeFailed);}};
        SettingCard(TextFor(TextKey.StartAtSignIn),TextFor(TextKey.StartAtSignInHint),startup);
        Status.Text=TextFor(TextKey.SettingsApplyNow);
    }

    private void AddBoxIconSizeSetting()
    {
        appearanceIconSize=new ComboBox { MinWidth=104 };
        foreach(int size in AppearanceSettings.BoxIconSizes)
            appearanceIconSize.Items.Add(size==AppearanceSettings.DefaultBoxIconSize ? TextFor(TextKey.DefaultSize,size) : size.ToString());
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
        AutomationProperties.SetName(appearanceIconSize,TextFor(TextKey.IconSize));
        SettingCard(TextFor(TextKey.IconSize),TextFor(TextKey.IconSizeHint),control);
    }

}
