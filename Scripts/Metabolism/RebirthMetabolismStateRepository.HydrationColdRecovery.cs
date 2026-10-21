using System;
using System.IO;
using System.Collections.Generic;

public static partial class RebirthMetabolismStateRepository
{
    private static readonly HashSet<RebirthHydrationColdRecoveryLease> ColdHydrationSettling=new HashSet<RebirthHydrationColdRecoveryLease>();
    internal static bool TrySettleColdLocalHydration(RebirthHydrationColdRecoveryLease lease)
    {
        if(!ThreadManager.IsMainThread()||lease==null||!ColdHydrationSettling.Add(lease))return false;
        try
        {
            if(!serverAuthority||!lease.Current()||!lease.Original.TryGetPlan(out var plan))return false;
            ItemStack after;
            if(plan.GearAfter.Count==0)after=ItemStack.Empty.Clone();
            else
            {
                if(plan.GearAfter.Count!=1||!RebirthNativeItemCodec.TryDecode(plan.GearAfter.ItemData,out var value)||
                    value==null||value.IsEmpty()||value.ItemClass==null)return false;
                after=new ItemStack(value,1);
            }
            lock(Sync)
            {
                if(!ColdRecoveryRepositoryCurrent(lease,out var state))return false;
                if(state.HydrationTransferReceipt?.Digest!=lease.Original.Digest)
                {
                    // Never manufacture a missing saved preimage. The native
                    // leg is already Applied; only this source leg may change.
                    if(!ObserveColdRecoveryRow(lease,false)||!ColdRecoveryRepositoryCurrent(lease,out var current)||
                        !ReferenceEquals(state,current))return false;
                    state.HydrationSlotItem=after;state.HydrationTransferReceipt=lease.Original;state.Touch();
                }
                if(!lease.Current())return false;
            }
            if(!HasSavedColdRecoveryHydrationAfter(lease)&&!TrySave("hydration-cold-source-leg",out _))return false;
            return HasSavedColdRecoveryHydrationAfter(lease);
        }
        catch{return false;}
        finally{ColdHydrationSettling.Remove(lease);}
    }
    internal static bool HasSavedColdRecoveryHydrationAfter(RebirthHydrationColdRecoveryLease lease)
        =>ObserveColdRecoveryRow(lease,true);
    private static bool ColdRecoveryRepositoryCurrent(RebirthHydrationColdRecoveryLease lease,out RebirthMetabolismState state)
    {
        state=null;
        return serverAuthority&&lease!=null&&lease.Current()&&ScopeCurrentLocked()&&ReferenceEquals(scopeWorld,lease.Player.world)&&
            States.TryGetValue(GetStablePlayerId(lease.Player),out state)&&ReferenceEquals(state,lease.State);
    }
    private static bool ObserveColdRecoveryRow(RebirthHydrationColdRecoveryLease lease,bool after)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||lease==null||!lease.Current())return false;
            lock(Sync)
            {
                if(!ColdRecoveryRepositoryCurrent(lease,out var state))return false;
                string id=GetStablePlayerId(lease.Player),path=scopePath;int generation=loadGeneration;
                if(after&&!ColdHydrationAfterRow(state,lease.Original)||!after&&!ColdRecoveryBeforeRow(state,lease.Original))return false;
                var info=new FileInfo(path);
                if(!info.Exists||info.Length<1||info.Length>MaximumStoreBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                var saved=ReadCandidate(path);
                if(!saved.TryGetValue(id,out var row)||(after?!ColdHydrationAfterRow(row,lease.Original):!ColdRecoveryBeforeRow(row,lease.Original)))return false;
                return generation==loadGeneration&&path==scopePath&&ColdRecoveryRepositoryCurrent(lease,out var current)&&
                    ReferenceEquals(state,current)&&(after?ColdHydrationAfterRow(state,lease.Original):ColdRecoveryBeforeRow(state,lease.Original));
            }
        }
        catch{return false;}
    }
    private static bool ColdRecoveryBeforeRow(RebirthMetabolismState state,RebirthHydrationTransferOriginal original)
    {
        if(state==null||!RebirthHydrationTransferScope.MatchesState(original.Creation,state)||
            state.HydrationTransferReceipt?.Operation==original.Operation||!original.TryGetPlan(out var plan))return false;
        var item=state.HydrationSlotItem;int count=item==null||item.IsEmpty()?0:item.count;
        return count==plan.GearBefore.Count&&(count==0?string.Empty:RebirthNativeItemCodec.Encode(item.itemValue))==plan.GearBefore.ItemData;
    }
}
