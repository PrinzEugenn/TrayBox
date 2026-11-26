using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace TrayBoxFluent;

public sealed partial class MainWindow
{
    private Border IconCard(UIElement child)
    {
        return new Border
        {
            Style=(Style)Application.Current.Resources["TrayBoxCardStyle"],
            Padding=new Thickness(12,6,12,6), Child=child
        };
    }

    private void AddIconSection(string title,StackPanel rows)
    {
        if(rows.Children.Count==0) return;
        iconResults.Children.Add(new TextBlock
        {
            Text=title, Style=(Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            Margin=new Thickness(0,10,0,2)
        });
        iconResults.Children.Add(rows);
    }

    private Border AvailableIconRow(TrayItem item,string label)
    {
        var row=new Grid { ColumnSpacing=10, MinHeight=34 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        var image=new Image { Width=20,Height=20,VerticalAlignment=VerticalAlignment.Center };
        LoadIcon(image,item);
        row.Children.Add(image);
        var toggle=new CheckBox
        {
            Content=new TextBlock { Text=label,TextWrapping=TextWrapping.Wrap },
            IsChecked=item.Boxed, MinWidth=0, HorizontalAlignment=HorizontalAlignment.Stretch
        };
        AutomationProperties.SetName(toggle,label);
        RoutedEventHandler change=delegate
        {
            bool wanted=toggle.IsChecked==true;
            if(wanted!=item.Boxed && !ChangeIcon(item,wanted)) toggle.IsChecked=item.Boxed;
        };
        toggle.Checked+=change;
        toggle.Unchecked+=change;
        Grid.SetColumn(toggle,1);
        row.Children.Add(toggle);
        return IconCard(row);
    }

    private FrameworkElement DiagnosisRow(string label,string detail,string source)
    {
        var body=new StackPanel { Spacing=3 };
        body.Children.Add(new TextBlock { Text=label,TextWrapping=TextWrapping.Wrap });
        body.Children.Add(new TextBlock
        {
            Text=detail, TextWrapping=TextWrapping.Wrap,
            Style=(Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Opacity=0.78
        });
        var details=new Flyout
        {
            Content=new StackPanel
            {
                Spacing=8,MaxWidth=320,
                Children=
                {
                    new TextBlock { Text=label,TextWrapping=TextWrapping.Wrap, Style=(Style)Application.Current.Resources["BodyStrongTextBlockStyle"] },
                    new TextBlock { Text=detail,TextWrapping=TextWrapping.Wrap },
                    new TextBlock { Text=source,TextWrapping=TextWrapping.Wrap, Style=(Style)Application.Current.Resources["CaptionTextBlockStyle"] }
                }
            }
        };
        var button=new Button
        {
            HorizontalAlignment=HorizontalAlignment.Stretch, HorizontalContentAlignment=HorizontalAlignment.Stretch,
            Style=(Style)Application.Current.Resources["SubtleButtonStyle"],
            Padding=new Thickness(12,9,12,9), Flyout=details
        };
        AutomationProperties.SetName(button,"查看 "+label+" 无法收纳的原因");
        var row=new Grid { ColumnSpacing=12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
        row.Children.Add(new FontIcon { Glyph="\uE946",FontSize=16,VerticalAlignment=VerticalAlignment.Center });
        Grid.SetColumn(body,1); row.Children.Add(body);
        var chevron=new FontIcon { Glyph="\uE76C",FontSize=12,VerticalAlignment=VerticalAlignment.Center };
        Grid.SetColumn(chevron,2); row.Children.Add(chevron);
        button.Content=row;
        return new Border { Child=button,Style=(Style)Application.Current.Resources["TrayBoxCardStyle"] };
    }
}
