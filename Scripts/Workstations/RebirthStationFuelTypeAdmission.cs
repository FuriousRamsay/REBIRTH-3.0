using System;
using HarmonyLib;

/// <summary>Retains a workstation block's authored FuelType across native fuel input paths.</summary>
internal static class RebirthStationFuelTypeAdmission
{
    internal static bool Accepts(XUiC_WorkstationFuelGrid grid, ItemStack incoming)
    {
        if (grid == null || incoming == null || incoming.IsEmpty()) return true;
        Block block = grid.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity?.block;
        string required = block?.Properties.GetString("FuelType");
        return string.IsNullOrEmpty(required) ||
            string.Equals(incoming.itemValue?.ItemClass?.Name, required, StringComparison.OrdinalIgnoreCase);
    }
}

[HarmonyPatch(typeof(XUiC_WorkstationFuelGrid), nameof(XUiC_WorkstationFuelGrid.AddItem),
    new[] { typeof(ItemClass), typeof(ItemStack) })]
internal static class RebirthStationFuelAddAdmission
{
    private static bool Prefix(XUiC_WorkstationFuelGrid __instance, ItemStack __1, ref bool __result)
    {
        if (RebirthStationFuelTypeAdmission.Accepts(__instance, __1)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.CanSwap), new[] { typeof(ItemStack) })]
internal static class RebirthStationFuelSwapAdmission
{
    private static void Postfix(XUiC_ItemStack __instance, ItemStack __0, ref bool __result)
    {
        if (__result && !RebirthStationFuelTypeAdmission.Accepts(
                __instance.GetParentByType<XUiC_WorkstationFuelGrid>(), __0)) __result = false;
    }
}

[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleDropOne))]
internal static class RebirthStationFuelSingleAdmission
{
    private static bool Prefix(XUiC_ItemStack __instance)
    {
        return RebirthStationFuelTypeAdmission.Accepts(
            __instance.GetParentByType<XUiC_WorkstationFuelGrid>(),
            __instance.xui?.DragAndDropWindow?.CurrentStack);
    }
}
