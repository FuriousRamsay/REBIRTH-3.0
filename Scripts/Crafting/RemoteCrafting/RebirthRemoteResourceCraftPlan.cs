using System;
using System.Collections.Generic;
using System.Linq;

// Detached prediction of this mod's existing native-owner grant consumer.
// It grants no source payment, queue admission or owner mutation permission.
internal static class RebirthRemoteResourceCraftPlan
{
    internal static bool IsExactShortfall(RebirthCamperEnergyActorImage before,IReadOnlyList<ItemStack> requirements,IReadOnlyList<ItemStack> remote,int beltCount)
    {
        try
        {
            if(before==null||requirements==null||remote==null||beltCount<0||beltCount>before.Belt.Length)return false;
            var needs=new Dictionary<int,long>();var paid=new Dictionary<int,long>();
            foreach(var item in requirements){if(item==null||item.IsEmpty()||item.count<1)return false;needs.TryGetValue(item.itemValue.type,out long n);needs[item.itemValue.type]=checked(n+item.count);}
            foreach(var item in remote){if(item==null||item.IsEmpty()||item.count<1)return false;paid.TryGetValue(item.itemValue.type,out long n);paid[item.itemValue.type]=checked(n+item.count);}
            if(paid.Keys.Any(key=>!needs.ContainsKey(key)))return false;
            foreach(var need in needs)
            {
                long native=before.Bag.Concat(before.Belt.Take(beltCount)).Where(item=>item!=null&&!item.IsEmpty()&&item.itemValue.type==need.Key&&!(item.itemValue.HasModSlots&&item.itemValue.HasMods())).Sum(item=>(long)item.count);
                paid.TryGetValue(need.Key,out long amount);if(amount!=Math.Max(0L,need.Value-native))return false;
            }
            return paid.Count>0;
        }
        catch{return false;}
    }
    internal static bool TryDebit(RebirthCamperEnergyActorImage before,IReadOnlyList<ItemStack> requirements,IReadOnlyList<ItemStack> paid,int beltCount,
        out RebirthCamperEnergyActorImage after,out List<ItemStack> removed)
    {
        after=null;removed=null;
        try
        {
            if(before==null||requirements==null||paid==null||beltCount<0||beltCount>before.Belt.Length||requirements.Any(stack=>stack==null||stack.IsEmpty()||stack.count<0)||
                paid.Any(stack=>stack==null||stack.IsEmpty()||stack.count<1))return false;
            var remote=new Dictionary<int,int>();var needs=new Dictionary<int,int>();
            foreach(var stack in paid){remote.TryGetValue(stack.itemValue.type,out int n);remote[stack.itemValue.type]=checked(n+stack.count);}
            foreach(var stack in requirements){needs.TryGetValue(stack.itemValue.type,out int n);needs[stack.itemValue.type]=checked(n+stack.count);}
            if(remote.Any(pair=>!needs.TryGetValue(pair.Key,out int total)||pair.Value>total))return false;
            var planned=before.Copy();var consumed=new List<ItemStack>();
            foreach(var pair in needs)
            {
                remote.TryGetValue(pair.Key,out int used);int remaining=pair.Value-used;
                foreach(var slots in new[]{planned.Bag,planned.Belt})
                {
                    int length=ReferenceEquals(slots,planned.Belt)?beltCount:slots.Length;
                    for(int slot=0;slot<length&&remaining>0;slot++)
                    {
                        var stack=slots[slot];if(stack==null||stack.IsEmpty()||stack.itemValue.type!=pair.Key||stack.itemValue.HasModSlots&&stack.itemValue.HasMods())continue;
                        int take=Math.Min(remaining,stack.count);consumed.Add(new ItemStack(stack.itemValue.Clone(),take));
                        var next=stack.Clone();next.count-=take;slots[slot]=next.count==0?ItemStack.Empty.Clone():next;remaining-=take;
                    }
                }
                if(remaining!=0)return false;
            }
            consumed.AddRange(paid.Select(stack=>stack.Clone()));after=planned;removed=consumed;return true;
        }
        catch{after=null;removed=null;return false;}
    }
    internal static bool TryCreate(EntityPlayerLocal player,ItemActionEntryCraft entry,IReadOnlyList<ItemStack> paid,
        out RebirthCamperEnergyActorImage before,out RebirthCamperEnergyActorImage after,out Recipe queued,out int count)
    {
        before=null;after=null;queued=null;count=0;
        try
        {
            var xui=entry?.ItemController?.xui;var source=(entry?.ItemController as XUiC_RecipeEntry)?.Recipe;
            if(player==null||source==null||!ReferenceEquals(xui?.playerUI?.entityPlayer,player)||paid==null||paid.Count==0||
                entry.craftCountControl==null||RemoteResourceClientGrantContext.Active)return false;
            count=entry.craftCountControl.Count;if(count<1||count>short.MaxValue)return false;
            bool hasItems;
            RemoteResourceClientGrantContext.Begin(paid.Select(stack=>stack.Clone()).ToList());
            try{hasItems=entry.hasItems(xui,source);}finally{RemoteResourceClientGrantContext.End();}
            if(!hasItems)return false;
            var ingredients=entry.tempIngredientList.Select(stack=>stack.Clone()).ToList();
            if(ingredients.Count==0||ingredients.Any(stack=>stack==null||stack.count<0||stack.count>0&&stack.IsEmpty())||paid.Any(stack=>stack==null||stack.IsEmpty()||stack.count<1))return false;
            before=RebirthCamperEnergyActorImage.Capture(player);after=before.Copy();
            var remote=new Dictionary<int,int>();var needs=new Dictionary<int,int>();
            foreach(var stack in paid){remote.TryGetValue(stack.itemValue.type,out int n);remote[stack.itemValue.type]=checked(n+stack.count);}
            foreach(var stack in ingredients){needs.TryGetValue(stack.itemValue.type,out int n);needs[stack.itemValue.type]=checked(n+stack.count*count);}
            if(remote.Any(pair=>!needs.TryGetValue(pair.Key,out int total)||pair.Value>total))return false;
            var removed=new List<ItemStack>();
            foreach(var pair in needs)
            {
                remote.TryGetValue(pair.Key,out int used);int remaining=pair.Value-used;
                // Existing ConsumeLocalRemainder aggregates repeated keys then
                // consumes bag before native owned belt slots, skipping mods.
                foreach(var slots in new[]{after.Bag,after.Belt})
                {
                    int length=ReferenceEquals(slots,after.Belt)?player.inventory.Length:slots.Length;
                    for(int slot=0;slot<length&&remaining>0;slot++)
                    {
                        var stack=slots[slot];if(stack==null||stack.IsEmpty()||stack.itemValue.type!=pair.Key||stack.itemValue.HasModSlots&&stack.itemValue.HasMods())continue;
                        int take=Math.Min(remaining,stack.count);removed.Add(new ItemStack(stack.itemValue.Clone(),take));
                        var next=stack.Clone();next.count-=take;slots[slot]=next.count==0?ItemStack.Empty.Clone():next;remaining-=take;
                    }
                }
                if(remaining!=0)return false;
            }
            removed.AddRange(paid.Select(stack=>stack.Clone()));
            queued=new Recipe{itemValueType=source.itemValueType,count=XUiM_Recipes.GetRecipeCraftOutputCount(xui,source),
                craftingArea=source.craftingArea,craftExpGain=source.craftExpGain,craftingTime=XUiM_Recipes.GetRecipeCraftTime(xui,source),
                craftingToolType=source.craftingToolType,craftingTier=entry.craftingTier,tags=source.tags};
            // Native OnActivated reconstructs quality ingredients from the
            // actual removed list AFTER adding the same Recipe reference.
            queued.AddIngredients(source.ingredients.Any(stack=>stack.itemValue.HasQuality)?removed:ingredients);
            if(queued.count<1||float.IsNaN(queued.craftingTime)||float.IsInfinity(queued.craftingTime)||queued.craftingTime<0f||
                queued.craftingTier<0||queued.craftingTier>byte.MaxValue)return false;
            // Enforce the actual native recipe and actor transport envelopes
            // during detached planning, before an original can pay sources.
            RebirthRemoteResourceOwnerAction.RecipeImage(queued);
            RebirthRemoteResourceClientOriginal.ActorImage(before);RebirthRemoteResourceClientOriginal.ActorImage(after);
            return before.Matches(RebirthCamperEnergyActorImage.Capture(player));
        }
        catch{before=null;after=null;queued=null;count=0;return false;}
    }
}
