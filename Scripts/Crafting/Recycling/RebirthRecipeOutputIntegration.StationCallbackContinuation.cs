using System;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private static bool CanAdvancePendingStationOutput(XUiC_RecipeStack card)
    {
        if(!pendingUiOutputs.TryGetValue(card,out var pending)||pending.Source?.HasStationGrid!=true)return true;
        var original=pending.Source;var scope=original.Scope;
        if(ui!=null||pending.QueueObserved||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty())return false;
        var previous=ui;ui=scope;
        try
        {
            // An entered SetSlots/UpdateData without both exact native grids
            // is retained unknown; never publish another combined grid here.
            if(!original.Current()||original.OutputEntered||!original.CallbackPhysicalComplete||!original.CallbackOwnerCurrent()||!CanInsertOutputs(original.Actor))return false;
            // The actual native cached-grid branch has no quest/cosmetic or
            // completion-sound callback. Its accepted grid needs only native
            // queue bookkeeping, never the inventory helper's extra callback.
            if(scope.Cached)
            {
                var ready=new XElement("recipeOutputCachedGridQueueReady",new XAttribute("operation",original.Operation.ToString("N")),
                    new XAttribute("original",UiOriginalHash(original.Original)),new XAttribute("outputPrefix",original.CallbackPrefix));
                if(!PublishPersonalColdBinding(original.Path+".cached-grid-queue-ready",UiOriginalBytes(ready)))return false;
                pending.NativeContinuationEntered=true;pending.Returned=true;
                card.isInventoryFull=false;
                return true;
            }
            if(pending.NativeContinuationEntered)return pending.Returned;
            var entry=new XElement("recipeOutputNativeContinuationEntered",new XAttribute("operation",original.Operation.ToString("N")),
                new XAttribute("original",UiOriginalHash(original.Original)),new XAttribute("outputPrefix",original.CallbackPrefix));
            pending.NativeContinuationEntered=true;pending.Returned=false;
            if(!PublishPersonalColdBinding(original.Path+".native-continuation-entered",UiOriginalBytes(entry)))return false;
            // Native helper owns its real quest event and completion sound.
            // Its sole inventory call acknowledges the already-accepted grid
            // primary through ConsumeUiOutputCachedPrimary, without insertion.
            scope.CachedContinuation=true;scope.CachedPrimaryConsumed=false;
            bool returned=card.AddItemToInventory();
            return returned&&scope.CachedPrimaryConsumed&&pending.NativeCallbackEntered&&pending.Returned&&
                original.Current()&&original.CallbackOwnerCurrent();
        }
        finally{scope.CachedContinuation=false;scope.CachedPrimaryConsumed=false;ui=previous;}
    }
}
