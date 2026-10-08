using HarmonyLib;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Replaces vanilla LightProcessor static Block.lightOpacity lookups with Advanced Farming's
/// per-instance opacity bridge for composite/model-entity covers. Ordinary blocks continue to
/// use vanilla opacity and vanilla light math.
/// </summary>
public static class Harmony_RebirthAdvancedFarmingLightProcessorPatch
{
    private const int ActiveFarmLightInfluenceRadius = 32;
    private const int ActiveFarmLightInfluenceVerticalRadius = 48;

    /// <summary>
    /// Diagnostic only. When true, BLOCK light processing falls through to vanilla
    /// LightProcessor while SUN remains patched. Dynamic covers can leak powered
    /// light while enabled, so this must not be left on for normal gameplay.
    /// </summary>
    public static bool BypassBlockLightPatch;

    public static bool Prefix_RefreshSunlightAtLocalPos(LightProcessor __instance, Chunk c, int x, int z, bool _isSpread)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return true;

        Vector3i columnWorld = new Vector3i(
            c.GetBlockWorldPosX(x), 0, c.GetBlockWorldPosZ(z));
        if (!AdvancedFarmingActiveAreaRegistry.IsHorizontalAreaActive(
            columnWorld, ActiveFarmLightInfluenceRadius))
        {
            return true;
        }

        bool sampling = AdvancedFarmingPerfSnapshotService.SamplingEnabled;
        long startTicks = sampling ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

        bool hardBlockedColumn = false;
        int currentSun = 15;
        IChunkAccess chunkAccess = __instance.m_World;

        for (int y = byte.MaxValue; y >= 0; --y)
        {
            int lightOpacity = AdvancedFarmingDynamicLightOpacityService.GetVerticalSunOpacity(chunkAccess, c, x, y, z);
            if (lightOpacity == byte.MaxValue)
                hardBlockedColumn = true;

            byte oldLight = c.GetLight(x, y, z, Chunk.LIGHT_TYPE.SUN);
            byte newLight;
            if (!hardBlockedColumn)
            {
                currentSun = Utils.FastMax(0, currentSun - lightOpacity);
                newLight = (byte)currentSun;
                if (oldLight != newLight)
                    c.SetLight(x, y, z, newLight, Chunk.LIGHT_TYPE.SUN);
            }
            else
            {
                if (oldLight != 0)
                    c.SetLight(x, y, z, 0, Chunk.LIGHT_TYPE.SUN);
                newLight = 0;
                if (_isSpread)
                    newLight = RefreshLightAtLocalPosPatched(__instance, c, x, y, z, Chunk.LIGHT_TYPE.SUN);
            }

            if (_isSpread)
            {
                if (oldLight > newLight)
                    __instance.UnspreadLight(c, x, y, z, oldLight, Chunk.LIGHT_TYPE.SUN);
                else if (oldLight < newLight)
                    __instance.SpreadLight(c, x, y, z, newLight, Chunk.LIGHT_TYPE.SUN, true);
            }
        }

        if (sampling)
            AdvancedFarmingPerfSnapshotService.RecordLightProcessorRefreshSunlight(System.Diagnostics.Stopwatch.GetTimestamp() - startTicks);

        return false;
    }

    public static bool Prefix_RefreshLightAtLocalPos(LightProcessor __instance, ref byte __result, Chunk c, int x, int y, int z, Chunk.LIGHT_TYPE type)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return true;

        Vector3i worldPos = new Vector3i(
            c.GetBlockWorldPosX(x), y, c.GetBlockWorldPosZ(z));
        if (!AdvancedFarmingActiveAreaRegistry.IsAreaActive(
            worldPos, ActiveFarmLightInfluenceRadius, ActiveFarmLightInfluenceVerticalRadius))
        {
            return true;
        }

        if (BypassBlockLightPatch && type == Chunk.LIGHT_TYPE.BLOCK)
            return true;

        bool sampling = AdvancedFarmingPerfSnapshotService.SamplingEnabled;
        long startTicks = sampling ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

        __result = RefreshLightAtLocalPosPatched(__instance, c, x, y, z, type);

        if (sampling)
            AdvancedFarmingPerfSnapshotService.RecordLightProcessorRefreshLocal(type, System.Diagnostics.Stopwatch.GetTimestamp() - startTicks);

        return false;
    }

    public static bool Prefix_SpreadLightRecursive(LightProcessor __instance, Chunk _chunk, int _blockX, int _blockY, int _blockZ, byte _lightValue, int depth, Chunk.LIGHT_TYPE type, bool bSetAtStarterPos = true)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return true;

        Vector3i worldPos = new Vector3i(
            _chunk.GetBlockWorldPosX(_blockX), _blockY, _chunk.GetBlockWorldPosZ(_blockZ));
        if (!AdvancedFarmingActiveAreaRegistry.IsAreaActive(
            worldPos, ActiveFarmLightInfluenceRadius, ActiveFarmLightInfluenceVerticalRadius))
        {
            return true;
        }

        if (BypassBlockLightPatch && type == Chunk.LIGHT_TYPE.BLOCK)
            return true;

        bool sampling = AdvancedFarmingPerfSnapshotService.SamplingEnabled;
        long startTicks = sampling ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

        SpreadLightRecursivePatched(__instance, _chunk, _blockX, _blockY, _blockZ, _lightValue, depth, type, bSetAtStarterPos);

        if (sampling)
            AdvancedFarmingPerfSnapshotService.RecordLightProcessorSpread(type, System.Diagnostics.Stopwatch.GetTimestamp() - startTicks);

        return false;
    }

    public static bool Prefix_UnspreadLightRecursive(LightProcessor __instance, Chunk _chunk, int _blockX, int _blockY, int _blockZ, byte _lightValue, int depth, Chunk.LIGHT_TYPE type, List<Vector3i> brightSpots)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return true;

        Vector3i worldPos = new Vector3i(
            _chunk.GetBlockWorldPosX(_blockX), _blockY, _chunk.GetBlockWorldPosZ(_blockZ));
        if (!AdvancedFarmingActiveAreaRegistry.IsAreaActive(
            worldPos, ActiveFarmLightInfluenceRadius, ActiveFarmLightInfluenceVerticalRadius))
        {
            return true;
        }

        if (BypassBlockLightPatch && type == Chunk.LIGHT_TYPE.BLOCK)
            return true;

        bool sampling = AdvancedFarmingPerfSnapshotService.SamplingEnabled;
        long startTicks = sampling ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

        UnspreadLightRecursivePatched(__instance, _chunk, _blockX, _blockY, _blockZ, _lightValue, depth, type, brightSpots);

        if (sampling)
            AdvancedFarmingPerfSnapshotService.RecordLightProcessorUnspread(type, System.Diagnostics.Stopwatch.GetTimestamp() - startTicks);

        return false;
    }

    private static byte RefreshLightAtLocalPosPatched(LightProcessor processor, Chunk c, int x, int y, int z, Chunk.LIGHT_TYPE type)
    {
        BlockValue currentValue = c.GetBlock(x, y, z);
        int baseOpacity = currentValue.Block != null ? currentValue.Block.lightOpacity : 0;
        if (baseOpacity == byte.MaxValue)
        {
            c.SetLight(x, y, z, 0, type);
            return 0;
        }

        IChunkAccess chunkAccess = processor.m_World;
        Vector3i currentWorld = new Vector3i(c.GetBlockWorldPosX(x), y, c.GetBlockWorldPosZ(z));
        byte oldSelf = GetLightAt(chunkAccess, currentWorld, type);

        int best = 0;
        for (int i = 0; i < Vector3i.AllDirections.Length; i++)
        {
            Vector3i dir = Vector3i.AllDirections[i];
            Vector3i neighborWorld = new Vector3i(currentWorld.x + dir.x, currentWorld.y + dir.y, currentWorld.z + dir.z);
            byte neighborLight = GetLightAt(chunkAccess, neighborWorld, type);
            if (neighborLight == 0)
                continue;

            BlockValue neighborValue = GetBlockAt(chunkAccess, neighborWorld);
            int opacity = AdvancedFarmingDynamicLightOpacityService.GetLightStepOpacity(chunkAccess, neighborWorld, neighborValue, currentWorld, currentValue);
            int candidate = neighborLight - 1 - opacity;
            if (candidate > best)
                best = candidate;
        }

        if (best < 0)
            best = 0;

        byte intensity = (byte)Utils.FastMax(best, oldSelf);
        c.SetLight(x, y, z, intensity, type);
        return intensity;
    }

    private static void SpreadLightRecursivePatched(LightProcessor processor, Chunk chunk, int blockX, int blockY, int blockZ, byte lightValue, int depth, Chunk.LIGHT_TYPE type, bool setAtStarterPos)
    {
        if (setAtStarterPos)
            chunk.SetLight(blockX, blockY, blockZ, lightValue, type);
        if (lightValue == 0)
            return;

        IChunkAccess chunkAccess = processor.m_World;
        Vector3i fromWorld = new Vector3i(chunk.GetBlockWorldPosX(blockX), blockY, chunk.GetBlockWorldPosZ(blockZ));
        BlockValue fromValue = chunk.GetBlock(blockX, blockY, blockZ);

        for (int i = Vector3i.AllDirections.Length - 1; i >= 0; --i)
        {
            Vector3i dir = Vector3i.AllDirections[i];
            int neighborY = blockY + dir.y;
            if ((uint)neighborY > byte.MaxValue)
                continue;

            int nx = blockX + dir.x;
            int nz = blockZ + dir.z;
            Chunk neighborChunk = chunk;
            if ((uint)nx >= 16U || (uint)nz >= 16U)
            {
                neighborChunk = chunkAccess.GetChunkFromWorldPos(chunk.GetBlockWorldPosX(nx), neighborY, chunk.GetBlockWorldPosZ(nz)) as Chunk;
                if (neighborChunk == null)
                    continue;
                nx = World.toBlockXZ(nx);
                nz = World.toBlockXZ(nz);
            }

            byte existing = neighborChunk.GetLight(nx, neighborY, nz, type);
            if (existing >= 15)
                continue;

            Vector3i toWorld = new Vector3i(neighborChunk.GetBlockWorldPosX(nx), neighborY, neighborChunk.GetBlockWorldPosZ(nz));
            BlockValue toValue = neighborChunk.GetBlock(nx, neighborY, nz);
            int opacity = AdvancedFarmingDynamicLightOpacityService.GetLightStepOpacity(chunkAccess, fromWorld, fromValue, toWorld, toValue);
            int next = lightValue - (opacity != 0 ? opacity : 1);
            if (next < 0)
                next = 0;
            byte nextLight = (byte)next;
            if (existing < nextLight)
                SpreadLightRecursivePatched(processor, neighborChunk, nx, neighborY, nz, nextLight, depth + 1, type, true);
        }
    }

    private static void UnspreadLightRecursivePatched(LightProcessor processor, Chunk chunk, int blockX, int blockY, int blockZ, byte lightValue, int depth, Chunk.LIGHT_TYPE type, List<Vector3i> brightSpots)
    {
        chunk.SetLight(blockX, blockY, blockZ, 0, type);
        IChunkAccess chunkAccess = processor.m_World;
        Vector3i fromWorld = new Vector3i(chunk.GetBlockWorldPosX(blockX), blockY, chunk.GetBlockWorldPosZ(blockZ));
        BlockValue fromValue = chunk.GetBlock(blockX, blockY, blockZ);

        for (int i = 0; i < Vector3i.AllDirections.Length; ++i)
        {
            Vector3i dir = Vector3i.AllDirections[i];
            int neighborY = blockY + dir.y;
            if ((uint)neighborY >= 256U)
                continue;

            int nx = blockX + dir.x;
            int nz = blockZ + dir.z;
            Chunk neighborChunk = chunk;
            if ((uint)nx >= 16U || (uint)nz >= 16U)
            {
                neighborChunk = chunkAccess.GetChunkFromWorldPos(chunk.GetBlockWorldPosX(nx), neighborY, chunk.GetBlockWorldPosZ(nz)) as Chunk;
                if (neighborChunk == null)
                    continue;
                nx = World.toBlockXZ(nx);
                nz = World.toBlockXZ(nz);
            }

            byte neighborLight = neighborChunk.GetLight(nx, neighborY, nz, type);
            if (neighborLight == 0)
                continue;

            Vector3i toWorld = new Vector3i(neighborChunk.GetBlockWorldPosX(nx), neighborY, neighborChunk.GetBlockWorldPosZ(nz));
            BlockValue toValue = neighborChunk.GetBlock(nx, neighborY, nz);
            int opacity = AdvancedFarmingDynamicLightOpacityService.GetLightStepOpacity(chunkAccess, fromWorld, fromValue, toWorld, toValue);
            int next = lightValue - (opacity != 0 ? opacity : 1);
            if (next < 0)
                next = 0;

            if (neighborLight < lightValue && next > 0)
                UnspreadLightRecursivePatched(processor, neighborChunk, nx, neighborY, nz, (byte)next, depth + 1, type, brightSpots);
            else if (neighborLight >= lightValue)
                brightSpots.Add(new Vector3i(neighborChunk.GetBlockWorldPosX(nx), neighborY, neighborChunk.GetBlockWorldPosZ(nz)));
        }
    }

    private static byte GetLightAt(IChunkAccess chunkAccess, Vector3i worldPos, Chunk.LIGHT_TYPE type)
    {
        IChunk chunk = chunkAccess.GetChunkFromWorldPos(worldPos.x, worldPos.y, worldPos.z);
        return chunk != null ? chunk.GetLight(World.toBlockXZ(worldPos.x), World.toBlockY(worldPos.y), World.toBlockXZ(worldPos.z), type) : (byte)0;
    }

    private static BlockValue GetBlockAt(IChunkAccess chunkAccess, Vector3i worldPos)
    {
        if (worldPos.y < 0 || worldPos.y > byte.MaxValue)
            return BlockValue.Air;

        IChunk chunk = chunkAccess.GetChunkFromWorldPos(worldPos.x, worldPos.y, worldPos.z);
        Chunk concreteChunk = chunk as Chunk;
        if (concreteChunk != null)
            return concreteChunk.GetBlock(World.toBlockXZ(worldPos.x), worldPos.y, World.toBlockXZ(worldPos.z));

        IBlockAccess blockAccess = chunkAccess as IBlockAccess;
        return blockAccess != null ? blockAccess.GetBlock(worldPos) : BlockValue.Air;
    }
}
