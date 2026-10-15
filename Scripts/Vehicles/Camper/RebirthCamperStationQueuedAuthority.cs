using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

// Original confirmed publications only. Serialized hashes cannot reconstruct
// this live authority; restored jobs need a separate native-save recovery check.
internal static class RebirthCamperStationQueuedAuthority
{
    internal sealed class Binding
    {
        internal readonly EntityVehicle Vehicle;
        internal EntityPlayer Player {get;private set;}
        internal readonly World World;
        internal readonly RebirthWorldCharacterRecord Owner;
        internal readonly RebirthWorldProgressionState Progression;
        internal readonly RebirthStablePlayerIdentity Identity;
        internal readonly RebirthCamperStationState Station;
        internal readonly RebirthCamperStationAddress Address;
        internal readonly RebirthStationGridAdmission Admission;
        internal readonly RebirthCamperStationPublicationRecord Publication;
        internal readonly string SaveRoot;
        // Native recipe custody keeps its original actor. Current Player is
        // separately authenticated through the stable owner record and identity.
        internal int QueuedOwner=>(int)Publication.Write().Attribute("queuedOwner");
        private RebirthStationTerminalIntent completionIntent;
        internal Binding(EntityPlayer player,RebirthWorldCharacterRecord owner,RebirthStablePlayerIdentity identity,
            RebirthCamperStationState station,RebirthCamperStationAddress address,RebirthStationGridAdmission admission,
            RebirthCamperStationPublicationRecord publication)
        {
            Vehicle=station.BoundVehicle;Player=player;World=player?.world??station.BoundVehicle?.world;Owner=owner;Progression=owner.Progression;
            Identity=identity;Station=station;Address=address;Admission=admission.Clone();Publication=publication.Clone();
            SaveRoot=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());
        }
        internal bool IsCurrent()
        {
            try
            {
                return IsOwnerCurrent()&&MatchesCompletionIntent()&&
                    !Progression.StationCancellationAttempts.ContainsKey(Admission.JobId)&&
                    !Progression.StationCancellationRefunds.ContainsKey(Admission.JobId);
            }
            catch{return false;}
        }
        // Owner/installation/publication scope only. Cancellation settlement
        // must separately verify its exact staged choice/refund/attempt records.
        // This predicate alone never grants queue processing or native effects.
        internal bool IsProcessingCurrent()=>IsOwnerScope(false)&&MatchesCompletionIntent()&&!Progression.StationCancellationAttempts.ContainsKey(Admission.JobId)&&!Progression.StationCancellationRefunds.ContainsKey(Admission.JobId);
        internal bool IsOwnerCurrent()=>IsOwnerScope(true);
        private bool IsOwnerScope(bool liveOwner)
        {
            try
            {
                if(!ThreadManager.IsMainThread()||World==null||World.IsRemote()||
                    !ReferenceEquals(GameManager.Instance?.World,World)||!ReferenceEquals(Vehicle?.world,World)||
                    !ReferenceEquals(World.GetEntity(Vehicle.entityId),Vehicle)||!ReferenceEquals(Station.BoundVehicle,Vehicle)||
                    liveOwner&&(!ReferenceEquals(Player?.world,World)||Player.IsDead()||!ReferenceEquals(World.GetEntity(Player.entityId),Player))||
                    !ReferenceEquals(Owner.Progression,Progression)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(Owner)||
                    !RebirthSurvivorRequestScope.Matches(Admission.CreationId,Owner.Origin?.CreationId)||
                    liveOwner&&(!RebirthWorldCharacterService.TryGetIdentity(Player,out var identity)||identity==null||
                    identity.StorageKey!=Identity.StorageKey||identity.CanonicalId!=Identity.CanonicalId||
                    !RebirthWorldCharacterService.TryGet(Player,out var owner)||!ReferenceEquals(owner,Owner))||
                    !Publication.MatchesSaveScope(GameIO.GetSaveGameDir())||
                    !string.Equals(SaveRoot,System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()),
                        System.IO.Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))return false;
                var runtime=RebirthCamperModuleRuntime.For(Vehicle);
                return !runtime.TransferPending&&runtime.Identity==Address.Vehicle&&
                    RebirthCamperVehicleIdentity.IsUniqueCurrent(Vehicle,Address.Vehicle)&&
                    ReferenceEquals(runtime.Stations.Find(Address.Installation)?.State,Station)&&
                    (string)Admission.WritePaymentImage().Attribute("block")==Station.InstalledBlock.Block.GetBlockName()&&
                    Progression.StationPreparations.TryGetValue(Admission.JobId,out var admission)&&admission!=null&&
                    XNode.DeepEquals(admission.Write(),Admission.Write())&&
                    Progression.CamperStationPublications.TryGetValue(Admission.JobId,out var publication)&&publication!=null&&
                    XNode.DeepEquals(publication.Write(),Publication.Write())&&
                    !Progression.StationPublications.ContainsKey(Admission.JobId);
            }
            catch{return false;}
        }
        internal bool TryRefreshPlayer(EntityPlayer candidate)
        {
            try
            {
                if(!IsProcessingCurrent()||candidate==null||candidate.IsDead()||!ReferenceEquals(candidate.world,World)||
                    !ReferenceEquals(World.GetEntity(candidate.entityId),candidate)||
                    !RebirthWorldCharacterService.TryGetIdentity(candidate,out var identity)||identity==null||
                    identity.StorageKey!=Identity.StorageKey||identity.CanonicalId!=Identity.CanonicalId||
                    !RebirthWorldCharacterService.TryGet(candidate,out var owner)||!ReferenceEquals(owner,Owner))return false;
                Player=candidate;return IsCurrent();
            }
            catch{return false;}
        }
        internal bool TryRefreshOnlinePlayer()
        {
            if(IsCurrent())return true;
            if(!ThreadManager.IsMainThread()||World?.Players?.list==null)return false;
            foreach(var candidate in World.Players.list)if(TryRefreshPlayer(candidate))return true;
            return false;
        }
        private bool MatchesCompletionIntent()
        {
            if(completionIntent==null)return !Progression.StationTerminalIntents.ContainsKey(Admission.JobId);
            return Progression.StationTerminalIntents.TryGetValue(Admission.JobId,out var saved)&&saved!=null&&
                XNode.DeepEquals(saved.Write(),completionIntent.Write());
        }
        // Original completed/trained authority only. This stages a durable
        // completion choice; it grants no native reward or receipt removal.
        internal bool TryStageCompletionIntent(RebirthCamperStationTrainingPlan plan,out RebirthStationTerminalIntent intent)
        {
            intent=null;
            if(plan==null||!ReferenceEquals(plan.Binding,this)||!plan.IsCurrent()||
                !RebirthCamperStationTrainingRecord.TryCreate(plan,out var training)||
                !RebirthWorldCharacterRepository.HasSavedCamperStationTrainingPlan(Identity,Admission,Publication,
                    plan.Publication,training,plan.HasTraining)||
                !RebirthStationTerminalIntent.TryCreate(Admission,true,Player.entityId,out var requested))return false;
            if(completionIntent!=null)
            {
                if(!XNode.DeepEquals(completionIntent.Write(),requested.Write()))return false;
            }
            else
            {
                if(Progression.StationTerminalIntents.ContainsKey(Admission.JobId)||Progression.StationTerminalIntents.Count>=64)return false;
                Progression.StationTerminalIntents.Add(Admission.JobId,requested.Clone());
                completionIntent=requested.Clone();
            }
            intent=completionIntent.Clone();return IsCurrent();
        }
    }
    private static readonly ConditionalWeakTable<RebirthCamperStationState,Dictionary<string,Binding>> bindings
        =new ConditionalWeakTable<RebirthCamperStationState,Dictionary<string,Binding>>();
    internal static bool TryReleaseArchived(RebirthCamperStationState station,RebirthWorldCharacterRecord owner,
        RebirthStablePlayerIdentity identity,RebirthStationRefundArchive archive,Func<bool> savedTerminal)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||station==null||owner==null||identity==null||archive==null||savedTerminal==null||
                !archive.IsMobile||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
                !RebirthStationRefundArchive.TryRead(archive.Write(),identity.StorageKey,out var valid)||
                !RebirthStationGridAdmission.TryReadStored(valid.Write().Element("stationAdmission"),out var admission)||
                !admission.TryGetMobileAddress(out var address)||
                !RebirthCamperVehicleIdentity.IsUniqueCurrent(station.BoundVehicle,address.Vehicle)||
                !ReferenceEquals(RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(address.Installation)?.State,station)||
                owner.Progression.StationPreparations.ContainsKey(admission.JobId)||!savedTerminal()||
                !RebirthWorldCharacterRepository.HasSavedStationRefundArchive(identity,valid))return false;
            if(station.Queue==null||!RebirthStationCompletionReceipt.HasNoJobReceipt(station.CraftCompleteList,admission.JobId))return false;
            foreach(var row in station.Queue)
                if(row==null||RebirthStationGridQueue.IsMarked(row.Recipe)&&
                    (!RebirthStationGridQueue.TryGetJobId(row.Recipe,out var queuedJob)||queuedJob==admission.JobId))return false;
            if(!bindings.TryGetValue(station,out var records)||!records.TryGetValue(admission.JobId,out var original))return true;
            if(!ReferenceEquals(original.Owner,owner)||original.Identity.StorageKey!=identity.StorageKey||
                !XNode.DeepEquals(original.Admission.Write(),admission.Write())||
                !XNode.DeepEquals(original.Publication.Write(),valid.Write().Element("camperStationPublication"))||!savedTerminal())return false;
            return records.Remove(admission.JobId);
        }
        catch{return false;}
    }
    // Mobile queues have no terrain tile address. Every marked entry must use
    // its own original saved publication, never the ground-station registry.
    internal static bool CanProcessNativeQueue(RebirthCamperStationState station)
    {
        if(RebirthCamperStationCancellationRemoval.IsHeld(station))return false;
        if(station?.Queue==null)return true;
        foreach(var queued in station.Queue)
        {
            if(queued?.Recipe==null||!RebirthStationGridQueue.IsMarked(queued.Recipe))continue;
            if(!Rebirth.Vehicles.Camper.RebirthCamperModulePatchInstaller.IsInstalled||
                RebirthCamperStationCompletionCallsite.IsHeld(station)||
                RebirthCamperStationSaveEvidence.IsPending(station)||
                !RebirthStationGridQueue.HasValidMultiplier(queued.Recipe,queued.Multiplier)||
                !RebirthStationGridQueue.HasResolvedDefinition(queued.Recipe))return false;
            if(!TryGetCurrent(station,queued,out _))
            {RebirthCamperStationProcessingRecoveryWatch.TryObserve(station);return false;}
        }
        return true;
    }
    internal static bool TryRegister(EntityPlayer player,RebirthStationGridAdmission admission,
        RebirthCamperStationSaveEvidence.Publication proof)
    {
        if(!ThreadManager.IsMainThread()||admission==null||proof==null||!admission.TryGetMobileAddress(out var address)||
            !RebirthStationLiveAccess.TryResolveMobile(player,address,admission.CreationId,out var tile,out var owner)||
            !(tile is RebirthCamperStationState station)||
            !RebirthCamperStationPublicationRecord.TryCreate(admission,player.entityId,proof,out var publication)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||
            !RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,admission,publication)||
            !RebirthCamperQueuedPublicationMatch.MatchesStation(station,address,admission,player.entityId))return false;
        var candidate=new Binding(player,owner,identity,station,address,admission,publication);
        if(!candidate.IsCurrent())return false;
        var records=bindings.GetValue(station,_=>new Dictionary<string,Binding>(StringComparer.Ordinal));
        if(records.TryGetValue(admission.JobId,out var existing))
            return existing.TryRefreshPlayer(player)&&ReferenceEquals(existing.Owner,owner)&&
                XNode.DeepEquals(existing.Publication.Write(),publication.Write());
        if(records.Count>=64)return false;
        records.Add(admission.JobId,candidate);return true;
    }
    internal static bool TryGetCurrent(RebirthCamperStationState station,RecipeQueueItem queued,out Binding binding)
    {
        binding=null;
        if(!ThreadManager.IsMainThread()||station==null||queued?.Recipe==null||
            !RebirthStationGridQueue.TryGetJobId(queued.Recipe,out var job)||
            !bindings.TryGetValue(station,out var records)||!records.TryGetValue(job,out var retained)||
            !retained.IsProcessingCurrent()||queued.StartingEntityId!=retained.QueuedOwner||queued.RepairItem!=null||
            queued.AmountToRepair!=0||!retained.Admission.MatchesQueuedQuality(queued.Recipe,queued.Multiplier,queued.Quality))return false;
        int matches=0;
        if(station.Queue==null)return false;
        foreach(var entry in station.Queue)
            if(entry?.Recipe!=null&&RebirthStationGridQueue.TryGetJobId(entry.Recipe,out var otherJob)&&otherJob==job)
            {if(!ReferenceEquals(entry,queued))return false;matches++;}
        if(matches!=1)return false;
        binding=retained;return true;
    }
    // A fresh vehicle-save observation corroborates the original durable paid
    // publication; it must never replace that publication with new raw hashes.
    // Processing-only reload path. Native persisted identity lookup belongs to
    // its caller; no player, window, training or inventory permit is minted here.
    internal static bool TryRegisterProcessingRecovered(RebirthWorldCharacterRecord owner,RebirthStablePlayerIdentity identity,
        RebirthCamperStationState station,RebirthCamperStationAddress address,RebirthStationGridAdmission admission,
        RebirthCamperStationPublicationRecord original,RebirthCamperStationSaveEvidence.Publication proof)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||owner==null||!owner.IsComplete||identity==null||station?.BoundVehicle==null||
                admission==null||original==null||proof==null||owner.StablePlayerKey!=identity.StorageKey||
                !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
                !RebirthSurvivorRequestScope.Matches(admission.CreationId,owner.Origin?.CreationId)||
                !admission.TryGetMobileAddress(out var expected)||!expected.Equals(address)||
                !RebirthCamperStationPublicationRecord.TryRead(original.Write(),admission,out var publication)||
                !publication.MatchesSaveScope(GameIO.GetSaveGameDir())||
                !RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,admission,publication)||
                RebirthCamperStationCompletionCallsite.IsHeld(station)||RebirthCamperStationCancellationRemoval.IsHeld(station)||
                owner.Progression.CamperStationCompletions.ContainsKey(admission.JobId)||
                owner.Progression.CamperStationTrainingPlans.ContainsKey(admission.JobId)||
                owner.Progression.CamperStationCancellations.ContainsKey(admission.JobId)||
                owner.Progression.CamperStationRefundDeliveries.ContainsKey(admission.JobId)||
                owner.Progression.StationCompletionPublications.ContainsKey(admission.JobId)||
                owner.Progression.StationCompletionExpectationProjections.ContainsKey(admission.JobId)||
                owner.Progression.StationCancellationPublications.ContainsKey(admission.JobId)||
                owner.Progression.StationTerminalIntents.ContainsKey(admission.JobId)||
                owner.Progression.StationCancellationAttempts.ContainsKey(admission.JobId)||
                owner.Progression.StationCancellationRefunds.ContainsKey(admission.JobId)||
                owner.Progression.StationRefundDeliveries.ContainsKey(admission.JobId)||
                owner.Progression.StationRefundArchives.ContainsKey(admission.JobId))return false;
            var image=publication.Write();int queuedOwner=(int)image.Attribute("queuedOwner");
            if((int)image.Attribute("vehicleClass")!=proof.VehicleClass||proof.VehicleEntityId!=station.BoundVehicle.entityId||
                !RebirthCamperQueuedPublicationMatch.MatchesRestored(proof,admission,queuedOwner)||
                !RebirthCamperQueuedPublicationMatch.MatchesRestoredStation(station,address,admission,queuedOwner))return false;
            var candidate=new Binding(null,owner,identity,station,address,admission,publication);
            if(!candidate.IsProcessingCurrent())return false;
            var records=bindings.GetValue(station,_=>new Dictionary<string,Binding>(StringComparer.Ordinal));
            if(records.TryGetValue(admission.JobId,out var existing))
                return existing.IsProcessingCurrent()&&ReferenceEquals(existing.Owner,owner)&&
                    existing.Identity.StorageKey==identity.StorageKey&&existing.Identity.CanonicalId==identity.CanonicalId&&
                    XNode.DeepEquals(existing.Admission.Write(),admission.Write())&&
                    XNode.DeepEquals(existing.Publication.Write(),publication.Write());
            if(records.Count>=64)return false;
            records.Add(admission.JobId,candidate);return true;
        }
        catch{return false;}
    }
    internal static bool TryRegisterRecovered(EntityPlayer player,RebirthStationGridAdmission admission,
        RebirthCamperStationPublicationRecord original,RebirthCamperStationSaveEvidence.Publication proof)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||admission==null||original==null||proof==null||
                !admission.TryGetMobileAddress(out var address)||
                !RebirthCamperStationPublicationRecord.TryRead(original.Write(),admission,out var publication)||
                !publication.MatchesSaveScope(GameIO.GetSaveGameDir())||
                !RebirthStationLiveAccess.TryResolveMobile(player,address,admission.CreationId,out var tile,out var owner)||
                !(tile is RebirthCamperStationState station)||RebirthCamperStationCompletionCallsite.IsHeld(station)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||
                !RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,admission,publication))return false;
            var record=publication.Write();
            int queuedOwner=(int)record.Attribute("queuedOwner");
            if((int)record.Attribute("vehicleClass")!=proof.VehicleClass||
                proof.VehicleEntityId!=station.BoundVehicle.entityId||
                owner.Progression.CamperStationCompletions.ContainsKey(admission.JobId)||
                owner.Progression.CamperStationTrainingPlans.ContainsKey(admission.JobId)||
                !RebirthCamperQueuedPublicationMatch.MatchesRestored(proof,admission,queuedOwner)||
                !RebirthCamperQueuedPublicationMatch.MatchesRestoredStation(station,address,admission,queuedOwner))return false;
            var candidate=new Binding(player,owner,identity,station,address,admission,publication);
            if(!candidate.IsCurrent())return false;
            var records=bindings.GetValue(station,_=>new Dictionary<string,Binding>(StringComparer.Ordinal));
            if(records.TryGetValue(admission.JobId,out var existing))
                return existing.TryRefreshPlayer(player)&&ReferenceEquals(existing.Owner,owner)&&
                    XNode.DeepEquals(existing.Publication.Write(),publication.Write());
            if(records.Count>=64)return false;
            records.Add(admission.JobId,candidate);return true;
        }
        catch{return false;}
    }
}
