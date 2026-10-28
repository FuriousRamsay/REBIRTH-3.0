using System.Runtime.CompilerServices;
using UnityEngine;

// Only the new painted infill owns this override. Original truck renderers,
// wheels, plate, bumper and the separate dark load floor retain native materials.
internal static class RebirthCamperPickupPaint
{
    private static readonly int Tint=Shader.PropertyToID("_Color");
    private static readonly ConditionalWeakTable<EntityVehicle,State> States=new ConditionalWeakTable<EntityVehicle,State>();
    private sealed class State
    {
        internal Transform Model;
        internal Renderer[] Panels;
        internal string LastOverride;
        internal Color Color;
        internal readonly MaterialPropertyBlock Properties=new MaterialPropertyBlock();
        internal readonly System.Collections.Generic.List<Material> Materials=new System.Collections.Generic.List<Material>();
    }
    internal static void Apply(EntityVehicle entity)
    {
        if(entity?.vehicle==null||entity.ModelTransform==null||GameManager.IsDedicatedServer)return;
        State state;
        if(States.TryGetValue(entity,out state)&&state.Model!=entity.ModelTransform){States.Remove(entity);state=null;}
        if(state==null)
        {
            var paint=entity.ModelTransform.Find("M/VisualBody/80sCamperTruck/body/"+RebirthCamperBodyVisibility.PickupPaint);
            if(paint==null)return; // Bind only once the actual model has arrived.
            state=new State{Model=entity.ModelTransform,Panels=paint.GetComponentsInChildren<Renderer>(true)};
            States.Add(entity,state);
        }
        // Identical byte-range contract to native Vehicle.SetColors. Do not
        // normalize, gamma-convert, replace materials or write saved metadata.
        string value=entity.vehicle.GetUpdatedItemValue().GetPropertyOverride(Block.PropTintColor,"255,255,255");
        if(state.LastOverride!=value)
        {
            var tint=Block.StringToVector3(value);
            state.Color=new Color(tint.x,tint.y,tint.z,1);state.LastOverride=value;
        }
        foreach(var panel in state.Panels)
        {
            if(panel==null)continue;
            // Observe the live materials every pass, without allocating the
            // sharedMaterials array or retrieving its first element twice.
            state.Materials.Clear();panel.GetSharedMaterials(state.Materials);
            if(state.Materials.Count==0||state.Materials[0]==null||!state.Materials[0].HasProperty(Tint))continue;
            Set(panel,state,-1);
            // An indexed block overrides the renderer block. Preserve all its
            // other values while updating an already-present indexed override.
            if(state.Materials.Count==1)Set(panel,state,0,true);
        }
        state.Materials.Clear();
    }
    private static void Set(Renderer panel,State state,int material,bool existingOnly=false)
    {
        var block=state.Properties;block.Clear();
        if(material<0)panel.GetPropertyBlock(block);else panel.GetPropertyBlock(block,material);
        if(existingOnly&&block.isEmpty)return;
        if(block.GetColor(Tint)==state.Color)return;
        block.SetColor(Tint,state.Color);
        if(material<0)panel.SetPropertyBlock(block);else panel.SetPropertyBlock(block,material);
    }
}
