using HarmonyLib;
using System;

#nullable disable

public static class Harmony_RebirthAdvancedFarmingTileEntityInstantiatePatch
{
    public static bool Prefix(
        PooledBinaryReader _br,
        StreamModeRead _eStreamMode,
        TileEntityType _type,
        Chunk _chunk,
        int[] _blockIdMapping,
        Func<int, int, int, BlockValue> _getBlock,
        ref TileEntity __result)
    {
        TileEntity te = null;

        if (_type == (TileEntityType)RebirthUtilities.TileEntityRebirth.TileEntityFarmPlotRebirth)
            te = new TileEntityFarmPlotRebirth(_chunk);
        else if (_type == (TileEntityType)RebirthUtilities.TileEntityRebirth.TileEntityWaterTankRebirth)
            te = new TileEntityWaterTankRebirth(_chunk);
        else if (_type == (TileEntityType)RebirthUtilities.TileEntityRebirth.TileEntityPlantGrowingRebirth)
            te = new TileEntityPlantGrowingRebirth(_chunk);
        else if (_type == (TileEntityType)RebirthUtilities.TileEntityRebirth.TileEntityRepairableVehicleRebirth)
            te = new TileEntityRepairableVehicleRebirth(_chunk);

        if (te == null)
            return true;

        te.read(_br, _eStreamMode);
        if (AdvancedFarmingRuntimePolicy.Enabled && !(te is TileEntityRepairableVehicleRebirth))
            AdvancedFarmingWaterProviderRegistry.RegisterTileEntity(te);
        __result = te;
        return false;
    }
}
