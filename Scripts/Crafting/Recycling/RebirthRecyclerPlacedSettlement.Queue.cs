using System;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // Detached projection of the installed native cycleRecipeQueue grammar.
    // The real station calls the actual native method once at application;
    // this projection only freezes its expected full after image in advance.
    internal static RecipeQueueItem[] ProjectNativeCycle(RecipeQueueItem[] source)
    {
        if(source==null||source.Length<1)return null;var queue=new RecipeQueueItem[source.Length];
        for(int i=0;i<source.Length;i++)
        {
            var row=source[i];if(row==null)return null;
            queue[i]=new RecipeQueueItem{Recipe=row.Recipe,Multiplier=row.Multiplier,CraftingTimeLeft=row.CraftingTimeLeft,OneItemCraftTime=row.OneItemCraftTime,
                IsCrafting=row.IsCrafting,Quality=row.Quality,StartingEntityId=row.StartingEntityId,RepairItem=row.RepairItem?.Clone(),AmountToRepair=row.AmountToRepair};
        }
        RecipeQueueItem last=null;if(queue[queue.Length-1].Multiplier>0)return queue;
        for(int i=0;i<queue.Length;i++)
        {
            last=queue[queue.Length-1];if(last.Multiplier!=0)break;
            for(int n=queue.Length-1;n>0;n--)
            {
                var destination=queue[n];var prior=queue[n-1];if(prior.Multiplier<0)prior.Multiplier=0;
                destination.Recipe=prior.Recipe;destination.Multiplier=prior.Multiplier;destination.CraftingTimeLeft=prior.CraftingTimeLeft;
                destination.IsCrafting=prior.IsCrafting;destination.Quality=prior.Quality;destination.OneItemCraftTime=prior.OneItemCraftTime;destination.StartingEntityId=prior.StartingEntityId;
                queue[n]=destination;queue[n-1]=new RecipeQueueItem{StartingEntityId=-1};
            }
        }
        if(last?.Recipe!=null&&!last.IsCrafting&&last.Multiplier!=0)last.IsCrafting=true;return queue;
    }
}
