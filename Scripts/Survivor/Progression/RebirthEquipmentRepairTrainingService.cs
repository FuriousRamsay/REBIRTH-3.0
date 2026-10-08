using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Authoritative completion witness for equipment-repair practical Skill training.
///
/// Personal repair queues execute on the owning client. A remote client therefore reports only
/// which item completed (player/entity, item type, native seed and an idempotency receipt). The
/// client never supplies restored condition or Skill XP. The server pays only when its own recent
/// inventory history independently proves that the same native item changed from a larger UseTimes
/// value to a smaller one. In 7DTD UseTimes is wear/damage, so that decrease is the actual condition
/// restored. The observed transition is consumed after one award, which also makes replayed notices
/// harmless.
/// </summary>
public static class RebirthEquipmentRepairTrainingService
{
    private const float SampleSeconds = 0.25f;
    private const float WitnessRetentionSeconds = 600f;
    private const float PendingSeconds = 30f;
    private const int MaxStateChangesPerItem = 8;
    private const int MaxPendingPerPlayer = 32;
    private const float Epsilon = 0.0005f;

    private sealed class RepairWitness
    {
        public float CapturedAt;
        public float UseTimes;
        public float MaxUseTimes;
        public string ItemName = string.Empty;
    }

    private sealed class RepairHistory
    {
        public float LastSeenAt;
        public readonly List<RepairWitness> States = new List<RepairWitness>(4);
    }

    private sealed class PendingRepair
    {
        public int PlayerId;
        public int ItemType;
        public ushort Seed;
        public long Receipt;
        public float ExpiresAt;
    }

    private static readonly Dictionary<string, RepairHistory> HistoryByItem = new Dictionary<string, RepairHistory>(StringComparer.Ordinal);
    private static readonly Dictionary<string, PendingRepair> PendingByReceipt = new Dictionary<string, PendingRepair>(StringComparer.Ordinal);
    private static readonly Dictionary<string, float> ConsumedReceipts = new Dictionary<string, float>(StringComparer.Ordinal);
    private static bool installed;
    private static float nextSample;
    private static long clientReceiptCounter;
    private static readonly long clientReceiptSession = DateTime.UtcNow.Ticks;

    public static string Install()
    {
        if (installed) return "[REBIRTH Equipment Repair Training] already installed";
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        installed = true;
        return "[REBIRTH Equipment Repair Training] installed";
    }

    /// <summary>
    /// Called only after XUiC_RecipeStack.outputStack reports successful repair completion.
    /// Local/listen-server repairs can use the already-authoritative completed repair amount.
    /// Remote clients send only item identity; the server independently derives restored work.
    /// </summary>
    public static void ReportCompleted(EntityPlayer player, int itemType, ushort seed, string itemName, float restoredUseTimes, float maxUseTimes)
    {
        if (player == null || player.world == null || itemType <= 0 || restoredUseTimes <= 0f || maxUseTimes <= 0f) return;
        World world = player.world;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (world.IsRemote())
        {
            if (connection == null || connection.IsServer) return;
            long receipt = NextClientReceipt(player.entityId);
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthEquipmentRepairCompleted>()
                .Setup(player.entityId, itemType, seed, receipt), false);
            return;
        }

        // Host/single-player execution is already on the authoritative world and the successful
        // repair queue output supplies the actual restored amount. Keep the same central router.
        if (!RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        RebirthSkillEventRouter.OnEquipmentRepairCompleted(player, itemName, restoredUseTimes, maxUseTimes);
    }

    internal static void HandleServerCompletion(EntityPlayer player, int itemType, ushort seed, long receipt)
    {
        if (!IsServerAuthority(player) || itemType <= 0 || receipt == 0L) return;
        float now = Time.realtimeSinceStartup;
        string receiptKey = ReceiptKey(player.entityId, receipt);
        if (ConsumedReceipts.ContainsKey(receiptKey) || PendingByReceipt.ContainsKey(receiptKey)) return;

        if (TryConsumeWitness(player, itemType, seed, now))
        {
            ConsumedReceipts[receiptKey] = now;
            return;
        }

        if (CountPendingForPlayer(player.entityId) >= MaxPendingPerPlayer) return;
        PendingByReceipt[receiptKey] = new PendingRepair
        {
            PlayerId = player.entityId,
            ItemType = itemType,
            Seed = seed,
            Receipt = receipt,
            ExpiresAt = now + PendingSeconds
        };
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data) { ResetRuntime(); }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) { ResetRuntime(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { ResetRuntime(); }

    private static void ResetRuntime()
    {
        HistoryByItem.Clear();
        PendingByReceipt.Clear();
        ConsumedReceipts.Clear();
        nextSample = 0f;
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        if (GameManager.Instance == null || GameManager.Instance.World == null) return;
        World world = GameManager.Instance.World;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (world.IsRemote() || connection == null || !connection.IsServer || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;

        float now = Time.realtimeSinceStartup;
        if (now >= nextSample)
        {
            nextSample = now + SampleSeconds;
            if (world.Players != null && world.Players.list != null)
            {
                List<EntityPlayer> players = world.Players.list;
                for (int i = 0; i < players.Count; i++) CapturePlayerInventory(players[i], now);
            }
            Prune(now);
        }
        ProcessPending(world, now);
    }

    private static void CapturePlayerInventory(EntityPlayer player, float now)
    {
        if (player == null) return;
        CaptureSlots(player.entityId, player.bag != null ? player.bag.ItemGrid.items : null, now);
        CaptureSlots(player.entityId, player.inventory != null ? player.inventory.ItemGrid.items : null, now);
    }

    private static void CaptureSlots(int playerId, ItemStack[] slots, float now)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue == null) continue;
            ItemValue value = stack.itemValue;
            float maxUse = value.MaxUseTimes;
            if (value.type <= 0 || maxUse <= 0f) continue;
            string key = ItemKey(playerId, value.type, value.Seed);
            RepairHistory history;
            if (!HistoryByItem.TryGetValue(key, out history) || history == null)
            {
                history = new RepairHistory();
                HistoryByItem[key] = history;
            }
            history.LastSeenAt = now;
            string itemName = value.ItemClass != null ? value.ItemClass.GetItemName() : string.Empty;
            RepairWitness last = history.States.Count > 0 ? history.States[history.States.Count - 1] : null;
            if (last != null && Mathf.Abs(last.UseTimes - value.UseTimes) <= Epsilon && Mathf.Abs(last.MaxUseTimes - maxUse) <= Epsilon)
            {
                last.CapturedAt = now;
                if (!string.IsNullOrEmpty(itemName)) last.ItemName = itemName;
                continue;
            }
            history.States.Add(new RepairWitness { CapturedAt = now, UseTimes = value.UseTimes, MaxUseTimes = maxUse, ItemName = itemName ?? string.Empty });
            while (history.States.Count > MaxStateChangesPerItem) history.States.RemoveAt(0);
        }
    }

    private static bool TryConsumeWitness(EntityPlayer player, int itemType, ushort seed, float now)
    {
        ItemValue current;
        if (!TryFindInventoryItem(player, itemType, seed, out current) || current == null || current.ItemClass == null || current.MaxUseTimes <= 0f) return false;
        string key = ItemKey(player.entityId, itemType, seed);
        RepairHistory history;
        if (!HistoryByItem.TryGetValue(key, out history) || history == null || history.States.Count == 0) return false;

        RepairWitness before = null;
        // Choose the newest authoritative state that proves actual restoration. The current state can
        // already be present in history because inventory sync may beat the completion packet.
        for (int i = history.States.Count - 1; i >= 0; i--)
        {
            RepairWitness candidate = history.States[i];
            if (candidate == null || now - candidate.CapturedAt > WitnessRetentionSeconds) continue;
            if (candidate.UseTimes > current.UseTimes + Epsilon)
            {
                before = candidate;
                break;
            }
        }
        if (before == null) return false;

        float maxUse = Mathf.Max(current.MaxUseTimes, before.MaxUseTimes);
        float restored = Mathf.Clamp(before.UseTimes - current.UseTimes, 0f, maxUse);
        if (restored <= Epsilon || maxUse <= 0f) return false;
        string itemName = current.ItemClass.GetItemName();

        // Consume the evidence before routing the award. Even if progression is temporarily ineligible,
        // replaying the same native condition transition must never manufacture a second award.
        history.States.Clear();
        history.LastSeenAt = now;
        history.States.Add(new RepairWitness
        {
            CapturedAt = now,
            UseTimes = current.UseTimes,
            MaxUseTimes = current.MaxUseTimes,
            ItemName = itemName ?? string.Empty
        });
        RebirthSkillEventRouter.OnEquipmentRepairCompleted(player, itemName, restored, maxUse);
        return true;
    }

    private static void ProcessPending(World world, float now)
    {
        if (PendingByReceipt.Count == 0 || world == null) return;
        List<string> keys = new List<string>(PendingByReceipt.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            string receiptKey = keys[i];
            PendingRepair pending;
            if (!PendingByReceipt.TryGetValue(receiptKey, out pending) || pending == null) continue;
            if (now > pending.ExpiresAt)
            {
                PendingByReceipt.Remove(receiptKey);
                continue;
            }
            EntityPlayer player = world.GetEntity(pending.PlayerId) as EntityPlayer;
            if (!IsServerAuthority(player)) continue;
            if (!TryConsumeWitness(player, pending.ItemType, pending.Seed, now)) continue;
            PendingByReceipt.Remove(receiptKey);
            ConsumedReceipts[receiptKey] = now;
        }
    }

    private static bool TryFindInventoryItem(EntityPlayer player, int itemType, ushort seed, out ItemValue value)
    {
        value = null;
        if (player == null) return false;
        if (TryFind(player.bag != null ? player.bag.ItemGrid.items : null, itemType, seed, out value)) return true;
        return TryFind(player.inventory != null ? player.inventory.ItemGrid.items : null, itemType, seed, out value);
    }

    private static bool TryFind(ItemStack[] slots, int itemType, ushort seed, out ItemValue value)
    {
        value = null;
        if (slots == null) return false;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue == null) continue;
            if (stack.itemValue.type != itemType || stack.itemValue.Seed != seed) continue;
            value = stack.itemValue;
            return true;
        }
        return false;
    }

    private static bool IsServerAuthority(EntityPlayer player)
    {
        if (player == null || player.world == null || player.world.IsRemote()) return false;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return connection != null && connection.IsServer && RebirthWorldCharacterRepository.IsServerAuthority && RebirthSurvivorMode.IsEnabledForCurrentWorld();
    }

    private static int CountPendingForPlayer(int playerId)
    {
        int count = 0;
        foreach (PendingRepair pending in PendingByReceipt.Values) if (pending != null && pending.PlayerId == playerId) count++;
        return count;
    }

    private static void Prune(float now)
    {
        List<string> removeHistory = new List<string>();
        foreach (KeyValuePair<string, RepairHistory> pair in HistoryByItem)
            if (pair.Value == null || now - pair.Value.LastSeenAt > WitnessRetentionSeconds) removeHistory.Add(pair.Key);
        for (int i = 0; i < removeHistory.Count; i++) HistoryByItem.Remove(removeHistory[i]);

        List<string> removeReceipts = new List<string>();
        foreach (KeyValuePair<string, float> pair in ConsumedReceipts)
            if (now - pair.Value > WitnessRetentionSeconds) removeReceipts.Add(pair.Key);
        for (int i = 0; i < removeReceipts.Count; i++) ConsumedReceipts.Remove(removeReceipts[i]);
    }

    private static long NextClientReceipt(int playerId)
    {
        long sequence = Interlocked.Increment(ref clientReceiptCounter);
        return unchecked(clientReceiptSession ^ ((long)playerId << 32) ^ sequence);
    }

    private static string ItemKey(int playerId, int itemType, ushort seed)
    {
        return playerId.ToString() + ":" + itemType.ToString() + ":" + seed.ToString();
    }

    private static string ReceiptKey(int playerId, long receipt)
    {
        return playerId.ToString() + ":" + receipt.ToString();
    }
}

[Preserve]
public sealed class NetPackageRebirthEquipmentRepairCompleted : NetPackage
{
    private int playerId;
    private int itemType;
    private ushort seed;
    private long receipt;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRebirthEquipmentRepairCompleted Setup(int entityId, int type, ushort itemSeed, long completionReceipt)
    {
        playerId = entityId;
        itemType = type;
        seed = itemSeed;
        receipt = completionReceipt;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        playerId = reader.ReadInt32();
        itemType = reader.ReadInt32();
        seed = (ushort)Mathf.Clamp(reader.ReadInt32(), 0, 65535);
        receipt = reader.ReadInt64();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        // Bind primitive overloads through BinaryWriter, as in the other game network packages.
        BinaryWriter binary = writer;
        binary.Write(playerId);
        binary.Write(itemType);
        binary.Write((int)seed);
        binary.Write(receipt);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || !ValidEntityIdForSender(playerId)) return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        if (player == null) return;
        RebirthEquipmentRepairTrainingService.HandleServerCompletion(player, itemType, seed, receipt);
    }

    public int GetLength() { return 24; }
}
