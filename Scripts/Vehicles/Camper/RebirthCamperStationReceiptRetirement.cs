using System;
using System.Collections.Generic;
using System.Xml.Linq;

// Remove only the original rewarded receipt. Keep the station held until the
// primary native vehicle file confirms that exact post-removal snapshot.
internal sealed class RebirthCamperStationReceiptRetirement : IDisposable
{
    private readonly RebirthCamperStationTrainingPlan plan;
    private readonly RebirthCamperStationNativeRewardDelivery delivery;
    private readonly RebirthCamperStationTrainingRecord training;
    private RebirthCamperStationSaveEvidence.Request request;
    private bool removed,closed,saved;
    private RebirthCamperStationReceiptRetirement(RebirthCamperStationTrainingPlan plan,RebirthCamperStationNativeRewardDelivery delivery,
        RebirthCamperStationTrainingRecord training)
    {this.plan=plan;this.delivery=delivery;this.training=training.Clone();}
    internal static bool TryBegin(RebirthCamperStationTrainingPlan plan,RebirthCamperStationNativeRewardDelivery delivery,
        out RebirthCamperStationReceiptRetirement retirement)
    {
        retirement=null;
        if(!ThreadManager.IsMainThread()||plan==null||delivery==null||!plan.IsCurrent()||!delivery.IsConfirmedFor(plan)||
            !RebirthCamperStationTrainingRecord.TryCreate(plan,out var training))return false;
        var candidate=new RebirthCamperStationReceiptRetirement(plan,delivery,training);
        // Retain this operation before mutation, including uncertain save failure.
        retirement=candidate;
        try
        {
            var station=plan.Binding.Station;
            var rows=new List<CraftCompleteData>(station.CraftCompleteList);int index=-1;
            for(int i=0;i<rows.Count;i++)
                if(RebirthStationCompletionReceipt.TryReadIdentity(rows[i],out var identity)&&identity.Job==plan.Binding.Admission.JobId)
                {if(index!=-1)return false;index=i;}
            if(index<0||!plan.IsCurrent()||!delivery.IsConfirmedFor(plan))return false;
            rows.RemoveAt(index);station.CraftCompleteList=rows;candidate.removed=true;station.SetModified();
            candidate.request=RebirthCamperStationSaveEvidence.BeginRetirement(plan.Binding.Station,plan.Binding.Address,candidate.IsCurrent);
            return true;
        }
        catch{return false;}
    }
    internal bool IsCurrent()
        =>!closed&&ThreadManager.IsMainThread()&&removed&&plan.Binding.IsCurrent()&&delivery.IsConfirmedFor(plan)&&
            plan.Completed.MatchesRetiredOutput(plan.Binding.Station)&&
            plan.Binding.Progression.CamperStationTrainingPlans.TryGetValue(plan.Binding.Admission.JobId,out var currentTraining)&&
            currentTraining!=null&&XNode.DeepEquals(currentTraining.Write(),training.Write())&&
            RebirthWorldCharacterRepository.HasSavedCamperStationTrainingPlan(plan.Binding.Identity,plan.Binding.Admission,
                plan.Binding.Publication,plan.Publication,training,plan.HasTraining)&&
            plan.Binding.Progression.StationTerminalIntents.TryGetValue(plan.Binding.Admission.JobId,out var choice)&&choice!=null&&
            RebirthWorldCharacterRepository.HasSavedStationTerminalIntent(plan.Binding.Identity,plan.Binding.Admission,choice);
    internal bool TryConfirm()
    {
        try
        {
            if(!IsCurrent())return false;
            if(request==null)request=RebirthCamperStationSaveEvidence.BeginRetirement(plan.Binding.Station,plan.Binding.Address,IsCurrent);
            if(!RebirthCamperStationSaveEvidence.TryObserve(request,out var proof))return false;
            if(!plan.Completed.MatchesRetiredOutput(proof.RestoreStationSnapshot())||!IsCurrent())return false;
            saved=true;
            return RebirthCamperStationCompletionCallsite.TryReleaseRetired(plan.Completed,this);
        }
        catch{return false;}
    }
    internal bool IsSavedFor(RebirthCamperStationCompletionCapture.SuccessfulOutput completed)
        =>saved&&ReferenceEquals(completed,plan.Completed)&&IsCurrent();
    public void Dispose(){request?.Dispose();closed=true;}
}
