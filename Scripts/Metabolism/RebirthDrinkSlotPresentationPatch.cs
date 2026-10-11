using System.Globalization;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
// Native stack bindings can reapply quality styling after liquid bindings. Commit only presentation.
[HarmonyPatch(typeof(XUiC_ItemStack),nameof(XUiC_ItemStack.Update))]
internal static class RebirthDrinkSlotPresentationPatch
{
    private sealed class Views
    {
        internal XUiV_Sprite Fill,Removed;internal XUiV_Label Count;internal int LastCount=-1;internal string CountText="";
        internal Views(XUiC_ItemStack slot)
        {
            Count=slot.GetChildById("stackValue")?.ViewComponent as XUiV_Label;
            if(slot.Children!=null)foreach(var child in slot.Children)
            {
                if(child.ViewComponent?.ID=="durability"&&child.ViewComponent is XUiV_FilledSprite sprite)Fill=sprite;
                if(child.ViewComponent?.ID=="durabilityBackground")Removed=child.ViewComponent as XUiV_Sprite;
            }
        }
    }
    private static readonly ConditionalWeakTable<XUiC_ItemStack,Views> Cache=new ConditionalWeakTable<XUiC_ItemStack,Views>();
    private static readonly Color Blue=new Color32(66,139,190,255);
    [HarmonyPostfix,HarmonyPriority(Priority.Last)]
    private static void Postfix(XUiC_ItemStack __instance)
    {
        if(__instance?.ViewComponent?.IsVisible==true&&RebirthSurvivorMode.IsEnabledForCurrentWorld())RebirthSlotPalette.ApplyLockIcon(__instance);
        var stack=__instance?.ItemStack;
        if(stack==null||stack.IsEmpty()||__instance.ViewComponent?.IsVisible!=true||
            !RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        if(!RebirthConsumableResolver.TryResolve(stack.itemValue.ItemClass,out var definition)||!definition.IsDrink)
        {RebirthSelectedDurability.RenderDragCursor(__instance);return;}
        var views=Cache.GetValue(__instance,slot=>new Views(slot));
        if(views.Fill!=null)
        {
            views.Fill.IsVisible=true;
            float fill=RebirthLiquidContainerService.GetFill01(stack.itemValue,definition);
            if(!Mathf.Approximately(views.Fill.Fill,fill))views.Fill.Fill=fill;
            if(views.Fill.Color!=Blue||views.Fill.Sprite?.color!=Blue)views.Fill.SetColorImmediately(Blue);
        }
        if(views.Removed!=null)views.Removed.IsVisible=false;
        if(views.Count!=null)
        {
            if(views.LastCount!=stack.count){views.LastCount=stack.count;views.CountText=stack.count.ToString(CultureInfo.InvariantCulture);}
            string text=views.CountText;
            if(views.Count.Text!=text)views.Count.SetTextImmediately(text);
            views.Count.Alignment=NGUIText.Alignment.Right;
        }
        RebirthSelectedDurability.RenderDragCursor(__instance);
    }
}