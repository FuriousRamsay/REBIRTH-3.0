using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
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
        public Renderer Rear, LegacyRear, Markers;
        public bool LegacyRearMasked, LegacyRearWasEnabled;
        public Light[] RearLights, MarkerLights, Headlights;
        public VPEngine Engine;
        public MaterialPropertyBlock RearProperties, MarkerProperties;
        public float LastRear=-1, LastMarker=-1;
        public readonly List<PickupLamp> Pickup=new List<PickupLamp>();
    }
    internal sealed class PickupLamp { internal Renderer Lens;internal Light Light;internal bool Reverse;internal readonly MaterialPropertyBlock Block=new MaterialPropertyBlock(); }
    private static void Postfix(VPHeadlight __instance)
    {
        if(GameManager.IsDedicatedServer || __instance.vehicle==null || !string.Equals(__instance.vehicle.GetName(),"rebirthCamper80s",StringComparison.OrdinalIgnoreCase))return;
        var entity=__instance.vehicle.entity;
        if(entity==null || entity.ModelTransform==null)return;
        RebirthCamperPatternAppearance.Apply(entity);
        LampState s;
        if(States.TryGetValue(__instance,out s) && s.Model!=entity.ModelTransform){States.Remove(__instance);s=null;}
        if(s==null){s=Bind(entity.ModelTransform,__instance.vehicle);States.Add(__instance,s);}
        // The body module has its own red lenses at the same position as the
        // original camper renderer. Mask the old copy only while the body
        // module is visible, and restore its previous state when it is hidden.
        if(s.LegacyRear!=null)
        {
            bool bodyRearVisible=s.Rear!=null && s.Rear!=s.LegacyRear && s.Rear.gameObject.activeInHierarchy && s.Rear.enabled;
            if(bodyRearVisible)
            {
                if(!s.LegacyRearMasked){s.LegacyRearWasEnabled=s.LegacyRear.enabled;s.LegacyRearMasked=true;}
                if(s.LegacyRear.enabled)s.LegacyRear.enabled=false;
            }
            else if(s.LegacyRearMasked)
            {
                s.LegacyRear.enabled=s.LegacyRearWasEnabled;
                s.LegacyRearMasked=false;
            }
        }
        // Native VPHeadlight.Update resets intensity/range when a light modifier changes.
        // Enforce the authored forward cone after that update without replacing native on/off.
        foreach(var light in s.Headlights)
        {
            if(light==null)continue;
            light.type=LightType.Spot;light.range=Mathf.Min(light.range,32f);light.spotAngle=34f;
            light.intensity=Mathf.Min(light.intensity,1.1f);
            light.transform.rotation=s.Model.rotation*Quaternion.Euler(7f,0f,0f);
        }
        float rear=RebirthCamperLampIntensity.Calculate(__instance.IsBroken(),__instance.vehicle.CurrentIsBreak,__instance.vehicle.CurrentForwardVelocity,__instance.isOn);
        Set(s.Rear,s.RearLights,s.RearProperties,rear,ref s.LastRear,4f,48f);
        foreach(var lamp in s.Pickup)
        {
            if(lamp.Lens==null)continue;
            float value=lamp.Reverse?RebirthCamperLampIntensity.PickupReverse(__instance.IsBroken(),__instance.vehicle.CurrentIsBreak,__instance.vehicle.CurrentForwardVelocity,__instance.isOn)
                :RebirthCamperLampIntensity.PickupRed(__instance.IsBroken(),__instance.vehicle.CurrentIsBreak,__instance.vehicle.CurrentForwardVelocity,__instance.isOn);
            if(!lamp.Lens.gameObject.activeInHierarchy)value=0;
            lamp.Lens.GetPropertyBlock(lamp.Block);
            lamp.Block.SetColor(Emission,(lamp.Reverse?Color.white:Color.red)*value);
            lamp.Lens.SetPropertyBlock(lamp.Block);
            if(lamp.Light!=null)
            {
                lamp.Light.range=lamp.Reverse?RebirthCamperLampIntensity.ReverseBeamRange:4f;
                lamp.Light.spotAngle=lamp.Reverse?RebirthCamperLampIntensity.ReverseBeamAngle:48f;
                lamp.Light.intensity=value*(lamp.Reverse?RebirthCamperLampIntensity.ReverseBeamMultiplier:1f);lamp.Light.enabled=value>0;
            }
        }
        // Actual native engine state, independent of headlight switch, braking and reverse.
        float marker=RebirthCamperLampIntensity.Marker(s.Engine!=null && s.Engine.isRunning);
        Set(s.Markers,s.MarkerLights,s.MarkerProperties,marker,ref s.LastMarker,.65f,100f);
    }
    private static void Set(Renderer lens,Light[] lights,MaterialPropertyBlock properties,float intensity,ref float last,float range,float angle,float beamMultiplier=1f)
    {
        if(lens==null)return;
        if(!lens.gameObject.activeInHierarchy)intensity=0;
        if(intensity==last)return;
        properties.SetColor(Emission,Color.red*intensity);lens.SetPropertyBlock(properties);
        foreach(var light in lights){if(light==null)continue;light.type=LightType.Spot;light.range=range;light.spotAngle=angle;light.intensity=intensity*beamMultiplier;light.enabled=intensity>0;}
        last=intensity;
    }
    private static LampState Bind(Transform model,Vehicle vehicle)
    {
        var s=new LampState{Model=model,Engine=vehicle.FindPart("engine") as VPEngine};
        s.LegacyRear=RendererAt(model,"M/VisualBody/80sCamperTruck/lights/RearRedLenses");
        s.Rear=RendererAt(model,"M/VisualBody/REBIRTHCamperBodyModule/Camper shared lights RearRedLenses")
            ??s.LegacyRear;
        s.Markers=RendererAt(model,"M/VisualBody/80sCamperTruck/lights/BodyMarkerLenses");
        s.RearLights=LightsAt(model,"M/RearLights");s.MarkerLights=LightsAt(model,"M/BodyMarkerLights");
        s.Headlights=LightsAt(model,"M/HeadLights/Lights");
        if(s.Rear!=null){s.RearProperties=new MaterialPropertyBlock();s.Rear.GetPropertyBlock(s.RearProperties);}
        if(s.Markers!=null){s.MarkerProperties=new MaterialPropertyBlock();s.Markers.GetPropertyBlock(s.MarkerProperties);}
        var pickup=model.Find("M/VisualBody/"+RebirthCamperBodyVisibility.PickupBodywork);
        if(pickup!=null&&s.Rear!=null&&s.Rear.sharedMaterial!=null)
        {
            // The added lenses inherited a legacy diffuse-only shader. Reuse
            // the native emissive atlas material; never mutate its shared asset.
            var assets=pickup.GetComponent<RebirthCamperRearLightAssets>();
            if(assets!=null&&assets.Material!=null){s.Pickup.AddRange(assets.Lamps);return s;}
            if(assets==null)assets=pickup.gameObject.AddComponent<RebirthCamperRearLightAssets>();
            assets.Material=new Material(s.Rear.sharedMaterial){name="REBIRTH pickup rear lens emission"};
            assets.Material.EnableKeyword("_EMISSION");
            foreach(var lens in pickup.GetComponentsInChildren<Renderer>(true))
            {
                if(!RebirthCamperLampIntensity.PickupLens(lens.name,out bool reverse))continue;
                lens.sharedMaterial=assets.Material;
                var lamp=new PickupLamp{Lens=lens,Reverse=reverse};
                // One physical beam per segment; flute meshes only emit.
                if(lens.name.StartsWith("Rear lamp lens segment",StringComparison.Ordinal))
                {
                    var beam=new GameObject("REBIRTH pickup rear beam");beam.transform.SetParent(lens.transform,false);
                    var mesh=lens.GetComponent<MeshFilter>();
                    beam.transform.localPosition=mesh!=null&&mesh.sharedMesh!=null?mesh.sharedMesh.bounds.center:Vector3.zero;
                    beam.transform.rotation=model.rotation*Quaternion.Euler(0,180,0);
                    lamp.Light=beam.AddComponent<Light>();lamp.Light.type=LightType.Spot;lamp.Light.range=4f;lamp.Light.spotAngle=48f;
                    lamp.Light.color=reverse?Color.white:Color.red;lamp.Light.enabled=false;
                }
                assets.Lamps.Add(lamp);
            }
            s.Pickup.AddRange(assets.Lamps);
        }
        return s;
    }
    private static Renderer RendererAt(Transform root,string path){var t=root.Find(path);return t==null?null:t.GetComponent<Renderer>();}
    private static Light[] LightsAt(Transform root,string path){var t=root.Find(path);return t==null?new Light[0]:t.GetComponentsInChildren<Light>(true);}
}

internal sealed class RebirthCamperRearLightAssets:MonoBehaviour
{
    internal Material Material;
    internal readonly List<RebirthCamperRearLights.PickupLamp> Lamps=new List<RebirthCamperRearLights.PickupLamp>();
    private void OnDestroy(){if(Material!=null)UnityEngine.Object.Destroy(Material);}
}
