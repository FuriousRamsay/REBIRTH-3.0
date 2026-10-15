using System;
using System.Xml.Linq;

// Persist the original frozen training inputs before any reward call. A failed
// save leaves custody intact; retrying this stage does not grant training.
internal static class RebirthCamperStationTrainingService
{
    // Original-event path only. Cold recovery must independently revalidate
    // durable completion custody before constructing its own award authority.
    internal static bool TryGrant(RebirthCamperStationTrainingPlan plan)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||plan==null||!TryRecord(plan)||!plan.IsCurrent()||
                !RebirthCamperStationTrainingRecord.TryCreate(plan,out var expected))return false;
            var binding=plan.Binding;
            if(!binding.Progression.CamperStationTrainingPlans.TryGetValue(binding.Admission.JobId,out var stored)||
                stored==null||!XNode.DeepEquals(stored.Write(),expected.Write())||
                !RebirthWorldCharacterRepository.HasSavedCamperStationTrainingPlan(binding.Identity,binding.Admission,binding.Publication,plan.Publication,stored))return false;
            // Unclassified recipes deliberately have no skill award. Their
            // saved plan remains the explicit no-training decision.
            if(!plan.HasTraining)return plan.IsCurrent();
            if(!RebirthSkillAwardService.TryAwardCraft(binding.Player,plan.Skill,plan.Model,plan.Cycles,
                plan.PreparationMultiplier,plan.Source,plan.Receipt)||!plan.IsCurrent())return false;
            return RebirthWorldCharacterRepository.HasSavedCamperStationTrainingPlan(binding.Identity,binding.Admission,
                binding.Publication,plan.Publication,stored,true)&&plan.IsCurrent();
        }
        catch{return false;}
    }
    internal static bool TryRecord(RebirthCamperStationTrainingPlan plan)
    {
        if(!ThreadManager.IsMainThread()||plan==null||!RebirthCamperStationTrainingRecord.TryCreate(plan,out var record))return false;
        var binding=plan.Binding;var progression=binding.Progression;string job=binding.Admission.JobId;
        bool staged=false;
        Func<bool> current=()=>plan.IsCurrent()&&
            (progression.CamperStationTrainingPlans.TryGetValue(job,out var retained)
                ?retained!=null&&XNode.DeepEquals(retained.Write(),record.Write()):!staged);
        try
        {
            if(!current())return false;
            if(progression.CamperStationTrainingPlans.TryGetValue(job,out var existing))
            {if(existing==null||!XNode.DeepEquals(existing.Write(),record.Write()))return false;}
            else progression.CamperStationTrainingPlans.Add(job,record.Clone());
            staged=true;
            RebirthWorldCharacterService.MarkDirty(binding.Owner,"camper-station-training-plan");
            return current()&&RebirthWorldCharacterService.FlushPlayer(binding.Player,"camper-station-training-plan")&&current()&&
                RebirthWorldCharacterRepository.HasSavedCamperStationTrainingPlan(binding.Identity,binding.Admission,binding.Publication,plan.Publication,record)&&current();
        }
        catch{return false;}
    }
}
