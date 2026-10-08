using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Converts the original 3.0 wood-burning stove block into a campfire-compatible
/// workstation while repairing old TileEntityLight data from existing chunks.
/// Pickup is intentionally disabled in XML until REBIRTH block-pickup behavior is
/// designed separately.
/// </summary>
[Preserve]
public sealed class BlockWoodBurningStoveRebirth : BlockCampfire
{
    public override void OnBlockLoaded(WorldBase world, Vector3i blockPos, BlockValue blockValue)
    {
        if (world != null && !world.IsRemote())
            EnsureWorkstationTileEntity(world, blockPos, blockValue, true);

        base.OnBlockLoaded(world, blockPos, blockValue);
    }

    public override void OnBlockEntityTransformAfterActivated(
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        BlockEntityData blockEntityData)
    {
        // Existing POI/save chunks may still contain the old TileEntityLight, or no
        // tile entity at all. Repair before the base workstation visibility path runs.
        EnsureWorkstationTileEntity(world, blockPos, blockValue, world != null && !world.IsRemote());
        base.OnBlockEntityTransformAfterActivated(world, blockPos, blockValue, blockEntityData);
    }

    private static TileEntityWorkstation EnsureWorkstationTileEntity(
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        bool persistRepair)
    {
        if (world == null || blockValue.ischild)
            return null;

        TileEntity existing = world.GetTileEntity(blockPos);
        TileEntityWorkstation workstation = existing as TileEntityWorkstation;
        if (workstation != null)
            return workstation;

        Chunk chunk = world.GetChunkFromWorldPos(blockPos) as Chunk;
        if (chunk == null)
            return null;

        // The vanilla 3.0 block was BlockLight and POI/save data can therefore carry
        // a TileEntityLight at this position. Remove any incompatible legacy tile
        // entity before installing the workstation tile entity.
        if (existing != null)
        {
            World concreteWorld = world as World;
            if (concreteWorld == null)
                return null;

            chunk.RemoveTileEntity(concreteWorld, existing);
        }

        workstation = new TileEntityWorkstation(chunk);
        workstation.localChunkPos = World.toBlock(blockPos);
        chunk.AddTileEntity(workstation);

        if (persistRepair)
            chunk.isModified = true;

        return workstation;
    }
}
