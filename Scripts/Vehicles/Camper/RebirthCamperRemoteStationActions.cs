using System;
using System.Collections.Generic;

// Server command dispatch uses the same paid station-input preparation path as
// local crafting. Clients never publish queue entries or completion receipts.
internal static class RebirthCamperRemoteStationActions
{
    private static Func<EntityPlayer,RebirthCamperStationAddress,string,object,Guid,bool> cancel;
    internal static void Install(Func<EntityPlayer,RebirthCamperStationAddress,string,object,Guid,bool> cancelAction)
    {
        if(cancelAction==null)throw new ArgumentNullException(nameof(cancelAction));
        RebirthCamperRemoteStationTransport.Register(RebirthCamperCharacterAccess.CurrentCreation,HandleTracked,ObserveTerminal);
        cancel=cancelAction;
    }
    private static void ObserveTerminal(EntityPlayer player,RebirthCamperStationState observed)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            RebirthCamperStationNativeActorPayment.ObserveRemote(player,observed);return;
        }
        if(!ThreadManager.IsMainThread()||observed==null||
            !RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Progression==null||
            !RebirthCamperRemoteStationTransport.TryGetCurrent(player,out var current,out var address,out _,out _)||
            !ReferenceEquals(current,observed))return;
        // Character-owned records are bounded by their existing repository limits.
        // Observe only this installation; no player/world scan and no new ledger.
        foreach(var job in new List<string>(owner.Progression.CamperStationPublications.Keys))
            if(owner.Progression.StationPreparations.TryGetValue(job,out var queueAdmission)&&queueAdmission!=null&&
                RebirthCamperStationQueueRecovery.TryGetRecoveryLease(player,queueAdmission,
                    out var recoveryStation,out var recoveryAddress,out _)&&
                ReferenceEquals(recoveryStation,observed)&&recoveryAddress.Equals(address))
                RebirthCamperStationQueueRecoveryWatch.TryObserveRemote(player,job);
        foreach(var pair in owner.Progression.CamperStationTrainingPlans)
            if(owner.Progression.StationPreparations.TryGetValue(pair.Key,out var admission)&&admission!=null&&
                admission.TryGetMobileAddress(out var preparedAddress)&&preparedAddress.Equals(address))
                RebirthCamperStationTrainingRecoveryWatch.TryObserveRemote(player,pair.Key);
        foreach(var history in owner.Progression.StationRefundArchives.Values)
            if(history!=null&&history.IsCompletion&&
                RebirthStationGridAdmission.TryReadStored(history.Write().Element("stationAdmission"),out var archivedAdmission)&&
                archivedAdmission.TryGetMobileAddress(out var archivedAddress)&&archivedAddress.Equals(address))
                RebirthCamperStationTrainingRecoveryWatch.TryObserveRemote(player,history.JobId);
        // Refund observation can retire active records during this callback.
        // Iterate snapshots so retirement cannot invalidate our enumeration.
        foreach(var job in new List<string>(owner.Progression.CamperStationRefundDeliveries.Keys))
            if(owner.Progression.StationPreparations.TryGetValue(job,out var refundAdmission)&&refundAdmission!=null&&
                refundAdmission.TryGetMobileAddress(out var refundAddress)&&refundAddress.Equals(address))
                RebirthCamperStationRefundRecoveryWatch.TryObserveRemote(player,job);
        foreach(var job in new List<string>(owner.Progression.StationRefundArchives.Keys))
            if(owner.Progression.StationRefundArchives.TryGetValue(job,out var refundHistory)&&refundHistory!=null&&
                refundHistory.IsMobile&&!refundHistory.IsCompletion&&
                RebirthStationGridAdmission.TryReadStored(refundHistory.Write().Element("stationAdmission"),out var settledAdmission)&&
                settledAdmission.TryGetMobileAddress(out var settledAddress)&&settledAddress.Equals(address))
                RebirthCamperStationRefundRecoveryWatch.TryObserveRemote(player,job);
    }
    // Warm invocation evidence only. Missing evidence after restart never means
    // unused. A service may return false after effects, so enter before calling it.
    [ThreadStatic] private static Invocation invocation;
    private sealed class Invocation
    {
        internal bool Entered,ReturnedRefusal;
        internal EntityPlayer Player;internal RebirthCamperStationAddress Address;
        internal string Creation;internal object Lease;
        internal RebirthCamperRemoteStationCommand Command;
    }
    internal sealed class BeforeEffectRefusal
    {
        internal readonly EntityPlayer Player;
        internal readonly RebirthCamperStationAddress Address;
        internal readonly string Creation;
        internal readonly object Lease;
        internal readonly RebirthCamperRemoteStationCommand Command;
        private BeforeEffectRefusal(EntityPlayer player,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command)
        {Player=player;Address=address;Creation=creation;Lease=lease;Command=command;}
        internal static BeforeEffectRefusal CaptureReturnedRefusal(EntityPlayer player,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command)
            =>invocation!=null&&invocation.ReturnedRefusal&&!invocation.Entered&&ReferenceEquals(invocation.Player,player)&&invocation.Address.Equals(address)&&invocation.Creation==creation&&ReferenceEquals(invocation.Lease,lease)&&ReferenceEquals(invocation.Command,command)?new BeforeEffectRefusal(player,address,creation,lease,command):null;
    }
    private static void EnterEffects(){if(invocation!=null)invocation.Entered=true;}
    private static bool HandleTracked(EntityPlayer player,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command)
    {
        if(invocation!=null)return false; // no nested command may borrow evidence
        invocation=new Invocation{Player=player,Address=address,Creation=creation,Lease=lease,Command=command};
        try
        {
            bool result=Handle(player,address,creation,lease,command);
            invocation.ReturnedRefusal=!result;
            if(!result&&!invocation.Entered&&RebirthCamperRemoteStationCommandStatus.HasNativeOriginal(command))
                RebirthCamperRemoteStationCommandStatusStore.TrySaveBeforeEffectRefusal(
                    BeforeEffectRefusal.CaptureReturnedRefusal(player,address,creation,lease,command));
            return result;
        }
        finally{invocation=null;}
    }
    internal static bool Handle(EntityPlayer player,RebirthCamperStationAddress address,string creation,object lease,
        RebirthCamperRemoteStationCommand command)
    {
        if(command==null||!ThreadManager.IsMainThread()||
            !RebirthCamperRemoteStationTransport.IsCurrent(player,address,creation,lease))return false;
        if(command.IsFuel)
        {
            if(!RebirthCamperRemoteStationTransport.TryResolve(player,address,creation,out var station,out var currentLease)
                ||!ReferenceEquals(currentLease,lease)||RebirthCamperStationGridCustody.IsHeld(station))return false;
            return RebirthCamperStationFuelOriginal.Begin(player,station,address,creation,lease,command,EnterEffects);
        }
        if(command.Kind==RebirthCamperRemoteStationCommandKind.ScrapQueue || command.Kind==RebirthCamperRemoteStationCommandKind.ScrapCancel)
        {
            if(!RebirthCamperRemoteStationTransport.TryResolve(player,address,creation,out var recycler,out var currentLease)
                || !ReferenceEquals(currentLease,lease) || !RebirthRecyclerRuntime.Supports(recycler)
                || RebirthCamperStationGridCustody.IsHeld(recycler))return false;
            Func<bool> current=()=>RebirthCamperRemoteStationTransport.IsCurrent(player,address,creation,lease)
                && RebirthCamperRemoteStationTransport.TryResolve(player,address,creation,out var observed,out var observedLease)
                && ReferenceEquals(observed,recycler) && ReferenceEquals(observedLease,lease);
            if(command.Kind==RebirthCamperRemoteStationCommandKind.ScrapCancel)
            {EnterEffects();return RebirthRecyclerRuntime.TryCancel(player,recycler,command.Job,current,out _);}
            if(command.Operation!=command.Job || recycler.Input==null || command.Tier<0 || command.Tier>=recycler.Input.Length)return false;
            var input=recycler.Input[command.Tier];
            if(input==null || input.IsEmpty() || input.itemValue.ItemClass.GetItemName()!=command.RecipeName)return false;
            EnterEffects();return RebirthRecyclerRuntime.TryQueue(player,recycler,command.Tier,command.Batches,command.Operation,current,out _);
        }
        if(command.Kind==RebirthCamperRemoteStationCommandKind.Cancel)
        {
            if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return RebirthCamperStationNativeCrafting.Cancel(player,address,creation,command.Job,command,EnterEffects);
            EnterEffects();return cancel!=null&&cancel(player,address,creation,lease,command.Job);
        }
        if(command.Kind!=RebirthCamperRemoteStationCommandKind.Craft&&command.Kind!=RebirthCamperRemoteStationCommandKind.CraftPlayer)return false;
        if(!RebirthCamperRemoteStationTransport.TryGetCurrent(player,out var sourceStation,out _,out _,out _)||
            !RebirthCamperRemoteStationPlayerSource.TryResolve(player,sourceStation,out var actorSource)||
            actorSource!=(command.Kind==RebirthCamperRemoteStationCommandKind.CraftPlayer))return false;
        Recipe recipe=null;
        var definitions=XUiM_Recipes.GetRecipes();if(definitions==null)return false;
        foreach(var candidate in definitions)
            if(candidate!=null&&candidate.GetName()==command.RecipeName)
            {if(recipe!=null)return false;recipe=candidate;}
        if(recipe==null||!RebirthCamperRemoteStationTransport.IsCurrent(player,address,creation,lease))return false;
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            if(actorSource){EnterEffects();return RebirthCamperStationNativeActorPayment.CraftPlayer(player,address,creation,
                command.Job,recipe,command.Batches,command.Tier);}
            return RebirthCamperStationNativeCrafting.Craft(player,address,creation,command.Job,recipe,command.Batches,command.Tier,command,EnterEffects);
        }
        var previous=RebirthStationPreparationService.RemoteUsesPlayerPayment;
        RebirthStationPreparationService.RemoteUsesPlayerPayment=(offered,station)=>
            actorSource&&ReferenceEquals(offered,player)&&ReferenceEquals(station,sourceStation)&&
            RebirthCamperRemoteStationTransport.IsCurrent(player,address,creation,lease)&&
            RebirthCamperRemoteStationPlayerSource.IsCurrent(player,station);
        try
        {
        EnterEffects();if(
            !RebirthStationPreparationService.CanPrepareMobile(player,address,creation,recipe,command.Batches,command.Tier))return false;
        EnterEffects();return RebirthStationPreparationService.TryPrepareMobile(player,address,creation,command.Job,recipe,command.Batches,
            out _,command.Tier)&&RebirthCamperStationPublicationWatch.TryPublishPrepared(player,address,creation,command.Job);
        }
        finally{RebirthStationPreparationService.RemoteUsesPlayerPayment=previous;}
    }
}




