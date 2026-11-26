using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Stable application/icon identities, independent of HWND, tooltip and process lifetime.
internal sealed class BoxGroup
{
    internal string Id, Name;
}
internal struct BoxPosition
{
    internal string Group, BeforeKey;
}
internal sealed class BoxLayout
{
    internal const string DefaultGroup = "default";
    private sealed class Entry { internal string Key, Group; }
    private readonly string path;
    private readonly List<BoxGroup> groups = new List<BoxGroup>();
    private readonly List<Entry> entries = new List<Entry>();
    internal IEnumerable<BoxGroup> Groups { get { return groups.AsReadOnly(); } }
    private BoxLayout(string file) { path=file; groups.Add(new BoxGroup { Id=DefaultGroup,Name="未分组" }); }
    internal static BoxLayout Load(string file)
    {
        var result=new BoxLayout(file);
        if(!File.Exists(file)) return result;
        try {
            string[] lines=File.ReadAllLines(file,Encoding.UTF8);
            if(lines.Length==0 || lines[0]!="TrayBoxLayout1") return result;
            foreach(string line in lines) {
                var cells=line.Split('\t');
                if(cells.Length!=3 || cells[0]!="G" || cells[1]==DefaultGroup || result.HasGroup(cells[1])) continue;
                Guid id; if(!Guid.TryParseExact(cells[1],"N",out id)) continue;
                try { string name=Decode(cells[2]); if(ValidName(name)) result.groups.Add(new BoxGroup { Id=cells[1],Name=name }); } catch(FormatException) { }
            }
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(string line in lines) {
                var cells=line.Split('\t'); if(cells.Length!=3 || cells[0]!="I") continue;
                try { string key=Decode(cells[1]); if(!String.IsNullOrWhiteSpace(key) && seen.Add(key)) result.entries.Add(new Entry { Key=key,Group=result.HasGroup(cells[2]) ? cells[2] : DefaultGroup }); } catch(FormatException) { }
            }
        } catch(IOException) { } catch(UnauthorizedAccessException) { }
        return result;
    }
    private static string Encode(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s)); }
    private static string Decode(string s) { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
    private static bool ValidName(string s) { return !String.IsNullOrWhiteSpace(s) && s.Length<=32 && s.IndexOfAny(new[]{'\r','\n','\t'})<0; }
    private bool HasGroup(string id) { return groups.Exists(g=>g.Id==id); }
    internal string GroupFor(string key) { var entry=entries.Find(e=>e.Key==key); return entry==null ? DefaultGroup : entry.Group; }
    internal BoxPosition PositionOf(string key)
    {
        int index=entries.FindIndex(entry=>entry.Key==key);
        if(index<0) return new BoxPosition();
        string group=entries[index].Group;
        int next=entries.FindIndex(index+1,entry=>entry.Group==group);
        return new BoxPosition { Group=group,BeforeKey=next<0 ? null : entries[next].Key };
    }
    internal void RestorePosition(string key,BoxPosition position)
    {
        if(position.Group!=null) Move(key,position.Group,position.BeforeKey);
        else if(entries.Exists(entry=>entry.Key==key)) Commit(copy=>copy.entries.RemoveAll(entry=>entry.Key==key));
    }
    internal void Remember(IEnumerable<string> keys)
    {
        var missing=new List<string>(); var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var key in keys) if(!String.IsNullOrWhiteSpace(key) && seen.Add(key) && !entries.Exists(e=>e.Key==key)) missing.Add(key);
        if(missing.Count>0) Commit(copy=> { foreach(var key in missing) copy.entries.Add(new Entry {Key=key,Group=DefaultGroup}); });
    }
    internal List<string> Order(IEnumerable<string> keys,string group)
    {
        var available=new List<string>(); var set=new HashSet<string>(StringComparer.Ordinal);
        foreach(var key in keys) if(!String.IsNullOrEmpty(key) && set.Add(key)) available.Add(key);
        var result=new List<string>();
        foreach(var entry in entries) if(entry.Group==group && set.Remove(entry.Key)) result.Add(entry.Key);
        foreach(var key in available) if(set.Contains(key) && GroupFor(key)==group) result.Add(key);
        return result;
    }
    internal string AddGroup(string name)
    {
        name=(name??"").Trim(); CheckName(name,null);
        string id=Guid.NewGuid().ToString("N");
        Commit(copy=>copy.groups.Add(new BoxGroup { Id=id,Name=name })); return id;
    }
    internal bool CanMoveGroup(string id,int direction)
    {
        if(direction!=-1 && direction!=1) return false;
        int index=GroupIndex(id);
        int destination=index+direction;
        return index>0 && destination>0 && destination<groups.Count;
    }
    internal int GroupIndex(string id) { return groups.FindIndex(group=>group.Id==id); }
    internal bool MoveGroupTo(string id,int destination)
    {
        int index=GroupIndex(id);
        if(index<=0 || destination<=0 || destination>=groups.Count || destination==index) return false;
        Commit(copy=>
        {
            var group=copy.groups[index];
            copy.groups.RemoveAt(index);
            copy.groups.Insert(destination,group);
        });
        return true;
    }
    internal void RenameGroup(string id,string name)
    {
        if(id==DefaultGroup || !HasGroup(id)) throw new ArgumentException("请选择自建分组");
        name=(name??"").Trim(); CheckName(name,id);
        Commit(copy=>copy.groups.Find(g=>g.Id==id).Name=name);
    }
    private void CheckName(string name,string current)
    {
        if(!ValidName(name)) throw new ArgumentException("分组名称需为 1–32 个字符");
        if(groups.Exists(g=>g.Id!=current && String.Equals(g.Name,name,StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("已有同名分组");
    }
    internal void RemoveGroup(string id)
    {
        if(id==DefaultGroup || !HasGroup(id)) return;
        Commit(copy=> { var moved=copy.entries.FindAll(e=>e.Group==id); copy.entries.RemoveAll(e=>e.Group==id); foreach(var e in moved) { e.Group=DefaultGroup; copy.entries.Add(e); } copy.groups.RemoveAll(g=>g.Id==id); });
    }
    internal void Move(string key,string group,string beforeKey=null)
    {
        if(String.IsNullOrEmpty(key) || !HasGroup(group)) throw new ArgumentException("图标或分组不可用");
        if(key==beforeKey) return;
        Commit(copy=> {
            copy.entries.RemoveAll(e=>e.Key==key);
            int index=copy.entries.FindIndex(e=>e.Key==beforeKey && e.Group==group);
            // A freshly boxed target may not have a stored position yet.
            if(index<0 && !String.IsNullOrEmpty(beforeKey) && copy.GroupFor(beforeKey)==group) { copy.entries.Add(new Entry {Key=beforeKey,Group=group}); index=copy.entries.Count-1; }
            var entry=new Entry {Key=key,Group=group};
            if(index<0) copy.entries.Add(entry); else copy.entries.Insert(index,entry);
        });
    }
    // Save before replacing the live state; a failed write must not claim success.
    private void Commit(Action<BoxLayout> change)
    {
        var copy=new BoxLayout(path); copy.groups.Clear();
        foreach(var group in groups) copy.groups.Add(new BoxGroup {Id=group.Id,Name=group.Name});
        foreach(var entry in entries) copy.entries.Add(new Entry {Key=entry.Key,Group=entry.Group});
        change(copy); copy.Save();
        groups.Clear(); groups.AddRange(copy.groups); entries.Clear(); entries.AddRange(copy.entries);
    }
    private void Save()
    {
        var lines=new List<string> {"TrayBoxLayout1"};
        foreach(var group in groups) if(group.Id!=DefaultGroup) lines.Add("G\t"+group.Id+"\t"+Encode(group.Name));
        foreach(var entry in entries) lines.Add("I\t"+Encode(entry.Key)+"\t"+entry.Group);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string temp=path+".tmp-"+Guid.NewGuid().ToString("N");
        try { File.WriteAllLines(temp,lines,Encoding.UTF8); if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path); }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
}
