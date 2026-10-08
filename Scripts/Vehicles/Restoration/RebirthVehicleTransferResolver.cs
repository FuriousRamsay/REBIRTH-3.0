using System;

// Loaded-world observation only. This is not evidence that a chunk or vehicles.dat
// reached disk, and never authorizes an owner item offer or changes an assembly.
public static class RebirthVehicleTransferResolver
{
    public static bool TryObserve(World world, RebirthVehicleTransferPlan plan, Func<bool> isCurrentSession,
        out RebirthVehicleAssembly snapshot, out Vector3i position, out int entityId)
    {
        snapshot = null; position = default(Vector3i); entityId = -1;
        if (world == null || world.IsRemote() || plan?.Target == null || isCurrentSession == null
            || !isCurrentSession() || !ReferenceEquals(world, GameManager.Instance?.World)) return false;
        RebirthVehicleAssembly indexed;
        Vector3i foundPosition; int foundEntity;
        if (!RebirthVehicleAssemblyIndex.TryLocateTransferTarget(plan.Target, plan.ReadBefore().AssemblyId,
            out foundPosition, out foundEntity, out indexed)) return false;
        RebirthVehicleAssembly live = null;
        if (plan.Target.Carrier == RebirthVehicleAssemblyCarrier.RepairableBlock)
            live = (world.GetTileEntity(foundPosition) as TileEntityRepairableVehicleRebirth)?.Assembly;
        else
        {
            var entity = world.GetEntity(foundEntity) as EntityVehicle;
            if (entity == null || !RebirthVehicleAssemblyIndex.TryGetEntity(entity, out live)) return false;
        }
        if (live == null || RebirthVehicleAssemblySerializer.ToBase64(live)
            != RebirthVehicleAssemblySerializer.ToBase64(indexed)) return false;
        if (!isCurrentSession() || !ReferenceEquals(world, GameManager.Instance?.World)) return false;
        snapshot = indexed; position = foundPosition; entityId = foundEntity;
        return true;
    }
}