using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

internal static class RebirthStandardVehiclePattern
{
    private sealed class Binding {internal Renderer Renderer;internal int MaterialIndex;}
    private sealed class State
    {
        internal Transform Model;
        internal readonly List<Binding> Paint=new List<Binding>();
        internal bool? Disco;
        internal string Tint;
        internal readonly MaterialPropertyBlock Block=new MaterialPropertyBlock();
    }
    private static readonly ConditionalWeakTable<EntityVehicle,State> States=new ConditionalWeakTable<EntityVehicle,State>();
    internal static void Apply(EntityVehicle entity)
    {
        if(entity?.vehicle==null || entity.ModelTransform==null)return;
        State state;
        if(States.TryGetValue(entity,out state) && state.Model!=entity.ModelTransform){States.Remove(entity);state=null;}
        if(state==null)
        {
            state=new State {Model=entity.ModelTransform};
            foreach(var renderer in state.Model.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                    if(materials[i]!=null && materials[i].shader.name=="REBIRTH/StandardVehiclePaint")
                        state.Paint.Add(new Binding {Renderer=renderer,MaterialIndex=i});
            }
            States.Add(entity,state);
        }
        bool disco=false;
        var item=entity.vehicle.GetUpdatedItemValue();
        if(item!=null)for(int slot=0;slot<item.CosmeticModCount;slot++)
        {
            var cosmetic=item.GetCosmeticMod(slot);
            if(cosmetic!=null && !cosmetic.IsEmpty() &&
               string.Equals(cosmetic.ItemClass?.GetItemName(),RebirthCamperPattern.DiscoModifier,StringComparison.Ordinal))
                {disco=true;break;}
        }
        string tint=item?.GetPropertyOverride(Block.PropTintColor,"255,255,255")??"255,255,255";
        if(state.Disco==disco && string.Equals(state.Tint,tint,StringComparison.Ordinal))return;
        Vector3 rgb=Block.StringToVector3(tint);
        var color=new Color(rgb.x,rgb.y,rgb.z,1);
        foreach(var binding in state.Paint)
        {
            if(binding.Renderer==null)continue;
            state.Block.Clear();binding.Renderer.GetPropertyBlock(state.Block,binding.MaterialIndex);
            state.Block.SetFloat("_PatternEnabled",disco?1:0);
            state.Block.SetColor("_Color",color);
            binding.Renderer.SetPropertyBlock(state.Block,binding.MaterialIndex);
        }
        state.Disco=disco;
        state.Tint=tint;
    }
}
