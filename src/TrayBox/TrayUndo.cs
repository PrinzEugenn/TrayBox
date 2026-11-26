using System;
using static TrayText;
using System.IO;
using Microsoft.UI.Xaml;

namespace TrayBoxFluent;

public sealed partial class MainWindow
{
    private sealed class UndoEntry
    {
        internal TrayItem Item;
        internal string Key;
        internal TextKey Message;
        internal BoxPosition Position;
        internal bool WasBoxed, ChangedVisibility;
        internal string GroupId, GroupLabel;
        internal int OriginalGroupIndex;
    }
    private UndoEntry lastChange;
    private bool undoCompleted;

    private void ChangeGroupOrder(string id,string name,int destination)
    {
        if(!SaveGroupOrder(id,name,destination)) return;
        Render();
        AnimatePage(false);
    }

    private bool SaveGroupOrder(string id,string name,int destination)
    {
        try
        {
            int original=layout.GroupIndex(id);
            if(!layout.MoveGroupTo(id,destination)) return false;
            lastChange=new UndoEntry
            {
                GroupId=id,GroupLabel=name,OriginalGroupIndex=original,Message=TextKey.GroupMoved
            };
            undoCompleted=false;
            return true;
        }
        catch(Exception error) when(error is IOException || error is UnauthorizedAccessException)
        {
            Status.Text=TextFor(TextKey.PersistGroupOrderFailed);
            return false;
        }
    }

    private bool ChangeIcon(TrayItem item, bool boxed, string group=null, string beforeKey=null)
    {
        var key=TrayEngine.SelectionKey(item) ?? item.Key;
        var original=layout.PositionOf(key);
        bool wasBoxed=item.Boxed;
        bool visibilityChanged=wasBoxed!=boxed;
        if(visibilityChanged && !engine.SetBoxed(item,boxed))
        {
            Status.Text=engine.LastOperationError;
            return false;
        }

        string layoutError=null;
        if(group!=null && (group!=original.Group || beforeKey!=original.BeforeKey))
        {
            try { layout.Move(key,group,beforeKey); }
            catch(Exception error) when(error is IOException || error is UnauthorizedAccessException)
            {
                layoutError=TextFor(TextKey.PersistPositionFailed);
            }
        }
        var current=layout.PositionOf(key);
        if(visibilityChanged || original.Group!=current.Group || original.BeforeKey!=current.BeforeKey)
        {
            undoCompleted=false;
            lastChange=new UndoEntry
            {
                Item=item, Key=key, Position=original, WasBoxed=wasBoxed,
                ChangedVisibility=visibilityChanged,
                Message=visibilityChanged ? (boxed ? TextKey.Collected : TextKey.Restored) : TextKey.Moved
            };
        }
        RenderAfterIconChange();
        if(layoutError!=null) Status.Text=layoutError;
        return true;
    }

    private void RenderAfterIconChange()
    {
        if(managing) RenderIconSettings();
        else Render();
    }

    private void UpdateUndo(string status)
    {
        UndoButton.Visibility=lastChange==null ? Visibility.Collapsed : Visibility.Visible;
        Status.Text=undoCompleted && !engine.SelectionPersistencePending ? TextFor(TextKey.Undone) : status;
        if(lastChange!=null && !engine.SelectionPersistencePending)
        {
            var name=lastChange.GroupLabel ?? lastChange.Item?.Tip;
            var label=String.IsNullOrWhiteSpace(name) ? TextFor(TextKey.Icon) : name.Replace('\n',' ').Replace('\r',' ');
            Status.Text=TextFor(lastChange.Message)+" · "+label;
        }
    }

    private void UndoClicked(object sender,RoutedEventArgs args)
    {
        var change=lastChange;
        if(change==null || tracking || itemPressed || groupDrag!=null) return;
        if(change.ChangedVisibility)
        {
            if(!engine.Snapshot().Contains(change.Item))
            {
                lastChange=null;
                RenderAfterIconChange();
                Status.Text=TextFor(TextKey.UndoIconChanged);
                return;
            }
            if(change.Item.Boxed!=change.WasBoxed && !engine.SetBoxed(change.Item,change.WasBoxed))
            {
                Status.Text=engine.LastOperationError;
                return;
            }
        }
        try
        {
            if(change.GroupId==null) layout.RestorePosition(change.Key,change.Position);
            else if(!layout.MoveGroupTo(change.GroupId,change.OriginalGroupIndex))
            {
                lastChange=null;
                RenderAfterIconChange();
                Status.Text=TextFor(TextKey.UndoGroupChanged);
                return;
            }
        }
        catch(Exception error) when(error is IOException || error is UnauthorizedAccessException)
        {
            Status.Text=TextFor(TextKey.UndoPositionFailed);
            return;
        }
        lastChange=null;
        undoCompleted=true;
        RenderAfterIconChange();
        Status.Text=engine.SelectionPersistencePending ? TextFor(TextKey.UndoSavePending) : TextFor(TextKey.Undone);
    }
}
