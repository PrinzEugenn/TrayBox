using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Point=System.Drawing.Point;

namespace TrayBoxFluent;
public sealed partial class MainWindow
{
    private bool organizing;
    private sealed class TileTarget { internal Button Element; internal TrayItem Item; internal string Key,Group,Tip; internal System.Drawing.Icon Image; }
    private sealed class GroupTarget
    {
        internal Border Element, Slot;
        internal TranslateTransform Offset;
        internal string Id,Name;
        internal int Index;
    }
    private readonly List<TileTarget> tileTargets=new List<TileTarget>();
    private readonly List<GroupTarget> groupTargets=new List<GroupTarget>();
    private Border organizeHighlight;
    private bool pendingAtRender;
    private int BoxTileSide => appearance.BoxIconSize+2*(5+(appearance.BoxIconSize-AppearanceSettings.DefaultBoxIconSize)/4);
    private int BoxTileHeight => BoxTileSide+(appearance.ShowBoxIconNames ? 20 : 0);
    private static string LayoutKey(TrayItem item) { return item.SelectionIdentity ?? item.Key; }
    private Dictionary<string,TrayItem> BoxItems()
    {
        var result=new Dictionary<string,TrayItem>(StringComparer.Ordinal);
        foreach(var item in engine.Snapshot()) if(item.Boxed) result[LayoutKey(item)]=item;
        return result;
    }
    private bool BoxPresentationChanged()
    {
        var items=BoxItems();
        if(items.Count!=tileTargets.Count || pendingAtRender!=engine.SelectionPersistencePending) return true;
        foreach(var tile in tileTargets)
            if(!items.TryGetValue(tile.Key,out var current) || current!=tile.Item || current.Tip!=tile.Tip || current.Image!=tile.Image) return true;
        return false;
    }
    private void RenderBoxContents()
    {
        CancelGroupDrag();
        pendingAtRender=engine.SelectionPersistencePending;
        var items=BoxItems(); string saveError=null;
        try { layout.Remember(items.Keys); } catch(System.IO.IOException) { saveError="无法保存图标顺序，请检查磁盘空间"; } catch(UnauthorizedAccessException) { saveError="没有权限保存图标顺序"; }
        Tiles.Items.Clear(); tileTargets.Clear(); groupTargets.Clear(); organizeHighlight=null;
        int groupIndex=-1;
        foreach(var group in layout.Groups)
        {
            groupIndex++;
            var ordered=layout.Order(items.Keys,group.Id);
            if(ordered.Count==0 && !organizing && group.Id==BoxLayout.DefaultGroup && items.Count==0) continue;
            var section=new StackPanel { Spacing=8 };
            var header=new Grid { ColumnSpacing=8 };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var title=new TextBlock { Text=(group.Id==BoxLayout.DefaultGroup && !organizing ? "已收纳" : group.Name)+"  ·  "+ordered.Count, FontSize=12, FontWeight=Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment=VerticalAlignment.Center, TextTrimming=TextTrimming.CharacterEllipsis, Margin=new Thickness(4,0,0,0) };
            if(group.Id!=BoxLayout.DefaultGroup) header.Children.Add(CreateGroupDragHandle(group,title));
            else header.Children.Add(title);
            if(organizing && group.Id!=BoxLayout.DefaultGroup) {
                var more=new Button { Content=new FontIcon { Glyph="\uE712",FontSize=16 },Padding=new Thickness(6),Style=(Style)Application.Current.Resources["SubtleButtonStyle"] };
                AutomationProperties.SetName(more,"分组操作："+group.Name);
                ToolTipService.SetToolTip(more,"排序与分组操作");
                var menu=new MenuFlyout();
                AddAction(menu,"上移分组",()=>ChangeGroupOrder(group.Id,group.Name,layout.GroupIndex(group.Id)-1),layout.CanMoveGroup(group.Id,-1),"\uE70E");
                AddAction(menu,"下移分组",()=>ChangeGroupOrder(group.Id,group.Name,layout.GroupIndex(group.Id)+1),layout.CanMoveGroup(group.Id,1),"\uE70D");
                menu.Items.Add(new MenuFlyoutSeparator());
                AddAction(menu,"重命名",()=>ShowGroupEditor(group));
                AddAction(menu,"解散分组",()=>ChangeLayout(()=>layout.RemoveGroup(group.Id)));
                more.Flyout=menu; Grid.SetColumn(more,1); header.Children.Add(more);
            }
            section.Children.Add(header);
            var grid=new ItemsControl { HorizontalContentAlignment=HorizontalAlignment.Stretch, MinHeight=organizing ? BoxTileHeight : 0 };
            grid.ItemsPanel=(ItemsPanelTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><ItemsWrapGrid Orientation='Horizontal'/></ItemsPanelTemplate>");
            foreach(var key in ordered) { var tile=CreateTile(items[key],group.Id); grid.Items.Add(tile); }
            section.Children.Add(grid);
            if(ordered.Count==0) section.Children.Add(new TextBlock { Text="将图标拖到此处分组",FontSize=12,Opacity=0.65,Margin=new Thickness(4,0,4,4) });
            var offset=new TranslateTransform();
            var card=new Border { Child=section, Padding=new Thickness(8), Style=(Style)Application.Current.Resources["TrayBoxCardStyle"], RenderTransform=offset };
            var slot=new Border { Child=card,Margin=new Thickness(0,0,0,GroupSpacing) };
            groupTargets.Add(new GroupTarget { Element=card,Slot=slot,Offset=offset,Id=group.Id,Name=group.Name,Index=groupIndex });
            Tiles.Items.Add(slot);
        }
        EmptyState.Visibility=Tiles.Items.Count==0 && !organizing ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text=items.Count+" 个图标";
        UpdateUndo(engine.SelectionPersistencePending ? "收纳记录暂未保存，TrayBox 会继续重试" :
            organizing ? "拖动图标或分组标题调整顺序" :
            groupTargets.Exists(group=>group.Id!=BoxLayout.DefaultGroup) ? "拖动分组标题可排序" :
            items.Count==0 ? "从通知区域拖入图标" : "拖出窗口可移出图标");
        if(saveError!=null) Status.Text=saveError;
    }
    private Button CreateTile(TrayItem item,string group)
    {
        string label=(item.Tip ?? "应用").Replace('\n',' ').Replace('\r',' ');
        int iconSize=appearance.BoxIconSize;
        int tileWidth=BoxTileSide+(appearance.ShowBoxIconNames ? 28 : 0);
        var stack=new StackPanel { Spacing=4, HorizontalAlignment=HorizontalAlignment.Center };
        var image=new Image { Width=iconSize,Height=iconSize,HorizontalAlignment=HorizontalAlignment.Center }; stack.Children.Add(image); LoadIcon(image,item);
        if(appearance.ShowBoxIconNames)
            stack.Children.Add(new TextBlock { Text=label,Width=tileWidth-8,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,FontSize=12 });
        var button=new Button { Width=tileWidth,Height=BoxTileHeight,MinWidth=0,MinHeight=0,Padding=new Thickness(0),Margin=new Thickness(2),Content=stack,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Style=(Style)Application.Current.Resources["SubtleButtonStyle"] };
        AutomationProperties.SetName(button,label); ToolTipService.SetToolTip(button,label);
        var target=new TileTarget {Element=button,Item=item,Key=LayoutKey(item),Group=group,Tip=item.Tip,Image=item.Image}; tileTargets.Add(target);
        if(organizing) { button.ContextFlyout=TileMenu(target); button.Flyout=TileMenu(target); }
        button.AddHandler(UIElement.PointerPressedEvent,new PointerEventHandler(delegate(object sender,PointerRoutedEventArgs e) {
            var state=e.GetCurrentPoint(button).Properties; bool right=state.IsRightButtonPressed;
            if((!right && !state.IsLeftButtonPressed) || (organizing && right)) return;
            itemPressed=true; pressedItem=item; pressedRight=right; itemDragging=false; itemReleasePoint=null; itemPressPoint=System.Windows.Forms.Cursor.Position;
            NativeRect r; if(GetWindowRect(hwnd,out r)) item.Anchor=System.Drawing.Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);
            if(right) engine.Send(item,0x204u,itemPressPoint);
            else StartDragFrames(item);
        }),true);
        button.AddHandler(UIElement.PointerReleasedEvent,new PointerEventHandler(delegate { CompleteClick(); }),true);
        if(!organizing) button.AddHandler(UIElement.KeyDownEvent,new KeyEventHandler((sender,args) =>
        {
            if(args.Key!=Windows.System.VirtualKey.Enter && args.Key!=Windows.System.VirtualKey.Space) return;
            args.Handled=true;
            if(args.KeyStatus.WasKeyDown) return;
            Point center=ScreenCenter(button);
            if(!engine.Send(item,0x201u,center) || !engine.Send(item,0x202u,center) ||
                (item.Version>=3 && !engine.Send(item,0x400u,center))) Status.Text=engine.LastOperationError;
        }),true);
        return button;
    }
    private Point ScreenCenter(FrameworkElement element)
    {
        var center=element.TransformToVisual(WindowSurface).TransformPoint(
            new Windows.Foundation.Point(element.ActualWidth/2,element.ActualHeight/2));
        double scale=Root.XamlRoot.RasterizationScale;
        var point=new Point((int)(center.X*scale),(int)(center.Y*scale));
        ClientToScreen(hwnd,ref point);
        return point;
    }
    private static void AddAction(MenuFlyout menu,string text,Action action,bool enabled=true,string glyph=null)
    {
        var entry=new MenuFlyoutItem {Text=text,IsEnabled=enabled};
        if(glyph!=null) entry.Icon=new FontIcon { Glyph=glyph };
        entry.Click+=delegate {action();}; menu.Items.Add(entry);
    }
    private MenuFlyout TileMenu(TileTarget tile)
    {
        var menu=new MenuFlyout(); var order=layout.Order(BoxItems().Keys,tile.Group); int index=order.IndexOf(tile.Key);
        AddAction(menu,"前移",()=>ChangeIcon(tile.Item,true,tile.Group,order[index-1]),index>0);
        AddAction(menu,"后移",()=>ChangeIcon(tile.Item,true,tile.Group,index+2<order.Count ? order[index+2] : null),index>=0 && index<order.Count-1);
        var groups=new MenuFlyoutSubItem {Text="移到分组"};
        foreach(var group in layout.Groups) {
            var choice=new MenuFlyoutItem {Text=group.Name,IsEnabled=group.Id!=tile.Group};
            choice.Click+=delegate {ChangeIcon(tile.Item,true,group.Id);}; groups.Items.Add(choice);
        }
        menu.Items.Add(groups); return menu;
    }
    private void OrganizeClicked(object sender,RoutedEventArgs args) { organizing=!organizing; lastClickKey=null; Render(); AnimatePage(); }
    private void NewGroupClicked(object sender,RoutedEventArgs args) { ShowGroupEditor(null); }
    private async void ShowGroupEditor(BoxGroup group)
    {
        var name=new TextBox {Text=group==null ? "" : group.Name,PlaceholderText="例如：工作、系统工具",MaxLength=32,MinWidth=220};
        AutomationProperties.SetName(name,"分组名称");
        var error=new TextBlock {TextWrapping=TextWrapping.Wrap,Foreground=(Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],Visibility=Visibility.Collapsed};
        var content=new StackPanel {Spacing=8}; content.Children.Add(name); content.Children.Add(error);
        var dialog=new ContentDialog {XamlRoot=Root.XamlRoot,RequestedTheme=Root.ActualTheme,Title=group==null ? "新建分组" : "重命名分组",Content=content,PrimaryButtonText="保存",CloseButtonText="取消",DefaultButton=ContentDialogButton.Primary};
        dialog.PrimaryButtonClick+=delegate(ContentDialog sender,ContentDialogButtonClickEventArgs e) {
            try {if(group==null) layout.AddGroup(name.Text); else layout.RenameGroup(group.Id,name.Text);}
            catch(Exception ex) when(ex is ArgumentException || ex is System.IO.IOException || ex is UnauthorizedAccessException) {e.Cancel=true; error.Text=ex is ArgumentException ? ex.Message : "无法保存分组，请检查配置文件权限与磁盘空间"; error.Visibility=Visibility.Visible;}
        };
        await dialog.ShowAsync(); if(!disposed && view==PanelView.Box) Render();
    }
    private void ChangeLayout(Action action)
    {
        try { action(); lastChange=null; undoCompleted=false; Render(); Status.Text="分组已解散"; }
        catch(System.IO.IOException) {Status.Text="无法保存整理结果，请检查磁盘空间";}
        catch(UnauthorizedAccessException) {Status.Text="没有权限保存整理结果";}
    }
    private bool ContainsPoint(FrameworkElement element,Point screen,out double relativeX)
    {
        relativeX=0; if(!ScreenToClient(hwnd,ref screen) || Root.XamlRoot==null) return false;
        var p=element.TransformToVisual(WindowSurface).TransformPoint(new Windows.Foundation.Point(0,0)); double scale=Root.XamlRoot.RasterizationScale;
        relativeX=screen.X/scale-p.X; double y=screen.Y/scale-p.Y;
        return relativeX>=0 && relativeX<=element.ActualWidth && y>=0 && y<=element.ActualHeight;
    }
    private GroupTarget OrganizeTarget(Point point,out string before)
    {
        before=null;
        double unused;
        if(!IsDropTarget(point) || !ContainsPoint(ContentScroll,point,out unused)) return null;
        foreach(var group in groupTargets) {
            if(!ContainsPoint(group.Element,point,out unused)) continue;
            Point client=point;
            if(!ScreenToClient(hwnd,ref client)) return null;
            double scale=Root.XamlRoot.RasterizationScale, x=client.X/scale,y=client.Y/scale;
            foreach(var tile in tileTargets) {
                if(tile.Group!=group.Id) continue;
                var origin=tile.Element.TransformToVisual(WindowSurface).TransformPoint(new Windows.Foundation.Point());
                if(y<origin.Y-2 || (y<=origin.Y+tile.Element.ActualHeight+2 && x<origin.X+tile.Element.ActualWidth/2)) {
                    before=tile.Key; break;
                }
            }
            return group;
        }
        return null;
    }
    private void ClearOrganizeHighlight()
    {
        if(organizeHighlight!=null) organizeHighlight.ClearValue(Border.BorderBrushProperty);
        organizeHighlight=null;
        InsertionMarker.Visibility=Visibility.Collapsed;
    }
    private readonly DragPreviewWindow dragPreview=new DragPreviewWindow();
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer previewTimer;
    private TrayItem dragVisualItem;
    private Point? paintedPoint;
    private GroupTarget paintedGroup;
    private string paintedBefore;
    private double paintedScrollOffset;
    private bool paintedTarget;
    private bool previewShown;
    private string statusBeforeDrag;
    private void StartDragFrames(TrayItem item)
    {
        if(item==null) return;
        dragVisualItem=item;
        if(!previewTimer.IsRunning) previewTimer.Start();
    }
    private void EndDragVisual()
    {
        previewTimer.Stop();
        dragVisualItem=null; paintedPoint=null; paintedGroup=null; paintedBefore=null;
        if(!previewShown) return;
        dragPreview.Hide();
        previewShown=false;
        foreach(var tile in tileTargets)
            ToolTipService.SetToolTip(tile.Element,AutomationProperties.GetName(tile.Element));
        DropHighlight.Visibility=Visibility.Collapsed;
        ClearOrganizeHighlight();
        Status.Text=statusBeforeDrag;
    }
    private void DrawDragFrame()
    {
        if(groupDrag!=null) { DrawGroupDragFrame(); return; }
        var item=dragVisualItem;
        if(item==null || !showing || IsMinimized() || !(itemPressed || tracking)) { EndDragVisual(); return; }
        var point=System.Windows.Forms.Cursor.Position;
        if(itemPressed && !itemDragging) {
            if(Distance(point,itemPressPoint)<12) return;
            itemDragging=true;
        }
        bool target=IsDropTarget(point);
        if(tracking && !dragCancelled)
        {
            if(!target || Distance(point,dragStart)<12) return;
            dragCancelled=true;
            engine.CancelNativeDrag();
            Trace("native preview release sent="+EndExplorerPreview());
        }
        if(!previewShown)
        {
            statusBeforeDrag=Status.Text;
            foreach(var tile in tileTargets) ToolTipService.SetToolTip(tile.Element,null);
            dragPreview.Start(item,appearance.BoxIconSize);
            previewShown=true;
        }
        if(target) ScrollNearEdge(point);
        double scrollOffset=ContentScroll.VerticalOffset;
        if(paintedPoint==point && paintedTarget==target && paintedScrollOffset==scrollOffset) return;
        string before=null;
        var group=target ? OrganizeTarget(point,out before) : null;
        bool sameSlot=paintedPoint.HasValue && paintedGroup==group && paintedBefore==before && paintedScrollOffset==scrollOffset;
        paintedPoint=point;
        paintedTarget=target;
        paintedScrollOffset=scrollOffset;
        paintedGroup=group; paintedBefore=before;
        bool acceptsBlank=target && group==null && !itemPressed;
        DropHighlight.Visibility=acceptsBlank ? Visibility.Visible : Visibility.Collapsed;
        string feedback=group!=null ? "放到「"+group.Name+"」" :
            acceptsBlank ? "松开以收纳" :
            itemPressed && !Inside(point) && !organizing ? "松开以移出" : "松开以取消";
        if(Status.Text!=feedback) Status.Text=feedback;
        if(sameSlot) return;
        if(organizeHighlight!=group?.Element)
        {
            ClearOrganizeHighlight();
            organizeHighlight=group?.Element;
            if(organizeHighlight!=null) organizeHighlight.BorderBrush=(Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        }
        bool hasMarker=false;
        if(group!=null) {
            TileTarget position=null;
            foreach(var tile in tileTargets) if(tile.Group==group.Id && (before==null || tile.Key==before)) position=tile;
            if(position!=null) {
                var origin=position.Element.TransformToVisual(Root).TransformPoint(new Windows.Foundation.Point(0,0));
                var viewport=ContentScroll.TransformToVisual(Root).TransformPoint(new Windows.Foundation.Point());
                double top=Math.Max(origin.Y+2,viewport.Y);
                double bottom=Math.Min(origin.Y+position.Element.ActualHeight-2,viewport.Y+ContentScroll.ActualHeight);
                if(bottom>top) {
                    Canvas.SetLeft(InsertionMarker,origin.X-Root.Padding.Left+(before==null ? position.Element.ActualWidth : -4));
                    Canvas.SetTop(InsertionMarker,top-Root.Padding.Top);
                    InsertionMarker.Height=bottom-top;
                    hasMarker=true;
                }
            }
        }
        InsertionMarker.Visibility=hasMarker ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ScrollNearEdge(Point screen)
    {
        if(ContentScroll.ScrollableHeight<=0 || !ScreenToClient(hwnd,ref screen)) return;
        var origin=ContentScroll.TransformToVisual(WindowSurface).TransformPoint(new Windows.Foundation.Point());
        double y=screen.Y/Root.XamlRoot.RasterizationScale-origin.Y;
        if(y<0 || y>ContentScroll.ActualHeight) return;
        double delta=y<18 ? -8 : y>ContentScroll.ActualHeight-18 ? 8 : 0;
        if(delta!=0) ContentScroll.ChangeView(null,Math.Clamp(ContentScroll.VerticalOffset+delta,0,ContentScroll.ScrollableHeight),null,true);
    }
    private GroupTarget DropSlot(Point point,out string before)
    {
        if(paintedPoint==point && paintedGroup!=null) {
            before=paintedBefore;
            return paintedGroup;
        }
        return OrganizeTarget(point,out before);
    }
    private void CompleteOrganizeMove(TrayItem item,GroupTarget group,string before)
    {
        if(group==null) {Render(); Status.Text="已取消移动，图标仍在收纳盒内"; return;}
        ChangeIcon(item,true,group.Id,before);
    }
}
