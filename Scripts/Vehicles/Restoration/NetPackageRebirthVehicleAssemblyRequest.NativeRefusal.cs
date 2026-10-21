using System;

public sealed partial class NetPackageRebirthVehicleAssemblyRequest
{
    // A positive no-effect path before plan publication, not an inference from
    // an absent reply. This producer cannot run for legacy/direct protocols.
    internal sealed class NativeRefusal
    {
        internal readonly Guid Operation;
        internal readonly RebirthVehicleRequestResult Result;
        private readonly NetPackageRebirthVehicleAssemblyRequest request;
        private readonly EntityPlayer player;
        private readonly RebirthVehicleTransferPlanStore store;
        private readonly Func<bool> current;
        private readonly string snapshot;
        internal NativeRefusal(NetPackageRebirthVehicleAssemblyRequest original,EntityPlayer owner,RebirthVehicleTransferPlanStore plans,Func<bool> scope,RebirthVehicleAssembly before,string message)
        {
            request=original;player=owner;store=plans;current=scope;Operation=original.requestId;snapshot=before==null?null:RebirthVehicleAssemblySerializer.ToBase64(before);
            Result=new RebirthVehicleRequestResult{ActorId=original.userId?.ToString()??string.Empty,Fingerprint=original.BuildFingerprint(),Carrier=original.carrier,Position=original.position,EntityId=original.entityId,
                Snapshot=before?.DeepClone(),Message=message,Outcome=RebirthVehicleRequestOutcome.Rejected};
        }
        internal bool CanPublish()
        {
            try
            {
                if(!current()||request.readVersion<3||request.action==RebirthVehicleAssemblyAction.Read||request.requestId!=Operation||request.BuildFingerprint()!=Result.Fingerprint||Result.ActorId!=(request.userId?.ToString()??string.Empty)||
                    Result.Outcome!=RebirthVehicleRequestOutcome.Rejected||(Result.Snapshot==null?null:RebirthVehicleAssemblySerializer.ToBase64(Result.Snapshot))!=snapshot||Result.Carrier!=request.carrier||Result.EntityId!=request.entityId||Result.Position!=request.position||
                    !Result.Fingerprint.StartsWith("native-owner-plan-v1|",StringComparison.Ordinal)||store.Read(Operation)!=null||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||
                    !RebirthVehicleTransferCoordinator.TryReadNativeReservation(player,Operation,identity.CanonicalId,request.expectedCreationId,Result.Fingerprint,request.ownerCustodyProof,false,current,out _,out _)||
                    !RebirthVehicleTransferCoordinator.HasSavedNeverEnteredOwner(player,Operation,current))return false;
                RebirthVehicleAssembly actual;
                if(request.carrier==RebirthVehicleAssemblyCarrier.VehicleEntity)
                {RebirthVehicleAssemblyIndex.TryGetEntity(player.world.GetEntity(request.entityId) as EntityVehicle,out actual);}
                else actual=(player.world.GetTileEntity(request.position) as TileEntityRepairableVehicleRebirth)?.Assembly;
                return (actual==null?null:RebirthVehicleAssemblySerializer.ToBase64(actual))==snapshot&&current();
            }
            catch{return false;}
        }
    }
}
