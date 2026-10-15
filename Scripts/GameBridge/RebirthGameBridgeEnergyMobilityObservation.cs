using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Native field/getter observation only. No runtime For, physics tick, input,
// attachment, cursor, body setter, source admission or save side effects.
internal static class RebirthGameBridgeEnergyMobilityObservation
{
    internal static JObject Snapshot(EntityVehicle vehicle)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||vehicle==null||world==null||!ReferenceEquals(vehicle.world,world)||
            !ReferenceEquals(world.GetEntity(vehicle.entityId),vehicle))
            return new JObject{["available"]=false,["missingReason"]="current native vehicle unavailable"};
        var local=world.GetPrimaryPlayer();
        var driver=vehicle.GetAttached(0);
        bool localCurrent=local!=null&&ReferenceEquals(local.world,world)&&ReferenceEquals(world.GetEntity(local.entityId),local);
        bool driverCurrent=driver!=null&&ReferenceEquals(driver.world,world)&&ReferenceEquals(world.GetEntity(driver.entityId),driver);
        int count=vehicle.GetAttachMaxCount();var seats=new JArray();
        for(int seat=0;seat<count;seat++)
        {
            var attached=vehicle.GetAttached(seat);
            seats.Add(new JObject{["seat"]=seat,["entityId"]=Id(attached),["isDriverSeat"]=seat==0,
                ["currentWorldEntity"]=attached!=null&&ReferenceEquals(attached.world,world)&&ReferenceEquals(world.GetEntity(attached.entityId),attached),
                ["attachedToEntityId"]=Id(attached?.AttachedToEntity),["reciprocalVehicleAttachment"]=attached!=null&&ReferenceEquals(attached.AttachedToEntity,vehicle)});
        }
        var body=vehicle.vehicleRB;
        JObject physics=body==null?new JObject{["available"]=false,["missingReason"]="native vehicleRB absent or destroyed"}:
            new JObject{["available"]=true,["constraints"]=body.constraints.ToString(),["constraintsValue"]=(int)body.constraints,
                ["isKinematic"]=body.isKinematic,["velocity"]=Vector(body.velocity),["angularVelocity"]=Vector(body.angularVelocity),
                ["localPosition"]=Vector(body.position),["worldPosition"]=Vector(body.position+Origin.position),
                ["rotation"]=Rotation(body.rotation)};
        return new JObject{["available"]=true,["authoritativeWorld"]=!world.IsRemote(),
            ["physicsScope"]=world.IsRemote()?"native local client body; not server physics proof":"native authoritative world body",
            ["vehicleEntityId"]=vehicle.entityId,["position"]=Vector(vehicle.position),["rotation"]=Rotation(vehicle.qrotation),
            ["driverSeat"]=0,["driverSeatAvailable"]=count>0,["driverEntityId"]=Id(driver),["driverCurrentWorldEntity"]=driverCurrent,
            ["driverAttachedToEntityId"]=Id(driver?.AttachedToEntity),
            ["driverReciprocalAttachment"]=driverCurrent&&ReferenceEquals(driver.AttachedToEntity,vehicle),
            ["nativeSeatCount"]=count,["seats"]=seats,
            ["localPlayerAvailable"]=localCurrent,["localPlayerMissingReason"]=localCurrent?null:"current primary local player unavailable",
            ["localPlayerEntityId"]=Id(local),["localPlayerAttachedToEntityId"]=Id(local?.AttachedToEntity),
            ["localPlayerSeatInVehicle"]=localCurrent?new JValue(vehicle.FindAttachSlot(local)):JValue.CreateNull(),
            ["localPlayerIsDriver"]=localCurrent&&driverCurrent&&ReferenceEquals(driver,local)&&ReferenceEquals(local.AttachedToEntity,vehicle),
            ["nativeModalAvailable"]=localCurrent,
            ["nativeModalOpen"]=localCurrent?new JValue(RebirthGameBridgeUi.AnyModalOpen()):JValue.CreateNull(),["rigidbody"]=physics};
    }
    private static JToken Id(Entity entity)=>entity==null?JValue.CreateNull():new JValue(entity.entityId);
    private static JArray Vector(Vector3 value)=>new JArray(value.x,value.y,value.z);
    private static JArray Rotation(Quaternion value)=>new JArray(value.x,value.y,value.z,value.w);
}
