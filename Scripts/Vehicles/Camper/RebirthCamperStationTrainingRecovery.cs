using System;
using System.Collections.Generic;
using System.Xml.Linq;

// Cold completion/training/native reward/receipt recovery. Caller retains the
// exact authenticated operation through native save confirmation.
internal sealed class RebirthCamperStationTrainingRecovery : IDisposable
{
    private readonly EntityPlayer player;
    private readonly RebirthWorldCharacterRecord owner;
    private readonly RebirthStablePlayerIdentity identity;
    private readonly RebirthCamperStationState station;
    private readonly RebirthCamperStationAddress address;
    private readonly ItemStack installed;
    private object lease;
    private readonly RebirthStationGridAdmission admission;
    private readonly RebirthCamperStationPublicationRecord queued;
    private readonly RebirthCamperStationCompletionRecord completed;
    private readonly RebirthCamperStationTrainingRecord training;
    private RebirthStationTerminalIntent completionIntent;
    private bool intentPending,nativeInvoked,nativeUncertain;
    private bool remoteAcknowledged,remoteCallback;
    private float nextNativeObservation;
    private RebirthCamperStationSaveEvidence.Request request;
    private RebirthCamperStationSaveEvidence.Publication proof;
    private bool closed;
    private bool retiring,retirementSaved;
    private string retirementDigest;
    private RebirthCamperStationSaveEvidence.Request retirementRequest;
    private RebirthCamperStationSaveEvidence.Publication retirementProof;

    private RebirthCamperStationTrainingRecovery(EntityPlayer player,RebirthWorldCharacterRecord owner,RebirthStablePlayerIdentity identity,
        RebirthCamperStationState station,RebirthCamperStationAddress address,ItemStack installed,object lease,RebirthStationGridAdmission admission,
        RebirthCamperStationPublicationRecord queued,RebirthCamperStationCompletionRecord completed,RebirthCamperStationTrainingRecord training,
        RebirthStationTerminalIntent completionIntent)
    {
        this.player=player;this.owner=owner;this.identity=identity;this.station=station;this.address=address;this.installed=installed.Clone();this.lease=lease;
        this.admission=admission.Clone();this.queued=queued.Clone();this.completed=completed.Clone();this.training=training.Clone();
        this.completionIntent=completionIntent?.Clone();
    }
    internal static bool TryBegin(EntityPlayer player,string job,out RebirthCamperStationTrainingRecovery recovery)
    {
        recovery=null;
        try
        {
            if(!ThreadManager.IsMainThread()||string.IsNullOrEmpty(job)||
                !RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Progression==null||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null)return false;
            var progression=owner.Progression;
            if(!progression.StationPreparations.TryGetValue(job,out var admitted)||admitted==null||
                !admitted.TryGetMobileAddress(out var address)||
                !RebirthStationLiveAccess.TryGetMobileLease(player,address,admitted.CreationId,out var state,out var lease))return false;
            if(RebirthCamperStationCompletionCallsite.TryGetRecoveryHold(state,out var held))
            {
                if(held.admission.JobId!=job||!held.TryResume(player,lease,address))return false;
                recovery=held;return true;
            }
            if(!progression.StationPreparations.TryGetValue(job,out var admission)||admission==null||
                !admission.TryGetMobileAddress(out var expected)||!address.Equals(expected)||
                !progression.CamperStationPublications.TryGetValue(job,out var queued)||queued==null||
                !progression.CamperStationCompletions.TryGetValue(job,out var completed)||completed==null||!completed.HasRecoveryImage||
                !progression.CamperStationTrainingPlans.TryGetValue(job,out var training)||training==null||
                !RebirthCamperStationTrainingRecord.TryRead(training.Write(),admission,queued,completed,out _)||
                RebirthCamperStationCompletionCallsite.TryGetSuccessfulOutput(state,job,out _))return false;
            var installed=RebirthCamperModuleRuntime.For(state.BoundVehicle).Stations.Find(address.Installation)?.InstalledItem;
            if(installed==null)return false;
            RebirthStationTerminalIntent choice=null;
            if(progression.StationTerminalIntents.TryGetValue(job,out var storedChoice)&&
                (storedChoice==null||!RebirthStationTerminalIntent.TryRead(storedChoice.Write(),admission,out choice)||!choice.IsCompletion))return false;
            var candidate=new RebirthCamperStationTrainingRecovery(player,owner,identity,state,address,installed,lease,admission,queued,completed,training,choice);
            if(!candidate.IsCurrent())return false;
            recovery=candidate;
            candidate.request=RebirthCamperStationSaveEvidence.BeginRecovery(player,state,address,candidate.IsCurrent);
            return true;
        }
        catch{return false;}
    }
    private bool IsCurrent()
    {
        try
        {
            if(closed||!ThreadManager.IsMainThread()||
                !RebirthCamperVehicleIdentity.IsUniqueCurrent(station.BoundVehicle,address.Vehicle)||
                !RebirthStationLiveAccess.TryGetMobileLease(player,address,admission.CreationId,out var leased,out var currentLease)||
                !ReferenceEquals(leased,station)||!ReferenceEquals(lease,currentLease)||
                !RebirthStationLiveAccess.TryResolveMobile(player,address,admission.CreationId,out var live,out var currentOwner)||
                !ReferenceEquals(live,station)||!ReferenceEquals(currentOwner,owner)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var currentIdentity)||currentIdentity==null||
                currentIdentity.StorageKey!=identity.StorageKey||currentIdentity.CanonicalId!=identity.CanonicalId)return false;
            var progression=owner.Progression;string job=admission.JobId;
            if(!progression.StationPreparations.TryGetValue(job,out var prepared)||prepared==null||!XNode.DeepEquals(prepared.Write(),admission.Write())||
                !progression.CamperStationPublications.TryGetValue(job,out var published)||published==null||!XNode.DeepEquals(published.Write(),queued.Write())||
                !progression.CamperStationCompletions.TryGetValue(job,out var completion)||completion==null||!XNode.DeepEquals(completion.Write(),completed.Write())||
                !progression.CamperStationTrainingPlans.TryGetValue(job,out var plan)||plan==null||!XNode.DeepEquals(plan.Write(),training.Write())||
                progression.StationPublications.ContainsKey(job)||!MatchesCompletionIntent()||
                progression.StationCancellationAttempts.ContainsKey(job)||progression.StationCancellationRefunds.ContainsKey(job)||
                progression.StationCompletionPublications.ContainsKey(job)||progression.StationCompletionExpectationProjections.ContainsKey(job)||
                progression.StationCancellationPublications.ContainsKey(job)||station.Queue==null)return false;
            foreach(var entry in station.Queue)
                if(entry==null||RebirthStationGridQueue.IsMarked(entry.Recipe)&&
                    (!RebirthStationGridQueue.TryGetJobId(entry.Recipe,out var queuedJob)||queuedJob==job))return false;
            return (retiring?RebirthCamperStationCompletionCallsite.HasRecoveryHold(station,this):RebirthCamperStationCompletionCallsite.IsHeld(station))&&
                RebirthCamperStationRecoveryImage.Digest(station,installed)==(retiring?retirementDigest:(string)completed.Write().Attribute("stationRecovery"))&&
                RebirthWorldCharacterRepository.HasSavedCamperStationTrainingPlan(identity,admission,queued,completed,training,
                    retiring&&training.RestoreInputs().HasTraining)&&
                (!retiring||RebirthCamperStationNativeRewardReceipt.HasSaved(identity,admission,true));
        }
        catch{return false;}
    }
    private bool MatchesCompletionIntent()
    {
        var intents=owner.Progression.StationTerminalIntents;
        if(completionIntent==null)return !intents.ContainsKey(admission.JobId);
        return completionIntent.IsCompletion&&intents.TryGetValue(admission.JobId,out var current)&&current!=null&&
            XNode.DeepEquals(current.Write(),completionIntent.Write())&&
            RebirthStationTerminalIntent.TryRead(current.Write(),admission,out var valid)&&valid.IsCompletion&&
            (intentPending||RebirthWorldCharacterRepository.HasSavedStationTerminalIntent(identity,admission,completionIntent));
    }
    internal bool CanContinue=>IsCurrent()&&(proof!=null||request!=null&&!request.Closed);
    internal bool TryGrant()
    {
        try
        {
            if(!IsCurrent())return false;
            if(retiring)return TryConfirmRetirement();
            if(proof==null)
            {
                if(!RebirthCamperStationSaveEvidence.TryObserve(request,out var observed))return false;
                proof=observed;
            }
            if(!completed.MatchesRecoveryImage(proof,queued)||!IsCurrent())return false;
            var inputs=training.RestoreInputs();
            if(inputs.HasTraining&&(!RebirthSkillAwardService.TryAwardCraft(player,inputs.Skill,inputs.Model,inputs.Cycles,inputs.PreparationMultiplier,
                inputs.Source,inputs.Receipt)||!IsCurrent()||
                !RebirthWorldCharacterRepository.HasSavedCamperStationTrainingPlan(identity,admission,queued,completed,training,true)))return false;
            if(!TrySaveCompletionChoice()||!TryNativeReward())return false;
            TryBeginRetirement();return false;
        }
        catch{return false;}
    }
    private bool TrySaveCompletionChoice()
    {
        if(!IsCurrent())return false;
        if(completionIntent==null)
        {
            if(owner.Progression.StationTerminalIntents.Count>=64||
                !RebirthStationTerminalIntent.TryCreate(admission,true,player.entityId,out var choice))return false;
            owner.Progression.StationTerminalIntents.Add(admission.JobId,choice.Clone());
            completionIntent=choice.Clone();intentPending=true;
        }
        if(intentPending)
        {
            RebirthWorldCharacterService.MarkDirty(owner,"camper-cold-completion-intent");
            if(!IsCurrent()||!RebirthWorldCharacterService.FlushPlayer(player,"camper-cold-completion-intent")||!IsCurrent()||
                !RebirthWorldCharacterRepository.HasSavedStationTerminalIntent(identity,admission,completionIntent))return false;
            intentPending=false;
        }
        return IsCurrent();
    }
    private bool TryNativeReward()
    {
        if(nativeUncertain||intentPending||!IsCurrent()||player.Buffs==null||
            RebirthCharacterCreationHoldService.IsHeld(player))return false;
        var local=player as EntityPlayerLocal;
        if(local!=null&&!ReferenceEquals(player,GameManager.Instance?.World?.GetPrimaryPlayer()))return false;
        float now=UnityEngine.Time.realtimeSinceStartup;if(now<nextNativeObservation)return false;nextNativeObservation=now+1f;
        string key=RebirthCamperStationNativeRewardReceipt.Key(admission);
        if(!RebirthCamperStationNativeRewardReceipt.TryReadSaved(identity,admission,out var savedPhase))return false;
        float livePhase=player.Buffs.GetCustomVar(key);
        if(livePhase==0f&&savedPhase==0f&&!nativeInvoked)
        {
            CraftCompleteData original=null;
            foreach(var row in station.CraftCompleteList)
                if(RebirthStationCompletionReceipt.TryReadIdentity(row,out var receipt)&&receipt.Job==admission.JobId)
                {if(original!=null)return false;original=row;}
            if(original==null||!IsCurrent())return false;
            var stack=original.CraftedItemStack.Clone();var remove=new List<string>();
            if(stack.itemValue.Metadata!=null)
                foreach(var name in stack.itemValue.Metadata.Keys)
                    if(name!=null&&(name.StartsWith(RebirthStationCompletionReceipt.Prefix,StringComparison.Ordinal)||name=="rebirth.cooking.token"))remove.Add(name);
            foreach(var name in remove)stack.itemValue.Metadata.Remove(name);
            var reward=new CraftCompleteData(player.entityId,stack,original.RecipeName,original.ItemScrapped,original.CraftExpGain,original.RecipeUsedCount);
            if(!RebirthCamperStationCompletionCapture.TryAggregateReward(admission,reward,out reward))return false;
            if(local==null)
            {
                if(player.world==null||player.world.IsRemote()||!RebirthCamperRemoteOwnerApplication.IsRegistered||
                    !RebirthCamperStationRemoteRewardPayload.TryCreate(admission,player.entityId,reward,out var payload)||!IsCurrent())return false;
                nativeInvoked=true;
                if(!RebirthCamperRemoteOwnerApplication.SendOwnerApplication(player,Guid.ParseExact(admission.JobId,"N"),
                    RebirthCamperRemoteOwnerEffectKind.Reward,payload,acknowledged=>
                    {
                        if(remoteCallback)return;remoteCallback=true;
                        remoteAcknowledged=acknowledged&&IsCurrent();if(!remoteAcknowledged)nativeUncertain=true;
                    }))nativeUncertain=true;
                return false;
            }
            nativeInvoked=true;
            try
            {
                player.Buffs.SetCustomVar(key,2f,true);
                if(player.Buffs.GetCustomVar(key)!=2f||!IsCurrent()){nativeUncertain=true;return false;}
                local.GiveExp(reward);
                if(!IsCurrent()){nativeUncertain=true;return false;}
                player.Buffs.SetCustomVar(key,1f,true);livePhase=player.Buffs.GetCustomVar(key);
            }
            catch{nativeUncertain=true;return false;}
        }
        if(livePhase!=1f)return false;
        if(local!=null)GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
        else if(nativeInvoked&&!remoteAcknowledged)return false;
        return IsCurrent()&&RebirthCamperStationNativeRewardReceipt.HasSaved(identity,admission,true)&&
            player.Buffs.GetCustomVar(key)==1f&&IsCurrent();
    }
    private void TryBeginRetirement()
    {
        if(retiring||!IsCurrent()||!RebirthCamperStationNativeRewardReceipt.HasSaved(identity,admission,true))return;
        var rows=new List<CraftCompleteData>(station.CraftCompleteList);int index=-1;
        for(int i=0;i<rows.Count;i++)
            if(RebirthStationCompletionReceipt.TryReadIdentity(rows[i],out var receipt)&&receipt.Job==admission.JobId)
            {if(index!=-1)return;index=i;}
        if(index<0)return;
        var detached=RebirthCamperStationSnapshot.Restore(address.Installation,installed.Clone(),
            RebirthCamperStationSnapshot.Capture(address.Installation,station));
        detached.CraftCompleteList.RemoveAt(index);
        string expected=RebirthCamperStationRecoveryImage.Digest(detached,installed);
        if(!IsCurrent()||!RebirthCamperStationCompletionCallsite.TryHoldRecovery(station,this))return;
        rows.RemoveAt(index);retirementDigest=expected;station.CraftCompleteList=rows;retiring=true;station.SetModified();
        retirementRequest=RebirthCamperStationSaveEvidence.BeginRetirement(station,address,IsCurrent);
    }
    private bool TryConfirmRetirement()
    {
        if(!IsCurrent())return false;
        if(retirementProof==null)
        {
            if(retirementRequest==null)retirementRequest=RebirthCamperStationSaveEvidence.BeginRetirement(station,address,IsCurrent);
            if(!RebirthCamperStationSaveEvidence.TryObserve(retirementRequest,out var observed))return false;
            retirementProof=observed;
        }
        if(!retirementProof.Address.Equals(address)||retirementProof.RecoveryDigest()!=retirementDigest||!IsCurrent())return false;
        retirementSaved=true;
        return RebirthCamperStationCompletionCallsite.TryReleaseRecovery(station,this);
    }
    private bool TryResume(EntityPlayer currentPlayer,object currentLease,RebirthCamperStationAddress currentAddress)
    {
        if(!ThreadManager.IsMainThread()||!closed||!retiring||!ReferenceEquals(currentPlayer,player)||
            currentLease==null||!currentAddress.Equals(address))return false;
        lease=currentLease;closed=false;retirementRequest=null;
        if(IsCurrent())return true;closed=true;return false;
    }
    internal bool IsRetirementSavedFor(RebirthCamperStationState expected)
        =>retirementSaved&&ReferenceEquals(expected,station)&&retirementProof!=null&&IsCurrent();
    internal bool HasRetired=>retirementSaved;
    public void Dispose()
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Release recovery on the game thread.");
        if(closed)return;retirementRequest?.Dispose();request?.Dispose();closed=true;
    }
}
