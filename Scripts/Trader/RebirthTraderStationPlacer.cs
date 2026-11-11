using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Adds the shared stations to native trader POIs. Spots are prefab-local, taken from the 2.6
/// custom traders where the 3.0 prefab still has room. A spot that is no longer free is logged
/// and skipped, so a changed trader prefab cannot overwrite anything: re-assess the table then.
/// </summary>
internal static class RebirthTraderStationPlacer
{
    private const string Recycler = "SonjaRecyclerStation";
    private const string Shredder = "WorkbenchShredder001_FR";
    private const string Saw = "WorkbenchCircularMachine001_FR";
    private const string Integration = "ModifierStation";

    private struct Spot
    {
        public string Block; public int X, Y, Z, Rotation;
        public Spot(string block, int x, int y, int z, int rotation) { Block = block; X = x; Y = y; Z = z; Rotation = rotation; }
    }

    private static readonly Dictionary<string, Spot[]> Spots = new Dictionary<string, Spot[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "trader_bob", new[] { new Spot(Integration, 52, 1, 35, 3), new Spot(Saw, 8, 1, 25, 0), new Spot(Shredder, 37, 2, 30, 0), new Spot(Recycler, 47, 1, 40, 2) } },
        { "trader_hugh", new[] { new Spot(Integration, 14, 1, 18, 1), new Spot(Saw, 14, 1, 14, 1), new Spot(Shredder, 16, 1, 13, 0), new Spot(Recycler, 53, 1, 40, 3) } },
        { "trader_jen", new[] { new Spot(Integration, 52, 2, 37, 1), new Spot(Saw, 52, 2, 42, 1), new Spot(Shredder, 52, 2, 44, 1), new Spot(Recycler, 19, 2, 20, 2) } },
        { "trader_joel", new[] { new Spot(Integration, 53, 1, 39, 3), new Spot(Saw, 19, 1, 39, 3), new Spot(Shredder, 17, 1, 36, 3), new Spot(Recycler, 41, 1, 50, 2) } },
        { "trader_rekt", new[] { new Spot(Integration, 39, 1, 16, 3), new Spot(Saw, 41, 1, 34, 1), new Spot(Shredder, 41, 1, 39, 1), new Spot(Recycler, 45, 1, 9, 0) } },
    };

    private const float IntervalSeconds = 5f;
    private static float nextCheck;
    private static World boundWorld;
    private static readonly HashSet<(int InstanceId, string Block)> Settled = new HashSet<(int, string)>();
    private static readonly Dictionary<Prefab, List<Vector3i>> ExcludedStationOffsets = new Dictionary<Prefab, List<Vector3i>>();

    internal static void Update()
    {
        if (Time.realtimeSinceStartup < nextCheck) return;
        nextCheck = Time.realtimeSinceStartup + IntervalSeconds;
        var world = GameManager.Instance?.World;
        if (world == null || world.IsRemote() || world.IsEditor()) return;
        if (!ReferenceEquals(world, boundWorld))
        {
            boundWorld = world;
            Settled.Clear();
            ExcludedStationOffsets.Clear();
        }
        bool rebirthProgression = RebirthXmlOptionConditionResolver.IsCharacterProgression("Rebirth");
        var prefabs = GameManager.Instance.GetDynamicPrefabDecorator()?.allPrefabs;
        if (prefabs == null) return;
        for (int i = 0; i < prefabs.Count; i++)
        {
            var instance = prefabs[i];
            if (instance?.prefab?.bTraderArea != true) continue;
            RemoveExcludedStations(world, instance);
            if (rebirthProgression && Spots.TryGetValue(instance.prefab.PrefabName ?? string.Empty, out var spots))
                for (int s = 0; s < spots.Length; s++) Place(world, instance, spots[s]);
        }
    }

    private static void RemoveExcludedStations(World world, PrefabInstance instance)
    {
        var prefab = instance.prefab;
        if (prefab.blockCells == null) return;
        if (!ExcludedStationOffsets.TryGetValue(prefab, out var offsets))
        {
            offsets = new List<Vector3i>();
            // The decorator stores the rotated prefab. These offsets are already world-aligned.
            var size = instance.boundingBoxSize;
            for (int y = 0; y < size.y; y++)
            for (int z = 0; z < size.z; z++)
            for (int x = 0; x < size.x; x++)
            {
                var authored = prefab.GetBlock(x, y, z);
                if (!authored.ischild && RebirthTraderStationPlaceholders.Excluded(authored))
                    offsets.Add(new Vector3i(x, y, z));
            }
            ExcludedStationOffsets.Add(prefab, offsets);
        }

        foreach (var offset in offsets)
        {
            var pos = instance.boundingBoxPosition + offset;
            if (world.GetChunkFromWorldPos(pos) == null || !world.IsWithinTraderArea(pos)) continue;
            var current = world.GetBlock(pos);
            if (current.isair || current.ischild) continue;
            var authored = prefab.GetBlock(offset.x, offset.y, offset.z);
            if (!RebirthTraderStationPlaceholders.MatchesSaved(authored, current)) continue;
            var tile = world.GetTileEntity(pos);
            if (tile?.IsUserAccessing() == true ||
                tile is TileEntityWorkstation station && (station.IsPlayerPlaced || !station.IsEmpty) ||
                tile is TileEntityComposite composite && composite.PlayerPlaced &&
                composite.ToWorldPos() == pos && ReferenceEquals(composite.TeData?.Block, current.Block)) continue;
            world.SetBlockRPC(pos, BlockValue.Air);
            RebirthDebug.Log("trader", "[REBIRTH TraderStations] Removed " + current.Block?.GetBlockName() + " at " + pos);
        }
    }

    private static Vector3i ToWorld(PrefabInstance instance, Spot spot)
    {
        return instance.GetWorldPositionOfPoiOffset(new Vector3i(spot.X, spot.Y, spot.Z));
    }

    private static void Place(World world, PrefabInstance instance, Spot spot)
    {
        var key = (instance.id, spot.Block);
        if (Settled.Contains(key)) return;
        Vector3i pos = ToWorld(instance, spot);
        if (world.GetChunkFromWorldPos(pos) == null) return;

        string prefabName = instance.prefab.PrefabName;
        if (instance.GetPositionRelativeToPoi(pos) != new Vector3i(spot.X, spot.Y, spot.Z))
        { Settled.Add(key); Log.Warning("[REBIRTH TraderStations] Rotation mismatch, skipped " + DescribeSpot(prefabName, spot, pos)); return; }

        BlockValue value = Block.GetBlockValue(spot.Block);
        if (value.isair || value.Block == null) { Settled.Add(key); return; }
        value.rotation = (byte)spot.Rotation;
        value = value.Block.shape.RotateY(true, value, instance.rotation & 3);

        if (world.GetBlock(pos).type == value.type) { Settled.Add(key); return; }

        var cells = new List<Vector3i>();
        var multi = value.Block.multiBlockPos;
        if (value.Block.isMultiBlock && multi != null)
            for (int i = 0; i < multi.Length; i++) cells.Add(pos + multi.Get(i, value.type, value.rotation));
        else cells.Add(pos);

        for (int i = 0; i < cells.Count; i++) if (world.GetChunkFromWorldPos(cells[i]) == null) return;

        List<Vector3i> clear = null;
        for (int i = 0; i < cells.Count; i++)
        {
            BlockValue present = world.GetBlock(cells[i]);
            if (present.isair) continue;
            if (!Clearable(present))
            { Settled.Add(key); Log.Warning("[REBIRTH TraderStations] Spot blocked by " + present.Block?.GetBlockName() + ", skipped " + DescribeSpot(prefabName, spot, pos)); return; }
            Vector3i parent = cells[i];
            if (present.ischild && present.Block?.multiBlockPos != null) parent = present.Block.multiBlockPos.GetParentPos(cells[i], present);
            clear ??= new List<Vector3i>();
            if (!clear.Contains(parent)) clear.Add(parent);
        }
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3i below = cells[i] + Vector3i.down;
            if (cells.Contains(below)) continue;
            if (world.GetBlock(below).isair)
            { Settled.Add(key); Log.Warning("[REBIRTH TraderStations] No floor, skipped " + DescribeSpot(prefabName, spot, pos)); return; }
        }

        // Clear first and place on a later pass, so multiblock children are gone before the new parent.
        if (clear != null)
        {
            bool ProtectPlayerClutter(Vector3i parent)
            {
                var current = world.GetBlock(parent);
                if (current.isair || current.ischild || current.Block == null ||
                    world.GetTileEntity(parent) is not TileEntityComposite tile || !tile.PlayerPlaced ||
                    tile.ToWorldPos() != parent || !ReferenceEquals(tile.TeData?.Block, current.Block))
                    return false;

                Settled.Add(key);
                Log.Warning("[REBIRTH TraderStations] Spot blocked by player-placed " + current.Block.GetBlockName() +
                    " at " + parent + ", skipped " + DescribeSpot(prefabName, spot, pos));
                return true;
            }

            for (int i = 0; i < clear.Count; i++)
                if (ProtectPlayerClutter(clear[i])) return;
            for (int i = 0; i < clear.Count; i++)
            {
                // A prior native clear can run callbacks before the next target is cleared.
                if (i > 0 && ProtectPlayerClutter(clear[i])) return;
                world.SetBlockRPC(clear[i], BlockValue.Air);
            }
            return;
        }
        world.SetBlockRPC(pos, value);
        Settled.Add(key);
        if (RebirthDebug.On && RebirthDebug.IsOn("trader"))
            RebirthDebug.Log("trader", "[REBIRTH TraderStations] Placed " + DescribeSpot(prefabName, spot, pos));
    }

    private static string DescribeSpot(string prefabName, Spot spot, Vector3i pos)
    {
        return prefabName + " " + spot.Block + " local " + spot.X + "," + spot.Y + "," + spot.Z + " world " + pos;
    }

    // What the 2.6 traders replaced at these spots: vegetation, busted or collapsed stations, loose yard clutter.
    private static bool Clearable(BlockValue value)
    {
        var block = value.Block;
        if (block == null) return false;
        string name = block.GetBlockName() ?? string.Empty;
        return name.IndexOf("Busted", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Collapsed", StringComparison.OrdinalIgnoreCase) >= 0
            || name.StartsWith("pallet", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("cinderBlocks", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("bricksStack", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(":sawhorse", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("treeGrass", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("plantedtreeGrass", StringComparison.OrdinalIgnoreCase)
            || (block.blockMaterial != null && block.blockMaterial.IsPlant);
    }
}

[Preserve]
public sealed class RebirthTraderStationPlacerModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnUpdate));
    }

    private static void OnUpdate(ref ModEvents.SGameUpdateData data)
    {
        RebirthTraderStationPlacer.Update();
    }
}
