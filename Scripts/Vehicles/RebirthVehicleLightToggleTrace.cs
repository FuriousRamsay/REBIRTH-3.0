using System;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using UnityEngine;

// One snapshot per headlight-on transition. No per-frame diagnostic output.
[HarmonyPatch(typeof(VPHeadlight), "Update")]
internal static class RebirthVehicleLightToggleTrace
{
    private sealed class State { internal bool WasOn; }
    private static readonly ConditionalWeakTable<VPHeadlight, State> States = new ConditionalWeakTable<VPHeadlight, State>();

    private static void Postfix(VPHeadlight __instance)
    {
        if (GameManager.IsDedicatedServer || __instance == null || __instance.vehicle == null) return;
        State state = States.GetValue(__instance, _ => new State());
        bool on = __instance.IsOn();
        if (!on || state.WasOn == on) { state.WasOn = on; return; }
        state.WasOn = on;

        var entity = __instance.vehicle.entity;
        var root = entity != null ? entity.ModelTransform : null;
        if (root == null) return;
        var lights = root.GetComponentsInChildren<Light>(true);
        var line = new StringBuilder(256);
        line.Append("[VehicleLightToggle] vehicle=").Append(__instance.vehicle.GetName())
            .Append(" entity=").Append(entity.entityId)
            .Append(" on=true lights=").Append(lights.Length);
        Log.Out(line.ToString());
        foreach (var light in lights)
        {
            if (light == null) continue;
            line.Length = 0;
            line.Append("[VehicleLightToggle] path=").Append(Path(light.transform, root))
                .Append(" type=").Append(light.type)
                .Append(" active=").Append(light.gameObject.activeInHierarchy)
                .Append(" enabled=").Append(light.enabled)
                .Append(" intensity=").Append(light.intensity.ToString("F3"))
                .Append(" range=").Append(light.range.ToString("F2"))
                .Append(" angle=").Append(light.spotAngle.ToString("F1"))
                .Append(" shadows=").Append(light.shadows)
                .Append(" cullingMask=").Append(light.cullingMask)
                .Append(" localPosition=").Append(root.InverseTransformPoint(light.transform.position).ToString("F3"))
                .Append(" localForward=").Append(root.InverseTransformDirection(light.transform.forward).ToString("F3"));
            Log.Out(line.ToString());
        }
    }

    private static string Path(Transform child, Transform root)
    {
        string path = child.name;
        while (child.parent != null && child.parent != root)
        {
            child = child.parent;
            path = child.name + "/" + path;
        }
        return path;
    }
}
