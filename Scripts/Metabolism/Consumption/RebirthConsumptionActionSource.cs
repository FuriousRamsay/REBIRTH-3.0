using System;
using System.Collections;
using System.Runtime.CompilerServices;
using HarmonyLib;

#nullable disable

// Native action address and preimage. A transient Hand is a view of one
// source cell, not the selected toolbelt index.
internal sealed class RebirthConsumptionActionSource
{
    private static readonly ConditionalWeakTable<Hand, RebirthConsumptionActionSource> simulated =
        new ConditionalWeakTable<Hand, RebirthConsumptionActionSource>();

    private readonly EntityPlayerLocal player;
    private readonly World world;
    private readonly int deaths;
    private readonly string creation;
    private readonly Hand hand;
    private readonly ItemStackGrid grid;
    private readonly ItemStack nativeStack;
    internal readonly RebirthMetabolismSourceKind Kind;
    internal readonly int Slot;
    internal readonly ItemStack Before;

    private RebirthConsumptionActionSource(EntityPlayerLocal owner, World ownerWorld, string ownerCreation,
        ItemStackGrid sourceGrid, RebirthMetabolismSourceKind kind, int slot, ItemStack source)
    {
        player = owner;
        world = ownerWorld;
        deaths = owner.Died;
        creation = ownerCreation;
        hand = owner.Hand;
        grid = sourceGrid;
        nativeStack = source;
        Kind = kind;
        Slot = slot;
        Before = source.Clone();
    }

    private static bool OwnerCurrent(EntityPlayerLocal owner, World ownerWorld)
        => ThreadManager.IsMainThread() && owner != null && ownerWorld != null && !owner.IsDead() &&
            ReferenceEquals(GameManager.Instance?.World, ownerWorld) && ReferenceEquals(owner.world, ownerWorld) &&
            ReferenceEquals(ownerWorld.GetPrimaryPlayer(), owner) && ReferenceEquals(ownerWorld.GetEntity(owner.entityId), owner);

    private static bool Capture(EntityPlayerLocal owner, ItemStackGrid sourceGrid, int slot, out RebirthConsumptionActionSource source)
    {
        source = null;
        var ownerWorld = owner?.world as World;
        if (!OwnerCurrent(owner, ownerWorld) || sourceGrid == null ||
            !RebirthConsumptionOwnerPublication.TryCaptureCreation(owner, out var ownerCreation)) return false;

        RebirthMetabolismSourceKind kind;
        if (ReferenceEquals(sourceGrid, owner.bag?.ItemGrid)) kind = RebirthMetabolismSourceKind.Backpack;
        else if (ReferenceEquals(sourceGrid, owner.inventory?.ItemGrid)) kind = RebirthMetabolismSourceKind.HeldToolbelt;
        else return false;

        var current = RebirthMetabolismService.GetConsumptionInventorySource(owner, kind, slot);
        if (current == null || current.IsEmpty() || current.itemValue == null) return false;
        source = new RebirthConsumptionActionSource(owner, ownerWorld, ownerCreation, sourceGrid, kind, slot, current);
        return true;
    }

    internal bool Current(EntityPlayer owner)
        => ReferenceEquals(owner, player) && OwnerCurrent(player, world) && player.Died == deaths &&
            ReferenceEquals(player.Hand, hand) &&
            RebirthConsumptionOwnerPublication.TryCaptureCreation(player, out var now) && now == creation &&
            ReferenceEquals(grid, Kind == RebirthMetabolismSourceKind.Backpack ? player.bag?.ItemGrid : player.inventory?.ItemGrid) &&
            ReferenceEquals(nativeStack, RebirthMetabolismService.GetConsumptionInventorySource(player, Kind, Slot)) &&
            RebirthStationGridIngredients.IsSameStackSnapshot(nativeStack, Before);

    private static bool OwnsHeldData(EntityPlayerLocal owner, ItemInventoryData data, int slot)
        => data != null && owner?.Hand is Hand nativeHand && nativeHand.mode == Hand.HoldingMode.Current &&
            slot >= 0 && slot == owner.inventory?.holdingItemIdx && nativeHand.slotDatas != null &&
            slot < nativeHand.slotDatas.Length && ReferenceEquals(nativeHand.slotDatas[slot], data);

    internal bool CurrentForAction(EntityPlayer owner, ItemInventoryData data)
    {
        if (data == null || !ReferenceEquals(data.holdingEntity, player) || !ReferenceEquals(data.itemStack, nativeStack) || !Current(owner))
            return false;
        if (ReferenceEquals(data, hand.transientData))
            return hand.mode == Hand.HoldingMode.Transient && simulated.TryGetValue(hand, out var active) && ReferenceEquals(active, this);
        return Kind == RebirthMetabolismSourceKind.HeldToolbelt && OwnsHeldData(player, data, Slot);
    }

    internal static bool TryCaptureHand(EntityPlayerLocal owner, ItemInventoryData data, out RebirthConsumptionActionSource source)
    {
        source = null;
        var nativeHand = owner?.Hand;
        if (nativeHand == null || data == null || !ReferenceEquals(data.holdingEntity, owner)) return false;
        if (ReferenceEquals(data, nativeHand.transientData))
            return simulated.TryGetValue(nativeHand, out source) && source.CurrentForAction(owner, data);

        int slot = data.slotIdx;
        return OwnsHeldData(owner, data, slot) && Capture(owner, owner.inventory.ItemGrid, slot, out source) &&
            ReferenceEquals(data.itemStack, source.nativeStack);
    }

    internal static bool TryCaptureInstant(EntityPlayerLocal owner, XUiC_ItemStack controller, ItemStack actionStack,
        bool isHeld, out RebirthConsumptionActionSource source)
    {
        source = null;
        if (owner == null || actionStack == null || actionStack.IsEmpty() || actionStack.itemValue == null) return false;
        if (controller == null)
            return isHeld && TryCaptureHand(owner, owner.inventory?.holdingItemData, out source) &&
                RebirthStationGridIngredients.IsSameStackSnapshot(actionStack, source.Before);

        if (!ReferenceEquals(controller.xui?.playerUI?.entityPlayer, owner)) return false;
        var grid = controller.StackLocation == XUiC_ItemStack.StackLocationTypes.Backpack ? owner.bag?.ItemGrid :
            controller.StackLocation == XUiC_ItemStack.StackLocationTypes.ToolBelt ? owner.inventory?.ItemGrid : null;
        return Capture(owner, grid, controller.SlotNumber, out source) &&
            RebirthStationGridIngredients.IsSameStackSnapshot(controller.ItemStack, source.Before) &&
            RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(actionStack.itemValue, 1), new ItemStack(source.Before.itemValue, 1));
    }

    internal static IEnumerator ObserveSimulation(Hand hand, EntityPlayerLocal owner, ItemStackGrid sourceGrid, int slot, IEnumerator native)
    {
        RebirthConsumptionActionSource bound = null;
        try
        {
            while (true)
            {
                // The native iterator can yield while another transient owns the
                // hand. Bind only the transition performed by this MoveNext.
                bool canBind = bound == null && hand.mode != Hand.HoldingMode.Transient;
                if (!native.MoveNext()) yield break;
                if (canBind && hand.mode == Hand.HoldingMode.Transient &&
                    Capture(owner, sourceGrid, slot, out var source) &&
                    ReferenceEquals(hand.transientData?.itemStack, source.nativeStack))
                {
                    simulated.Remove(hand);
                    simulated.Add(hand, source);
                    bound = source;
                }
                yield return native.Current;
            }
        }
        finally
        {
            try { (native as IDisposable)?.Dispose(); }
            finally
            {
                if (bound != null && simulated.TryGetValue(hand, out var active) && ReferenceEquals(active, bound))
                    simulated.Remove(hand);
            }
        }
    }
}

[HarmonyPatch(typeof(Hand), nameof(Hand.SimulateActionExecution), new Type[] { typeof(int), typeof(ItemStackGrid), typeof(int) })]
internal static class RebirthMetabolismSimulatedSourcePatch
{
    private static void Postfix(Hand __instance, int __0, ItemStackGrid __1, int __2, ref IEnumerator __result)
    {
        if (__result == null || __instance.entity is not EntityPlayerLocal owner || __1 == null ||
            __2 < 0 || __2 >= __1.Length) return;
        var actions = __1[__2]?.itemValue?.ItemClass?.Actions;
        if (actions == null || __0 < 0 || __0 >= actions.Length || actions[__0] is not ItemActionConsumeMetabolismRebirth) return;
        __result = RebirthConsumptionActionSource.ObserveSimulation(__instance, owner, __1, __2, __result);
    }
}
