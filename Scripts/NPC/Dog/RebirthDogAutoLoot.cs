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
    VanillaFallback = 2
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
public static class RebirthDogAutoLootBridge
{
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
    private static readonly Dictionary<PendingKey,Guid> Pending = new Dictionary<PendingKey,Guid>();
    [ThreadStatic] private static bool replayingVanilla;

    public static bool IsReplayingVanilla { get { return replayingVanilla; } }

    public static bool TryInterceptSlot(XUi xui, int slotIndex, bool takeAllFallback)
    {
        if (replayingVanilla || !RebirthDogInventoryPolicy.AutoLootEnabled || xui == null) return false;
        TEFeatureStorage source = xui.LootContainer;
        EntityPlayerLocal player = xui.playerUI?.entityPlayer;
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentLocalPlayer();
        if (source == null || player == null || persistent?.PrimaryId == null || source.ItemGrid.items == null ||
            slotIndex < 0 || slotIndex >= source.ItemGrid.items.Length) return false;

        ItemStack current = source.ItemGrid.items[slotIndex];
        if (current == null || current.IsEmpty() || current.count <= 0 ||
            !RebirthDogInventoryPolicy.IsDogAutoLootItem(current.itemValue)) return false;

        PendingKey key = new PendingKey(source.ToWorldPos(), slotIndex);
        Guid operationId = Guid.NewGuid();
        lock (Sync)
        {
            Guid existing;
            if (Pending.TryGetValue(key, out existing)) return true;
            Pending[key] = operationId;
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
                key.Position, slotIndex, current.itemValue.type, current.count);
            ReceiveResult(operationId, key.Position, slotIndex, current.itemValue.type, current.count, takeAllFallback, result);
        }
        else
        {
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthDogAutoLootRequest>()
                .Setup(operationId, player.entityId, persistent.PrimaryId, key.Position, slotIndex,
                    current.itemValue.type, current.count, takeAllFallback));
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
        RebirthDogAutoLootResult result)
    {
        PendingKey key = new PendingKey(position, slotIndex);
        lock (Sync)
        {
            Guid pendingOperation;
            if (!Pending.TryGetValue(key, out pendingOperation) || pendingOperation != operationId) return;
            Pending.Remove(key);
        }

        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        XUi xui = ui?.xui;
        TEFeatureStorage source = xui?.LootContainer;
        if (source == null || source.ToWorldPos() != position || source.ItemGrid.items == null ||
            slotIndex < 0 || slotIndex >= source.ItemGrid.items.Length) return;

        if (result == RebirthDogAutoLootResult.Rejected) return;

        ItemStack current = source.ItemGrid.items[slotIndex];
        if (result == RebirthDogAutoLootResult.RoutedToDog)
        {
            // Server already owns the authoritative mutation. This local mirror update
            // removes UI latency; the normal tile-entity sync will confirm the same state.
            if (current == null || current.IsEmpty() ||
                (current.itemValue.type == expectedItemType && current.count == expectedCount))
                source.UpdateSlot(slotIndex, ItemStack.Empty);
            if (takeAllFallback) TryCloseCompletedTakeAll(xui, source, position);
            return;
        }

        // Server did not mutate the source. Only replay vanilla handling when the same
        // authoritative item is still in the same open slot. A stale response therefore
        // cannot move a replacement item that appeared later.
        if (current == null || current.IsEmpty() || current.itemValue.type != expectedItemType ||
            current.count != expectedCount) return;

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
            Guid pendingOperation;
            if (Pending.TryGetValue(key, out pendingOperation) && pendingOperation == operationId) Pending.Remove(key);
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

    public static void ResetForWorldChange()
    {
        lock (Sync) Pending.Clear();
        replayingVanilla = false;
    }

    internal static RebirthDogAutoLootResult ProcessServer(
        World world,
        int playerId,
        PlatformUserIdentifierAbs userId,
        Vector3i position,
        int slotIndex,
        int expectedItemType,
        int expectedCount)
    {
        if (world == null || world.IsRemote() || userId == null || !RebirthDogInventoryPolicy.AutoLootEnabled)
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
            current.itemValue.type != expectedItemType || current.count != expectedCount ||
            !RebirthDogInventoryPolicy.IsDogAutoLootItem(current.itemValue)) return RebirthDogAutoLootResult.Rejected;

        ItemStack award = current.Clone();
        RebirthNpcStableId dogId; uint dogRevision;
        if (!RebirthDogInventoryPolicy.TrySelectAutoRouteTarget(player, award, out dogId, out dogRevision))
            return RebirthDogAutoLootResult.VanillaFallback;

        source.UpdateSlot(slotIndex, ItemStack.Empty);
        source.SetModified();
        int added;
        if (RebirthDogInventoryService.TryAddAtRevision(dogId, dogRevision, award, out added) && added == award.count)
            return RebirthDogAutoLootResult.RoutedToDog;

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
                !RebirthDogInventoryPolicy.IsDogAutoLootItem(stack.itemValue)) continue;

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
        bool fromTakeAll)
    {
        operationId = requestedOperationId;
        playerId = entityId;
        userId = persistentUserId;
        position = sourcePosition;
        slotIndex = sourceSlot;
        expectedItemType = itemType;
        expectedCount = itemCount;
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
        binary.Write(takeAllFallback);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || userId == null ||
            !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId)) return;

        RebirthDogAutoLootResult result = RebirthDogAutoLootBridge.ProcessServer(
            world, playerId, userId, position, slotIndex, expectedItemType, expectedCount);

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        NetPackageRebirthDogAutoLootResponse response = NetPackageManager
            .GetPackage<NetPackageRebirthDogAutoLootResponse>()
            .Setup(operationId, position, slotIndex, expectedItemType, expectedCount, takeAllFallback, result);
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

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthDogAutoLootResponse Setup(
        Guid requestedOperationId,
        Vector3i sourcePosition,
        int sourceSlot,
        int itemType,
        int itemCount,
        bool fromTakeAll,
        RebirthDogAutoLootResult value)
    {
        operationId = requestedOperationId;
        position = sourcePosition;
        slotIndex = sourceSlot;
        expectedItemType = itemType;
        expectedCount = itemCount;
        takeAllFallback = fromTakeAll;
        result = value;
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
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthDogAutoLootBridge.ReceiveResult(
            operationId, position, slotIndex, expectedItemType, expectedCount, takeAllFallback, result);
    }

    public int GetLength() => 0;
}
