using System;
using System.Globalization;
// The displayed total includes only complete, sellable bundles, quoted by the native trader.
internal static class RebirthBackpackSaleQuote
{
    internal static int TradableCount(ItemStack stack)
    {
        var item=stack?.itemValue?.ItemClass;
        if(item==null||stack.IsEmpty()||!(item.IsBlock()?Block.list[stack.itemValue.type].SellableToTrader:item.SellableToTrader))return 0;
        int bundle=Math.Max(1,item.IsBlock()?Block.list[stack.itemValue.type].EconomicBundleSize:item.EconomicBundleSize);
        return stack.count/bundle*bundle;
    }
    internal static int TradableCount(XUi ui,ItemStack stack)
    {
        int count=TradableCount(stack);
        var data=ui?.Trader?.TraderData;
        if(count==0||data==null)return count;
        int limit=stack.itemValue.ItemClass.MaxCount*TraderInfo.TraderBuyLimit;
        if(limit<=0)return count;
        int available=Math.Max(0,limit-data.GetPrimaryItemCount(stack.itemValue));
        var item=stack.itemValue.ItemClass;
        int bundle=Math.Max(1,item.IsBlock()?Block.list[stack.itemValue.type].EconomicBundleSize:item.EconomicBundleSize);
        return Math.Min(count,available)/bundle*bundle;
    }
    internal static string Format(XUi ui,RebirthBackpackSellStashView view)
    {
        if(view==null)return "—";
        long total=0; var reserved=new System.Collections.Generic.Dictionary<int,int>();
        for(int i=0;i<view.Capacity;i++)
        {
            if(!view.TryGetSlot(i,out var stack))continue;
            int count=TradableCount(ui,stack); if(count<=0)continue; int type=stack.itemValue.type; reserved.TryGetValue(type,out int used); var item=stack.itemValue.ItemClass; int bundle=Math.Max(1,item.IsBlock()?Block.list[type].EconomicBundleSize:item.EconomicBundleSize); if(ui?.Trader?.TraderData!=null&&item.MaxCount*TraderInfo.TraderBuyLimit>0){int remaining=Math.Max(0,item.MaxCount*TraderInfo.TraderBuyLimit-ui.Trader.TraderData.GetPrimaryItemCount(stack.itemValue)-used);count=Math.Min(count,remaining)/bundle*bundle;} if(count<=0)continue; reserved[type]=used+count;
            var quote=stack.Clone();quote.count=count;
            if(!RebirthItemSaleEstimate.TryGet(ui,quote,out int price))return "—";
            total+=Math.Max(0,price);
        }
        return total.ToString("N0",CultureInfo.InvariantCulture)+" $";
    }
}