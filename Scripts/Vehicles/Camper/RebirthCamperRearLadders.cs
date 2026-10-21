using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Adds authored vehicle ladder volumes to the native ladder admission check.
// Native movement still owns climbing, jumping off, gravity and player replication.
internal static class RebirthCamperRearLadders
{
    private sealed class Binding
    {
        internal WeakReference<EntityVehicle> Vehicle;
        internal Transform Model;
        internal BoxCollider[] Volumes;
    }
    private static readonly List<Binding> bindings = new List<Binding>();
    internal static void Bind(EntityVehicle vehicle)
    {
        for (int i = bindings.Count - 1; i >= 0; i--)
            if (!bindings[i].Vehicle.TryGetTarget(out var old) || old == null || old == vehicle || old.world != vehicle.world)
                bindings.RemoveAt(i);
        var volumes = new List<BoxCollider>();
        foreach (var box in vehicle.RootTransform.GetComponentsInChildren<BoxCollider>(true))
            if (box.name == "REBIRTHRearLadderClimbVolume" || box.name == "REBIRTHFrontLadderClimbVolume") volumes.Add(box);
        if (volumes.Count != 0) bindings.Add(new Binding { Vehicle = new WeakReference<EntityVehicle>(vehicle), Model = vehicle.ModelTransform, Volumes = volumes.ToArray() });
    }
    internal static bool Contains(EntityPlayerLocal player)
    {
        if (player == null || player.IsDead() || player.AttachedToEntity != null || player.IsFlyMode.Value || player.RootTransform == null) return false;
        for (int i = bindings.Count - 1; i >= 0; i--)
        {
            var entry = bindings[i];
            if (!entry.Vehicle.TryGetTarget(out var vehicle) || vehicle == null || vehicle.world != player.world || vehicle.ModelTransform != entry.Model
                || !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId), vehicle))
            { bindings.RemoveAt(i); continue; }
            if (vehicle.IsDead() || (vehicle.position - player.position).sqrMagnitude > 100f
                || !RebirthCamperBodyAttachment.HasBody(vehicle)
                || !RebirthCamperEnergyRuntime.TryObservePresentation(vehicle, out var mode, out _, out _, out _)
                || (mode != RebirthCamperEnergyMode.Camp && mode != RebirthCamperEnergyMode.Travel)) continue;
            foreach (var volume in entry.Volumes)
            {
                if (volume == null || !volume.gameObject.activeInHierarchy) continue;
                // Feet must rise into the authored jump-to-reach volume. Its
                // lower edge accounts for hand reach to the raised first rung.
                var feet = volume.transform.InverseTransformPoint(player.RootTransform.position);
                if (new Bounds(volume.center, volume.size).Contains(feet)) return true;
            }
        }
        return false;
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.CalcIfInElevator))]
internal static class RebirthCamperRearLadderAdmission
{
    private static void Postfix(EntityAlive __instance)
    {
        if (!__instance.bInElevator && __instance is EntityPlayerLocal player && RebirthCamperRearLadders.Contains(player))
            __instance.bInElevator = true;
    }
}
