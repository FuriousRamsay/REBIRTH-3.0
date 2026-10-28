using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEngine;

// Appearance follows the authoritative native metadata; selecting never edits dye.
internal static class RebirthCamperPatternAppearance
{
    private static readonly int Enabled=Shader.PropertyToID("_PatternEnabled");
    private static readonly ConditionalWeakTable<EntityVehicle,State> States=new ConditionalWeakTable<EntityVehicle,State>();
    private sealed class State
    {
        public Transform Model;
        public readonly List<Renderer> Panels=new List<Renderer>();
        public readonly List<Renderer> Candidates=new List<Renderer>();
        public readonly List<Material> Materials=new List<Material>();
        public float NextBind;
        public bool Applying;
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
        // Unity/material callbacks must not mutate these scratch lists while
        // an outer observation is consuming them. The next normal update
        // observes any changes made during such a nested callback.
        if(state.Applying)return;
        state.Applying=true;
        try { ApplyCurrent(entity,state); }
        finally { state.Materials.Clear();state.Candidates.Clear();state.Applying=false; }
    }
    private static void ApplyCurrent(EntityVehicle entity,State state)
    {
        string pattern=RebirthCamperPattern.Current(entity);
        bool force=!string.Equals(state.Last,pattern,StringComparison.Ordinal);
        if(Time.realtimeSinceStartup>=state.NextBind)
        {
            // Camper body, rear door and distance LODs were split/reparented.
            // The shader explicitly declares paint-pattern eligibility; floors,
            // wheels, plates and brown steel use Standard and are not selected.
            var visual=state.Model.Find("M/VisualBody");
            if(visual==null)return;
            state.Candidates.Clear();state.Panels.Clear();
            visual.GetComponentsInChildren<Renderer>(true,state.Candidates);
            foreach(var renderer in state.Candidates)
            {
                state.Materials.Clear();renderer.GetSharedMaterials(state.Materials);
                foreach(var material in state.Materials)
                    if(material!=null&&material.HasProperty(Enabled)){state.Panels.Add(renderer);break;}
            }
            state.NextBind=Time.realtimeSinceStartup+1f;force=true;
        }
        float enabled=string.Equals(pattern,"disco",StringComparison.Ordinal)?1f:0f;
        foreach(var panel in state.Panels)
        {
            bool collection=RebirthCamperWrapAppearance.Apply(panel,pattern);
            Set(panel,state.Properties,state.Materials,collection?1f:enabled,force);
        }
        state.Last=pattern;
    }
    private static void Set(Renderer renderer,MaterialPropertyBlock properties,List<Material> materials,float enabled,bool force)
    {
        if(renderer==null)return;
        properties.Clear();renderer.GetPropertyBlock(properties);
        if(force||properties.GetFloat(Enabled)!=enabled){properties.SetFloat(Enabled,enabled);renderer.SetPropertyBlock(properties);}
        materials.Clear();renderer.GetSharedMaterials(materials);
        for(int i=0;i<materials.Count;i++)
        {
            if(materials[i]==null||!materials[i].HasProperty(Enabled))continue;
            properties.Clear();renderer.GetPropertyBlock(properties,i);
            // Existing indexed dye/finish blocks override the renderer block.
            if(properties.isEmpty)continue;
            if(force||properties.GetFloat(Enabled)!=enabled){properties.SetFloat(Enabled,enabled);renderer.SetPropertyBlock(properties,i);}
        }
    }
}
