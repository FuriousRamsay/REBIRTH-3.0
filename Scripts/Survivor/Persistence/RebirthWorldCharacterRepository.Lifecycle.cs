using System;
using System.IO;
using System.Xml.Linq;

public static partial class RebirthWorldCharacterRepository
{
    private static World scopeWorld;
    private static string scopePath;
    private static bool ScopeCurrentLocked()
    {
        try{return scopeWorld!=null&&ReferenceEquals(scopeWorld,GameManager.Instance?.World)&&
            string.Equals(scopePath,Path.GetFullPath(RootDirectory),RepositoryPathComparison);}
        catch{return false;}
    }
    private static bool BindScopeLocked()
    {
        if(!serverAuthority)return false;
        if(scopeWorld!=null)return ScopeCurrentLocked();
        var world=GameManager.Instance?.World;
        if(world==null||string.IsNullOrEmpty(RootDirectory))return false;
        scopeWorld=world;scopePath=Path.GetFullPath(RootDirectory);return true;
    }
    private static bool CanRetireLocked()
    {
        if(scopeWorld!=null&&!ScopeCurrentLocked())return false;
        if(RetryAfterUtcTicks.Count!=0)return false;
        foreach(var entry in KeyWriteLocks.Values)
            if(entry is RepositoryWriteGate gate&&(gate.Active||gate.PendingJournal!=null))return false;
        foreach(var record in Cache.Values)if(record==null||record.Dirty)return false;
        return true;
    }
    public static bool CanResetAfterFlush(){lock(Sync)return CanRetireLocked();}
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
