using System;
using System.IO;
using System.Xml.Linq;

public static partial class RebirthMetabolismStateRepository
{
    private static World scopeWorld;
    private static string scopePath;
    private static bool ScopeCurrentLocked()
    {
        try{return ReferenceEquals(scopeWorld,GameManager.Instance?.World)&&scopeWorld!=null&&
            string.Equals(scopePath,Path.GetFullPath(PathName),Environment.OSVersion.Platform==PlatformID.Win32NT?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);}
        catch{return false;}
    }
    private static bool BindScopeLocked()
    {
        if(!serverAuthority)return false;
        if(scopeWorld!=null)return ScopeCurrentLocked();
        var world=GameManager.Instance?.World;
        if(world==null||string.IsNullOrEmpty(PathName))return false;
        scopeWorld=world;scopePath=Path.GetFullPath(PathName);return true;
    }
    private static bool CanRetireLocked()
    {
        if(scopeWorld!=null&&!ScopeCurrentLocked())return false;
        if(!string.IsNullOrEmpty(failedLoadPath))return false;
        foreach(var state in States.Values)if(state==null||state.Dirty)return false;
        return true;
    }
    public static bool CanResetAfterFlush(){lock(Sync)return !RebirthConsumptionDurableDelivery.HoldsInventoryWorld(scopeWorld)&&!RebirthNpcMetabolismCustody.HoldsRepositoryScope(scopeWorld)&&!(RebirthHydrationOwnerHold.HoldsWorld(scopeWorld)||RebirthHydrationServerSourceHold.HoldsWorld(scopeWorld))&&CanRetireLocked();}
    public static bool TryResetAfterFlush(bool authority)
    {lock(Sync){if(RebirthConsumptionDurableDelivery.HoldsInventoryWorld(scopeWorld)||RebirthNpcMetabolismCustody.HoldsRepositoryScope(scopeWorld)||(RebirthHydrationOwnerHold.HoldsWorld(scopeWorld)||RebirthHydrationServerSourceHold.HoldsWorld(scopeWorld))||!CanRetireLocked())return false;Reset(authority);return scopeWorld==null;}}
    public static bool TryFlush(string reason)
    {
        lock(Sync){if(!serverAuthority)return scopeWorld==null&&States.Count==0; if(!BindScopeLocked())return false;}
        if(!TrySave(reason,out var error))return false;
        lock(Sync)
        {
            if(!CanRetireLocked())return false;
            try
            {
                var expected=new XElement("rebirthMetabolismPlayers",new XAttribute("version",RebirthMetabolismState.CurrentVersion));
                foreach(var pair in States){ValidateStateForPersistence(pair.Value);expected.Add(SerializePlayer(pair.Key,pair.Value));}
                if(!loaded)return States.Count==0;
                return XNode.DeepEquals(expected,XDocument.Load(scopePath).Root)&&ScopeCurrentLocked();
            }
            catch{return false;}
        }
    }
}

