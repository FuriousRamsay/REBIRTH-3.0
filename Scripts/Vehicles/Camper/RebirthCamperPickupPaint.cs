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
            if(panel==null||panel.sharedMaterial==null||!panel.sharedMaterial.HasProperty(Tint))continue;
            Set(panel,state,-1);
            // An indexed block overrides the renderer block. Preserve all its
            // other values while updating an already-present indexed override.
            if(panel.sharedMaterials.Length==1){state.Properties.Clear();panel.GetPropertyBlock(state.Properties,0);if(!state.Properties.isEmpty)Set(panel,state,0);}
        }
    }
    private static void Set(Renderer panel,State state,int material)
    {
        var block=state.Properties;block.Clear();
        if(material<0)panel.GetPropertyBlock(block);else panel.GetPropertyBlock(block,material);
        if(block.GetColor(Tint)==state.Color)return;
        block.SetColor(Tint,state.Color);
        if(material<0)panel.SetPropertyBlock(block);else panel.SetPropertyBlock(block,material);
    }
}
