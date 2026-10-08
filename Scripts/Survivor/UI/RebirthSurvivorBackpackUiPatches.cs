using System;
using HarmonyLib;

#nullable disable

/// <summary>
/// Presentation-only adapter for the expandable physical backpack. The grid can author up to
/// 100 item controllers, but only the authoritative Bag backing-array prefix is shown. This does
/// not change CarryCapacity, encumbrance, inventory ownership, or slot contents.
/// </summary>
[HarmonyPatch(typeof(XUiC_Backpack), nameof(XUiC_Backpack.SetStacks))]
public static class RebirthSurvivorBackpackSetStacksPatch
{
    [HarmonyPostfix]
    public static void Postfix(XUiC_Backpack __instance, ItemStack[] stackList)
    {
        if (__instance == null || __instance.itemControllers == null || stackList == null) return;
        int physical = Math.Min(stackList.Length, RebirthSurvivorGearService.MaxPhysicalBagSlots);
        for (int i = 0; i < __instance.itemControllers.Length; i++)
        {
            XUiC_ItemStack slot = __instance.itemControllers[i];
            if (slot == null || slot.ViewComponent == null) continue;
            bool visible = i < physical;
            slot.ViewComponent.IsVisible = visible;
            if (!visible)
                slot.ItemStack = ItemStack.Empty.Clone();
        }

        // Keep the authored grid row count aligned to the authoritative physical prefix.
        // XUiC_RebirthExpandableBackpackScroll performs the actual row offset and input handling.
        XUiV_Grid grid = __instance.ViewComponent as XUiV_Grid;
        if (grid != null)
        {
            // PC081: Personal Crafting owns an independent eight-column viewport. Do not let
            // this legacy seven-column presentation patch rewrite its geometry. The custom grid
            // is authored to the 104-cell presentation ceiling and hides every non-authoritative
            // tail cell; its scroll controller computes the real physical row count separately.
            if (__instance is XUiC_RebirthCraftingInventory)
            {
                grid.Columns = RebirthCraftingInventoryBridge.Columns;
                grid.Rows = RebirthCraftingInventoryBridge.AuthoredRows;
            }
            else
            {
                grid.Rows = Math.Max(1, (physical + 6) / 7);
            }
        }
    }
}
