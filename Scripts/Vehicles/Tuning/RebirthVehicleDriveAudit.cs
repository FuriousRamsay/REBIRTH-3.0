#if REBIRTH_DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

// Bounded, read-only capture for owner-driven tuner tests. No background audit.
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.PhysicsFixedUpdate))]
internal static class RebirthVehicleDriveAudit
{
    internal static bool Enabled=true;
    private sealed class Capture {internal float Next;internal int Samples;internal readonly List<string> Rows=new List<string>(45);}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityVehicle,Capture> captures=new System.Runtime.CompilerServices.ConditionalWeakTable<EntityVehicle,Capture>();
    internal static string[] Snapshot(EntityVehicle vehicle)
    {
        return vehicle!=null&&captures.TryGetValue(vehicle,out var capture)?capture.Rows.ToArray():Array.Empty<string>();
    }
    [HarmonyPrefix,HarmonyPriority(Priority.Last)]
    private static void Prefix(EntityVehicle __instance)
    {
        if(!Enabled||__instance==null||__instance.isEntityRemote||
            !RebirthVehicleTuningUi.Sessions.TryGetValue(__instance,out var session)||!session.Valid)return;
        if(!ReferenceEquals(GameManager.Instance?.World?.GetPrimaryPlayer()?.AttachedToEntity,__instance)||
            __instance.movementInput==null||Mathf.Abs(__instance.movementInput.moveForward)<.01f)return;
        var capture=captures.GetValue(__instance,_=>new Capture());
        if(capture.Samples>=45||Time.realtimeSinceStartup<capture.Next)return;
        capture.Next=Time.realtimeSinceStartup+2;capture.Samples++;
        var native=__instance.vehicle;var body=__instance.vehicleRB;
        float speed=body==null?0:body.velocity.magnitude*3.6f;
        // Keep evidence in a bounded memory buffer. Synchronous log writes can
        // themselves stall physics; the authenticated /vehicle snapshot reads it.
        capture.Rows.Add(string.Format(CultureInfo.InvariantCulture,
            "[REBIRTH Vehicle Drive] entity={0} sample={1}/45 speedKmh={2:F2} normal={3:F4} turbo={4:F4} torque={5:F2} turboTorque={6:F2} frameMs={7:F1} turboPressed={8} throttle={9} rampLimit={10:F4} brake={11:F2}",
            __instance.entityId,capture.Samples,speed,native.VelocityMaxForward,native.VelocityMaxTurboForward,
            native.MotorTorqueForward,native.MotorTorqueTurboForward,Time.unscaledDeltaTime*1000,
            __instance.movementInput?.running,__instance.movementInput?.moveForward,__instance.velocityMax,__instance.brakeTorque));
    }
}
#endif
