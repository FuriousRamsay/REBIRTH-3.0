using System;
using System.IO;

public static partial class RebirthMetabolismStateRepository
{
    internal static bool TrySaveRemoteHydrationBefore(RebirthHydrationServerSourceHold.Lease lease)
    {
        try
        {
            if(lease==null||!lease.Current()||!serverAuthority)return false;
            if(HasSavedRemoteHydrationBefore(lease))return true;
            if(!TrySave("hydration-remote-source-before",out _))return false;
            return HasSavedRemoteHydrationBefore(lease);
        }
        catch{return false;}
    }
    // Saved source observation only; no client checkpoint/grant, mutation or
    // terminal release can be inferred from this preimage by itself.
    internal static bool HasSavedRemoteHydrationBefore(RebirthHydrationServerSourceHold.Lease lease)
    {
        try
        {
            if(lease==null||!lease.Current()||!serverAuthority||!lease.Original.TryGetPlan(out var plan))return false;
            lock(Sync)
            {
                if(!ScopeCurrentLocked()||!ReferenceEquals(scopeWorld,lease.HeldWorld)||!lease.Current())return false;
                string id=GetStablePlayerId(lease.Player),path=scopePath;int generation=loadGeneration;
                if(!States.TryGetValue(id,out var state)||!ReferenceEquals(state,lease.State))return false;
                var info=new FileInfo(path);
                if(!info.Exists||info.Length<1||info.Length>MaximumStoreBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                // Existing strict decoder reads only this final primary path;
                // ReadStoreCandidates/backup recovery is deliberately not used.
                var saved=ReadCandidate(path);
                if(!saved.TryGetValue(id,out var row)||!RebirthHydrationTransferScope.MatchesState(lease.Original.Creation,row))return false;
                var item=row.HydrationSlotItem;int count=item==null||item.IsEmpty()?0:item.count;
                string data=count==0?string.Empty:RebirthNativeItemCodec.Encode(item.itemValue);
                return count==plan.GearBefore.Count&&data==plan.GearBefore.ItemData&&
                    generation==loadGeneration&&path==scopePath&&ScopeCurrentLocked()&&lease.Current()&&
                    States.TryGetValue(id,out var current)&&ReferenceEquals(current,state);
            }
        }
        catch{return false;}
    }
}
