using System;

// Stable cooking ticket locations. Mobile identity never aliases the terrain
// tile beneath a camper, and an unavailable installation is not an empty queue.
internal static class RebirthCookingStationLocator
{
    internal static string Encode(TileEntityWorkstation station)
    {
        if(station is RebirthCamperStationState mobile)
        {
            var vehicle=mobile.BoundVehicle;
            string text;Guid identity;
            if(vehicle?.vehicle==null || !vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity",out text)
                || !Guid.TryParseExact(text,"N",out identity) || identity==Guid.Empty)
                throw new InvalidOperationException("Mobile cooking station identity unavailable.");
            return new RebirthCamperStationAddress(identity,mobile.InstallationId).ToString();
        }
        if(station==null)throw new InvalidOperationException("Cooking station unavailable.");
        return RebirthCookingHeat.Position(station.ToWorldPos());
    }
    internal static bool IsValid(string encoded)
        =>RebirthCamperStationAddress.TryParse(encoded,out _) || RebirthCookingJobRuntime.TryPosition(encoded,out _);

    internal static TileEntityWorkstation Resolve(World world,string encoded)
    {
        if(world==null || world.IsRemote() || !ThreadManager.IsMainThread())return null;
        if(!RebirthCamperStationAddress.TryParse(encoded,out var address))
            return RebirthCookingJobRuntime.TryPosition(encoded,out var position)?world.GetTileEntity(position) as TileEntityWorkstation:null;
        EntityVehicle found=null;
        foreach(var entity in world.Entities.list)
        {
            if(!(entity is EntityVehicle vehicle) || !RebirthCamperRadial.IsCamper(vehicle))continue;
            string text;
            if(!vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity",out text)
                || text!=address.Vehicle.ToString("N"))continue;
            if(found!=null)return null; // Duplicate identity is ambiguous, never authority.
            found=vehicle;
        }
        return found==null?null:RebirthCamperModuleRuntime.For(found).Stations.Find(address.Installation)?.State;
    }
    internal static void Watch(World world,string encoded,TileEntityWorkstation station,string reason)
    {
        if(RebirthCamperStationAddress.TryParse(encoded,out _))
        {
            var vehicle=(station as RebirthCamperStationState)?.BoundVehicle;
            if(vehicle==null)return;
            var p=vehicle.position;
            RebirthCookingStationTracking.Watch(world,new Vector3i(UnityEngine.Mathf.FloorToInt(p.x),UnityEngine.Mathf.FloorToInt(p.y),UnityEngine.Mathf.FloorToInt(p.z)),reason);
        }
        else if(RebirthCookingJobRuntime.TryPosition(encoded,out var position))
            RebirthCookingStationTracking.Watch(world,position,reason);
    }
}
