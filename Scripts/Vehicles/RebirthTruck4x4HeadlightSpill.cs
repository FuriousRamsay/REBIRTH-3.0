using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Keep the stock 4x4's 77-degree spot from washing the vegetation beside the road.
// VPHeadlight.Update restores native range each frame, so this runs afterward.
[HarmonyPatch(typeof(VPHeadlight), "Update")]
internal static class RebirthTruck4x4HeadlightSpill
{
    private const float MaxRange = 32f;
    private const float MaxAngle = 34f;

    private sealed class State
    {
        internal Transform Model;
        internal Light[] Spots;
    }

    private static readonly ConditionalWeakTable<VPHeadlight, State> States =
        new ConditionalWeakTable<VPHeadlight, State>();

    [HarmonyPriority(Priority.High)]
    private static void Postfix(VPHeadlight __instance)
    {
        if (GameManager.IsDedicatedServer || __instance?.vehicle == null) return;

        string vehicleName = __instance.vehicle.GetName();
        if (!string.Equals(vehicleName, "vehicleTruck4x4", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(vehicleName, "repairableVehicle4x4Rebirth", StringComparison.OrdinalIgnoreCase)) return;

        Transform model = __instance.vehicle.entity?.ModelTransform;
        if (model == null) return;

        State state;
        if (States.TryGetValue(__instance, out state) && state.Model != model)
        {
            States.Remove(__instance);
            state = null;
        }
        if (state == null)
        {
            Transform headlightRoot = model.Find("M/HeadLights/Lights");
            state = new State
            {
                Model = model,
                Spots = headlightRoot != null
                    ? headlightRoot.GetComponentsInChildren<Light>(true)
                    : new Light[0]
            };
            States.Add(__instance, state);
        }

        foreach (Light spot in state.Spots)
        {
            if (spot == null || spot.type != LightType.Spot) continue;
            spot.range = Mathf.Min(spot.range, MaxRange);
            spot.spotAngle = Mathf.Min(spot.spotAngle, MaxAngle);
        }
    }
}
