using System;
using System.Xml.Linq;

// Immutable proposed owner checkpoint only. This does not stage a choice in
// the owner record, remove a live row or authorize a refund.
internal sealed class RebirthCamperStationCancellationCheckpoint
{
    internal readonly RebirthCamperStationQueuedAuthority.Binding Binding;
    private readonly RebirthStationTerminalIntent intent;
    private readonly RebirthStationCancellationRefund refund;
    private readonly RebirthStationCancellationAttempt attempt;
    private readonly RebirthStationNativeQueueSnapshot before,after;
    private readonly RebirthStationTerminalSnapshot terminal;
    private readonly ItemStack installed;
    private readonly string stationBefore;
    private readonly object lease;
    private RebirthCamperStationCancellationCheckpoint(RebirthCamperStationQueuedAuthority.Binding binding,
        RebirthStationTerminalIntent intent,RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,
        RebirthStationNativeQueueSnapshot before,RebirthStationNativeQueueSnapshot after,RebirthStationTerminalSnapshot terminal,
        ItemStack installed,string stationBefore,object lease)
    {Binding=binding;this.intent=intent.Clone();this.refund=refund.Clone();this.attempt=attempt.Clone();this.before=before;this.after=after;this.terminal=terminal;this.installed=installed.Clone();this.stationBefore=stationBefore;this.lease=lease;}
    internal RebirthStationTerminalIntent CopyIntent()=>intent.Clone();
    internal RebirthStationCancellationRefund CopyRefund()=>refund.Clone();
    internal RebirthStationCancellationAttempt CopyAttempt()=>attempt.Clone();
    internal bool MatchesBefore()=>Binding.IsCurrent()&&MatchesScopeAndSnapshot();
    internal bool MatchesStagedBefore()
        =>MatchesStagedOwner()&&before.Matches(Binding.Station.Queue)&&terminal.Matches(Binding.Station)&&
            RebirthCamperStationRecoveryImage.Digest(Binding.Station,installed)==stationBefore;
    internal bool MatchesStagedOwner()
    {
        try
        {
            var progression=Binding.Progression;string job=Binding.Admission.JobId;
            return Binding.IsOwnerCurrent()&&MatchesScope()&&
                progression.StationTerminalIntents.TryGetValue(job,out var choice)&&choice!=null&&XNode.DeepEquals(choice.Write(),intent.Write())&&
                progression.StationCancellationRefunds.TryGetValue(job,out var obligation)&&obligation!=null&&XNode.DeepEquals(obligation.Write(),refund.Write())&&
                progression.StationCancellationAttempts.TryGetValue(job,out var pending)&&pending!=null&&XNode.DeepEquals(pending.Write(),attempt.Write())&&
                !progression.CamperStationCompletions.ContainsKey(job)&&!progression.CamperStationTrainingPlans.ContainsKey(job);
        }
        catch{return false;}
    }
    private bool MatchesScopeAndSnapshot()=>MatchesScope()&&
        before.Matches(Binding.Station.Queue)&&terminal.Matches(Binding.Station)&&
        RebirthCamperStationRecoveryImage.Digest(Binding.Station,installed)==stationBefore;
    private bool MatchesScope()=>
        RebirthStationLiveAccess.TryGetMobileLease(Binding.Player,Binding.Address,Binding.Admission.CreationId,out var leaseStation,out var currentLease)&&
        ReferenceEquals(leaseStation,Binding.Station)&&ReferenceEquals(lease,currentLease)&&
        RebirthStationLiveAccess.TryResolveMobile(Binding.Player,Binding.Address,Binding.Admission.CreationId,out var state,out var owner)&&
        ReferenceEquals(state,Binding.Station)&&ReferenceEquals(owner,Binding.Owner)&&
        RebirthWorldCharacterRepository.HasSavedCamperStationPublication(Binding.Identity,Binding.Admission,Binding.Publication);
    internal string StationDigest(RebirthCamperStationState state)=>RebirthCamperStationRecoveryImage.Digest(state,installed);
    internal bool TryBuildRemoval(out RecipeQueueItem[] replacement,out string digest)
    {
        replacement=null;digest=null;
        try
        {
            if(!MatchesStagedBefore()||!RebirthCamperStationCancellationQueuePlan.TryBuild(Binding.Station.Queue,
                Binding.Admission,intent,Binding.QueuedOwner,out var planned)||!after.Matches(planned))return false;
            var detached=RebirthCamperStationSnapshot.Restore(Binding.Address.Installation,installed.Clone(),
                RebirthCamperStationSnapshot.Capture(Binding.Address.Installation,Binding.Station));
            if(!TryBuildRemovalInput(out var input))return false;
            detached.Input=input;detached.Queue=planned;digest=StationDigest(detached);
            if(!MatchesStagedBefore())return false;
            replacement=planned;return true;
        }
        catch{return false;}
    }
    internal bool MatchesPlannedQueue(RecipeQueueItem[] candidate)=>after.Matches(candidate);
    internal bool TryBuildRemovalInput(out ItemStack[] input)
    {
        input=null;
        return MatchesStagedBefore()&&RebirthCamperStationCounterRefund.TryBuild(Binding.Station,refund,out input)&&MatchesStagedBefore();
    }
    internal static bool TryCreate(RebirthCamperStationQueuedAuthority.Binding binding,out RebirthCamperStationCancellationCheckpoint checkpoint)
    {
        checkpoint=null;
        try
        {
            if(!ThreadManager.IsMainThread()||binding==null||!binding.IsCurrent()||
                !RebirthStationLiveAccess.TryGetMobileLease(binding.Player,binding.Address,binding.Admission.CreationId,out var leaseStation,out var lease)||
                !ReferenceEquals(leaseStation,binding.Station)||
                !RebirthStationLiveAccess.TryResolveMobile(binding.Player,binding.Address,binding.Admission.CreationId,out var station,out var owner)||
                !ReferenceEquals(station,binding.Station)||!ReferenceEquals(owner,binding.Owner)||
                RebirthCamperStationCompletionCallsite.IsHeld(binding.Station)||
                RebirthCamperStationSaveEvidence.IsPending(binding.Station)||
                binding.Progression.CamperStationCompletions.ContainsKey(binding.Admission.JobId)||
                binding.Progression.CamperStationTrainingPlans.ContainsKey(binding.Admission.JobId)||
                !RebirthWorldCharacterRepository.HasSavedCamperStationPublication(binding.Identity,binding.Admission,binding.Publication)||
                !TryGetRemaining(binding,out var remaining)||
                !RebirthStationTerminalIntent.TryCreate(binding.Admission,false,binding.Player.entityId,remaining,out var intent)||
                !RebirthStationCancellationRefund.TryCreate(binding.Admission,XUiM_Recipes.GetRecipes(),intent,out var refund)||
                !RebirthCamperStationCancellationQueuePlan.TryBuild(binding.Station.Queue,binding.Admission,intent,binding.QueuedOwner,out var replacement)||
                !RebirthStationNativeQueueSnapshot.TryCapture(binding.Station.Queue,out var before)||
                !RebirthStationNativeQueueSnapshot.TryCapture(replacement,out var after)||
                !RebirthStationTerminalSnapshot.TryCapture(binding.Station,out var terminal)||
                !RebirthStationCompletionReceipt.HasNoJobReceipt(binding.Station.CraftCompleteList,binding.Admission.JobId)||
                !RebirthStationCancellationAttempt.TryCreate(binding.Admission,intent,refund,before,after,terminal,out var attempt))return false;
            var installed=RebirthCamperModuleRuntime.For(binding.Vehicle).Stations.Find(binding.Address.Installation)?.InstalledItem;
            if(installed==null)return false;
            string stationBefore=RebirthCamperStationRecoveryImage.Digest(binding.Station,installed);
            var proposed=new RebirthCamperStationCancellationCheckpoint(binding,intent,refund,attempt,before,after,terminal,installed,stationBefore,lease);
            if(!proposed.MatchesBefore())return false;
            checkpoint=proposed;return true;
        }
        catch{return false;}
    }
    private static bool TryGetRemaining(RebirthCamperStationQueuedAuthority.Binding binding,out int remaining)
    {
        remaining=0;
        if(binding?.Station.Queue==null)return false;
        foreach(var row in binding.Station.Queue)
            if(row?.Recipe!=null&&RebirthStationGridQueue.TryGetJobId(row.Recipe,out var job)&&job==binding.Admission.JobId)
            {if(remaining!=0||!binding.Admission.MatchesQueuedQuality(row.Recipe,row.Multiplier,row.Quality))return false;remaining=row.Multiplier;}
        return remaining>0;
    }
}
