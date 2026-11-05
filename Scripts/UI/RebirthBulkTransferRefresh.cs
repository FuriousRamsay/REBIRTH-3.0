using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthBackpackInfoWindowLookup));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthInfoWindowVisibility));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthEmptySlotRefresh));
    }

    private const int MaximumFlushRounds = 4;
    [ThreadStatic] private static int depth;
    [ThreadStatic] private static HashSet<XUiC_Backpack> backpacks;
    [ThreadStatic] private static Dictionary<XUiC_LootContainer, ITileEntity> containers;
    [ThreadStatic] private static bool flushing;
    [ThreadStatic] private static XUiC_Backpack flushingBag;
    [ThreadStatic] private static bool flushingBagEntered;

    [HarmonyPatch(typeof(XUiM_LootContainer), nameof(XUiM_LootContainer.StashItems))]
    private static class Transfer
    {
        private static void Prefix() { ++depth; }
        private static Exception Finalizer(Exception __exception)
        {
            if (--depth != 0) return __exception;
            try
            {
                FlushBackpacks(ref __exception);
                if (containers != null) foreach (var pair in containers)
                {
                    try { pair.Key.OnTileEntityChanged(pair.Value); }
                    catch (Exception ex) { RecordRefreshFailure(ex, ref __exception); }
                }
            }
            finally { backpacks?.Clear(); containers?.Clear(); }
            return __exception;
        }
        // Refreshing one grid can notify the others. A bag asked to refresh again runs in a later
        // round, so the final presentation still reads the final inventory, without nested rebuilds.
        private static void FlushBackpacks(ref Exception transferException)
        {
            if (backpacks == null) return;
            flushing = true;
            try
            {
                for (int round = 0; round < MaximumFlushRounds && backpacks.Count != 0; round++)
                {
                    var batch = new List<XUiC_Backpack>(backpacks);
                    backpacks.Clear();
                    foreach (var bag in batch)
                    {
                        flushingBag = bag;
                        flushingBagEntered = false;
                        try { bag.RefreshBackpackSlots(); }
                        catch (Exception ex) { RecordRefreshFailure(ex, ref transferException); }
                    }
                }
            }
            finally { flushing = false; flushingBag = null; flushingBagEntered = false; }
            // Bounded above; anything still waiting refreshes through the native path.
            foreach (var bag in new List<XUiC_Backpack>(backpacks))
            {
                try { bag.RefreshBackpackSlots(); }
                catch (Exception ex) { RecordRefreshFailure(ex, ref transferException); }
            }
        }
        private static void RecordRefreshFailure(Exception failure, ref Exception transferException)
        {
            Log.Warning("[REBIRTH Inventory] bulk-transfer presentation refresh failed: " + failure.Message);
            if (transferException == null) transferException = failure;
        }
    }

    [HarmonyPatch(typeof(XUiC_Backpack), nameof(XUiC_Backpack.RefreshBackpackSlots))]
    private static class BackpackRefresh
    {
        private static bool Prefix(XUiC_Backpack __instance)
        {
            if (flushing)
            {
                if (ReferenceEquals(__instance, flushingBag) && !flushingBagEntered) { flushingBagEntered = true; return true; }
                backpacks.Add(__instance);
                return false;
            }
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

// XUi.GetChild*ByType walks every controller in every window group. The window set is fixed
// once a UI is built, so these two per-refresh native lookups keep their result.
internal static class RebirthUiLookupCache
{
    private sealed class Entry
    {
        internal int Groups;
        internal XUiC_ItemInfoWindow InfoWindow;
        internal List<XUiC_InfoWindow> InfoWindows;
    }
    private static readonly ConditionalWeakTable<XUi, Entry> Entries = new ConditionalWeakTable<XUi, Entry>();
    private static Entry For(XUi xui)
    {
        var entry = Entries.GetOrCreateValue(xui);
        if (entry.Groups != xui.WindowGroups.Count)
        {
            entry.Groups = xui.WindowGroups.Count;
            entry.InfoWindow = null;
            entry.InfoWindows = null;
        }
        return entry;
    }
    internal static XUiC_ItemInfoWindow ItemInfoWindow(XUi xui)
    {
        var entry = For(xui);
        return entry.InfoWindow ?? (entry.InfoWindow = xui.GetChildByType<XUiC_ItemInfoWindow>());
    }
    internal static List<XUiC_InfoWindow> InfoWindows(XUi xui)
    {
        var entry = For(xui);
        return entry.InfoWindows ?? (entry.InfoWindows = xui.GetChildrenByType<XUiC_InfoWindow>());
    }
}

[HarmonyPatch(typeof(XUiC_Backpack), "SetStacks")]
internal static class RebirthBackpackInfoWindowLookup
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var cached = AccessTools.Method(typeof(RebirthUiLookupCache), nameof(RebirthUiLookupCache.ItemInfoWindow));
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.IsGenericMethod && method.Name == "GetChildByType" &&
                method.DeclaringType == typeof(XUi) && method.GetGenericArguments()[0] == typeof(XUiC_ItemInfoWindow))
                yield return new CodeInstruction(OpCodes.Call, cached) { labels = instruction.labels, blocks = instruction.blocks };
            else yield return instruction;
        }
    }
}

// Native marks a slot for a full binding refresh on every assignment, so one changed item
// refreshes the whole grid. Writing "empty" over an empty, unselected slot changes nothing it
// shows. Every other assignment, and every other reason a slot refreshes, stays native.
[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.ItemStack), MethodType.Setter)]
internal static class RebirthEmptySlotRefresh
{
    private static void Prefix(XUiC_ItemStack __instance, ItemStack value, out bool __state)
    {
        var current = __instance.ItemStack;
        __state = !__instance.IsDirty && !__instance.IsSelected && value != null && value.IsEmpty() && current != null && current.IsEmpty();
    }
    private static void Postfix(XUiC_ItemStack __instance, bool __state)
    {
        if (__state) __instance.IsDirty = false;
    }
}

[HarmonyPatch(typeof(XUiC_InfoWindow), nameof(XUiC_InfoWindow.OnVisibilityChanged))]
internal static class RebirthInfoWindowVisibility
{
    // Same visibility rule as native, without rebuilding the list on every window change.
    private static bool Prefix(XUiC_InfoWindow __instance, bool _visibleInScene)
    {
        if (!_visibleInScene) return true;
        bool showing = __instance.windowGroup == null || __instance.windowGroup.isShowing;
        var windows = RebirthUiLookupCache.InfoWindows(__instance.xui);
        for (int i = 0; i < windows.Count; i++)
            if (windows[i] != __instance && (showing || !windows[i].windowGroup.isShowing))
                windows[i].ViewComponent.IsVisible = false;
        return false;
    }
}
