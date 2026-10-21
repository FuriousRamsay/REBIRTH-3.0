using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object=UnityEngine.Object;

// Native geometry only. Electrical links are owned by the relay wiring adapter,
// never inferred from the existence of these child renderers.
internal static class RebirthCamperRelayVisuals
{
    internal const string Asset="@:Entities/Electrical/relayPrefab.prefab";
    private sealed class Binding {internal Transform Root,Model;internal GameObject Group;internal float Next;internal string Reason;}
    private static readonly ConditionalWeakTable<EntityVehicle,Binding> bindings=new ConditionalWeakTable<EntityVehicle,Binding>();
    internal static GameObject Group(EntityVehicle vehicle)=>vehicle!=null&&bindings.TryGetValue(vehicle,out var binding)?binding.Group:null;
    private static void Wait(EntityVehicle vehicle,Binding binding,string reason)
    {
        if(binding.Reason==reason)return;binding.Reason=reason;
        Log.Warning("[Camper relays] vehicle "+vehicle.entityId+" waiting: "+reason);
    }
    internal static void Attach(EntityVehicle vehicle)
    {
        if(!vehicle?.RootTransform||!vehicle.ModelTransform)return;
        var root=vehicle.RootTransform;var frame=vehicle.ModelTransform;
        if(bindings.TryGetValue(vehicle,out var binding)&&(binding.Root!=root||binding.Model!=vehicle.ModelTransform)){bindings.Remove(vehicle);binding=null;}
        if(binding==null){binding=new Binding{Root=root,Model=vehicle.ModelTransform};bindings.Add(vehicle,binding);}
        if(binding.Group||Time.realtimeSinceStartup<binding.Next)return;
        binding.Next=Time.realtimeSinceStartup+1f;
        var existing=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="REBIRTHCamperNativeRelays");
        if(existing)
        {
            if(existing.GetComponentsInChildren<RebirthCamperWireTarget>(true).Count(t=>t.Kind==0)==2
                &&existing.GetComponentsInChildren<Renderer>(true).Length>=2){binding.Group=existing.gameObject;return;}
            existing.gameObject.SetActive(false);Object.Destroy(existing.gameObject);
        }
        var battery=root.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault(c=>c.name=="REBIRTHInteraction_battery_bank");
        if(!battery){Wait(vehicle,binding,"battery anchor not loaded");return;}
        var skin=root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f=>f.name=="camper_body"&&f.sharedMesh);
        if(!skin||!skin.sharedMesh.isReadable){Wait(vehicle,binding,"readable camper skin not loaded");return;}
        var prefab=DataLoader.LoadAsset<GameObject>(Asset);if(!prefab){Wait(vehicle,binding,"native relay asset not loaded");return;}
        // Bundled shaders need not be registered by name. Reuse the exact shader
        // already referenced by the native station fade materials, including
        // inactive stations, before attempting the global name lookup.
        Shader fade=null;
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach(var material in renderer.sharedMaterials)
                if(material!=null&&material.HasProperty("_CampVisibility")&&material.shader!=null)
                {fade=material.shader;break;}
        if(!fade)fade=Shader.Find("REBIRTH/CamperCampFade");
        if(!fade){Wait(vehicle,binding,"station fade shader not loaded");return;}
        // RootTransform is a stationary factory wrapper. Authoring stripe height
        // and bank depth belong to the moving model's local coordinate system.
        var center=frame.InverseTransformPoint(battery.transform.TransformPoint(battery.center));
        var vertices=skin.sharedMesh.vertices.Select(v=>frame.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
        var triangles=skin.sharedMesh.triangles;
        var anchors=new Vector3[2];var normals=new Vector3[2];
        for(int i=0;i<2;i++)
        {
            int side=i==0?-1:1;
            if(!RebirthCamperRelayGeometry.Surface(vertices,triangles,new Vector3(side*3,RebirthCamperRelayGeometry.RedStripeCenter,center.z),new Vector3(-side,0,0),out anchors[i],out normals[i])){Wait(vehicle,binding,"camper skin mount ray missed side "+side);return;}
        }
        var group=new GameObject("REBIRTHCamperNativeRelays");group.SetActive(false);group.transform.SetParent(root,false);
        var materials=group.AddComponent<RebirthCamperRelayFadeMaterials>();
        var bodyRoot=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="REBIRTHCamperBodyModule");if(bodyRoot)group.transform.SetParent(bodyRoot,true);
        group.AddComponent<RebirthCamperCableView>().Vehicle=vehicle;
        foreach(int side in new[]{-1,1})
        {
            var mount=new GameObject(side<0?"Camper relay left":"Camper relay right");mount.transform.SetParent(group.transform,false);
            int index=side<0?0:1;
            mount.transform.position=frame.TransformPoint(anchors[index]);
            var normal=frame.TransformDirection(normals[index]).normalized;
            mount.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(frame.forward,normal).normalized,normal);
            // Owner-requested half-size native relay; local Y points out of its mounting plate.
            var relay=Object.Instantiate(prefab,mount.transform,false);relay.name="Native electric wire relay";
            relay.SetActive(true);
            relay.transform.localScale*=.5f;
            foreach(var script in relay.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
            foreach(var collider in relay.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            var renderers=relay.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0){Object.Destroy(group);Wait(vehicle,binding,"native relay has no renderers");return;}
            foreach(var renderer in renderers)
                renderer.sharedMaterials=renderer.sharedMaterials.Select(material=>materials.Convert(material,fade)).ToArray();
            bool first=true;var bounds=new Bounds();
            // World AABBs grow when the truck rotates. Use actual mesh bounds
            // transformed into the relay mount so yaw cannot move the plates.
            foreach(var filter in relay.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh)continue;
                var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var p=mount.transform.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,
                        new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
            }
            if(first){Object.Destroy(group);Wait(vehicle,binding,"native relay has no mesh bounds");return;}
            var shift=RebirthCamperRelayGeometry.SeatOffset(bounds);relay.transform.localPosition+=shift;
            bounds.center+=shift;
            var target=new GameObject("Camper relay wire endpoint");target.transform.SetParent(mount.transform,false);
            var tip=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);target.transform.localPosition=tip;
            var localBounds=new Bounds(bounds.center-tip,bounds.size+Vector3.one*.025f);
            RebirthCamperWireTarget.Attach(target,vehicle,0,side<0?0:1,localBounds);
        }
        // Appearance owns this group's activation and the same station opacity.
        binding.Group=group;binding.Reason=null;
        Log.Out("[Camper relays] vehicle "+vehicle.entityId+" mounted both native relay endpoints; station fade and deployed Camp interaction");
    }
}

internal sealed class RebirthCamperRelayFadeMaterials:MonoBehaviour
{
    private readonly System.Collections.Generic.Dictionary<Material,Material> materials=new System.Collections.Generic.Dictionary<Material,Material>();
    internal Material Convert(Material source,Shader shader)
    {
        if(source==null)return null;
        if(materials.TryGetValue(source,out var material))return material;
        material=new Material(source){name="Camper relay station fade",shader=shader,renderQueue=-1};
        material.SetOverrideTag("RenderType","Opaque");material.SetFloat("_CampVisibility",0);
        materials.Add(source,material);return material;
    }
    private void OnDestroy(){foreach(var material in materials.Values)if(material!=null)Object.Destroy(material);}
}
