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
    private struct ActiveCollider
    {
        internal Collider Collider;
        internal int Layer;
    }
    private sealed class State
    {
        internal Transform Root, Model;
        internal Rigidbody Body;
        internal int Children;
        internal float NextBind;
        internal float NextCheck;
        internal bool Dirty=true;
        internal BoxCollider CabHull;
        internal GameObject BodyGuardrails;
        internal bool BareApplied;
        internal readonly List<BoxCollider> BareHull=new List<BoxCollider>();
        internal CharacterController HoodController;
        internal bool HoodIgnored;
        internal readonly List<MeshCollider> HoodSupports=new List<MeshCollider>();
        internal readonly List<Collider> All=new List<Collider>();
        internal readonly List<Collider> Detached=new List<Collider>();
        internal readonly List<Collider> Compound=new List<Collider>();
        internal readonly List<ActiveCollider> ActiveCompound=new List<ActiveCollider>();
        internal readonly uint[] KnownLayers=new uint[32], IgnoredLayers=new uint[32];
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
        bool hasBody=RebirthCamperBodyAttachment.HasBody(vehicle);
        ApplyBodyGuardrails(hasBody,state.BodyGuardrails);
        bool travel=known&&mode!=RebirthCamperEnergyMode.Camp&&hasBody;
        // The native CabHull spans the entire pickup bed at cab-roof height.
        // Replace that broad solid only while the camper body is absent.
        bool bare=!hasBody&&state.CabHull!=null&&state.BareHull.Count==11;
        if(state.CabHull!=null&&(bare||state.BareApplied))
        {
            bool cabEnabled=!bare&&(!known||mode!=RebirthCamperEnergyMode.Camp);
            if(state.CabHull.enabled!=cabEnabled)state.CabHull.enabled=cabEnabled;
        }
        state.BareApplied=bare;
        foreach(var part in state.BareHull)
            if(part!=null&&part.enabled!=bare)part.enabled=bare;
        foreach(var support in state.HoodSupports)
            if(support!=null&&support.enabled!=travel)support.enabled=travel;
        // Snapshot compound membership once per pass, not once per detached
        // collider. No persistent cache: native enable/layer changes are still
        // observed each pass and every eligible physical pair is rechecked.
        state.ActiveCompound.Clear();
        foreach(var compound in state.Compound)
        {
            if(compound==null||!compound.enabled)continue;
            var go=compound.gameObject;
            if(go.activeInHierarchy)state.ActiveCompound.Add(new ActiveCollider{Collider=compound,Layer=go.layer});
        }
        Array.Clear(state.KnownLayers,0,32);Array.Clear(state.IgnoredLayers,0,32);
        foreach(var detached in state.Detached)
        {
            if(detached==null||!detached.enabled||!detached.gameObject.activeInHierarchy)continue;
            int detachedLayer=detached.gameObject.layer;
            foreach(var active in state.ActiveCompound)
            {
                uint bit=1u<<active.Layer;
                if((state.KnownLayers[detachedLayer]&bit)==0)
                {
                    state.KnownLayers[detachedLayer]|=bit;
                    if(Physics.GetIgnoreLayerCollision(detachedLayer,active.Layer))state.IgnoredLayers[detachedLayer]|=bit;
                }
                if((state.IgnoredLayers[detachedLayer]&bit)!=0)continue;
                var compound=active.Collider;
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
        // These native compound solids are siblings of the visual addon, so
        // hiding BodyModule alone leaves invisible rooftop rails on the pickup.
        // Keep the original shapes/flags and Physics Rigidbody ownership.
        var rails=state.Body.transform.Find("Turret-facing guardrail collision");
        state.BodyGuardrails=rails!=null?rails.gameObject:null;
        state.CabHull=null;state.HoodSupports.Clear();state.BareHull.Clear();
        foreach(var node in state.Root.GetComponentsInChildren<Transform>(true))
        {
            if(node.name=="CabHull")state.CabHull=node.GetComponent<BoxCollider>();
            if(node.name.StartsWith("REBIRTH bare pickup "))
            {var part=node.GetComponent<BoxCollider>();if(part!=null)state.BareHull.Add(part);}
            if(node.name.StartsWith("REBIRTH travel Camp "))
            {var existing=node.GetComponent<MeshCollider>();if(existing!=null)state.HoodSupports.Add(existing);}
        }
        if(state.CabHull!=null&&state.BareHull.Count==0)
        {
            // The cab's main solid stops below its roof. A shallow roof plate
            // avoids recreating a tall invisible wall at the windshield.
            // All coordinates are in the original CabHull's Physics space.
            AddBarePart(state,"cab",new Vector3(0,1.036f,-.09f),new Vector3(1.8075f,1.127f,.75f));
            AddBarePart(state,"cab roof",new Vector3(0,1.696f,-.09f),new Vector3(1.8075f,.052f,.75f));
            // These overlapping short steps follow the visible windshield
            // down to the hood. They remain in the native Rigidbody compound
            // even when the camper module and its Camp shell are absent.
            AddBarePart(state,"windshield upper",new Vector3(0,1.08f,.375f),new Vector3(1.52f,1.06f,.19f));
            AddBarePart(state,"windshield middle",new Vector3(0,.99f,.545f),new Vector3(1.52f,.88f,.19f));
            AddBarePart(state,"windshield lower",new Vector3(0,.905f,.715f),new Vector3(1.52f,.71f,.19f));
            AddBarePart(state,"hood rear",new Vector3(0,.875f,1.105f),new Vector3(1.52f,.65f,.69f));
            AddBarePart(state,"hood front",new Vector3(0,.84f,1.72f),new Vector3(1.52f,.58f,.66f));
            AddBarePart(state,"bed floor",new Vector3(0,.712f,-1.59f),new Vector3(1.57f,.068f,2.25f));
            AddBarePart(state,"left bed side",new Vector3(-.79f,.94f,-1.59f),new Vector3(.095f,.40f,2.31f));
            AddBarePart(state,"right bed side",new Vector3(.79f,.94f,-1.59f),new Vector3(.095f,.40f,2.31f));
            AddBarePart(state,"tailgate",new Vector3(0,.94f,-2.755f),new Vector3(1.58f,.40f,.075f));
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
    internal static void ApplyBodyGuardrails(bool hasBody,GameObject guardrails)
    {
        if(guardrails!=null&&guardrails.activeSelf!=hasBody)guardrails.SetActive(hasBody);
    }
    private static void AddBarePart(State state,string name,Vector3 center,Vector3 size)
    {
        var go=new GameObject("REBIRTH bare pickup "+name);
        go.layer=state.CabHull.gameObject.layer;
        go.transform.SetParent(state.CabHull.transform,false);
        var part=go.AddComponent<BoxCollider>();
        part.center=center;part.size=size;part.enabled=false;
        state.BareHull.Add(part);
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
