using HarmonyLib;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthDogAutoLootResult : byte
{
    Rejected = 0,
    RoutedToDog = 1,
    VanillaFallback = 2,
    RoutedToHuman = 3,
    PendingHuman = 4,
    RoutedToHumanPartial = 5,
    PendingDog = 6
}

/// <summary>
/// Server-authoritative 3.1 bridge for the 2.6 dog auto-loot contract.
///
/// 2.6 offered eligible corpse-loot awards to the first usable owned ally-animal
/// before falling back to the player. 3.1 no longer contains that EntityZombieSDX
/// award path, so the modern bridge hooks the vanilla loot-container transfer
/// surface instead. The client never tells the server what item to grant: it only
/// identifies the already-open, server-locked loot source and slot. The server
/// resolves the authoritative stack, validates the player's lock/identity and then
/// either moves the complete eligible stack to an owned active dog or leaves the
/// source untouched and tells the client to execute the original vanilla transfer.
/// </summary>
public static partial class RebirthDogAutoLootBridge
{
    // Enable only after the parent agent's single profiler-backed batch proves
    // the named-container journal, restart recovery and client remainder path.
#if DEBUG
    // DEBUG builds only: read-only view over RebirthP11GateOverride.Live (default OFF, guarded `rbp11 gates live`).
    internal static bool SmartLootingLiveRouteQualified => RebirthP11GateOverride.LiveOn;
#else
    internal const bool SmartLootingLiveRouteQualified = false;
#endif
    private sealed class LootAudit{internal long Routed,ConservationFailures;internal bool Conserved;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,LootAudit> Audit=new System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,LootAudit>();
    public static Newtonsoft.Json.Linq.JObject CaptureAuditState(EntityPlayer player=null)
    {player=player??GameManager.Instance?.World?.GetPrimaryPlayer();LootAudit audit=null;if(player!=null)Audit.TryGetValue(player,out audit);lock(Sync)return new Newtonsoft.Json.Linq.JObject{["actor"]=player?.entityId??-1,["pending"]=Pending.Count,["replayingVanilla"]=replayingVanilla,["routed"]=audit?.Routed??0,["routedToOwnedDog"]=(audit?.Routed??0)>0,["conserved"]=audit?.Conserved??false,["conservationFailures"]=audit?.ConservationFailures??0};}
    private static Dictionary<string,long> AuditTotals(ItemStack[] slots,ItemStack source)
    {
        var totals=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var stack in slots)AuditCount(totals,stack);AuditCount(totals,source);return totals;
    }
    private static void AuditCount(Dictionary<string,long> totals,ItemStack stack)
    {if(stack==null||stack.IsEmpty())return;string image=RebirthNativeItemCodec.Encode(stack.itemValue);totals.TryGetValue(image,out long count);totals[image]=count+stack.count;}
    private struct PendingKey : IEquatable<PendingKey>
    {
        public Vector3i Position;
        public int Slot;

        public PendingKey(Vector3i position, int slot)
        {
            Position = position;
            Slot = slot;
        }

        public bool Equals(PendingKey other)
        {
            return Position == other.Position && Slot == other.Slot;
        }

        public override bool Equals(object obj)
        {
            return obj is PendingKey && Equals((PendingKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return (Position.GetHashCode() * 397) ^ Slot; }
        }
    }

    private static readonly object Sync = new object();
    private static Dictionary<PendingKey,PendingTransfer> Pending = new Dictionary<PendingKey,PendingTransfer>();
    [ThreadStatic] private static bool replayingVanilla;

    public static bool IsReplayingVanilla { get { return replayingVanilla; } }

    public static bool TryInterceptSlot(XUi xui, int slotIndex, bool takeAllFallback)
    {
        if (replayingVanilla || !RebirthDogInventoryPolicy.AutoLootEnabled || xui == null) return false;
        if(!CanAdmitAutoLoot(xui))return true;
        TEFeatureStorage source = xui.LootContainer;
        EntityPlayerLocal player = xui.playerUI?.entityPlayer;
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentLocalPlayer();
        if (source == null || player == null || persistent?.PrimaryId == null || source.ItemGrid.items == null ||
            slotIndex < 0 || slotIndex >= source.ItemGrid.items.Length) return false;

        ItemStack current = source.ItemGrid.items[slotIndex];
        if (current == null || current.IsEmpty() || current.count <= 0 ||
            !RebirthDogInventoryPolicy.IsDogAutoLootItem(current.itemValue) &&
            !IsNamedSmartLootSource(source)) return false;

        // Bind the request to the complete native item image. A same-type,
        // same-count replacement can carry different mods, quality or metadata.
        string requestedImage;
        try { requestedImage = RebirthNativeItemCodec.Encode(current.itemValue); }
        catch { return false; }
        if (string.IsNullOrEmpty(requestedImage) || requestedImage.Length > 65536) return false;

        PendingKey key = new PendingKey(source.ToWorldPos(), slotIndex);
        Guid operationId = Guid.NewGuid();
        lock (Sync)
        {
            PendingTransfer existing;
            if (Pending.TryGetValue(key, out existing)) return true;
            Pending[key] = CapturePendingAutoLoot(operationId,xui,player,source,current);
        }

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null)
        {
            RemovePending(key, operationId);
            return false;
        }

        if (connection.IsServer)
        {
            RebirthDogAutoLootResult result = ProcessServer(GameManager.Instance.World, player.entityId, persistent.PrimaryId,
                key.Position, slotIndex, current.itemValue.type, current.count, requestedImage, out string remainderFrame);
            ReceiveResult(operationId, key.Position, slotIndex, current.itemValue.type, current.count, takeAllFallback, result, remainderFrame);
        }
        else
        {
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthDogAutoLootRequest>()
                .Setup(operationId, player.entityId, persistent.PrimaryId, key.Position, slotIndex,
                    current.itemValue.type, current.count, requestedImage, takeAllFallback));
        }
        return true;
    }

    public static void ReceiveResult(
        Guid operationId,
        Vector3i position,
        int slotIndex,
        int expectedItemType,
        int expectedCount,
        bool takeAllFallback,
        RebirthDogAutoLootResult result,
        string remainderFrame = null)
    {
        PendingKey key = new PendingKey(position, slotIndex);
        string pendingImage;
        lock (Sync)
        {
            PendingTransfer pendingOperation;
            if (!Pending.TryGetValue(key, out pendingOperation) || pendingOperation.Operation != operationId||!PendingAutoLootCurrent(pendingOperation)) return;
            var slots=pendingOperation.Source.ItemGrid.items;
            if(slots==null||slotIndex<0||slotIndex>=slots.Length)return;
            var observed=slots[slotIndex];
            bool original=observed==null||observed.IsEmpty()||
                observed.count==pendingOperation.Count&&
                RebirthNativeItemCodec.Encode(observed.itemValue)==pendingOperation.Item;
            bool partialSeen=result==RebirthDogAutoLootResult.RoutedToHumanPartial&&
                observed!=null&&!observed.IsEmpty()&&
                RebirthHumanEquipment.Frame(observed)==remainderFrame;
            if(!original&&!partialSeen)return;
            pendingImage = pendingOperation.Item;
            Pending.Remove(key);
        }

        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        XUi xui = ui?.xui;
        TEFeatureStorage source = xui?.LootContainer;
        if (source == null || source.ToWorldPos() != position || source.ItemGrid.items == null ||
            slotIndex < 0 || slotIndex >= source.ItemGrid.items.Length) return;

        if (result == RebirthDogAutoLootResult.Rejected ||
            result == RebirthDogAutoLootResult.PendingHuman ||
            result == RebirthDogAutoLootResult.PendingDog) return;

        ItemStack current = source.ItemGrid.items[slotIndex];
        if (result == RebirthDogAutoLootResult.RoutedToDog ||
            result == RebirthDogAutoLootResult.RoutedToHuman)
        {
            // Server already owns the authoritative mutation. This local mirror update
            // removes UI latency; the normal tile-entity sync will confirm the same state.
            if (current == null || current.IsEmpty() ||
                (current.count == expectedCount &&
                 RebirthNativeItemCodec.Encode(current.itemValue) == pendingImage))
                source.UpdateSlot(slotIndex, ItemStack.Empty);
            if (takeAllFallback) TryCloseCompletedTakeAll(xui, source, position);
            return;
        }

        if (result == RebirthDogAutoLootResult.RoutedToHumanPartial)
        {
            // Mirror only a typed, server-proven remainder of the same native
            // ItemValue. Tile sync remains the authoritative final image.
            ItemStack remainder;
            try { remainder = RebirthHumanEquipment.Decode(remainderFrame); }
            catch { return; }
            if (remainder.IsEmpty() || remainder.count <= 0 || remainder.count >= expectedCount ||
                RebirthNativeItemCodec.Encode(remainder.itemValue) != pendingImage)
                return;
            if (current != null && !current.IsEmpty() &&
                RebirthNativeItemCodec.Encode(current.itemValue) == pendingImage &&
                current.count == expectedCount)
            {
                source.UpdateSlot(slotIndex, remainder);
                source.SetModified();
            }
            // A visible Take All may continue with the saved remainder only
            // after the previous native original reached terminal. Defer one
            // frame to avoid recursive request processing on a local host.
            if (takeAllFallback)
                ThreadManager.StartCoroutine(ContinueTakeAllRemainder(xui, source,
                    position, slotIndex, remainderFrame));
            return;
        }

        // Server did not mutate the source. Only replay vanilla handling when the same
        // authoritative item is still in the same open slot. A stale response therefore
        // cannot move a replacement item that appeared later.
        if (current == null || current.IsEmpty() || current.itemValue.type != expectedItemType ||
            current.count != expectedCount) return;
        try { if (RebirthNativeItemCodec.Encode(current.itemValue) != pendingImage) return; }
        catch { return; }

        replayingVanilla = true;
        try
        {
            if (takeAllFallback)
            {
                ItemStack give = current.Clone();
                if (!xui.PlayerInventory.AddItem(give))
                    xui.PlayerInventory.DropItem(give);
                source.UpdateSlot(slotIndex, ItemStack.Empty);
                source.SetModified();
                TryCloseCompletedTakeAll(xui, source, position);
                return;
            }

            XUiC_LootWindowGroup group = XUiC_LootWindowGroup.GetInstance(xui);
            XUiC_ItemStack[] controllers = group?.lootWindow?.lootContainer?.GetItemStackControllers();
            if (controllers == null || slotIndex < 0 || slotIndex >= controllers.Length || controllers[slotIndex] == null)
                return;
            controllers[slotIndex].HandleMoveToPreferredLocation();
        }
        finally
        {
            replayingVanilla = false;
        }
    }

    private static void RemovePending(PendingKey key, Guid operationId)
    {
        lock (Sync)
        {
            PendingTransfer pendingOperation;
            if (Pending.TryGetValue(key, out pendingOperation) && pendingOperation.Operation == operationId&&PendingAutoLootCurrent(pendingOperation)) Pending.Remove(key);
        }
    }

    private static bool HasPendingForPosition(Vector3i position)
    {
        lock (Sync)
        {
            foreach (PendingKey key in Pending.Keys)
                if (key.Position == position) return true;
        }
        return false;
    }

    private static void TryCloseCompletedTakeAll(XUi xui, TEFeatureStorage source, Vector3i position)
    {
        if (xui == null || source == null || HasPendingForPosition(position) || source.ItemGrid.items == null) return;
        for (int i = 0; i < source.ItemGrid.items.Length; i++)
            if (source.ItemGrid.items[i] != null && !source.ItemGrid.items[i].IsEmpty()) return;

        try
        {
            XUiC_LootWindowGroup group = XUiC_LootWindowGroup.GetInstance(xui);
            XUiC_LootWindow loot = group?.lootWindow;
            if (loot != null && !loot.isClosing)
                ThreadManager.StartCoroutine(loot.closeInventoryLater());
        }
        catch { }
    }

    private static System.Collections.IEnumerator ContinueTakeAllRemainder(XUi xui,
        TEFeatureStorage source, Vector3i position, int slot, string remainderFrame)
    {
        yield return null;
        if (xui == null || source == null || !ReferenceEquals(xui.LootContainer, source) ||
            source.ToWorldPos() != position || source.ItemGrid?.items == null ||
            slot < 0 || slot >= source.ItemGrid.items.Length ||
            RebirthHumanEquipment.Frame(source.ItemGrid.items[slot]) != remainderFrame)
            yield break;
        TryInterceptSlot(xui, slot, true);
    }

    public static void ResetForWorldChange()
    {
        if(!CanRetireEmptyAutoLoot)throw new InvalidOperationException("Original auto-loot callback custody requires typed retirement");
    }

    internal static RebirthDogAutoLootResult ProcessServer(
        World world,
        int playerId,
        PlatformUserIdentifierAbs userId,
        Vector3i position,
        int slotIndex,
        int expectedItemType,
        int expectedCount,
        string expectedItemImage,
        out string remainderFrame)
    {
        remainderFrame = null;
        if (!ThreadManager.IsMainThread()||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||!ReferenceEquals(world,GameManager.Instance?.World)||world == null || world.IsRemote() || userId == null || !RebirthDogInventoryPolicy.AutoLootEnabled)
            return RebirthDogAutoLootResult.Rejected;

        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerId);
        if (player == null || persistent?.PrimaryId == null || !persistent.PrimaryId.Equals(userId)) return RebirthDogAutoLootResult.Rejected;

        TileEntity tileEntity = world.GetTileEntity(position);
        TEFeatureStorage source = tileEntity?.GetSelfOrFeature<TEFeatureStorage>();
        if (source == null || source.ItemGrid.items == null || source.ToWorldPos() != position ||
            slotIndex < 0 || slotIndex >= source.ItemGrid.items.Length) return RebirthDogAutoLootResult.Rejected;

        if (!PlayerOwnsServerLootLock(source, playerId)) return RebirthDogAutoLootResult.Rejected;

        float maxDistance = Constants.cCollectItemDistance + 30f;
        if ((player.position - source.ToWorldCenterPos()).sqrMagnitude > maxDistance * maxDistance) return RebirthDogAutoLootResult.Rejected;

        ItemStack current = source.ItemGrid.items[slotIndex];
        if (current == null || current.IsEmpty() || current.count <= 0 ||
            current.itemValue.type != expectedItemType || current.count != expectedCount)
            return RebirthDogAutoLootResult.Rejected;
        if (string.IsNullOrEmpty(expectedItemImage) || expectedItemImage.Length > 65536)
            return RebirthDogAutoLootResult.Rejected;
        try { if (RebirthNativeItemCodec.Encode(current.itemValue) != expectedItemImage)
            return RebirthDogAutoLootResult.Rejected; }
        catch { return RebirthDogAutoLootResult.Rejected; }

        // The named static source is the only current Smart Looting original
        // with a durable source incarnation. Its backend either proves an
        // exact whole-stack human credit or retains the native slot; neither
        // outcome may fall through to vanilla Take All.
        // Unified ordering (gated): a same-owner dog that outranks every human cell skips the human leg.
        // The selector runs FIRST and decides named-source status itself (an unnamed source yields
        // Dog or None only); the human leg runs only for a named source whose winner is not Dog.
        var winner = RebirthP11SmartLootGate.UnifiedOrderingQualified
            ? RebirthP11UnifiedSelector.Choose(player, source, slotIndex, out _)
            : RebirthP11UnifiedSelector.Winner.None;
        bool named = IsNamedSmartLootSource(source);
        bool humanSkipped = named && winner == RebirthP11UnifiedSelector.Winner.Dog;
        RebirthDogAutoLootResult humanResult;
        if (named && !humanSkipped &&
            TryHumanLeg(player, source, slotIndex, out humanResult, out remainderFrame)) return humanResult;

        // Dog winner whose dog leg cannot act (not a dog auto-loot item, journaled leg NotEligible, legacy
        // target gone): the human candidate is retried once, so the stack is never handed to vanilla while
        // a same-owner human could take it. A NotEligible dog leg wrote no row and debited nothing, so the
        // retry cannot double-route; Pending is never retried (the dog row still owns the slot).
        if (!RebirthDogInventoryPolicy.IsDogAutoLootItem(current.itemValue))
        {
            if (humanSkipped && TryHumanLeg(player, source, slotIndex, out humanResult, out remainderFrame))
                return humanResult;
            return RebirthDogAutoLootResult.VanillaFallback;
        }

        // Journaled dog leg (saved row, write-ahead, atomic dog credit, restart recovery). Active only
        // when its qualification gate is on AND the source-custody owner supplied the source lease;
        // then the legacy source-first debit below is never reached for a tile source.
        if (RebirthDogSmartLootTransfer.Active)
        {
            switch (RebirthDogSmartLootTransfer.TryRouteOne(player, source, slotIndex, out _))
            {
                case RebirthDogSmartLootTransfer.RouteResult.Completed: return RebirthDogAutoLootResult.RoutedToDog;
                case RebirthDogSmartLootTransfer.RouteResult.Pending: return RebirthDogAutoLootResult.PendingDog;
                default:
                    if (humanSkipped && TryHumanLeg(player, source, slotIndex, out humanResult, out remainderFrame))
                        return humanResult;
                    return RebirthDogAutoLootResult.VanillaFallback;
            }
        }

        // Legacy 2.6 contract (non-journaled; used while the journaled leg is not qualified): source-first
        // debit with compensation. NOT crash safe across the source chunk and dog aggregate saves.
        ItemStack award = current.Clone();
        RebirthNpcStableId dogId; uint dogRevision;
        if (!RebirthDogInventoryPolicy.TrySelectAutoRouteTarget(player, award, out dogId, out dogRevision))
        {
            if (humanSkipped && TryHumanLeg(player, source, slotIndex, out humanResult, out remainderFrame))
                return humanResult;
            return RebirthDogAutoLootResult.VanillaFallback;
        }

#if DEBUG
        Dictionary<string,long> auditBefore=null;
        try{auditBefore=AuditTotals(RebirthDogInventoryService.GetSnapshot(dogId).Slots,award);}catch{}
#endif

        source.UpdateSlot(slotIndex, ItemStack.Empty);
        source.SetModified();
        int added;
        if (RebirthDogInventoryService.TryAddAtRevision(dogId, dogRevision, award, out added) && added == award.count)
        {
#if DEBUG
            var audit=Audit.GetOrCreateValue(player);audit.Routed++;audit.Conserved=false;
            try
            {
                var auditAfter=AuditTotals(RebirthDogInventoryService.GetSnapshot(dogId).Slots,source.ItemGrid.items[slotIndex]);
                bool same=auditBefore!=null&&auditBefore.Count==auditAfter.Count;
                if(same)foreach(var pair in auditBefore)if(!auditAfter.TryGetValue(pair.Key,out var count)||count!=pair.Value){same=false;break;}
                audit.Conserved=same;
            }
            catch{}
            if(!audit.Conserved)audit.ConservationFailures++;
#endif
            return RebirthDogAutoLootResult.RoutedToDog;
        }

        ItemStack after = source.ItemGrid.items != null && slotIndex >= 0 && slotIndex < source.ItemGrid.items.Length ? source.ItemGrid.items[slotIndex] : null;
        if (after == null || after.IsEmpty())
        {
            source.UpdateSlot(slotIndex, award);
            source.SetModified();
        }
        else
        {
            ItemStack compensation = award.Clone();
            if (!(player.bag.AddItem(compensation) || player.inventory.AddItem(compensation)))
                GameManager.Instance.ItemDropServer(compensation, player.position + Vector3.up, Vector3.zero, player.entityId, 60f, false);
        }
        return RebirthDogAutoLootResult.Rejected;
    }

    // The named-source human leg. True = the slot is settled or owned by a saved human row (caller returns
    // 'result'); false = NotEligible, nothing was debited, the caller may try another leg.
    private static bool TryHumanLeg(EntityPlayer player, TEFeatureStorage source, int slotIndex,
        out RebirthDogAutoLootResult result, out string remainderFrame)
    {
        result = RebirthDogAutoLootResult.VanillaFallback;
        var human = RebirthSmartLootingHumanTransfer.TryRouteOne(player, source, slotIndex, out remainderFrame);
        if (human == RebirthSmartLootingHumanTransfer.RouteResult.Completed)
        {
            result = string.IsNullOrEmpty(remainderFrame) ? RebirthDogAutoLootResult.RoutedToHuman :
                remainderFrame.Length <= 65536 ? RebirthDogAutoLootResult.RoutedToHumanPartial :
                RebirthDogAutoLootResult.PendingHuman;
            return true;
        }
        if (human == RebirthSmartLootingHumanTransfer.RouteResult.Pending)
        {
            result = RebirthDogAutoLootResult.PendingHuman;
            return true;
        }
        remainderFrame = null;
        return false;
    }

    private static bool PlayerOwnsServerLootLock(ILockTarget source, int playerId)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (source == null || connection == null || !connection.IsServer) return false;
        LockManager.LockEntry entry = new LockManager.LockEntry(source, 0);
        if (source.IsSharedLock(0))
        {
            IReadOnlyCollection<int> owners;
            if (!LockManager.Instance.sharedLocks.TryGetByValue(entry, out owners) || owners == null) return false;
            foreach (int owner in owners) if (owner == playerId) return true;
            return false;
        }
        int lockOwner;
        return LockManager.Instance.singleLocks.TryGetByValue(entry, out lockOwner) && lockOwner == playerId;
    }

    internal static bool IsNamedSmartLootSource(TEFeatureStorage source) =>
        SmartLootingLiveRouteQualified && RebirthSandboxOptionManager.Current.SmartLooting &&
        (RebirthP11StorageIncarnation.TryResolve(GameManager.Instance?.World, source, false, out _) ||
            // A remote client cannot read the server-only generated-storage marker; it forwards the slot
            // and the server answers VanillaFallback when the source is not routable.
            RebirthP11SmartLootGate.GeneratedStorageQualified && source != null &&
            !(SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer ?? true));
}

[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleMoveToPreferredLocation))]
public static class RebirthDogAutoLootItemStackPatch
{
    public static bool Prefix(XUiC_ItemStack __instance)
    {
        if (__instance == null || RebirthDogAutoLootBridge.IsReplayingVanilla ||
            __instance.StackLocation != XUiC_ItemStack.StackLocationTypes.LootContainer) return true;
        return !RebirthDogAutoLootBridge.TryInterceptSlot(__instance.xui, __instance.SlotNumber, false);
    }
}

[HarmonyPatch(typeof(XUiM_LootContainer), nameof(XUiM_LootContainer.StashItems))]
public static class RebirthDogAutoLootStashItemsPatch
{
    public static void Prefix(
        XUiController _srcWindow,
        XUiC_ItemStackGrid _srcGrid,
        IInventory _dstInventory,
        int _ignoreSlots,
        ref PackedBoolArray _ignoredSlots,
        XUiM_LootContainer.EItemMoveKind _moveKind,
        out bool __state)
    {
        __state = false;
        // 3.1's visible Loot "Take All" control does not call TakeAll(); it calls
        // ContainerStandardControls.MoveAll -> StashItems(All). Intercept only that
        // exact full-transfer path. Fill/smart operations retain their vanilla semantics.
        if (RebirthDogAutoLootBridge.IsReplayingVanilla || !RebirthDogInventoryPolicy.AutoLootEnabled ||
            _moveKind != XUiM_LootContainer.EItemMoveKind.All || !(_srcWindow is XUiC_LootWindow) ||
            !(_dstInventory is XUiM_PlayerInventory) || _srcGrid == null || _srcWindow?.xui == null) return;

        XUiC_ItemStack[] controllers = _srcGrid.GetItemStackControllers();
        if (controllers == null || controllers.Length == 0) return;

        PackedBoolArray effectiveIgnored = _ignoredSlots?.Clone() ?? new PackedBoolArray(controllers.Length);
        if (effectiveIgnored.Length < controllers.Length) effectiveIgnored.Length = controllers.Length;

        bool changed = false;
        for (int slot = 0; slot < controllers.Length; slot++)
        {
            if (slot < _ignoreSlots || effectiveIgnored[slot]) continue;
            XUiC_ItemStack controller = controllers[slot];
            if (controller == null || controller.StackLock) continue;

            ItemStack stack = controller.ItemStack;
            if (stack == null || stack.IsEmpty() ||
                !RebirthDogInventoryPolicy.IsDogAutoLootItem(stack.itemValue) &&
                !RebirthDogAutoLootBridge.IsNamedSmartLootSource(_srcWindow.xui.LootContainer)) continue;

            if (RebirthDogAutoLootBridge.TryInterceptSlot(_srcWindow.xui, slot, true))
            {
                // Prevent vanilla StashItems from also moving this slot while the
                // server-authoritative dog-routing request is outstanding.
                effectiveIgnored[slot] = true;
                changed = true;
            }
        }

        if (changed)
        {
            _ignoredSlots = effectiveIgnored;
            __state = true;
        }
    }

    public static void Postfix(bool __state, ref ValueTuple<bool, bool> __result)
    {
        if (!__state) return;
        // Keep the Loot window open while eligible slots await authoritative
        // server results. ReceiveResult closes it after the final successful
        // route/fallback empties the source. Count the intercepted transfer as
        // activity so vanilla Take All audio/callouts still behave naturally.
        __result.Item1 = false;
        __result.Item2 = true;
    }
}

[Preserve]
public sealed class NetPackageRebirthDogAutoLootRequest : NetPackage
{
    private Guid operationId;
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private Vector3i position;
    private int slotIndex;
    private int expectedItemType;
    private int expectedCount;
    private string expectedItemImage;
    private bool takeAllFallback;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthDogAutoLootRequest Setup(
        Guid requestedOperationId,
        int entityId,
        PlatformUserIdentifierAbs persistentUserId,
        Vector3i sourcePosition,
        int sourceSlot,
        int itemType,
        int itemCount,
        string itemImage,
        bool fromTakeAll)
    {
        operationId = requestedOperationId;
        playerId = entityId;
        userId = persistentUserId;
        position = sourcePosition;
        slotIndex = sourceSlot;
        expectedItemType = itemType;
        expectedCount = itemCount;
        expectedItemImage = itemImage;
        takeAllFallback = fromTakeAll;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        operationId = new Guid(((BinaryReader)reader).ReadBytes(16));
        playerId = reader.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream((BinaryReader)reader);
        position = StreamUtils.ReadVector3i((BinaryReader)reader);
        slotIndex = reader.ReadInt32();
        expectedItemType = reader.ReadInt32();
        expectedCount = reader.ReadInt32();
        expectedItemImage = RebirthSurvivorNetworkCodec.ReadBoundedString((BinaryReader)reader, 65536);
        takeAllFallback = reader.ReadBoolean();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(operationId.ToByteArray());
        binary.Write(playerId);
        userId.ToStream(binary);
        StreamUtils.Write(binary, position);
        binary.Write(slotIndex);
        binary.Write(expectedItemType);
        binary.Write(expectedCount);
        binary.Write(expectedItemImage ?? string.Empty);
        binary.Write(takeAllFallback);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || userId == null ||
            !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId)) return;

        RebirthDogAutoLootResult result = RebirthDogAutoLootBridge.ProcessServer(
            world, playerId, userId, position, slotIndex, expectedItemType, expectedCount,
            expectedItemImage,
            out string remainderFrame);

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        NetPackageRebirthDogAutoLootResponse response = NetPackageManager
            .GetPackage<NetPackageRebirthDogAutoLootResponse>()
            .Setup(operationId, position, slotIndex, expectedItemType, expectedCount,
                takeAllFallback, result, remainderFrame);
        if (connection != null && connection.IsServer && world.GetPrimaryPlayerId() != playerId)
            connection.SendPackage(response, _attachedToEntityId: playerId);
        else
            response.ProcessPackage(world, callbacks);
    }

    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthDogAutoLootResponse : NetPackage
{
    private Guid operationId;
    private Vector3i position;
    private int slotIndex;
    private int expectedItemType;
    private int expectedCount;
    private bool takeAllFallback;
    private RebirthDogAutoLootResult result;
    private string remainderFrame;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthDogAutoLootResponse Setup(
        Guid requestedOperationId,
        Vector3i sourcePosition,
        int sourceSlot,
        int itemType,
        int itemCount,
        bool fromTakeAll,
        RebirthDogAutoLootResult value,
        string sourceRemainder)
    {
        operationId = requestedOperationId;
        position = sourcePosition;
        slotIndex = sourceSlot;
        expectedItemType = itemType;
        expectedCount = itemCount;
        takeAllFallback = fromTakeAll;
        result = value;
        remainderFrame = value == RebirthDogAutoLootResult.RoutedToHumanPartial &&
            sourceRemainder != null && sourceRemainder.Length <= 65536 ?
                sourceRemainder : string.Empty;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        operationId = new Guid(((BinaryReader)reader).ReadBytes(16));
        position = StreamUtils.ReadVector3i((BinaryReader)reader);
        slotIndex = reader.ReadInt32();
        expectedItemType = reader.ReadInt32();
        expectedCount = reader.ReadInt32();
        takeAllFallback = reader.ReadBoolean();
        result = (RebirthDogAutoLootResult)reader.ReadByte();
        remainderFrame = RebirthSurvivorNetworkCodec.ReadBoundedString((BinaryReader)reader, 65536);
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(operationId.ToByteArray());
        StreamUtils.Write(binary, position);
        binary.Write(slotIndex);
        binary.Write(expectedItemType);
        binary.Write(expectedCount);
        binary.Write(takeAllFallback);
        binary.Write((byte)result);
        binary.Write(remainderFrame ?? string.Empty);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthDogAutoLootBridge.ReceiveResult(
            operationId, position, slotIndex, expectedItemType, expectedCount,
            takeAllFallback, result, remainderFrame);
    }

    public int GetLength() => 0;
}
