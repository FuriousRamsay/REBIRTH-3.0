using System.Collections.Generic;
using HarmonyLib;

public sealed partial class XUiC_RebirthCookingWorkspace
{
    private XUiC_ItemStack inspectedResultSlot;
    internal void SelectInventoryResult(XUiC_ItemStack slot)
    {
        if(!open||slot?.xui!=xui||station.UsesSharedMillingPresentation)return;
        inspectedResultSlot=slot.ItemStack?.IsEmpty()==false?slot:null;
        Render();
    }
    private void PaintInventoryResult()
    {
        var stack=inspectedResultSlot?.ItemStack;
        bool show=stack!=null&&!stack.IsEmpty();
        Show("cookingItemContext",show);Show("resultRecipeContent",!show);
        if(!show){Text("resultTitle",Localization.Get("xuiCamperCookingResult"));return;}
        Text("resultTitle",Localization.Get("rebirthTraderSelectedTitle"));
        Text("cookingItemName",stack.itemValue.ItemClass.GetLocalizedItemName());
        Show("cookingItemCount",false);
        Text("cookingItemDescription",XUiC_RebirthCraftingItemContext.ResolveDescription(stack,xui));
        if(Find("cookingItemIcon")?.ViewComponent is XUiV_Sprite icon)
        {icon.SpriteName=stack.itemValue.GetPropertyOverride(ItemClass.PropCustomIcon,stack.itemValue.ItemClass.GetIconName());icon.Color=stack.itemValue.ItemClass.GetIconTint(stack.itemValue);}
        RebirthSelectedDurability.Render(this,"cookingItemDurability",inspectedResultSlot);
        var rows=UIDisplayInfoManager.Current.GetDisplayStatsForTag(stack.itemValue.ItemClass.DisplayType);
        var lines=new List<string>();
        if(rows!=null)foreach(var stat in rows.DisplayStats)
            if(stat!=null)lines.Add((stat.TitleOverride??UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType))+": "+
                RebirthItemStatColors.Format(RebirthItemStatColors.NativeValue(stack,xui.playerUI.entityPlayer,stat)));
        Text("cookingItemStats",string.Join("\n",lines));
    }
}
[HarmonyPatch(typeof(XUiC_ItemStack),nameof(XUiC_ItemStack.HandleClickComplete))]
internal static class RebirthCookingItemResultSelection
{
    private static void Postfix(XUiC_ItemStack __instance)
        =>XUiC_RebirthCookingWorkspace.ActiveInstance?.SelectInventoryResult(__instance);
}
