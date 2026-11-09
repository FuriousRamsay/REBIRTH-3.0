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
    private static readonly HashSet<string> Settled = new HashSet<string>(StringComparer.Ordinal);

    internal static void Update()
    {
        if (Time.realtimeSinceStartup < nextCheck) return;
        nextCheck = Time.realtimeSinceStartup + IntervalSeconds;
        var world = GameManager.Instance?.World;
        if (world == null || world.IsRemote() || world.IsEditor()) return;
        if (!ReferenceEquals(world, boundWorld)) { boundWorld = world; Settled.Clear(); }
        if (!RebirthXmlOptionConditionResolver.IsCharacterProgression("Rebirth")) return;
        var prefabs = GameManager.Instance.GetDynamicPrefabDecorator()?.allPrefabs;
        if (prefabs == null) return;
        for (int i = 0; i < prefabs.Count; i++)
        {
            var instance = prefabs[i];
            Spot[] spots;
            if (instance?.prefab == null || !Spots.TryGetValue(instance.prefab.PrefabName ?? string.Empty, out spots)) continue;
            for (int s = 0; s < spots.Length; s++) Place(world, instance, spots[s]);
        }
    }

    private static Vector3i ToWorld(PrefabInstance instance, Spot spot)
    {
        int maxX = instance.boundingBoxSize.x - 1, maxZ = instance.boundingBoxSize.z - 1, dx, dz;
        switch (instance.rotation & 3)
        {
            case 1: dx = maxZ - spot.Z; dz = spot.X; break;
            case 2: dx = maxX - spot.X; dz = maxZ - spot.Z; break;
            case 3: dx = spot.Z; dz = maxX - spot.X; break;
            default: dx = spot.X; dz = spot.Z; break;
        }
        return instance.boundingBoxPosition + new Vector3i(dx, spot.Y, dz);
    }

    private static void Place(World world, PrefabInstance instance, Spot spot)
    {
        string key = instance.id + "|" + spot.Block;
        if (Settled.Contains(key)) return;
        Vector3i pos = ToWorld(instance, spot);
        if (world.GetChunkFromWorldPos(pos) == null) return;

        string where = instance.prefab.PrefabName + " " + spot.Block + " local " + spot.X + "," + spot.Y + "," + spot.Z + " world " + pos;
        if (instance.GetPositionRelativeToPoi(pos) != new Vector3i(spot.X, spot.Y, spot.Z))
        { Settled.Add(key); Log.Warning("[REBIRTH TraderStations] Rotation mismatch, skipped " + where); return; }

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

        var clear = new List<Vector3i>();
        for (int i = 0; i < cells.Count; i++)
        {
            BlockValue present = world.GetBlock(cells[i]);
            if (present.isair) continue;
            if (!Clearable(present))
            { Settled.Add(key); Log.Warning("[REBIRTH TraderStations] Spot blocked by " + present.Block?.GetBlockName() + ", skipped " + where); return; }
            Vector3i parent = cells[i];
            if (present.ischild && present.Block?.multiBlockPos != null) parent = present.Block.multiBlockPos.GetParentPos(cells[i], present);
            if (!clear.Contains(parent)) clear.Add(parent);
        }
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3i below = cells[i] + Vector3i.down;
            if (cells.Contains(below)) continue;
            if (world.GetBlock(below).isair)
            { Settled.Add(key); Log.Warning("[REBIRTH TraderStations] No floor, skipped " + where); return; }
        }

        // Clear first and place on a later pass, so multiblock children are gone before the new parent.
        if (clear.Count > 0)
        {
            for (int i = 0; i < clear.Count; i++) world.SetBlockRPC(clear[i], BlockValue.Air);
            return;
        }
        world.SetBlockRPC(pos, value);
        Settled.Add(key);
        RebirthDebug.Log("trader", "[REBIRTH TraderStations] Placed " + where);
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
