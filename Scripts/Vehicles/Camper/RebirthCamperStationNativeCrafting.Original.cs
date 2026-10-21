using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class RebirthCamperStationNativeCrafting
{
    // Reused by the actual cancellation planner and the cold original decoder.
    // Inputs and refunds here are detached; no owner inventory or drop fallback.
    private static bool TryBuildStationInputRefund(RebirthCamperStationState station,List<ItemStack> refunds,out ItemStack[] input)
    {
        input=null;
        if(station?.Input==null||station.InputSlotCount<0||station.InputSlotCount>station.Input.Length||refunds==null||refunds.Count<1||refunds.Count>9)return false;
        var next=station.Input.Select(s=>s?.Clone()).ToArray();
        foreach(var refund in refunds)
        {
            if(refund?.itemValue==null||refund.count<1||refund.itemValue.HasMetadata(ActorSourceKey))return false;
            if(refund.itemValue.HasMetadata(MaterialKey))
            {
                if(!refund.itemValue.TryGetMetadata(MaterialKey,out string name)||!refund.itemValue.TryGetMetadata(SlotKey,out int slot)||slot<station.InputSlotCount||slot>=next.Length||
                    station.MaterialNames==null||slot-station.InputSlotCount>=station.MaterialNames.Length||name!="unit_"+station.MaterialNames[slot-station.InputSlotCount]||next[slot]?.itemValue==null||
                    next[slot].itemValue.type!=refund.itemValue.type||next[slot].count<0||(long)next[slot].count+refund.count>int.MaxValue)return false;
                next[slot].count+=refund.count;continue;
            }
            var cells=new ItemStack[station.InputSlotCount];Array.Copy(next,cells,cells.Length);
            int maximum=refund.itemValue.ItemClass?.MaxCount??0;
            if(maximum<1||ItemStack.AddToItemStackArray(cells,refund.Clone(),maximum)==-1)return false;
            Array.Copy(cells,next,cells.Length);
        }
        input=next;return true;
    }
    private static bool TryBuildStationInputDebit(RebirthCamperStationState before,RecipeQueueItem row,out ItemStack[] input)
    {
        input=null;
        if(before?.Input==null||before.InputSlotCount<0||before.InputSlotCount>before.Input.Length||row?.Recipe?.ingredients==null||row.Recipe.ingredients.Any(f=>f?.itemValue==null||f.itemValue.HasMetadata(ActorSourceKey))||
            !row.Recipe.ingredients[0].itemValue.TryGetMetadata(BatchesKey,out int batches)||batches!=row.Multiplier||!TryRemainingRefund(row,out var paid))return false;
        var next=ItemStack.Clone(before.Input);
        foreach(var fragment in paid)
        {
            if(fragment?.itemValue==null||fragment.count<1)return false;
            if(fragment.itemValue.HasMetadata(MaterialKey))
            {
                if(!fragment.itemValue.TryGetMetadata(MaterialKey,out string name)||!fragment.itemValue.TryGetMetadata(SlotKey,out int slot)||slot<before.InputSlotCount||slot>=next.Length||
                    before.MaterialNames==null||slot-before.InputSlotCount>=before.MaterialNames.Length||name!="unit_"+before.MaterialNames[slot-before.InputSlotCount]||next[slot]==null||next[slot].count<fragment.count)return false;
                var clean=fragment.Clone();clean.itemValue.Metadata.Remove(UnitsKey);clean.itemValue.Metadata.Remove(MaterialKey);clean.itemValue.Metadata.Remove(SlotKey);
                var source=next[slot].Clone();source.count=fragment.count;
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(source,clean))return false;
                next[slot].count-=fragment.count;continue;
            }
            int remaining=fragment.count;
            for(int slot=0;slot<before.InputSlotCount&&remaining>0;slot++)
            {
                var source=next[slot];if(source==null||source.IsEmpty()||source.itemValue.HasModSlots&&source.itemValue.HasMods())continue;
                int take=Math.Min(remaining,source.count);var expected=fragment.Clone();expected.count=take;var candidate=source.Clone();candidate.count=take;
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(candidate,expected))continue;
                remaining-=take;source.count-=take;if(source.count==0)next[slot]=ItemStack.Empty.Clone();
            }
            if(remaining!=0)return false;
        }
        input=next;return true;
    }
    // Positive exact delta proof from immutable paid fragments, not a new recipe
    // lookup or a replay of current character modifiers during cold recovery.
    internal static bool ValidateStationInputOriginal(RebirthCamperStationState before,RebirthCamperStationState after,RebirthCamperRemoteStationCommand command)
    {
        try
        {
            if(before==null||after==null||command==null||before.InstallationId!=after.InstallationId||before.Queue==null||after.Queue==null||before.Queue.Length!=after.Queue.Length||before.Input==null||after.Input==null||before.Input.Length!=after.Input.Length)return false;
            int slot=-1;ItemStack[] input;var queue=(RecipeQueueItem[])before.Queue.Clone();
            if(command.Kind==RebirthCamperRemoteStationCommandKind.Craft)
            {
                if(command.Operation!=command.Job||!RebirthStationNativeQueueEntry.TryFindVacantSlot(before.Queue,command.Job,out slot))return false;
                foreach(var row in before.Queue)if(TryJob(row?.Recipe,out var job)&&job==command.Job)return false;
                var next=after.Queue[slot];
                if(next?.Recipe==null||next.RepairItem!=null||next.AmountToRepair!=0||next.Recipe.IsScrap||RebirthStationGridQueue.IsMarked(next.Recipe)||RebirthRemoteResourceFetchedStationOriginal.IsCancellationCandidate(next.Recipe)||
                    !TryJob(next.Recipe,out var jobId)||jobId!=command.Job||next.Multiplier!=command.Batches||next.Quality!=command.Tier||next.Recipe.GetName()!=command.RecipeName||
                    float.IsNaN(next.OneItemCraftTime)||float.IsInfinity(next.OneItemCraftTime)||next.OneItemCraftTime<0||next.CraftingTimeLeft!=next.OneItemCraftTime||
                    !TryBuildStationInputDebit(before,next,out input))return false;
                queue[slot]=next;
            }
            else if(command.Kind==RebirthCamperRemoteStationCommandKind.Cancel)
            {
                if(command.RecipeName.Length!=0||command.Batches!=0||command.Tier!=0)return false;
                for(int i=0;i<before.Queue.Length;i++)if(TryJob(before.Queue[i]?.Recipe,out var job)&&job==command.Job){if(slot>=0)return false;slot=i;}
                if(slot<0)return false;var row=before.Queue[slot];
                if(row.RepairItem!=null||row.AmountToRepair!=0||RebirthStationGridQueue.IsMarked(row.Recipe)||RebirthRemoteResourceFetchedStationOriginal.IsCancellationCandidate(row.Recipe)||
                    row.Recipe.ingredients.Any(f=>f?.itemValue==null||f.itemValue.HasMetadata(ActorSourceKey))||!TryRemainingRefund(row,out var refunds)||!TryBuildStationInputRefund(before,refunds,out input))return false;
                queue[slot]=new RecipeQueueItem{StartingEntityId=-1};
            }
            else return false;
            // Build only the allowed fields on a detached copy of Before. Full
            // snapshot equality also checks every unrelated row/timer/output.
            if(!SameCells(input,after.Input))return false;
            // Queue and all other fields are qualified by the caller's complete
            // before-derived candidate comparison, with this one changed row.
            var originalInput=after.Input;var originalQueue=after.Queue;
            try
            {
                after.Input=input;after.Queue=queue;
                byte[] expected=RebirthCamperStationSnapshot.Capture(after.InstallationId,after);
                after.Input=originalInput;after.Queue=originalQueue;
                return expected.SequenceEqual(RebirthCamperStationSnapshot.Capture(after.InstallationId,after));
            }
            finally{after.Input=originalInput;after.Queue=originalQueue;}
        }
        catch{return false;}
    }
    private static bool SameCells(ItemStack[] a,ItemStack[] b)
    {if(a==null||b==null||a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(a[i],b[i]))return false;return true;}
}
