using System;
using static TrayText;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Point=System.Drawing.Point;

namespace TrayBoxFluent;

public sealed partial class MainWindow
{
    private const double GroupSpacing=12;
    private const double DropDuration=155;

    private sealed class GroupDragSession
    {
        internal GroupTarget Source;
        internal uint PointerId;
        internal Point Start, Position;
        internal bool IsMouse, Lifted, Released, Committed, Animate;
        internal string PreviousStatus;
        internal double GrabY, Height, SettleFrom;
        internal long LastFrame, ReleasedAt;
        internal int Destination;
    }
    private GroupDragSession groupDrag;
    private readonly RectangleGeometry groupDragClip=new RectangleGeometry();

    private void InitializeGroupDrag()
    {
        GroupDragLayer.Clip=groupDragClip;
        Root.AddHandler(UIElement.PointerMovedEvent,new PointerEventHandler((_,args)=>
        {
            if(groupDrag is { Released:false, IsMouse:false } drag && drag.PointerId==args.Pointer.PointerId)
                drag.Position=GroupPointerPosition(args);
        }),true);
        Root.AddHandler(UIElement.PointerReleasedEvent,new PointerEventHandler((_,args)=>
        {
            var drag=groupDrag;
            if(drag==null || drag.Released || drag.PointerId!=args.Pointer.PointerId) return;
            ReleaseGroupDrag(GroupPointerPosition(args));
            args.Handled=true;
        }),true);
        Root.PointerCanceled+=GroupCaptureLost;
        Root.PointerCaptureLost+=GroupCaptureLost;
    }

    private void GroupCaptureLost(object sender,PointerRoutedEventArgs args)
    {
        if(groupDrag is { Released:false } drag && drag.PointerId==args.Pointer.PointerId) CancelGroupDrag();
    }

    private Grid CreateGroupDragHandle(BoxGroup group,TextBlock title)
    {
        var grip=new Grid
        {
            Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            ColumnSpacing=5,MinHeight=24,ManipulationMode=ManipulationModes.None
        };
        grip.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
        grip.ColumnDefinitions.Add(new ColumnDefinition());
        var dots=new GeometryGroup();
        for(int row=0;row<3;row++)
            for(int column=0;column<2;column++)
                dots.Children.Add(new EllipseGeometry
                {
                    Center=new Windows.Foundation.Point(4+5*column,4+5*row),
                    RadiusX=1.5,RadiusY=1.5
                });
        grip.Children.Add(new PathIcon
        {
            Data=dots,Width=14,Height=18,
            VerticalAlignment=VerticalAlignment.Center,Opacity=0.7,IsHitTestVisible=false
        });
        Grid.SetColumn(title,1);
        grip.Children.Add(title);
        grip.PointerPressed+=(_,args)=>
        {
            if(groupDrag!=null || itemPressed || tracking || groupTargets.Count<=1) return;
            bool isMouse=args.Pointer.PointerDeviceType==PointerDeviceType.Mouse;
            if(isMouse && !args.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
            var source=groupTargets.Find(target=>target.Id==group.Id);
            var screen=GroupPointerPosition(args);
            if(source==null || !ScreenToClient(hwnd,ref screen) || !Root.CapturePointer(args.Pointer)) return;
            double grabY=screen.Y/Root.XamlRoot.RasterizationScale-GroupTop(source);
            screen=GroupPointerPosition(args);
            groupDrag=new GroupDragSession
            {
                Source=source,PointerId=args.Pointer.PointerId,Start=screen,Position=screen,
                GrabY=grabY,IsMouse=isMouse,PreviousStatus=Status.Text,Animate=systemUi.AnimationsEnabled
            };
            previewTimer.Start();
            args.Handled=true;
        };
        return grip;
    }

    private Point GroupPointerPosition(PointerRoutedEventArgs args)
    {
        var position=args.GetCurrentPoint(WindowSurface).Position;
        double scale=Root.XamlRoot.RasterizationScale;
        var screen=new Point((int)Math.Round(position.X*scale),(int)Math.Round(position.Y*scale));
        ClientToScreen(hwnd,ref screen);
        return screen;
    }

    private double GroupTop(GroupTarget target) =>
        target.Slot.TransformToVisual(WindowSurface).TransformPoint(new Windows.Foundation.Point()).Y;

    private int GroupDropDestination(GroupDragSession drag,Point screen)
    {
        if(!IsDropTarget(screen) || !ContainsPoint(ContentScroll,screen,out _) || !ScreenToClient(hwnd,ref screen)) return -1;
        double midpoint=screen.Y/Root.XamlRoot.RasterizationScale-drag.GrabY+drag.Height/2;
        int slot=1;
        foreach(var target in groupTargets)
        {
            if(target.Index==0 || target==drag.Source) continue;
            if(midpoint<GroupTop(target)+target.Slot.ActualHeight/2) return slot;
            slot++;
        }
        return slot;
    }

    private void LiftGroup(GroupDragSession drag)
    {
        var card=drag.Source.Element;
        var origin=GroupDragLayer.TransformToVisual(WindowSurface).TransformPoint(new Windows.Foundation.Point());
        var position=drag.Source.Slot.TransformToVisual(WindowSurface).TransformPoint(new Windows.Foundation.Point());
        double width=card.ActualWidth;
        drag.Height=card.ActualHeight;
        drag.Source.Slot.Height=drag.Height;
        drag.Source.Slot.Child=null;
        card.Width=width;
        card.Height=drag.Height;
        card.BorderBrush=GroupInsertionMarker.Background;
        Canvas.SetLeft(card,position.X-origin.X);
        Canvas.SetTop(card,position.Y-origin.Y);
        GroupDragLayer.Children.Add(card);
        drag.Lifted=true;
        drag.LastFrame=Environment.TickCount64;
    }

    private void DrawGroupDragFrame()
    {
        var drag=groupDrag;
        if(!showing || IsMinimized()) { CancelGroupDrag(); return; }
        var screen=drag.IsMouse ? System.Windows.Forms.Cursor.Position : drag.Position;
        if(!drag.Lifted)
        {
            if(Math.Abs(screen.Y-drag.Start.Y)<8*Root.XamlRoot.RasterizationScale) return;
            LiftGroup(drag);
        }

        if(!drag.Released && IsDropTarget(screen)) ScrollNearEdge(screen);
        int target=drag.Released ? drag.Destination : GroupDropDestination(drag,screen);
        int slot=target>0 ? target : drag.Source.Index;
        var origin=GroupDragLayer.TransformToVisual(WindowSurface).TransformPoint(new Windows.Foundation.Point());
        var viewport=ContentScroll.TransformToVisual(WindowSurface).TransformPoint(new Windows.Foundation.Point());
        groupDragClip.Rect=new Windows.Foundation.Rect(viewport.X-origin.X,viewport.Y-origin.Y,ContentScroll.ActualWidth,ContentScroll.ActualHeight);

        long now=Environment.TickCount64;
        double step=drag.Animate ? 1-Math.Exp(-(now-drag.LastFrame)/48.0) : 1;
        drag.LastFrame=now;
        MoveNeighbourCards(drag,slot,step);
        double gapTop=GroupGapTop(drag,slot);
        var progress=drag.Released ? Math.Min(1,(now-drag.ReleasedAt)/DropDuration) : 0;
        if(drag.Released)
        {
            double eased=1-Math.Pow(1-progress,3);
            Canvas.SetTop(drag.Source.Element,drag.SettleFrom+(gapTop-origin.Y-drag.SettleFrom)*eased);
        }
        else if(ScreenToClient(hwnd,ref screen))
        {
            Canvas.SetTop(drag.Source.Element,screen.Y/Root.XamlRoot.RasterizationScale-drag.GrabY-origin.Y);
        }
        ShowGroupDropLine(!drag.Released && target>0 && slot!=drag.Source.Index,viewport,gapTop);
        if(!drag.Released)
        {
            string status=target<=0 ? TextFor(TextKey.ReleaseToCancel) : target==drag.Source.Index ? TextFor(TextKey.ReleaseToKeep) : TextFor(TextKey.ReleaseToMoveGroup);
            if(Status.Text!=status) Status.Text=status;
        }
        if(progress>=1) FinishGroupDrag();
    }

    private double GroupGapTop(GroupDragSession drag,int slot)
    {
        var target=groupTargets.Find(group=>group.Index==slot) ?? drag.Source;
        return GroupTop(target)+(slot>drag.Source.Index ? target.Slot.ActualHeight-drag.Height : 0);
    }

    private void MoveNeighbourCards(GroupDragSession drag,int destination,double step)
    {
        double distance=drag.Height+GroupSpacing;
        foreach(var target in groupTargets)
        {
            int index=target.Index;
            double offset=index>=destination && index<drag.Source.Index ? distance :
                index>drag.Source.Index && index<=destination ? -distance : 0;
            double delta=offset-target.Offset.Y;
            if(delta!=0) target.Offset.Y=Math.Abs(delta)<0.5 ? offset : target.Offset.Y+delta*step;
        }
    }

    private void ShowGroupDropLine(bool show,Windows.Foundation.Point viewport,double gapTop)
    {
        GroupInsertionMarker.Visibility=show ? Visibility.Visible : Visibility.Collapsed;
        if(!show) return;
        var origin=DropMarkers.TransformToVisual(WindowSurface).TransformPoint(new Windows.Foundation.Point());
        Canvas.SetLeft(GroupInsertionMarker,viewport.X-origin.X+4);
        double y=Math.Clamp(gapTop-4,viewport.Y,viewport.Y+Math.Max(0,ContentScroll.ActualHeight-3));
        Canvas.SetTop(GroupInsertionMarker,y-origin.Y);
        GroupInsertionMarker.Width=Math.Max(0,ContentScroll.ActualWidth-8);
    }

    private void ReleaseGroupDrag(Point screen)
    {
        var drag=groupDrag;
        if(!drag.Lifted) { CancelGroupDrag(); return; }
        int destination=GroupDropDestination(drag,screen);
        if(destination>0 && destination!=drag.Source.Index)
        {
            if(!SaveGroupOrder(drag.Source.Id,drag.Source.Name,destination))
            {
                var error=Status.Text;
                CancelGroupDrag();
                Status.Text=error;
                return;
            }
            drag.Committed=true;
        }
        drag.Destination=drag.Committed ? destination : drag.Source.Index;
        drag.Released=true;
        Root.ReleasePointerCaptures();
        drag.SettleFrom=Canvas.GetTop(drag.Source.Element);
        drag.ReleasedAt=Environment.TickCount64;
        if(!drag.Animate) FinishGroupDrag();
    }

    private void FinishGroupDrag()
    {
        bool committed=groupDrag.Committed;
        CancelGroupDrag();
        if(committed || BoxPresentationChanged()) Render();
    }

    private bool CancelGroupDrag()
    {
        var drag=groupDrag;
        if(drag==null) return false;
        groupDrag=null;
        previewTimer.Stop();
        Root.ReleasePointerCaptures();
        GroupInsertionMarker.Visibility=Visibility.Collapsed;
        if(drag.Lifted)
        {
            var card=drag.Source.Element;
            GroupDragLayer.Children.Remove(card);
            card.ClearValue(FrameworkElement.WidthProperty);
            card.ClearValue(FrameworkElement.HeightProperty);
            card.ClearValue(Border.BorderBrushProperty);
            drag.Source.Slot.ClearValue(FrameworkElement.HeightProperty);
            drag.Source.Slot.Child=card;
            foreach(var target in groupTargets) target.Offset.Y=0;
            Status.Text=drag.PreviousStatus;
        }
        return true;
    }
}
