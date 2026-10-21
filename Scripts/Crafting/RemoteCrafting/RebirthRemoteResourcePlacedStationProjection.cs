using System;
using System.Collections.Generic;
using System.Linq;

// Readonly availability for the real native-input pool. It is never payment,
// source reservation, inventory authority or permission to invoke a callback.
// Native callsite/UI installation is separate and currently remains inactive.
internal static class RebirthRemoteResourcePlacedStationProjection
{
    internal static bool TryShortfall(ItemStack[] actualInput,IReadOnlyList<ItemStack> nativeModifiedPerBatch,int batches,
        Func<ItemValue,int> remoteOnly,out List<ItemStack> required,out List<ItemStack> remote)
    {
        required=null;remote=null;
        try
        {
            if(actualInput==null||actualInput.Length>byte.MaxValue||nativeModifiedPerBatch==null||nativeModifiedPerBatch.Count>8192||batches<1||batches>short.MaxValue||remoteOnly==null)return false;
            var needed=new Dictionary<int,ItemStack>();
            foreach(var item in nativeModifiedPerBatch)
            {
                if(item==null||item.count<0)return false;if(item.count==0)continue;
                if(item.itemValue==null||item.IsEmpty())return false;
                long count=checked((long)item.count*batches);
                if(needed.TryGetValue(item.itemValue.type,out var prior))count=checked(count+prior.count);
                if(count>int.MaxValue)return false;needed[item.itemValue.type]=new ItemStack(item.itemValue.Clone(),(int)count);
            }
            if(needed.Count<1)return false;
            var local=new Dictionary<int,long>();
            foreach(var item in actualInput)
            {
                if(item==null||item.count<0)return false;if(item.IsEmpty())continue;
                if(item.itemValue==null||item.count<1)return false;
                local.TryGetValue(item.itemValue.type,out long count);local[item.itemValue.type]=checked(count+item.count);
            }
            var full=new List<ItemStack>();var missing=new List<ItemStack>();
            foreach(var pair in needed.OrderBy(pair=>pair.Key))
            {
                full.Add(pair.Value.Clone());local.TryGetValue(pair.Key,out long have);long deficit=Math.Max(0,(long)pair.Value.count-have);
                if(deficit==0)continue;
                int available=remoteOnly(pair.Value.itemValue);
                if(available<0||available<deficit)return false;
                missing.Add(new ItemStack(pair.Value.itemValue.Clone(),checked((int)deficit)));
            }
            required=full;remote=missing;return true;
        }
        catch{required=null;remote=null;return false;}
    }
    internal static bool TryActualInputPool(ItemActionEntryCraft entry,out EntityPlayerLocal player,out ItemStack[] input)
    {
        player=null;input=null;
        try
        {
            var actor=entry?.ItemController?.xui?.playerUI?.entityPlayer;var recipe=(entry?.ItemController as XUiC_RecipeEntry)?.Recipe;
            if(actor==null||recipe==null||recipe.IsScrap||RebirthCookingHeat.Managed(recipe)||RebirthRecyclerRuntime.IsMarked(recipe)||
                !RebirthRemoteResourcePlacedStationOriginal.AllowsReviewedPlacedMode||RebirthStationGridQueue.IsMarked(recipe)||!RemoteResourcesRuntimePolicy.Enabled||
                !RebirthRemoteResourcePlacedStationScope.TryObserveWindow(actor,entry,out var station,out var payment)||payment!=RebirthRemoteResourcePlacedStationScope.PaymentSource.StationInput)return false;
            var grid=entry.craftCountControl.WindowGroup.Controller.GetChildByType<XUiC_WorkstationInputGrid>();var slots=grid?.GetSlots();
            if(slots==null||slots.Length>byte.MaxValue||slots.Length!=station.Input?.Length||
                slots.Where((item,index)=>!RebirthStationGridIngredients.IsSameStackSnapshot(item,station.Input[index])).Any())return false;
            player=actor;input=slots;return true; // readonly native UI references
        }
        catch{player=null;input=null;return false;}
    }
    internal static bool TryCurrentShortfall(ItemActionEntryCraft entry,out List<ItemStack> required,out List<ItemStack> remote)
    {
        required=null;remote=null;
        // Native hasItems can return during its eligibility scan after only a
        // prefix of the requirements was appended. Such a list is never the
        // complete original payment, even if that prefix is affordable.
        var recipe=(entry?.ItemController as XUiC_RecipeEntry)?.Recipe;
        return recipe?.ingredients!=null&&TryActualInputPool(entry,out var player,out var input)&&entry.tempIngredientList!=null&&
            entry.tempIngredientList.Count==recipe.ingredients.Count&&
            TryShortfall(input,entry.tempIngredientList,entry.craftCountControl.Count,item=>RemoteResourceClientAvailability.GetCount(player,item),out required,out remote);
    }
}
