using System;

// Reconstructs live queue authority only after original owner publication and
// a new primary native vehicle save are both observed in the exact window lease.
internal sealed class RebirthCamperStationQueueRecovery : IDisposable
{
    private readonly RebirthCamperStationQueuedAuthority.Binding binding;
    private readonly object lease;
    private RebirthCamperStationSaveEvidence.Request request;
    private RebirthCamperStationSaveEvidence.Publication proof;
    private bool closed;
    private RebirthCamperStationRelocationRecovery relocation;
    private RebirthCamperStationQueueRecovery(RebirthCamperStationQueuedAuthority.Binding binding,object lease)
    {this.binding=binding;this.lease=lease;}
    private RebirthCamperStationQueueRecovery(RebirthCamperStationRelocationRecovery operation)
    {relocation=operation;}
    internal static bool TryGetRecoveryLease(EntityPlayer player,RebirthStationGridAdmission admission,
        out RebirthCamperStationState station,out RebirthCamperStationAddress address,out object lease)
    {
        station=null;address=default;lease=null;
        if(admission==null||!admission.TryGetMobileAddress(out var source))return false;
        if(RebirthStationLiveAccess.TryGetMobileLease(player,source,admission.CreationId,out station,out lease)&&lease!=null)
        {address=source;return true;}
        RebirthCamperStationAddress current;
        if(RebirthCamperStationWindowSession.TryGetCurrent(player,out _,out current)||
            RebirthCamperRemoteStationTransport.TryGetCurrent(player,out _,out current,out _,out _))
        {
            if(current.Installation!=source.Installation)return false;
            if(RebirthStationLiveAccess.TryGetMobileLease(player,current,admission.CreationId,out station,out lease)&&lease!=null)
            {address=current;return true;}
        }
        return false;
    }
    internal static bool TryBegin(EntityPlayer player,string job,out RebirthCamperStationQueueRecovery recovery)
    {
        recovery=null;
        try
        {
            if(!ThreadManager.IsMainThread()||string.IsNullOrEmpty(job)||
                !RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Progression==null||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||
                !owner.Progression.StationPreparations.TryGetValue(job,out var admission)||admission==null||
                !admission.TryGetMobileAddress(out var address)||
                !TryGetRecoveryLease(player,admission,out var station,out var currentAddress,out var lease)||lease==null||
                !owner.Progression.CamperStationPublications.TryGetValue(job,out var saved)||saved==null||
                !RebirthCamperStationPublicationRecord.TryRead(saved.Write(),admission,out var publication))return false;
            // Already-bound jobs need no new save or recovery operation.
            foreach(var entry in station.Queue)
                if(entry?.Recipe!=null&&RebirthStationGridQueue.TryGetJobId(entry.Recipe,out var entryJob)&&entryJob==job&&
                    RebirthCamperStationQueuedAuthority.TryGetCurrent(station,entry,out _))return false;
            if(!address.Equals(currentAddress)||!RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,admission,publication))
            {
                bool started=RebirthCamperStationRelocationRecovery.TryBegin(player,owner,identity,station,currentAddress,lease,admission,publication,out var relocated);
                if(relocated!=null)recovery=new RebirthCamperStationQueueRecovery(relocated);
                return started;
            }
            var candidate=new RebirthCamperStationQueueRecovery(
                new RebirthCamperStationQueuedAuthority.Binding(player,owner,identity,station,address,admission,publication),lease);
            if(!candidate.IsCurrent())return false;
            recovery=candidate;
            candidate.request=RebirthCamperStationSaveEvidence.BeginRecovery(player,station,address,candidate.IsCurrent);
            return candidate.request!=null;
        }
        catch{return false;}
    }
    private bool IsCurrent()
    {
        if(relocation!=null)return !closed&&relocation.CanContinue;
        try
        {
            return !closed&&binding.IsCurrent()&&
                RebirthStationLiveAccess.TryGetMobileLease(binding.Player,binding.Address,binding.Admission.CreationId,out var state,out var currentLease)&&ReferenceEquals(lease,currentLease)&&
                ReferenceEquals(state,binding.Station)&&
                !RebirthCamperStationCompletionCallsite.IsHeld(state)&&
                !binding.Progression.CamperStationCompletions.ContainsKey(binding.Admission.JobId)&&
                !binding.Progression.CamperStationTrainingPlans.ContainsKey(binding.Admission.JobId)&&
                RebirthWorldCharacterRepository.HasSavedCamperStationPublication(binding.Identity,binding.Admission,binding.Publication)&&
                RebirthCamperQueuedPublicationMatch.MatchesRestoredStation(state,binding.Address,binding.Admission,binding.QueuedOwner);
        }
        catch{return false;}
    }
    internal bool CanContinue=>relocation!=null?!closed&&relocation.CanContinue:IsCurrent()&&(proof!=null||request!=null&&!request.Closed);
    internal bool TryRecover()
    {
        if(relocation!=null)return !closed&&relocation.TryRecover();
        if(!IsCurrent())return false;
        if(proof==null)
        {
            if(!RebirthCamperStationSaveEvidence.TryObserve(request,out var observed))return false;
            proof=observed;
        }
        return IsCurrent()&&RebirthCamperStationQueuedAuthority.TryRegisterRecovered(binding.Player,binding.Admission,binding.Publication,proof);
    }
    public void Dispose()
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Release queue recovery on game thread.");
        if(closed)return;relocation?.Dispose();request?.Dispose();closed=true;
    }
}
