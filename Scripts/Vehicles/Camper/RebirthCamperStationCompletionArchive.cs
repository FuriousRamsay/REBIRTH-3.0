using System;
using System.Collections.Generic;
using System.Xml.Linq;

// Retained native-save observation after reward/receipt retirement. Never awards,
// inserts output or removes a native receipt; queue absence alone is insufficient.
internal sealed class RebirthCamperStationCompletionArchive : IDisposable
{
    private readonly EntityPlayer player;
    private readonly RebirthWorldCharacterRecord owner;
    private readonly RebirthStablePlayerIdentity identity;
    private readonly RebirthCamperStationState station;
    private readonly RebirthCamperStationAddress address;
    private readonly object lease,world;
    private readonly int actor;
    private readonly ItemStack installed;
    private readonly RebirthStationGridAdmission admission;
    private readonly RebirthCamperStationPublicationRecord queued;
    private readonly RebirthCamperStationCompletionRecord completed;
    private readonly RebirthCamperStationTrainingRecord training;
    private readonly RebirthStationTerminalIntent intent;
    private RebirthStationRefundArchive archive;
    private RebirthCamperStationSaveEvidence.Request request;
    private RebirthCamperStationSaveEvidence.Publication proof;
    private bool closed,settled;
    private RebirthCamperStationCompletionArchive(EntityPlayer player,RebirthWorldCharacterRecord owner,
        RebirthStablePlayerIdentity identity,RebirthCamperStationState station,RebirthCamperStationAddress address,
        object lease,ItemStack installed,RebirthStationGridAdmission admission,RebirthCamperStationPublicationRecord queued,
        RebirthCamperStationCompletionRecord completed,RebirthCamperStationTrainingRecord training,RebirthStationTerminalIntent intent,
        RebirthStationRefundArchive archive)
    {
        this.player=player;this.owner=owner;this.identity=identity;this.station=station;this.address=address;this.lease=lease;
        world=player.world;actor=player.entityId;this.installed=installed.Clone();this.admission=admission.Clone();
        this.queued=queued.Clone();this.completed=completed.Clone();this.training=training.Clone();this.intent=intent.Clone();
        this.archive=archive?.Clone();
    }
    internal static bool TryBegin(EntityPlayer player,string job,out RebirthCamperStationCompletionArchive operation)
    {
        operation=null;
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||string.IsNullOrEmpty(job)||
                !RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Progression==null||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null)return false;
            var state=owner.Progression;
            RebirthStationGridAdmission admission;RebirthCamperStationPublicationRecord queued;
            RebirthCamperStationCompletionRecord completed;RebirthCamperStationTrainingRecord training;RebirthStationTerminalIntent intent;
            RebirthStationRefundArchive archive=null;
            if(state.StationRefundArchives.TryGetValue(job,out var cached))
            {
                if(cached==null||!cached.IsCompletion||!RebirthStationRefundArchive.TryRead(cached.Write(),identity.StorageKey,out archive))return false;
                var node=archive.Write();
                if(!RebirthStationGridAdmission.TryReadStored(node.Element("stationAdmission"),out admission)||
                    !RebirthCamperStationPublicationRecord.TryRead(node.Element("camperStationPublication"),admission,out queued)||
                    !RebirthCamperStationCompletionRecord.TryRead(node.Element("camperStationCompletion"),admission,queued,out completed)||
                    !RebirthCamperStationTrainingRecord.TryRead(node.Element("camperStationTraining"),admission,queued,completed,out training)||
                    !RebirthStationTerminalIntent.TryRead(node.Element("stationTerminalIntent"),admission,out intent))return false;
            }
            else if(!state.StationPreparations.TryGetValue(job,out admission)||admission==null||
                !state.CamperStationPublications.TryGetValue(job,out queued)||queued==null||
                !state.CamperStationCompletions.TryGetValue(job,out completed)||completed==null||
                !state.CamperStationTrainingPlans.TryGetValue(job,out training)||training==null||
                !state.StationTerminalIntents.TryGetValue(job,out intent)||intent==null)return false;
            if(!admission.TryGetMobileAddress(out var address)||
                !RebirthStationLiveAccess.TryGetMobileLease(player,address,admission.CreationId,out var station,out var lease)||
                RebirthCamperStationSaveEvidence.IsPending(station)||!intent.IsCompletion||
                !RebirthStationRefundArchive.TryCreate(identity.StorageKey,admission,queued,completed,training,intent,out var proposed))return false;
            if(archive==null)
            {
                var all=new Dictionary<string,RebirthStationRefundArchive>(state.StationRefundArchives,StringComparer.Ordinal);
                all.Add(job,proposed);RebirthStationRefundArchive.WriteAll(all,identity.StorageKey);
            }
            var installed=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(address.Installation)?.InstalledItem;
            if(installed==null)return false;
            var candidate=new RebirthCamperStationCompletionArchive(player,owner,identity,station,address,lease,installed,
                admission,queued,completed,training,intent,archive);
            if(!candidate.Current())return false;
            // Retain before BeginRecovery can dirty native state or acquire pending custody.
            operation=candidate;candidate.request=RebirthCamperStationSaveEvidence.BeginRecovery(player,station,address,candidate.Current);
            return true;
        }
        catch{return false;}
    }
    private static bool Same(XElement first,XElement second)=>XNode.DeepEquals(first,second);
    private bool Retired(RebirthCamperStationState state)
    {
        if(state?.Queue==null||!RebirthStationCompletionReceipt.HasNoJobReceipt(state.CraftCompleteList,admission.JobId))return false;
        foreach(var row in state.Queue)
            if(row==null||RebirthStationGridQueue.IsMarked(row.Recipe)&&
                (!RebirthStationGridQueue.TryGetJobId(row.Recipe,out var job)||job==admission.JobId))return false;
        return true;
    }
    private bool Current()
    {
        try
        {
            if(closed||!ThreadManager.IsMainThread()||player.entityId!=actor||!ReferenceEquals(player.world,world)||
                player.world==null||player.world.IsRemote()||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||
                player is EntityPlayerLocal&&(!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                    GameManager.Instance?.getPersistentPlayerID(null)?.CombinedString!=identity.CanonicalId)||
                player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player)||!ReferenceEquals(GameManager.Instance?.World,world)||
                !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
                !RebirthCamperVehicleIdentity.IsUniqueCurrent(station.BoundVehicle,address.Vehicle)||
                !RebirthStationLiveAccess.TryGetMobileLease(player,address,admission.CreationId,out var leased,out var currentLease)||
                !ReferenceEquals(leased,station)||!ReferenceEquals(lease,currentLease)||
                !RebirthStationLiveAccess.TryResolveMobile(player,address,admission.CreationId,out var live,out var currentOwner)||
                !ReferenceEquals(live,station)||!ReferenceEquals(currentOwner,owner)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var currentIdentity)||currentIdentity==null||
                currentIdentity.StorageKey!=identity.StorageKey||currentIdentity.CanonicalId!=identity.CanonicalId||
                !RebirthStationGridIngredients.IsSameStackSnapshot(installed,
                    RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(address.Installation)?.InstalledItem)||
                !queued.MatchesSaveScope(GameIO.GetSaveGameDir())||!Retired(station)||
                RebirthCamperStationCompletionCallsite.IsHeld(station)||
                !RebirthCamperStationNativeRewardReceipt.HasSaved(identity,admission,true)||
                player.Buffs?.CVars==null||!player.Buffs.CVars.TryGetValue(RebirthCamperStationNativeRewardReceipt.Key(admission),out var receipt)||receipt!=1f)return false;
            var state=owner.Progression;string job=admission.JobId;
            if(training.RestoreInputs().HasTraining&&!state.SkillAwardReceipts.Contains(training.Receipt))return false;
            if(state.StationPublications.ContainsKey(job)||state.StationCancellationAttempts.ContainsKey(job)||
                state.StationCancellationRefunds.ContainsKey(job)||state.StationCancellationPublications.ContainsKey(job)||
                state.StationCompletionPublications.ContainsKey(job)||state.StationCompletionExpectationProjections.ContainsKey(job)||
                state.CamperStationCancellations.ContainsKey(job)||state.CamperStationRefundDeliveries.ContainsKey(job)||
                state.StationRefundDeliveries.ContainsKey(job))return false;
            if(archive!=null)
                return archive.IsCompletion&&state.StationRefundArchives.TryGetValue(job,out var cached)&&cached!=null&&Same(cached.Write(),archive.Write())&&
                    !state.StationPreparations.ContainsKey(job)&&!state.CamperStationPublications.ContainsKey(job)&&
                    !state.CamperStationCompletions.ContainsKey(job)&&!state.CamperStationTrainingPlans.ContainsKey(job)&&!state.StationTerminalIntents.ContainsKey(job);
            return !state.StationRefundArchives.ContainsKey(job)&&
                state.StationPreparations.TryGetValue(job,out var a)&&a!=null&&Same(a.Write(),admission.Write())&&
                state.CamperStationPublications.TryGetValue(job,out var q)&&q!=null&&Same(q.Write(),queued.Write())&&
                state.CamperStationCompletions.TryGetValue(job,out var c)&&c!=null&&Same(c.Write(),completed.Write())&&
                state.CamperStationTrainingPlans.TryGetValue(job,out var t)&&t!=null&&Same(t.Write(),training.Write())&&
                state.StationTerminalIntents.TryGetValue(job,out var i)&&i!=null&&Same(i.Write(),intent.Write())&&
                RebirthWorldCharacterRepository.HasSavedCamperStationTrainingPlan(identity,admission,queued,completed,training,training.RestoreInputs().HasTraining)&&
                RebirthWorldCharacterRepository.HasSavedStationTerminalIntent(identity,admission,intent);
        }
        catch{return false;}
    }
    internal bool CanContinue=>Current()&&(proof!=null||request==null||!request.Closed);
    private bool Observe()
    {
        if(!Current())return false;
        if(proof==null)
        {
            if(request==null)request=RebirthCamperStationSaveEvidence.BeginRecovery(player,station,address,Current);
            if(!RebirthCamperStationSaveEvidence.TryObserve(request,out var observed))return false;
            proof=observed;
        }
        return proof.Address.Equals(address)&&proof.VehicleEntityId==station.BoundVehicle.entityId&&
            proof.VehicleClass==(int)queued.Write().Attribute("vehicleClass")&&queued.MatchesSaveScope(proof.SaveDirectory)&&
            Retired(proof.RestoreStationSnapshot())&&Current();
    }
    internal bool TryArchive()
    {
        try
        {
            if(settled)return !closed;
            if(!Observe())return false;
            if(archive==null)
            {
                if(!RebirthStationRefundArchive.TryCreate(identity.StorageKey,admission,queued,completed,training,intent,out var proposed))return false;
                var state=owner.Progression;var all=new Dictionary<string,RebirthStationRefundArchive>(state.StationRefundArchives,StringComparer.Ordinal);
                all.Add(admission.JobId,proposed);RebirthStationRefundArchive.WriteAll(all,identity.StorageKey);
                if(!Observe())return false;
                archive=proposed.Clone();state.StationRefundArchives.Add(admission.JobId,archive.Clone());
                state.StationPreparations.Remove(admission.JobId);state.CamperStationPublications.Remove(admission.JobId);
                state.CamperStationCompletions.Remove(admission.JobId);state.CamperStationTrainingPlans.Remove(admission.JobId);
                state.StationTerminalIntents.Remove(admission.JobId);
                RebirthWorldCharacterService.MarkDirty(owner,"camper-completion-archive");
            }
            if(!Observe())return false;
            RebirthWorldCharacterService.MarkDirty(owner,"camper-completion-archive");
            if(!RebirthWorldCharacterService.FlushPlayer(player,"camper-completion-archive")||!Observe()||
                !RebirthWorldCharacterRepository.HasSavedStationRefundArchive(identity,archive))return false;
            Func<bool> savedTerminal=()=>Observe()&&RebirthWorldCharacterRepository.HasSavedStationRefundArchive(identity,archive);
            return settled=RebirthStationPreparationReservation.TryReleaseSettled(player.world,owner,admission,savedTerminal)&&
                RebirthCamperStationQueuedAuthority.TryReleaseArchived(station,owner,identity,archive,savedTerminal);
        }
        catch{return false;}
    }
    public void Dispose()
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Release completion archive on game thread.");
        if(closed)return;request?.Dispose();closed=true;
    }
}
