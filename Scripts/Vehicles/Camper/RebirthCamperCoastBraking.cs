using System;
using HarmonyLib;

// Native wheel brake torque supplies rolling/engine resistance without altering
// gravity, airborne velocity, active acceleration, or stronger player braking.
internal static class RebirthCamperCoastPolicy
{
    internal static float Brake(float current,float fullBrake,float throttle,float motor)
    {
        if(float.IsNaN(throttle)||float.IsInfinity(throttle)||Math.Abs(throttle)>.001f||motor!=0||
            float.IsNaN(fullBrake)||float.IsInfinity(fullBrake)||fullBrake<=0)return current;
        return Math.Max(current,fullBrake*.005f);
    }
}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.SetWheelsForces))]
internal static class RebirthCamperCoastBraking
{
    private static void Prefix(EntityVehicle __instance,float __0,ref float __2)
    {
        if(__instance==null||__instance.isEntityRemote||!RebirthCamperRadial.IsCamper(__instance)||
            !(__instance.GetAttached(0) is EntityPlayer)||__instance.movementInput==null||__instance.vehicle==null)return;
        __2=RebirthCamperCoastPolicy.Brake(__2,__instance.vehicle.BrakeTorque,__instance.movementInput.moveForward,__0);
        __instance.brakeTorque=__2;
    }
}
