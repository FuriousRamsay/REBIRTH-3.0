using Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public sealed class QuickStackWorkstationOutputResult
{
    public int ItemsCollected;
    public int OutputStacksChanged;
    public int WorkstationsUsed;
    public int BusySkipped;
    public int UnauthorizedSkipped;
    public int OutputItemsRemaining;
    public long ElapsedMilliseconds;
}

public static class QuickStackWorkstationOutputService
{
    public static void RequestLocal()
    {
        if (!QuickStackRuntimePolicy.Enabled) return;
        EntityPlayerLocal player = GameManager.Instance != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        PersistentPlayerData persistent = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        if (player == null || persistent == null || persistent.PrimaryId == null) return;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection.IsServer)
        {
            QuickStackWorkstationOutputResult result = ProcessServer(GameManager.Instance.World, player.entityId, persistent.PrimaryId);
            ShowResult(result);
        }
        else
        {
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageQuickStackCollectOutputsRequest>()
                .Setup(player.entityId, persistent));
        }
    }

    public static QuickStackWorkstationOutputResult ProcessServer(
        World world, int playerId, PlatformUserIdentifierAbs userId)
    {
        return ProcessServer(world, playerId, userId, string.Empty);
    }

    public static QuickStackWorkstationOutputResult ProcessServer(
        World world, int playerId, PlatformUserIdentifierAbs userId, string selectedWorkstationId)
    {
        return ProcessServerCore(world, playerId, userId, selectedWorkstationId, null);
    }

    // Quick Stack radial selected-mode entry point. An empty selection string means
    // the player unchecked every item, so nothing is collected.
    public static QuickStackWorkstationOutputResult ProcessServerSelected(
        World world, int playerId, PlatformUserIdentifierAbs userId,
        string selectedWorkstationId, string selectedSourceKeys)
    {
        return ProcessServerCore(world, playerId, userId, selectedWorkstationId, selectedSourceKeys ?? string.Empty);
    }

    private static QuickStackWorkstationOutputResult ProcessServerCore(
        World world, int playerId, PlatformUserIdentifierAbs userId,
        string selectedWorkstationId, string selectedSourceKeys)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        QuickStackWorkstationOutputResult result = new QuickStackWorkstationOutputResult();
        EntityPlayer player = world != null ? world.GetEntity(playerId) as EntityPlayer : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId)
            : null;
        if (player == null || persistent == null || persistent.PrimaryId == null ||
            userId == null || !persistent.PrimaryId.Equals(userId) ||
            RebirthSandboxOptionManager.Current.QuickStack == RebirthQuickStackMode.Off)
        {
            stopwatch.Stop();
            result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            return result;
        }

        Dictionary<int, int> selectedLimits = null;
        if (selectedSourceKeys != null)
        {
            selectedLimits = ParseSelectedWorkstationLimits(selectedSourceKeys, selectedWorkstationId);
            if (selectedLimits.Count == 0)
            {
                stopwatch.Stop();
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                return result;
            }
        }

        List<Vector3i> positions = FindNearbyWorkstationPositions(
            world, player.position, QuickStackService.Radius);
        PackedBoolArray protectedSlots = player.bag.LockedSlots;

        for (int i = 0; i < positions.Count; i++)
        {
            WorkstationOutputResourceSource source = new WorkstationOutputResourceSource(world, positions[i]);
            if (!string.IsNullOrEmpty(selectedWorkstationId) &&
                !string.Equals(source.StableId, selectedWorkstationId, StringComparison.Ordinal))
                continue;
            if (!source.IsLoaded) continue;
            if (source.IsBusy)
            {
                result.BusySkipped++;
                continue;
            }
            string reason;
            if (!RemoteResourceEligibility.CanUse(source, player, true, false, out reason))
            {
                if (reason.IndexOf("access", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    reason.IndexOf("owner", StringComparison.OrdinalIgnoreCase) >= 0)
                    result.UnauthorizedSkipped++;
                continue;
            }

            int initialRevision = source.Revision;
            ItemStack[] live = source.Slots;
            if (live == null) continue;
            int initialBagRevision = RemoteResourceAccess.ComputeRevision(player.bag.ItemGrid.items);
            ItemStack[] proposedBag = ItemStack.Clone((IList<ItemStack>)player.bag.ItemGrid.items);
            ItemStack[] proposed = ItemStack.Clone((IList<ItemStack>)live);
            int collectedHere = 0;
            int changedStacksHere = 0;

            for (int slot = 0; slot < proposed.Length; slot++)
            {
                ItemStack output = proposed[slot];
                if (output == null || output.IsEmpty()) continue;

                int allowed = int.MaxValue;
                if (selectedLimits != null)
                {
                    if (!selectedLimits.TryGetValue(output.itemValue.type, out allowed) || allowed <= 0) continue;
                }

                ItemStack transfer = output.Clone();
                if (selectedLimits != null) transfer.count = Math.Min(transfer.count, allowed);
                int before = transfer.count;
                MoveIntoBackpack(proposedBag, transfer, protectedSlots);
                int moved = before - transfer.count;
                if (moved <= 0) continue;

                ItemStack after = output.Clone();
                after.count -= moved;
                proposed[slot] = after.count > 0 ? after : ItemStack.Empty.Clone();
                if (selectedLimits != null) selectedLimits[output.itemValue.type] = allowed - moved;
                collectedHere += moved;
                changedStacksHere++;
            }

            if (collectedHere <= 0)
            {
                result.OutputItemsRemaining += CountItems(live);
                continue;
            }

            // Commit-time revalidation. Workstation output is assigned once so input, fuel,
            // tools, queues and partially completed production are never exposed or touched.
            if (!QuickStackRuntimePolicy.Enabled ||
                !RemoteResourceEligibility.CanUse(source, player, true, false, out reason) ||
                source.Revision != initialRevision ||
                RemoteResourceAccess.ComputeRevision(player.bag.ItemGrid.items) != initialBagRevision)
            {
                result.OutputItemsRemaining += CountItems(source.Slots);
                continue;
            }

            TileEntityWorkstation workstation = world.GetTileEntity(positions[i]) as TileEntityWorkstation;
            if (workstation == null || workstation.IsUserAccessing() || RemoteResourceAccess.IsServerBusy(workstation))
            {
                result.BusySkipped++;
                result.OutputItemsRemaining += CountItems(source.Slots);
                continue;
            }

            workstation.Output = proposed;
            player.bag.SetSlots(proposedBag);
            result.ItemsCollected += collectedHere;
            result.OutputStacksChanged += changedStacksHere;
            result.WorkstationsUsed++;
            result.OutputItemsRemaining += CountItems(proposed);
        }

        stopwatch.Stop();
        result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
        QuickStackDiagnostics.Write(
            "collect outputs items=" + result.ItemsCollected +
            " stacks=" + result.OutputStacksChanged +
            " workstations=" + result.WorkstationsUsed +
            " busy=" + result.BusySkipped +
            " remaining=" + result.OutputItemsRemaining +
            " elapsedMs=" + result.ElapsedMilliseconds);
        return result;
    }

    public static List<Vector3i> FindNearbyWorkstationPositions(
        World world,
        Vector3 center,
        float radius)
    {
        List<Vector3i> result = new List<Vector3i>();
        if (world == null || radius <= 0f) return result;

        Vector3i blockCenter = World.worldToBlockPos(center);
        int centerChunkX = World.toChunkXZ(blockCenter.x);
        int centerChunkZ = World.toChunkXZ(blockCenter.z);
        int chunkRadius = Mathf.CeilToInt(radius / 16f) + 1;
        float radiusSquared = radius * radius;
        HashSet<Vector3i> seen = new HashSet<Vector3i>();

        for (int dz = -chunkRadius; dz <= chunkRadius; dz++)
        {
            for (int dx = -chunkRadius; dx <= chunkRadius; dx++)
            {
                Chunk chunk = world.GetChunkSync(centerChunkX + dx, centerChunkZ + dz) as Chunk;
                if (chunk == null) continue;

                DictionaryList<Vector3i, TileEntity> tileEntities = chunk.GetTileEntities();
                if (tileEntities == null) continue;

                for (int i = 0; i < tileEntities.list.Count; i++)
                {
                    TileEntityWorkstation workstation = tileEntities.list[i] as TileEntityWorkstation;
                    if (workstation == null) continue;

                    Vector3i position = workstation.ToWorldPos();
                    if ((position.ToVector3() - center).sqrMagnitude > radiusSquared) continue;
                    if (!seen.Add(position)) continue;

                    // Reconcile the normal remote-resource registry while we have the
                    // authoritative live tile entity in hand. This fixes missed
                    // load/placement lifecycle registrations without making the UI
                    // depend on the cache being correct in the first place.
                    RemoteResourceRegistry.Register(workstation);
                    result.Add(position);
                }
            }
        }

        result.Sort(delegate(Vector3i a, Vector3i b)
        {
            int byDistance = (a.ToVector3() - center).sqrMagnitude.CompareTo(
                (b.ToVector3() - center).sqrMagnitude);
            if (byDistance != 0) return byDistance;
            int byX = a.x.CompareTo(b.x); if (byX != 0) return byX;
            int byY = a.y.CompareTo(b.y); if (byY != 0) return byY;
            return a.z.CompareTo(b.z);
        });
        return result;
    }

    public static string BuildPreview(World world, EntityPlayer player)
    {
        if (world == null || player == null) return Localization.Get("xuiRebirthQuickStackCollectOutputsPreviewNone");
        int items = 0;
        int workstations = 0;
        int busy = 0;
        List<Vector3i> positions = FindNearbyWorkstationPositions(
            world, player.position, QuickStackService.Radius);
        for (int i = 0; i < positions.Count; i++)
        {
            WorkstationOutputResourceSource source = new WorkstationOutputResourceSource(world, positions[i]);
            if (source.IsBusy) { busy++; continue; }
            string reason;
            if (!RemoteResourceEligibility.CanUse(source, player, true, false, out reason)) continue;
            int count = CountItems(source.Slots);
            if (count <= 0) continue;
            items += count;
            workstations++;
        }
        if (items <= 0) return Localization.Get("xuiRebirthQuickStackCollectOutputsPreviewNone");
        return string.Format(Localization.Get("xuiRebirthQuickStackCollectOutputsPreview"), workstations, items, busy);
    }

    public static void ShowResult(QuickStackWorkstationOutputResult result)
    {
        EntityPlayerLocal player = GameManager.Instance != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (player == null || result == null) return;
        string key = result.ItemsCollected > 0
            ? "xuiRebirthQuickStackCollectOutputsResult"
            : "xuiRebirthQuickStackCollectOutputsNone";
        string text = result.ItemsCollected > 0
            ? string.Format(Localization.Get(key), result.ItemsCollected, result.OutputStacksChanged,
                result.WorkstationsUsed, result.BusySkipped, result.OutputItemsRemaining)
            : string.Format(Localization.Get(key), result.BusySkipped, result.OutputItemsRemaining);
        GameManager.ShowTooltip(player, text);
    }

    private static void MoveIntoBackpack(ItemStack[] bag, ItemStack source, PackedBoolArray protectedSlots)
    {
        for (int i = 0; i < bag.Length && source.count > 0; i++)
        {
            // Existing protected backpack stacks can still be topped up.
            if (bag[i] == null || bag[i].IsEmpty()) continue;
            int amount;
            if (!bag[i].CanStackPartlyWith(source, out amount) || amount <= 0) continue;
            ItemStack after = bag[i].Clone();
            after.count += amount;
            source.count -= amount;
            bag[i] = after;
        }
        int maxStack = ItemClass.GetForId(source.itemValue.type).Stacknumber.Value;
        for (int i = 0; i < bag.Length && source.count > 0; i++)
        {
            if (IsProtected(protectedSlots, i) || (bag[i] != null && !bag[i].IsEmpty())) continue;
            int amount = Math.Min(maxStack, source.count);
            bag[i] = new ItemStack(source.itemValue.Clone(), amount);
            source.count -= amount;
        }
    }

    private static Dictionary<int, int> ParseSelectedWorkstationLimits(
        string selectedSourceKeys, string selectedWorkstationId)
    {
        Dictionary<int, int> result = new Dictionary<int, int>();
        string[] values = (selectedSourceKeys ?? string.Empty).Split('\u001f');
        for (int i = 0; i < values.Length; i++)
        {
            string value = (values[i] ?? string.Empty).Trim();
            if (value.Length == 0) continue;
            int sep = value.LastIndexOf('\u001d');
            string key = sep >= 0 ? value.Substring(0, sep) : value;
            int maxCount = int.MaxValue;
            if (sep >= 0 && (!int.TryParse(value.Substring(sep + 1), out maxCount) || maxCount <= 0)) continue;

            string kind, sourceId; int itemType;
            if (!LogisticsPreviewService.TryParseSourceSelectionKey(key, out kind, out sourceId, out itemType) ||
                kind != "W" || !string.Equals(sourceId, selectedWorkstationId, StringComparison.Ordinal)) continue;
            result[itemType] = maxCount;
        }
        return result;
    }

    private static int CountItems(ItemStack[] slots)
    {
        int total = 0;
        for (int i = 0; slots != null && i < slots.Length; i++)
            if (slots[i] != null && !slots[i].IsEmpty()) total += slots[i].count;
        return total;
    }

    private static bool IsProtected(PackedBoolArray protectedSlots, int slot)
    {
        return protectedSlots != null && slot >= 0 && slot < protectedSlots.Length && protectedSlots[slot];
    }
}

[Preserve]
public sealed class NetPackageQuickStackCollectOutputsRequest : NetPackage
{
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageQuickStackCollectOutputsRequest Setup(int id, PersistentPlayerData persistent)
    {
        playerId = id;
        userId = persistent.PrimaryId;
        return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        playerId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(playerId);
        userId.ToStream(binary);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId)) return;
        QuickStackWorkstationOutputResult result = QuickStackWorkstationOutputService.ProcessServer(world, playerId, userId);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageQuickStackCollectOutputsResult>().Setup(result),
            _attachedToEntityId: playerId);
    }
    public int GetLength() { return 32; }
}

[Preserve]
public sealed class NetPackageQuickStackCollectOutputsResult : NetPackage
{
    private QuickStackWorkstationOutputResult result = new QuickStackWorkstationOutputResult();
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageQuickStackCollectOutputsResult Setup(QuickStackWorkstationOutputResult value)
    {
        result = value ?? new QuickStackWorkstationOutputResult();
        return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        result.ItemsCollected = binary.ReadInt32();
        result.OutputStacksChanged = binary.ReadInt32();
        result.WorkstationsUsed = binary.ReadInt32();
        result.BusySkipped = binary.ReadInt32();
        result.UnauthorizedSkipped = binary.ReadInt32();
        result.OutputItemsRemaining = binary.ReadInt32();
        result.ElapsedMilliseconds = binary.ReadInt64();
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(result.ItemsCollected);
        binary.Write(result.OutputStacksChanged);
        binary.Write(result.WorkstationsUsed);
        binary.Write(result.BusySkipped);
        binary.Write(result.UnauthorizedSkipped);
        binary.Write(result.OutputItemsRemaining);
        binary.Write(result.ElapsedMilliseconds);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        QuickStackWorkstationOutputService.ShowResult(result);
    }
    public int GetLength() { return 36; }
}
