using System;
using System.Collections.Generic;
using UnityEngine;

// Current native selling arithmetic, with an explicit owner for dedicated servers.
// Native GetSellPrice otherwise obtains its selling-bonus owner from the local UI singleton.
internal static class RebirthStashBatchPrice
{
    internal static int Price(EntityPlayer player,TraderData trader,ItemValue value,int count)
    {
        var item=value.ItemClass;float economic;int bundle;
        if(item.IsBlock()){var block=Block.list[value.type];economic=block.EconomicValue*block.EconomicSellScale;bundle=block.EconomicBundleSize;}
        else {economic=EffectManager.GetValue(PassiveEffects.EconomicValue,value,item.EconomicValue*item.EconomicSellScale,player);bundle=item.EconomicBundleSize;}
        if(economic==0)return 0;
        bool overridden=trader!=null&&trader.TraderInfo.OverrideSellMarkdown!=-1f;
        float markdown=overridden?trader.TraderInfo.OverrideSellMarkdown:TraderInfo.SellMarkdown;
        float price=0;
        if(value.HasQuality)
        {
            float min=item.TraderQualityMinMod,max=item.TraderQualityMaxMod;
            if(min<=0&&max<=0){min=TraderInfo.QualityMinMod;max=TraderInfo.QualityMaxMod;}
            price=economic*markdown*Mathf.Lerp(min,max,(value.Quality-1f)/5f)*value.PercentUsesLeft;
        }
        else if(item.HasSubItems){for(int i=0;i<value.ModificationCount;i++){var mod=value.GetModification(i);if(!mod.IsEmpty())price+=Price(player,trader,mod,1);}}
        else price=economic*markdown;
        if(!overridden)price+=price*EffectManager.GetValue(PassiveEffects.BarteringSelling,null,0f,player,tags:item.ItemTags);
        return Mathf.CeilToInt((int)(price*(count/Math.Max(1,bundle)))*SandboxOptions.SandboxOptionManager.GetFloat(SandboxOptions.SandboxOptions.TraderSellPrices));
    }
    internal static bool Plan(EntityPlayer player,TraderData trader,ItemValue pack,out ItemValue after,out List<ItemStack> sold,out int total)
    {
        after=null;sold=new List<ItemStack>();total=0;
        if(!RebirthBackpackSellStashContents.TryRead(pack,out var cells))return false;
        var reserved=new Dictionary<int,int>();long sum=0;
        for(int i=0;i<cells.Length;i++)
        {
            var stack=cells[i];int count=RebirthBackpackSaleQuote.TradableCount(stack);if(count==0)continue;
            int type=stack.itemValue.type;reserved.TryGetValue(type,out int used);var item=stack.itemValue.ItemClass;
            int limit=item.MaxCount*TraderInfo.TraderBuyLimit;
            int bundle=Math.Max(1,item.IsBlock()?Block.list[type].EconomicBundleSize:item.EconomicBundleSize);
            if(trader!=null&&limit>0)count=Math.Min(count,Math.Max(0,limit-trader.GetPrimaryItemCount(stack.itemValue)-used))/bundle*bundle;
            if(count<=0)continue;
            int price=Price(player,trader,stack.itemValue,count);if(price<=0)continue;
            sum+=price;if(sum>int.MaxValue)return false;
            var selling=stack.Clone();selling.count=count;sold.Add(selling);reserved[type]=used+count;
            cells[i]=stack.Clone();cells[i].count-=count;if(cells[i].count==0)cells[i]=ItemStack.Empty.Clone();
        }
        if(sum==0||!RebirthBackpackSellStashContents.TryWrite(pack,cells,out after))return false;
        total=(int)sum;return true;
    }
}
