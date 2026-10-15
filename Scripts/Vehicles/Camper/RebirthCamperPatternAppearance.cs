using System;
using System.Runtime.CompilerServices;
using UnityEngine;

// Appearance follows the authoritative native metadata; selecting never edits dye.
internal static class RebirthCamperPatternAppearance
{
    private static readonly int Enabled=Shader.PropertyToID("_PatternEnabled");
    private static readonly ConditionalWeakTable<EntityVehicle,State> States=new ConditionalWeakTable<EntityVehicle,State>();
    private sealed class State
    {
        public Transform Model;
        public Renderer Body,Camper;
        public string Last;
        public readonly MaterialPropertyBlock Properties=new MaterialPropertyBlock();
    }
    internal static void Apply(EntityVehicle entity)
    {
        if(entity==null || entity.ModelTransform==null)return;
        State state;
        if(States.TryGetValue(entity,out state) && state.Model!=entity.ModelTransform){States.Remove(entity);state=null;}
        if(state==null)
        {
            var model=entity.ModelTransform;
            state=new State{Model=model,
                Body=model.Find("M/VisualBody/80sCamperTruck/body")?.GetComponent<Renderer>(),
                Camper=model.Find("M/VisualBody/80sCamperTruck/camper_body")?.GetComponent<Renderer>()};
            States.Add(entity,state);
        }
        string pattern=RebirthCamperPattern.Current(entity);
        if(string.Equals(state.Last,pattern,StringComparison.Ordinal))return;
        float enabled=string.Equals(pattern,"disco",StringComparison.Ordinal)?1f:0f;
        Set(state.Body,state.Properties,enabled);Set(state.Camper,state.Properties,enabled);
        state.Last=pattern;
    }
    private static void Set(Renderer renderer,MaterialPropertyBlock properties,float enabled)
    {
        if(renderer==null || renderer.sharedMaterial==null || !renderer.sharedMaterial.HasProperty(Enabled))return;
        properties.Clear();renderer.GetPropertyBlock(properties);
        properties.SetFloat(Enabled,enabled);renderer.SetPropertyBlock(properties);
    }
}
