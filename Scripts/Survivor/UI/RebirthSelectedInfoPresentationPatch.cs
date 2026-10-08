using System.Runtime.CompilerServices;
using HarmonyLib;
// Covers native selected-item panels (trader, loot, rewards), using the clicked real slot.
[HarmonyPatch(typeof(XUiC_ItemInfoWindow),nameof(XUiC_ItemInfoWindow.SetInfo))]
internal static class RebirthSelectedInfoSourcePatch
{
    internal sealed class Source { internal XUiController Slot,GeometrySlot; }
    internal static readonly ConditionalWeakTable<XUiC_ItemInfoWindow,Source> Sources=new ConditionalWeakTable<XUiC_ItemInfoWindow,Source>();
    private static void Postfix(XUiC_ItemInfoWindow __instance,XUiController controller)
    { Sources.GetValue(__instance,_=>new Source()).Slot=controller; }
}
[HarmonyPatch(typeof(XUiC_ItemInfoWindow),nameof(XUiC_ItemInfoWindow.Update))]
internal static class RebirthSelectedInfoPresentationPatch
{
    [HarmonyPostfix,HarmonyPriority(Priority.Last)]
    private static void Postfix(XUiC_ItemInfoWindow __instance)
    {
        if(__instance.ViewComponent?.IsVisible!=true||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        if(!RebirthSelectedInfoSourcePatch.Sources.TryGetValue(__instance,out var source))return;
        var slot=source.Slot;
        if(slot?.GetChildById("itemIcon")!=null)RebirthSelectedDurability.CopyNativePresentation(__instance,slot,true);
        else
        {
            // Stock/reward rows place their quality indicator away from the item icon.
            // Their detail view uses a real Backpack cell as the geometry reference only;
            // native bindings retain the inspected reward/stock item's quality and fill.
            if(source.GeometrySlot==null){var slots=__instance.windowGroup.Controller.GetChildByType<XUiC_RebirthCraftingInventory>()?.GetItemStackControllers();if(slots!=null&&slots.Length>0)source.GeometrySlot=slots[0];}
            if(source.GeometrySlot!=null)RebirthSelectedDurability.CopyNativePresentation(__instance,source.GeometrySlot,true,__instance.itemStack);
        }
    }
}