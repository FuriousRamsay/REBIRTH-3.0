using System;

// Persistent GUIDs bind module custody; native entity IDs only locate live
// objects. A duplicated GUID must not establish authority for two live trucks.
internal static class RebirthCamperVehicleIdentity
{
    internal static bool IsUniqueCurrent(EntityVehicle vehicle,Guid expected)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||expected==Guid.Empty||vehicle?.world==null||vehicle.world.IsRemote()||
                !ReferenceEquals(GameManager.Instance?.World,vehicle.world)||
                !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)||vehicle.vehicle==null||
                !vehicle.vehicle.GetName().Equals("rebirthCamper80s",StringComparison.OrdinalIgnoreCase)||
                !TryRead(vehicle,out var current)||current!=expected)return false;
            int matches=0;
            foreach(var entity in vehicle.world.Entities.list)
            {
                if(!(entity is EntityVehicle other)||other.vehicle==null||
                    !other.vehicle.GetName().Equals("rebirthCamper80s",StringComparison.OrdinalIgnoreCase))continue;
                if(!TryRead(other,out var identity)||identity!=expected)continue;
                if(!ReferenceEquals(other,vehicle)||++matches!=1)return false;
            }
            return matches==1;
        }
        catch{return false;}
    }
    private static bool TryRead(EntityVehicle vehicle,out Guid identity)
    {
        identity=Guid.Empty;
        return vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity",out string text)&&
            Guid.TryParse(text,out identity)&&identity!=Guid.Empty;
    }
}
