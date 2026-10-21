using System;
using System.Linq;

// Compatibility for queues changed by the earlier UI before admission finished.
// This accepts only progress on the SAME original recipe, never another job,
// owner, quantity, ingredient, output definition or admission/payment receipt.
internal static class RebirthCookingAdmissionProgress
{
    internal static bool TryMatch(string baseline,string observed,Guid operation,bool allowCommitted,out string paused)
    {
        paused=null;
        try
        {
            var original=RebirthCookingRemoteAdmissionBackend.ReadQueue(baseline);
            var current=RebirthCookingRemoteAdmissionBackend.ReadQueue(observed);
            var retained=RebirthCookingRemoteAdmissionBackend.ReadQueue(observed);
            if(original.Length!=current.Length)return false;
            int matched=0;
            for(int i=0;i<original.Length;i++)
            {
                var before=original[i];var live=current[i];
                if(before==null||live==null)return false;
                if(before.Recipe==null&&live.Recipe==null)
                {
                    if(before.RepairItem!=null&&!before.RepairItem.IsEmpty()||live.RepairItem!=null&&!live.RepairItem.IsEmpty()||
                        before.AmountToRepair!=0||live.AmountToRepair!=0)return false;
                    current[i]=before; // Empty-row display flags carry no job.
                    continue;
                }
                if(!RebirthCookingHeat.Managed(before.Recipe)||!RebirthCookingHeat.Managed(live.Recipe))continue;
                var expected=before.Recipe.ingredients[0].itemValue;
                var actual=live.Recipe.ingredients[0].itemValue;
                if(!expected.TryGetMetadata("rebirth.cooking.queue.token",out string token)||token!=operation.ToString("N"))continue;
                if(++matched!=1||!actual.TryGetMetadata("rebirth.cooking.queue.token",out string own)||own!=token||
                    !actual.TryGetMetadata(RebirthCookingRemoteAdmission.PendingKey,out int phase)||phase!=1&&(!allowCommitted||phase!=2))return false;
                if(!expected.TryGetMetadata("rebirth.cooking.queue.duration",out float duration)||!Finite(duration)||duration<0||
                    !actual.TryGetMetadata("rebirth.cooking.queue.elapsed",out float elapsed)||!Finite(elapsed)||elapsed<0||elapsed>duration||
                    !actual.TryGetMetadata("rebirth.cooking.queue.overdue",out float overdue)||!Finite(overdue)||overdue<0||
                    !Finite(live.CraftingTimeLeft)||live.CraftingTimeLeft<0||live.CraftingTimeLeft>before.CraftingTimeLeft)return false;
                if(actual.Metadata.ContainsKey("rebirth.cooking.queue.reported")&&
                    (!actual.TryGetMetadata("rebirth.cooking.queue.reported",out float reported)||reported!=0f&&reported!=1f))return false;
                foreach(string key in expected.Metadata.Keys.Concat(actual.Metadata.Keys).Distinct().ToArray())
                    if(key=="rebirth.cooking.queue.elapsed"||key=="rebirth.cooking.queue.overdue"||key=="rebirth.cooking.queue.reported"||
                        key.StartsWith("rebirth.cooking.delivery.",StringComparison.Ordinal))
                    {expected.Metadata.Remove(key);actual.Metadata.Remove(key);}
                actual.SetMetadata(RebirthCookingRemoteAdmission.PendingKey,1);
                retained[i].Recipe.ingredients[0].itemValue.SetMetadata(RebirthCookingRemoteAdmission.PendingKey,1);
                live.CraftingTimeLeft=before.CraftingTimeLeft;live.IsCrafting=before.IsCrafting;
            }
            if(matched!=1||RebirthCookingRemoteAdmissionBackend.Queue(original)!=RebirthCookingRemoteAdmissionBackend.Queue(current))return false;
            paused=RebirthCookingRemoteAdmissionBackend.Queue(retained);return true;
        }
        catch{return false;}
    }
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
}
