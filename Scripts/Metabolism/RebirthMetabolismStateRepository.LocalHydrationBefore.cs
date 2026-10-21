using System;
using System.IO;

public static partial class RebirthMetabolismStateRepository
{
    internal static bool HasCurrentLocalHydrationBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try{return LocalHydrationBeforeCurrent(player,original,out _);}
        catch{return false;}
    }
    internal static bool TrySaveLocalHydrationBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try
        {
            if(HasSavedLocalHydrationBefore(player,original))return true;
            if(!LocalHydrationBeforeCurrent(player,original,out _)||!TrySave("hydration-local-untouched-source",out _))return false;
            return HasSavedLocalHydrationBefore(player,original);
        }
        catch{return false;}
    }
    internal static bool HasSavedLocalHydrationBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try
        {
            if(!LocalHydrationBeforeCurrent(player,original,out var state))return false;
            lock(Sync)
            {
                if(!ScopeCurrentLocked()||!ReferenceEquals(scopeWorld,player.world))return false;
                string id=GetStablePlayerId(player),path=scopePath;int generation=loadGeneration;
                if(!States.TryGetValue(id,out var retained)||!ReferenceEquals(retained,state))return false;
                var info=new FileInfo(path);
                if(!info.Exists||info.Length<1||info.Length>MaximumStoreBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                var saved=ReadCandidate(path);
                return saved.TryGetValue(id,out var row)&&LocalHydrationBeforeRow(original,row)&&
                    generation==loadGeneration&&path==scopePath&&ScopeCurrentLocked()&&
                    LocalHydrationBeforeCurrent(player,original,out var current)&&ReferenceEquals(state,current);
            }
        }
        catch{return false;}
    }
    private static bool LocalHydrationBeforeCurrent(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out RebirthMetabolismState state)
    {
        state=null;
        return ThreadManager.IsMainThread()&&serverAuthority&&player?.world?.IsRemote()==false&&
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true&&original!=null&&
            !RebirthHydrationTransferGrant.HasIssued(original.Operation)&&RebirthHydrationOwnerHold.Matches(player,original)&&
            TryGet(player,out state)&&state.Revision==original.Revision&&LocalHydrationBeforeRow(original,state);
    }
    private static bool LocalHydrationBeforeRow(RebirthHydrationTransferOriginal original,RebirthMetabolismState state)
    {
        // Revision is a live repository witness, not a persisted row field.
        if(state==null||!RebirthHydrationTransferScope.MatchesState(original.Creation,state)||
            state.HydrationTransferReceipt?.Operation==original.Operation||!original.TryGetPlan(out var plan))return false;
        var item=state.HydrationSlotItem;int count=item==null||item.IsEmpty()?0:item.count;
        return count==plan.GearBefore.Count&&(count==0?string.Empty:RebirthNativeItemCodec.Encode(item.itemValue))==plan.GearBefore.ItemData;
    }
}
