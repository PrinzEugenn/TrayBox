using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
internal sealed class TargetedTrayProcess
{
    internal int Id;
    internal string Path;
    internal long StartTicks;
    internal readonly List<string> Names = new List<string>();
    internal string Key { get { return Id + "|" + StartTicks; } }
}
internal static class TargetedTrayDiscovery
{
    [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(ref Guid id,uint flags,IntPtr token,out IntPtr path);
    internal static string ExpandPath(string value)
    {
        if(String.IsNullOrWhiteSpace(value)) return null;
        string path=Environment.ExpandEnvironmentVariables(value.Trim());
        if(path.StartsWith("{",StringComparison.Ordinal)) {
            int end=path.IndexOf('}'); Guid id;
            if(end<0 || !Guid.TryParse(path.Substring(0,end+1),out id)) return null;
            IntPtr folder=IntPtr.Zero;
            try { if(SHGetKnownFolderPath(ref id,0,IntPtr.Zero,out folder)!=0) return null; path=System.IO.Path.Combine(Marshal.PtrToStringUni(folder),path.Substring(end+1).TrimStart('\\','/')); }
            finally { if(folder!=IntPtr.Zero) Marshal.FreeCoTaskMem(folder); }
        }
        try { return System.IO.Path.IsPathRooted(path) ? System.IO.Path.GetFullPath(path) : null; } catch { return null; }
    }
    private const string SettingsPath = @"Control Panel\NotifyIconSettings";
    internal static List<TargetedTrayProcess> Resolve(IEnumerable<string> names, IDictionary<string,string> notes=null)
    {
        var targets=new List<TargetedTrayProcess>();
        var requestedNames=new List<string>(names);
        try
        {
            using(var root=Registry.CurrentUser.OpenSubKey(SettingsPath))
            {
                if(root==null)
                {
                    if(notes!=null) foreach(string name in requestedNames) notes[name]=TrayText.TextFor(TextKey.NoOwnerRecords);
                    return targets;
                }
                var entries=new List<KeyValuePair<string,string>>();
                foreach(string keyName in root.GetSubKeyNames()) using(var key=root.OpenSubKey(keyName))
                {
                    if(key==null) continue;
                    string tip=key.GetValue("InitialTooltip") as string, path=key.GetValue("ExecutablePath") as string;
                    if(String.IsNullOrWhiteSpace(tip) || String.IsNullOrWhiteSpace(path)) continue;
                    string expanded=ExpandPath(path);
                    if(expanded!=null) entries.Add(new KeyValuePair<string,string>(Normalize(tip),expanded));
                }
                entries.RemoveAll(entry=>!requestedNames.Exists(name=>NamesMatch(name,entry.Key)));
                // Discard historical driver paths before checking name ambiguity.
                var live=new Dictionary<string,List<TargetedTrayProcess>>(StringComparer.OrdinalIgnoreCase);
                var denied=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int ownSession; using(var own=Process.GetCurrentProcess()) ownSession=own.SessionId;
                foreach(var entry in entries) {
                    if(live.ContainsKey(entry.Value)) continue;
                    var matches=new List<TargetedTrayProcess>(); live[entry.Value]=matches;
                    string processName=System.IO.Path.GetFileNameWithoutExtension(entry.Value);
                    if(String.IsNullOrWhiteSpace(processName) || processName.Equals("explorer",StringComparison.OrdinalIgnoreCase)) continue;
                    foreach(var process in Process.GetProcessesByName(processName)) using(process) try {
                        string actual; long created; int level,error;
                        if(process.SessionId!=ownSession) continue;
                        if(ProcessAccess.TryRead((uint)process.Id,out actual,out created,out level,out error)) {
                            if(String.Equals(actual,entry.Value,StringComparison.OrdinalIgnoreCase)) matches.Add(new TargetedTrayProcess { Id=process.Id,Path=actual,StartTicks=created });
                        } else if(error==5) denied.Add(entry.Value);
                    } catch { }
                }
                if(notes!=null) foreach(string name in requestedNames) {
                    bool restricted=entries.Exists(entry=>NamesMatch(name,entry.Key) && denied.Contains(entry.Value));
                    if(restricted) notes[name]=TrayText.TextFor(TextKey.OwnerQueryRestricted);
                }
                entries.RemoveAll(entry=>live[entry.Value].Count==0);
                var uniquePaths=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach(string raw in requestedNames)
                {
                    string path=MatchUniquePath(raw,entries);
                    if(path==null) {if(notes!=null && !notes.ContainsKey(raw)) notes[raw]=entries.Exists(e=>NamesMatch(raw,e.Key)) ? TrayText.TextFor(TextKey.AmbiguousIconOwner) : TrayText.TextFor(TextKey.NoMatchingApp);continue;}
                    List<string> requested;
                    if(!uniquePaths.TryGetValue(path,out requested)) { requested=new List<string>(); uniquePaths[path]=requested; }
                    requested.Add(raw);
                }
                var ids=new HashSet<int>(); int ownId; using(var own=Process.GetCurrentProcess()) ownId=own.Id;
                foreach(var pair in uniquePaths)
                {
                    foreach(var target in live[pair.Key])
                    {
                        if(target.Id==ownId || !ids.Add(target.Id)) continue;
                        target.Names.AddRange(pair.Value); targets.Add(target);
                    }
                }
            }
        }
        catch (UnauthorizedAccessException) { SetNotes(requestedNames,notes,TrayText.TextFor(TextKey.OwnerRecordsDenied)); }
        catch (IOException) { SetNotes(requestedNames,notes,TrayText.TextFor(TextKey.OwnerRecordsFailed)); }
        catch (System.Security.SecurityException) { SetNotes(requestedNames,notes,TrayText.TextFor(TextKey.OwnerRecordsDenied)); }
        return targets;
    }
    private static void SetNotes(IEnumerable<string> names,IDictionary<string,string> notes,string reason)
    {
        if(notes!=null) foreach(string name in names) notes[name]=reason;
    }
    private static bool NamesMatch(string first,string second)
    {
        string a=Normalize(first),b=Normalize(second);
        return a.Length>0 && (a==b || (a.Length>=3 && b.Length>=3 && (a.StartsWith(b+" ",StringComparison.Ordinal) || b.StartsWith(a+" ",StringComparison.Ordinal))));
    }
    internal static string MatchUniquePath(string value, IEnumerable<KeyValuePair<string,string>> entries)
    {
        string name=Normalize(value); if(name.Length==0) return null;
        var exact=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var partial=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in entries)
        {
            string tip=Normalize(entry.Key); if(tip.Length==0 || String.IsNullOrWhiteSpace(entry.Value)) continue;
            if(name==tip) exact.Add(entry.Value);
            else if(name.Length>=3 && tip.Length>=3 && (name.StartsWith(tip+" ",StringComparison.Ordinal) || tip.StartsWith(name+" ",StringComparison.Ordinal))) partial.Add(entry.Value);
        }
        var choices=exact.Count>0 ? exact : partial;
        if(choices.Count!=1) return null;
        foreach(var path in choices) return path;
        return null;
    }
    internal static bool StillMatches(TargetedTrayProcess target)
    {
        try { string path; long created; int level,error; return ProcessAccess.TryRead((uint)target.Id,out path,out created,out level,out error) && created==target.StartTicks && String.Equals(path,target.Path,StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
    private static string Normalize(string value) { return String.Join(" ",(value??"").Split((char[])null,StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant(); }
}
