using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Native Physics is a sibling of the visual model. Every solid authored below
// that model must ignore its own compound rig, including optional plow/armor
// and turret geometry outside the camper cosmetic module.
internal static class RebirthCamperOwnCollision
{
    private sealed class State
    {
        internal Transform Root, Model;
        internal Rigidbody Body;
        internal int Children;
        internal float NextBind;
        internal bool Dirty=true;
        internal readonly List<Collider> All=new List<Collider>();
        internal readonly List<Collider> Detached=new List<Collider>();
        internal readonly List<Collider> Compound=new List<Collider>();
    }
    private static readonly ConditionalWeakTable<EntityVehicle,State> states=new ConditionalWeakTable<EntityVehicle,State>();
    internal static void Invalidate(EntityVehicle vehicle)
    {
        if(vehicle!=null&&states.TryGetValue(vehicle,out var state))state.Dirty=true;
    }
    internal static void Apply(EntityVehicle vehicle)
    {
        if(!ThreadManager.IsMainThread()||!RebirthCamperRadial.IsCamper(vehicle)
            ||vehicle.RootTransform==null||vehicle.ModelTransform==null||vehicle.vehicleRB==null)return;
        var state=states.GetValue(vehicle,_=>new State());
        if(state.Dirty||state.Root!=vehicle.RootTransform||state.Model!=vehicle.ModelTransform||state.Body!=vehicle.vehicleRB
            ||state.Children!=vehicle.RootTransform.childCount||Time.realtimeSinceStartup>=state.NextBind)
        {
            state.Root=vehicle.RootTransform;state.Model=vehicle.ModelTransform;state.Body=vehicle.vehicleRB;
            state.Children=state.Root.childCount;state.Dirty=false;state.NextBind=Time.realtimeSinceStartup+5f;
            state.All.Clear();state.Detached.Clear();state.Compound.Clear();
            state.Root.GetComponentsInChildren<Collider>(true,state.All);
            foreach(var collider in state.All)
            {
                if(collider==null||collider.isTrigger)continue;
                // Occupants/other actors may be parented to the vehicle. Their
                // physical bodies are not authored truck parts.
                var actor=collider.GetComponentInParent<Entity>();
                if(actor!=null&&!ReferenceEquals(actor,vehicle))continue;
                if(collider.attachedRigidbody==state.Body)state.Compound.Add(collider);
                else state.Detached.Add(collider);
            }
        }
        foreach(var detached in state.Detached)
        {
            if(detached==null||!detached.enabled||!detached.gameObject.activeInHierarchy)continue;
            foreach(var compound in state.Compound)
            {
                if(compound==null||!compound.enabled||!compound.gameObject.activeInHierarchy
                    ||Physics.GetIgnoreLayerCollision(detached.gameObject.layer,compound.gameObject.layer))continue;
                // Native attachment can toggle colliders/layers in one call;
                // Unity may clear pair ignores, so verify before simulation.
                if(!Physics.GetIgnoreCollision(detached,compound))Physics.IgnoreCollision(detached,compound,true);
            }
        }
        // The native model box is broad selection geometry, not a body wall.
        // Its full camper bounds cover the otherwise reachable stock hood.
        // Keep it queryable, but let native player movement use the real hulls.
        var player=vehicle.world?.GetPrimaryPlayer() as EntityPlayerLocal;
        var controller=player?.vp_FPController?.CharacterController;
        var proxy=state.Model.GetComponent<BoxCollider>();
        if(controller!=null&&controller.enabled&&proxy!=null&&proxy.enabled&&proxy.gameObject.activeInHierarchy
            &&ReferenceEquals(player.world,vehicle.world)&&!Physics.GetIgnoreCollision(proxy,controller))
            Physics.IgnoreCollision(proxy,controller,true);
    }
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.SetVehicleDriven))]
internal static class RebirthCamperOwnCollisionDriven
{
    private static void Postfix(EntityVehicle __instance)
    {RebirthCamperOwnCollision.Invalidate(__instance);RebirthCamperOwnCollision.Apply(__instance);}
}
[HarmonyPatch(typeof(Vehicle),nameof(Vehicle.SetItemValue))]
internal static class RebirthCamperOwnCollisionParts
{
    private static void Postfix(Vehicle __instance)
    {if(__instance?.entity is EntityVehicle vehicle){RebirthCamperOwnCollision.Invalidate(vehicle);RebirthCamperOwnCollision.Apply(vehicle);}}
}
