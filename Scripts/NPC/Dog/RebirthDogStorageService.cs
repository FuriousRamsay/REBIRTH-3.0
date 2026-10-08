using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#nullable disable

/// <summary>
/// Bridges the canonical ten-slot RebirthDogInventoryService inventory to the base-game
/// entity bag/loot-container UI. The aggregate remains authoritative; the native Bag is
/// only a synchronized edit surface protected by the normal Entity lock protocol.
/// </summary>
public static class RebirthDogStorageService
{
    public const string LockCommand = "rebirthDogStorage";
    public const string LootContainerName = "rebirthDogCompanionStorage";

    private static readonly object Sync = new object();
    private static readonly Dictionary<int, uint> OpenServerEntities = new Dictionary<int, uint>();
    private static bool initialized;

    public static void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        Harmony harmony = new Harmony("rebirth.dog.native.storage.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDogNativeBagSyncPatch));
    }

    // Installed lock context is streamed; all dog storage uses native channel zero.
    public static bool IsStorageContext(ushort channel) { return channel == 0; }

    /// <summary>
    /// Populates the server-side native bag from the canonical dog inventory before
    /// Entity.OnLockedServer serializes that bag back to the requesting client.
    /// </summary>
    public static bool EnsureServerContainer(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null) return false;
        RebirthDogInventorySnapshot snapshot = RebirthDogInventoryService.GetSnapshot(dog.RebirthRuntimeState.StableId);
        if (snapshot == null) return false;

        ItemStack[] slots = new ItemStack[RebirthDogInventoryService.Capacity];
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack source = snapshot.Slots != null && i < snapshot.Slots.Length ? snapshot.Slots[i] : null;
            slots[i] = source != null ? source.Clone() : ItemStack.Empty.Clone();
        }

        if (dog.bag == null || dog.bag.SlotCount != RebirthDogInventoryService.Capacity)
            dog.bag = new Bag(new Vector2i(RebirthDogInventoryService.Capacity, 1), XUiC_ItemStack.StackLocationTypes.Backpack, dog);
        dog.bag.SetSlots(slots);
        dog.bag.ItemGrid.SetSlotLocks(snapshot.Locks != null
            ? snapshot.Locks.Clone()
            : new PackedBoolArray(RebirthDogInventoryService.Capacity));
        // This is player storage, never random loot. Marking it touched prevents the base
        // loot manager from trying to populate the empty shell when the lock is granted.
        dog.bag.ItemGrid.Touch();
        return true;
    }

    public static bool PrepareServerLock(EntityRebirthDogCompanion dog, int playerEntityId, out string reason)
    {
        reason = string.Empty;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (connection == null || !connection.IsServer || world == null || dog == null)
        {
            reason = "storage authority unavailable";
            return false;
        }

        EntityPlayer player = world.GetEntity(playerEntityId) as EntityPlayer;
        if (player == null || !RebirthDogLifecycleService.IsOwnedBy(dog, player))
        {
            reason = "companion ownership denied";
            return false;
        }
        if ((dog.position - player.position).sqrMagnitude >
            RebirthCompanionService.StorageDistance * RebirthCompanionService.StorageDistance)
        {
            reason = "companion storage is out of range";
            return false;
        }

        RebirthNpcPersistentRecordView record;
        RebirthDogPersistentRecordView dogRecord;
        if (!RebirthDogStateService.TryGetView(dog.RebirthRuntimeState.StableId, out record, out dogRecord) ||
            dogRecord == null || dogRecord.Lifecycle == RebirthDogLifecycleKind.PickedUp ||
            dogRecord.Lifecycle == RebirthDogLifecycleKind.Removed)
        {
            reason = "companion storage is unavailable";
            return false;
        }
        return EnsureServerContainer(dog);
    }

    public static bool OpenLocal(EntityPlayerLocal player, EntityRebirthDogCompanion dog, out string reason)
    {
        reason = string.Empty;
        if (player == null || dog == null || dog.IsDead() || dog.RebirthRuntimeState == null)
        {
            reason = Localization.Get("xuiRebirthDogInteractionUnavailable");
            return false;
        }
        if (!RebirthDogLifecycleService.IsOwnedBy(dog, player))
        {
            reason = Localization.Get("xuiRebirthDogInteractionUnavailable");
            return false;
        }
        if ((dog.position - player.position).sqrMagnitude >
            RebirthCompanionService.StorageDistance * RebirthCompanionService.StorageDistance)
        {
            reason = Localization.Get("xuiRebirthStorageDistanceRequirement");
            return false;
        }

        LocalPlayerUI playerUi = LocalPlayerUI.GetUIForPlayer(player);
        if (playerUi == null || playerUi.xui == null)
        {
            reason = Localization.Get("xuiRebirthDogInteractionUnavailable");
            return false;
        }

        // The lock response will replace this client mirror with the authoritative server
        // bag. Keeping a correctly sized shell here also makes CanLockLocally deterministic.
        if (dog.bag == null || dog.bag.SlotCount != RebirthDogInventoryService.Capacity)
            dog.bag = new Bag(new Vector2i(RebirthDogInventoryService.Capacity, 1), XUiC_ItemStack.StackLocationTypes.Backpack, dog);

        playerUi.windowManager.Close(RebirthCompanionUiService.Group);
        playerUi.windowManager.Close(RebirthCompanionInteractionUiService.Group);

        // Calling LockManager.LockRequestLocal directly makes the compiler inspect its 3.1
        // ReadOnlySpan<ILockTarget> overload and produces CS7069 in RebirthUtils. Invoke the
        // verified single-target overload by reflection so no Span type crosses our compile boundary.
        if (!RequestLocalEntityLock(dog))
        {
            reason = Localization.Get("xuiRebirthDogInteractionUnavailable");
            return false;
        }
        return true;
    }

    private static MethodInfo lockRequestLocalSingleTarget;

    private static bool RequestLocalEntityLock(ILockTarget target)
    {
        try
        {
            if (target == null || LockManager.Instance == null) return false;
            if (lockRequestLocalSingleTarget == null)
            {
                lockRequestLocalSingleTarget = typeof(LockManager).GetMethod(
                    "LockRequestLocal",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new Type[] { typeof(ILockTarget), typeof(ushort) },
                    null);
            }
            if (lockRequestLocalSingleTarget == null)
            {
                Log.Error("[REBIRTH Dog] Unable to resolve the installed single-target LockRequestLocal overload.");
                return false;
            }
            lockRequestLocalSingleTarget.Invoke(
                LockManager.Instance,
                new object[] { target, (ushort)0 });
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Dog] Native storage lock request failed: " + ex.Message);
            return false;
        }
    }

    // Retained compatibility signature for older callers. Native storage is initiated
    // locally because LockManager owns client/server lock negotiation.
    public static bool OpenServer(EntityPlayer player, EntityRebirthDogCompanion dog, out string reason)
    {
        reason = string.Empty;
        EntityPlayerLocal local = player as EntityPlayerLocal;
        if (local == null)
        {
            reason = "native dog storage must be opened by the owning local player";
            return false;
        }
        return OpenLocal(local, dog, out reason);
    }

    public static void OnLockedServer(EntityRebirthDogCompanion dog, int playerEntityId)
    {
        if (dog == null) return;
        RebirthDogInventorySnapshot opened = dog.RebirthRuntimeState != null
            ? RebirthDogInventoryService.GetSnapshot(dog.RebirthRuntimeState.StableId) : null;
        lock (Sync) OpenServerEntities[dog.entityId] = opened != null ? opened.Revision : 0u;

        // An actively edited companion bag is not an eligible remote-resource source.
        // Push that busy-state transition immediately so nearby ingredient UIs do not
        // advertise inventory which the authoritative transaction path will reject.
        if (dog.RebirthRuntimeState != null)
            RemoteResourceLiveSync.NotifySourceChanged(
                "N:" + dog.RebirthRuntimeState.StableId, dog.position, "dog native storage opened");
    }

    public static bool OnLockedLocal(
        EntityRebirthDogCompanion dog,
        bool success,
        PooledBinaryReader context,
        ushort channel)
    {

        LocalPlayerUI playerUi = LocalPlayerUI.GetUIForPrimaryPlayer();
        if (dog == null || dog.bag == null || !success || playerUi == null || playerUi.xui == null)
        {
            if (playerUi != null)
                GameManager.ShowTooltip(playerUi.entityPlayer, Localization.Get("ttNoInteractItem"), string.Empty, "ui_denied");
            return false;
        }

        // Match Entity.OnLockResponseServer before opening the custom storage shell.
        context.ReadBoolean(); // native first-open timer; companion inventory never randomizes
        dog.bag.ItemGrid.ReadInto(context, StreamModeRead.FromServer);

        // Use the dedicated REBIRTH storage shell added to Config/loot.xml.  Do not use
        // dog.GetLootList(): that is the entity's ordinary loot-list contract and is not the
        // ten-slot companion storage definition.  Passing a null LootContainer causes
        // XUiC_BagContainer.SetBag() to return before assigning Bag, after which the stock
        // LockedSlots binding dereferences a null Bag on the next update.
        LootContainer lootContainer = LootContainer.GetLootContainer(LootContainerName);
        if (lootContainer == null)
        {
            Log.Error("[REBIRTH Dog] Missing loot container definition '" + LootContainerName + "'.");
            LockManager.Instance.UnlockRequestLocal();
            GameManager.ShowTooltip(playerUi.entityPlayer, Localization.Get("xuiRebirthDogInteractionUnavailable"), string.Empty, "ui_denied");
            return false;
        }

        string displayName = string.IsNullOrWhiteSpace(dog.EntityName)
            ? Localization.Get("xuiStorage")
            : dog.EntityName;

        try
        {
            XUiC_BagStorageWindowGroup.Open(
                playerUi.xui,
                dog,
                dog.bag,
                lootContainer,
                displayName,
                delegate { OnLocalBagModified(dog); },
                delegate { LockManager.Instance.UnlockRequestLocal(); },
                delegate
                {
                    EntityPlayerLocal local = playerUi.entityPlayer;
                    return local != null && !dog.IsDead() &&
                           RebirthDogLifecycleService.IsOwnedBy(dog, local) &&
                           (dog.position - local.position).sqrMagnitude <=
                           RebirthCompanionService.StorageDistance * RebirthCompanionService.StorageDistance;
                },
                false);
            dog.bag.ItemGrid.Touch();
            return true;
        }
        catch (Exception ex)
        {
            // Never leave the player's entity lock held when the storage surface fails to
            // construct.  A leaked lock makes every later Storage entry point look unavailable.
            Log.Error("[REBIRTH Dog] Failed to open companion storage UI: " + ex);
            LockManager.Instance.UnlockRequestLocal();
            GameManager.ShowTooltip(playerUi.entityPlayer, Localization.Get("xuiRebirthDogInteractionUnavailable"), string.Empty, "ui_denied");
            return false;
        }
    }

    private static void OnLocalBagModified(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.bag == null) return;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection != null && connection.IsServer)
            CaptureNow(dog);
        else
            dog.OnBagModified();
    }

    /// <summary>
    /// Commits the native bag mirror to the canonical ten-slot dog inventory.
    /// Expected revision 0 is intentional: the Entity lock is the edit lease, while all
    /// other remote-resource consumers treat an open native dog bag as busy.
    /// </summary>
    public static void CaptureNow(EntityRebirthDogCompanion dog)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || dog == null || dog.bag == null ||
            dog.RebirthRuntimeState == null || dog.bag.SlotCount != RebirthDogInventoryService.Capacity) return;

        ItemStack[] source = dog.bag.ItemGrid.items;
        ItemStack[] slots = new ItemStack[RebirthDogInventoryService.Capacity];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = source != null && i < source.Length && source[i] != null
                ? source[i].Clone()
                : ItemStack.Empty.Clone();
        PackedBoolArray locks = dog.bag.LockedSlots != null
            ? dog.bag.LockedSlots.Clone()
            : new PackedBoolArray(RebirthDogInventoryService.Capacity);

        uint expectedRevision;
        lock (Sync)
        {
            if (!OpenServerEntities.TryGetValue(dog.entityId, out expectedRevision)) return;
        }
        string error;
        if (!RebirthDogInventoryService.CommitWorkingSlots(
                dog.RebirthRuntimeState.StableId, expectedRevision, slots, locks, out error))
        {
            Log.Warning("[REBIRTH Dog] native storage commit conflict entity=" + dog.entityId +
                        " stableId=" + dog.RebirthRuntimeState.StableId +
                        " expectedRevision=" + expectedRevision +
                        " reason=" + (error ?? string.Empty));
            // Restore the edit mirror from canonical state instead of overwriting a newer
            // auto-loot/transaction revision with stale editor contents.
            EnsureServerContainer(dog);
            return;
        }
        RebirthDogInventorySnapshot committed = RebirthDogInventoryService.GetSnapshot(dog.RebirthRuntimeState.StableId);
        if (committed != null) lock (Sync) OpenServerEntities[dog.entityId] = committed.Revision;
    }

    public static void OnUnlockedServer(EntityRebirthDogCompanion dog)
    {
        if (dog == null) return;
        CaptureNow(dog);
        lock (Sync) OpenServerEntities.Remove(dog.entityId);

        // CaptureNow invalidates while the bag is still marked Busy. Once the entity lock
        // is released, push one final snapshot so the freshly edited dog inventory becomes
        // eligible again without requiring the Remote Resources toggle to be cycled.
        if (dog.RebirthRuntimeState != null)
            RemoteResourceLiveSync.NotifySourceChanged(
                "N:" + dog.RebirthRuntimeState.StableId, dog.position, "dog native storage closed");
        RebirthNpcAggregatePersistenceStore.Save();
    }

    public static bool IsOpenServer(int entityId)
    {
        lock (Sync) return OpenServerEntities.ContainsKey(entityId);
    }

    public static void Remove(int entityId)
    {
        lock (Sync) OpenServerEntities.Remove(entityId);
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) OpenServerEntities.Clear();
    }
}

/// <summary>
/// Remote clients send the edited native Bag through the base-game NetPackageBag. Once
/// the server has applied that bag to the entity, mirror it into REBIRTH's canonical dog
/// inventory so the Companions UI, healing and Remote Resources see the same contents.
/// </summary>
[HarmonyPatch(typeof(NetPackageBag), nameof(NetPackageBag.ProcessPackage))]
internal static class RebirthDogNativeBagSyncPatch
{
    // Native NetPackageBag trusts entityId and mutates the bag directly. A dog packet
    // must belong to its authenticated owner and currently held edit lease first.
    internal static bool Prefix(NetPackageBag __instance, World _world, out bool __state)
    {
        __state = false;
        if (__instance == null || _world == null) return true;
        var dog = _world.GetEntity(__instance.entityId) as EntityRebirthDogCompanion;
        if (dog == null) return true;
        if (_world.IsRemote() || __instance.Sender == null ||
            !RebirthDogStorageService.IsOpenServer(dog.entityId)) return false;
        var player = _world.GetEntity(__instance.Sender.entityId) as EntityPlayer;
        if (player == null || !RebirthDogLifecycleService.IsOwnedBy(dog, player)) return false;
        int lockOwner;
        if (!LockManager.Instance.singleLocks.TryGetByValue(new LockManager.LockEntry(dog, 0), out lockOwner) ||
            lockOwner != player.entityId) return false;
        __state = true;
        return true;
    }

    private static void Postfix(NetPackageBag __instance, World _world, bool __state)
    {
        if (!__state) return;
        if (__instance == null || _world == null || _world.IsRemote()) return;
        int entityId = __instance.entityId;
        if (!RebirthDogStorageService.IsOpenServer(entityId)) return;
        EntityRebirthDogCompanion dog = _world.GetEntity(entityId) as EntityRebirthDogCompanion;
        if (dog != null) RebirthDogStorageService.CaptureNow(dog);
    }
}
