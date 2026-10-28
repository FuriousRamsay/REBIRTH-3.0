using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Native pose is already in the vehicle save. This transient owner holds only the
// exact Rigidbody constraint additions, never kinematic/collision/sleep lifecycle.
public static class RebirthCamperEnergyImmobility
{
    private sealed class Pin
    {internal World World;internal Rigidbody Body;internal Vector3 Position;internal Quaternion Rotation;internal RigidbodyConstraints Restore;internal bool Applying;}
    private static readonly ConditionalWeakTable<EntityVehicle,Pin> pins=new ConditionalWeakTable<EntityVehicle,Pin>();
    private static bool Live(EntityVehicle vehicle)=>ThreadManager.IsMainThread()&&RebirthCamperEnergyRuntime.IsRegistered
        &&vehicle?.world!=null&&!vehicle.world.IsRemote()&&ReferenceEquals(GameManager.Instance?.World,vehicle.world)
        &&ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)&&RebirthCamperRadial.IsCamper(vehicle);
    public static bool CampClaimReady(EntityVehicle vehicle)
        =>Live(vehicle)&&RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode)&&mode==RebirthCamperEnergyMode.Camp
            &&pins.TryGetValue(vehicle,out var pin)&&ReferenceEquals(pin.World,vehicle.world)&&ReferenceEquals(pin.Body,vehicle.vehicleRB)
            &&pin.Body!=null&&pin.Body.constraints==RigidbodyConstraints.FreezeAll
            &&(vehicle.position-pin.Position).sqrMagnitude<=0.000001f
            &&(pin.Body.position+Origin.position-pin.Position).sqrMagnitude<=0.000001f
            &&Quaternion.Angle(vehicle.qrotation,pin.Rotation)<=0.001f;
    internal static bool Apply(EntityVehicle vehicle,RebirthCamperEnergyMode mode)
    {
        if(!Live(vehicle))return false;
        if(mode==RebirthCamperEnergyMode.Travel){Release(vehicle);return true;}
        var body=vehicle.vehicleRB;if(body==null)return false;
        if(!pins.TryGetValue(vehicle,out var pin))
        {
            pin=new Pin {World=vehicle.world,Body=body,Position=vehicle.position,Rotation=vehicle.qrotation,Restore=body.constraints};
            pins.Add(vehicle,pin);
        }
        if(!ReferenceEquals(pin.World,vehicle.world)||!ReferenceEquals(pin.Body,body)){Release(vehicle);return false;}
        // Preserve any independently observed native/mod constraint assignment.
        // Current native EntityVehicle has no constraints writes; it does own
        // isKinematic and that state is deliberately never overwritten here.
        if(body.constraints!=RigidbodyConstraints.FreezeAll)
        {
            pin.Restore=body.constraints;
            body.constraints=RigidbodyConstraints.FreezeAll;
        }
        pin.Applying=true;
        try
        {
            // Setting an unchanged Rigidbody pose still enters native physics.
            // Keep observing on both sides of simulation, but only correct an
            // actual difference. Recompute local position for origin shifts.
            var position=pin.Position-Origin.position;
            if(!body.position.Equals(position))body.position=position;
            if(!body.rotation.Equals(pin.Rotation))body.rotation=pin.Rotation;
            if(!body.isKinematic)
            {
                if(!body.velocity.Equals(Vector3.zero))body.velocity=Vector3.zero;
                if(!body.angularVelocity.Equals(Vector3.zero))body.angularVelocity=Vector3.zero;
            }
            if(vehicle.position!=pin.Position)vehicle.SetPosition(pin.Position,true);
            if(vehicle.qrotation!=pin.Rotation)vehicle.SetRotation(pin.Rotation.eulerAngles);
        }
        finally{pin.Applying=false;}
        return true;
    }
    internal static void ObserveAndApply(EntityVehicle vehicle)
    {
        if(!Live(vehicle)){Release(vehicle);return;}
        if(!RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode)){Release(vehicle);return;}
        Apply(vehicle,mode);
    }
    internal static bool AllowPosition(EntityVehicle vehicle,Vector3 position)
    {
        if(!Live(vehicle))return true;
        if(pins.TryGetValue(vehicle,out var existing)&&existing.Applying)return true;
        if(!RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode)||mode==RebirthCamperEnergyMode.Travel)return true;
        if(!Apply(vehicle,mode)||!pins.TryGetValue(vehicle,out var pin))return false;
        return pin.Applying||position==pin.Position;
    }
    internal static bool AllowRotation(EntityVehicle vehicle,Vector3 rotation)
    {
        if(!Live(vehicle))return true;
        if(!RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode)||mode==RebirthCamperEnergyMode.Travel)return true;
        if(pins.TryGetValue(vehicle,out var existing)&&existing.Applying)return true;
        if(!Apply(vehicle,mode)||!pins.TryGetValue(vehicle,out var pin))return false;
        return Quaternion.Angle(Quaternion.Euler(rotation),pin.Rotation)<=0.001f;
    }
    internal static void Release(EntityVehicle vehicle)
    {
        if(vehicle==null||!pins.TryGetValue(vehicle,out var pin))return;
        pins.Remove(vehicle);
        if(pin.Body!=null&&pin.Body.constraints==RigidbodyConstraints.FreezeAll)pin.Body.constraints=pin.Restore;
    }
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.PhysicsFixedUpdate))]
internal static class RebirthCamperEnergyImmobilityPhysics
{
    private static void Prefix(EntityVehicle __instance)=>RebirthCamperEnergyImmobility.ObserveAndApply(__instance);
    private static void Postfix(EntityVehicle __instance)=>RebirthCamperEnergyImmobility.ObserveAndApply(__instance);
}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.SetPosition))]
internal static class RebirthCamperEnergyImmobilityPosition
{private static bool Prefix(EntityVehicle __instance,Vector3 _pos)=>RebirthCamperEnergyImmobility.AllowPosition(__instance,_pos);}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.SetRotation))]
internal static class RebirthCamperEnergyImmobilityRotation
{private static bool Prefix(EntityVehicle __instance,Vector3 _rot)=>RebirthCamperEnergyImmobility.AllowRotation(__instance,_rot);}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.OnEntityUnload))]
internal static class RebirthCamperEnergyImmobilityUnload
{
    private static void Prefix(EntityVehicle __instance)=>RebirthCamperEnergyImmobility.ObserveAndApply(__instance);
    private static void Postfix(EntityVehicle __instance){RebirthCamperEnergyImmobility.Release(__instance);RebirthCamperEnergyRuntime.PauseClock(__instance);}
}
