using System;
using System.Linq;

public static partial class RebirthVehicleAssemblyIndex
{
    // Exact two-carrier observation for the existing conversion's independently
    // saved vehicles/chunk boundary. No inventory or world mutation is granted.
    internal static bool TryLocatePaidConversionPair(World world,RebirthVehicleTransferPlan plan,Func<bool> current,out EntityVehicle vehicle)
    {
        vehicle=null;
        if(plan?.Target?.ConvertsToEntity!=true||world==null||world.IsRemote()||current==null||!current())return false;
        lock(Sync)
        {
            var id=plan.ReadBefore().AssemblyId;var position=new Vector3i(plan.Target.X,plan.Target.Y,plan.Target.Z);
            var blocks=Blocks.Where(pair=>pair.Value?.Assembly?.AssemblyId==id).ToArray();
            var entities=Entities.Where(pair=>pair.Value?.AssemblyId==id).ToArray();
            if(blocks.Length!=1||entities.Length!=1||blocks[0].Key!=position||
                RebirthVehicleAssemblySerializer.ToBase64(entities[0].Value)!=plan.AfterPayload)return false;
            var actual=world.GetEntity(entities[0].Key) as EntityVehicle;
            var tile=world.GetTileEntity(position) as TileEntityRepairableVehicleRebirth;
            if(actual==null||!ReferenceEquals(actual.world,world)||tile==null||!ReferenceEquals(tile,blocks[0].Value)||
                !TryGetEntity(actual,out var native)||RebirthVehicleAssemblySerializer.ToBase64(native)!=plan.AfterPayload||!current())return false;
            var reserved=plan.ReadBefore();RebirthVehicleTransferReservation.TryReserve(reserved,plan.TransactionId,reserved.Revision);
            string source=RebirthVehicleAssemblySerializer.ToBase64(tile.Assembly);
            if(source!=plan.BeforePayload&&source!=plan.AfterPayload&&source!=RebirthVehicleAssemblySerializer.ToBase64(reserved))return false;
            vehicle=actual;return true;
        }
    }
}
