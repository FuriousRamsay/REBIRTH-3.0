using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Called only after the actual-camper station and four dense sweep checks pass.
// Produces a staged prefab and LOD input, never writes the installed game bundle.
public static class RebirthCamperTurretProduction
{
    public const string Folder="Assets/REBIRTHCamperTurretProduction_20261020";
    const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output";
    [Serializable] public sealed class Indices {public int[] triangles;}
    [Serializable] public sealed class MeshInput
    {
        public string key,asset,name,guid;public long localId;public Vector3[] vertices,normals;public Vector4[] tangents;
        public Vector2[] uv,uv2;public Color[] colors;public Indices[] submeshes;
    }
    [Serializable] public sealed class Inputs {public MeshInput[] meshes;}
    static string PathOf(Transform t,Transform root)
    {var parts=new List<string>();while(t&&t!=root){parts.Add(t.name);t=t.parent;}parts.Reverse();return string.Join("/",parts);}
    public static void Stage(GameObject root)
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);
        var lines=new List<string>();
        var frontOwner=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Short straight front ladders and ladder mounted supports");
        var fitted=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Fitted actual ")&&t.name.EndsWith(" turret")).ToArray();
        if(fitted.Length!=4)throw new Exception("Exactly four actual fitted rigs required");
        foreach(var turret in fitted)
        {
            bool front=turret.name.Contains("front"),left=turret.name.Contains("left");int corner=(front?0:2)+(left?0:1);
            var slot=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHCamperTurretSlot"+corner);
            var previous=slot.Cast<Transform>().ToArray();
            if(front)
            {
                turret.SetParent(slot,true);
                var parts=frontOwner.Cast<Transform>().Where(t=>t.GetComponent<MeshRenderer>()&&
                    new[]{"Split saddle on ladder left rail","Ladder mounted welded upper arm","Ladder mounted open diagonal brace","Socket platform floor","Recessed locating tooth socket wall"}.Contains(t.name)&&
                    (t.GetComponent<Renderer>().bounds.center.x<0)==left).ToArray();
                if(parts.Length<5)throw new Exception("Front station support is incomplete: "+corner);
                foreach(var part in parts)part.SetParent(slot,true);
            }
            else turret.parent.SetParent(slot,true);
            foreach(var old in previous)Object.DestroyImmediate(old.gameObject);
            turret.name="Optimized robot turret "+(corner+1);
            var pitch=turret.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="TurretPitch");
            var yaw=turret.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="TurretYaw");
            pitch.localRotation=Quaternion.identity;yaw.localRotation=Quaternion.identity;
            var limits=turret.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="TurretPitchLimits");limits.localPosition=new Vector3(-75,29.5f,90);
            var yawLimits=new GameObject("TurretYawLimits").transform;yawLimits.SetParent(turret,false);yawLimits.localPosition=new Vector3(-90,90,0);
            slot.gameObject.SetActive(false);
            lines.Add("Corner "+corner+" actual fitted position="+turret.position.ToString("F5")+" rotation="+turret.rotation.eulerAngles+" rig fixed/yaw/whole-body pitch unchanged; default hidden.");
        }
        foreach(int side in new[]{-1,1})
        {
            var volume=new GameObject("REBIRTHFrontLadderClimbVolume");volume.transform.SetParent(frontOwner,false);
            volume.transform.position=new Vector3(side*.78f,2.28f,1.37f);volume.transform.rotation=Quaternion.identity;
            var scale=frontOwner.lossyScale;volume.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            var box=volume.AddComponent<BoxCollider>();box.size=new Vector3(.49f,1.68f,.60f);box.isTrigger=true;box.enabled=false;
        }
        foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(-1);
        RebirthCamperCampIntegration.SetMode(root,false);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(RebirthRobotTurretImport.Folder+"/rebirthCamper80s.prefab");
        foreach(var t in source.GetComponentsInChildren<Transform>(true))
        {
            string path=PathOf(t,source.transform);var target=string.IsNullOrEmpty(path)?root.transform:root.transform.Find(path);
            if(target)target.gameObject.SetActive(t.gameObject.activeSelf);
        }
        if(root.GetComponentsInChildren<WheelCollider>(true).Length!=4||!root.transform.Find("Physics")||!root.transform.Find("GameObject/M/VisualBody"))throw new Exception("Native camper structure changed");
        root.name="rebirthCamper80s";
        PrefabUtility.SaveAsPrefabAsset(root,Folder+"/rebirthCamper80s.prefab");AssetDatabase.SaveAssets();
        ExportLodInputs(root,lines);
        lines.Add("Staged only. Sampled edge/surface clearance does not prove continuous sweep or gameplay. Camper/station LOD generation/import, live rig/controls, materials and multiplayer validation remain required before bundle deployment.");
        File.WriteAllLines(Output+"/production-staging.txt",lines);
    }
    static void ExportLodInputs(GameObject root,List<string> lines)
    {
        var covered=new HashSet<Renderer>(root.GetComponentsInChildren<LODGroup>(true).SelectMany(g=>g.GetLODs()).SelectMany(l=>l.renderers));
        var inputs=new List<MeshInput>();var seen=new HashSet<Mesh>();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer=filter.GetComponent<MeshRenderer>();var mesh=filter.sharedMesh;
            if(!renderer||!mesh||covered.Contains(renderer)||!seen.Add(mesh))continue;
            string key="mesh"+inputs.Count.ToString("D4");
            var data=Read(mesh);data.key=key;data.asset=AssetDatabase.GetAssetPath(mesh);data.name=mesh.name;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out data.guid,out data.localId);
            inputs.Add(data);lines.Add("LOD input "+key+" asset="+data.asset+" mesh="+mesh.name+" triangles="+data.submeshes.Sum(s=>s.triangles.Length/3));
        }
        File.WriteAllText(Output+"/camper-lod-input.json",JsonUtility.ToJson(new Inputs{meshes=inputs.ToArray()}));
    }
    static T[] Channel<T>(int count,Action<NativeArray<T>> read) where T:struct
    {using(var buffer=new NativeArray<T>(count,Allocator.Temp)){read(buffer);return buffer.ToArray();}}
    static MeshInput Read(Mesh mesh)
    {
        // The editor MeshUtility API can inspect non-readable imported meshes
        // without changing importer flags or making shipped meshes CPU-readable.
        using(var array=MeshUtility.AcquireReadOnlyMeshData(mesh))
        {
            var m=array[0];int count=m.vertexCount;
            var data=new MeshInput{
                vertices=Channel<Vector3>(count,m.GetVertices),
                normals=m.HasVertexAttribute(VertexAttribute.Normal)?Channel<Vector3>(count,m.GetNormals):Array.Empty<Vector3>(),
                tangents=m.HasVertexAttribute(VertexAttribute.Tangent)?Channel<Vector4>(count,m.GetTangents):Array.Empty<Vector4>(),
                uv=m.HasVertexAttribute(VertexAttribute.TexCoord0)?Channel<Vector2>(count,b=>m.GetUVs(0,b)):Array.Empty<Vector2>(),
                uv2=m.HasVertexAttribute(VertexAttribute.TexCoord1)?Channel<Vector2>(count,b=>m.GetUVs(1,b)):Array.Empty<Vector2>(),
                colors=m.HasVertexAttribute(VertexAttribute.Color)?Channel<Color>(count,m.GetColors):Array.Empty<Color>(),
                submeshes=new Indices[m.subMeshCount]};
            for(int s=0;s<m.subMeshCount;s++){int sub=s;data.submeshes[s]=new Indices{triangles=Channel<int>(m.GetSubMesh(s).indexCount,b=>m.GetIndices(b,sub,true))};}
            return data;
        }
    }
    public static void ExportOnly()
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/rebirthCamper80s.prefab");if(!source)throw new Exception("Passed production stage prefab missing");
        var root=Object.Instantiate(source);
        try{var lines=new List<string>();ExportLodInputs(root,lines);File.WriteAllLines(Output+"/production-lod-export.txt",lines);}
        finally{Object.DestroyImmediate(root);}
    }
}
