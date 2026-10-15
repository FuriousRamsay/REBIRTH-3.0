using System;

// Save the owner's terminal choice after output and training are confirmed.
// Success is not native XP delivery, receipt deletion or station release.
internal static class RebirthCamperStationCompletionIntentService
{
    internal static bool TrySave(RebirthCamperStationTrainingPlan plan)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||plan==null||
                !plan.Binding.TryStageCompletionIntent(plan,out var intent))return false;
            var binding=plan.Binding;
            RebirthWorldCharacterService.MarkDirty(binding.Owner,"camper-station-completion-intent");
            return plan.IsCurrent()&&RebirthWorldCharacterService.FlushPlayer(binding.Player,"camper-station-completion-intent")&&
                plan.IsCurrent()&&RebirthWorldCharacterRepository.HasSavedStationTerminalIntent(binding.Identity,binding.Admission,intent)&&
                plan.IsCurrent();
        }
        catch{return false;}
    }
}
