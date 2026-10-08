// Optional presentation data must never prevent inspecting an item.
public static class RebirthItemSaleEstimate
{
    public static bool TryGetSaleBundle(XUi ui, ItemStack stack, out int estimate, out int quantity)
    {
        estimate=0;quantity=1;
        if(ui?.Trader==null||ui.playerUI?.entityPlayer==null||stack==null||stack.IsEmpty()||stack.itemValue?.ItemClass==null)return false;
        try
        {
            var item=stack.itemValue.ItemClass;
            quantity=System.Math.Max(1,item.IsBlock()?Block.list[stack.itemValue.type].EconomicBundleSize:item.EconomicBundleSize);
            estimate=XUiM_Trader.GetSellPrice(ui,stack.itemValue,quantity);
            return true;
        }
        catch{return false;}
    }    public static bool TryGetUnit(XUi ui,ItemStack stack,out int estimate)
    {
        estimate=0;
        if(ui?.Trader==null||ui.playerUI?.entityPlayer==null||stack==null||stack.IsEmpty()||stack.itemValue==null)return false;
        try{estimate=XUiM_Trader.GetSellPrice(ui,stack.itemValue,1);return true;}
        catch{return false;}
    }    public static bool TryGet(XUi ui, ItemStack stack, out int estimate)
    {
        estimate = 0;
        if (ui?.Trader == null || ui.playerUI?.entityPlayer == null ||
            stack == null || stack.IsEmpty() || stack.itemValue == null)
            return false;
        try
        {
            estimate = XUiM_Trader.GetSellPrice(ui, stack.itemValue, stack.count);
            return true;
        }
        catch
        {
            // A missing quote is not a zero-value quote. Keep the item UI usable.
            return false;
        }
    }
}
