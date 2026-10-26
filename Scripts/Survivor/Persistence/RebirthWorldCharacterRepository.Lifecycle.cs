using System;
using System.IO;
using System.Xml.Linq;

public static partial class RebirthWorldCharacterRepository
{
    private static World scopeWorld;
    private static string scopePath;
    private static string scopeSaveDirectory;
    private static bool ScopeCurrentLocked(string current=null)
    {
        try
        {
            if(scopeWorld==null||!ReferenceEquals(scopeWorld,GameManager.Instance?.World))return false;
            if(current==null)current=GameIO.GetSaveGameDir();
            if(string.Equals(current,scopeSaveDirectory,StringComparison.Ordinal))return true;
            if(string.IsNullOrEmpty(current))return false;
            return string.Equals(scopePath,Path.GetFullPath(Path.Combine(current,"RebirthData","Survivor","Players")),RepositoryPathComparison);
        }
        catch{return false;}
    }
    private static bool BindScopeLocked()
    {
        if(!serverAuthority)return false;
        var world=GameManager.Instance?.World;
        string saveDirectory=GameIO.GetSaveGameDir();
        if(world==null||string.IsNullOrEmpty(saveDirectory))return false;
        if(scopeWorld!=null)
        {
            if(ScopeCurrentLocked(saveDirectory))return true;
            // The native World may be replaced while loading the same save. Retire the
            // old binding only when its cached records are clean and still match disk.
            // Otherwise keep refusing access; never project a stale cache into a new World.
            if(!CanRetireLocked())return false;
            Reset(true);
            if(scopeWorld!=null)return false;
        }
        scopeWorld=world;scopeSaveDirectory=saveDirectory;
        scopePath=Path.GetFullPath(Path.Combine(saveDirectory,"RebirthData","Survivor","Players"));return true;
    }
    private static bool CanRetireLocked()
    {
        // A replacement native World can arrive before the old shutdown event
        // finishes. Retire that old scope only when every cached record still
        // matches its exact original final file; never carry its cache into the
        // new save or discard an unsaved change.
        if(scopeWorld!=null&&!ScopeCurrentLocked())
        {
            if(string.IsNullOrEmpty(scopePath))return false;
            try
            {
                foreach(var pair in Cache)
                {
                    if(pair.Value==null||pair.Value.Dirty||
                        !XNode.DeepEquals(Serialize(pair.Value).Root,
                            XDocument.Load(Path.Combine(scopePath,pair.Key+".xml")).Root))return false;
                }
            }
            catch{return false;}
        }
        if(RetryAfterUtcTicks.Count!=0)return false;
        foreach(var entry in KeyWriteLocks.Values)
            if(entry is RepositoryWriteGate gate&&(gate.Active||gate.PendingJournal!=null))return false;
        foreach(var record in Cache.Values)if(record==null||record.Dirty)return false;
        return true;
    }
    public static bool CanResetAfterFlush(){lock(Sync)return CanRetireLocked();}
    internal static string ResetDiagnostic()
    {
        lock(Sync)
        {
            int dirty=0,active=0,pending=0;
            foreach(var record in Cache.Values)if(record==null||record.Dirty)dirty++;
            foreach(var entry in KeyWriteLocks.Values)
                if(entry is RepositoryWriteGate gate)
                {if(gate.Active)active++;if(gate.PendingJournal!=null)pending++;}
            return "authority="+serverAuthority+" scoped="+(scopeWorld!=null)+
                " current="+ScopeCurrentLocked()+" cache="+Cache.Count+" dirty="+dirty+
                " retry="+RetryAfterUtcTicks.Count+" active="+active+" pending="+pending;
        }
    }
    public static bool TryResetAfterFlush(bool authority)
    {lock(Sync){if(!CanRetireLocked())return false;Reset(authority);return scopeWorld==null;}}
    public static bool TryFlush(string reason)
    {
        lock(Sync){if(!serverAuthority)return scopeWorld==null&&Cache.Count==0;if(!BindScopeLocked())return false;}
        SaveAllDirty(reason);
        lock(Sync)
        {
            if(!CanRetireLocked())return false;
            try
            {
                foreach(var pair in Cache)
                    if(!XNode.DeepEquals(Serialize(pair.Value).Root,XDocument.Load(GetPath(pair.Key)).Root))return false;
                return ScopeCurrentLocked();
            }
            catch{return false;}
        }
    }
}
