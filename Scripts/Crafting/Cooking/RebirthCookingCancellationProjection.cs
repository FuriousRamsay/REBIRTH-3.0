using System;
using System.Linq;

// Native cards only. Financial retirement is already positively terminal.
internal static class RebirthCookingCancellationProjection
{
    internal static bool Refresh(XUiC_RecipeStack source,string location,string token,Func<bool> current)
    {
        if(!ThreadManager.IsMainThread()||source==null||current?.Invoke()!=true)return false;
        var window=source.GetParentByType<XUiC_WorkstationWindowGroup>();
        if(window?.windowGroup?.isShowing!=true)return true;
        var station=window.WorkstationData?.TileEntity;
        if(station==null||RebirthCookingStationLocator.Encode(station)!=location)return false;
        var cards=window.GetChildByType<XUiC_CraftingQueue>()?.GetRecipesToCraft();
        var image=RebirthCookingRemoteAdmissionBackend.Queue(station.Queue);
        var rows=RebirthCookingRemoteAdmissionBackend.ReadQueue(image);
        if(cards==null||cards.Length!=rows.Length||rows.Any(row=>row==null||row.RepairItem!=null||row.AmountToRepair!=0))return false;
        // A remote replica must first receive the authoritative queue removal.
        // Do not clear a replacement card or manufacture a local empty queue.
        if(rows.Any(row=>RebirthCookingHeat.Managed(row.Recipe)&&row.Recipe.ingredients[0].itemValue.TryGetMetadata("rebirth.cooking.queue.token",out string own)&&own==token))return false;
        bool Current()=>current()&&ReferenceEquals(source.GetParentByType<XUiC_WorkstationWindowGroup>(),window)&&ReferenceEquals(window.WorkstationData?.TileEntity,station)&&RebirthCookingStationLocator.Encode(station)==location&&
            RebirthCookingRemoteAdmissionBackend.Queue(station.Queue)==image;
        for(int i=0;i<rows.Length;i++)
        {
            if(!Current()||cards[i]==null)return false;var row=rows[i];
            if(!cards[i].SetRecipe(row.Recipe,row.Multiplier,row.CraftingTimeLeft,true,row.StartingEntityId,row.Quality,row.OneItemCraftTime)||!Current())return false;
            cards[i].IsCrafting=false;cards[i].IsDirty=true;
        }
        return Current();
    }
}
