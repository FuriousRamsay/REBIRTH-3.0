using System;
using System.Collections.Generic;
using HarmonyLib;

// Native StashItems performs each inventory mutation synchronously. Coalesce only
// the two full-grid presentation callbacks; backend events and item accounting remain native.
internal static class RebirthBulkTransferRefresh
{
    public static void Install(Harmony harmony)
    {
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(Transfer));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(BackpackRefresh));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(ContainerRefresh));
    }

    [ThreadStatic] private static int depth;
    [ThreadStatic] private static HashSet<XUiC_Backpack> backpacks;
    [ThreadStatic] private static Dictionary<XUiC_LootContainer, ITileEntity> containers;

    [HarmonyPatch(typeof(XUiM_LootContainer), nameof(XUiM_LootContainer.StashItems))]
    private static class Transfer
    {
        private static void Prefix() { ++depth; }
        private static Exception Finalizer(Exception __exception)
        {
            if (--depth != 0) return __exception;
            try
            {
                if (backpacks != null) foreach (var bag in backpacks) bag.RefreshBackpackSlots();
                if (containers != null) foreach (var pair in containers) pair.Key.OnTileEntityChanged(pair.Value);
            }
            catch (Exception ex)
            {
                Log.Warning("[REBIRTH Inventory] bulk-transfer presentation refresh failed: " + ex.Message);
                if (__exception == null) __exception = ex;
            }
            finally { backpacks?.Clear(); containers?.Clear(); }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(XUiC_Backpack), nameof(XUiC_Backpack.RefreshBackpackSlots))]
    private static class BackpackRefresh
    {
        private static bool Prefix(XUiC_Backpack __instance)
        {
            if (depth == 0) return true;
            if (backpacks == null) backpacks = new HashSet<XUiC_Backpack>();
            backpacks.Add(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(XUiC_LootContainer), nameof(XUiC_LootContainer.OnTileEntityChanged))]
    private static class ContainerRefresh
    {
        private static bool Prefix(XUiC_LootContainer __instance, ITileEntity __0)
        {
            if (depth == 0) return true;
            if (containers == null) containers = new Dictionary<XUiC_LootContainer, ITileEntity>();
            containers[__instance] = __0;
            return false;
        }
    }
}
