using System;
using System.Linq;

public sealed partial class XUiC_RebirthCookingWorkspace
{
    private bool sharedCraftPending;
    private string sharedLastStatus;
    public bool CanCraftShared(Recipe recipe,int count) => string.IsNullOrEmpty(SharedCraftBlocker(recipe,count));
    public string SharedCraftBlocker(Recipe recipe,int count)
    {
        if(!open||station==null||!station.IsMilling||recipe==null||count<=0)
            return Localization.Get("xuiRebirthStationSelectBatch");
        if(pulling||sharedCraftPending)return Localization.Get("xuiRebirthCookingCollectingIngredients");
        if(submittingCook)return Localization.Get("xuiRebirthCookingSubmitting");
        if(Preparation.IsPreparing||preparingRequest)return Localization.Get("xuiRebirthCookingPreparing");
        if(!station.CraftingRequirementsValid(recipe))
        {
            string reason=station.CraftingRequirementsInvalidMessage(recipe);
            return string.IsNullOrEmpty(reason)?Localization.Get("xuiRebirthCraftRequirementsBlocked"):reason;
        }
        if(MissingTool(recipe)!=null)return Localization.Get("ttMissingCraftingTools");
        if(Feasible(recipe)<count)return Localization.Get("xuiRebirthCookingNotEnoughAvailableIngredients");
        var queue=station.craftingQueue as XUiC_RebirthCraftingQueue;
        if(queue!=null ? queue.ActiveCount>=queue.RuntimeCapacity : !station.craftingQueue.GetRecipesToCraft().Any(e=>e.GetRecipe()==null))
            return Localization.Get("xuiRebirthStationQueueFull");
        return null;
    }
    public void CraftShared(Recipe recipe,int count)
    {
        if(!CanCraftShared(recipe,count))return;
        Select(recipe);SetBatch(count);
        if(batch!=count)return;
        sharedCraftPending=true;
        Pull();
        CompleteSharedCraft();
    }

    private void CompleteSharedCraft()
    {
        if (!sharedCraftPending || pulling) return;
        sharedCraftPending = false;
        if (string.IsNullOrEmpty(status)) Cook();
        else ReturnIngredients();
        windowGroup.Controller.GetChildByType<XUiC_RebirthCraftingOutcome>()?.RefreshNow();
    }
}
