using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// v95: vanilla-aligned visual refresh plus MacroAO correction for model/composite door-like covers.
///
/// Root cause: vanilla UpdateLight/UpdateLightOnAllMaterials bakes a SUN-only ambient value into
/// the model shader property (_MacroAO) when the block entity is first registered. If that bake
/// occurs before local SUN has settled after placement/open/close, the door can remain visually
/// bright even when the current gameplay light data is dark. World reload fixes the appearance
/// because UpdateLight runs again after chunk SUN is already settled.
///
/// This service does not toggle GameObjects, renderers, SetBlockEntityRendering, material colors,
/// or block state. It waits until the vanilla block entity has a live transform, clears the
/// Advanced Farming light cache, then calls vanilla UpdateLight.UpdateLighting(1f) so vanilla
/// recomputes its own _MacroAO value from current SUN, then corrects _MacroAO from the actual block-cell SUN/BLOCK values. This is required for ceiling-mounted covers because vanilla UpdateLight samples y+1 as well, which can be full exterior SUN even when the rendered door cell and dark-room side are unlit. v95 also allows refresh for composite doors whose BlockEntityData reports bRenderingOn=false even while the transform and renderers are active, as observed with iron doors.
/// </summary>
public static class AdvancedFarmingDoorVisualRefreshService
{
    // Delay long enough for the local SUN rebuild and the AdvancedFarmingLightService one-second
    // cache window to settle before forcing vanilla UpdateLight to re-sample SUN.
    private const int DeferTicks = 24;
    private const int MaxRetryTicks = 160;

    // Composite doors can report/open from a child block position while the live
    // BlockEntityData/UpdateLight lives on the master cell. Vault doors are the
    // common case: 1x2 and 2x2 model-entity doors can retain stale _MacroAO on
    // the model even after the gameplay SUN cells around the aperture are fixed.
    private const int VisualRefreshNeighborhoodHorizontalRadius = 2;
    private const int VisualRefreshNeighborhoodDown = 1;
    private const int VisualRefreshNeighborhoodUp = 3;

    private struct PendingVisualRefresh
    {
        public int RemainingTicks;
        public int AgeTicks;
    }

    private static readonly Dictionary<Vector3i, PendingVisualRefresh> _pending = new Dictionary<Vector3i, PendingVisualRefresh>(64);
    private static readonly List<Vector3i> _scratchKeys = new List<Vector3i>(64);
    private static readonly List<Vector3i> _scratchReady = new List<Vector3i>(16);

    public static void ClearPendingVisualRefreshes()
    {
        _pending.Clear();
    }

    public static void QueueDoorVisualRefresh(Vector3i worldPos)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        QueueDoorVisualRefreshAt(worldPos);
    }

    public static void QueueDoorVisualRefreshNeighborhood(Vector3i worldPos)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        QueueDoorVisualRefreshAt(worldPos);

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.ChunkCache == null)
            return;

        for (int x = worldPos.x - VisualRefreshNeighborhoodHorizontalRadius; x <= worldPos.x + VisualRefreshNeighborhoodHorizontalRadius; x++)
        {
            for (int y = worldPos.y - VisualRefreshNeighborhoodDown; y <= worldPos.y + VisualRefreshNeighborhoodUp; y++)
            {
                if (y < 0 || y > byte.MaxValue)
                    continue;

                for (int z = worldPos.z - VisualRefreshNeighborhoodHorizontalRadius; z <= worldPos.z + VisualRefreshNeighborhoodHorizontalRadius; z++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    if (pos.Equals(worldPos))
                        continue;

                    if (ShouldQueueNeighborVisualRefresh(world, pos))
                        QueueDoorVisualRefreshAt(pos);
                }
            }
        }
    }

    private static void QueueDoorVisualRefreshAt(Vector3i worldPos)
    {
        PendingVisualRefresh pending;
        pending.RemainingTicks = DeferTicks;
        pending.AgeTicks = 0;
        _pending[worldPos] = pending;
        AdvancedFarmingPerfSnapshotService.RecordVisualRefreshQueued();
    }

    public static void PumpPendingVisualRefreshesForGameUpdate()
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        if (_pending.Count == 0)
            return;

        _scratchKeys.Clear();
        _scratchKeys.AddRange(_pending.Keys);
        _scratchReady.Clear();

        for (int i = 0; i < _scratchKeys.Count; i++)
        {
            Vector3i pos = _scratchKeys[i];
            PendingVisualRefresh pending = _pending[pos];
            pending.AgeTicks++;
            pending.RemainingTicks--;

            if (pending.RemainingTicks <= 0)
            {
                _scratchReady.Add(pos);
            }
            // The ready pass reads the dictionary again. Persist age on attempt ticks too,
            // otherwise every retry loses one update and exceeds MaxRetryTicks.
            _pending[pos] = pending;
        }

        for (int i = 0; i < _scratchReady.Count; i++)
        {
            Vector3i pos = _scratchReady[i];
            PendingVisualRefresh pending;
            if (!_pending.TryGetValue(pos, out pending))
                continue;

            _pending.Remove(pos);
            if (!RefreshDoorVisualsInternal(pos, pending.AgeTicks))
            {
                if (pending.AgeTicks < MaxRetryTicks)
                {
                    pending.RemainingTicks = 2;
                    _pending[pos] = pending;
                }
            }
        }
    }

    public static void RefreshDoorVisualsNow(Vector3i worldPos)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        RefreshDoorVisualsInternal(worldPos, MaxRetryTicks);
    }

    private static bool ShouldQueueNeighborVisualRefresh(World world, Vector3i pos)
    {
        Block block = null;
        try
        {
            BlockValue value = world.GetBlock(pos);
            block = value.Block;
        }
        catch
        {
            block = null;
        }

        if (IsVisualRefreshCoverCandidate(block))
            return true;

        BlockEntityData bed = TryGetBlockEntityData(world, pos);
        if (bed == null || (UnityEngine.Object)bed.transform == null)
            return false;

        try
        {
            return bed.transform.GetComponentInChildren<UpdateLight>(true) != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsVisualRefreshCoverCandidate(Block block)
    {
        if (block == null)
            return false;

        string name = string.Empty;
        try
        {
            name = block.GetBlockName();
        }
        catch
        {
            name = string.Empty;
        }

        if (string.IsNullOrEmpty(name))
            return false;

        string lower = name.ToLowerInvariant();
        return lower.IndexOf("door") >= 0
            || lower.IndexOf("hatch") >= 0
            || lower.IndexOf("shutter") >= 0
            || lower.IndexOf("curtain") >= 0
            || lower.IndexOf("drape") >= 0
            || lower.IndexOf("blind") >= 0;
    }

    private static BlockEntityData TryGetBlockEntityData(World world, Vector3i worldPos)
    {
        if (world == null || world.ChunkCache == null)
            return null;

        Chunk chunk = null;
        try
        {
            chunk = world.ChunkCache.GetChunkFromWorldPos(worldPos.x, worldPos.y, worldPos.z) as Chunk;
        }
        catch
        {
            chunk = null;
        }

        if (chunk == null)
            return null;

        try
        {
            return chunk.GetBlockEntity(worldPos);
        }
        catch
        {
            return null;
        }
    }

    private static bool RefreshDoorVisualsInternal(Vector3i worldPos, int ageTicks)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.ChunkCache == null)
            return true;

        BlockEntityData bed = TryGetBlockEntityData(world, worldPos);

        // Vanilla can legitimately take a few frames to display block entities after placement.
        // Do not require bRenderingOn here. Some composite door families, including iron doors,
        // report bRenderingOn=false even while the transform exists and child renderers are active.
        // Requiring that flag prevented the _MacroAO correction from ever running for those models.
        // Still avoid SetBlockEntityRendering/GameObject toggles; only use the live transform and
        // the same material-value path vanilla block entities already expose.
        if (bed == null || !bed.bHasTransform || (UnityEngine.Object)bed.transform == null)
            return false;

        UpdateLight updateLight = null;
        try
        {
            updateLight = bed.transform.GetComponentInChildren<UpdateLight>(true);
        }
        catch
        {
            updateLight = null;
        }

        if (updateLight == null)
        {
            ApplyCellMacroAo(bed, world, worldPos);
            return true;
        }

        try
        {
            AdvancedFarmingLightService.ClearCache();
            AdvancedFarmingTemperatureService.ClearCache();
        }
        catch
        {
            // Cache invalidation is best-effort; UpdateLight still reads current chunk SUN directly.
        }

        try
        {
            // Let vanilla update its normal state first. This keeps us aligned with the engine path
            // for UpdateLightOnAllMaterials and avoids GameObject/renderer lifecycle mutation.
            updateLight.UpdateLighting(1f);

            // Ceiling/floor-mounted model covers expose the vanilla UpdateLight weakness: it samples
            // max(SUN at cell, SUN at y+1). A closed ceiling door can therefore bake bright _MacroAO
            // forever from the exterior cell above even though the model's own block cell is dark.
            // Correct only the same shader property vanilla UpdateLight owns, using the actual current
            // light at the block-entity cell. Include BLOCK light so torches/etc can still light it.
            ApplyCellMacroAo(bed, world, worldPos);
            return true;
        }
        catch
        {
            try
            {
                // If vanilla UpdateLight throws for a model family, still attempt the direct
                // material-property correction. This is safer than leaving the stale bright value
                // in place and does not mutate render lifecycle state.
                ApplyCellMacroAo(bed, world, worldPos);
                return true;
            }
            catch
            {
                return ageTicks >= MaxRetryTicks;
            }
        }
    }
    private static void ApplyCellMacroAo(BlockEntityData bed, World world, Vector3i worldPos)
    {
        if (bed == null || world == null || world.ChunkCache == null)
            return;

        int sun = 0;
        int block = 0;
        try
        {
            sun = world.ChunkCache.GetLight(worldPos, Chunk.LIGHT_TYPE.SUN);
        }
        catch
        {
            sun = 0;
        }

        try
        {
            block = world.ChunkCache.GetLight(worldPos, Chunk.LIGHT_TYPE.BLOCK);
        }
        catch
        {
            block = 0;
        }

        int light = sun > block ? sun : block;
        light = GetLowestDoorVisualCellLight(world, worldPos, light);
        if (light < 0)
            light = 0;
        else if (light > 15)
            light = 15;

        float macroAo = light / 15f;

        try
        {
            bed.SetMaterialValue("_MacroAO", macroAo);
        }
        catch
        {
            // Best-effort visual correction only. Do not retry aggressively or mutate render state.
        }
    }

    private static int GetLowestDoorVisualCellLight(World world, Vector3i worldPos, int fallbackLight)
    {
        if (world == null || world.ChunkCache == null)
            return fallbackLight;

        int lowest = fallbackLight;
        bool found = false;

        for (int x = worldPos.x - VisualRefreshNeighborhoodHorizontalRadius; x <= worldPos.x + VisualRefreshNeighborhoodHorizontalRadius; x++)
        {
            for (int y = worldPos.y - VisualRefreshNeighborhoodDown; y <= worldPos.y + VisualRefreshNeighborhoodUp; y++)
            {
                if (y < 0 || y > byte.MaxValue)
                    continue;

                for (int z = worldPos.z - VisualRefreshNeighborhoodHorizontalRadius; z <= worldPos.z + VisualRefreshNeighborhoodHorizontalRadius; z++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    Block block = null;
                    try
                    {
                        BlockValue value = world.GetBlock(pos);
                        block = value.Block;
                    }
                    catch
                    {
                        block = null;
                    }

                    if (!IsVisualRefreshCoverCandidate(block))
                        continue;

                    int cellLight = GetCellLight(world, pos);
                    if (!found || cellLight < lowest)
                    {
                        lowest = cellLight;
                        found = true;
                    }
                }
            }
        }

        return found ? lowest : fallbackLight;
    }

    private static int GetCellLight(World world, Vector3i worldPos)
    {
        int sun = 0;
        int block = 0;
        try
        {
            sun = world.ChunkCache.GetLight(worldPos, Chunk.LIGHT_TYPE.SUN);
        }
        catch
        {
            sun = 0;
        }

        try
        {
            block = world.ChunkCache.GetLight(worldPos, Chunk.LIGHT_TYPE.BLOCK);
        }
        catch
        {
            block = 0;
        }

        int light = sun > block ? sun : block;
        if (light < 0)
            return 0;
        if (light > 15)
            return 15;
        return light;
    }

}
