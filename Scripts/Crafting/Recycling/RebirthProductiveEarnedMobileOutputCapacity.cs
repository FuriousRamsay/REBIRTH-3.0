using System;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private static bool MobileOutputCanFit(TileEntityWorkstation station,RecipeQueueItem row)
    {
        try
        {
            if(row?.Recipe==null||row.Multiplier<1||row.Recipe.count<1||station.Output==null||
                !RebirthRecipeOutputs.TryPlan(row.Recipe,1,out var additional,out _))return false;
            var item=new ItemValue(row.Recipe.itemValueType);
            if(item.ItemClass?.HasQuality==true)item=new ItemValue(row.Recipe.itemValueType,row.Quality,row.Quality);
            return item.ItemClass!=null&&RebirthRecipeOutputIntegration.Predict(station.Output,new ItemStack(item,row.Recipe.count),-1,
                additional.ToArray(),out _,out _);
        }
        catch{return false;}
    }
}
