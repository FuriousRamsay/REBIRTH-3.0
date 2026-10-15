using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(VPHeadlight), "Update")]
internal static class RebirthCamperRearLights
{
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    private static readonly ConditionalWeakTable<VPHeadlight, LampState> States = new ConditionalWeakTable<VPHeadlight, LampState>();
    private sealed class LampState
    {
        public Transform Model;
        public Renderer Rear, Markers;
        public Light[] RearLights, MarkerLights, Headlights;
        public VPEngine Engine;
        public MaterialPropertyBlock RearProperties, MarkerProperties;
        public float LastRear=-1, LastMarker=-1;
    }
    private static void Postfix(VPHeadlight __instance)
    {
        if(GameManager.IsDedicatedServer || __instance.vehicle==null || !string.Equals(__instance.vehicle.GetName(),"rebirthCamper80s",StringComparison.OrdinalIgnoreCase))return;
        var entity=__instance.vehicle.entity;
        if(entity==null || entity.ModelTransform==null)return;
        RebirthCamperPatternAppearance.Apply(entity);
        LampState s;
        if(States.TryGetValue(__instance,out s) && s.Model!=entity.ModelTransform){States.Remove(__instance);s=null;}
        if(s==null){s=Bind(entity.ModelTransform,__instance.vehicle);States.Add(__instance,s);}
        // Native VPHeadlight.Update resets intensity/range when a light modifier changes.
        // Enforce the authored forward cone after that update without replacing native on/off.
        foreach(var light in s.Headlights)
        {
            if(light==null)continue;
            light.type=LightType.Spot;light.range=Mathf.Min(light.range,32f);light.spotAngle=48f;
            light.intensity=Mathf.Min(light.intensity,1.1f);
            light.transform.rotation=s.Model.rotation*Quaternion.Euler(3f,0f,0f);
        }
        float rear=RebirthCamperLampIntensity.Calculate(__instance.IsBroken(),__instance.vehicle.CurrentIsBreak,__instance.vehicle.CurrentForwardVelocity,__instance.isOn);
        Set(s.Rear,s.RearLights,s.RearProperties,rear,ref s.LastRear,4f,48f);
        // Actual native engine state, independent of headlight switch, braking and reverse.
        float marker=RebirthCamperLampIntensity.Marker(s.Engine!=null && s.Engine.isRunning);
        Set(s.Markers,s.MarkerLights,s.MarkerProperties,marker,ref s.LastMarker,.65f,100f);
    }
    private static void Set(Renderer lens,Light[] lights,MaterialPropertyBlock properties,float intensity,ref float last,float range,float angle)
    {
        if(lens==null || intensity==last)return;
        properties.SetColor(Emission,Color.red*intensity);lens.SetPropertyBlock(properties);
        foreach(var light in lights){if(light==null)continue;light.type=LightType.Spot;light.range=range;light.spotAngle=angle;light.intensity=intensity;light.enabled=intensity>0;}
        last=intensity;
    }
    private static LampState Bind(Transform model,Vehicle vehicle)
    {
        var s=new LampState{Model=model,Engine=vehicle.FindPart("engine") as VPEngine};
        s.Rear=RendererAt(model,"M/VisualBody/80sCamperTruck/lights/RearRedLenses");
        s.Markers=RendererAt(model,"M/VisualBody/80sCamperTruck/lights/BodyMarkerLenses");
        s.RearLights=LightsAt(model,"M/RearLights");s.MarkerLights=LightsAt(model,"M/BodyMarkerLights");
        s.Headlights=LightsAt(model,"M/HeadLights/Lights");
        if(s.Rear!=null){s.RearProperties=new MaterialPropertyBlock();s.Rear.GetPropertyBlock(s.RearProperties);}
        if(s.Markers!=null){s.MarkerProperties=new MaterialPropertyBlock();s.Markers.GetPropertyBlock(s.MarkerProperties);}
        return s;
    }
    private static Renderer RendererAt(Transform root,string path){var t=root.Find(path);return t==null?null:t.GetComponent<Renderer>();}
    private static Light[] LightsAt(Transform root,string path){var t=root.Find(path);return t==null?new Light[0]:t.GetComponentsInChildren<Light>(true);}
}
