using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#nullable disable

/// <summary>Conservative real-input loot staging. Never spawns crates or rewrites inventories.</summary>
public sealed class RebirthGameBridgePoiStorage
{
    public const string CrateName = "cntWoodWritableCrate";
    private readonly List<Vector3i> crates = new List<Vector3i>();
    private readonly Dictionary<Vector3i, TEFeatureStorage> placedCrateFeatures = new Dictionary<Vector3i, TEFeatureStorage>();
    private readonly Dictionary<Vector3i, TileEntityComposite> placementParents = new Dictionary<Vector3i, TileEntityComposite>();
    private readonly Dictionary<Vector3i, TEFeatureRebirthPoiCrateIdentity> placementMarkers = new Dictionary<Vector3i, TEFeatureRebirthPoiCrateIdentity>();
    private readonly Dictionary<Vector3i, Guid> placedCrateIds = new Dictionary<Vector3i, Guid>();
    private readonly HashSet<Guid> publishedPlacementIds = new HashSet<Guid>();
    private PrefabInstance boundPoi;
    private World custodyWorld;
    private bool expectationsLoaded;
    private string expectationFailure, recoveryFailure;
    private readonly HashSet<string> retainedSupplyKeys = new HashSet<string>(StringComparer.Ordinal);
    private Vector3? anchor;
    public string Failure { get; private set; }
    public bool RetryAfterThreat { get; private set; }
    public IReadOnlyList<Vector3i> Crates { get { return crates; } }
    public bool Contains(EntityPlayerLocal player, Vector3i pos) { return PlacedLoot(player, pos) != null; }

    public IEnumerator RefreshExpectations(EntityPlayerLocal player, PrefabInstance poi)
    {
        expectationsLoaded = false;
        expectationFailure = null;
        var world = player?.world;
        if (world == null || poi == null || GameManager.Instance == null
            || !ReferenceEquals(GameManager.Instance.World, world) || !ReferenceEquals(world.GetPrimaryPlayer(), player)
            || (custodyWorld != null && !ReferenceEquals(custodyWorld, world)))
        { expectationFailure = "storage recovery world changed or is unavailable"; yield break; }
        custodyWorld = world;
        int expectedPoiId = poi.id;
        Vector3i expectedOrigin = poi.boundingBoxPosition, expectedSize = poi.boundingBoxSize;
        Func<bool> sameRecoveryContext = () => ReferenceEquals(player.world, world) && !player.IsDead()
            && GameManager.Instance != null && ReferenceEquals(GameManager.Instance.World, world)
            && ReferenceEquals(world.GetPrimaryPlayer(), player)
            && poi.id == expectedPoiId && poi.boundingBoxPosition.Equals(expectedOrigin)
            && poi.boundingBoxSize.Equals(expectedSize);
        List<RebirthPoiCrateExpectation> expected = null;
        yield return RebirthPoiCrateRecovery.Request(player, poi, (records, error) =>
        { expected = records; expectationFailure = error; });
        if (!sameRecoveryContext())
        { expectationFailure = "storage recovery context changed"; yield break; }
        if (expectationFailure != null || expected == null)
        { if (expectationFailure == null) expectationFailure = "expected crate snapshot unavailable"; yield break; }
        if (expected.Count > RebirthPoiCrateLedger.MaxPerScope)
        { expectationFailure = "expected crate snapshot exceeds bounds"; yield break; }
        var snapshotIds = new HashSet<Guid>();
        var snapshotPositions = new HashSet<Vector3i>();
        foreach (var record in expected)
        {
            Guid prior;
            if (record == null || !record.Valid || !snapshotIds.Add(record.Placement) || !snapshotPositions.Add(record.Position)
                || record.PoiId != poi.id || !record.PoiOrigin.Equals(poi.boundingBoxPosition)
                || !record.PoiSize.Equals(poi.boundingBoxSize)
                || (placedCrateIds.TryGetValue(record.Position, out prior) && prior != record.Placement))
            { expectationFailure = "expected crate snapshot conflicts with remembered custody"; yield break; }
        }
        boundPoi = poi;
        foreach (var record in expected)
        {
            placedCrateIds[record.Position] = record.Placement;
            publishedPlacementIds.Add(record.Placement);
            if (!crates.Contains(record.Position)) crates.Add(record.Position);
        }
        expectationsLoaded = true;
        var pendingRecovery = RecoverPendingPlacements(player, poi, snapshotPositions, sameRecoveryContext);
        try { while (pendingRecovery.MoveNext()) yield return pendingRecovery.Current; }
        finally { (pendingRecovery as IDisposable)?.Dispose(); }
        if (!expectationsLoaded) yield break;
        // Resume a published expectation whose native POI bind was interrupted before
        // acknowledgement. Approach only a matching owned GUID, then within interaction reach
        // may be retried; missing/replaced crates remain unresolved, never recreated.
        bool requested = false;
        foreach (var record in expected)
        {
            var marker = RebirthPoiCrateIdentity.Resolve(player, record.Position);
            if (marker == null || marker.PlacementId != record.Placement || !marker.IsUnbound
                || !marker.HasLocalOwner(player)) continue;
            Func<string> interrupted = () =>
            {
                if (!sameRecoveryContext()) return "recovery_context_changed";
                var current = RebirthPoiCrateIdentity.Resolve(player, record.Position);
                if (!ReferenceEquals(current, marker) || current.PlacementId != record.Placement
                    || !current.IsUnbound || !current.HasLocalOwner(player)) return "recovery_marker_changed";
                if (RebirthGameBridgeCombat.AwakeThreats(player, 30f, false).Count > 0) return "recovery_threat";
                return null;
            };
            if (interrupted() != null) continue;
            if (Vector3.Distance(player.position, marker.ToWorldCenterPos()) > 8f)
            {
                string walked = null;
                using (var recoveryInput = new RebirthGameBridgeInput.OwnedInputScope(player, () => interrupted() == null))
                {
                var movement = RebirthGameBridgePlayer.WalkToGuarded(player, marker.ToWorldCenterPos(),
                    4f, 30f, interrupted, result => walked = result, recoveryInput);
                try { while (movement.MoveNext()) yield return movement.Current; }
                finally { (movement as IDisposable)?.Dispose(); }
                }
                if (walked != "arrived") continue;
            }
            if (interrupted() == null && Vector3.Distance(player.position, marker.ToWorldCenterPos()) <= 8f)
                requested |= RebirthPoiCrateIdentity.RequestBinding(player, poi, record.Position, record.Placement);
        }
        float until = Time.realtimeSinceStartup + 5f;
        while (requested && Time.realtimeSinceStartup < until && sameRecoveryContext())
        {
            bool waiting = false;
            foreach (var record in expected)
            {
                var marker = RebirthPoiCrateIdentity.Resolve(player, record.Position);
                if (marker != null && marker.PlacementId == record.Placement && marker.IsUnbound
                    && marker.HasLocalOwner(player) && Vector3.Distance(player.position, marker.ToWorldCenterPos()) <= 8f)
                { waiting = true; break; }
            }
            if (!waiting) break;
            yield return null;
        }
    }
    private IEnumerator RecoverPendingPlacements(EntityPlayerLocal player, PrefabInstance poi,
        HashSet<Vector3i> publishedPositions, Func<bool> sameContext)
    {
        var pending = new List<KeyValuePair<Vector3i, TEFeatureStorage>>(placedCrateFeatures);
        foreach (var entry in pending)
        {
            if (publishedPositions.Contains(entry.Key)) continue;
            Vector3i position = entry.Key;
            TEFeatureStorage original = entry.Value;
            TEFeatureRebirthPoiCrateIdentity marker = RebirthPoiCrateIdentity.Resolve(player, position);
            Guid expectedPlacement = Guid.Empty;
            Func<string> interrupted = () =>
            {
                if (!sameContext()) return "pending crate recovery context changed";
                if (!IsNewPlacementWitness(player, position, original, marker)
                    || (expectedPlacement != Guid.Empty && (marker == null || marker.PlacementId != expectedPlacement)))
                    return "pending original crate or marker changed";
                if (RebirthGameBridgeCombat.AwakeThreats(player, 30f, false).Count > 0)
                    return "pending crate recovery threat";
                return null;
            };
            float until = Time.realtimeSinceStartup + 5f;
            string error = interrupted();
            while (error == null && (marker == null || marker.PlacementId == Guid.Empty)
                && Time.realtimeSinceStartup < until)
            {
                yield return null;
                if (!sameContext()) { error = "pending crate recovery context changed"; break; }
                var current = RebirthPoiCrateIdentity.Resolve(player, position);
                if (marker != null && !ReferenceEquals(marker, current))
                { error = "pending original crate marker changed"; break; }
                marker = current;
                error = interrupted();
            }
            if (error == null && (marker == null || marker.PlacementId == Guid.Empty))
                error = "pending original crate durable identity unavailable";
            if (error == null) expectedPlacement = marker.PlacementId;
            Guid remembered;
            if (error == null && placedCrateIds.TryGetValue(position, out remembered)
                && remembered != marker.PlacementId) error = "pending original crate placement identity changed";
            if (error == null && !marker.IsUnbound && !marker.MatchesPoi(poi))
                error = "pending original crate belongs to another POI";
            if (error == null && Vector3.Distance(player.position, marker.ToWorldCenterPos()) > 8f)
            {
                string walked = null;
                using (var recoveryInput = new RebirthGameBridgeInput.OwnedInputScope(player, () => interrupted() == null))
                {
                var movement = RebirthGameBridgePlayer.WalkToGuarded(player, marker.ToWorldCenterPos(),
                    4f, 30f, interrupted, result => walked = result, recoveryInput);
                try { while (movement.MoveNext()) yield return movement.Current; }
                finally { (movement as IDisposable)?.Dispose(); }
                }
                error = interrupted();
                if (error == null && (walked != "arrived"
                    || Vector3.Distance(player.position, marker.ToWorldCenterPos()) > 8f))
                    error = "pending original crate recovery route incomplete";
            }
            if (error == null) error = interrupted();
            if (error == null)
            {
                placedCrateIds[position] = expectedPlacement;
                if (!RebirthPoiCrateIdentity.RequestBinding(player, poi, position, expectedPlacement))
                    error = "pending original crate binding refused";
            }
            until = Time.realtimeSinceStartup + 5f;
            while (error == null && !marker.MatchesPoi(poi) && Time.realtimeSinceStartup < until)
            { yield return null; error = interrupted(); }
            if (error == null) error = interrupted();
            if (error == null && !marker.MatchesPoi(poi)) error = "pending original crate binding unacknowledged";
            if (error == null)
            {
                // A pre-existing bound marker is not a new binding ACK. Confirm the actual
                // published expectation as well, including on remote request replay.
                List<RebirthPoiCrateExpectation> verified = null;
                string snapshotError = null;
                var request = RebirthPoiCrateRecovery.Request(player, poi,
                    (records, failure) => { verified = records; snapshotError = failure; });
                try
                {
                    while (request.MoveNext())
                    {
                        error = interrupted();
                        if (error != null) break;
                        yield return request.Current;
                        error = interrupted();
                        if (error != null) break;
                    }
                }
                finally { (request as IDisposable)?.Dispose(); }
                if (error == null)
                {
                    bool found = false;
                    if (snapshotError == null && verified != null
                        && verified.Count <= RebirthPoiCrateLedger.MaxPerScope)
                        foreach (var record in verified)
                            if (record != null && record.Valid && record.Position.Equals(position)
                                && record.Placement == expectedPlacement && record.PoiId == poi.id
                                && record.PoiOrigin.Equals(poi.boundingBoxPosition)
                                && record.PoiSize.Equals(poi.boundingBoxSize)) { found = true; break; }
                    if (!found) error = "pending original crate durable expectation unacknowledged";
                    else publishedPlacementIds.Add(expectedPlacement);
                }
            }
            if (error != null)
            { expectationFailure = error; expectationsLoaded = false; yield break; }
        }
    }
    // Discover only explicitly bound, native-persisted markers belonging to this player.
    // Coordinates locate candidates; matching placement IDs authorize their reuse.
    public bool Recover(EntityPlayerLocal player, PrefabInstance poi)
    {
        if (player?.world == null || poi == null || !ReferenceEquals(player.world, custodyWorld)
            || GameManager.Instance == null || !ReferenceEquals(GameManager.Instance.World, custodyWorld)
            || !ReferenceEquals(custodyWorld.GetPrimaryPlayer(), player)) return false;
        recoveryFailure = null;
        if (!expectationsLoaded) return false;
        boundPoi = poi;
        bool allLoaded = true;
        Vector3i min = poi.boundingBoxPosition, size = poi.boundingBoxSize;
        for (int cx = World.toChunkXZ(min.x - 12); cx <= World.toChunkXZ(min.x + size.x + 12); cx++)
            for (int cz = World.toChunkXZ(min.z - 12); cz <= World.toChunkXZ(min.z + size.z + 12); cz++)
            {
                var chunk = player.world.GetChunkSync(cx, cz) as Chunk;
                if (chunk == null) { allLoaded = false; continue; }
                foreach (TileEntity tile in chunk.GetTileEntities().list)
                {
                    TEFeatureRebirthPoiCrateIdentity marker;
                    if (tile == null || !tile.TryGetSelfOrFeature(out marker) || marker == null
                        || !marker.MatchesPoi(poi) || !marker.HasLocalOwner(player)) continue;
                    Vector3i pos = tile.ToWorldPos();
                    TEFeatureStorage loot = Loot(player, pos);
                    if (loot == null) continue;
                    Guid expected;
                    if (!placedCrateIds.TryGetValue(pos, out expected))
                    { recoveryFailure = "native POI crate has no durable placement expectation"; continue; }
                    if (expected != marker.PlacementId) continue;
                    placedCrateIds[pos] = marker.PlacementId;
                    placedCrateFeatures[pos] = loot;
                    if (!crates.Contains(pos)) crates.Add(pos);
                }
            }
        return allLoaded;
    }
    public string CustodyFailure(EntityPlayerLocal player)
    {
        if (!expectationsLoaded) return expectationFailure ?? "expected crate ledger has not been recovered";
        if (recoveryFailure != null) return recoveryFailure;
        if (player?.world == null) return "storage world is unavailable";
        if (!ReferenceEquals(player.world, custodyWorld) || GameManager.Instance == null
            || !ReferenceEquals(GameManager.Instance.World, custodyWorld)
            || !ReferenceEquals(custodyWorld.GetPrimaryPlayer(), player)) return "storage custody belongs to another world or player";
        foreach (Vector3i position in crates)
            if (PlacedLoot(player, position) == null)
                return "remembered storage crate identity is unresolved at " + position;
        return null;
    }
    private static IEnumerator CraftCrate(EntityPlayerLocal p, Action<string> log, Func<bool> canAct, Func<bool> canCleanup = null)
    {
        if (!canAct() || RebirthGameBridgeNeeds.CountItem(p, CrateName) > 0) yield break;
        if (canCleanup == null || canCleanup()) yield return RebirthGameBridgeUi.CloseMenus();
        if (!canAct()) yield break;
        foreach (var action in RebirthGameBridgeInput.FindByKey("Tab")) RebirthGameBridgeInput.HoldFrames(action, 3);
        yield return new WaitForSeconds(.7f);
        if (!canAct()) { if (canCleanup == null || canCleanup()) yield return RebirthGameBridgeUi.CloseMenus(); yield break; }
        ItemValue value = ItemClass.GetItem(CrateName, true);
        string search = value != null && value.ItemClass != null ? value.ItemClass.GetLocalizedItemName() : CrateName;
        string searchId = "rebirthCraftingRecipeSearch";
        bool searched = RebirthGameBridgeUi.TypeInput("crafting", searchId, search);
        if (!searched)
        {
            searchId = "searchInput";
            searched = RebirthGameBridgeUi.TypeInput("crafting", searchId, search);
        }
        if (searched)
        {
            yield return new WaitForSeconds(.5f);
            Vector2 pt;
            if (canAct() && RebirthGameBridgeUi.TryFindRecipe("crafting", CrateName, out pt))
            {
                // ClickAt itself yields while moving the cursor. Admission must be checked
                // after that movement, then again after the recipe-selection UI delay.
                bool clicked = false;
                yield return RebirthGameBridgeUi.ClickAt(pt, () => { clicked = canAct(); return clicked; });
                yield return new WaitForSeconds(.2f);
                if (clicked && canAct())
                {
                    foreach (var action in RebirthGameBridgeInput.FindByKey("W")) RebirthGameBridgeInput.HoldFrames(action, 3);
                    float deadline = Time.realtimeSinceStartup + 20f;
                    while (Time.realtimeSinceStartup < deadline && RebirthGameBridgeNeeds.CountItem(p, CrateName) == 0)
                    {
                        if (!canAct()) break;
                        yield return null;
                    }
                }
            }
            if (canAct()) RebirthGameBridgeUi.TypeInput("crafting", searchId, "");
        }
        if (canCleanup == null || canCleanup()) yield return RebirthGameBridgeUi.CloseMenus();
        log(RebirthGameBridgeNeeds.CountItem(p, CrateName) > 0 ? "crafted storage crate through backpack crafting"
            : "crate crafting unavailable or incomplete; materials and recipe access required");
    }
    private static IEnumerator CraftCrateOwned(EntityPlayerLocal p, Action<string> log, Func<bool> canAct, Func<bool> canCleanup, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!canAct() || RebirthGameBridgeNeeds.CountItem(p, CrateName) > 0) yield break;
        if (canCleanup == null || canCleanup()) yield return RebirthGameBridgeUi.CloseMenus(scope);
        if (!canAct()) yield break;
        foreach (var action in RebirthGameBridgeInput.FindByKey("Tab")) if (!scope.TryHoldFrames(action, 3)) yield break;
        yield return new WaitForSeconds(.7f);
        if (!canAct()) { if (canCleanup == null || canCleanup()) yield return RebirthGameBridgeUi.CloseMenus(scope); yield break; }
        ItemValue value = ItemClass.GetItem(CrateName, true);
        string search = value != null && value.ItemClass != null ? value.ItemClass.GetLocalizedItemName() : CrateName;
        string searchId = "rebirthCraftingRecipeSearch";
        bool searched = RebirthGameBridgeUi.TypeInput("crafting", searchId, search, scope);
        if (!searched)
        {
            searchId = "searchInput";
            searched = RebirthGameBridgeUi.TypeInput("crafting", searchId, search, scope);
        }
        if (searched)
        {
            yield return new WaitForSeconds(.5f);
            Vector2 pt;
            if (canAct() && RebirthGameBridgeUi.TryFindRecipe("crafting", CrateName, out pt))
            {
                // ClickAt itself yields while moving the cursor. Admission must be checked
                // after that movement, then again after the recipe-selection UI delay.
                bool clicked = false;
                yield return RebirthGameBridgeUi.ClickAt(pt, scope, () => { clicked = canAct(); return clicked; });
                yield return new WaitForSeconds(.2f);
                if (clicked && canAct())
                {
                    foreach (var action in RebirthGameBridgeInput.FindByKey("W")) if (!scope.TryHoldFrames(action, 3)) yield break;
                    float deadline = Time.realtimeSinceStartup + 20f;
                    while (Time.realtimeSinceStartup < deadline && RebirthGameBridgeNeeds.CountItem(p, CrateName) == 0)
                    {
                        if (!canAct()) break;
                        yield return null;
                    }
                }
            }
            if (canAct()) RebirthGameBridgeUi.TypeInput("crafting", searchId, "", scope);
        }
        if (canCleanup == null || canCleanup()) yield return RebirthGameBridgeUi.CloseMenus(scope);
        log(RebirthGameBridgeNeeds.CountItem(p, CrateName) > 0 ? "crafted storage crate through backpack crafting"
            : "crate crafting unavailable or incomplete; materials and recipe access required");
    }
    public static bool CanAccept(TEFeatureStorage loot, ItemStack stack)
    {
        if (loot == null || loot.ItemGrid == null || loot.ItemGrid.items == null || stack == null || stack.IsEmpty()) return false;
        // Shift-click can merge before using an empty cell. Reject the whole destination if
        // any eligible merge would discard full metadata/equipment custody.
        foreach(ItemStack cell in loot.ItemGrid.items)
            if(cell!=null&&!cell.IsEmpty()&&cell.CanStackWith(stack,true))
            {
                try {if(ConservationKey(cell.itemValue)!=ConservationKey(stack.itemValue))return false;}
                catch {return false;}
            }
        foreach (ItemStack cell in loot.ItemGrid.items)
            if (cell == null || cell.IsEmpty() || cell.CanStackWith(stack, true)) return true;
        return false;
    }

    private static TEFeatureStorage Loot(EntityPlayerLocal p, Vector3i pos)
    {
        BlockValue block=p.world.GetBlock(pos);
        if(block.Block==null||block.Block.GetBlockName()!=CrateName)return null;
        TileEntity te = p.world.GetTileEntity(pos);
        TEFeatureStorage loot;
        return te != null && te.TryGetSelfOrFeature(out loot) ? loot : null;
    }

    private TEFeatureStorage PlacedLoot(EntityPlayerLocal player, Vector3i position)
    {
        if (player?.world == null || !ReferenceEquals(player.world, custodyWorld)) return null;
        TEFeatureStorage current = Loot(player, position);

        Guid expected;
        if (placedCrateIds.TryGetValue(position, out expected))
        {
            if (!expectationsLoaded || !publishedPlacementIds.Contains(expected)) return null;
            var marker = RebirthPoiCrateIdentity.Resolve(player, position);
            return current != null && marker != null && marker.PlacementId == expected
                && marker.MatchesPoi(boundPoi) && marker.HasLocalOwner(player) ? current : null;
        }
        return null; // An original session feature without durable binding cannot admit cargo.
    }

    private static bool IsNewPlacementWitness(EntityPlayerLocal player, Vector3i position,
        TEFeatureStorage expected, TEFeatureRebirthPoiCrateIdentity marker)
    {
        if (expected == null || !ReferenceEquals(expected, Loot(player, position))) return false;
        // A marker may arrive later on a remote client, but only on this exact placed tile.
        if (marker == null) return RebirthPoiCrateIdentity.Resolve(player, position) == null;
        return ReferenceEquals(marker, RebirthPoiCrateIdentity.Resolve(player, position))
            && ReferenceEquals(marker.Parent, expected.Parent) && marker.HasLocalOwner(player);
    }
    private static bool IsDepositTarget(EntityPlayerLocal player,Vector3i position,
        TEFeatureStorage expected,TEFeatureStorage opened)
        => expected!=null&&ReferenceEquals(expected,opened)&&ReferenceEquals(expected,Loot(player,position));

    private static bool IsPlacementSite(EntityPlayerLocal player, Vector3i site, Vector3 entrance)
    {
        return player?.world != null && Math.Abs(site.y - entrance.y) <= 2f
            && !RebirthGameBridgeEntry.Roofed(player, site.ToVector3())
            && player.world.GetBlock(site).isair
            && player.world.GetBlock(site.x, site.y + 1, site.z).isair
            && !player.world.GetBlock(site.x, site.y - 1, site.z).isair;
    }

    private static bool HasReadyPlacementItem(EntityPlayerLocal player, int slot)
    {
        if (player == null || player.IsDead() || player.inventory == null
            || slot < 0 || slot >= RebirthGameBridgeNeeds.ToolbeltSize(player)
            || player.inventory.holdingItemIdx != slot) return false;
        try
        {
            ItemStack selected = player.inventory.GetItem(slot);
            return selected != null && !selected.IsEmpty()
                && selected.itemValue.ItemClass != null
                && selected.itemValue.ItemClass.GetItemName() == CrateName
                && player.inventory.holdingItem != null
                && player.inventory.holdingItem.GetItemName() == CrateName
                && (!player.inventory.Hand.IsSwitching)
                && !player.inventory.IsHoldingItemActionRunning();
        }
        catch { return false; } // Missing hand state cannot authorize a placement input.
    }
    private static bool TryCount(ItemStack[] slots,out long count)
    {
        count=0;if(slots==null)return false;
        foreach(var stack in slots)
        {
            if(stack==null)continue;
            if(stack.count<0){count=0;return false;}
            if(!stack.IsEmpty())count+=stack.count;
        }
        return true;
    }

    private static Dictionary<string, long> CombinedCounts(ItemStack[] backpack, ItemStack[] crate)
    {
        try
        {
            var counts = new Dictionary<string, long>(StringComparer.Ordinal);
            AddCounts(counts, backpack);
            AddCounts(counts, crate);
            return counts;
        }
        catch { return null; } // An unverifiable transfer must not be reported as conserved.
    }

    private static void AddCounts(Dictionary<string, long> counts, ItemStack[] slots)
    {
        if (slots == null) throw new InvalidDataException("Storage inventory unavailable.");
        foreach (var stack in slots)
        {
            if (stack != null && stack.count < 0) throw new InvalidDataException("Invalid storage stack count.");
            if (stack == null || stack.IsEmpty()) continue;
            string key = ConservationKey(stack.itemValue);
            long count;
            counts.TryGetValue(key, out count);
            counts[key] = checked(count + stack.count);
        }
    }

    private static string ConservationKey(ItemValue item)
    {
        // Native stacking merges ordinary same-type supplies, potentially keeping
        // only one stack's seed. Equipment must retain its exact quality/mods/data.
        if (item.ItemClass != null && item.ItemClass.MaxCount > 1
            && item.Quality == 0 && item.UseTimes == 0f && item.Meta == 0 && item.Flags == 0
            && item.SelectedAmmoTypeIndex == 0 && item.TextureFullArray.IsDefault
            && (item.Stats == null || item.Stats.Length == 0)
            && (item.Metadata == null || item.Metadata.Count == 0)
            && (item.modifications == null || item.modifications.Length == 0)
            && (item.cosmeticMods == null || item.cosmeticMods.Length == 0)) return "type:" + item.type;
        using (var stream = new MemoryStream())
        using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            writer.SetBaseStream(stream);
            ItemValue.Write(item, writer);
            writer.Flush();
            return "value:" + Convert.ToBase64String(stream.ToArray());
        }
    }

    internal static string OwnedItemKey(ItemStack stack) => RestoreKey(stack);
    internal static bool TryOwnedWearReceipt(RebirthGameBridgeInput.OwnedInputScope scope, int slot, ItemStack stack, ItemValue value,
        int count, float beforeUseTimes, string beforeKey, out RebirthGameBridgeInput.OwnedWearReceipt receipt)
    {
        receipt = null;
        EntityPlayerLocal p = scope?.Player;
        if (scope == null || !scope.Admitted || p?.inventory == null || slot < 0 || slot >= RebirthGameBridgeNeeds.ToolbeltSize(p)
            || p.inventory.holdingItemIdx != slot || !ReferenceEquals(p.inventory.GetItem(slot), stack) || stack == null || stack.IsEmpty()
            || value == null || value.type == 0 || value.ItemClass == null || !ReferenceEquals(stack.itemValue, value)
            || count < 1 || stack.count != count || beforeKey == null || float.IsNaN(beforeUseTimes) || float.IsInfinity(beforeUseTimes)
            || float.IsNaN(value.UseTimes) || float.IsInfinity(value.UseTimes) || value.UseTimes < beforeUseTimes) return false;
        try
        {
            ItemStack normalized = stack.Clone();
            if (normalized == null || ReferenceEquals(normalized, stack) || normalized.itemValue == null || ReferenceEquals(normalized.itemValue, value)) return false;
            normalized.itemValue.UseTimes = beforeUseTimes;
            if (normalized.count != count || RestoreKey(normalized) != beforeKey) return false;
            string afterKey = RestoreKey(stack);
            if (afterKey == null || !scope.Admitted) return false;
            receipt = new RebirthGameBridgeInput.OwnedWearReceipt(p, p.world, slot, stack, value, count, beforeKey, afterKey, beforeUseTimes, value.UseTimes);
            return true;
        }
        catch { return false; }
    }

    private static string RestoreKey(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty()) return null;
        try { return ConservationKey(stack.itemValue); }
        catch { return null; }
    }

    private static bool SameCounts(Dictionary<string, long> before, ItemStack[] backpack, ItemStack[] crate)
    {
        var after = CombinedCounts(backpack, crate);
        if (before == null || after == null) return false;
        if (before.Count != after.Count) return false;
        foreach (var pair in before)
        {
            long count;
            if (!after.TryGetValue(pair.Key, out count) || count != pair.Value) return false;
        }
        return true;
    }

    private bool HasCargo(EntityPlayerLocal p)
    {
        ItemStack[] slots = p?.bag?.ItemGrid.items;
        if(slots==null){if(Failure==null)Failure="backpack inventory unavailable";return false;}
        for (int i = 0; i < slots.Length; i++)
            if (IsCargo(p, i, slots[i])) return true;
        return false;
    }

    private bool IsCargo(EntityPlayerLocal p, int index, ItemStack stack)
    {
        PackedBoolArray locks = p.bag.LockedSlots;
        return (locks == null || index >= locks.Length || !locks[index])
            && stack != null && !stack.IsEmpty() && !RetainSupply(stack)
            && stack.itemValue.ItemClass.GetItemName() != CrateName;
    }

    private bool RetainSupply(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty()) return false;
        string key = RestoreKey(stack);
        if (key == null)
        { if (Failure == null) Failure = "could not fingerprint retained storage supply"; return true; }
        return retainedSupplyKeys.Contains(key);
    }

    private void RememberDisplacedSupplies(ItemStack[] before, EntityPlayerLocal player)
    {
        ItemStack[] after = player.inventory.ItemGrid.items;
        int count = Math.Min(before.Length, RebirthGameBridgeNeeds.ToolbeltSize(player));
        for (int i = 0; i < count; i++)
        {
            ItemStack previous = before[i];
            if (previous == null || previous.IsEmpty() || previous.itemValue.ItemClass.GetItemName() == CrateName) continue;
            string key = RestoreKey(previous);
            if (key == null)
            { Failure = "could not fingerprint displaced toolbelt supply"; return; }
            if (i >= after.Length || RestoreKey(after[i]) != key)
                retainedSupplyKeys.Add(key);
        }
    }
    private bool FitsCargo(EntityPlayerLocal p, TEFeatureStorage loot)
    {
        ItemStack[] slots = p?.bag?.ItemGrid.items;
        if(slots==null){if(Failure==null)Failure="backpack inventory unavailable";return false;}
        for (int i = 0; i < slots.Length; i++)
            if (IsCargo(p, i, slots[i]) && CanAccept(loot, slots[i])) return true;
        return false;
    }

    private static string CompletionFailure(string failure,bool dead,bool cargoRemaining)
    {
        if(failure!=null)return failure;
        if(dead)return "death interrupted storage deposit";
        return cargoRemaining?"cargo remains after storage deposit":null;
    }

    private bool OwnedCustodyCurrent(EntityPlayerLocal player)
    {
        if (!expectationsLoaded || recoveryFailure != null || player?.world == null || custodyWorld == null
            || !ReferenceEquals(player.world, custodyWorld) || GameManager.Instance == null
            || !ReferenceEquals(GameManager.Instance.World, custodyWorld) || !ReferenceEquals(custodyWorld.GetPrimaryPlayer(), player)) return false;
        foreach (Vector3i position in crates)
        {
            Guid expectedId;
            bool knownId = placedCrateIds.TryGetValue(position, out expectedId);
            if (knownId && publishedPlacementIds.Contains(expectedId))
            { if (PlacedLoot(player, position) == null) return false; continue; }
            TEFeatureStorage original; TileEntityComposite parent;
            if (!placedCrateFeatures.TryGetValue(position, out original) || !placementParents.TryGetValue(position, out parent)
                || !ReferenceEquals(original?.Parent, parent)) return false;
            var marker = RebirthPoiCrateIdentity.Resolve(player, position);
            if (!IsNewPlacementWitness(player, position, original, marker)) return false;
            TEFeatureRebirthPoiCrateIdentity remembered;
            if (placementMarkers.TryGetValue(position, out remembered) && !ReferenceEquals(remembered, marker)) return false;
            if (marker != null)
            {
                placementMarkers[position] = marker;
                if (knownId && marker.PlacementId != expectedId) return false;
                if (marker.PlacementId != Guid.Empty)
                {
                    if (!marker.IsUnbound && !marker.MatchesPoi(boundPoi)) return false;
                    if (!knownId) placedCrateIds[position] = marker.PlacementId;
                }
            }
            else if (knownId) return false;
        }
        return true;
    }
    private bool ThreatInterrupt(EntityPlayerLocal player, float radius, string failure)
    {
        if (player == null || player.IsDead() || custodyWorld == null || !ReferenceEquals(player.world, custodyWorld)
            || GameManager.Instance == null || !ReferenceEquals(GameManager.Instance.World, custodyWorld)
            || !ReferenceEquals(custodyWorld.GetPrimaryPlayer(), player)
            || RebirthGameBridgeCombat.AwakeThreats(player, radius, false).Count == 0) return false;
        RetryAfterThreat = true;
        Failure = failure;
        return true;
    }
    private string TripContextFailure(EntityPlayerLocal player, PrefabInstance poi, World expectedWorld,
        int expectedPoiId, Vector3i expectedOrigin, Vector3i expectedSize)
    {
        if (player == null || player.IsDead() || expectedWorld == null
            || !ReferenceEquals(player.world, expectedWorld) || !ReferenceEquals(custodyWorld, expectedWorld)
            || GameManager.Instance == null || !ReferenceEquals(GameManager.Instance.World, expectedWorld)
            || !ReferenceEquals(expectedWorld.GetPrimaryPlayer(), player))
            return "storage trip player or world changed";
        if (poi == null || !ReferenceEquals(boundPoi, poi) || poi.id != expectedPoiId
            || !poi.boundingBoxPosition.Equals(expectedOrigin) || !poi.boundingBoxSize.Equals(expectedSize))
            return "storage trip POI binding changed";
        return null;
    }
    public IEnumerator Deposit(EntityPlayerLocal p, PrefabInstance pi, Action<string> log, Action<bool> done)
    {
        RetryAfterThreat = false;
        World tripWorld = p?.world;
        int tripPoiId = pi != null ? pi.id : -1;
        Vector3i tripOrigin = pi != null ? pi.boundingBoxPosition : default(Vector3i);
        Vector3i tripSize = pi != null ? pi.boundingBoxSize : default(Vector3i);
        Func<bool> sameContext = () =>
        {
            string changed = TripContextFailure(p, pi, tripWorld, tripPoiId, tripOrigin, tripSize);
            if (changed == null) return true;
            RetryAfterThreat = false;
            if (Failure == null) Failure = changed;
            return false;
        };
        yield return RefreshExpectations(p, pi);
        bool recoveryLoaded = Recover(p, pi);
        Failure = CustodyFailure(p);
        if (Failure != null) { log(Failure); done(false); yield break; }
        if (!recoveryLoaded) { Failure = "exterior storage recovery chunks are not loaded"; log(Failure); done(false); yield break; }
        if (!sameContext()) { done(false); yield break; }
        int previous = p.inventory.holdingItemIdx;
        ItemStack previousStack = previous >= 0 && previous < RebirthGameBridgeNeeds.ToolbeltSize(p)
            ? p.inventory.GetItem(previous) : null;
        string previousKey = RestoreKey(previousStack);
        Func<ItemStack, bool> restoreMatch = stack => previousKey != null && RestoreKey(stack) == previousKey;
        string previousName = previousStack == null || previousStack.IsEmpty()
            ? null : previousStack.itemValue.ItemClass.GetItemName();
        using (var input = new RebirthGameBridgeInput.OwnedInputScope(p, () => sameContext()
            && !ThreatInterrupt(p, 10f, "threat interrupted owned storage input") && OwnedCustodyCurrent(p)))
        {
        Func<bool> guardedContext = () =>
        {
            if (!sameContext()) return false;
            if (input.Admitted)
            {
                string afterKey;
                if (input.TryConsumeWear(previousStack, previous, previousKey, out afterKey))
                {
                    if (retainedSupplyKeys.Remove(previousKey)) retainedSupplyKeys.Add(afterKey);
                    previousKey = afterKey;
                }
                if (input.Admitted) return true;
            }
            if (input.Failure != "input admission refused") RetryAfterThreat = false;
            if (input.CursorCustodyPending) { RetryAfterThreat = false; Failure = "native cursor item custody remains unresolved"; }
            else if (Failure == null) Failure = input.Failure ?? "owned storage input interrupted";
            return false;
        };
        if (RebirthGameBridgeUi.HeldItemName() != null) input.MarkCursorCustodyPending();
        if (!guardedContext()) { done(false); yield break; }
        yield return RebirthGameBridgeUi.CloseMenus(input);
        if (!guardedContext()) { done(false); yield break; }
        if (!anchor.HasValue)
        {
            var entrances = RebirthGameBridgeEntry.FindEntrances(p, pi);
            foreach (var entrance in entrances)
                if (entrance.Roofed && !RebirthGameBridgeEntry.Roofed(p, entrance.Outside + Vector3.up))
                { anchor = entrance.Outside; break; }
            if (!anchor.HasValue) { Failure = "no verified exterior entrance for storage"; done(false); yield break; }
        }
        try
        {
            // Each pass must either move cargo or consume a real carried crate. Bound failures.
            for (int pass = 0; pass < 32 && HasCargo(p) && !p.IsDead(); pass++)
            {
                if (Failure == null) Failure = CustodyFailure(p);
                if (Failure != null) break;
                if (p.IsDead()) { Failure = "death interrupted storage deposit"; break; }
                    if (ThreatInterrupt(p, 14f, "threat near storage route")) break;
                Vector3i? chosen = null;
                foreach (Vector3i pos in crates)
                    if (FitsCargo(p, PlacedLoot(p, pos))) { chosen = pos; break; }
                if (!chosen.HasValue)
                {
                    // Existing crates have no compatible room for remaining cargo.
                    using (var crafting = new RebirthGameBridgeInput.OwnedInputScope(p, () => input.Admitted, input))
                    {
                    try
                    {
                    yield return RebirthGameBridgeUi.RunGuarded(CraftCrateOwned(p, log, () =>
                    {
                        if (Failure != null || !guardedContext()) return false;
                        if (p.IsDead() || !ReferenceEquals(p.world, custodyWorld))
                        { Failure = "storage crafting context changed"; return false; }
                        if (p.IsDead()) { Failure = "death interrupted storage deposit"; return false; }
                    if (ThreatInterrupt(p, 14f, "threat interrupted crate crafting")) return false;
                        Failure = CustodyFailure(p);
                        return Failure == null;
                    }, guardedContext, crafting), crafting);
                    }
                    finally
                    {
                        if (crafting.CursorCustodyPending) { input.MarkCursorCustodyPending(); RetryAfterThreat = false; Failure = "native cursor item custody remains unresolved"; }
                        else if (crafting.Failure != null) { input.Refuse(crafting.Failure); if (Failure == null) Failure = crafting.Failure; if (crafting.Failure != "input admission refused") RetryAfterThreat = false; }
                    }
                    }
                    if (Failure != null) break;
                    // Crafting can yield for seconds and may itself stop for a threat.
                    // Recheck before opening inventory to prepare the placement item.
                    if (p.IsDead()) { Failure = "death interrupted storage deposit"; break; }
                    if (ThreatInterrupt(p, 14f, "threat interrupted crate preparation")) break;
                    ItemStack[] beforePreparation = ItemStack.Clone(p.inventory.ItemGrid.items);
                    yield return RebirthGameBridgeNeeds.Prepare(p, ic => ic.GetItemName() == CrateName,
                        null, "a storage crate", log, input); // null explicitly disables test supply
                    if (!guardedContext()) { done(false); yield break; }
                    RememberDisplacedSupplies(beforePreparation, p);
                    if (Failure != null) break;
                    string name;
                    int slot = RebirthGameBridgeNeeds.ToolbeltSlot(p, ic => ic.GetItemName() == CrateName, out name);
                    if (slot < 0) { Failure = "need a crafted wooden storage crate"; break; }
                    bool equipped = false;
                    yield return RebirthGameBridgeNeeds.Equip(p, slot, CrateName, ok => equipped = ok, input);
                    if (!guardedContext()) { done(false); yield break; }
                    if (!equipped) { Failure = "could not equip storage crate"; break; }
                    Vector3i? candidate = null;
                    for (int radius = 2; radius <= 7 && !candidate.HasValue; radius++)
                        for (int dx = -radius; dx <= radius && !candidate.HasValue; dx++)
                            for (int dz = -radius; dz <= radius; dz++)
                            {
                                if (Math.Abs(dx) != radius && Math.Abs(dz) != radius) continue;
                                int x = Mathf.FloorToInt(anchor.Value.x) + dx, z = Mathf.FloorToInt(anchor.Value.z) + dz;
                                int y = Mathf.FloorToInt(p.world.GetTerrainHeight(x, z)) + 1;
                                var pos = new Vector3i(x, y, z);
                                if (!IsPlacementSite(p, pos, anchor.Value)) continue;
                                candidate = pos; break;
                            }
                    if (!candidate.HasValue) { Failure = "no clear ground beside the entrance"; break; }
                    Vector3i site = candidate.Value;
                    Vector3? stand = RebirthGameBridgePath.StandSpot(p, site);
                    bool routed = false;
                    if (stand.HasValue) yield return RebirthGameBridgePath.GoTo(p, stand.Value, .6f, 30f, log, ok => routed = ok, input);
                    if (!guardedContext()) { done(false); yield break; }
                    if (!routed) { Failure = "storage placement route blocked"; break; }
                    if (p.IsDead()) { Failure = "death interrupted storage deposit"; break; }
                    if (ThreatInterrupt(p, 14f, "threat near crate placement")) break;
                    Vector3 aim = new Vector3(site.x + .5f, site.y - .05f, site.z + .5f);
                    yield return RebirthGameBridgePlayer.TurnToRoutine(p, () => aim, 0f, 0f, 1.5f, input);
                    if (!guardedContext()) { done(false); yield break; }
                    int before = RebirthGameBridgeNeeds.CountItem(p, CrateName);
                    var action = RebirthGameBridgeInput.Find("Secondary");
                    if (action == null) { Failure = "placement action unavailable"; break; }
                    // Travel and camera turns yield; the chosen ground may have changed.
                    // Revalidate immediately before real placement input, preserving carried crates on refusal.
                    if (!IsPlacementSite(p, site, anchor.Value))
                    { Failure = "storage placement ground changed or is no longer exterior"; break; }
                    if (p.IsDead()) { Failure = "death interrupted storage deposit"; break; }
                    if (ThreatInterrupt(p, 14f, "threat interrupted crate placement aim")) break;
                    if (!HasReadyPlacementItem(p, slot))
                    { Failure = "held storage crate changed or hands are busy"; break; }
                    if (!input.TryHoldFrames(action, 2)) { Failure = input.Failure; break; }
                    yield return new WaitForSeconds(.7f);
                    if (!guardedContext()) { done(false); yield break; }
                    BlockValue placed = p.world.GetBlock(site);
                    if (placed.Block == null || placed.Block.GetBlockName() != CrateName
                        || RebirthGameBridgeNeeds.CountItem(p, CrateName) != before - 1)
                    { Failure = "crate placement not verified"; break; }
                    TEFeatureStorage placedFeature = Loot(p, site);
                    if (placedFeature == null)
                    { Failure = "placed crate inventory identity could not be verified"; break; }
                    placedCrateFeatures[site] = placedFeature;
                    placementParents[site] = placedFeature.Parent;
                    if (!crates.Contains(site)) crates.Add(site);
                    float markerUntil = Time.realtimeSinceStartup + 5f;
                    TEFeatureRebirthPoiCrateIdentity marker = RebirthPoiCrateIdentity.Resolve(p, site);
                    if (marker != null) placementMarkers[site] = marker;
                    while ((marker == null || marker.PlacementId == Guid.Empty) && Time.realtimeSinceStartup < markerUntil
                        && !p.IsDead() && RebirthGameBridgeCombat.AwakeThreats(p, 14f, false).Count == 0)
                    {
                        if (!IsNewPlacementWitness(p, site, placedFeature, marker))
                        { Failure = "placed crate changed while awaiting durable identity"; break; }
                        yield return null;
                        if (!guardedContext()) { done(false); yield break; }
                        var currentMarker = RebirthPoiCrateIdentity.Resolve(p, site);
                        if (marker != null && !ReferenceEquals(marker, currentMarker))
                        { Failure = "placed crate marker changed while awaiting durable identity"; break; }
                        marker = currentMarker;
                    }
                    if (Failure != null) break;
                    if (!guardedContext()) { done(false); yield break; }
                    if (marker == null || marker.PlacementId == Guid.Empty)
                    { Failure = "placed crate durable identity is unavailable"; break; }
                    if (!IsNewPlacementWitness(p, site, placedFeature, marker))
                    { Failure = "placed crate changed before durable binding"; break; }
                    if (!marker.IsUnbound)
                    { Failure = "newly placed crate already has a POI binding"; break; }
                    Guid expectedPlacement = marker.PlacementId;
                    placedCrateIds[site] = expectedPlacement;
                    if (!IsNewPlacementWitness(p, site, placedFeature, marker))
                    { Failure = "placed crate changed before binding request"; break; }
                    if (!RebirthPoiCrateIdentity.RequestBinding(p, pi, site, expectedPlacement))
                    { Failure = "placed crate POI binding was refused"; break; }
                    markerUntil = Time.realtimeSinceStartup + 5f;
                    while (!marker.MatchesPoi(pi) && Time.realtimeSinceStartup < markerUntil
                        && !p.IsDead() && RebirthGameBridgeCombat.AwakeThreats(p, 14f, false).Count == 0)
                    {
                        yield return null;
                        if (!guardedContext()) { done(false); yield break; }
                        if (!IsNewPlacementWitness(p, site, placedFeature, marker)
                            || marker.PlacementId != expectedPlacement)
                        { Failure = "placed crate identity changed while awaiting binding"; break; }
                    }
                    if (Failure != null) break;
                    if (!guardedContext()) { done(false); yield break; }
                    if (!IsNewPlacementWitness(p, site, placedFeature, marker)
                        || marker.PlacementId != expectedPlacement || !marker.MatchesPoi(pi))
                    { Failure = "placed crate POI binding was not acknowledged"; break; }
                    publishedPlacementIds.Add(expectedPlacement);
                    chosen = site;
                    log("placed storage crate " + crates.Count + " at " + site);
                }
                Vector3? storageStand = RebirthGameBridgePath.StandSpot(p, chosen.Value);
                bool reachedStorage = false;
                if (storageStand.HasValue)
                    yield return RebirthGameBridgePath.GoTo(p, storageStand.Value, .6f, 30f, log, ok => reachedStorage = ok, input);
                if (!guardedContext()) { done(false); yield break; }
                if (!reachedStorage) { Failure = "route back to storage crate blocked"; break; }
                if (p.IsDead()) { Failure = "death interrupted storage deposit"; break; }
                    if (ThreatInterrupt(p, 14f, "threat near storage crate")) break;
                if(PlacedLoot(p,chosen.Value)==null){Failure="storage crate was removed or replaced";break;}
                yield return RebirthGameBridgePlayer.ApproachAndActivate(p, chosen.Value, log, 1.2f, 20f, input);
                yield return new WaitForSeconds(.7f);
                if (!guardedContext()) { done(false); yield break; }
                var ui = LocalPlayerUI.GetUIForPlayer(p);
                TEFeatureStorage target = PlacedLoot(p, chosen.Value);
                if (target == null || ui == null || ui.xui.LootContainer != target)
                { Failure = "storage crate did not open"; break; }
                int moved = 0, scrolls = 0;
                // Wheel magnitude is deliberately ignored by the UI: one event moves
                // one row, with at most one event per frame. Walk back to the top
                // before searching so a previous visit's scroll cannot hide cargo.
                var scrollInventory=p.bag?.ItemGrid.items;
                if(scrollInventory==null){Failure="backpack inventory unavailable";yield return RebirthGameBridgeUi.CloseMenus(input);break;}
                int rows = (scrollInventory.Length + 12) / 13;
                for (int row = 0; row < rows; row++)
                {
                    if (!guardedContext()) { done(false); yield break; }
                    if (p.IsDead()) { Failure = "death interrupted storage deposit"; break; }
                    if (ThreatInterrupt(p, 10f, "threat interrupted storage view preparation")) break;
                    if(!IsDepositTarget(p,chosen.Value,target,ui.xui.LootContainer))
                    {Failure="storage container changed while preparing deposit view";break;}
                    RebirthGameBridgeUi.ScrollContextBackpack(1f);
                    yield return null;
                }
                if(Failure!=null){yield return RebirthGameBridgeUi.CloseMenus(input);break;}
                yield return new WaitForSeconds(.2f);
                while (HasCargo(p) && FitsCargo(p, target) && scrolls < 24 && moved < 200)
                {
                    if (!guardedContext()) { done(false); yield break; }
                    if (p.IsDead()) { Failure = "death interrupted storage deposit"; break; }
                    if (ThreatInterrupt(p, 10f, "threat interrupted storage deposit")) break;
                    if(!IsDepositTarget(p,chosen.Value,target,ui.xui.LootContainer))
                    {Failure="storage container changed during deposit";break;}
                    Vector2 pt;
                    if (!RebirthGameBridgeUi.TryFindDepositSlot(target, out pt, RetainSupply))
                    { if (Failure != null) break; if (!guardedContext() || !RebirthGameBridgeUi.ScrollContextBackpack(-.1f, input)) { if (Failure == null) Failure = "owned backpack scroll refused"; break; } scrolls++; yield return new WaitForSeconds(.1f); continue; }
                    if (Failure != null) break;
                    long beforeBag,beforeCrate;
                    if(!TryCount(p.bag?.ItemGrid.items,out beforeBag)||!TryCount(target.ItemGrid.items,out beforeCrate))
                    {Failure="storage inventory unavailable or invalid before deposit";break;}
                    var beforeContents = CombinedCounts(p.bag.ItemGrid.items, target.ItemGrid.items);
                    if (beforeContents == null)
                    { Failure = "could not fingerprint storage contents before deposit"; break; }
                    using (var transfer = new RebirthGameBridgeInput.OwnedInputScope(p, () => input.Admitted, input))
                    {
                        if (!transfer.TrySetShift(true)) { Failure = transfer.Failure; break; }
                        try
                        {
                        yield return RebirthGameBridgeUi.ClickAt(pt,transfer,()=>
                        {
                            if (!guardedContext()) return false;
                            if (p.IsDead()) { Failure = "death interrupted storage deposit"; return false; }
                    if (ThreatInterrupt(p, 10f, "threat interrupted storage click")) return false;
                            if(!IsDepositTarget(p,chosen.Value,target,ui.xui.LootContainer))
                            {Failure="storage container changed before click";return false;}
                            Vector2 currentPoint;
                            if(!RebirthGameBridgeUi.TryFindDepositSlot(target,out currentPoint,RetainSupply)
                                ||Failure!=null||Mathf.Abs(currentPoint.x-pt.x)>.5f||Mathf.Abs(currentPoint.y-pt.y)>.5f)
                            {if(Failure==null)Failure="backpack cargo view changed before click";return false;}
                            if(!SameCounts(beforeContents,p.bag.ItemGrid.items,target.ItemGrid.items))
                            {Failure="inventory contents changed before storage click";return false;}
                            return true;
                        });
                        }
                        finally
                        {
                            if (transfer.CursorCustodyPending) { input.MarkCursorCustodyPending(); RetryAfterThreat = false; Failure = "native cursor item custody remains unresolved"; }
                            else if (transfer.Failure != null) { input.Refuse(transfer.Failure); if (Failure == null) Failure = transfer.Failure; if (transfer.Failure != "input admission refused") RetryAfterThreat = false; }
                        }
                    }
                    if(Failure!=null)break;
                    yield return new WaitForSeconds(.15f);
                    if (!guardedContext()) { done(false); yield break; }
                    if(!IsDepositTarget(p,chosen.Value,target,ui.xui.LootContainer))
                    {Failure="storage container changed while transferring";break;}
                    long afterBag,afterCrate;
                    if(!TryCount(p.bag?.ItemGrid.items,out afterBag)||!TryCount(target.ItemGrid.items,out afterCrate))
                    {Failure="storage inventory unavailable or invalid after deposit";break;}
                    long removed = beforeBag - afterBag;
                    if (removed <= 0 || afterCrate - beforeCrate != removed
                        || !SameCounts(beforeContents, p.bag.ItemGrid.items, target.ItemGrid.items))
                    { Failure = "deposit failed conservation check"; break; }
                    moved++; scrolls = 0;
                }
                if (!guardedContext()) { done(false); yield break; }
                yield return RebirthGameBridgeUi.CloseMenus(input);
                if (!guardedContext()) { done(false); yield break; }
                if (Failure != null) break;
                if (moved == 0 || (scrolls >= 24 && FitsCargo(p, target)))
                { Failure = "remaining cargo not reachable in backpack view"; break; }
                log("stored " + moved + " backpack stacks; retaining displaced toolbelt supplies and locked backpack slots");
            }
            if (HasCargo(p) && Failure == null) Failure = "storage trip budget exhausted";
        }
        finally { /* Input cleanup belongs to the exact owned scope below. */ }
        if (!guardedContext()) { done(false); yield break; }
        yield return RebirthGameBridgeUi.CloseMenus(input);
        if (!guardedContext()) { done(false); yield break; }
        if (!p.IsDead() && previousName != null)
        {
            // Preparing a crate may have moved the selected supply into the bag.
            // Restore by item identity, not the old slot now occupied by the crate.
            string restoredName;
            int restoredSlot = RebirthGameBridgeNeeds.ToolbeltSlot(p, ic => ic.GetItemName() == previousName, out restoredName, restoreMatch);
            if (restoredSlot < 0 && RebirthGameBridgeCombat.AwakeThreats(p, 10f, false).Count == 0)
            {
                yield return RebirthGameBridgeNeeds.Prepare(p, ic => ic.GetItemName() == previousName,
                    null, "the previously held item", log, input, restoreMatch);
                if (!guardedContext()) { done(false); yield break; }
                restoredSlot = RebirthGameBridgeNeeds.ToolbeltSlot(p, ic => ic.GetItemName() == previousName, out restoredName, restoreMatch);
            }
            // Do not reopen inventory beside an awake threat after an interrupted deposit.
            // Leave recovery to the guard/combat loop, keeping the displaced supply in the bag.
            bool restored = false;
            if (restoredSlot >= 0)
                yield return RebirthGameBridgeNeeds.Equip(p, restoredSlot, previousName, ok => restored = ok && restoreMatch(p.inventory.GetItem(restoredSlot)), input);
            if (!restored && Failure == null) Failure = "could not restore the previously held item";
        }
        else if (!p.IsDead() && previous >= 0)
            yield return RebirthGameBridgeNeeds.Equip(p, previous, null, ok => { }, input);
        if (!guardedContext()) { done(false); yield break; }
        Failure=CompletionFailure(Failure,p.IsDead(),HasCargo(p));
        if (Failure != null) log("storage incomplete: " + Failure);
        done(Failure == null);
        }
    }
}





