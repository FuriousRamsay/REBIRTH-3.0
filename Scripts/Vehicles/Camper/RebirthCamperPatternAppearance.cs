using System;
using System.Runtime.CompilerServices;
using System.Linq;
using UnityEngine;

// Appearance follows the authoritative native metadata; selecting never edits dye.
internal static class RebirthCamperPatternAppearance
{
    private static readonly int Enabled=Shader.PropertyToID("_PatternEnabled");
    private static readonly ConditionalWeakTable<EntityVehicle,State> States=new ConditionalWeakTable<EntityVehicle,State>();
    private sealed class State
    {
        public Transform Model;
        public Renderer[] Panels=new Renderer[0];
        public float NextBind;
        public string Last;
        public readonly MaterialPropertyBlock Properties=new MaterialPropertyBlock();
    }
    internal static void Apply(EntityVehicle entity)
    {
        if(entity==null || entity.ModelTransform==null)return;
        RebirthCamperPickupPaint.Apply(entity);
        State state;
        if(States.TryGetValue(entity,out state) && state.Model!=entity.ModelTransform){States.Remove(entity);state=null;}
        if(state==null)
        {
            var model=entity.ModelTransform;
            state=new State{Model=model};
            States.Add(entity,state);
        }
        string pattern=RebirthCamperPattern.Current(entity);
        bool force=!string.Equals(state.Last,pattern,StringComparison.Ordinal);
        if(Time.realtimeSinceStartup>=state.NextBind)
        {
            // Camper body, rear door and distance LODs were split/reparented.
            // The shader explicitly declares paint-pattern eligibility; floors,
            // wheels, plates and brown steel use Standard and are not selected.
            var visual=state.Model.Find("M/VisualBody");
            if(visual==null)return;
            state.Panels=visual.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>m!=null&&m.HasProperty(Enabled))).ToArray();
            state.NextBind=Time.realtimeSinceStartup+1f;force=true;
        }
        float enabled=string.Equals(pattern,"disco",StringComparison.Ordinal)?1f:0f;
        foreach(var panel in state.Panels)Set(panel,state.Properties,enabled,force);
        state.Last=pattern;
    }
    private static void Set(Renderer renderer,MaterialPropertyBlock properties,float enabled,bool force)
    {
        if(renderer==null)return;
        properties.Clear();renderer.GetPropertyBlock(properties);
        if(force||properties.GetFloat(Enabled)!=enabled){properties.SetFloat(Enabled,enabled);renderer.SetPropertyBlock(properties);}
        var materials=renderer.sharedMaterials;
        for(int i=0;i<materials.Length;i++)
        {
            if(materials[i]==null||!materials[i].HasProperty(Enabled))continue;
            properties.Clear();renderer.GetPropertyBlock(properties,i);
            // Existing indexed dye/finish blocks override the renderer block.
            if(properties.isEmpty)continue;
            if(force||properties.GetFloat(Enabled)!=enabled){properties.SetFloat(Enabled,enabled);renderer.SetPropertyBlock(properties,i);}
        }
    }
}
