using System;
using System.IO;

public static partial class RebirthMetabolismStateRepository
{
    internal static bool TrySettleRemoteHydration(RebirthHydrationServerSourceHold.Lease lease)
        =>QualifyRemoteHydration(lease,false);
    internal static bool HasSavedRemoteHydrationAfter(RebirthHydrationServerSourceHold.Lease lease)
        =>QualifyRemoteHydration(lease,true);
    internal static bool HasSavedRemoteHydrationAfterForAcknowledgment(RebirthHydrationServerSourceHold.Lease lease)
        =>QualifyRemoteHydration(lease,true,true);
    private static bool QualifyRemoteHydration(RebirthHydrationServerSourceHold.Lease lease,bool observeOnly,bool historicalOwner=false)
    {
        try
        {
            if(lease==null||!lease.Current()||!serverAuthority||!lease.Original.TryGetPlan(out var plan)||
                !RebirthHydrationTransferJournal.HasAttempted(lease.Player,lease.Original)||
                !(historicalOwner?observeOnly&&RebirthHydrationTransferJournal.HasApplied(lease.Player,lease.Original):RebirthHydrationRemoteOwnerWitness.HasApplied(lease)))return false;
            var original=lease.Original;ItemStack after;
            if(plan.GearAfter.Count==0)after=ItemStack.Empty.Clone();
            else
            {
                if(plan.GearAfter.Count!=1||!RebirthNativeItemCodec.TryDecode(plan.GearAfter.ItemData,out var value)||
                    value==null||value.IsEmpty()||value.ItemClass==null)return false;
                after=new ItemStack(value,1);
            }
            string id,path;int generation;RebirthMetabolismState state;
            lock(Sync)
            {
                if(!ScopeCurrentLocked()||!ReferenceEquals(scopeWorld,lease.HeldWorld)||!lease.Current()||
                    !States.TryGetValue(id=GetStablePlayerId(lease.Player),out state)||!ReferenceEquals(state,lease.State))return false;
                generation=loadGeneration;path=scopePath;
                if(state.HydrationTransferReceipt?.Digest!=original.Digest)
                {
                    if(observeOnly||!HasSavedRemoteHydrationBefore(lease)||!lease.Current())return false;
                    // No server inventory mirror/native client setter is used.
                    state.HydrationSlotItem=after;state.HydrationTransferReceipt=original;state.Touch();
                }
                if(!lease.Current())return false;
            }
            if(!observeOnly&&!HasSavedRemoteHydrationAfter(lease)&&!TrySave("hydration-remote-metabolism-leg",out _))return false;
            lock(Sync)
            {
                if(generation!=loadGeneration||path!=scopePath||!ScopeCurrentLocked()||!lease.Current()||
                    !States.TryGetValue(id,out var current)||!ReferenceEquals(current,state))return false;
                var info=new FileInfo(path);
                if(!info.Exists||info.Length<1||info.Length>MaximumStoreBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                var saved=ReadCandidate(path);
                if(!saved.TryGetValue(id,out var row)||row.HydrationTransferReceipt?.Digest!=original.Digest||
                    !RebirthHydrationTransferScope.MatchesState(original.Creation,row))return false;
                var item=row.HydrationSlotItem;int count=item==null||item.IsEmpty()?0:item.count;
                string data=count==0?string.Empty:RebirthNativeItemCodec.Encode(item.itemValue);
                return count==plan.GearAfter.Count&&data==plan.GearAfter.ItemData&&
                    generation==loadGeneration&&path==scopePath&&ScopeCurrentLocked()&&lease.Current()&&
                    States.TryGetValue(id,out current)&&ReferenceEquals(current,state)&&
                    RebirthHydrationTransferJournal.HasAttempted(lease.Player,original)&&
                    (historicalOwner?RebirthHydrationTransferJournal.HasApplied(lease.Player,original):RebirthHydrationRemoteOwnerWitness.HasApplied(lease));
            }
        }
        catch{return false;}
    }
}
