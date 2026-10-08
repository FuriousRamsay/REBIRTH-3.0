using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public sealed class LogisticsItemPreviewRow
{
    public int ItemType;
    public int Count;
    public string Secondary = string.Empty;
    public string SourceName = string.Empty;
    public string SelectionKey = string.Empty;
    public bool Selectable;
    public bool Selected = true;
}

public sealed class LogisticsTargetPreviewRow
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Distance = string.Empty;
    public string IconAtlas = string.Empty;
    public string IconName = string.Empty;
    public bool Selected;
}

public sealed class LogisticsPreviewData
{
    // Legacy text fields are retained for a safe fallback and older diagnostics,
    // while v129 renders the structured rows below with real game icons.
    public readonly List<string> Items = new List<string>();
    public readonly List<string> Targets = new List<string>();
    public readonly List<string> TargetIds = new List<string>();
    public readonly List<string> TargetNames = new List<string>();
    public readonly List<LogisticsItemPreviewRow> ItemRows = new List<LogisticsItemPreviewRow>();
    public readonly List<LogisticsTargetPreviewRow> TargetRows = new List<LogisticsTargetPreviewRow>();
    public string SelectedTargetId = string.Empty;
    public bool UsesTargetSelector;

    private static string Clean(string value)
    {
        return (value ?? string.Empty)
            .Replace('\u001c', ' ')
            .Replace('\u001d', ' ')
            .Replace('\u001e', ' ')
            .Replace('\u001f', ' ');
    }

    private static string SerializeItemRows(List<LogisticsItemPreviewRow> rows)
    {
        List<string> values = new List<string>();
        for (int i = 0; rows != null && i < rows.Count; i++)
        {
            LogisticsItemPreviewRow row = rows[i];
            if (row == null) continue;
            values.Add(row.ItemType + "\u001c" + row.Count + "\u001c" + Clean(row.Secondary) +
                       "\u001c" + Clean(row.SourceName) + "\u001c" + Clean(row.SelectionKey) +
                       "\u001c" + (row.Selectable ? "1" : "0") + "\u001c" + (row.Selected ? "1" : "0"));
        }
        return string.Join("\u001f", values.ToArray());
    }

    private static string SerializeTargetRows(List<LogisticsTargetPreviewRow> rows)
    {
        List<string> values = new List<string>();
        for (int i = 0; rows != null && i < rows.Count; i++)
        {
            LogisticsTargetPreviewRow row = rows[i];
            if (row == null) continue;
            values.Add(Clean(row.Id) + "\u001c" + Clean(row.Name) + "\u001c" +
                       Clean(row.Distance) + "\u001c" + Clean(row.IconAtlas) + "\u001c" +
                       Clean(row.IconName) + "\u001c" + (row.Selected ? "1" : "0"));
        }
        return string.Join("\u001f", values.ToArray());
    }

    public string Serialize()
    {
        return string.Join("\u001e", new[]
        {
            string.Join("\u001f", Items.ToArray()),
            string.Join("\u001f", Targets.ToArray()),
            string.Join("\u001f", TargetIds.ToArray()),
            string.Join("\u001f", TargetNames.ToArray()),
            Clean(SelectedTargetId),
            UsesTargetSelector ? "1" : "0",
            SerializeItemRows(ItemRows),
            SerializeTargetRows(TargetRows)
        });
    }

    public static LogisticsPreviewData Deserialize(string value)
    {
        LogisticsPreviewData d = new LogisticsPreviewData();
        string[] p = (value ?? string.Empty).Split('\u001e');
        if (p.Length > 0 && p[0].Length > 0) d.Items.AddRange(p[0].Split('\u001f'));
        if (p.Length > 1 && p[1].Length > 0) d.Targets.AddRange(p[1].Split('\u001f'));
        if (p.Length > 2 && p[2].Length > 0) d.TargetIds.AddRange(p[2].Split('\u001f'));
        if (p.Length > 3 && p[3].Length > 0) d.TargetNames.AddRange(p[3].Split('\u001f'));
        if (p.Length > 4) d.SelectedTargetId = p[4] ?? string.Empty;
        if (p.Length > 5) d.UsesTargetSelector = p[5] == "1";

        if (p.Length > 6 && p[6].Length > 0)
        {
            string[] rows = p[6].Split('\u001f');
            for (int i = 0; i < rows.Length; i++)
            {
                string[] f = rows[i].Split('\u001c');
                int type;
                int count;
                if (f.Length < 2 || !int.TryParse(f[0], out type) || !int.TryParse(f[1], out count)) continue;
                d.ItemRows.Add(new LogisticsItemPreviewRow
                {
                    ItemType = type,
                    Count = count,
                    Secondary = f.Length > 2 ? f[2] : string.Empty,
                    SourceName = f.Length > 3 ? f[3] : string.Empty,
                    SelectionKey = f.Length > 4 ? f[4] : string.Empty,
                    Selectable = f.Length > 5 && f[5] == "1",
                    Selected = f.Length <= 6 || f[6] == "1"
                });
            }
        }

        if (p.Length > 7 && p[7].Length > 0)
        {
            string[] rows = p[7].Split('\u001f');
            for (int i = 0; i < rows.Length; i++)
            {
                string[] f = rows[i].Split('\u001c');
                if (f.Length < 3) continue;
                d.TargetRows.Add(new LogisticsTargetPreviewRow
                {
                    Id = f[0],
                    Name = f[1],
                    Distance = f[2],
                    IconAtlas = f.Length > 3 ? f[3] : string.Empty,
                    IconName = f.Length > 4 ? f[4] : string.Empty,
                    Selected = f.Length > 5 && f[5] == "1"
                });
            }
        }
        return d;
    }
}


internal static class QuickStackRemotePlayerBagSync
{
    private const int MaxAcceptedBagSlots = 256;

    public static bool ApplyServerSnapshot(EntityPlayer player, ClientInfo sender, Bag snapshot, string phase)
    {
        if (player == null || player.bag == null || snapshot == null)
        {
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("remote bag sync rejected phase=" + phase + " reason=missing player/bag/snapshot"); }
            return false;
        }

        int incomingSlots = snapshot.SlotCount;
        int serverSlots = player.bag.SlotCount;
        if (incomingSlots <= 0 || incomingSlots > MaxAcceptedBagSlots ||
            (serverSlots > 0 && incomingSlots != serverSlots))
        {
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("remote bag sync rejected phase=" + phase +
                " entity=" + player.entityId + " incomingSlots=" + incomingSlots +
                " serverSlots=" + serverSlots); }
            return false;
        }

        // Native copy preserves bound cells and the live holder, plus locks/preferences/touch metadata.
        player.bag.ItemGrid.CopyFrom(snapshot.ItemGrid, false);

        if (sender != null && sender.latestPlayerData != null)
        {
            sender.latestPlayerData.bagData = StreamUtils.ToBlob(writer => player.bag.Write(writer, StreamModeWrite.Persistency));
            sender.latestPlayerData.bModifiedSinceLastSave = true;
        }

        if (QuickStackDiagnostics.Enabled)
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("remote bag sync applied phase=" + phase +
                " entity=" + player.entityId + " slots=" + incomingSlots +
                " used=" + CountUsed(player.bag.ItemGrid.items)); }
        return true;
    }


    public static bool MatchesServerSnapshot(EntityPlayer player, Bag snapshot, string phase)
    {
        if (player == null || player.bag == null || snapshot == null) return false;
        ItemStack[] server = player.bag.ItemGrid.items;
        ItemStack[] client = snapshot.ItemGrid.items;
        if (server == null || client == null || server.Length != client.Length) return false;
        for (int i = 0; i < server.Length; i++)
        {
            ItemStack a = server[i];
            ItemStack b = client[i];
            bool ae = a == null || a.IsEmpty();
            bool be = b == null || b.IsEmpty();
            if (ae != be) return false;
            if (ae) continue;
            if (a.count != b.count || a.itemValue == null || b.itemValue == null || !a.itemValue.Equals(b.itemValue))
            {
                if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write(
                    "remote bag snapshot mismatch phase=" + phase + " entity=" + player.entityId + " slot=" + i);
                return false;
            }
        }
        return true;
    }

    public static void CommitServerBag(EntityPlayer player, ClientInfo sender)
    {
        if (player == null || player.bag == null) return;
        if (sender != null && sender.latestPlayerData != null)
        {
            sender.latestPlayerData.bagData = StreamUtils.ToBlob(writer => player.bag.Write(writer, StreamModeWrite.Persistency));
            sender.latestPlayerData.bModifiedSinceLastSave = true;
        }
        if (QuickStackDiagnostics.Enabled)
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("remote bag sync committed entity=" + player.entityId +
                " used=" + CountUsed(player.bag.ItemGrid.items)); }
    }

    public static ItemStack[] CaptureItems(Bag bag)
    {
        return bag != null ? ItemStack.Clone((IList<ItemStack>)bag.ItemGrid.items) : null;
    }

    public static void ApplyClientItems(ItemStack[] items)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        if (player == null || player.bag == null || items == null) return;

        if (player.bag.SlotCount > 0 && items.Length != player.bag.SlotCount)
        {
            Log.Warning("[QuickStack] server bag sync ignored: clientSlots=" +
                player.bag.SlotCount + " serverSlots=" + items.Length);
            return;
        }

        // Preserve client-side protected-slot preferences. Only item contents changed
        // authoritatively on the server are applied here.
        player.bag.SetSlots(ItemStack.Clone((IList<ItemStack>)items));
        if (QuickStackDiagnostics.Enabled)
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("client bag sync applied slots=" + items.Length +
                " used=" + CountUsed(items)); }
    }

    private static int CountUsed(ItemStack[] slots)
    {
        int used = 0;
        for (int i = 0; slots != null && i < slots.Length; i++)
            if (slots[i] != null && !slots[i].IsEmpty()) used++;
        return used;
    }
}

public static class LogisticsPreviewService
{
    private sealed class MobileTarget
    {
        public string Id;
        public string Name;
        public Vector3 Position;
        public string IconAtlas = string.Empty;
        public string IconName = string.Empty;
        public IRemoteResourceSource Resource;
        public EntityRebirthNPC Npc;
        public RebirthNpcStableId NpcId;
    }

    private sealed class QuickStackSourceCandidate
    {
        public string SelectionKey = string.Empty;
        public string SourceName = string.Empty;
        public int ItemType;
        public int Count;
        public ItemStack Representative = ItemStack.Empty;
    }

    private static Action<QuickStackRadialAction, LogisticsPreviewData> callback;
    private static QuickStackRadialAction pending;
    private static World requestWorld;
    private static ulong requestEpoch = 1;
    private static ulong nextRequestId;
    private static ulong pendingRequestId;
    private static float requestDeadline;
    private const float RequestTimeoutSeconds = 5f;
    private const float CompanionRadius = 30f;

    public static void Request(
        QuickStackRadialAction action,
        string selectedTargetId,
        Action<QuickStackRadialAction, LogisticsPreviewData> cb)
    {
        callback = cb;
        pending = action;
        requestWorld = GameManager.Instance != null ? GameManager.Instance.World : null;
        unchecked { pendingRequestId = ++nextRequestId; }
        if (pendingRequestId == 0) pendingRequestId = ++nextRequestId;
        requestDeadline = Time.unscaledTime + RequestTimeoutSeconds;
        EntityPlayerLocal player = requestWorld != null
            ? GameManager.Instance.World.GetPrimaryPlayer()
            : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (player == null || player.bag == null || persistent == null || persistent.PrimaryId == null || connection == null)
        {
            Unavailable(player);
            return;
        }
        if (connection.IsServer)
        {
            Receive(requestEpoch, pendingRequestId, action, Build(requestWorld, player, action, selectedTargetId));
            return;
        }
        var packet = NetPackageManager.GetPackage<NetPackageLogisticsPreviewRequest>();
        if (!LogisticsTransferService.ClientChannelReady(connection, packet))
        {
            Unavailable(player);
            return;
        }
        connection.SendToServer(packet
            .Setup(player.entityId, persistent, requestEpoch, pendingRequestId, action, selectedTargetId, player.bag));
    }

    private static void Unavailable(EntityPlayerLocal player)
    {
        Action<QuickStackRadialAction, LogisticsPreviewData> cb = callback;
        QuickStackRadialAction action = pending;
        Clear();
        if (player != null) GameManager.ShowTooltip(player, Localization.Get("xuiRebirthInventoryTransferUnavailable"));
        if (cb != null) cb(action, new LogisticsPreviewData());
    }

    public static void Clear()
    {
        callback = null; pendingRequestId = 0; requestDeadline = 0f; requestWorld = null;
        unchecked { requestEpoch++; }
        if (requestEpoch == 0) requestEpoch = 1;
    }

    public static void UpdatePendingRequest()
    {
        if (callback == null || pendingRequestId == 0 || Time.unscaledTime < requestDeadline) return;
        Action<QuickStackRadialAction, LogisticsPreviewData> cb = callback;
        QuickStackRadialAction action = pending;
        callback = null; pendingRequestId = 0; requestDeadline = 0f; requestWorld = null;
        if (cb != null) cb(action, new LogisticsPreviewData());
    }

    public static void Receive(ulong epoch, ulong requestId, QuickStackRadialAction action, LogisticsPreviewData data)
    {
        Action<QuickStackRadialAction, LogisticsPreviewData> cb = callback;
        if (cb == null || epoch != requestEpoch || requestId == 0 || requestId != pendingRequestId || action != pending) return;
        if (requestWorld == null || !ReferenceEquals(requestWorld, GameManager.Instance != null ? GameManager.Instance.World : null))
        {
            Clear(); return;
        }
        callback = null; pendingRequestId = 0; requestDeadline = 0f; requestWorld = null;
        cb(action, data ?? new LogisticsPreviewData());
    }

    public static LogisticsPreviewData Build(
        World world,
        EntityPlayer player,
        QuickStackRadialAction action,
        string requestedTargetId)
    {
        LogisticsPreviewData data = new LogisticsPreviewData();
        if (world == null || player == null) return data;

        switch (action)
        {
            case QuickStackRadialAction.Deposit:
                BuildStaticDeposit(world, player, false, data);
                break;
            case QuickStackRadialAction.DepositOwned:
                BuildStaticDeposit(world, player, true, data);
                break;
            case QuickStackRadialAction.Restock:
                BuildStaticRestock(world, player, data);
                break;
            case QuickStackRadialAction.CollectWorkstationOutputs:
                BuildWorkstationOutputs(world, player, requestedTargetId, data);
                break;
            case QuickStackRadialAction.PushVehicle:
            case QuickStackRadialAction.PullVehicle:
                BuildVehicles(world, player, action, requestedTargetId, data);
                break;
            case QuickStackRadialAction.PushDrone:
            case QuickStackRadialAction.PullDrone:
                BuildCompanions(world, player, action, requestedTargetId, data);
                break;
        }

        BuildLegacyFallbackLines(data, action);
        return data;
    }

    private static void BuildStaticDeposit(World world, EntityPlayer player, bool owned, LogisticsPreviewData data)
    {
        // Category classification is stable definition data. The registry owns explicit
        // invalidation; preview generation must not flush/reclassify it every time.

        // v130 Quick Stack source model: the dump list represents every nearby
        // inventory the player is allowed to use, not just the backpack. Rows are
        // item+source pairs so the player can disable one source without disabling
        // the same item in another inventory.
        List<QuickStackSourceCandidate> sources = GatherQuickStackSources(world, player);
        if (QuickStackDiagnostics.Enabled)
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("preview deposit owned=" + owned + " sources=" + sources.Count +
                " mode=" + RebirthSandboxOptionManager.Current.QuickStack); }
        HashSet<int> sourceItemTypes = new HashSet<int>();
        for (int i = 0; i < sources.Count; i++) sourceItemTypes.Add(sources[i].ItemType);

        List<QuickStackContainerEntry> candidates = QuickStackContainerRegistry.Query(
            world, player.position, QuickStackService.Radius);
        Dictionary<Vector3i, ItemStack[]> simulatedDestinations = new Dictionary<Vector3i, ItemStack[]>();
        Dictionary<Vector3i, PackedBoolArray> destinationLocks = new Dictionary<Vector3i, PackedBoolArray>();

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3i pos = candidates[i].Position;
            TileEntity te = world.GetTileEntity(pos);
            TEFeatureStorage loot;
            string reason;
            if (!QuickStackService.CanUse(world, player, te, owned, out loot, out reason))
            {
                { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("preview target rejected pos=" + pos + " reason='" + reason + "'"); }
                continue;
            }
            if (!IsQualifiedStatic(pos, loot, sourceItemTypes))
            {
                { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("preview target not qualified pos=" + pos +
                    " assigned=[" + string.Join(",", QuickStackAcceptedCategoryRegistry.Get(pos)) + "]"); }
                continue;
            }

            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("preview target qualified pos=" + pos +
                " assigned=[" + string.Join(",", QuickStackAcceptedCategoryRegistry.Get(pos)) + "]"); }
            StaticContainerResourceSource target = new StaticContainerResourceSource(world, pos);
            string atlas;
            string icon;
            GetStaticContainerIcon(world, pos, out atlas, out icon);
            AddTarget(data, target.StableId, target.DisplayName, target.Position, player, false, atlas, icon);
            simulatedDestinations[pos] = CloneSlots(loot.ItemGrid.items);
            destinationLocks[pos] = loot.ItemGrid.SlotLocks;
        }

        RebirthQuickStackMode mode = RebirthSandboxOptionManager.Current.QuickStack;
        for (int i = 0; i < sources.Count; i++)
        {
            QuickStackSourceCandidate candidate = sources[i];
            if (candidate == null || candidate.Representative == null || candidate.Representative.IsEmpty()) continue;

            ItemStack source = candidate.Representative.Clone();
            source.count = candidate.Count;
            QuickStackCategory sourceCategory = QuickStackCategoryRegistry.Get(candidate.ItemType);
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("preview source itemType=" + candidate.ItemType +
                " item='" + (ItemClass.GetForId(candidate.ItemType) != null ? ItemClass.GetForId(candidate.ItemType).GetItemName() : "<null>") +
                "' source='" + candidate.SourceName + "' count=" + candidate.Count +
                " category=" + sourceCategory + " key='" + candidate.SelectionKey + "'"); }
            QuickStackTransferPlanner.Plan plan = QuickStackTransferPlanner.Find(world, player, source, owned, mode);
            if (plan == null || plan.Destinations.Count == 0)
            {
                { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("preview source has no destinations itemType=" + candidate.ItemType +
                    " category=" + sourceCategory); }
                continue;
            }

            ItemStack remaining = source.Clone();
            int before = remaining.count;
            // All rows share one staged destination image so the preview reports joint
            // capacity rather than independently promising the same empty slot to several rows.
            for (int d = 0; d < plan.Destinations.Count && remaining.count > 0; d++)
            {
                QuickStackTransferPlanner.Destination plannedDestination = plan.Destinations[d];
                Vector3i pos = plannedDestination.Position;
                ItemStack[] destination;
                if (!simulatedDestinations.TryGetValue(pos, out destination)) continue;
                PackedBoolArray locks;
                destinationLocks.TryGetValue(pos, out locks);
                SimulateInto(destination, locks, remaining, plannedDestination.Category);
            }

            int moved = before - remaining.count;
            if (moved <= 0)
            {
                { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("preview source no simulated capacity itemType=" + candidate.ItemType +
                    " destinations=" + plan.Destinations.Count); }
                continue;
            }
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("preview source accepted itemType=" + candidate.ItemType +
                " movable=" + moved + " destinations=" + plan.Destinations.Count +
                " categoryPass=" + plan.CategoryPass); }
            data.ItemRows.Add(new LogisticsItemPreviewRow
            {
                ItemType = candidate.ItemType,
                Count = moved,
                SourceName = candidate.SourceName,
                Secondary = candidate.SourceName,
                SelectionKey = candidate.SelectionKey,
                Selectable = true,
                Selected = true
            });
        }

        data.ItemRows.Sort(delegate(LogisticsItemPreviewRow a, LogisticsItemPreviewRow b)
        {
            ItemClass ia = ItemClass.GetForId(a.ItemType);
            ItemClass ib = ItemClass.GetForId(b.ItemType);
            string an = ia != null ? ia.GetLocalizedItemName() : a.ItemType.ToString();
            string bn = ib != null ? ib.GetLocalizedItemName() : b.ItemType.ToString();
            int byItem = string.Compare(an, bn, StringComparison.CurrentCultureIgnoreCase);
            if (byItem != 0) return byItem;
            return string.Compare(a.SourceName, b.SourceName, StringComparison.CurrentCultureIgnoreCase);
        });
    }

    private static List<QuickStackSourceCandidate> GatherQuickStackSources(World world, EntityPlayer player)
    {
        Dictionary<string, QuickStackSourceCandidate> byKey =
            new Dictionary<string, QuickStackSourceCandidate>(StringComparer.Ordinal);
        if (world == null || player == null) return new List<QuickStackSourceCandidate>();

        AddStackSourceCandidates(
            byKey, "B", string.Empty,
            Localization.Get("xuiRebirthQuickStackSourceBackpack"),
            player.bag.ItemGrid.items, player.bag.LockedSlots);

        // Completed workstation outputs are first-class Quick Stack sources too.
        // Only Output[] is exposed by WorkstationOutputResourceSource, so input,
        // fuel, tools and queued crafting materials remain untouched.
        List<WorkstationOutputResourceSource> workstationSources =
            GatherWorkstationOutputSources(world, player, true);
        for (int i = 0; i < workstationSources.Count; i++)
        {
            WorkstationOutputResourceSource workstation = workstationSources[i];
            AddStackSourceCandidates(
                byKey, "W", workstation.StableId,
                workstation.DisplayName,
                workstation.Slots, workstation.SlotLocks);
        }

        // Vehicles and drones use the same live authorization/security path as the
        // explicit Vehicle/Companion actions. Empty or inaccessible inventories do
        // not appear as Quick Stack sources.
        List<IRemoteResourceSource> nearby = RemoteResourceRegistry.ResolveNearby(
            world, player, QuickStackService.Radius, false, false, true);
        for (int i = 0; i < nearby.Count; i++)
        {
            IRemoteResourceSource resource = nearby[i];
            if (resource == null ||
                (resource.Kind != RemoteResourceSourceKind.VehicleStorage &&
                 resource.Kind != RemoteResourceSourceKind.DroneStorage)) continue;
            if (resource.Kind == RemoteResourceSourceKind.DroneStorage &&
                (resource.Position - player.position).sqrMagnitude > CompanionRadius * CompanionRadius) continue;
            string reason;
            if (!CanUseMobile(resource, player, true, out reason)) continue;
            string fallback = resource.Kind == RemoteResourceSourceKind.DroneStorage
                ? Localization.Get("xuiRebirthQuickStackSourceDrone")
                : Localization.Get("xuiRebirthVehicleStorage");
            AddStackSourceCandidates(
                byKey, "R", resource.StableId,
                GetMobileDisplayName(resource, fallback),
                resource.Slots, resource.SlotLocks);
        }

        // Rebirth survivor/animal companions keep their inventory in the NPC
        // transactional store rather than an ItemStack[] bag. Snapshot quantities
        // are still presented as normal item+source rows.
        Bounds bounds = new Bounds(player.position, Vector3.one * CompanionRadius * 2f);
        List<Entity> entities = world.GetEntitiesInBounds(typeof(EntityAlive), bounds, new List<Entity>());
        for (int i = 0; i < entities.Count; i++)
        {
            EntityRebirthNPC npc = entities[i] as EntityRebirthNPC;
            if (npc == null || npc.IsDead() ||
                (npc.position - player.position).sqrMagnitude > CompanionRadius * CompanionRadius) continue;
            RebirthNpcRuntimeState state = npc.RebirthRuntimeState;
            if (state == null || state.StableId.IsEmpty) continue;
            RebirthNpcProfile profile;
            if (!RebirthNpcProfileRegistry.TryResolve(state.ProfileId, out profile) ||
                !profile.Has(RebirthNpcCapabilities.Inventory) || !IsCompanionCategory(profile.Category) ||
                !CanAccessNpcCompanion(player, state)) continue;

            RemoteNpcResourceSource npcResource = new RemoteNpcResourceSource(npc);
            string npcReason;
            if (!CanUseMobile(npcResource, player, false, out npcReason)) continue;

            string sourceName = SafeName(npc.EntityName, Localization.Get("xuiRebirthCompanionStorage"));
            if (profile.Category == RebirthNpcCategory.DogCompanion)
            {
                // Dogs use the fixed-slot aggregate inventory, not the generic NPC
                // quantity store.  Keep the selection kind as N so execution can
                // resolve the companion by stable id while preserving per-slot locks.
                AddStackSourceCandidates(
                    byKey, "N", state.StableId.ToString(), sourceName, npcResource.Slots, npcResource.SlotLocks);
                continue;
            }

            RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(state.StableId);
            if (snapshot == null || snapshot.Quantities == null) continue;
            foreach (KeyValuePair<string, int> pair in snapshot.Quantities)
            {
                if (pair.Value <= 0 || RebirthCompanionInventoryLockService.IsNpcItemLocked(state.StableId, pair.Key)) continue;
                int reserved = 0;
                if (snapshot.Reservations != null) snapshot.Reservations.TryGetValue(pair.Key, out reserved);
                int available = Math.Max(0, pair.Value - reserved);
                if (available <= 0) continue;
                ItemValue value = ItemClass.GetItem(pair.Key, false);
                if (value.IsEmpty() || value.ItemClass == null) continue;
                int type = value.type;
                string key = BuildSourceSelectionKey("N", state.StableId.ToString(), type);
                QuickStackSourceCandidate existing;
                if (!byKey.TryGetValue(key, out existing))
                {
                    existing = new QuickStackSourceCandidate
                    {
                        SelectionKey = key,
                        SourceName = sourceName,
                        ItemType = type,
                        Representative = new ItemStack(value.Clone(), 1)
                    };
                    byKey[key] = existing;
                }
                existing.Count += available;
            }
        }

        List<QuickStackSourceCandidate> result = new List<QuickStackSourceCandidate>(byKey.Values);
        result.Sort(delegate(QuickStackSourceCandidate a, QuickStackSourceCandidate b)
        {
            ItemClass ia = ItemClass.GetForId(a.ItemType);
            ItemClass ib = ItemClass.GetForId(b.ItemType);
            string an = ia != null ? ia.GetLocalizedItemName() : a.ItemType.ToString();
            string bn = ib != null ? ib.GetLocalizedItemName() : b.ItemType.ToString();
            int byItem = string.Compare(an, bn, StringComparison.CurrentCultureIgnoreCase);
            if (byItem != 0) return byItem;
            return string.Compare(a.SourceName, b.SourceName, StringComparison.CurrentCultureIgnoreCase);
        });
        return result;
    }

    internal static string BuildAllEligibleDepositSelectionKeys(World world, EntityPlayer player)
    {
        if (world == null || player == null) return string.Empty;

        // The hotkey is the no-window equivalent of Deposit Items -> Execute with every
        // currently eligible source row selected.  Reuse the exact authoritative preview
        // builder so source discovery and eligibility cannot drift between the window and Q.
        // This intentionally includes every source type exposed by Deposit Items:
        // backpack, completed workstation outputs, vehicles, drones, dogs, and other
        // inventory-capable Rebirth companions.
        LogisticsPreviewData preview = Build(
            world, player, QuickStackRadialAction.Deposit, string.Empty);
        if (preview == null || preview.ItemRows == null || preview.ItemRows.Count == 0)
            return string.Empty;

        List<string> selected = new List<string>(preview.ItemRows.Count);
        int backpackRows = 0;
        int workstationRows = 0;
        int resourceRows = 0;
        int npcRows = 0;
        int totalQuantity = 0;

        for (int i = 0; i < preview.ItemRows.Count; i++)
        {
            LogisticsItemPreviewRow row = preview.ItemRows[i];
            if (row == null || !row.Selectable || !row.Selected || row.Count <= 0 ||
                string.IsNullOrEmpty(row.SelectionKey))
                continue;

            selected.Add(row.SelectionKey + '\u001d' + row.Count);
            totalQuantity += row.Count;

            if (row.SelectionKey.StartsWith("B|", StringComparison.Ordinal)) backpackRows++;
            else if (row.SelectionKey.StartsWith("W|", StringComparison.Ordinal)) workstationRows++;
            else if (row.SelectionKey.StartsWith("R|", StringComparison.Ordinal)) resourceRows++;
            else if (row.SelectionKey.StartsWith("N|", StringComparison.Ordinal)) npcRows++;
        }

        { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
            "BuildAllEligibleDepositSelectionKeys eligibleRows=" + selected.Count +
            " backpack=" + backpackRows +
            " workstation=" + workstationRows +
            " vehicleOrDrone=" + resourceRows +
            " companion=" + npcRows +
            " totalQuantity=" + totalQuantity); }

        return string.Join("\u001f", selected.ToArray());
    }

    internal static List<WorkstationOutputResourceSource> GatherWorkstationOutputSources(
        World world,
        EntityPlayer player,
        bool requireItems)
    {
        List<WorkstationOutputResourceSource> result = new List<WorkstationOutputResourceSource>();
        if (world == null || player == null) return result;

        List<Vector3i> positions = QuickStackWorkstationOutputService.FindNearbyWorkstationPositions(
            world, player.position, QuickStackService.Radius);
        for (int i = 0; i < positions.Count; i++)
        {
            WorkstationOutputResourceSource source =
                new WorkstationOutputResourceSource(world, positions[i]);
            string reason;
            if (!RemoteResourceEligibility.CanUse(source, player, requireItems, false, out reason))
            {
                if (QuickStackDiagnostics.Enabled)
                    { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("workstation source rejected id='" + source.StableId +
                        "' reason='" + reason + "'"); }
                continue;
            }

            result.Add(source);
            if (QuickStackDiagnostics.Enabled)
                { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("workstation source qualified id='" + source.StableId +
                    "' name='" + source.DisplayName + "'"); }
        }

        result.Sort(delegate(WorkstationOutputResourceSource a, WorkstationOutputResourceSource b)
        {
            int byDistance = (a.Position - player.position).sqrMagnitude.CompareTo(
                (b.Position - player.position).sqrMagnitude);
            if (byDistance != 0) return byDistance;
            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase);
        });
        return result;
    }

    internal static WorkstationOutputResourceSource FindWorkstationOutputSource(
        World world,
        EntityPlayer player,
        string sourceId,
        bool requireItems)
    {
        if (world == null || player == null || string.IsNullOrEmpty(sourceId)) return null;
        List<WorkstationOutputResourceSource> sources =
            GatherWorkstationOutputSources(world, player, requireItems);
        for (int i = 0; i < sources.Count; i++)
            if (string.Equals(sources[i].StableId, sourceId, StringComparison.Ordinal))
                return sources[i];
        return null;
    }

    private static void AddStackSourceCandidates(
        Dictionary<string, QuickStackSourceCandidate> byKey,
        string kind,
        string sourceId,
        string sourceName,
        ItemStack[] slots,
        PackedBoolArray locks)
    {
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            if (IsLocked(locks, i))
            {
                ItemStack protectedStack = slots[i];
                if (QuickStackDiagnostics.Enabled && protectedStack != null && !protectedStack.IsEmpty())
                    { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("source slot excluded protected kind=" + kind +
                        " source='" + sourceName + "' slot=" + i + " itemType=" + protectedStack.itemValue.type +
                        " count=" + protectedStack.count); }
                continue; // protected source stacks are never removed
            }
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty()) continue;
            int type = stack.itemValue.type;
            string key = BuildSourceSelectionKey(kind, sourceId, type);
            QuickStackSourceCandidate existing;
            if (!byKey.TryGetValue(key, out existing))
            {
                existing = new QuickStackSourceCandidate
                {
                    SelectionKey = key,
                    SourceName = sourceName,
                    ItemType = type,
                    Representative = stack.Clone()
                };
                existing.Representative.count = 1;
                byKey[key] = existing;
            }
            existing.Count += stack.count;
        }
    }

    internal static string BuildSourceSelectionKey(string kind, string sourceId, int itemType)
    {
        return (kind ?? string.Empty) + "|" + (sourceId ?? string.Empty) + "|" + itemType;
    }

    internal static bool TryParseSourceSelectionKey(
        string key, out string kind, out string sourceId, out int itemType)
    {
        kind = string.Empty;
        sourceId = string.Empty;
        itemType = 0;
        string[] parts = (key ?? string.Empty).Split('|');
        if (parts.Length != 3 || !int.TryParse(parts[2], out itemType)) return false;
        kind = parts[0];
        sourceId = parts[1];
        return (kind == "B" || kind == "R" || kind == "N" || kind == "T" || kind == "W") && itemType > 0;
    }

    private static void BuildStaticRestock(World world, EntityPlayer player, LogisticsPreviewData data)
    {
        HashSet<int> wanted = GetPlayerItemTypes(player, true);
        List<QuickStackContainerEntry> candidates = QuickStackContainerRegistry.Query(world, player.position, QuickStackService.Radius);
        ItemStack[] simulatedBag = CloneSlots(player.bag.ItemGrid.items);
        PackedBoolArray bagLocks = player.bag.LockedSlots;
        Dictionary<int, int> amounts = new Dictionary<int, int>();

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3i pos = candidates[i].Position;
            TileEntity te = world.GetTileEntity(pos);
            TEFeatureStorage loot;
            string reason;
            if (!QuickStackService.CanUse(world, player, te, false, out loot, out reason)) continue;
            if (!IsQualifiedStatic(pos, loot, wanted)) continue;

            StaticContainerResourceSource resource = new StaticContainerResourceSource(world, pos);
            string atlas;
            string icon;
            GetStaticContainerIcon(world, pos, out atlas, out icon);
            AddTarget(data, resource.StableId, resource.DisplayName, resource.Position, player, false, atlas, icon);

            PackedBoolArray sourceLocks = loot.ItemGrid.SlotLocks;
            for (int slot = 0; slot < loot.ItemGrid.items.Length; slot++)
            {
                if (IsLocked(sourceLocks, slot)) continue; // source lock means do not remove it
                ItemStack source = loot.ItemGrid.items[slot];
                if (source == null || source.IsEmpty() || !wanted.Contains(source.itemValue.type)) continue;
                ItemStack remaining = source.Clone();
                int before = remaining.count;
                SimulateInto(simulatedBag, bagLocks, remaining);
                int moved = before - remaining.count;
                if (moved > 0) AddAmount(amounts, source.itemValue.type, moved);
            }
        }
        AddItemLines(data, amounts, null);
        for (int i = 0; i < data.ItemRows.Count; i++)
        {
            LogisticsItemPreviewRow row = data.ItemRows[i];
            if (row == null || row.ItemType <= 0 || row.Count <= 0) continue;
            row.SelectionKey = BuildSourceSelectionKey("T", "RESTOCK", row.ItemType);
            row.Selectable = true;
            row.Selected = true;
        }
    }

    private static void BuildWorkstationOutputs(
        World world,
        EntityPlayer player,
        string requestedTargetId,
        LogisticsPreviewData data)
    {
        // Reconcile against the live loaded workstation tile entities every time the
        // window is opened/refreshed. The registry is still maintained for the remote
        // resource system, but it is not authoritative for this UI because a placed or
        // newly loaded workstation can otherwise be missing until a lifecycle hook runs.
        List<Vector3i> positions = QuickStackWorkstationOutputService.FindNearbyWorkstationPositions(
            world, player.position, QuickStackService.Radius);
        List<WorkstationOutputResourceSource> sources = new List<WorkstationOutputResourceSource>();

        for (int i = 0; i < positions.Count; i++)
        {
            WorkstationOutputResourceSource source = new WorkstationOutputResourceSource(world, positions[i]);
            string reason;
            if (!RemoteResourceEligibility.CanUse(source, player, true, false, out reason)) continue;

            ItemStack[] slots = source.Slots;
            bool hasOutput = false;
            for (int slot = 0; slots != null && slot < slots.Length; slot++)
            {
                if (slots[slot] != null && !slots[slot].IsEmpty())
                {
                    hasOutput = true;
                    break;
                }
            }
            if (hasOutput) sources.Add(source);
        }

        sources.Sort(delegate(WorkstationOutputResourceSource a, WorkstationOutputResourceSource b)
        {
            int byDistance = (a.Position - player.position).sqrMagnitude.CompareTo(
                (b.Position - player.position).sqrMagnitude);
            if (byDistance != 0) return byDistance;
            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase);
        });

        WorkstationOutputResourceSource selected = null;
        if (!string.IsNullOrEmpty(requestedTargetId))
        {
            for (int i = 0; i < sources.Count; i++)
            {
                if (string.Equals(sources[i].StableId, requestedTargetId, StringComparison.Ordinal))
                {
                    selected = sources[i];
                    break;
                }
            }
        }
        if (selected == null && sources.Count > 0) selected = sources[0];

        data.UsesTargetSelector = false;
        if (selected != null) data.SelectedTargetId = selected.StableId;

        for (int i = 0; i < sources.Count; i++)
            AddWorkstationTarget(data, world, sources[i], player,
                selected != null && string.Equals(sources[i].StableId, selected.StableId, StringComparison.Ordinal));

        // The left pane is the preview for the currently highlighted workstation,
        // not an aggregate of every station. This makes row selection meaningful and
        // ensures Execute Transfer applies to exactly the station the player chose.
        if (selected == null) return;

        Dictionary<int, int> sourceAmounts = new Dictionary<int, int>();
        ItemStack[] selectedSlots = selected.Slots;
        for (int slot = 0; selectedSlots != null && slot < selectedSlots.Length; slot++)
        {
            ItemStack item = selectedSlots[slot];
            if (item == null || item.IsEmpty()) continue;
            AddAmount(sourceAmounts, item.itemValue.type, item.count);
        }

        string stationName = SafeName(selected.DisplayName, Localization.Get("xuiRebirthWorkstationOutput"));
        foreach (KeyValuePair<int, int> pair in sourceAmounts)
        {
            data.ItemRows.Add(new LogisticsItemPreviewRow
            {
                ItemType = pair.Key,
                Count = pair.Value,
                Secondary = "Source: " + stationName,
                SourceName = stationName,
                SelectionKey = BuildSourceSelectionKey("W", selected.StableId, pair.Key),
                Selectable = true,
                Selected = true
            });
        }

        data.ItemRows.Sort(delegate(LogisticsItemPreviewRow a, LogisticsItemPreviewRow b)
        {
            ItemClass ia = ItemClass.GetForId(a.ItemType);
            ItemClass ib = ItemClass.GetForId(b.ItemType);
            string an = ia != null ? ia.GetLocalizedItemName() : a.ItemType.ToString();
            string bn = ib != null ? ib.GetLocalizedItemName() : b.ItemType.ToString();
            return string.Compare(an, bn, StringComparison.CurrentCultureIgnoreCase);
        });
    }

    private static void BuildVehicles(
        World world,
        EntityPlayer player,
        QuickStackRadialAction action,
        string requestedTargetId,
        LogisticsPreviewData data)
    {
        bool pull = action == QuickStackRadialAction.PullVehicle;
        List<MobileTarget> targets = GatherVehicleTargets(world, player, pull);
        SortTargets(player, targets);

        // Pull is an aggregate retrieve action: every qualified vehicle is a source.
        // No vehicle selector is required and each item row identifies its source.
        if (pull)
        {
            data.UsesTargetSelector = false;
            ItemStack[] simulatedBag = CloneSlots(player.bag.ItemGrid.items);
            PackedBoolArray bagLocks = player.bag.LockedSlots;
            for (int i = 0; i < targets.Count; i++)
            {
                MobileTarget target = targets[i];
                AddTarget(data, target.Id, target.Name, target.Position, player, false,
                    target.IconAtlas, target.IconName);
                AppendPullRowsFromResource(data, target.Resource, target.Name, simulatedBag, bagLocks);
            }
            SortItemRows(data);
            return;
        }

        // Push uses direct row selection. The closest qualified vehicle is selected
        // by default and clicking another target row rebuilds the source preview.
        data.UsesTargetSelector = false;
        MobileTarget selected = SelectTarget(targets, requestedTargetId);
        if (selected != null) data.SelectedTargetId = selected.Id;
        for (int i = 0; i < targets.Count; i++)
            AddTarget(data, targets[i].Id, targets[i].Name, targets[i].Position, player,
                selected != null && targets[i].Id == selected.Id,
                targets[i].IconAtlas, targets[i].IconName);

        if (selected == null || selected.Resource == null) return;
        BuildQualifiedPushRowsToResource(world, player, selected.Resource, selected.Id, data);
    }

    private static void BuildCompanions(
        World world,
        EntityPlayer player,
        QuickStackRadialAction action,
        string requestedTargetId,
        LogisticsPreviewData data)
    {
        bool pull = action == QuickStackRadialAction.PullDrone;
        List<MobileTarget> targets = GatherCompanionTargets(world, player, pull);
        SortTargets(player, targets);

        // Pull from Companions retrieves from every qualified nearby companion.
        // Rows identify the drone/survivor/animal that currently holds the item.
        if (pull)
        {
            data.UsesTargetSelector = false;
            ItemStack[] simulatedBag = CloneSlots(player.bag.ItemGrid.items);
            PackedBoolArray bagLocks = player.bag.LockedSlots;

            for (int i = 0; i < targets.Count; i++)
            {
                MobileTarget target = targets[i];
                AddTarget(data, target.Id, target.Name, target.Position, player, false,
                    target.IconAtlas, target.IconName);
                if (target.Resource != null)
                {
                    if (target.Id.StartsWith("N:", StringComparison.Ordinal))
                        AppendPullRowsFromResource(data, target.Resource, target.Name, simulatedBag, bagLocks,
                            "N", target.NpcId.ToString());
                    else
                        AppendPullRowsFromResource(data, target.Resource, target.Name, simulatedBag, bagLocks);
                }
                else
                    AppendPullRowsFromNpc(data, target.NpcId, target.Name, simulatedBag, bagLocks);
            }
            SortItemRows(data);
            return;
        }

        // Push uses direct row selection. The closest qualified companion is
        // selected by default and clicking another row changes the destination.
        data.UsesTargetSelector = false;
        MobileTarget selected = SelectTarget(targets, requestedTargetId);
        if (selected != null) data.SelectedTargetId = selected.Id;
        for (int i = 0; i < targets.Count; i++)
            AddTarget(data, targets[i].Id, targets[i].Name, targets[i].Position, player,
                selected != null && targets[i].Id == selected.Id,
                targets[i].IconAtlas, targets[i].IconName);

        if (selected == null) return;
        if (selected.Resource != null)
            BuildQualifiedPushRowsToResource(world, player, selected.Resource, selected.Id, data);
        else
            BuildQualifiedPushRowsToNpc(world, player, selected.NpcId, selected.Id, data);
    }

    private static List<MobileTarget> GatherVehicleTargets(World world, EntityPlayer player, bool requireItems)
    {
        List<MobileTarget> result = new List<MobileTarget>();
        List<IRemoteResourceSource> nearby = RemoteResourceRegistry.ResolveNearby(
            world, player, QuickStackService.Radius, false, false, true);
        for (int i = 0; i < nearby.Count; i++)
        {
            IRemoteResourceSource source = nearby[i];
            if (source == null || source.Kind != RemoteResourceSourceKind.VehicleStorage) continue;
            string reason;
            if (!CanUseMobile(source, player, requireItems, out reason)) continue;
            string name;
            string atlas;
            string icon;
            GetMobilePresentation(source, Localization.Get("xuiRebirthVehicleStorage"),
                out name, out atlas, out icon);
            result.Add(new MobileTarget
            {
                Id = source.StableId,
                Name = name,
                Position = source.Position,
                IconAtlas = atlas,
                IconName = icon,
                Resource = source
            });
        }
        return result;
    }

    private static List<MobileTarget> GatherCompanionTargets(World world, EntityPlayer player, bool requireItems)
    {
        List<MobileTarget> result = new List<MobileTarget>();

        List<IRemoteResourceSource> nearby = RemoteResourceRegistry.ResolveNearby(
            world, player, CompanionRadius, false, false, true);
        for (int i = 0; i < nearby.Count; i++)
        {
            IRemoteResourceSource source = nearby[i];
            if (source == null || source.Kind != RemoteResourceSourceKind.DroneStorage) continue;
            string reason;
            if (!CanUseMobile(source, player, requireItems, out reason)) continue;
            string name;
            string atlas;
            string icon;
            GetMobilePresentation(source, Localization.Get("xuiRebirthDroneStorage"),
                out name, out atlas, out icon);
            result.Add(new MobileTarget
            {
                Id = source.StableId,
                Name = name,
                Position = source.Position,
                IconAtlas = atlas,
                IconName = icon,
                Resource = source
            });
        }

        Bounds bounds = new Bounds(player.position, Vector3.one * CompanionRadius * 2f);
        List<Entity> entities = world.GetEntitiesInBounds(typeof(EntityAlive), bounds, new List<Entity>());
        for (int i = 0; i < entities.Count; i++)
        {
            EntityRebirthNPC npc = entities[i] as EntityRebirthNPC;
            if (npc == null || npc.IsDead() ||
                (npc.position - player.position).sqrMagnitude > CompanionRadius * CompanionRadius) continue;
            RebirthNpcRuntimeState state = npc.RebirthRuntimeState;
            if (state == null || state.StableId.IsEmpty) continue;
            RebirthNpcProfile profile;
            if (!RebirthNpcProfileRegistry.TryResolve(state.ProfileId, out profile) ||
                !profile.Has(RebirthNpcCapabilities.Inventory) || !IsCompanionCategory(profile.Category) ||
                !CanAccessNpcCompanion(player, state)) continue;

            bool dogCompanion = profile.Category == RebirthNpcCategory.DogCompanion;
            RemoteNpcResourceSource npcResource = new RemoteNpcResourceSource(npc);
            string npcReason;
            if (!CanUseMobile(npcResource, player, false, out npcReason)) continue;
            if (requireItems)
            {
                if (dogCompanion)
                {
                    if (!HasUsableUnlockedItem(npcResource.Slots, npcResource.SlotLocks)) continue;
                }
                else
                {
                    RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(state.StableId);
                    bool hasAvailable = false;
                    if (snapshot != null && snapshot.Quantities != null)
                    {
                        foreach (KeyValuePair<string, int> pair in snapshot.Quantities)
                        {
                            if (pair.Value <= 0 || RebirthCompanionInventoryLockService.IsNpcItemLocked(state.StableId, pair.Key)) continue;
                            int reserved = 0;
                            if (snapshot.Reservations != null) snapshot.Reservations.TryGetValue(pair.Key, out reserved);
                            if (pair.Value - reserved > 0) { hasAvailable = true; break; }
                        }
                    }
                    if (!hasAvailable) continue;
                }
            }

            string displayName = string.Empty;
            try { displayName = RebirthNpcWorldIntegrationService.GetDisplayName(state.StableId); }
            catch { displayName = string.Empty; }
            string dogIcon = string.Empty;
            if (dogCompanion)
            {
                RebirthDogBreedDefinition breed;
                if (RebirthDogStateService.TryGetBreed(state.StableId, out breed)) dogIcon = breed.IconName;
            }
            result.Add(new MobileTarget
            {
                Id = "N:" + state.StableId.ToString(),
                Name = SafeName(displayName, SafeName(npc.EntityName, Localization.Get("xuiRebirthCompanionStorage"))),
                Position = npc.position,
                IconAtlas = dogCompanion ? "ItemIconAtlas" : string.Empty,
                IconName = dogIcon,
                Npc = npc,
                NpcId = state.StableId,
                Resource = dogCompanion ? (IRemoteResourceSource)npcResource : null
            });
        }
        return result;
    }

    private static void GetMobilePresentation(
        IRemoteResourceSource source,
        string fallbackName,
        out string name,
        out string atlas,
        out string icon)
    {
        name = SafeName(source != null ? source.DisplayName : string.Empty, fallbackName);
        atlas = "ItemIconAtlas";
        icon = string.Empty;

        ItemValue itemValue = null;
        VehicleResourceSource vehicleSource = source as VehicleResourceSource;
        if (vehicleSource != null && vehicleSource.Vehicle != null &&
            vehicleSource.Vehicle.vehicle != null)
            itemValue = vehicleSource.Vehicle.vehicle.itemValue;

        DroneResourceSource droneSource = source as DroneResourceSource;
        if (itemValue == null && droneSource != null && droneSource.Drone != null)
            itemValue = droneSource.Drone.OriginalItemValue;

        if (itemValue == null || itemValue.IsEmpty()) return;
        ItemClass itemClass = ItemClass.GetForId(itemValue.type);
        if (itemClass == null) return;

        string localized = itemClass.GetLocalizedItemName();
        if (!string.IsNullOrWhiteSpace(localized)) name = localized.Trim();

        string actualIcon = itemClass.GetIconName();
        if (!string.IsNullOrWhiteSpace(actualIcon)) icon = actualIcon.Trim();

        if (vehicleSource != null && vehicleSource.Vehicle != null &&
            vehicleSource.Vehicle.vehicle != null && vehicleSource.Vehicle.vehicle.OwnerId != null)
        {
            string ownerName = RebirthBlockPickupAccessService.GetOwnerDisplayName(
                vehicleSource.Vehicle.vehicle.OwnerId);
            if (!string.IsNullOrWhiteSpace(ownerName))
                name = name + " ([6F9F67]" + ownerName.Trim() + "[-])";
        }
    }

    private static string GetMobileDisplayName(IRemoteResourceSource source, string fallbackName)
    {
        string name;
        string atlas;
        string icon;
        GetMobilePresentation(source, fallbackName, out name, out atlas, out icon);
        return name;
    }

    private static void SortTargets(EntityPlayer player, List<MobileTarget> targets)
    {
        targets.Sort(delegate(MobileTarget a, MobileTarget b)
        {
            int distance = (a.Position - player.position).sqrMagnitude.CompareTo(
                (b.Position - player.position).sqrMagnitude);
            if (distance != 0) return distance;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static MobileTarget SelectTarget(List<MobileTarget> targets, string requestedTargetId)
    {
        if (!string.IsNullOrEmpty(requestedTargetId))
            for (int i = 0; i < targets.Count; i++)
                if (string.Equals(targets[i].Id, requestedTargetId, StringComparison.Ordinal))
                    return targets[i];

        // Targets are sorted nearest-first, so an empty or stale selection always
        // falls back to the closest eligible destination.
        return targets.Count > 0 ? targets[0] : null;
    }

    private static void AppendPullRowsFromResource(
        LogisticsPreviewData data,
        IRemoteResourceSource source,
        string sourceName,
        ItemStack[] simulatedBag,
        PackedBoolArray bagLocks,
        string selectionKind = "R",
        string selectionSourceId = null)
    {
        if (source == null || simulatedBag == null) return;
        Dictionary<int, int> amounts = new Dictionary<int, int>();
        ItemStack[] slots = source.Slots;
        PackedBoolArray sourceLocks = source.SlotLocks;
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            if (IsLocked(sourceLocks, i)) continue;
            ItemStack item = slots[i];
            if (item == null || item.IsEmpty()) continue;
            ItemStack remaining = item.Clone();
            int before = remaining.count;
            SimulateInto(simulatedBag, bagLocks, remaining);
            int moved = before - remaining.count;
            if (moved > 0) AddAmount(amounts, item.itemValue.type, moved);
        }
        AddItemRowsForSource(data, amounts, sourceName, selectionKind,
            string.IsNullOrEmpty(selectionSourceId) ? source.StableId : selectionSourceId);
    }

    private static void AppendPullRowsFromNpc(
        LogisticsPreviewData data,
        RebirthNpcStableId npcId,
        string sourceName,
        ItemStack[] simulatedBag,
        PackedBoolArray bagLocks)
    {
        RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(npcId);
        if (snapshot == null || snapshot.Quantities == null || simulatedBag == null) return;
        Dictionary<int, int> amounts = new Dictionary<int, int>();
        List<KeyValuePair<string, int>> pairs = new List<KeyValuePair<string, int>>(snapshot.Quantities);
        pairs.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
        { return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase); });

        for (int i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].Value <= 0 || RebirthCompanionInventoryLockService.IsNpcItemLocked(npcId, pairs[i].Key)) continue;
            int reserved = 0;
            if (snapshot.Reservations != null) snapshot.Reservations.TryGetValue(pairs[i].Key, out reserved);
            int available = Math.Max(0, pairs[i].Value - reserved);
            if (available <= 0) continue;
            ItemValue value = ItemClass.GetItem(pairs[i].Key, false);
            if (value.IsEmpty() || value.ItemClass == null) continue;
            ItemStack remaining = new ItemStack(value.Clone(), available);
            int before = remaining.count;
            SimulateInto(simulatedBag, bagLocks, remaining);
            int moved = before - remaining.count;
            if (moved > 0) AddAmount(amounts, value.type, moved);
        }
        AddItemRowsForSource(data, amounts, sourceName, "N", npcId.ToString());
    }

    private static void BuildQualifiedPushRowsToResource(
        World world,
        EntityPlayer player,
        IRemoteResourceSource target,
        string selectedTargetId,
        LogisticsPreviewData data)
    {
        if (world == null || player == null || target == null) return;
        ItemStack[] simulatedTarget = CloneSlots(target.Slots);
        PackedBoolArray targetLocks = target.SlotLocks;

        AppendPushRowsFromSlots(data, player.bag.ItemGrid.items, player.bag.LockedSlots,
            Localization.Get("xuiRebirthQuickStackSourceBackpack"), "B", string.Empty,
            simulatedTarget, targetLocks);

        // Completed outputs in qualified nearby workstations can feed the selected
        // vehicle/drone directly without first routing through the backpack.
        List<WorkstationOutputResourceSource> workstationSources =
            GatherWorkstationOutputSources(world, player, true);
        for (int i = 0; i < workstationSources.Count; i++)
        {
            WorkstationOutputResourceSource workstation = workstationSources[i];
            AppendPushRowsFromSlots(data, workstation.Slots, workstation.SlotLocks,
                workstation.DisplayName, "W", workstation.StableId,
                CloneSlots(target.Slots), targetLocks);
        }

        // Any other qualified nearby vehicle or drone can feed the selected destination.
        List<IRemoteResourceSource> nearby = RemoteResourceRegistry.ResolveNearby(
            world, player, QuickStackService.Radius, false, false, true);
        List<IRemoteResourceSource> sources = new List<IRemoteResourceSource>();
        for (int i = 0; i < nearby.Count; i++)
        {
            IRemoteResourceSource source = nearby[i];
            if (source == null ||
                (source.Kind != RemoteResourceSourceKind.VehicleStorage &&
                 source.Kind != RemoteResourceSourceKind.DroneStorage) ||
                string.Equals(source.StableId, selectedTargetId, StringComparison.Ordinal)) continue;
            if (source.Kind == RemoteResourceSourceKind.DroneStorage &&
                (source.Position - player.position).sqrMagnitude > CompanionRadius * CompanionRadius) continue;
            string reason;
            if (!CanUseMobile(source, player, true, out reason)) continue;
            sources.Add(source);
        }
        sources.Sort(delegate(IRemoteResourceSource a, IRemoteResourceSource b)
        {
            return string.Compare(
                SafeName(a.DisplayName, a.StableId),
                SafeName(b.DisplayName, b.StableId),
                StringComparison.CurrentCultureIgnoreCase);
        });

        for (int i = 0; i < sources.Count; i++)
        {
            IRemoteResourceSource source = sources[i];
            string fallback = source.Kind == RemoteResourceSourceKind.DroneStorage
                ? Localization.Get("xuiRebirthDroneStorage")
                : Localization.Get("xuiRebirthVehicleStorage");
            AppendPushRowsFromSlots(data, source.Slots, source.SlotLocks,
                GetMobileDisplayName(source, fallback), "R", source.StableId,
                simulatedTarget, targetLocks);
        }

        List<MobileTarget> companions = GatherCompanionTargets(world, player, true);
        SortTargets(player, companions);
        for (int i = 0; i < companions.Count; i++)
        {
            MobileTarget companion = companions[i];
            if (companion.Resource != null)
            {
                // Drones were already handled through RemoteResourceRegistry. Dogs
                // are not in that registry and must remain visible as N:<stableId>
                // sources while using their fixed-slot inventory/locks.
                if (companion.Id.StartsWith("N:", StringComparison.Ordinal) &&
                    !string.Equals(companion.Id, selectedTargetId, StringComparison.Ordinal))
                    AppendPushRowsFromSlots(data, companion.Resource.Slots, companion.Resource.SlotLocks,
                        companion.Name, "N", companion.NpcId.ToString(),
                        CloneSlots(target.Slots), targetLocks);
                continue;
            }
            AppendPushRowsFromNpc(data, companion.NpcId, companion.Name,
                simulatedTarget, targetLocks);
        }

        SortItemRows(data);
    }

    private static void BuildQualifiedPushRowsToNpc(
        World world,
        EntityPlayer player,
        RebirthNpcStableId targetNpcId,
        string selectedTargetId,
        LogisticsPreviewData data)
    {
        if (world == null || player == null || targetNpcId.IsEmpty) return;

        // Logical Rebirth NPC inventories do not have a fixed slot grid, so every
        // item in a qualified source is transferable to the selected companion.
        AppendAllRowsFromSlots(data, player.bag.ItemGrid.items, player.bag.LockedSlots,
            Localization.Get("xuiRebirthQuickStackSourceBackpack"), "B", string.Empty);

        List<WorkstationOutputResourceSource> workstationSources =
            GatherWorkstationOutputSources(world, player, true);
        for (int i = 0; i < workstationSources.Count; i++)
        {
            WorkstationOutputResourceSource workstation = workstationSources[i];
            AppendAllRowsFromSlots(data, workstation.Slots, workstation.SlotLocks,
                workstation.DisplayName, "W", workstation.StableId);
        }

        List<IRemoteResourceSource> nearby = RemoteResourceRegistry.ResolveNearby(
            world, player, QuickStackService.Radius, false, false, true);
        List<IRemoteResourceSource> sources = new List<IRemoteResourceSource>();
        for (int i = 0; i < nearby.Count; i++)
        {
            IRemoteResourceSource source = nearby[i];
            if (source == null ||
                (source.Kind != RemoteResourceSourceKind.VehicleStorage &&
                 source.Kind != RemoteResourceSourceKind.DroneStorage)) continue;
            if (source.Kind == RemoteResourceSourceKind.DroneStorage &&
                (source.Position - player.position).sqrMagnitude > CompanionRadius * CompanionRadius) continue;
            string reason;
            if (!CanUseMobile(source, player, true, out reason)) continue;
            sources.Add(source);
        }
        sources.Sort(delegate(IRemoteResourceSource a, IRemoteResourceSource b)
        { return string.Compare(SafeName(a.DisplayName, a.StableId), SafeName(b.DisplayName, b.StableId),
            StringComparison.CurrentCultureIgnoreCase); });

        for (int i = 0; i < sources.Count; i++)
        {
            IRemoteResourceSource source = sources[i];
            string fallback = source.Kind == RemoteResourceSourceKind.DroneStorage
                ? Localization.Get("xuiRebirthDroneStorage")
                : Localization.Get("xuiRebirthVehicleStorage");
            AppendAllRowsFromSlots(data, source.Slots, source.SlotLocks,
                GetMobileDisplayName(source, fallback), "R", source.StableId);
        }

        List<MobileTarget> companions = GatherCompanionTargets(world, player, true);
        SortTargets(player, companions);
        for (int i = 0; i < companions.Count; i++)
        {
            MobileTarget companion = companions[i];
            if (companion.NpcId == targetNpcId ||
                string.Equals(companion.Id, selectedTargetId, StringComparison.Ordinal)) continue;
            if (companion.Resource != null)
            {
                if (companion.Id.StartsWith("N:", StringComparison.Ordinal))
                    AppendAllRowsFromSlots(data, companion.Resource.Slots, companion.Resource.SlotLocks,
                        companion.Name, "N", companion.NpcId.ToString());
                continue;
            }
            AppendAllRowsFromNpc(data, companion.NpcId, companion.Name);
        }

        SortItemRows(data);
    }

    private static void AppendPushRowsFromSlots(
        LogisticsPreviewData data,
        ItemStack[] sourceSlots,
        PackedBoolArray sourceLocks,
        string sourceName,
        string sourceKind,
        string sourceId,
        ItemStack[] simulatedTarget,
        PackedBoolArray targetLocks)
    {
        Dictionary<int, int> amounts = new Dictionary<int, int>();
        for (int i = 0; sourceSlots != null && i < sourceSlots.Length; i++)
        {
            if (IsLocked(sourceLocks, i)) continue;
            ItemStack source = sourceSlots[i];
            if (source == null || source.IsEmpty()) continue;
            ItemStack remaining = source.Clone();
            int before = remaining.count;
            SimulateInto(simulatedTarget, targetLocks, remaining);
            int moved = before - remaining.count;
            if (moved > 0) AddAmount(amounts, source.itemValue.type, moved);
        }
        AddItemRowsForSource(data, amounts, sourceName, sourceKind, sourceId);
    }

    private static void AppendPushRowsFromNpc(
        LogisticsPreviewData data,
        RebirthNpcStableId npcId,
        string sourceName,
        ItemStack[] simulatedTarget,
        PackedBoolArray targetLocks)
    {
        RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(npcId);
        if (snapshot == null || snapshot.Quantities == null) return;
        Dictionary<int, int> amounts = new Dictionary<int, int>();
        List<KeyValuePair<string, int>> pairs = new List<KeyValuePair<string, int>>(snapshot.Quantities);
        pairs.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
        { return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase); });
        for (int i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].Value <= 0 || RebirthCompanionInventoryLockService.IsNpcItemLocked(npcId, pairs[i].Key)) continue;
            int reserved = 0;
            if (snapshot.Reservations != null) snapshot.Reservations.TryGetValue(pairs[i].Key, out reserved);
            int available = Math.Max(0, pairs[i].Value - reserved);
            if (available <= 0) continue;
            ItemValue value = ItemClass.GetItem(pairs[i].Key, false);
            if (value.IsEmpty() || value.ItemClass == null) continue;
            ItemStack remaining = new ItemStack(value.Clone(), available);
            int before = remaining.count;
            SimulateInto(simulatedTarget, targetLocks, remaining);
            int moved = before - remaining.count;
            if (moved > 0) AddAmount(amounts, value.type, moved);
        }
        AddItemRowsForSource(data, amounts, sourceName, "N", npcId.ToString());
    }

    private static void AppendAllRowsFromSlots(
        LogisticsPreviewData data,
        ItemStack[] sourceSlots,
        PackedBoolArray sourceLocks,
        string sourceName,
        string sourceKind,
        string sourceId)
    {
        Dictionary<int, int> amounts = new Dictionary<int, int>();
        for (int i = 0; sourceSlots != null && i < sourceSlots.Length; i++)
        {
            if (IsLocked(sourceLocks, i)) continue;
            ItemStack source = sourceSlots[i];
            if (source != null && !source.IsEmpty()) AddAmount(amounts, source.itemValue.type, source.count);
        }
        AddItemRowsForSource(data, amounts, sourceName, sourceKind, sourceId);
    }

    private static void AppendAllRowsFromNpc(
        LogisticsPreviewData data,
        RebirthNpcStableId npcId,
        string sourceName)
    {
        RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(npcId);
        if (snapshot == null || snapshot.Quantities == null) return;
        Dictionary<int, int> amounts = new Dictionary<int, int>();
        foreach (KeyValuePair<string, int> pair in snapshot.Quantities)
        {
            if (pair.Value <= 0 || RebirthCompanionInventoryLockService.IsNpcItemLocked(npcId, pair.Key)) continue;
            int reserved = 0;
            if (snapshot.Reservations != null) snapshot.Reservations.TryGetValue(pair.Key, out reserved);
            int available = Math.Max(0, pair.Value - reserved);
            if (available <= 0) continue;
            ItemValue value = ItemClass.GetItem(pair.Key, false);
            if (value.IsEmpty() || value.ItemClass == null) continue;
            AddAmount(amounts, value.type, available);
        }
        AddItemRowsForSource(data, amounts, sourceName, "N", npcId.ToString());
    }

    private static void AddItemRowsForSource(
        LogisticsPreviewData data,
        Dictionary<int, int> amounts,
        string sourceName,
        string sourceKind,
        string sourceId)
    {
        foreach (KeyValuePair<int, int> pair in amounts)
        {
            if (pair.Value <= 0) continue;
            string key = BuildSourceSelectionKey(sourceKind, sourceId, pair.Key);
            data.ItemRows.Add(new LogisticsItemPreviewRow
            {
                ItemType = pair.Key,
                Count = pair.Value,
                SourceName = sourceName ?? string.Empty,
                Secondary = sourceName ?? string.Empty,
                SelectionKey = key,
                Selectable = !string.IsNullOrEmpty(sourceKind),
                Selected = true
            });
        }
    }

    private static void SortItemRows(LogisticsPreviewData data)
    {
        data.ItemRows.Sort(delegate(LogisticsItemPreviewRow a, LogisticsItemPreviewRow b)
        {
            string sourceA = a != null ? (a.SourceName ?? string.Empty) : string.Empty;
            string sourceB = b != null ? (b.SourceName ?? string.Empty) : string.Empty;
            int bySource = string.Compare(sourceA, sourceB, StringComparison.CurrentCultureIgnoreCase);
            if (bySource != 0) return bySource;

            ItemClass ia = a != null ? ItemClass.GetForId(a.ItemType) : null;
            ItemClass ib = b != null ? ItemClass.GetForId(b.ItemType) : null;
            string an = ia != null ? ia.GetLocalizedItemName() : (a != null ? a.ItemType.ToString() : string.Empty);
            string bn = ib != null ? ib.GetLocalizedItemName() : (b != null ? b.ItemType.ToString() : string.Empty);
            return string.Compare(an, bn, StringComparison.CurrentCultureIgnoreCase);
        });
    }

    private static Dictionary<int, int> PreviewPushToResource(EntityPlayer player, IRemoteResourceSource target)
    {
        Dictionary<int, int> amounts = new Dictionary<int, int>();
        ItemStack[] simulated = CloneSlots(target.Slots);
        PackedBoolArray targetLocks = target.SlotLocks;
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray bagLocks = player.bag.LockedSlots;
        for (int i = 0; i < bag.Length; i++)
        {
            if (IsLocked(bagLocks, i)) continue;
            ItemStack source = bag[i];
            if (source == null || source.IsEmpty()) continue;
            ItemStack remaining = source.Clone();
            int before = remaining.count;
            SimulateInto(simulated, targetLocks, remaining);
            int moved = before - remaining.count;
            if (moved > 0) AddAmount(amounts, source.itemValue.type, moved);
        }
        return amounts;
    }

    private static Dictionary<int, int> PreviewPullFromResource(EntityPlayer player, IRemoteResourceSource source)
    {
        Dictionary<int, int> amounts = new Dictionary<int, int>();
        ItemStack[] simulatedBag = CloneSlots(player.bag.ItemGrid.items);
        PackedBoolArray bagLocks = player.bag.LockedSlots;
        ItemStack[] slots = source.Slots;
        PackedBoolArray sourceLocks = source.SlotLocks;
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            if (IsLocked(sourceLocks, i)) continue;
            ItemStack item = slots[i];
            if (item == null || item.IsEmpty()) continue;
            ItemStack remaining = item.Clone();
            int before = remaining.count;
            SimulateInto(simulatedBag, bagLocks, remaining);
            int moved = before - remaining.count;
            if (moved > 0) AddAmount(amounts, item.itemValue.type, moved);
        }
        return amounts;
    }

    private static Dictionary<int, int> PreviewPushToNpc(EntityPlayer player)
    {
        Dictionary<int, int> amounts = new Dictionary<int, int>();
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locks = player.bag.LockedSlots;
        for (int i = 0; i < bag.Length; i++)
        {
            if (IsLocked(locks, i)) continue;
            ItemStack stack = bag[i];
            if (stack != null && !stack.IsEmpty()) AddAmount(amounts, stack.itemValue.type, stack.count);
        }
        return amounts;
    }

    private static Dictionary<int, int> PreviewPullFromNpc(EntityPlayer player, RebirthNpcStableId npcId)
    {
        Dictionary<int, int> amounts = new Dictionary<int, int>();
        RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(npcId);
        if (snapshot == null || snapshot.Quantities == null) return amounts;

        ItemStack[] simulatedBag = CloneSlots(player.bag.ItemGrid.items);
        PackedBoolArray bagLocks = player.bag.LockedSlots;
        List<KeyValuePair<string, int>> pairs = new List<KeyValuePair<string, int>>(snapshot.Quantities);
        pairs.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
        { return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase); });
        for (int i = 0; i < pairs.Count; i++)
        {
            if (RebirthCompanionInventoryLockService.IsNpcItemLocked(npcId, pairs[i].Key)) continue;
            ItemValue value = ItemClass.GetItem(pairs[i].Key, false);
            if (value.IsEmpty() || pairs[i].Value <= 0) continue;
            ItemStack remaining = new ItemStack(value.Clone(), pairs[i].Value);
            int before = remaining.count;
            SimulateInto(simulatedBag, bagLocks, remaining);
            int moved = before - remaining.count;
            if (moved > 0) AddAmount(amounts, value.type, moved);
        }
        return amounts;
    }

    private static bool IsQualifiedStatic(Vector3i pos, TEFeatureStorage loot, HashSet<int> playerItemTypes)
    {
        if (QuickStackAcceptedCategoryRegistry.HasAny(pos)) return true;
        if (loot == null || loot.ItemGrid.items == null || playerItemTypes == null || playerItemTypes.Count == 0) return false;
        for (int i = 0; i < loot.ItemGrid.items.Length; i++)
        {
            ItemStack stack = loot.ItemGrid.items[i];
            if (stack != null && !stack.IsEmpty() && playerItemTypes.Contains(stack.itemValue.type)) return true;
        }
        return false;
    }

    private static HashSet<int> GetPlayerItemTypes(EntityPlayer player, bool includeLocked)
    {
        HashSet<int> result = new HashSet<int>();
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locks = player.bag.LockedSlots;
        for (int i = 0; i < bag.Length; i++)
        {
            if (!includeLocked && IsLocked(locks, i)) continue;
            ItemStack stack = bag[i];
            if (stack != null && !stack.IsEmpty()) result.Add(stack.itemValue.type);
        }
        return result;
    }

    internal static bool CanUseMobile(
        IRemoteResourceSource source,
        EntityPlayer player,
        bool requireItems,
        out string reason)
    {
        reason = string.Empty;
        if (source == null || !source.IsLoaded) { reason = "unloaded"; return false; }
        if (source.IsBusy) { reason = "open, reserved, or being edited"; return false; }
        PlatformUserIdentifierAbs userId;
        if (!RemoteResourceAccess.TryGetPersistentId(player, out userId))
        { reason = "missing persistent player"; return false; }
        if (RemoteResourceStateStore.IsExcluded(source.StableId, userId))
        { reason = "excluded for this player"; return false; }
        if (!source.IsAuthorized(player, false, out reason)) return false;
        if (requireItems && !RemoteResourceAccess.HasUsableItem(source.Slots))
        { reason = "empty"; return false; }
        return true;
    }

    internal static bool CanAccessNpcCompanion(EntityPlayer player, RebirthNpcRuntimeState state)
    {
        if (player == null || state == null || state.OwnershipKind == RebirthNpcOwnershipKind.None) return false;
        PlatformUserIdentifierAbs userId;
        if (!RemoteResourceAccess.TryGetPersistentId(player, out userId) || userId == null) return false;
        string viewer = userId.CombinedString;
        if (string.IsNullOrEmpty(viewer) || string.IsNullOrEmpty(state.OwnerId)) return false;
        if (string.Equals(viewer, state.OwnerId, StringComparison.OrdinalIgnoreCase)) return true;
        if (state.OwnershipKind == RebirthNpcOwnershipKind.Party && RebirthNpcWorldIntegrationAdapters.IsPartyMember != null)
            return RebirthNpcWorldIntegrationAdapters.IsPartyMember(viewer, state.OwnerId);
        return false;
    }

    private static bool IsCompanionCategory(RebirthNpcCategory category)
    {
        return category == RebirthNpcCategory.Survivor ||
               category == RebirthNpcCategory.DogCompanion ||
               category == RebirthNpcCategory.PantherCompanion;
    }

    private static void AddTarget(
        LogisticsPreviewData data,
        string id,
        string name,
        Vector3 position,
        EntityPlayer player,
        bool selected,
        string iconAtlas = "",
        string iconName = "")
    {
        float distance = Vector3.Distance(player.position, position);
        string distanceText = distance.ToString("0.0") + "m";
        string safeName = SafeName(name, Localization.Get("xuiRebirthContainerStorage"));
        string safeId = id ?? string.Empty;
        data.TargetIds.Add(safeId);
        data.TargetNames.Add(safeName + "  —  " + distanceText);
        data.Targets.Add((selected ? "▶ " : "• ") + safeName + "  •  " + distanceText);
        data.TargetRows.Add(new LogisticsTargetPreviewRow
        {
            Id = safeId,
            Name = safeName,
            Distance = distanceText,
            IconAtlas = iconAtlas ?? string.Empty,
            IconName = iconName ?? string.Empty,
            Selected = selected
        });
    }

    private static void GetStaticContainerIcon(
        World world,
        Vector3i position,
        out string atlas,
        out string icon)
    {
        atlas = "ItemIconAtlas";
        icon = string.Empty;
        if (world == null) return;
        try
        {
            BlockValue blockValue = world.GetBlock(position);
            if (blockValue.Block == null) return;
            // Use the placed block's authoritative UI icon directly. Block.GetIconName()
            // respects CustomIcon and otherwise falls back to the actual block name,
            // so storage rows never substitute a generic crate or infer an item icon.
            icon = blockValue.Block.GetIconName() ?? string.Empty;
        }
        catch { icon = string.Empty; }
    }

    private static void AddWorkstationTarget(
        LogisticsPreviewData data,
        World world,
        WorkstationOutputResourceSource source,
        EntityPlayer player,
        bool selected)
    {
        string atlas = "ItemIconAtlas";
        string icon = string.Empty;
        try
        {
            BlockValue blockValue = world.GetBlock(World.worldToBlockPos(source.Position));
            if (blockValue.Block != null)
            {
                // Workstation rows use the actual placed block icon as well, including
                // any CustomIcon declared by that workstation block.
                icon = blockValue.Block.GetIconName() ?? string.Empty;
            }
        }
        catch { icon = string.Empty; }
        AddTarget(data, source.StableId, source.DisplayName, source.Position, player, selected, atlas, icon);
    }

    private static string SafeName(string value, string fallback)
    { return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim(); }

    private static void AddItemLines(LogisticsPreviewData data, Dictionary<int, int> amounts, string suffix)
    {
        List<KeyValuePair<int, int>> pairs = new List<KeyValuePair<int, int>>(amounts);
        pairs.Sort(delegate(KeyValuePair<int, int> a, KeyValuePair<int, int> b)
        {
            ItemClass ia = ItemClass.GetForId(a.Key);
            ItemClass ib = ItemClass.GetForId(b.Key);
            string an = ia != null ? ia.GetLocalizedItemName() : a.Key.ToString();
            string bn = ib != null ? ib.GetLocalizedItemName() : b.Key.ToString();
            return string.Compare(an, bn, StringComparison.CurrentCultureIgnoreCase);
        });
        for (int i = 0; i < pairs.Count; i++)
        {
            ItemClass item = ItemClass.GetForId(pairs[i].Key);
            string name = item != null ? item.GetLocalizedItemName() : pairs[i].Key.ToString();
            string secondary = (suffix ?? string.Empty).Trim();
            string sourceName = string.Empty;
            if (secondary.StartsWith("Source: ", StringComparison.OrdinalIgnoreCase))
                sourceName = secondary.Substring(8).Trim();
            else if (secondary.StartsWith("Destination: ", StringComparison.OrdinalIgnoreCase))
                sourceName = Localization.Get("xuiRebirthQuickStackSourceBackpack");
            data.Items.Add("• " + name + "   x" + pairs[i].Value +
                           (secondary.Length > 0 ? "  •  " + secondary : string.Empty));
            data.ItemRows.Add(new LogisticsItemPreviewRow
            {
                ItemType = pairs[i].Key,
                Count = pairs[i].Value,
                Secondary = secondary,
                SourceName = sourceName
            });
        }
    }

    private static void BuildLegacyFallbackLines(LogisticsPreviewData data, QuickStackRadialAction action)
    {
        // Keep the legacy text fields useful for diagnostics/network compatibility.
        if (data.Items.Count == 0 && data.ItemRows.Count > 0)
        {
            for (int i = 0; i < data.ItemRows.Count; i++)
            {
                LogisticsItemPreviewRow row = data.ItemRows[i];
                ItemClass item = ItemClass.GetForId(row.ItemType);
                string name = item != null ? item.GetLocalizedItemName() : row.ItemType.ToString();
                string secondary = string.IsNullOrEmpty(row.Secondary) ? string.Empty : "  •  " + row.Secondary;
                data.Items.Add("• " + name + "   x" + row.Count + secondary);
            }
        }

        if (data.Items.Count == 0)
            data.Items.Add(Localization.Get("xuiRebirthQuickStackPreviewNone"));

        if (data.Targets.Count == 0)
        {
            string key;
            switch (action)
            {
                case QuickStackRadialAction.CollectWorkstationOutputs:
                    key = "xuiRebirthNoNearbyWorkstationOutputs";
                    break;
                case QuickStackRadialAction.PushVehicle:
                case QuickStackRadialAction.PullVehicle:
                    key = "xuiRebirthNoNearbyVehicles";
                    break;
                case QuickStackRadialAction.PushDrone:
                case QuickStackRadialAction.PullDrone:
                    key = "xuiRebirthNoNearbyCompanions";
                    break;
                default:
                    key = "xuiRebirthNoQualifiedNearbyStorage";
                    break;
            }
            data.Targets.Add(Localization.Get(key));
        }
    }

    internal static int SimulateInto(ItemStack[] destination, PackedBoolArray locks, ItemStack remaining)
    {
        return SimulateInto(destination, locks, remaining, true);
    }

    internal static int SimulateInto(
        ItemStack[] destination,
        PackedBoolArray locks,
        ItemStack remaining,
        bool allowNewStacks)
    {
        if (destination == null || remaining == null || remaining.IsEmpty()) return 0;
        int before = remaining.count;

        // Existing matching stacks may be topped up even when the destination slot
        // is protected/locked. A lock protects the stack from being moved out; it
        // does not prevent more of the same item from stacking onto it.
        for (int i = 0; i < destination.Length && remaining.count > 0; i++)
        {
            ItemStack current = destination[i];
            if (current == null || current.IsEmpty()) continue;
            int amount;
            if (!current.CanStackPartlyWith(remaining, out amount) || amount <= 0) continue;
            ItemStack after = current.Clone();
            after.count += amount;
            remaining.count -= amount;
            destination[i] = after;
        }

        if (!allowNewStacks || remaining.count <= 0)
            return before - remaining.count;

        ItemClass itemClass = ItemClass.GetForId(remaining.itemValue.type);
        int maxStack = itemClass != null ? itemClass.Stacknumber.Value : 1;
        for (int i = 0; i < destination.Length && remaining.count > 0; i++)
        {
            if (IsLocked(locks, i)) continue; // do not create a new stack in a protected empty slot
            if (destination[i] != null && !destination[i].IsEmpty()) continue;
            int amount = Math.Min(maxStack, remaining.count);
            destination[i] = new ItemStack(remaining.itemValue.Clone(), amount);
            remaining.count -= amount;
        }
        return before - remaining.count;
    }

    internal static ItemStack[] CloneSlots(ItemStack[] slots)
    {
        if (slots == null) return new ItemStack[0];
        ItemStack[] result = new ItemStack[slots.Length];
        for (int i = 0; i < slots.Length; i++)
            result[i] = slots[i] != null ? slots[i].Clone() : ItemStack.Empty.Clone();
        return result;
    }

    internal static bool IsLocked(PackedBoolArray locks, int index)
    { return locks != null && index >= 0 && index < locks.Length && locks[index]; }

    private static bool HasUsableUnlockedItem(ItemStack[] slots, PackedBoolArray locks)
    {
        for (int i = 0; slots != null && i < slots.Length; i++)
            if (!IsLocked(locks, i) && slots[i] != null && !slots[i].IsEmpty() && slots[i].count > 0)
                return true;
        return false;
    }

    private static int TotalCount(ItemStack[] slots)
    {
        int total = 0;
        for (int i = 0; slots != null && i < slots.Length; i++)
            if (slots[i] != null && !slots[i].IsEmpty()) total += slots[i].count;
        return total;
    }

    private static void AddAmount(Dictionary<int, int> values, int type, int amount)
    {
        if (amount <= 0) return;
        int current;
        values.TryGetValue(type, out current);
        values[type] = current + amount;
    }
}

public enum LogisticsTransferRequestMode : byte
{
    ExecuteOrReplay = 1,
    QueryOnly = 2
}

public static class LogisticsTransferService
{
    private const float CompanionRadius = 30f;
    private const string AllEligibleDepositToken = "\u001eREBIRTH_QUICKSTACK_ALL\u001e";
    private static Action transferCompleted;
    private const float PendingOutcomeQuerySeconds = 3f;
    private static int nextRequestId;
    private static int pendingRequestId;
    private static int lastAppliedResultRequestId;
    private static ulong clientRequestEpoch = CreateClientRequestEpoch();
    private static ulong pendingRequestEpoch;
    private static float pendingRequestDeadline;
    private static bool pendingRequestWasHotkey;
    private static QuickStackRadialAction pendingAction;
    private static string pendingTargetId = string.Empty;
    private static string pendingSourceKeys = string.Empty;

    private static ulong CreateClientRequestEpoch()
    {
        ulong value = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
        return value == 0 ? 1UL : value;
    }

    internal static bool ClientChannelReady(ConnectionManager connection, NetPackage packet)
    {
        if (connection == null || connection.IsServer || !connection.IsConnected || packet == null) return false;
        var channels = connection.GetConnectionToServer();
        int channel = packet.Channel;
        return channels != null && channel >= 0 && channel < channels.Length &&
            channels[channel] != null && !channels[channel].IsDisconnected();
    }

    internal static bool CanDispatch(World world, EntityPlayer player, QuickStackRadialAction action, out string reason)
    {
        reason = string.Empty;
        if (world == null || world.IsRemote() || player == null) { reason = "authority or player unavailable"; return false; }
        if (action == QuickStackRadialAction.CompanionInventoryPull) return true; // Target authorization is performed by the companion service.
        action = LogisticsInventoryCredits.ExecutionAction(action);
        if ((byte)action > (byte)QuickStackRadialAction.PullDrone) { reason = "unsupported transfer action"; return false; }
        if (!QuickStackRuntimePolicy.Enabled) { reason = "Quick Stack is disabled"; return false; }
        return true;
    }

    internal static bool CanMutateResource(EntityPlayer player, IRemoteResourceSource source, bool requireItems)
    {
        if (!QuickStackRuntimePolicy.Enabled || player == null || player.world == null || player.world.IsRemote() || source == null) return false;
        float radius = source.Kind == RemoteResourceSourceKind.DroneStorage || source.Kind == RemoteResourceSourceKind.NpcStorage
            ? CompanionRadius : QuickStackService.Radius;
        if ((source.Position - player.position).sqrMagnitude > radius * radius) return false;
        string reason;
        return LogisticsPreviewService.CanUseMobile(source, player, requireItems, out reason);
    }

    private static void AddTransferNotification(
        Dictionary<int, int> notifications,
        int itemType,
        int signedCount)
    {
        if (notifications == null || itemType <= 0 || signedCount == 0) return;
        int current;
        notifications.TryGetValue(itemType, out current);
        int combined = current + signedCount;
        if (combined == 0) notifications.Remove(itemType);
        else notifications[itemType] = combined;
    }

    private static bool HasTransferNotifications(Dictionary<int, int> notifications)
    {
        if (notifications == null || notifications.Count == 0) return false;
        foreach (KeyValuePair<int, int> pair in notifications)
            if (pair.Key > 0 && pair.Value != 0) return true;
        return false;
    }

    private static bool HasTransferNotifications(int[] itemTypes, int[] signedCounts)
    {
        if (itemTypes == null || signedCounts == null) return false;
        int count = Math.Min(itemTypes.Length, signedCounts.Length);
        for (int i = 0; i < count; i++)
            if (itemTypes[i] > 0 && signedCounts[i] != 0) return true;
        return false;
    }

    private static void PlayHotkeyTransferSound()
    {
        Audio.Manager.PlayInsidePlayerHead("item_pickup");
        { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("hotkey transfer completed with items moved; played vanilla item_pickup sound."); }
    }

    private static Dictionary<int, int> CaptureBagQuantities(EntityPlayer player)
    {
        Dictionary<int, int> result = new Dictionary<int, int>();
        if (player == null || player.bag == null) return result;
        ItemStack[] slots = player.bag.ItemGrid.items;
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty() || stack.count <= 0) continue;
            int current;
            result.TryGetValue(stack.itemValue.type, out current);
            result[stack.itemValue.type] = current + stack.count;
        }
        return result;
    }

    private static void AddBagDeltaNotifications(
        Dictionary<int, int> before,
        EntityPlayer player,
        Dictionary<int, int> notifications)
    {
        if (notifications == null || player == null) return;
        Dictionary<int, int> after = CaptureBagQuantities(player);
        HashSet<int> types = new HashSet<int>();
        if (before != null)
            foreach (KeyValuePair<int, int> pair in before) types.Add(pair.Key);
        foreach (KeyValuePair<int, int> pair in after) types.Add(pair.Key);

        foreach (int itemType in types)
        {
            int oldCount = 0;
            int newCount = 0;
            if (before != null) before.TryGetValue(itemType, out oldCount);
            after.TryGetValue(itemType, out newCount);
            int delta = newCount - oldCount;
            if (delta != 0) AddTransferNotification(notifications, itemType, delta);
        }
    }

    private static void ShowTransferNotifications(
        EntityPlayer player,
        Dictionary<int, int> notifications)
    {
        EntityPlayerLocal localPlayer = player as EntityPlayerLocal;
        if (localPlayer == null || notifications == null || notifications.Count == 0) return;

        List<int> itemTypes = new List<int>(notifications.Keys);
        itemTypes.Sort();
        for (int i = 0; i < itemTypes.Count; i++)
        {
            int itemType = itemTypes[i];
            int signedCount;
            if (!notifications.TryGetValue(itemType, out signedCount) || signedCount == 0) continue;
            ItemClass itemClass = ItemClass.GetForId(itemType);
            if (itemClass == null || string.IsNullOrWhiteSpace(itemClass.Name)) continue;
            ItemValue value = ItemClass.GetItem(itemClass.Name, false);
            if (value.IsEmpty()) continue;

            localPlayer.AddUIHarvestingItem(
                new ItemStack(value.Clone(), signedCount), false);

            if (QuickStackDiagnostics.Enabled)
                { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write(
                    "transfer notification item='" + itemClass.Name +
                    "' count=" + signedCount); }
        }
    }

    internal static void ShowTransferNotifications(int[] itemTypes, int[] signedCounts)
    {
        if (itemTypes == null || signedCounts == null) return;
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer()
            : null;
        if (player == null) return;

        Dictionary<int, int> notifications = new Dictionary<int, int>();
        int count = Math.Min(itemTypes.Length, signedCounts.Length);
        for (int i = 0; i < count; i++)
            AddTransferNotification(notifications, itemTypes[i], signedCounts[i]);
        ShowTransferNotifications(player, notifications);
    }

    public static void RequestAllEligibleDeposit()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
            "RequestAllEligibleDeposit enter policyEnabled=" + QuickStackRuntimePolicy.Enabled +
            " mode=" + QuickStackRuntimePolicy.Mode +
            " connectionNull=" + (connection == null) +
            " isServer=" + (connection != null && connection.IsServer) +
            " pendingRequestId=" + pendingRequestId); }

        if (!QuickStackRuntimePolicy.Enabled)
        {
            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("RequestAllEligibleDeposit BLOCKED reason=runtime-policy-off"); }
            return;
        }

        // The hotkey is intentionally fire-once. Do not queue a second remote transfer
        // before the authoritative bag/result for the first one has returned.
        if (connection != null && !connection.IsServer && pendingRequestId != 0)
        {
            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                "RequestAllEligibleDeposit BLOCKED reason=remote-request-still-pending pendingRequestId=" +
                pendingRequestId); }
            return;
        }

        { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("RequestAllEligibleDeposit forwarding to Request(Deposit, ALL token)."); }
        Request(QuickStackRadialAction.Deposit, string.Empty, AllEligibleDepositToken, null);
    }

    public static void Request(QuickStackRadialAction action, string selectedTargetId)
    {
        Request(action, selectedTargetId, string.Empty, null);
    }

    public static void Request(
        QuickStackRadialAction action,
        string selectedTargetId,
        string selectedSourceKeys)
    {
        Request(action, selectedTargetId, selectedSourceKeys, null);
    }

    public static void Request(
        QuickStackRadialAction action,
        string selectedTargetId,
        string selectedSourceKeys,
        Action onCompleted)
    {
        action = LogisticsInventoryCredits.RequestAction(action);
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer()
            : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;

        bool hotkeyAll = action == QuickStackRadialAction.Deposit &&
                         string.Equals(selectedSourceKeys, AllEligibleDepositToken, StringComparison.Ordinal);
        if (hotkeyAll)
        {
            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                "Request enter hotkey-all playerNull=" + (player == null) +
                " persistentNull=" + (persistent == null) +
                " primaryIdNull=" + (persistent == null || persistent.PrimaryId == null)); }
        }

        if (player == null || persistent == null || persistent.PrimaryId == null)
        {
            if (hotkeyAll)
                { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("Request BLOCKED reason=missing-local-player-or-persistent-id"); }
            return;
        }

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null)
        {
            if (hotkeyAll)
                { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("Request BLOCKED reason=connection-manager-null"); }
            return;
        }

        if (!connection.IsServer && pendingRequestId != 0)
        {
            if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("transfer request blocked while authoritative outcome remains pending requestId=" + pendingRequestId);
            return;
        }

        if (connection.IsServer)
        {
            string dispatchDenial;
            if (!CanDispatch(GameManager.Instance.World, player, action, out dispatchDenial))
            {
                GameManager.ShowTooltip(player, dispatchDenial);
                if (onCompleted != null) onCompleted();
                return;
            }
            if (hotkeyAll)
                { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                    "Request executing hotkey-all locally on authoritative server entity=" + player.entityId); }
            Dictionary<int, int> notifications = ProcessWithNotifications(
                GameManager.Instance.World, player.entityId, persistent.PrimaryId,
                action, selectedTargetId, selectedSourceKeys);
            ShowTransferNotifications(player, notifications);
            if (hotkeyAll && HasTransferNotifications(notifications))
                PlayHotkeyTransferSound();
            if (hotkeyAll)
                { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("Request local authoritative Process returned."); }
            if (onCompleted != null) onCompleted();
            return;
        }

        var packet = NetPackageManager.GetPackage<NetPackageLogisticsTransferRequest>();
        if (!ClientChannelReady(connection, packet))
        {
            GameManager.ShowTooltip(player, Localization.Get("xuiRebirthInventoryTransferUnavailable"));
            return;
        }

        int requestId = ++nextRequestId;
        if (requestId <= 0)
        {
            nextRequestId = 1;
            requestId = 1;
            clientRequestEpoch = CreateClientRequestEpoch();
            lastAppliedResultRequestId = 0;
        }
        pendingRequestId = requestId;
        pendingRequestEpoch = clientRequestEpoch;
        pendingRequestDeadline = Time.unscaledTime + PendingOutcomeQuerySeconds;
        pendingRequestWasHotkey = hotkeyAll;
        pendingAction = action;
        pendingTargetId = selectedTargetId ?? string.Empty;
        pendingSourceKeys = selectedSourceKeys ?? string.Empty;
        transferCompleted = onCompleted;
        if (hotkeyAll)
        {
            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                "Request sending hotkey-all package to server requestId=" + requestId +
                " entity=" + player.entityId); }
        }
        connection.SendToServer(packet
            .Setup(player.entityId, persistent, action, selectedTargetId, selectedSourceKeys,
                player.bag, requestId, pendingRequestEpoch, LogisticsTransferRequestMode.ExecuteOrReplay));
        if (hotkeyAll)
            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("Request hotkey-all package queued requestId=" + requestId); }
    }

    internal static void ReceiveCompleted(
        ulong requestEpoch,
        int requestId,
        bool outcomeKnown,
        ItemStack[] serverBagItems,
        int[] notificationItemTypes,
        int[] notificationSignedCounts)
    {
        if (requestEpoch != pendingRequestEpoch || requestId != pendingRequestId) return;
        if (!outcomeKnown)
        {
            pendingRequestDeadline = Time.unscaledTime + PendingOutcomeQuerySeconds;
            return;
        }
        { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
            "ReceiveCompleted requestId=" + requestId +
            " pendingRequestId=" + pendingRequestId +
            " lastAppliedResultRequestId=" + lastAppliedResultRequestId +
            " serverBagItems=" + (serverBagItems == null ? -1 : serverBagItems.Length)); }

        // Do not let a delayed older transfer response overwrite a newer authoritative
        // bag. In normal reliable ordering this never triggers, but it makes the custom
        // sync robust if multiple Execute clicks are queued before the UI refreshes.
        bool isNewResult = requestId > 0 && requestId > lastAppliedResultRequestId;
        if (requestId > 0 && requestId >= lastAppliedResultRequestId)
        {
            lastAppliedResultRequestId = requestId;
            if (LogisticsInventoryCredits.UsesReceipt(pendingAction))
                LogisticsInventoryCredits.Apply(serverBagItems);
            else
                QuickStackRemotePlayerBagSync.ApplyClientItems(serverBagItems);
        }
        if (isNewResult)
        {
            if (serverBagItems == null)
            {
                EntityPlayerLocal local = GameManager.Instance?.World?.GetPrimaryPlayer();
                if (local != null) GameManager.ShowTooltip(local, "Quick Stack transfer denied (disabled or unavailable). Inventory was not changed.");
            }
            ShowTransferNotifications(notificationItemTypes, notificationSignedCounts);
            if (requestId == pendingRequestId && pendingRequestWasHotkey &&
                HasTransferNotifications(notificationItemTypes, notificationSignedCounts))
            {
                PlayHotkeyTransferSound();
            }
        }

        if (requestId != pendingRequestId)
        {
            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                "ReceiveCompleted ignored for pending-clear because requestId does not match current pending request."); }
            return;
        }
        pendingRequestId = 0;
        pendingRequestEpoch = 0;
        pendingRequestDeadline = 0f;
        pendingRequestWasHotkey = false;
        pendingTargetId = string.Empty;
        pendingSourceKeys = string.Empty;
        { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("ReceiveCompleted cleared pending hotkey/transfer request."); }
        Action callback = transferCompleted;
        transferCompleted = null;
        if (callback != null) callback();
    }

    public static void UpdatePendingRequest()
    {
        if (pendingRequestId == 0 || pendingRequestEpoch == 0 || Time.unscaledTime < pendingRequestDeadline) return;
        pendingRequestDeadline = Time.unscaledTime + PendingOutcomeQuerySeconds;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        PersistentPlayerData persistent = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        if (connection == null || connection.IsServer || player == null || persistent == null || persistent.PrimaryId == null) return;
        var packet = NetPackageManager.GetPackage<NetPackageLogisticsTransferRequest>();
        if (!ClientChannelReady(connection, packet)) return;
        connection.SendToServer(packet
            .Setup(player.entityId, persistent, pendingAction, pendingTargetId, pendingSourceKeys, null,
                pendingRequestId, pendingRequestEpoch, LogisticsTransferRequestMode.QueryOnly));
    }

    public static void ResetClientRequestScope()
    {
        pendingRequestId = 0; pendingRequestEpoch = 0; pendingRequestDeadline = 0f;
        pendingRequestWasHotkey = false; pendingTargetId = string.Empty; pendingSourceKeys = string.Empty;
        transferCompleted = null; lastAppliedResultRequestId = 0;
        unchecked { clientRequestEpoch++; }
        if (clientRequestEpoch == 0) clientRequestEpoch = 1;
    }

    public static void Process(
        World world,
        int playerId,
        PlatformUserIdentifierAbs userId,
        QuickStackRadialAction action,
        string selectedTargetId)
    {
        Process(world, playerId, userId, action, selectedTargetId, string.Empty);
    }

    public static void Process(
        World world,
        int playerId,
        PlatformUserIdentifierAbs userId,
        QuickStackRadialAction action,
        string selectedTargetId,
        string selectedSourceKeys)
    {
        ProcessWithNotifications(world, playerId, userId, action, selectedTargetId, selectedSourceKeys);
    }

    internal static Dictionary<int, int> ProcessWithNotifications(
        World world,
        int playerId,
        PlatformUserIdentifierAbs userId,
        QuickStackRadialAction action,
        string selectedTargetId,
        string selectedSourceKeys)
    {
        action = LogisticsInventoryCredits.ExecutionAction(action);
        Dictionary<int, int> notifications = new Dictionary<int, int>();
        EntityPlayer player = world != null ? world.GetEntity(playerId) as EntityPlayer : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId)
            : null;
        bool hotkeyAll = action == QuickStackRadialAction.Deposit &&
                         string.Equals(selectedSourceKeys, AllEligibleDepositToken, StringComparison.Ordinal);

        if (hotkeyAll)
        {
            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                "server Process received hotkey-all playerId=" + playerId +
                " worldNull=" + (world == null) +
                " playerNull=" + (player == null) +
                " persistentNull=" + (persistent == null) +
                " userIdNull=" + (userId == null)); }
        }

        if (player == null || persistent == null || persistent.PrimaryId == null || userId == null ||
            !persistent.PrimaryId.Equals(userId))
        {
            if (hotkeyAll)
                { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("server Process BLOCKED reason=sender-or-persistent-validation"); }
            return notifications;
        }

        string dispatchDenial;
        if (!CanDispatch(world, player, action, out dispatchDenial))
        {
            if (QuickStackDiagnostics.Enabled) { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("transfer denied: " + dispatchDenial); }
            return notifications;
        }

        if (action == QuickStackRadialAction.CompanionInventoryPull)
        {
            int slot, itemType, quantity;
            string itemKey;
            if (!RebirthCompanionService.TryReadInventoryTransferSelection(selectedSourceKeys,
                out slot, out itemType, out quantity, out itemKey)) return notifications;
            Dictionary<int, int> before = CaptureBagQuantities(player);
            RebirthCompanionService.ProcessInventoryAction(world, playerId, userId, selectedTargetId,
                RebirthCompanionInventoryAction.TransferToPlayer, slot, itemType, itemKey, quantity);
            AddBagDeltaNotifications(before, player, notifications);
            return notifications;
        }

        if (action == QuickStackRadialAction.Deposit)
        {
            if (hotkeyAll)
            {
                { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("server Process accepted hotkey-all; building all eligible selection."); }
                ProcessAllEligibleQuickStack(world, player, notifications);
                { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("server ProcessAllEligibleQuickStack returned."); }
                return notifications;
            }
            ProcessSelectedQuickStack(world, player, false, selectedSourceKeys, notifications);
            return notifications;
        }
        if (action == QuickStackRadialAction.DepositOwned)
        {
            ProcessSelectedQuickStack(world, player, true, selectedSourceKeys, notifications);
            return notifications;
        }
        if (action == QuickStackRadialAction.Restock)
        {
            Dictionary<int, int> before = CaptureBagQuantities(player);
            QuickStackRestockService.ProcessServerSelected(world, playerId, userId, false, selectedSourceKeys);
            AddBagDeltaNotifications(before, player, notifications);
            return notifications;
        }
        if (action == QuickStackRadialAction.CollectWorkstationOutputs)
        {
            Dictionary<int, int> before = CaptureBagQuantities(player);
            QuickStackWorkstationOutputService.ProcessServerSelected(
                world, playerId, userId, selectedTargetId, selectedSourceKeys);
            AddBagDeltaNotifications(before, player, notifications);
            return notifications;
        }

        if (action == QuickStackRadialAction.PullVehicle ||
            action == QuickStackRadialAction.PushVehicle ||
            action == QuickStackRadialAction.PullDrone ||
            action == QuickStackRadialAction.PushDrone)
        {
            ProcessSelectedMobileTransfer(
                world, player, action, selectedTargetId, selectedSourceKeys, notifications);
            return notifications;
        }

        return notifications;
    }

    private sealed class SelectedMobileSource
    {
        public string Kind = string.Empty;
        public string SourceId = string.Empty;
        public int ItemType;
        public int MaxCount;
    }

    private static List<SelectedMobileSource> ParseSelectedMobileSources(string selectedSourceKeys)
    {
        List<SelectedMobileSource> result = new List<SelectedMobileSource>();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        string[] values = (selectedSourceKeys ?? string.Empty).Split('\u001f');
        for (int i = 0; i < values.Length; i++)
        {
            string value = (values[i] ?? string.Empty).Trim();
            if (value.Length == 0) continue;

            string key = value;
            int maxCount = int.MaxValue;
            int quantitySeparator = value.LastIndexOf('\u001d');
            if (quantitySeparator >= 0)
            {
                key = value.Substring(0, quantitySeparator);
                int parsed;
                if (!int.TryParse(value.Substring(quantitySeparator + 1), out parsed) || parsed <= 0)
                    continue;
                maxCount = parsed;
            }

            string kind;
            string sourceId;
            int itemType;
            if (!LogisticsPreviewService.TryParseSourceSelectionKey(
                key, out kind, out sourceId, out itemType) || !seen.Add(key)) continue;

            result.Add(new SelectedMobileSource
            {
                Kind = kind,
                SourceId = sourceId,
                ItemType = itemType,
                MaxCount = maxCount
            });
        }
        return result;
    }

    private static void ProcessSelectedMobileTransfer(
        World world,
        EntityPlayer player,
        QuickStackRadialAction action,
        string selectedTargetId,
        string selectedSourceKeys,
        Dictionary<int, int> notifications)
    {
        if (world == null || player == null) return;
        List<SelectedMobileSource> selected = ParseSelectedMobileSources(selectedSourceKeys);
        if (selected.Count == 0) return; // unchecked-all intentionally transfers nothing

        if (action == QuickStackRadialAction.PullVehicle || action == QuickStackRadialAction.PullDrone)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                SelectedMobileSource request = selected[i];
                if (request.Kind == "R")
                {
                    if (action == QuickStackRadialAction.PullVehicle &&
                        !request.SourceId.StartsWith("V:", StringComparison.Ordinal)) continue;
                    if (action == QuickStackRadialAction.PullDrone &&
                        !request.SourceId.StartsWith("D:", StringComparison.Ordinal)) continue;

                    IRemoteResourceSource source = FindSelectedMobileSource(
                        world, player, request.SourceId, true);
                    if (source != null)
                    {
                        int moved = TransferResourceItemToBag(
                            player, source, request.ItemType, request.MaxCount);
                        AddTransferNotification(notifications, request.ItemType, moved);
                    }
                    continue;
                }

                if (action == QuickStackRadialAction.PullDrone && request.Kind == "N")
                {
                    RebirthNpcStableId npcId;
                    if (!RebirthNpcStableId.TryParse(request.SourceId, out npcId)) continue;
                    if (ResolveNpcTarget(world, player, npcId) != null)
                    {
                        int moved = TransferNpcItemToBackpack(
                            world, player, npcId, request.ItemType, request.MaxCount);
                        AddTransferNotification(notifications, request.ItemType, moved);
                    }
                }
            }
            return;
        }

        if (string.IsNullOrEmpty(selectedTargetId)) return;

        IRemoteResourceSource resourceTarget = null;
        RebirthNpcStableId npcTargetId = default(RebirthNpcStableId);
        bool targetIsNpc = false;

        if (action == QuickStackRadialAction.PushVehicle)
        {
            resourceTarget = FindResourceTarget(
                world, player, selectedTargetId, RemoteResourceSourceKind.VehicleStorage,
                QuickStackService.Radius, false);
            if (resourceTarget == null) return;
        }
        else if (action == QuickStackRadialAction.PushDrone)
        {
            if (selectedTargetId.StartsWith("N:", StringComparison.Ordinal))
            {
                if (!RebirthNpcStableId.TryParse(selectedTargetId.Substring(2), out npcTargetId)) return;
                EntityRebirthNPC npcTarget = ResolveNpcTarget(world, player, npcTargetId);
                if (npcTarget == null) return;
                IRemoteResourceSource dogTarget;
                if (TryResolveDogResource(world, player, npcTargetId, false, out dogTarget))
                    resourceTarget = dogTarget;
                else
                    targetIsNpc = true;
            }
            else
            {
                resourceTarget = FindResourceTarget(
                    world, player, selectedTargetId, RemoteResourceSourceKind.DroneStorage,
                    CompanionRadius, false);
                if (resourceTarget == null) return;
            }
        }

        for (int i = 0; i < selected.Count; i++)
        {
            SelectedMobileSource request = selected[i];
            if (request.Kind == "B")
            {
                int moved = targetIsNpc
                    ? TransferBackpackItemToNpc(
                        world, player, npcTargetId, request.ItemType, request.MaxCount)
                    : TransferBackpackItemToResource(
                        player, resourceTarget, request.ItemType, request.MaxCount);
                AddTransferNotification(notifications, request.ItemType, -moved);
                continue;
            }

            if (request.Kind == "R")
            {
                IRemoteResourceSource source = FindSelectedMobileSource(
                    world, player, request.SourceId, true);
                if (source == null) continue;
                if (!targetIsNpc && string.Equals(source.StableId, resourceTarget.StableId, StringComparison.Ordinal))
                    continue;

                int moved = targetIsNpc
                    ? TransferResourceItemToNpc(
                        world, player, source, npcTargetId, request.ItemType, request.MaxCount)
                    : TransferResourceItemToResource(player, 
                        source, resourceTarget, request.ItemType, request.MaxCount);
                AddTransferNotification(notifications, request.ItemType, -moved);
                continue;
            }

            if (request.Kind == "W")
            {
                WorkstationOutputResourceSource source =
                    LogisticsPreviewService.FindWorkstationOutputSource(
                        world, player, request.SourceId, true);
                if (source == null) continue;

                int moved = targetIsNpc
                    ? TransferResourceItemToNpc(
                        world, player, source, npcTargetId, request.ItemType, request.MaxCount)
                    : TransferResourceItemToResource(player, 
                        source, resourceTarget, request.ItemType, request.MaxCount);
                AddTransferNotification(notifications, request.ItemType, -moved);
                continue;
            }

            if (request.Kind == "N")
            {
                RebirthNpcStableId sourceNpcId;
                if (!RebirthNpcStableId.TryParse(request.SourceId, out sourceNpcId) ||
                    ResolveNpcTarget(world, player, sourceNpcId) == null) continue;
                if (targetIsNpc && sourceNpcId == npcTargetId) continue;

                int moved = targetIsNpc
                    ? TransferNpcItemToNpc(
                        world, player, sourceNpcId, npcTargetId, request.ItemType, request.MaxCount)
                    : TransferNpcItemToResource(
                        world, player, sourceNpcId, resourceTarget, request.ItemType, request.MaxCount);
                AddTransferNotification(notifications, request.ItemType, -moved);
            }
        }
    }

    private static IRemoteResourceSource FindSelectedMobileSource(
        World world, EntityPlayer player, string sourceId, bool requireItems)
    {
        if (string.IsNullOrEmpty(sourceId)) return null;
        if (sourceId.StartsWith("V:", StringComparison.Ordinal))
            return FindResourceTarget(world, player, sourceId,
                RemoteResourceSourceKind.VehicleStorage, QuickStackService.Radius, requireItems);
        if (sourceId.StartsWith("D:", StringComparison.Ordinal))
            return FindResourceTarget(world, player, sourceId,
                RemoteResourceSourceKind.DroneStorage, CompanionRadius, requireItems);
        return null;
    }

    private static int TransferBackpackItemToResource(
        EntityPlayer player, IRemoteResourceSource target, int itemType, int maxCount)
    {
        if (player == null || target == null || maxCount <= 0) return 0;
        int remainingLimit = maxCount;
        int moved = 0;
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locks = player.bag.LockedSlots;
        for (int i = 0; i < bag.Length && remainingLimit > 0; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            ItemStack stack = bag[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue.type != itemType) continue;
            ItemStack transfer = stack.Clone();
            transfer.count = Math.Min(stack.count, remainingLimit);
            if (!CanMutateResource(player, target, false)) break;
            int before = transfer.count;
            MoveInto(target, transfer);
            int count = before - transfer.count;
            if (count <= 0) continue;
            ItemStack after = stack.Clone();
            after.count -= count;
            player.bag.SetSlot(i, after.count > 0 ? after : ItemStack.Empty);
            remainingLimit -= count;
            moved += count;
            bag = player.bag.ItemGrid.items;
        }
        if (moved > 0) target.MarkModified();
        return moved;
    }

    private static int TransferResourceItemToBag(
        EntityPlayer player, IRemoteResourceSource source, int itemType, int maxCount)
    {
        if (player == null || source == null || maxCount <= 0) return 0;
        int remainingLimit = maxCount;
        int moved = 0;
        ItemStack[] slots = source.Slots;
        PackedBoolArray locks = source.SlotLocks;
        for (int i = 0; slots != null && i < slots.Length && remainingLimit > 0; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue.type != itemType) continue;
            ItemStack transfer = stack.Clone();
            transfer.count = Math.Min(stack.count, remainingLimit);
            if (!CanMutateResource(player, source, true)) break;
            int before = transfer.count;
            MoveIntoBag(player, transfer);
            int count = before - transfer.count;
            if (count <= 0) continue;
            ItemStack after = stack.Clone();
            after.count -= count;
            source.SetSlot(i, after.count > 0 ? after : ItemStack.Empty);
            remainingLimit -= count;
            moved += count;
            slots = source.Slots;
        }
        if (moved > 0) source.MarkModified();
        return moved;
    }

    private static int TransferResourceItemToResource(
        EntityPlayer player, IRemoteResourceSource source, IRemoteResourceSource target, int itemType, int maxCount)
    {
        if (source == null || target == null || maxCount <= 0 ||
            string.Equals(source.StableId, target.StableId, StringComparison.Ordinal)) return 0;
        int remainingLimit = maxCount;
        int moved = 0;
        ItemStack[] slots = source.Slots;
        PackedBoolArray locks = source.SlotLocks;
        for (int i = 0; slots != null && i < slots.Length && remainingLimit > 0; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue.type != itemType) continue;
            ItemStack transfer = stack.Clone();
            transfer.count = Math.Min(stack.count, remainingLimit);
            if (!CanMutateResource(player, source, true) || !CanMutateResource(player, target, false)) break;
            int before = transfer.count;
            MoveInto(target, transfer);
            int count = before - transfer.count;
            if (count <= 0) continue;
            ItemStack after = stack.Clone();
            after.count -= count;
            source.SetSlot(i, after.count > 0 ? after : ItemStack.Empty);
            remainingLimit -= count;
            moved += count;
            slots = source.Slots;
        }
        if (moved > 0) { source.MarkModified(); target.MarkModified(); }
        return moved;
    }

    private static int TransferBackpackItemToNpc(
        World world, EntityPlayer player, RebirthNpcStableId targetNpcId, int itemType, int maxCount)
    {
        IRemoteResourceSource dogTarget;
        if (TryResolveDogResource(world, player, targetNpcId, false, out dogTarget))
            return TransferBackpackItemToResource(player, dogTarget, itemType, maxCount);
        LogisticsBackpackEndpoint endpoint = new LogisticsBackpackEndpoint(player);
        return TransferExternalItemToNpc(endpoint, endpoint, targetNpcId, itemType, maxCount);
    }

    private static int TransferResourceItemToNpc(
        World world, EntityPlayer player, IRemoteResourceSource source, RebirthNpcStableId targetNpcId, int itemType, int maxCount)
    {
        IRemoteResourceSource dogTarget;
        if (TryResolveDogResource(world, player, targetNpcId, false, out dogTarget))
            return TransferResourceItemToResource(player, source, dogTarget, itemType, maxCount);
        LogisticsRemoteResourceEndpoint endpoint = new LogisticsRemoteResourceEndpoint(source, player);
        return TransferExternalItemToNpc(endpoint, endpoint, targetNpcId, itemType, maxCount);
    }

    private static int TransferExternalItemToNpc(
        IRebirthNpcExternalInventoryEndpoint endpoint,
        IRebirthNpcExternalInventoryQuantityEndpoint quantities,
        RebirthNpcStableId targetNpcId,
        int itemType,
        int maxCount)
    {
        if (endpoint == null || quantities == null || targetNpcId.IsEmpty || maxCount <= 0) return 0;
        ItemClass item = ItemClass.GetForId(itemType);
        if (item == null || string.IsNullOrWhiteSpace(item.Name)) return 0;
        int available = quantities.GetAvailableDebitQuantity(item.Name);
        if (available <= 0) return 0;

        RebirthNpcExternalInventoryEndpointRegistry.Register(endpoint);
        try
        {
            RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(targetNpcId);
            uint revision = snapshot != null ? snapshot.Revision : 0U;
            int requested = Math.Min(available, maxCount);
            int quantity = RebirthNpcExternalInventoryEndpointRegistry.GetNegotiatedQuantity(
                endpoint.EndpointId, item.Name, requested, true);
            if (quantity <= 0) return 0;
            using (RebirthNpcInventoryAuthorityLease lease = RebirthNpcInventoryAuthorityService.Issue(
                "quickstack.selected-external-to-companion",
                RebirthNpcInventoryAuthorityOperations.Transfer,
                TimeSpan.FromSeconds(30), targetNpcId))
            {
                uint nextRevision;
                string error;
                RebirthNpcExternalTransferResult result = RebirthNpcExternalInventoryTransferCoordinator.Apply(
                    new RebirthNpcExternalTransferRequest(
                        Guid.NewGuid(), targetNpcId, revision, endpoint.EndpointId,
                        item.Name, quantity, RebirthNpcExternalTransferDirection.ExternalToNpc,
                        lease.AuthorityKey), out nextRevision, out error);
                return result == RebirthNpcExternalTransferResult.Applied ||
                       result == RebirthNpcExternalTransferResult.Replayed ? quantity : 0;
            }
        }
        finally
        {
            RebirthNpcExternalInventoryEndpointRegistry.Unregister(endpoint.EndpointId);
        }
    }

    private static int TransferNpcItemToBackpack(
        World world, EntityPlayer player, RebirthNpcStableId sourceNpcId, int itemType, int maxCount)
    {
        IRemoteResourceSource dogSource;
        if (TryResolveDogResource(world, player, sourceNpcId, true, out dogSource))
            return TransferResourceItemToBag(player, dogSource, itemType, maxCount);
        LogisticsBackpackEndpoint endpoint = new LogisticsBackpackEndpoint(player);
        return TransferNpcItemToExternal(endpoint, endpoint, sourceNpcId, itemType, maxCount);
    }

    private static int TransferNpcItemToResource(
        World world, EntityPlayer player, RebirthNpcStableId sourceNpcId, IRemoteResourceSource target,
        int itemType, int maxCount)
    {
        if (player == null || target == null) return 0;
        IRemoteResourceSource dogSource;
        if (TryResolveDogResource(world, player, sourceNpcId, true, out dogSource))
            return TransferResourceItemToResource(player, dogSource, target, itemType, maxCount);
        LogisticsRemoteResourceEndpoint endpoint = new LogisticsRemoteResourceEndpoint(target, player);
        return TransferNpcItemToExternal(endpoint, endpoint, sourceNpcId, itemType, maxCount);
    }

    private static int TransferNpcItemToExternal(
        IRebirthNpcExternalInventoryEndpoint endpoint,
        IRebirthNpcExternalInventoryQuantityEndpoint quantities,
        RebirthNpcStableId sourceNpcId,
        int itemType,
        int maxCount)
    {
        if (endpoint == null || quantities == null || sourceNpcId.IsEmpty || maxCount <= 0) return 0;
        ItemClass item = ItemClass.GetForId(itemType);
        if (item == null || string.IsNullOrWhiteSpace(item.Name) ||
            RebirthCompanionInventoryLockService.IsNpcItemLocked(sourceNpcId, item.Name)) return 0;
        RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(sourceNpcId);
        if (snapshot == null || snapshot.Quantities == null) return 0;
        int available;
        if (!snapshot.Quantities.TryGetValue(item.Name, out available) || available <= 0) return 0;
        int reserved = 0;
        if (snapshot.Reservations != null) snapshot.Reservations.TryGetValue(item.Name, out reserved);
        available = Math.Max(0, available - reserved);
        if (available <= 0) return 0;

        RebirthNpcExternalInventoryEndpointRegistry.Register(endpoint);
        try
        {
            int requested = Math.Min(available, maxCount);
            int quantity = RebirthNpcExternalInventoryEndpointRegistry.GetNegotiatedQuantity(
                endpoint.EndpointId, item.Name, requested, false);
            if (quantity <= 0) return 0;
            uint revision = snapshot.Revision;
            using (RebirthNpcInventoryAuthorityLease lease = RebirthNpcInventoryAuthorityService.Issue(
                "quickstack.selected-companion-to-external",
                RebirthNpcInventoryAuthorityOperations.Transfer,
                TimeSpan.FromSeconds(30), sourceNpcId))
            {
                uint nextRevision;
                string error;
                RebirthNpcExternalTransferResult result = RebirthNpcExternalInventoryTransferCoordinator.Apply(
                    new RebirthNpcExternalTransferRequest(
                        Guid.NewGuid(), sourceNpcId, revision, endpoint.EndpointId,
                        item.Name, quantity, RebirthNpcExternalTransferDirection.NpcToExternal,
                        lease.AuthorityKey), out nextRevision, out error);
                return result == RebirthNpcExternalTransferResult.Applied ||
                       result == RebirthNpcExternalTransferResult.Replayed ? quantity : 0;
            }
        }
        finally
        {
            RebirthNpcExternalInventoryEndpointRegistry.Unregister(endpoint.EndpointId);
        }
    }

    private static int TransferNpcItemToNpc(
        World world, EntityPlayer player, RebirthNpcStableId sourceNpcId, RebirthNpcStableId targetNpcId, int itemType, int maxCount)
    {
        if (sourceNpcId.IsEmpty || targetNpcId.IsEmpty || sourceNpcId == targetNpcId || maxCount <= 0) return 0;

        IRemoteResourceSource dogSource;
        IRemoteResourceSource dogTarget;
        bool sourceIsDog = TryResolveDogResource(world, player, sourceNpcId, true, out dogSource);
        bool targetIsDog = TryResolveDogResource(world, player, targetNpcId, false, out dogTarget);
        if (sourceIsDog && targetIsDog)
            return TransferResourceItemToResource(player, dogSource, dogTarget, itemType, maxCount);
        if (sourceIsDog)
            return TransferResourceItemToNpc(world, player, dogSource, targetNpcId, itemType, maxCount);
        if (targetIsDog)
            return TransferNpcItemToResource(world, player, sourceNpcId, dogTarget, itemType, maxCount);
        ItemClass item = ItemClass.GetForId(itemType);
        if (item == null || string.IsNullOrWhiteSpace(item.Name) ||
            RebirthCompanionInventoryLockService.IsNpcItemLocked(sourceNpcId, item.Name)) return 0;
        RebirthNpcInventorySnapshot source = RebirthNpcInventoryTransactionService.GetSnapshot(sourceNpcId);
        uint targetRevision = RebirthNpcInventoryTransactionService.GetRevision(targetNpcId);
        if (source == null || source.Quantities == null) return 0;
        int available;
        if (!source.Quantities.TryGetValue(item.Name, out available) || available <= 0) return 0;
        int reserved = 0;
        if (source.Reservations != null) source.Reservations.TryGetValue(item.Name, out reserved);
        available = Math.Max(0, available - reserved);
        if (available <= 0) return 0;
        int quantity = Math.Min(available, maxCount);

        using (RebirthNpcInventoryAuthorityLease lease = RebirthNpcInventoryAuthorityService.Issue(
            "quickstack.selected-companion-to-companion",
            RebirthNpcInventoryAuthorityOperations.Transfer,
            TimeSpan.FromSeconds(30), sourceNpcId, targetNpcId))
        {
            uint nextSource;
            uint nextTarget;
            RebirthNpcInventoryTransferResult result = RebirthNpcInventoryTransactionService.ApplyTransfer(
                new RebirthNpcInventoryTransfer(
                    Guid.NewGuid(), sourceNpcId, targetNpcId, source.Revision, targetRevision,
                    item.Name, quantity, lease.AuthorityKey), out nextSource, out nextTarget);
            return result == RebirthNpcInventoryTransferResult.Applied ||
                   result == RebirthNpcInventoryTransferResult.Replayed ? quantity : 0;
        }
    }

    private static void ProcessAllEligibleQuickStack(
        World world,
        EntityPlayer player,
        Dictionary<int, int> notifications)
    {
        if (world == null || player == null || !QuickStackRuntimePolicy.Enabled)
        {
            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                "ProcessAllEligibleQuickStack BLOCKED worldNull=" + (world == null) +
                " playerNull=" + (player == null) +
                " policyEnabled=" + QuickStackRuntimePolicy.Enabled); }
            return;
        }

        { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
            "ProcessAllEligibleQuickStack building selection entity=" + player.entityId +
            " mode=" + QuickStackRuntimePolicy.Mode +
            " radius=" + QuickStackRuntimePolicy.Radius); }

        string selectedSourceKeys = LogisticsPreviewService.BuildAllEligibleDepositSelectionKeys(world, player);
        if (string.IsNullOrEmpty(selectedSourceKeys))
        {
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("hotkey quickstack found no eligible source items"); }
            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("ProcessAllEligibleQuickStack result=no-eligible-source-items"); }
            return;
        }

        { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("hotkey quickstack executing all eligible Deposit Items sources"); }
        { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
            "ProcessAllEligibleQuickStack selection built chars=" + selectedSourceKeys.Length +
            "; executing selected transfer."); }
        ProcessSelectedQuickStack(
            world, player, false, selectedSourceKeys, notifications);
        { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("ProcessAllEligibleQuickStack selected transfer returned."); }
    }

    private static void ProcessSelectedQuickStack(
        World world,
        EntityPlayer player,
        bool ownedDestinations,
        string selectedSourceKeys,
        Dictionary<int, int> notifications)
    {
        if (world == null || player == null ||
            RebirthSandboxOptionManager.Current.QuickStack == RebirthQuickStackMode.Off) return;

        // Server execution uses the same cached stable classification as preview.

        List<KeyValuePair<string, int>> selected = new List<KeyValuePair<string, int>>();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        string[] values = (selectedSourceKeys ?? string.Empty).Split('\u001f');
        for (int i = 0; i < values.Length; i++)
        {
            string value = (values[i] ?? string.Empty).Trim();
            if (value.Length == 0) continue;

            string key = value;
            int maxCount = int.MaxValue; // backward-compatible with pre-v137 key-only requests
            int quantitySeparator = value.LastIndexOf('\u001d');
            if (quantitySeparator >= 0)
            {
                key = value.Substring(0, quantitySeparator);
                int parsed;
                if (!int.TryParse(value.Substring(quantitySeparator + 1), out parsed) || parsed <= 0)
                    continue;
                maxCount = parsed;
            }

            if (key.Length > 0 && seen.Add(key))
                selected.Add(new KeyValuePair<string, int>(key, maxCount));
        }
        if (selected.Count == 0) return;

        for (int selectedIndex = 0; selectedIndex < selected.Count; selectedIndex++)
        {
            string key = selected[selectedIndex].Key;
            int maxCount = selected[selectedIndex].Value;
            string kind;
            string sourceId;
            int itemType;
            if (!LogisticsPreviewService.TryParseSourceSelectionKey(
                key, out kind, out sourceId, out itemType)) continue;

            if (kind == "B")
            {
                int moved = MoveBackpackItemTypeToStatic(
                    world, player, itemType, ownedDestinations, maxCount);
                AddTransferNotification(notifications, itemType, -moved);
                continue;
            }

            if (kind == "R")
            {
                RemoteResourceSourceKind resourceKind;
                float radius;
                if (sourceId.StartsWith("V:", StringComparison.Ordinal))
                {
                    resourceKind = RemoteResourceSourceKind.VehicleStorage;
                    radius = QuickStackService.Radius;
                }
                else if (sourceId.StartsWith("D:", StringComparison.Ordinal))
                {
                    resourceKind = RemoteResourceSourceKind.DroneStorage;
                    radius = CompanionRadius;
                }
                else continue;

                IRemoteResourceSource source = FindResourceTarget(
                    world, player, sourceId, resourceKind, radius, true);
                if (source != null)
                {
                    int moved = MoveResourceItemTypeToStatic(
                        world, player, source, itemType, ownedDestinations, maxCount);
                    AddTransferNotification(notifications, itemType, -moved);
                }
                continue;
            }

            if (kind == "W")
            {
                WorkstationOutputResourceSource source =
                    LogisticsPreviewService.FindWorkstationOutputSource(
                        world, player, sourceId, true);
                if (source != null)
                {
                    int moved = MoveResourceItemTypeToStatic(
                        world, player, source, itemType, ownedDestinations, maxCount);
                    AddTransferNotification(notifications, itemType, -moved);
                }
                continue;
            }

            if (kind == "N")
            {
                RebirthNpcStableId npcId;
                if (!RebirthNpcStableId.TryParse(sourceId, out npcId)) continue;
                EntityRebirthNPC npc = ResolveNpcTarget(world, player, npcId);
                if (npc != null)
                {
                    int moved = MoveNpcItemTypeToStatic(
                        world, player, npcId, itemType, ownedDestinations, maxCount);
                    AddTransferNotification(notifications, itemType, -moved);
                }
            }
        }
    }

    private static int MoveBackpackItemTypeToStatic(
        World world, EntityPlayer player, int itemType, bool owned, int maxCount)
    {
        if (maxCount <= 0) return 0;
        int moved = 0;
        int remainingLimit = maxCount;
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locks = player.bag.LockedSlots;
        for (int i = 0; i < bag.Length && remainingLimit > 0; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            ItemStack source = bag[i];
            if (source == null || source.IsEmpty() || source.itemValue.type != itemType) continue;

            ItemStack transfer = source.Clone();
            transfer.count = Math.Min(source.count, remainingLimit);
            QuickStackTransferPlanner.Plan plan = QuickStackTransferPlanner.Find(
                world, player, transfer, owned, RebirthSandboxOptionManager.Current.QuickStack);
            if (plan == null || plan.Destinations.Count == 0) continue;

            ItemStack remaining;
            int count = QuickStackTransferExecutor.CommitExternal(
                world, player, transfer, plan, owned, out remaining);
            if (count <= 0) continue;

            ItemStack after = source.Clone();
            after.count -= count;
            player.bag.SetSlot(i, after.count > 0 ? after : ItemStack.Empty);
            moved += count;
            remainingLimit -= count;
        }
        return moved;
    }

    private static int MoveResourceItemTypeToStatic(
        World world,
        EntityPlayer player,
        IRemoteResourceSource source,
        int itemType,
        bool owned,
        int maxCount)
    {
        if (source == null || maxCount <= 0) return 0;
        string reason;
        if (!LogisticsPreviewService.CanUseMobile(source, player, true, out reason)) return 0;
        int moved = 0;
        int remainingLimit = maxCount;
        ItemStack[] slots = source.Slots;
        PackedBoolArray locks = source.SlotLocks;
        for (int i = 0; slots != null && i < slots.Length && remainingLimit > 0; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue.type != itemType) continue;

            ItemStack transfer = stack.Clone();
            transfer.count = Math.Min(stack.count, remainingLimit);
            QuickStackTransferPlanner.Plan plan = QuickStackTransferPlanner.Find(
                world, player, transfer, owned, RebirthSandboxOptionManager.Current.QuickStack);
            if (plan == null || plan.Destinations.Count == 0) continue;

            ItemStack remaining;
            int count = QuickStackTransferExecutor.CommitExternal(
                world, player, transfer, plan, owned, out remaining);
            if (count <= 0) continue;

            ItemStack after = stack.Clone();
            after.count -= count;
            source.SetSlot(i, after.count > 0 ? after : ItemStack.Empty);
            moved += count;
            remainingLimit -= count;
            slots = source.Slots;
        }
        if (moved > 0) source.MarkModified();
        return moved;
    }

    private static int MoveNpcItemTypeToStatic(
        World world,
        EntityPlayer player,
        RebirthNpcStableId npcId,
        int itemType,
        bool owned,
        int maxCount)
    {
        if (maxCount <= 0) return 0;
        ItemClass item = ItemClass.GetForId(itemType);
        if (item == null || string.IsNullOrWhiteSpace(item.Name)) return 0;

        IRemoteResourceSource dogSource;
        if (TryResolveDogResource(world, player, npcId, true, out dogSource))
            return MoveResourceItemTypeToStatic(world, player, dogSource, itemType, owned, maxCount);

        if (RebirthCompanionInventoryLockService.IsNpcItemLocked(npcId, item.Name)) return 0;
        RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(npcId);
        if (snapshot == null || snapshot.Quantities == null) return 0;
        int available;
        if (!snapshot.Quantities.TryGetValue(item.Name, out available) || available <= 0) return 0;
        int reserved = 0;
        if (snapshot.Reservations != null) snapshot.Reservations.TryGetValue(item.Name, out reserved);
        available = Math.Max(0, available - reserved);
        if (available <= 0) return 0;

        QuickStackStaticDestinationEndpoint endpoint =
            new QuickStackStaticDestinationEndpoint(world, player, owned);
        RebirthNpcExternalInventoryEndpointRegistry.Register(endpoint);
        try
        {
            using (RebirthNpcInventoryAuthorityLease lease = RebirthNpcInventoryAuthorityService.Issue(
                "quickstack.dump", RebirthNpcInventoryAuthorityOperations.Transfer,
                TimeSpan.FromSeconds(30), npcId))
            {
                int requested = Math.Min(available, maxCount);
                int quantity = RebirthNpcExternalInventoryEndpointRegistry.GetNegotiatedQuantity(
                    endpoint.EndpointId, item.Name, requested, false);
                if (quantity <= 0) return 0;
                uint revision = snapshot.Revision;
                uint nextRevision;
                string error;
                RebirthNpcExternalTransferResult result = RebirthNpcExternalInventoryTransferCoordinator.Apply(
                    new RebirthNpcExternalTransferRequest(Guid.NewGuid(), npcId, revision,
                        endpoint.EndpointId, item.Name, quantity,
                        RebirthNpcExternalTransferDirection.NpcToExternal, lease.AuthorityKey),
                    out nextRevision, out error);
                return result == RebirthNpcExternalTransferResult.Applied ||
                       result == RebirthNpcExternalTransferResult.Replayed
                    ? quantity : 0;
            }
        }
        finally
        {
            RebirthNpcExternalInventoryEndpointRegistry.Unregister(endpoint.EndpointId);
        }
    }

    private static void PullAllQualifiedResources(
        World world,
        EntityPlayer player,
        RemoteResourceSourceKind kind,
        float radius)
    {
        if (world == null || player == null) return;
        List<IRemoteResourceSource> all = RemoteResourceRegistry.ResolveNearby(
            world, player, radius, false, false, true);
        for (int i = 0; i < all.Count; i++)
        {
            IRemoteResourceSource source = all[i];
            if (source == null || source.Kind != kind) continue;
            string reason;
            if (!LogisticsPreviewService.CanUseMobile(source, player, true, out reason)) continue;
            int moved = Pull(player, source);
            if (moved > 0) source.MarkModified();
        }
    }

    private static void PullAllQualifiedCompanions(World world, EntityPlayer player)
    {
        if (world == null || player == null) return;

        List<IRemoteResourceSource> all = RemoteResourceRegistry.ResolveNearby(
            world, player, CompanionRadius, false, false, true);
        for (int i = 0; i < all.Count; i++)
        {
            IRemoteResourceSource source = all[i];
            if (source == null || source.Kind != RemoteResourceSourceKind.DroneStorage) continue;
            string reason;
            if (!LogisticsPreviewService.CanUseMobile(source, player, true, out reason)) continue;
            int moved = Pull(player, source);
            if (moved > 0) source.MarkModified();
        }

        List<RebirthNpcStableId> npcs = GetQualifiedNpcCompanions(world, player, true);
        for (int i = 0; i < npcs.Count; i++)
            PullFromNpc(player, npcs[i]);
    }

    private static void PushQualifiedSourcesToResource(
        World world,
        EntityPlayer player,
        IRemoteResourceSource target)
    {
        if (world == null || player == null || target == null) return;

        int bagMoved = Push(player, target);
        if (bagMoved > 0) target.MarkModified();

        List<WorkstationOutputResourceSource> workstationSources =
            LogisticsPreviewService.GatherWorkstationOutputSources(world, player, true);
        for (int i = 0; i < workstationSources.Count; i++)
            TransferResourceToResource(player, workstationSources[i], target);

        List<IRemoteResourceSource> all = RemoteResourceRegistry.ResolveNearby(
            world, player, QuickStackService.Radius, false, false, true);
        List<IRemoteResourceSource> sources = new List<IRemoteResourceSource>();
        for (int i = 0; i < all.Count; i++)
        {
            IRemoteResourceSource source = all[i];
            if (source == null ||
                string.Equals(source.StableId, target.StableId, StringComparison.Ordinal) ||
                (source.Kind != RemoteResourceSourceKind.VehicleStorage &&
                 source.Kind != RemoteResourceSourceKind.DroneStorage)) continue;
            if (source.Kind == RemoteResourceSourceKind.DroneStorage &&
                (source.Position - player.position).sqrMagnitude > CompanionRadius * CompanionRadius) continue;
            string reason;
            if (!LogisticsPreviewService.CanUseMobile(source, player, true, out reason)) continue;
            sources.Add(source);
        }
        sources.Sort(delegate(IRemoteResourceSource a, IRemoteResourceSource b)
        {
            return string.Compare(a.StableId, b.StableId, StringComparison.Ordinal);
        });

        for (int i = 0; i < sources.Count; i++)
            TransferResourceToResource(player, sources[i], target);

        List<RebirthNpcStableId> npcs = GetQualifiedNpcCompanions(world, player, true);
        for (int i = 0; i < npcs.Count; i++)
            TransferNpcToResource(player, npcs[i], target);
    }

    private static void PushQualifiedSourcesToNpc(
        World world,
        EntityPlayer player,
        RebirthNpcStableId targetNpcId)
    {
        if (world == null || player == null || targetNpcId.IsEmpty) return;

        // Backpack first, then workstation outputs, remote/mobile inventories,
        // then other companions.
        PushToNpc(player, targetNpcId);

        List<WorkstationOutputResourceSource> workstationSources =
            LogisticsPreviewService.GatherWorkstationOutputSources(world, player, true);
        for (int i = 0; i < workstationSources.Count; i++)
            TransferResourceToNpc(player, workstationSources[i], targetNpcId);

        List<IRemoteResourceSource> all = RemoteResourceRegistry.ResolveNearby(
            world, player, QuickStackService.Radius, false, false, true);
        List<IRemoteResourceSource> sources = new List<IRemoteResourceSource>();
        for (int i = 0; i < all.Count; i++)
        {
            IRemoteResourceSource source = all[i];
            if (source == null ||
                (source.Kind != RemoteResourceSourceKind.VehicleStorage &&
                 source.Kind != RemoteResourceSourceKind.DroneStorage)) continue;
            if (source.Kind == RemoteResourceSourceKind.DroneStorage &&
                (source.Position - player.position).sqrMagnitude > CompanionRadius * CompanionRadius) continue;
            string reason;
            if (!LogisticsPreviewService.CanUseMobile(source, player, true, out reason)) continue;
            sources.Add(source);
        }
        sources.Sort(delegate(IRemoteResourceSource a, IRemoteResourceSource b)
        {
            return string.Compare(a.StableId, b.StableId, StringComparison.Ordinal);
        });
        for (int i = 0; i < sources.Count; i++)
            TransferResourceToNpc(player, sources[i], targetNpcId);

        List<RebirthNpcStableId> npcs = GetQualifiedNpcCompanions(world, player, true);
        for (int i = 0; i < npcs.Count; i++)
        {
            if (npcs[i] == targetNpcId) continue;
            TransferNpcToNpc(npcs[i], targetNpcId);
        }
    }

    private static List<RebirthNpcStableId> GetQualifiedNpcCompanions(
        World world,
        EntityPlayer player,
        bool requireItems)
    {
        List<RebirthNpcStableId> result = new List<RebirthNpcStableId>();
        if (world == null || player == null) return result;

        Bounds bounds = new Bounds(player.position, Vector3.one * CompanionRadius * 2f);
        List<Entity> entities = world.GetEntitiesInBounds(typeof(EntityAlive), bounds, new List<Entity>());
        for (int i = 0; i < entities.Count; i++)
        {
            EntityRebirthNPC npc = entities[i] as EntityRebirthNPC;
            if (npc == null || npc.IsDead() ||
                (npc.position - player.position).sqrMagnitude > CompanionRadius * CompanionRadius) continue;
            RebirthNpcRuntimeState state = npc.RebirthRuntimeState;
            if (state == null || state.StableId.IsEmpty) continue;
            RebirthNpcProfile profile;
            if (!RebirthNpcProfileRegistry.TryResolve(state.ProfileId, out profile) ||
                !profile.Has(RebirthNpcCapabilities.Inventory) ||
                (profile.Category != RebirthNpcCategory.Survivor &&
                 profile.Category != RebirthNpcCategory.DogCompanion &&
                 profile.Category != RebirthNpcCategory.PantherCompanion) ||
                !LogisticsPreviewService.CanAccessNpcCompanion(player, state)) continue;

            if (requireItems)
            {
                if (profile.Category == RebirthNpcCategory.DogCompanion)
                {
                    if (!RebirthDogInventoryService.HasAnyItems(state.StableId)) continue;
                }
                else
                {
                    RebirthNpcInventorySnapshot snapshot =
                        RebirthNpcInventoryTransactionService.GetSnapshot(state.StableId);
                    bool hasAvailable = false;
                    if (snapshot != null && snapshot.Quantities != null)
                    {
                        foreach (KeyValuePair<string, int> pair in snapshot.Quantities)
                        {
                            if (pair.Value <= 0 || RebirthCompanionInventoryLockService.IsNpcItemLocked(state.StableId, pair.Key)) continue;
                            int reserved = 0;
                            if (snapshot.Reservations != null) snapshot.Reservations.TryGetValue(pair.Key, out reserved);
                            if (pair.Value - reserved > 0) { hasAvailable = true; break; }
                        }
                    }
                    if (!hasAvailable) continue;
                }
            }
            result.Add(state.StableId);
        }
        result.Sort(delegate(RebirthNpcStableId a, RebirthNpcStableId b)
        { return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal); });
        return result;
    }

    private static int TransferResourceToResource(
        EntityPlayer player, IRemoteResourceSource source,
        IRemoteResourceSource target)
    {
        if (source == null || target == null ||
            string.Equals(source.StableId, target.StableId, StringComparison.Ordinal)) return 0;

        int moved = 0;
        ItemStack[] slots = source.Slots;
        PackedBoolArray locks = source.SlotLocks;
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty()) continue;
            ItemStack remaining = stack.Clone();
            if (!CanMutateResource(player, source, true) || !CanMutateResource(player, target, false)) break;
            int before = remaining.count;
            MoveInto(target, remaining);
            int count = before - remaining.count;
            if (count <= 0) continue;
            source.SetSlot(i, remaining.count > 0 ? remaining : ItemStack.Empty);
            moved += count;
            slots = source.Slots;
        }

        if (moved > 0)
        {
            source.MarkModified();
            target.MarkModified();
        }
        return moved;
    }

    private static void TransferResourceToNpc(
        EntityPlayer player,
        IRemoteResourceSource source,
        RebirthNpcStableId targetNpcId)
    {
        if (player == null || source == null || targetNpcId.IsEmpty) return;
        LogisticsRemoteResourceEndpoint endpoint = new LogisticsRemoteResourceEndpoint(source, player);
        RebirthNpcExternalInventoryEndpointRegistry.Register(endpoint);
        try
        {
            RebirthNpcInventorySnapshot snapshot =
                RebirthNpcInventoryTransactionService.GetSnapshot(targetNpcId);
            uint revision = snapshot != null ? snapshot.Revision : 0U;
            using (RebirthNpcInventoryAuthorityLease lease = RebirthNpcInventoryAuthorityService.Issue(
                "quickstack.push-qualified-resource-to-companion",
                RebirthNpcInventoryAuthorityOperations.Transfer,
                TimeSpan.FromSeconds(30), targetNpcId))
            {
                Dictionary<string, int> amounts = endpoint.GetUnlockedDebitQuantities();
                List<KeyValuePair<string, int>> pairs =
                    new List<KeyValuePair<string, int>>(amounts);
                pairs.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
                { return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase); });

                for (int i = 0; i < pairs.Count; i++)
                {
                    int quantity = RebirthNpcExternalInventoryEndpointRegistry.GetNegotiatedQuantity(
                        endpoint.EndpointId, pairs[i].Key, pairs[i].Value, true);
                    if (quantity <= 0) continue;
                    uint nextRevision;
                    string error;
                    RebirthNpcExternalTransferResult result =
                        RebirthNpcExternalInventoryTransferCoordinator.Apply(
                            new RebirthNpcExternalTransferRequest(
                                Guid.NewGuid(), targetNpcId, revision,
                                endpoint.EndpointId, pairs[i].Key, quantity,
                                RebirthNpcExternalTransferDirection.ExternalToNpc,
                                lease.AuthorityKey),
                            out nextRevision, out error);
                    if (result == RebirthNpcExternalTransferResult.Applied ||
                        result == RebirthNpcExternalTransferResult.Replayed)
                        revision = nextRevision;
                }
            }
        }
        finally
        {
            RebirthNpcExternalInventoryEndpointRegistry.Unregister(endpoint.EndpointId);
        }
    }

    private static void TransferNpcToResource(
        EntityPlayer player,
        RebirthNpcStableId sourceNpcId,
        IRemoteResourceSource target)
    {
        if (player == null || sourceNpcId.IsEmpty || target == null) return;
        LogisticsRemoteResourceEndpoint endpoint = new LogisticsRemoteResourceEndpoint(target, player);
        RebirthNpcExternalInventoryEndpointRegistry.Register(endpoint);
        try
        {
            RebirthNpcInventorySnapshot snapshot =
                RebirthNpcInventoryTransactionService.GetSnapshot(sourceNpcId);
            if (snapshot == null || snapshot.Quantities == null) return;
            uint revision = snapshot.Revision;
            using (RebirthNpcInventoryAuthorityLease lease = RebirthNpcInventoryAuthorityService.Issue(
                "quickstack.push-qualified-companion-to-resource",
                RebirthNpcInventoryAuthorityOperations.Transfer,
                TimeSpan.FromSeconds(30), sourceNpcId))
            {
                List<KeyValuePair<string, int>> pairs =
                    new List<KeyValuePair<string, int>>(snapshot.Quantities);
                pairs.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
                { return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase); });

                for (int i = 0; i < pairs.Count; i++)
                {
                    int quantity = RebirthNpcExternalInventoryEndpointRegistry.GetNegotiatedQuantity(
                        endpoint.EndpointId, pairs[i].Key, pairs[i].Value, false);
                    if (quantity <= 0) continue;
                    uint nextRevision;
                    string error;
                    RebirthNpcExternalTransferResult result =
                        RebirthNpcExternalInventoryTransferCoordinator.Apply(
                            new RebirthNpcExternalTransferRequest(
                                Guid.NewGuid(), sourceNpcId, revision,
                                endpoint.EndpointId, pairs[i].Key, quantity,
                                RebirthNpcExternalTransferDirection.NpcToExternal,
                                lease.AuthorityKey),
                            out nextRevision, out error);
                    if (result == RebirthNpcExternalTransferResult.Applied ||
                        result == RebirthNpcExternalTransferResult.Replayed)
                        revision = nextRevision;
                }
            }
        }
        finally
        {
            RebirthNpcExternalInventoryEndpointRegistry.Unregister(endpoint.EndpointId);
        }
    }

    private static void TransferNpcToNpc(
        RebirthNpcStableId sourceNpcId,
        RebirthNpcStableId targetNpcId)
    {
        if (sourceNpcId.IsEmpty || targetNpcId.IsEmpty || sourceNpcId == targetNpcId) return;
        RebirthNpcInventorySnapshot source =
            RebirthNpcInventoryTransactionService.GetSnapshot(sourceNpcId);
        if (source == null || source.Quantities == null) return;

        uint sourceRevision = source.Revision;
        uint targetRevision = RebirthNpcInventoryTransactionService.GetRevision(targetNpcId);
        using (RebirthNpcInventoryAuthorityLease lease = RebirthNpcInventoryAuthorityService.Issue(
            "quickstack.push-qualified-companion-to-companion",
            RebirthNpcInventoryAuthorityOperations.Transfer,
            TimeSpan.FromSeconds(30), sourceNpcId, targetNpcId))
        {
            List<KeyValuePair<string, int>> pairs =
                new List<KeyValuePair<string, int>>(source.Quantities);
            pairs.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
            { return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase); });
            for (int i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Value <= 0) continue;
                uint nextSource;
                uint nextTarget;
                RebirthNpcInventoryTransferResult result =
                    RebirthNpcInventoryTransactionService.ApplyTransfer(
                        new RebirthNpcInventoryTransfer(
                            Guid.NewGuid(), sourceNpcId, targetNpcId,
                            sourceRevision, targetRevision, pairs[i].Key,
                            pairs[i].Value, lease.AuthorityKey),
                        out nextSource, out nextTarget);
                if (result == RebirthNpcInventoryTransferResult.Applied ||
                    result == RebirthNpcInventoryTransferResult.Replayed)
                {
                    sourceRevision = nextSource;
                    targetRevision = nextTarget;
                }
            }
        }
    }

    private static IRemoteResourceSource FindResourceTarget(
        World world,
        EntityPlayer player,
        string targetId,
        RemoteResourceSourceKind kind,
        float radius,
        bool requireItems)
    {
        List<IRemoteResourceSource> all = RemoteResourceRegistry.ResolveNearby(
            world, player, radius, false, false, true);
        IRemoteResourceSource fallback = null;
        for (int i = 0; i < all.Count; i++)
        {
            IRemoteResourceSource source = all[i];
            if (source.Kind != kind) continue;
            string reason;
            if (!LogisticsPreviewService.CanUseMobile(source, player, requireItems, out reason)) continue;
            if (fallback == null) fallback = source;
            if (!string.IsNullOrEmpty(targetId) && string.Equals(source.StableId, targetId, StringComparison.Ordinal))
                return source;
        }
        return string.IsNullOrEmpty(targetId) ? fallback : null;
    }

    private static EntityRebirthNPC ResolveNpcTarget(World world, EntityPlayer player, RebirthNpcStableId npcId)
    {
        int entityId;
        if (!RebirthNpcRuntimeRegistry.TryGetEntityId(npcId, out entityId)) return null;
        EntityRebirthNPC npc = world.GetEntity(entityId) as EntityRebirthNPC;
        if (npc == null || npc.IsDead() ||
            (npc.position - player.position).sqrMagnitude > CompanionRadius * CompanionRadius) return null;
        RebirthNpcRuntimeState state = npc.RebirthRuntimeState;
        RebirthNpcProfile profile;
        if (state == null || !RebirthNpcProfileRegistry.TryResolve(state.ProfileId, out profile) ||
            !profile.Has(RebirthNpcCapabilities.Inventory) ||
            (profile.Category != RebirthNpcCategory.Survivor &&
             profile.Category != RebirthNpcCategory.DogCompanion &&
             profile.Category != RebirthNpcCategory.PantherCompanion) ||
            !LogisticsPreviewService.CanAccessNpcCompanion(player, state)) return null;
        RemoteNpcResourceSource resource = new RemoteNpcResourceSource(npc);
        string reason;
        if (!LogisticsPreviewService.CanUseMobile(resource, player, false, out reason)) return null;
        return npc;
    }

    private static bool TryResolveDogResource(
        World world, EntityPlayer player, RebirthNpcStableId npcId, bool requireItems,
        out IRemoteResourceSource resource)
    {
        resource = null;
        EntityRebirthNPC npc = ResolveNpcTarget(world, player, npcId);
        if (npc == null) return false;
        RebirthNpcRuntimeState state = npc.RebirthRuntimeState;
        RebirthNpcProfile profile;
        if (state == null || !RebirthNpcProfileRegistry.TryResolve(state.ProfileId, out profile) ||
            profile.Category != RebirthNpcCategory.DogCompanion) return false;
        RemoteNpcResourceSource dog = new RemoteNpcResourceSource(npc);
        string reason;
        if (!LogisticsPreviewService.CanUseMobile(dog, player, requireItems, out reason)) return false;
        resource = dog;
        return true;
    }

    private static int Push(EntityPlayer player, IRemoteResourceSource target)
    {
        int moved = 0;
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locks = player.bag.LockedSlots;
        for (int i = 0; i < bag.Length; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue; // protected source
            ItemStack source = bag[i];
            if (source == null || source.IsEmpty()) continue;
            ItemStack remaining = source.Clone();
            int before = remaining.count;
            MoveInto(target, remaining);
            int count = before - remaining.count;
            if (count <= 0) continue;
            player.bag.SetSlot(i, remaining.count > 0 ? remaining : ItemStack.Empty);
            moved += count;
        }
        return moved;
    }

    private static int Pull(EntityPlayer player, IRemoteResourceSource target)
    {
        int moved = 0;
        ItemStack[] slots = target.Slots;
        PackedBoolArray locks = target.SlotLocks;
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue; // protected source
            ItemStack source = slots[i];
            if (source == null || source.IsEmpty()) continue;
            ItemStack remaining = source.Clone();
            int before = remaining.count;
            MoveIntoBag(player, remaining);
            int count = before - remaining.count;
            if (count <= 0) continue;
            target.SetSlot(i, remaining.count > 0 ? remaining : ItemStack.Empty);
            moved += count;
        }
        return moved;
    }

    internal static void MoveInto(IRemoteResourceSource target, ItemStack remaining)
    {
        ItemStack[] slots = target.Slots;
        PackedBoolArray locks = target.SlotLocks;
        if (slots == null) return;

        // Stack onto existing stacks first, including protected destination stacks.
        for (int i = 0; i < slots.Length && remaining.count > 0; i++)
        {
            ItemStack current = slots[i];
            if (current == null || current.IsEmpty()) continue;
            int amount;
            if (!current.CanStackPartlyWith(remaining, out amount) || amount <= 0) continue;
            ItemStack after = current.Clone();
            after.count += amount;
            remaining.count -= amount;
            target.SetSlot(i, after);
            slots = target.Slots;
        }

        int maxStack = ItemClass.GetForId(remaining.itemValue.type).Stacknumber.Value;
        for (int i = 0; i < slots.Length && remaining.count > 0; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            if (slots[i] != null && !slots[i].IsEmpty()) continue;
            int amount = Math.Min(maxStack, remaining.count);
            target.SetSlot(i, new ItemStack(remaining.itemValue.Clone(), amount));
            remaining.count -= amount;
            slots = target.Slots;
        }
    }

    internal static void MoveIntoBag(EntityPlayer player, ItemStack remaining)
    {
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locks = player.bag.LockedSlots;

        // Locked backpack slots can be topped up if they already contain the item.
        for (int i = 0; i < bag.Length && remaining.count > 0; i++)
        {
            ItemStack current = bag[i];
            if (current == null || current.IsEmpty()) continue;
            int amount;
            if (!current.CanStackPartlyWith(remaining, out amount) || amount <= 0) continue;
            ItemStack after = current.Clone();
            after.count += amount;
            remaining.count -= amount;
            player.bag.SetSlot(i, after);
        }

        int maxStack = ItemClass.GetForId(remaining.itemValue.type).Stacknumber.Value;
        for (int i = 0; i < bag.Length && remaining.count > 0; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            if (bag[i] != null && !bag[i].IsEmpty()) continue;
            int amount = Math.Min(maxStack, remaining.count);
            player.bag.SetSlot(i, new ItemStack(remaining.itemValue.Clone(), amount));
            remaining.count -= amount;
        }
    }

    private static void PushToNpc(EntityPlayer player, RebirthNpcStableId npcId)
    {
        LogisticsBackpackEndpoint endpoint = new LogisticsBackpackEndpoint(player);
        RebirthNpcExternalInventoryEndpointRegistry.Register(endpoint);
        try
        {
            RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(npcId);
            uint revision = snapshot != null ? snapshot.Revision : 0U;
            using (RebirthNpcInventoryAuthorityLease lease = RebirthNpcInventoryAuthorityService.Issue(
                "quickstack.companion", RebirthNpcInventoryAuthorityOperations.Transfer,
                TimeSpan.FromSeconds(30), npcId))
            {
                Dictionary<string, int> amounts = endpoint.GetUnlockedDebitQuantities();
                List<KeyValuePair<string, int>> pairs = new List<KeyValuePair<string, int>>(amounts);
                for (int i = 0; i < pairs.Count; i++)
                {
                    int quantity = RebirthNpcExternalInventoryEndpointRegistry.GetNegotiatedQuantity(
                        endpoint.EndpointId, pairs[i].Key, pairs[i].Value, true);
                    if (quantity <= 0) continue;
                    uint nextRevision;
                    string error;
                    RebirthNpcExternalTransferResult result = RebirthNpcExternalInventoryTransferCoordinator.Apply(
                        new RebirthNpcExternalTransferRequest(Guid.NewGuid(), npcId, revision,
                            endpoint.EndpointId, pairs[i].Key, quantity,
                            RebirthNpcExternalTransferDirection.ExternalToNpc, lease.AuthorityKey),
                        out nextRevision, out error);
                    if (result == RebirthNpcExternalTransferResult.Applied ||
                        result == RebirthNpcExternalTransferResult.Replayed)
                        revision = nextRevision;
                }
            }
        }
        finally
        {
            RebirthNpcExternalInventoryEndpointRegistry.Unregister(endpoint.EndpointId);
        }
    }

    private static void PullFromNpc(EntityPlayer player, RebirthNpcStableId npcId)
    {
        LogisticsBackpackEndpoint endpoint = new LogisticsBackpackEndpoint(player);
        RebirthNpcExternalInventoryEndpointRegistry.Register(endpoint);
        try
        {
            RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(npcId);
            if (snapshot == null || snapshot.Quantities == null) return;
            uint revision = snapshot.Revision;
            using (RebirthNpcInventoryAuthorityLease lease = RebirthNpcInventoryAuthorityService.Issue(
                "quickstack.companion", RebirthNpcInventoryAuthorityOperations.Transfer,
                TimeSpan.FromSeconds(30), npcId))
            {
                List<KeyValuePair<string, int>> pairs = new List<KeyValuePair<string, int>>(snapshot.Quantities);
                for (int i = 0; i < pairs.Count; i++)
                {
                    if (RebirthCompanionInventoryLockService.IsNpcItemLocked(npcId, pairs[i].Key)) continue;
                    int quantity = RebirthNpcExternalInventoryEndpointRegistry.GetNegotiatedQuantity(
                        endpoint.EndpointId, pairs[i].Key, pairs[i].Value, false);
                    if (quantity <= 0) continue;
                    uint nextRevision;
                    string error;
                    RebirthNpcExternalTransferResult result = RebirthNpcExternalInventoryTransferCoordinator.Apply(
                        new RebirthNpcExternalTransferRequest(Guid.NewGuid(), npcId, revision,
                            endpoint.EndpointId, pairs[i].Key, quantity,
                            RebirthNpcExternalTransferDirection.NpcToExternal, lease.AuthorityKey),
                        out nextRevision, out error);
                    if (result == RebirthNpcExternalTransferResult.Applied ||
                        result == RebirthNpcExternalTransferResult.Replayed)
                        revision = nextRevision;
                }
            }
        }
        finally
        {
            RebirthNpcExternalInventoryEndpointRegistry.Unregister(endpoint.EndpointId);
        }
    }
}

/// <summary>
/// Transactional external-inventory endpoint used when a survivor/animal companion
/// is a Quick Stack dump source. The NPC coordinator debits the logical companion
/// inventory; this endpoint atomically credits only currently authorized qualified
/// static containers. A reservation snapshots destination revisions before the NPC
/// debit occurs so a stale destination cannot cause a duplicate/partial commit.
/// </summary>
internal sealed class QuickStackStaticDestinationEndpoint :
    IRebirthNpcExternalInventoryEndpoint,
    IRebirthNpcExternalInventoryQuantityEndpoint
{
    private sealed class DestinationMutation
    {
        public Vector3i Position;
        public int Revision;
        public ItemStack[] After;
    }

    private sealed class Reservation : IRebirthNpcExternalInventoryReservation
    {
        private readonly QuickStackStaticDestinationEndpoint owner;
        private readonly List<DestinationMutation> mutations;
        private bool terminal;
        public string EndpointId { get { return owner.EndpointId; } }
        public Guid TransactionId { get; private set; }

        public Reservation(
            QuickStackStaticDestinationEndpoint value,
            Guid transactionId,
            List<DestinationMutation> values)
        {
            owner = value;
            TransactionId = transactionId;
            mutations = values;
        }

        public bool Commit(out string error)
        {
            error = string.Empty;
            if (terminal) return true;

            if (!QuickStackRuntimePolicy.Enabled) { error = "Quick Stack disabled before commit."; return false; }
            // Revalidate every destination before mutating any of them.
            List<TEFeatureStorage> liveLoot = new List<TEFeatureStorage>(mutations.Count);
            for (int i = 0; i < mutations.Count; i++)
            {
                DestinationMutation mutation = mutations[i];
                TileEntity te = owner.world.GetTileEntity(mutation.Position);
                TEFeatureStorage loot;
                string reason;
                if (!QuickStackService.CanUse(
                    owner.world, owner.player, te, owner.owned, out loot, out reason))
                {
                    error = "Quick Stack destination became unavailable: " + reason;
                    return false;
                }
                if (RemoteResourceAccess.ComputeRevision(loot.ItemGrid.items) != mutation.Revision)
                {
                    error = "Quick Stack destination changed before companion transfer commit.";
                    return false;
                }
                liveLoot.Add(loot);
            }

            for (int i = 0; i < mutations.Count; i++)
            {
                DestinationMutation mutation = mutations[i];
                TEFeatureStorage loot = liveLoot[i];
                ItemStack[] after = mutation.After;
                for (int slot = 0; after != null && slot < after.Length; slot++)
                    loot.UpdateSlot(slot, after[slot] != null ? after[slot].Clone() : ItemStack.Empty.Clone());
                loot.SetModified();
                RemoteResourceStateStore.Activate(RemoteResourceIdentity.Static(mutation.Position));
            }

            terminal = true;
            return true;
        }

        public bool Rollback(out string error)
        {
            terminal = true;
            error = string.Empty;
            return true;
        }

        public void Dispose()
        {
            if (!terminal)
            {
                string ignored;
                Rollback(out ignored);
            }
        }
    }

    internal readonly World world;
    internal readonly EntityPlayer player;
    internal readonly bool owned;
    public string EndpointId { get; private set; }

    public QuickStackStaticDestinationEndpoint(World value, EntityPlayer entity, bool exactOwner)
    {
        world = value;
        player = entity;
        owned = exactOwner;
        EndpointId = "quickstack-static-destination:" +
            (entity != null ? entity.entityId.ToString() : "-1") + ":" + Guid.NewGuid().ToString("N");
    }

    public int GetAvailableDebitQuantity(string itemKey) { return 0; }

    public int GetAvailableCreditQuantity(string itemKey)
    {
        List<DestinationMutation> ignored;
        int transferable;
        // Large probe count gives the actual destination capacity without mutating.
        TryBuildMutations(itemKey, 100000000, false, out ignored, out transferable);
        return transferable;
    }

    public bool TryReserveDebit(
        Guid transactionId,
        string itemKey,
        int quantity,
        out IRebirthNpcExternalInventoryReservation reservation,
        out string error)
    {
        reservation = null;
        error = "Quick Stack static destination cannot be debited.";
        return false;
    }

    public bool TryReserveCredit(
        Guid transactionId,
        string itemKey,
        int quantity,
        out IRebirthNpcExternalInventoryReservation reservation,
        out string error)
    {
        reservation = null;
        error = string.Empty;
        if (transactionId == Guid.Empty || quantity <= 0)
        {
            error = "Invalid Quick Stack destination reservation.";
            return false;
        }

        List<DestinationMutation> mutations;
        int transferable;
        if (!TryBuildMutations(itemKey, quantity, true, out mutations, out transferable) ||
            transferable < quantity)
        {
            error = "Qualified Quick Stack storage no longer has enough capacity.";
            return false;
        }

        reservation = new Reservation(this, transactionId, mutations);
        return true;
    }

    private bool TryBuildMutations(
        string itemKey,
        int requested,
        bool requireFull,
        out List<DestinationMutation> mutations,
        out int transferable)
    {
        mutations = new List<DestinationMutation>();
        transferable = 0;
        if (world == null || player == null || requested <= 0) return false;
        ItemValue value = ItemClass.GetItem(itemKey, false);
        if (value.IsEmpty() || value.ItemClass == null) return false;

        ItemStack source = new ItemStack(value.Clone(), requested);
        QuickStackTransferPlanner.Plan plan = QuickStackTransferPlanner.Find(
            world, player, source, owned, RebirthSandboxOptionManager.Current.QuickStack);
        if (plan == null || plan.Destinations.Count == 0) return false;

        ItemStack remaining = source.Clone();
        Dictionary<Vector3i, DestinationMutation> byPosition = new Dictionary<Vector3i, DestinationMutation>();
        for (int i = 0; i < plan.Destinations.Count && remaining.count > 0; i++)
        {
            Vector3i position = plan.Destinations[i].Position;
            TileEntity te = world.GetTileEntity(position);
            TEFeatureStorage loot;
            string reason;
            if (!QuickStackService.CanUse(world, player, te, owned, out loot, out reason)) continue;

            DestinationMutation mutation;
            if (!byPosition.TryGetValue(position, out mutation))
            {
                mutation = new DestinationMutation
                {
                    Position = position,
                    Revision = RemoteResourceAccess.ComputeRevision(loot.ItemGrid.items),
                    After = LogisticsPreviewService.CloneSlots(loot.ItemGrid.items)
                };
                byPosition[position] = mutation;
                mutations.Add(mutation);
            }
            PackedBoolArray locks = loot.ItemGrid.SlotLocks;
            LogisticsPreviewService.SimulateInto(mutation.After, locks, remaining);
        }

        transferable = requested - remaining.count;
        return !requireFull || remaining.count == 0;
    }
}

/// <summary>
/// NPC companion transfers use the existing transactional external-inventory
/// coordinator, but this endpoint intentionally represents only the backpack.
/// It also implements the same protected-slot rule as Quick Stack: locked source
/// slots are not removed, locked occupied destination stacks may be topped up,
/// and locked empty slots are never used for a new stack.
/// </summary>
internal sealed class LogisticsBackpackEndpoint :
    IRebirthNpcExternalInventoryEndpoint,
    IRebirthNpcExternalInventoryQuantityEndpoint
{
    private sealed class Reservation : IRebirthNpcExternalInventoryReservation, IRebirthNpcExternalInventoryReservationOutcome
    {
        private readonly LogisticsBackpackEndpoint owner;
        private readonly string itemKey;
        private readonly int quantity;
        private readonly bool debit;
        private RebirthTransactionState state = RebirthTransactionState.Prepared;
        private string detail = string.Empty;
        public RebirthTransactionState State { get { return state; } }
        public string Detail { get { return detail; } }
        public string EndpointId { get { return owner.EndpointId; } }
        public Guid TransactionId { get; private set; }

        public Reservation(LogisticsBackpackEndpoint value, Guid id, string key, int amount, bool remove)
        { owner = value; TransactionId = id; itemKey = key; quantity = amount; debit = remove; }

        public bool Commit(out string error)
        {
            lock (this)
            {
                error = detail;
                if (state == RebirthTransactionState.Committed) return true;
                if (state != RebirthTransactionState.Prepared) return false;
                bool attempted = false;
                try
                {
                    
                    ItemStack[] slots = owner.player.bag.ItemGrid.items;
                    if (slots == null) throw new InvalidOperationException("Inventory is unavailable.");
                    ItemStack[] before = new ItemStack[slots.Length];
                    for (int i = 0; i < slots.Length; i++)
                        before[i] = slots[i] == null ? null : slots[i].Clone();
                    // Mark in flight before native callbacks can re-enter this reservation.
                    state = RebirthTransactionState.Indeterminate;
                    attempted = true;
                    bool success = debit ? owner.Remove(itemKey, quantity, out error) : owner.Add(itemKey, quantity, out error);
                    state = success ? RebirthTransactionState.Committed :
                        (SameContents(before, owner.player.bag.ItemGrid.items)
                            ? RebirthTransactionState.Failed : RebirthTransactionState.Indeterminate);
                    detail = success ? string.Empty : (string.IsNullOrEmpty(error) ? "Inventory transfer failed." : error);
                    error = detail;
                    return success;
                }
                catch (Exception ex)
                {
                    state = attempted ? RebirthTransactionState.Indeterminate : RebirthTransactionState.Failed;
                    error = detail = "Inventory transfer failed: " + ex.Message;
                    return false;
                }
            }
        }

        private static bool SameContents(ItemStack[] before, ItemStack[] after)
        {
            if (after == null || before.Length != after.Length) return false;
            for (int i = 0; i < before.Length; i++)
            {
                ItemStack a = before[i], b = after[i];
                if (a == null || b == null) { if (a != b) return false; continue; }
                if (a.count != b.count || !object.Equals(a.itemValue, b.itemValue)) return false;
            }
            return true;
        }

        public bool Rollback(out string error)
        {
            lock (this)
            {
                if (state == RebirthTransactionState.Committed || state == RebirthTransactionState.Indeterminate)
                {
                    error = "Inventory may have changed; explicit compensation is required.";
                    return false;
                }
                state = RebirthTransactionState.RolledBack;
                error = detail = "Inventory reservation was rolled back.";
                return true;
            }
        }

        public void Dispose()
        {
            if (state == RebirthTransactionState.Prepared) { string ignored; Rollback(out ignored); }
        }
    }

    private readonly EntityPlayer player;
    public string EndpointId { get; private set; }

    public LogisticsBackpackEndpoint(EntityPlayer value)
    {
        player = value;
        EndpointId = "quickstack-backpack:" + (value != null ? value.entityId.ToString() : "-1");
    }

    public Dictionary<string, int> GetUnlockedDebitQuantities()
    {
        Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (player == null) return result;
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locks = player.bag.LockedSlots;
        for (int i = 0; i < bag.Length; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            ItemStack stack = bag[i];
            if (stack == null || stack.IsEmpty()) continue;
            ItemClass item = ItemClass.GetForId(stack.itemValue.type);
            if (item == null || string.IsNullOrWhiteSpace(item.Name)) continue;
            int current;
            result.TryGetValue(item.Name, out current);
            result[item.Name] = current + stack.count;
        }
        return result;
    }

    public int GetAvailableDebitQuantity(string itemKey)
    {
        ItemValue value = ItemClass.GetItem(itemKey, false);
        if (player == null || value.IsEmpty()) return 0;
        int total = 0;
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locks = player.bag.LockedSlots;
        for (int i = 0; i < bag.Length; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            ItemStack stack = bag[i];
            if (stack != null && !stack.IsEmpty() && stack.itemValue.type == value.type) total += stack.count;
        }
        return total;
    }

    public int GetAvailableCreditQuantity(string itemKey)
    {
        ItemValue value = ItemClass.GetItem(itemKey, false);
        if (player == null || player.bag == null || value.IsEmpty()) return 0;

        long capacity = 0;
        int maxStack = ItemClass.GetForId(value.type).Stacknumber.Value;
        if (maxStack <= 0) return 0;
        ItemStack incoming = new ItemStack(value, int.MaxValue);
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locks = player.bag.LockedSlots;
        for (int i = 0; bag != null && i < bag.Length; i++)
        {
            ItemStack current = bag[i];
            if (current != null && !current.IsEmpty())
            {
                // Match MoveIntoBag's native stacking rules, including item metadata.
                int amount;
                if (current.CanStackPartlyWith(incoming, out amount) && amount > 0)
                    capacity += amount; // protected occupied stacks may be topped up
            }
            else if (!LogisticsPreviewService.IsLocked(locks, i))
                capacity += maxStack;
            if (capacity >= int.MaxValue) return int.MaxValue;
        }
        return (int)capacity;
    }

    public bool TryReserveDebit(Guid transactionId, string itemKey, int quantity,
        out IRebirthNpcExternalInventoryReservation reservation, out string error)
    {
        reservation = null; error = string.Empty;
        if (transactionId == Guid.Empty || quantity <= 0 || GetAvailableDebitQuantity(itemKey) < quantity)
        { error = "Backpack has insufficient unlocked source quantity."; return false; }
        reservation = new Reservation(this, transactionId, itemKey, quantity, true);
        return true;
    }

    public bool TryReserveCredit(Guid transactionId, string itemKey, int quantity,
        out IRebirthNpcExternalInventoryReservation reservation, out string error)
    {
        reservation = null; error = string.Empty;
        if (transactionId == Guid.Empty || quantity <= 0 || GetAvailableCreditQuantity(itemKey) < quantity)
        { error = "Backpack has insufficient destination capacity."; return false; }
        reservation = new Reservation(this, transactionId, itemKey, quantity, false);
        return true;
    }

    private bool Remove(string itemKey, int quantity, out string error)
    {
        error = string.Empty;
        ItemValue value = ItemClass.GetItem(itemKey, false);
        if (player == null || value.IsEmpty()) { error = "Backpack or item is unavailable."; return false; }
        // Preflight before mutating so a stale reservation cannot partially debit the bag.
        if (GetAvailableDebitQuantity(itemKey) < quantity)
        { error = "Backpack source changed before companion transfer commit."; return false; }
        int left = quantity;
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locks = player.bag.LockedSlots;
        for (int i = 0; i < bag.Length && left > 0; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            ItemStack stack = bag[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue.type != value.type) continue;
            int take = Math.Min(left, stack.count);
            ItemStack after = stack.Clone();
            after.count -= take;
            player.bag.SetSlot(i, after.count > 0 ? after : ItemStack.Empty);
            left -= take;
        }
        if (left == 0) return true;
        error = "Backpack source changed before companion transfer commit.";
        return false;
    }

    private bool Add(string itemKey, int quantity, out string error)
    {
        error = string.Empty;
        ItemValue value = ItemClass.GetItem(itemKey, false);
        if (player == null || value.IsEmpty()) { error = "Backpack or item is unavailable."; return false; }
        // Preflight before mutating so a stale reservation cannot partially credit the bag.
        if (GetAvailableCreditQuantity(itemKey) < quantity)
        { error = "Backpack destination changed before companion transfer commit."; return false; }
        ItemStack remaining = new ItemStack(value.Clone(), quantity);
        LogisticsTransferService.MoveIntoBag(player, remaining);
        if (remaining.count <= 0) return true;
        error = "Backpack destination changed before companion transfer commit.";
        return false;
    }
}


/// <summary>
/// Transactional endpoint over an already-authorized vehicle or drone inventory.
/// It lets the existing NPC inventory coordinator move items between Rebirth NPC
/// companions and mobile resource inventories without routing through the backpack.
/// </summary>
internal sealed class LogisticsRemoteResourceEndpoint :
    IRebirthNpcExternalInventoryEndpoint,
    IRebirthNpcExternalInventoryQuantityEndpoint
{
    private sealed class Reservation : IRebirthNpcExternalInventoryReservation, IRebirthNpcExternalInventoryReservationOutcome
    {
        private readonly LogisticsRemoteResourceEndpoint owner;
        private readonly string itemKey;
        private readonly int quantity;
        private readonly bool debit;
        private readonly int expectedRevision;
        private RebirthTransactionState state = RebirthTransactionState.Prepared;
        private string detail = string.Empty;
        public RebirthTransactionState State { get { return state; } }
        public string Detail { get { return detail; } }

        public string EndpointId { get { return owner.EndpointId; } }
        public Guid TransactionId { get; private set; }

        public Reservation(
            LogisticsRemoteResourceEndpoint value,
            Guid id,
            string key,
            int amount,
            bool remove,
            int revision)
        {
            owner = value;
            TransactionId = id;
            itemKey = key;
            quantity = amount;
            debit = remove;
            expectedRevision = revision;
        }

        public bool Commit(out string error)
        {
            lock (this)
            {
                error = detail;
                if (state == RebirthTransactionState.Committed) return true;
                if (state != RebirthTransactionState.Prepared) return false;
                bool attempted = false;
                try
                {
                    if (!LogisticsTransferService.CanMutateResource(owner.player, owner.source, debit) ||
                        owner.source.Revision != expectedRevision)
                    {
                        state = RebirthTransactionState.Failed;
                        error = detail = "Mobile inventory changed before transfer commit.";
                        return false;
                    }
                    ItemStack[] slots = owner.source.Slots;
                    if (slots == null) throw new InvalidOperationException("Inventory is unavailable.");
                    ItemStack[] before = new ItemStack[slots.Length];
                    for (int i = 0; i < slots.Length; i++)
                        before[i] = slots[i] == null ? null : slots[i].Clone();
                    // Mark in flight before native callbacks can re-enter this reservation.
                    state = RebirthTransactionState.Indeterminate;
                    attempted = true;
                    bool success = debit ? owner.Remove(itemKey, quantity, out error) : owner.Add(itemKey, quantity, out error);
                    state = success ? RebirthTransactionState.Committed :
                        (SameContents(before, owner.source.Slots)
                            ? RebirthTransactionState.Failed : RebirthTransactionState.Indeterminate);
                    detail = success ? string.Empty : (string.IsNullOrEmpty(error) ? "Inventory transfer failed." : error);
                    error = detail;
                    return success;
                }
                catch (Exception ex)
                {
                    state = attempted ? RebirthTransactionState.Indeterminate : RebirthTransactionState.Failed;
                    error = detail = "Inventory transfer failed: " + ex.Message;
                    return false;
                }
            }
        }

        private static bool SameContents(ItemStack[] before, ItemStack[] after)
        {
            if (after == null || before.Length != after.Length) return false;
            for (int i = 0; i < before.Length; i++)
            {
                ItemStack a = before[i], b = after[i];
                if (a == null || b == null) { if (a != b) return false; continue; }
                if (a.count != b.count || !object.Equals(a.itemValue, b.itemValue)) return false;
            }
            return true;
        }

        public bool Rollback(out string error)
        {
            lock (this)
            {
                if (state == RebirthTransactionState.Committed || state == RebirthTransactionState.Indeterminate)
                {
                    error = "Inventory may have changed; explicit compensation is required.";
                    return false;
                }
                state = RebirthTransactionState.RolledBack;
                error = detail = "Inventory reservation was rolled back.";
                return true;
            }
        }

        public void Dispose()
        {
            if (state == RebirthTransactionState.Prepared) { string ignored; Rollback(out ignored); }
        }
    }

    private readonly IRemoteResourceSource source;
    private readonly EntityPlayer player;
    public string EndpointId { get; private set; }

    public LogisticsRemoteResourceEndpoint(IRemoteResourceSource value, EntityPlayer requester)
    {
        source = value;
        player = requester;
        EndpointId = "quickstack-mobile:" +
            (value != null ? value.StableId : "missing") + ":" + Guid.NewGuid().ToString("N");
    }

    public Dictionary<string, int> GetUnlockedDebitQuantities()
    {
        Dictionary<string, int> result =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (source == null) return result;

        ItemStack[] slots = source.Slots;
        PackedBoolArray locks = source.SlotLocks;
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty()) continue;
            ItemClass item = ItemClass.GetForId(stack.itemValue.type);
            if (item == null || string.IsNullOrWhiteSpace(item.Name)) continue;
            int current;
            result.TryGetValue(item.Name, out current);
            result[item.Name] = current + stack.count;
        }
        return result;
    }

    public int GetAvailableDebitQuantity(string itemKey)
    {
        ItemValue value = ItemClass.GetItem(itemKey, false);
        if (source == null || value.IsEmpty()) return 0;
        int total = 0;
        ItemStack[] slots = source.Slots;
        PackedBoolArray locks = source.SlotLocks;
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            ItemStack stack = slots[i];
            if (stack != null && !stack.IsEmpty() && stack.itemValue.type == value.type)
                total += stack.count;
        }
        return total;
    }

    public int GetAvailableCreditQuantity(string itemKey)
    {
        ItemValue value = ItemClass.GetItem(itemKey, false);
        if (source == null || value.IsEmpty()) return 0;

        long capacity = 0;
        int maxStack = ItemClass.GetForId(value.type).Stacknumber.Value;
        if (maxStack <= 0) return 0;
        ItemStack incoming = new ItemStack(value, int.MaxValue);
        ItemStack[] bag = source.Slots;
        PackedBoolArray locks = source.SlotLocks;
        for (int i = 0; bag != null && i < bag.Length; i++)
        {
            ItemStack current = bag[i];
            if (current != null && !current.IsEmpty())
            {
                // Match MoveInto's native stacking rules, including item metadata.
                int amount;
                if (current.CanStackPartlyWith(incoming, out amount) && amount > 0)
                    capacity += amount; // protected occupied stacks may be topped up
            }
            else if (!LogisticsPreviewService.IsLocked(locks, i))
                capacity += maxStack;
            if (capacity >= int.MaxValue) return int.MaxValue;
        }
        return (int)capacity;
    }

    public bool TryReserveDebit(
        Guid transactionId,
        string itemKey,
        int quantity,
        out IRebirthNpcExternalInventoryReservation reservation,
        out string error)
    {
        reservation = null;
        error = string.Empty;
        if (source == null || transactionId == Guid.Empty || quantity <= 0 ||
            GetAvailableDebitQuantity(itemKey) < quantity)
        {
            error = "Mobile source has insufficient unlocked quantity.";
            return false;
        }
        reservation = new Reservation(
            this, transactionId, itemKey, quantity, true, source.Revision);
        return true;
    }

    public bool TryReserveCredit(
        Guid transactionId,
        string itemKey,
        int quantity,
        out IRebirthNpcExternalInventoryReservation reservation,
        out string error)
    {
        reservation = null;
        error = string.Empty;
        if (source == null || transactionId == Guid.Empty || quantity <= 0 ||
            GetAvailableCreditQuantity(itemKey) < quantity)
        {
            error = "Mobile destination has insufficient capacity.";
            return false;
        }
        reservation = new Reservation(
            this, transactionId, itemKey, quantity, false, source.Revision);
        return true;
    }

    private bool Remove(string itemKey, int quantity, out string error)
    {
        error = string.Empty;
        ItemValue value = ItemClass.GetItem(itemKey, false);
        if (source == null || value.IsEmpty())
        {
            error = "Mobile source or item is unavailable.";
            return false;
        }
        if (GetAvailableDebitQuantity(itemKey) < quantity)
        {
            error = "Mobile source changed before transfer commit.";
            return false;
        }

        int left = quantity;
        ItemStack[] slots = source.Slots;
        PackedBoolArray locks = source.SlotLocks;
        for (int i = 0; slots != null && i < slots.Length && left > 0; i++)
        {
            if (LogisticsPreviewService.IsLocked(locks, i)) continue;
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue.type != value.type) continue;
            int take = Math.Min(left, stack.count);
            ItemStack after = stack.Clone();
            after.count -= take;
            source.SetSlot(i, after.count > 0 ? after : ItemStack.Empty);
            left -= take;
            slots = source.Slots;
        }
        if (left != 0)
        {
            error = "Mobile source changed before transfer commit.";
            return false;
        }
        source.MarkModified();
        return true;
    }

    private bool Add(string itemKey, int quantity, out string error)
    {
        error = string.Empty;
        ItemValue value = ItemClass.GetItem(itemKey, false);
        if (source == null || value.IsEmpty())
        {
            error = "Mobile destination or item is unavailable.";
            return false;
        }
        if (GetAvailableCreditQuantity(itemKey) < quantity)
        {
            error = "Mobile destination changed before transfer commit.";
            return false;
        }

        ItemStack remaining = new ItemStack(value.Clone(), quantity);
        LogisticsTransferService.MoveInto(source, remaining);
        if (remaining.count > 0)
        {
            error = "Mobile destination changed before transfer commit.";
            return false;
        }
        source.MarkModified();
        return true;
    }
}

[Preserve]
public sealed class NetPackageLogisticsPreviewRequest : NetPackage
{
    private int id;
    private PlatformUserIdentifierAbs uid;
    private ulong requestEpoch;
    private ulong requestId;
    private QuickStackRadialAction action;
    private string targetId;
    private Bag clientBag;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageLogisticsPreviewRequest Setup(
        int entityId,
        PersistentPlayerData persistent,
        ulong epoch,
        ulong request,
        QuickStackRadialAction value,
        string selectedTargetId,
        Bag bagSnapshot)
    {
        id = entityId;
        uid = persistent.PrimaryId;
        requestEpoch = epoch; requestId = request;
        action = value;
        targetId = selectedTargetId ?? string.Empty;
        // The server builds previews from its own authoritative inventory. Retain the
        // optional field on the wire for older peers, but do not clone/send an unused bag.
        clientBag = null;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        id = binary.ReadInt32();
        uid = PlatformUserIdentifierAbs.FromStream(binary);
        requestEpoch = binary.ReadUInt64(); requestId = binary.ReadUInt64();
        action = (QuickStackRadialAction)binary.ReadByte();
        targetId = RebirthSurvivorNetworkCodec.ReadBoundedString(binary, 512);
        clientBag = binary.ReadBoolean() ? LogisticsBagCodec.Read(reader) : null;
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        PooledBinaryWriter binary = writer;
        binary.Write(id);
        uid.ToStream(binary);
        binary.Write(requestEpoch); binary.Write(requestId);
        binary.Write((byte)action);
        RebirthSurvivorNetworkCodec.WriteString(binary, targetId, 512);
        binary.Write(clientBag != null);
        if (clientBag != null) LogisticsBagCodec.Write(writer, clientBag);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || requestEpoch == 0 || requestId == 0 ||
            (byte)action > (byte)QuickStackRadialAction.PullDrone || !ValidEntityIdForSender(id) || !ValidUserIdForSender(uid)) return;
        EntityPlayer player = world.GetEntity(id) as EntityPlayer;
        if (player == null) return;

        // Preview is strictly observational. Never replace authoritative server inventory
        // from a client-provided preview snapshot.
        string data = LogisticsPreviewService.Build(world, player, action, targetId).Serialize();
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageLogisticsPreviewResult>().Setup(requestEpoch, requestId, action, data),
            _attachedToEntityId: id);
    }

    public int GetLength() { return 0; }
}

[Preserve]
public sealed class NetPackageLogisticsPreviewResult : NetPackage
{
    private ulong requestEpoch;
    private ulong requestId;
    private QuickStackRadialAction action;
    private string data;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageLogisticsPreviewResult Setup(ulong epoch, ulong request, QuickStackRadialAction value, string serialized)
    { requestEpoch = epoch; requestId = request; action = value; data = serialized; return this; }
    public override void read(PooledBinaryReader reader)
    { PooledBinaryReader binary = reader; requestEpoch = binary.ReadUInt64(); requestId = binary.ReadUInt64(); action = (QuickStackRadialAction)binary.ReadByte(); data = RebirthSurvivorNetworkCodec.ReadBoundedString(binary, 131072); }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer); PooledBinaryWriter binary = writer; binary.Write(requestEpoch); binary.Write(requestId); binary.Write((byte)action); RebirthSurvivorNetworkCodec.WriteString(binary, data, 131072); }
    public override void ProcessPackage(World world, GameManager callbacks)
    { LogisticsPreviewService.Receive(requestEpoch, requestId, action, LogisticsPreviewData.Deserialize(data)); }
    public int GetLength() { return 131072; }
}

internal sealed class LogisticsTransferOutcome
{
    public bool Known;
    public ItemStack[] BagItems;
    public int[] NotificationItemTypes;
    public int[] NotificationSignedCounts;

    public static LogisticsTransferOutcome Unknown() { return new LogisticsTransferOutcome { Known = false }; }
    public static LogisticsTransferOutcome Denied() { return new LogisticsTransferOutcome { Known = true, BagItems = null }; }

    public static LogisticsTransferOutcome From(EntityPlayer player, Dictionary<int, int> notifications)
    {
        LogisticsTransferOutcome result = new LogisticsTransferOutcome
        {
            Known = true,
            BagItems = QuickStackRemotePlayerBagSync.CaptureItems(player != null ? player.bag : null)
        };
        if (notifications != null && notifications.Count > 0)
        {
            List<int> keys = new List<int>(notifications.Keys); keys.Sort();
            result.NotificationItemTypes = new int[keys.Count];
            result.NotificationSignedCounts = new int[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                result.NotificationItemTypes[i] = keys[i];
                result.NotificationSignedCounts[i] = notifications[keys[i]];
            }
        }
        return result;
    }

    public LogisticsTransferOutcome Clone()
    {
        return new LogisticsTransferOutcome
        {
            Known = Known,
            BagItems = BagItems != null ? ItemStack.Clone((IList<ItemStack>)BagItems) : null,
            NotificationItemTypes = NotificationItemTypes != null ? (int[])NotificationItemTypes.Clone() : null,
            NotificationSignedCounts = NotificationSignedCounts != null ? (int[])NotificationSignedCounts.Clone() : null
        };
    }
}

internal static class LogisticsTransferOutcomeJournal
{
    private const int MaxEntries = 1024;
    private static readonly Dictionary<string, LogisticsTransferOutcome> Outcomes = new Dictionary<string, LogisticsTransferOutcome>(StringComparer.Ordinal);
    private static readonly Queue<string> Order = new Queue<string>();
    private static World boundWorld;

    public static bool TryGet(World world, PlatformUserIdentifierAbs uid, ulong epoch, int requestId, out LogisticsTransferOutcome outcome)
    {
        BindWorld(world);
        LogisticsTransferOutcome value;
        if (Outcomes.TryGetValue(Key(uid, epoch, requestId), out value))
        {
            outcome = value.Clone(); return true;
        }
        outcome = null; return false;
    }

    public static LogisticsTransferOutcome Publish(World world, PlatformUserIdentifierAbs uid, ulong epoch, int requestId, LogisticsTransferOutcome outcome)
    {
        BindWorld(world);
        string key = Key(uid, epoch, requestId);
        LogisticsTransferOutcome existing;
        if (Outcomes.TryGetValue(key, out existing)) return existing.Clone();
        LogisticsTransferOutcome value = (outcome ?? LogisticsTransferOutcome.Unknown()).Clone();
        Outcomes[key] = value; Order.Enqueue(key);
        while (Outcomes.Count > MaxEntries && Order.Count > 0) Outcomes.Remove(Order.Dequeue());
        return value.Clone();
    }

    public static void ResetForWorldUnload() { Outcomes.Clear(); Order.Clear(); boundWorld = null; }
    private static void BindWorld(World world) { if (ReferenceEquals(boundWorld, world)) return; Outcomes.Clear(); Order.Clear(); boundWorld = world; }
    private static string Key(PlatformUserIdentifierAbs uid, ulong epoch, int requestId)
    { return (uid != null ? uid.CombinedString : string.Empty) + "|" + epoch + "|" + requestId; }
}

[Preserve]
public sealed class NetPackageLogisticsTransferRequest : NetPackage
{
    private const int MaxTargetIdLength = 512;
    private const int MaxSourceKeysLength = 8192;
    private int id;
    private PlatformUserIdentifierAbs uid;
    private QuickStackRadialAction action;
    private string targetId;
    private string selectedSourceKeys;
    private Bag clientBag;
    private int requestId;
    private ulong requestEpoch;
    private LogisticsTransferRequestMode requestMode;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageLogisticsTransferRequest Setup(
        int entityId,
        PersistentPlayerData persistent,
        QuickStackRadialAction value,
        string selectedTargetId,
        string sourceKeys,
        Bag bagSnapshot,
        int transferRequestId,
        ulong transferRequestEpoch,
        LogisticsTransferRequestMode mode)
    {
        id = entityId;
        uid = persistent.PrimaryId;
        action = value;
        targetId = selectedTargetId ?? string.Empty;
        selectedSourceKeys = sourceKeys ?? string.Empty;
        clientBag = LogisticsBagCodec.Capture(bagSnapshot);
        requestId = transferRequestId;
        requestEpoch = transferRequestEpoch;
        requestMode = mode;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        id = binary.ReadInt32();
        uid = PlatformUserIdentifierAbs.FromStream(binary);
        action = (QuickStackRadialAction)binary.ReadByte();
        targetId = RebirthSurvivorNetworkCodec.ReadBoundedString(binary, MaxTargetIdLength);
        selectedSourceKeys = RebirthSurvivorNetworkCodec.ReadBoundedString(binary, MaxSourceKeysLength);
        requestId = binary.ReadInt32();
        requestEpoch = binary.ReadUInt64();
        requestMode = (LogisticsTransferRequestMode)binary.ReadByte();
        clientBag = binary.ReadBoolean() ? LogisticsBagCodec.Read(reader) : null;
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        PooledBinaryWriter binary = writer;
        binary.Write(id);
        uid.ToStream(binary);
        binary.Write((byte)action);
        RebirthSurvivorNetworkCodec.WriteString(binary, targetId, MaxTargetIdLength);
        RebirthSurvivorNetworkCodec.WriteString(binary, selectedSourceKeys, MaxSourceKeysLength);
        binary.Write(requestId);
        binary.Write(requestEpoch);
        binary.Write((byte)requestMode);
        binary.Write(clientBag != null);
        if (clientBag != null) LogisticsBagCodec.Write(writer, clientBag);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || requestEpoch == 0 || requestId <= 0 ||
            requestMode < LogisticsTransferRequestMode.ExecuteOrReplay || requestMode > LogisticsTransferRequestMode.QueryOnly ||
            (byte)action > (byte)QuickStackRadialAction.PullDroneCredits ||
            !ValidEntityIdForSender(id) || !ValidUserIdForSender(uid)) return;
        EntityPlayer player = world.GetEntity(id) as EntityPlayer;
        if (player == null) return;

        LogisticsTransferOutcome existing;
        if (LogisticsTransferOutcomeJournal.TryGet(world, uid, requestEpoch, requestId, out existing))
        {
            SendOutcome(existing);
            return;
        }
        if (requestMode == LogisticsTransferRequestMode.QueryOnly)
        {
            SendOutcome(LogisticsTransferOutcome.Unknown());
            return;
        }

        LogisticsTransferOutcome outcome;
        string dispatchDenial;
        if (!LogisticsTransferService.CanDispatch(world, player, action, out dispatchDenial))
        {
            if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("transfer denied before bag sync: " + dispatchDenial);
            outcome = LogisticsTransferOutcome.Denied();
        }
        else if (!QuickStackRemotePlayerBagSync.MatchesServerSnapshot(player, clientBag, "transfer"))
        {
            // A credit response must never contain a resync of the entire bag.
            outcome = LogisticsInventoryCredits.UsesReceipt(action)
                ? LogisticsTransferOutcome.Denied() : LogisticsTransferOutcome.From(player, null);
        }
        else
        {
            Dictionary<int, int> notifications = LogisticsTransferService.ProcessWithNotifications(
                world, id, uid, action, targetId, selectedSourceKeys);
            QuickStackRemotePlayerBagSync.CommitServerBag(player, Sender);
            outcome = LogisticsTransferOutcome.From(player, notifications);
            if (LogisticsInventoryCredits.UsesReceipt(action))
                outcome.BagItems = LogisticsInventoryCredits.Capture(clientBag.ItemGrid.items, outcome.BagItems);
        }

        outcome = LogisticsTransferOutcomeJournal.Publish(world, uid, requestEpoch, requestId, outcome);
        SendOutcome(outcome);
    }

    private void SendOutcome(LogisticsTransferOutcome outcome)
    {
        LogisticsTransferOutcome value = outcome ?? LogisticsTransferOutcome.Unknown();
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageLogisticsTransferResult>()
                .Setup(requestEpoch, requestId, value.Known, value.BagItems, value.NotificationItemTypes, value.NotificationSignedCounts),
            _attachedToEntityId: id);
    }

    public int GetLength() { return 0; }
}

[Preserve]
public sealed class NetPackageLogisticsTransferResult : NetPackage
{
    private const int MaxNotificationEntries = 256;
    private ulong requestEpoch;
    private int requestId;
    private bool outcomeKnown;
    private ItemStack[] bagItems;
    private int[] notificationItemTypes;
    private int[] notificationSignedCounts;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageLogisticsTransferResult Setup(
        ulong transferRequestEpoch,
        int transferRequestId,
        bool known,
        ItemStack[] items,
        int[] notificationTypes,
        int[] notificationCounts)
    {
        requestEpoch = transferRequestEpoch;
        requestId = transferRequestId;
        outcomeKnown = known;
        bagItems = items != null ? ItemStack.Clone((IList<ItemStack>)items) : null;
        notificationItemTypes = notificationTypes != null ? (int[])notificationTypes.Clone() : null;
        notificationSignedCounts = notificationCounts != null ? (int[])notificationCounts.Clone() : null;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        requestEpoch = binary.ReadUInt64();
        requestId = binary.ReadInt32();
        outcomeKnown = binary.ReadBoolean();
        bagItems = binary.ReadBoolean() ? ItemStack.ReadArray(binary) : null;

        int notificationCount = binary.ReadUInt16();
        if (notificationCount > MaxNotificationEntries) throw new InvalidDataException("Quick Stack notification count exceeds packet bound.");
        if (notificationCount <= 0)
        {
            notificationItemTypes = null;
            notificationSignedCounts = null;
            return;
        }

        notificationItemTypes = new int[notificationCount];
        notificationSignedCounts = new int[notificationCount];
        for (int i = 0; i < notificationCount; i++)
        {
            notificationItemTypes[i] = binary.ReadInt32();
            notificationSignedCounts[i] = binary.ReadInt32();
        }
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        PooledBinaryWriter binary = writer;
        binary.Write(requestEpoch);
        binary.Write(requestId);
        binary.Write(outcomeKnown);
        binary.Write(bagItems != null);
        if (bagItems != null) ItemStack.WriteArray(binary, bagItems);

        int notificationCount = notificationItemTypes != null && notificationSignedCounts != null
            ? Math.Min(notificationItemTypes.Length, notificationSignedCounts.Length)
            : 0;
        if (notificationCount > MaxNotificationEntries) throw new InvalidDataException("Quick Stack notification count exceeds packet bound.");
        binary.Write((ushort)notificationCount);
        for (int i = 0; i < notificationCount; i++)
        {
            binary.Write(notificationItemTypes[i]);
            binary.Write(notificationSignedCounts[i]);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        LogisticsTransferService.ReceiveCompleted(
            requestEpoch, requestId, outcomeKnown, bagItems, notificationItemTypes, notificationSignedCounts);
    }

    public int GetLength() { return 0; }
}
