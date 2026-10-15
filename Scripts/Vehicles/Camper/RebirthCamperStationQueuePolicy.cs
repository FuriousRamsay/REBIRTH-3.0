using System;

internal static class RebirthCamperStationQueuePolicy
{
    // Paid recipes keep their historical native actor. Current policy is checked
    // against the independently authenticated live owner, never an ID lookup.
    internal static bool Authorize(RebirthCamperStationState station,out string reason)
    {
        reason="camper-queued-authority";
        try
        {
            if(!ThreadManager.IsMainThread()||station?.Queue==null||station.Queue.Length==0||
                !RebirthCamperStationQueuedAuthority.CanProcessNativeQueue(station))return false;
            var row=station.Queue[station.Queue.Length-1];
            if(!RebirthCamperStationQueuedAuthority.TryGetCurrent(station,row,out var binding)||
                !RebirthStationGridQueue.TryGetAdmittedSource(row.Recipe,XUiM_Recipes.GetRecipes(),out var source)||source==null)return false;
            if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()){reason=null;return true;}
            var evaluation=RebirthCapabilityService.EvaluateRecipe(binding.Player,source.GetName());
            if(evaluation.IsAllowed){reason=null;return true;}
            reason="crafting-policy:"+evaluation.FirstMissingReason;return false;
        }
        catch{return false;}
    }
}
