using System;
using System.Collections.Generic;

// Local owning-action conductor. Remote transport and cold originals use their
// own admission/recovery; this never adopts saved descriptions as warm grants.
internal static class RebirthHydrationLocalTransfer
{
    internal enum Result { Refused, Pending, Applied }
    private sealed class Pending
    {
        internal EntityPlayerLocal Player;internal RebirthHydrationTransferOriginal Original;
        internal RebirthHydrationTransferGrant Grant;
        internal Pending Previous;
        internal bool Running,Reentered;
    }
    private static readonly Dictionary<EntityPlayerLocal,Pending> pending=new Dictionary<EntityPlayerLocal,Pending>();
    private static bool admitting;
    private static World tickWorld;
    private static double nextTick,lastTick;
    internal static bool Tick()
    {
        if(!ThreadManager.IsMainThread())return false;
        var world=GameManager.Instance?.World;
        if(world==null||world.IsRemote()||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true)return false;
        double now=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
        if(!ReferenceEquals(tickWorld,world)||now<lastTick)nextTick=now;
        if(now<nextTick)return false;
        tickWorld=world;lastTick=now;nextTick=now+0.75d;
        // Resume only the actual current owner. No saved description or timer
        // creates an action, and a foreign/ended body retains its original.
        var player=world.GetPrimaryPlayer();
        if(player==null)return false;
        var result=TryResume(player);
        // Refused with nothing held means the journal on disk has no work. Starting a transfer
        // does not wait for this tick, so the disk rescan can run less often while idle.
        if(result==Result.Refused&&!pending.ContainsKey(player)&&!RebirthHydrationColdRecoveryLease.TryGetRetained(player,out _))
            nextTick=now+5d;
        if(result!=Result.Applied)return false;
        RebirthMetabolismService.SendSnapshotToOwner(player,true);
        return true;
    }
    internal static Result TryBegin(EntityPlayerLocal player,bool sourceIsBag,int sourceIndex,bool returning)
    {
        bool entered=false;
        try
        {
            if(player==null||!ThreadManager.IsMainThread())return Refuse(player,returning,sourceIsBag,sourceIndex,"invalid-owner-or-thread");
            // Any request on this exact actor resumes its retained original;
            // changed selection/action never replaces an entered operation.
            if(pending.TryGetValue(player,out var retained))return Resume(retained);
            if(RebirthHydrationColdRecoveryLease.TryGetRetained(player,out var recovery))
                return recovery.TryComplete()?Result.Applied:Result.Pending;
            if(admitting)return Refuse(player,returning,sourceIsBag,sourceIndex,"admission-busy");
            var recovered=TryRecoverOriginal(player);
            if(recovered!=Result.Refused)return recovered;
            admitting=true;entered=true;
            if(pending.Count>=64||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
                player.world==null||player.world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,player.world)||
                !ReferenceEquals(player.world.GetPrimaryPlayer(),player)||player.IsDead()||!player.IsSpawned()||
                RebirthHydrationOwnerHold.HoldsMetabolismOwner(player)||
                !RebirthHydrationTransferScope.TryCapture(player,out var creation)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                !Guid.TryParse(player.world.Guid,out var world)||world==Guid.Empty||
                !RebirthMetabolismStateRepository.TryGet(player,out var state)||
                !RebirthHydrationTransferScope.MatchesState(creation,state))return Refuse(player,returning,sourceIsBag,sourceIndex,"owner-or-state-scope");
            if(!TryClearCommittedCold(player))
                return RebirthHydrationColdRecoveryLease.TryGetRetained(player,out _)?Result.Pending:Refuse(player,returning,sourceIsBag,sourceIndex,"cold-recovery");
            // Recovery may publish the source result. A replacement repository
            // row cannot inherit the new action's pre-recovery state witness.
            if(!RebirthMetabolismStateRepository.TryGet(player,out var latest)||!ReferenceEquals(latest,state)||
                !RebirthHydrationTransferScope.MatchesState(creation,state))return Refuse(player,returning,sourceIsBag,sourceIndex,"state-changed-after-recovery");
            var bag=player.bag?.ItemGrid?.items;var belt=player.inventory?.ItemGrid?.items;
            if(bag==null||belt==null)return Refuse(player,returning,sourceIsBag,sourceIndex,"native-grid-unavailable");
            int owned=RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length);
            if(!RebirthGearInventorySnapshot.TryCapture(bag,belt,owned,out var snapshot))return Refuse(player,returning,sourceIsBag,sourceIndex,"native-snapshot-failed");
            var hydration=state.HydrationSlotItem;
            if (RebirthDebug.IsOn("metabolism"))
                Trace(player,returning,sourceIsBag,sourceIndex,"admission",$"slot={SafeName(hydration)} count={(hydration==null||hydration.IsEmpty()?0:hydration.count)} bag={bag.Length} freeUnlocked={FreeUnlocked(player,bag)} stateRevision={state.Revision}");
            var cell=new RebirthGearInventoryPlan.Stack{Count=hydration==null||hydration.IsEmpty()?0:hydration.count};
            cell.ItemData=cell.Count==0?string.Empty:RebirthNativeItemCodec.Encode(hydration.itemValue);
            var nativeLocks=player.bag.LockedSlots;
            if(nativeLocks!=null&&nativeLocks.Length!=bag.Length)return Refuse(player,returning,sourceIsBag,sourceIndex,"lock-grid-length-mismatch");
            var lockedBag=nativeLocks==null?null:Enumerable.Range(0,nativeLocks.Length).Select(index=>nativeLocks[index]).ToArray();
            if(!RebirthHydrationTransferPlan.TryCreate(snapshot.Bag,snapshot.Belt,lockedBag,owned,cell,
                sourceIsBag,returning?-1:sourceIndex,out var plan))return Refuse(player,returning,sourceIsBag,sourceIndex,"plan-refused");
            var container=returning?plan.GearBefore:plan.GearAfter;
            if(!RebirthNativeItemCodec.TryDecode(container.ItemData,out var value)||value==null||value.ItemClass==null)
                return Refuse(player,returning,sourceIsBag,sourceIndex,"item-decode-failed");
            // A consumed drink can leave its empty jar in this exact custody cell.
            // Returning any valid occupied cell is safe: the conserved plan moves
            // that same item to an unlocked native backpack slot without cloning it.
            if(!returning&&(!RebirthConsumableResolver.TryResolve(value,out var definition)||definition==null||!definition.IsDrink||!definition.HydrationEquippable))
                return Refuse(player,returning,sourceIsBag,sourceIndex,"source-is-not-equippable-drink");
            if(!returning&&RebirthSurvivorMode.IsEnabledForCurrentWorld())
            {
                if(!RebirthWorldCharacterService.TryGet(player,out var character)||character?.Support==null)return Refuse(player,returning,sourceIsBag,sourceIndex,"survivor-support-unavailable");
                if(character.Support.EquippedGearBySlot.TryGetValue(RebirthSurvivorGearService.SupportSlotId,out var gear)&&!string.IsNullOrEmpty(gear))return Refuse(player,returning,sourceIsBag,sourceIndex,"support-slot-occupied");
            }
            if(!RebirthHydrationTransferOriginal.TryCreate(Guid.NewGuid(),world,owner.StorageKey,creation,player.Died,state.Revision,
                owned,lockedBag,plan,out var original)||!RebirthHydrationOwnerHold.TryAcquire(player,original))return Refuse(player,returning,sourceIsBag,sourceIndex,"original-or-owner-hold-failed");
            var next=new Pending{Player=player,Original=original};pending.Add(player,next);
            var result=Resume(next);
            Trace(player,returning,sourceIsBag,sourceIndex,"result",result.ToString());
            return result;
        }
        catch(Exception ex){Trace(player,returning,sourceIsBag,sourceIndex,"exception",ex.GetType().Name);return player!=null&&pending.ContainsKey(player)?Result.Pending:Result.Refused;}
        finally{if(entered)admitting=false;}
    }
    private static Result Refuse(EntityPlayerLocal player,bool returning,bool sourceIsBag,int sourceIndex,string reason)
    {Trace(player,returning,sourceIsBag,sourceIndex,"refused",reason);return Result.Refused;}
    private static void Trace(EntityPlayerLocal player,bool returning,bool sourceIsBag,int sourceIndex,string phase,string detail)
    {RebirthDebug.Log("metabolism", $"[REBIRTH HydrationTransfer] {phase} action={(returning?"return":"equip")} actor={player?.entityId.ToString()??"none"} source={(sourceIsBag?"bag":"belt")} index={sourceIndex} {detail}");}
    private static string SafeName(ItemStack stack)=>stack==null||stack.IsEmpty()?"empty":stack.itemValue?.ItemClass?.GetItemName()??"unknown";
    private static int FreeUnlocked(EntityPlayerLocal player,ItemStack[] bag)
    {if(bag==null)return -1;var locks=player?.bag?.LockedSlots;int free=0;for(int i=0;i<bag.Length;i++)if((bag[i]==null||bag[i].IsEmpty())&&(locks==null||i>=locks.Length||!locks[i]))free++;return free;}
    internal static Result TryResume(EntityPlayerLocal player)
    {
        if(player==null||!ThreadManager.IsMainThread())return Result.Refused;
        if(pending.TryGetValue(player,out var original))return Resume(original);
        if(RebirthHydrationColdRecoveryLease.TryGetRetained(player,out var recovery))return recovery.TryComplete()?Result.Applied:Result.Pending;
        return TryRecoverOriginal(player);
    }
    private static Result TryRecoverOriginal(EntityPlayerLocal player)
    {
        if(admitting)return Result.Pending;
        admitting=true;
        try
        {
            pending.TryGetValue(player,out var prior);
            if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=false||
                !RebirthHydrationTransferJournal.TryReadPending(player,out var saved,out _))return Result.Pending;
            if(saved==null)return prior==null?Result.Refused:Result.Pending;
            if(prior!=null)
            {
                if(prior.Original.Digest!=saved.Digest||RebirthHydrationOwnerHold.Matches(player,prior.Original))return Result.Pending;
                int count=0;for(var cursor=prior;cursor!=null;cursor=cursor.Previous)if(++count>=64)return Result.Pending;
            }
            // A retained warm grant can already own the dirty metabolism After
            // when only the native manager/backing object was replaced. Rebind
            // the exact saved owner result, then finish metadata only. Never
            // route this continuation through the inventory application API.
            if(prior?.Grant!=null&&prior.Grant.Original.Digest==saved.Digest&&
                RebirthHydrationOwnerHold.TryReenterRemote(player,saved)&&prior.Grant.Current()&&
                RebirthHydrationOwnerCheckpoint.HasSaved(player,saved,RebirthGearOwnerReceipt.Applied))
            {
                if(!prior.Grant.TryCompleteLocal())return Result.Pending;
                if(pending.TryGetValue(player,out var completedWarm)&&ReferenceEquals(completedWarm,prior))pending.Remove(player);
                return Result.Applied;
            }
            if(RebirthHydrationTransferJournal.TryCompleteSavedLocal(player,saved))
            {if(prior!=null&&pending.TryGetValue(player,out var completed)&&ReferenceEquals(completed,prior))pending.Remove(player);return Result.Applied;}
            if(RebirthHydrationColdRecoveryLease.TryAcquire(player,saved,out var recovery))
            {
                if(!recovery.TryComplete())return Result.Pending;
                if(prior!=null&&pending.TryGetValue(player,out var completed)&&ReferenceEquals(completed,prior))pending.Remove(player);
                return Result.Applied;
            }
            if(prior==null&&pending.Count>=64||!RebirthHydrationOwnerHold.TryReenterRemote(player,saved))return Result.Pending;
            var next=new Pending{Player=player,Original=saved,Reentered=true,Previous=prior};pending[player]=next;
            return Resume(next);
        }
        catch{return Result.Pending;}
        finally{admitting=false;}
    }
    private static bool TryClearCommittedCold(EntityPlayerLocal player)
    {
        if(!RebirthHydrationTransferJournal.TryReadPending(player,out var cold,out _))return false;
        if(cold==null)return true;
        bool completed=RebirthHydrationTransferJournal.TryCompleteSavedLocal(player,cold)||
            RebirthHydrationColdRecoveryLease.TryAcquire(player,cold,out var recovery)&&recovery.TryComplete();
        return completed&&
            RebirthHydrationTransferJournal.TryReadPending(player,out var remaining,out _)&&remaining==null;
    }
    internal static Result TryCancelPrepared(EntityPlayerLocal player)
    {
        if(player==null||!ThreadManager.IsMainThread()||!pending.TryGetValue(player,out var retained))return Result.Refused;
        if(retained.Running||retained.Grant!=null)return Result.Pending;
        retained.Running=true;
        try
        {
            var original=retained.Original;
            if(!RebirthHydrationOwnerHold.Matches(player,original)||RebirthHydrationTransferGrant.HasIssued(original.Operation)||
                !RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying)||
                !RebirthMetabolismStateRepository.TrySaveLocalHydrationBefore(player,original)||
                !RebirthHydrationTransferJournal.LocalPreparedRejection.TryPublish(player,original,out var rejection)||
                !RebirthHydrationLocalRefusedCheckpoint.TryPersist(rejection)||
                !RebirthHydrationOwnerHold.TryReleaseLocalPreparedRefusal(rejection))return Result.Pending;
            pending.Remove(player);return Result.Refused;
        }
        catch{return Result.Pending;}
        finally{retained.Running=false;}
    }
    private static Result Resume(Pending original)
    {
        if(original==null||original.Running)return Result.Pending;
        if(ThreadManager.IsMainThread()&&original?.Player!=null&&pending.TryGetValue(original.Player,out var current)&&ReferenceEquals(current,original)&&
            !RebirthHydrationOwnerHold.Matches(original.Player,original.Original))return TryRecoverOriginal(original.Player);
        original.Running=true;
        try
        {
            var player=original.Player;
            if(!ThreadManager.IsMainThread()||!pending.TryGetValue(player,out var retained)||!ReferenceEquals(retained,original)||
                !RebirthHydrationOwnerHold.Matches(player,original.Original))return Result.Pending;
            if(original.Grant==null)
            {
                if(original.Reentered)
                {
                    if(!RebirthHydrationTransferGrant.TryIssueLocalReentered(player,original.Original,out var recovered))return Result.Pending;
                    original.Grant=recovered;
                }
                else
                {
                    if(!RebirthHydrationTransferJournal.TryPrepare(player,original.Original,out _)||
                        !RebirthMetabolismStateRepository.TrySaveLocalHydrationBefore(player,original.Original)||
                        !RebirthHydrationOwnerCheckpoint.TryPersist(player,original.Original,RebirthGearOwnerReceipt.Applying)||
                        !RebirthHydrationTransferGrant.TryIssueLocal(player,original.Original,out var grant))return Result.Pending;
                    original.Grant=grant;
                }
            }
            if(RebirthHydrationOwnerApply.Apply(original.Grant)!=RebirthGearOwnerApplySequence.Result.Applied||
                !original.Grant.TryCompleteLocal())return Result.Pending;
            var remove=new List<EntityPlayerLocal>();
            foreach(var pair in pending)if(pair.Value.Original.Digest==original.Original.Digest)remove.Add(pair.Key);
            foreach(var key in remove)pending.Remove(key);
            return Result.Applied;
        }
        catch{return Result.Pending;}
        finally{original.Running=false;}
    }
}
