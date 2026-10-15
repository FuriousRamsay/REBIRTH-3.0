using System;
using UnityEngine;
using HarmonyLib;

// Native land-claim material on an independently owned two-sided sphere mesh.
// Never resize/recolor or retire an actual claim helper in its native pool.
public static class RebirthCamperEnergyBoundsDisplay
{
    private sealed class Display
    {internal EntityVehicle Vehicle;internal EntityPlayerLocal Player;internal World World;internal GameObject Root;internal Material Material;internal Mesh Mesh;}
    private static Display shown;
    private static bool installed;
    private static int diagnostics;
    // Explicit Show/Hide requests only; never emit one line per vehicle tick.
    private static void Diagnose(EntityVehicle vehicle,EntityPlayerLocal player,string stage,string detail)
    {
        if(diagnostics>=24)return;diagnostics++;
        Log.Out("[REBIRTH CamperEnergyBounds] stage="+stage+" vehicle="+(vehicle?.entityId??-1)
            +" player="+(player?.entityId??-1)+" "+detail);
    }
    private static string MaterialDetails(Material material)
    {
        if(material==null)return "resource=missing path=Materials/LandClaimBoundary";
        string result="resource=present name="+material.name+" shader="+(material.shader!=null?material.shader.name:"missing")
            +" color="+material.HasProperty("_Color")+" baseColor="+material.HasProperty("_BaseColor")
            +" tintColor="+material.HasProperty("_TintColor");
        // Query the actual runtime shader, without requiring a guessed shader ABI
        // or introducing an alternative shader/material into the native visual.
        try
        {
            var shader=material.shader;if(shader==null)return result;
            var type=shader.GetType();var count=type.GetMethod("GetPropertyCount",Type.EmptyTypes);
            var name=type.GetMethod("GetPropertyName",new[]{typeof(int)});
            var kind=type.GetMethod("GetPropertyType",new[]{typeof(int)});
            if(count==null||name==null||kind==null)return result+" properties=runtimeAPIUnavailable";
            int total=(int)count.Invoke(shader,null);result+=" propertyCount="+total+" properties=";
            for(int i=0;i<Math.Min(total,24);i++)result+=(i==0?"":",")+name.Invoke(shader,new object[]{i})+":"+kind.Invoke(shader,new object[]{i});
        }
        catch(Exception error){result+=" propertyObserver="+error.GetType().Name;}
        return result;
    }
    public static void Install()
    {if(installed)return;ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);installed=true;}
    private static bool Current(EntityVehicle vehicle,EntityPlayerLocal player)
    {
        if(!ThreadManager.IsMainThread()||vehicle==null||player==null||player.IsDead()||vehicle.world==null
            ||!ReferenceEquals(GameManager.Instance?.World,vehicle.world)||!ReferenceEquals(player.world,vehicle.world)
            ||!ReferenceEquals(vehicle.world.GetPrimaryPlayer(),player)||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)
            ||!RebirthCamperRadial.IsCamper(vehicle)||!RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)
            ||mode!=RebirthCamperEnergyMode.Camp||!TryRadius(vehicle,out _))return false;
        var owner=vehicle.GetOwner();var data=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        return owner!=null&&data!=null&&(owner.Equals(data.PrimaryId)||owner.Equals(data.NativeId));
    }
    public static bool IsShown(EntityVehicle vehicle,EntityPlayerLocal player)
        =>shown!=null&&ReferenceEquals(shown.Vehicle,vehicle)&&ReferenceEquals(shown.Player,player)&&shown.Root!=null&&Current(vehicle,player);
    public static bool Toggle(EntityVehicle vehicle,EntityPlayerLocal player,out string reason)
        =>SetShown(vehicle,player,!IsShown(vehicle,player),out reason);
    private static bool TryRadius(EntityVehicle vehicle,out float radius)
    {
        radius=0;if(vehicle?.world==null)return false;
        if(vehicle.world.IsRemote())return RebirthCamperClaimProjection.TryRange(vehicle,out radius);
        return RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out _,out _,out radius);
    }
    public static bool SetShown(EntityVehicle vehicle,EntityPlayerLocal player,bool visible,out string reason)
    {
        reason=null;
        if(!visible)
        {
            if(shown!=null&&ReferenceEquals(shown.Vehicle,vehicle)&&ReferenceEquals(shown.Player,player))Hide();
            return true;
        }
        if(!Current(vehicle,player))
        {
            reason="Bounds are available only for this current owned camper in Camp.";
            bool observed=RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out var revision,out _);
            Diagnose(vehicle,player,"refusedCurrent","mainThread="+ThreadManager.IsMainThread()+" modeObserved="+observed
                +" mode="+mode+" revision="+revision+" primary="+ReferenceEquals(vehicle?.world?.GetPrimaryPlayer(),player)
                +" liveVehicle="+(vehicle?.world!=null&&ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle))
                +" sameWorld="+ReferenceEquals(vehicle?.world,player?.world)+" dead="+(player!=null&&player.IsDead()));return false;
        }
        if(IsShown(vehicle,player))return true;
        // Same native resource used by LandClaimBoundsHelper.GetBoundsHelper.
        var native=Resources.Load("Materials/LandClaimBoundary",typeof(Material)) as Material;
        // Actual Hidden/LandClaimBoundary shader exposes these two colors, not
        // Material.color's _Color. Preserve each original shader alpha.
        if(native==null||!native.HasProperty("_BaseColor")||!native.HasProperty("_BoundaryColor")){reason="Native land-claim boundary color material unavailable.";Diagnose(vehicle,player,"refusedMaterial",MaterialDetails(native));return false;}
        Diagnose(vehicle,player,"nativeMaterial",MaterialDetails(native));
        Hide();var root=new GameObject("RebirthCamperEnergyBounds");var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var collider=sphere.GetComponent<Collider>();if(collider!=null){collider.enabled=false;UnityEngine.Object.Destroy(collider);}
        sphere.transform.parent=root.transform;sphere.transform.localScale=Vector3.one;sphere.transform.localPosition=Vector3.zero;sphere.transform.localRotation=Quaternion.identity;
        var filter=sphere.GetComponent<MeshFilter>();var mesh=TwoSided(filter.sharedMesh);filter.sharedMesh=mesh;
        var material=new Material(native);
        Color baseTint=material.GetColor("_BaseColor"),boundaryTint=material.GetColor("_BoundaryColor");
        material.SetColor("_BaseColor",new Color(.65f,.35f,.85f,baseTint.a));
        material.SetColor("_BoundaryColor",new Color(.65f,.35f,.85f,boundaryTint.a));
        sphere.GetComponent<Renderer>().material=material;
        shown=new Display {Vehicle=vehicle,Player=player,World=vehicle.world,Root=root,Material=material,Mesh=mesh};Update(vehicle);
        Diagnose(vehicle,player,"shown","active="+root.activeInHierarchy+" span="+root.transform.localScale
            +" baseTint="+material.GetColor("_BaseColor")+" boundaryTint="+material.GetColor("_BoundaryColor"));return true;
    }
    private static Mesh TwoSided(Mesh nativeSphere)
    {
        // Preserve the native sphere's vertex channels and the exact shader;
        // reversed faces make it visible from inside without a new shader or
        // an unsupported material culling-property assumption.
        var back=UnityEngine.Object.Instantiate(nativeSphere);var triangles=back.triangles;
        for(int i=0;i<triangles.Length;i+=3){int swap=triangles[i];triangles[i]=triangles[i+1];triangles[i+1]=swap;}
        back.triangles=triangles;var normals=back.normals;for(int i=0;i<normals.Length;i++)normals[i]=-normals[i];back.normals=normals;
        var mesh=new Mesh {name="RebirthCamperEnergyBoundsSphere"};
        mesh.CombineMeshes(new[]{new CombineInstance {mesh=nativeSphere},new CombineInstance {mesh=back}},true,false);
        UnityEngine.Object.Destroy(back);return mesh;
    }
    public static void Hide()
    {
        var prior=shown;shown=null;if(prior==null)return;
        if(prior.Root!=null)UnityEngine.Object.Destroy(prior.Root);
        if(prior.Material!=null)UnityEngine.Object.Destroy(prior.Material);
        if(prior.Mesh!=null)UnityEngine.Object.Destroy(prior.Mesh);
    }
    internal static void Update(EntityVehicle vehicle)
    {
        var display=shown;if(display==null||!ReferenceEquals(display.Vehicle,vehicle))return;
        if(!ReferenceEquals(display.World,vehicle.world)||!Current(vehicle,display.Player)){Hide();return;}
        display.Root.transform.position=RebirthCamperEnergyBounds.CurrentCenter(vehicle)-Origin.position;
        if(!TryRadius(vehicle,out float radius)){Hide();return;}
        float span=radius*2f;display.Root.transform.localScale=new Vector3(span,span,span);
    }
    internal static void OnUnload(EntityVehicle vehicle)
    {if(shown!=null&&ReferenceEquals(shown.Vehicle,vehicle))Hide();}
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data){Hide();diagnostics=0;}
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.Update))]
internal static class RebirthCamperEnergyBoundsDisplayTick
{private static void Postfix(EntityVehicle __instance)=>RebirthCamperEnergyBoundsDisplay.Update(__instance);}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.OnEntityUnload))]
internal static class RebirthCamperEnergyBoundsDisplayUnload
{private static void Postfix(EntityVehicle __instance)=>RebirthCamperEnergyBoundsDisplay.OnUnload(__instance);}
