using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

// The native rectSlotLock owns lock-editing clicks. Its parent ItemStack must
// not poll the same release again as a pickup, split, swap or quick transfer.
public static class RebirthInventorySlotLockInput
{
    private sealed class Binding
    { internal XUiController Parent, Owner; internal XUiView Overlay; }
    internal struct InputScope
    { internal bool Active, Press, Over; }
    private static readonly ConditionalWeakTable<XUiC_ItemStack, Binding> bindings = new ConditionalWeakTable<XUiC_ItemStack, Binding>();
    private static bool installed;
    public static void Install()
    {
        if (installed) return;
        var harmony = new Harmony("rebirth.inventory.slotlock.input.v1");
        harmony.CreateClassProcessor(typeof(RebirthInventorySlotLockUpdate)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthInventorySlotLockMutation)).Patch();
        installed = true;
    }
    private static bool Managed(XUiC_ItemStack slot)
        => slot is XUiC_RebirthCharacterBackpackSlot || slot is XUiC_RebirthCraftingInventorySlot
            || slot is XUiC_RebirthEditorBackpackSlot || slot is XUiC_RebirthBackpackSectionNativeSlot
            || slot is XUiC_RebirthContextSlot || slot is XUiC_RebirthCamperEnergyBackpackSlot || slot is XUiC_RebirthCamperTurretBackpackSlot;
    private static Binding Resolve(XUiC_ItemStack slot)
    {
        if (bindings.TryGetValue(slot, out var found) && !ReferenceEquals(found.Parent, slot.Parent))
        { bindings.Remove(slot); found = null; }
        if (found != null) return found;
        found = new Binding { Parent = slot.Parent, Overlay = slot.GetChildById("rectSlotLock")?.ViewComponent };
        if (found.Overlay == null) { bindings.Add(slot, found); return found; }
        for (XUiController owner = slot; owner != null; owner = owner.Parent)
        {
            string value = "false";
            if (!owner.GetBindingValue(ref value, "userlockmode")) continue;
            found.Owner = owner; break;
        }
        bindings.Add(slot, found); return found;
    }
    internal static bool IsLockMode(XUiC_ItemStack slot)
    {
        if (slot == null || !Managed(slot) || slot.xui == null) return false;
        var binding = Resolve(slot);
        if (binding?.Owner == null || binding.Overlay == null) return false;
        string value = "false";
        return binding.Owner.GetBindingValue(ref value, "userlockmode")
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
    internal static InputScope Begin(XUiC_ItemStack slot)
    {
        if (!IsLockMode(slot) || slot.ViewComponent == null) return default;
        var scope = new InputScope { Active = true, Press = slot.ViewComponent.EventOnPress, Over = slot.isOver };
        slot.ViewComponent.EventOnPress = false;
        // The native timed-lock cancellation branch also polls clicks directly,
        // without checking EventOnPress. Leave it to the lock overlay too.
        slot.isOver = false;
        return scope;
    }
    internal static void End(XUiC_ItemStack slot, InputScope scope)
    {
        if (!scope.Active) return;
        if (slot.ViewComponent != null) slot.ViewComponent.EventOnPress = scope.Press;
        slot.isOver = scope.Over;
    }
}

[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.Update))]
internal static class RebirthInventorySlotLockUpdate
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(XUiC_ItemStack __instance, out RebirthInventorySlotLockInput.InputScope __state)
        => __state = RebirthInventorySlotLockInput.Begin(__instance);
    private static void Finalizer(XUiC_ItemStack __instance, RebirthInventorySlotLockInput.InputScope __state)
        => RebirthInventorySlotLockInput.End(__instance, __state);
}

[HarmonyPatch]
internal static class RebirthInventorySlotLockMutation
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "HandleStackSwap", "HandlePartialStackPickup", "HandleDropOne", "SwapItem", "HandleMoveToPreferredLocation" })
        {
            var method = AccessTools.DeclaredMethod(typeof(XUiC_ItemStack), name, Type.EmptyTypes);
            if (method == null) throw new MissingMethodException(typeof(XUiC_ItemStack).FullName, name);
            yield return method;
        }
    }
    [HarmonyPriority(Priority.First)]
    [HarmonyBefore("rebirth.library.cursor-reservation.3.0", "rebirth.camper.production")]
    private static bool Prefix(XUiC_ItemStack __instance) => !RebirthInventorySlotLockInput.IsLockMode(__instance);
}
