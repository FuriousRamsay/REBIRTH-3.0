using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Native vehicle input/state drives authored rear lamps; no prefab scripts needed.
[HarmonyPatch(typeof(VPHeadlight), "Update")]
internal static class RebirthStandardVehicleRearLights
{
    private sealed class State
    {
        internal Transform Model;
        internal Renderer[] Lenses;
        internal Light[] Lamps;
        internal float Last=-1;
        internal readonly MaterialPropertyBlock Block=new MaterialPropertyBlock();
    }
    private static readonly ConditionalWeakTable<VPHeadlight,State> States=new ConditionalWeakTable<VPHeadlight,State>();
    private static void Postfix(VPHeadlight __instance)
    {
        if(GameManager.IsDedicatedServer || __instance.vehicle==null)return;
        string name=__instance.vehicle.GetName();
        if(!string.Equals(name,"rebirthBuggy",StringComparison.OrdinalIgnoreCase) &&
           !string.Equals(name,"rebirthBike",StringComparison.OrdinalIgnoreCase))return;
        Transform model=__instance.vehicle.entity?.ModelTransform;
        if(model==null)return;
        RebirthStandardVehiclePattern.Apply(__instance.vehicle.entity);
        State state;
        if(States.TryGetValue(__instance,out state) && state.Model!=model){States.Remove(__instance);state=null;}
        if(state==null)
        {
            state=new State {Model=model,
                Lenses=model.Find("M/RearLenses")?.GetComponentsInChildren<Renderer>(true)??new Renderer[0],
                Lamps=model.Find("M/RearLights")?.GetComponentsInChildren<Light>(true)??new Light[0]};
            States.Add(__instance,state);
        }
        float intensity=RebirthCamperLampIntensity.Calculate(__instance.IsBroken(),
            __instance.vehicle.CurrentIsBreak,__instance.vehicle.CurrentForwardVelocity,__instance.isOn);
        if(intensity==state.Last)return;
        foreach(var lens in state.Lenses)
        {
            if(lens==null)continue;
            state.Block.Clear();lens.GetPropertyBlock(state.Block);
            state.Block.SetColor("_EmissionColor",Color.red*intensity);lens.SetPropertyBlock(state.Block);
        }
        foreach(var light in state.Lamps)
        {if(light==null)continue;light.intensity=intensity;light.enabled=intensity>0;}
        state.Last=intensity;
    }
}
