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
        internal float NextCheck;
        internal bool Dirty=true;
        internal BoxCollider CabHull;
        internal CharacterController HoodController;
        internal bool HoodIgnored;
        internal readonly List<MeshCollider> HoodSupports=new List<MeshCollider>();
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
        // Native collider changes are repaired promptly, but a stationary camp
        // shell does not need an all-pairs Physics query twice every frame.
        if(!state.Dirty&&state.Root==vehicle.RootTransform&&state.Model==vehicle.ModelTransform
            &&state.Body==vehicle.vehicleRB&&state.Children==vehicle.RootTransform.childCount
            &&Time.realtimeSinceStartup<state.NextCheck)return;
        state.NextCheck=Time.realtimeSinceStartup+.25f;
        if(state.Dirty||state.Root!=vehicle.RootTransform||state.Model!=vehicle.ModelTransform||state.Body!=vehicle.vehicleRB
            ||state.Children!=vehicle.RootTransform.childCount||Time.realtimeSinceStartup>=state.NextBind)
        {
            state.Root=vehicle.RootTransform;state.Model=vehicle.ModelTransform;state.Body=vehicle.vehicleRB;
            state.Children=state.Root.childCount;state.Dirty=false;state.NextBind=Time.realtimeSinceStartup+5f;
            BindHoodSupports(state);
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
        bool known=RebirthCamperEnergyRuntime.TryObservePresentation(vehicle,out var mode,out _,out _,out _);
        bool travel=known&&mode!=RebirthCamperEnergyMode.Camp&&RebirthCamperBodyAttachment.HasBody(vehicle);
        foreach(var support in state.HoodSupports)if(support!=null)support.enabled=travel;
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
        // Travel's native broad cab box has a 1.72m-high front wall across the
        // 1.10m hood. Keep its vehicle/world collisions; only the local on-foot
        // controller approaching the hood uses the already authored sloped skin.
        bool hood=travel&&state.HoodSupports.Count==2&&controller!=null&&controller.enabled&&player.AttachedToEntity==null
            &&ReferenceEquals(player.world,vehicle.world);
        if(hood)
        {
            var feet=state.Model.InverseTransformPoint(controller.transform.TransformPoint(
                controller.center-Vector3.up*(controller.height*.5f)));
            hood=RebirthCamperHoodAccess.InApproach(feet.x,feet.y,feet.z);
        }
        if(state.HoodController!=controller&&state.HoodController!=null&&state.CabHull!=null)
            Physics.IgnoreCollision(state.CabHull,state.HoodController,false);
        bool keep=state.HoodIgnored&&state.HoodController==controller&&travel&&controller!=null&&state.CabHull!=null
            &&state.CabHull.bounds.Intersects(controller.bounds);
        bool ignore=hood||keep;
        if(state.CabHull!=null&&controller!=null&&Physics.GetIgnoreCollision(state.CabHull,controller)!=ignore)
            Physics.IgnoreCollision(state.CabHull,controller,ignore);
        state.HoodController=controller;state.HoodIgnored=ignore;
    }

    private static void BindHoodSupports(State state)
    {
        state.CabHull=null;state.HoodSupports.Clear();
        foreach(var node in state.Root.GetComponentsInChildren<Transform>(true))
        {
            if(node.name=="CabHull")state.CabHull=node.GetComponent<BoxCollider>();
            if(node.name.StartsWith("REBIRTH travel Camp "))
            {var existing=node.GetComponent<MeshCollider>();if(existing!=null)state.HoodSupports.Add(existing);}
        }
        if(state.HoodSupports.Count==2)return;
        foreach(var node in state.Root.GetComponentsInChildren<Transform>(true))
        {
            if(node.name!="Camp hood exterior"&&node.name!="Camp front cab exterior")continue;
            var source=node.GetComponent<MeshCollider>();if(source==null||source.sharedMesh==null)continue;
            bool exists=false;foreach(var support in state.HoodSupports)if(support.name=="REBIRTH travel "+node.name)exists=true;
            if(exists)continue;
            var go=new GameObject("REBIRTH travel "+node.name);go.layer=node.gameObject.layer;
            go.transform.SetParent(node.parent,false);go.transform.localPosition=node.localPosition;
            go.transform.localRotation=node.localRotation;go.transform.localScale=node.localScale;
            go.transform.SetParent(state.Model,true);
            var clone=go.AddComponent<MeshCollider>();clone.enabled=false;clone.sharedMesh=source.sharedMesh;
            clone.convex=source.convex;clone.sharedMaterial=source.sharedMaterial;state.HoodSupports.Add(clone);
        }
    }
}

internal static class RebirthCamperHoodAccess
{
    internal static bool InApproach(float x,float y,float z)=>Math.Abs(x)<1.2f&&z>.7f&&z<4.2f&&y>-.5f&&y<3.2f;
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
