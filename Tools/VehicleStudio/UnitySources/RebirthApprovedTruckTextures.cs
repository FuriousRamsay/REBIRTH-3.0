using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Staged review asset only. Uses the existing vehicle materials and native paint hierarchy.
// Does not replace the live bundle, run the game or alter source materials.
public static class RebirthApprovedTruckTextures
{
    const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TruckBodyworkTextured_20261020";
    const string Folder="Assets/REBIRTHApprovedPickupTextured_20261020";
    [Serializable] public class Part {public string name,group,material;public Vector3[] vertices,normals;public Vector2[] uv;public int[] triangles;}
    [Serializable] public class MeshData {public Part[] parts;}
    [Serializable] public class Entry {public string part,asset;}
    [Serializable] public class Survey {public Entry[] materials;}
    [Serializable] public class Report {public string status;public int meshes,paintChildren;public bool sharedDyeMaterial,plateSeparate,sourceAtlasesRetained;public string paintShader,plateTexture;public string[] renders;}
    static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
    static Material body;
    static Transform Group(Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    public static void Build()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var source=JsonUtility.FromJson<MeshData>(File.ReadAllText(Output+"/textured-truck-meshes.json"));
        var survey=JsonUtility.FromJson<Survey>(File.ReadAllText(Output+"/source-materials.json"));
        foreach(string key in source.parts.Select(p=>p.material).Distinct())
        {
            var entry=survey.materials.First(e=>e.part.Split('/').Last()==key);
            var original=AssetDatabase.LoadAssetAtPath<Material>(entry.asset);
            if(!original)throw new Exception("Missing original material "+entry.asset);
            // A staged copy prevents dye preview colors from changing any production material.
            var m=new Material(original){name="Original_"+key};
            if(key=="body")
            {
                // Original neutral factory finish. Retain the existing shader's native byte-color
                // dye handling and paint classification; only its undyed base sample differs.
                m.shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/RebirthPickupOriginalFinish.shader");
                if(!m.shader)throw new Exception("Original-finish shader missing.");
                m.SetFloat("_PatternEnabled",0);m.color=new Color(255,255,255,1);body=m;
            }
            string path=Folder+"/"+m.name+".mat";
            var saved=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(saved){EditorUtility.CopySerialized(m,saved);Object.DestroyImmediate(m);m=saved;}
            else AssetDatabase.CreateAsset(m,path);
            materials[key]=m;if(key=="body")body=m;
        }
        // Preserve the original renderer/mesh/transform hierarchy instead of round-tripping it.
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHBareTruckBodywork_20261020_v3/rebirthCamper80s.prefab"));
        root.name="Approved pickup textured review";
        foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(lod);
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.enabled=false;
        var native=root.transform;
        var common=Group(root.transform,"TruckCommonStructure");
        var pickup=Group(root.transform,"REBIRTHPickupOnlyBodywork");
        Transform bodyTransform=null;var painted=new List<Transform>();int serial=0;
        foreach(var p in source.parts)
        {
            if(p.group=="NativeTruckReference")
            {
                var entry=survey.materials.First(e=>e.part.Split('/').Last()==p.name);
                var original=root.transform.Find(entry.part);
                if(!original)throw new Exception("Original renderer missing "+entry.part);
                for(var t=original;t!=null;t=t.parent)t.gameObject.SetActive(true);
                var r=original.GetComponent<Renderer>();r.enabled=true;r.sharedMaterial=materials[p.material];
                if(p.name=="licence_plate")
                {
                    var filter=original.GetComponent<MeshFilter>();var plateMesh=Object.Instantiate(filter.sharedMesh);
                    var vertices=plateMesh.vertices;var indices=plateMesh.triangles;var front=new List<int>();
                    for(int i=0;i<indices.Length;i+=3)
                        if(original.TransformPoint(vertices[indices[i]]).z>0)front.AddRange(new[]{indices[i],indices[i+1],indices[i+2]});
                    plateMesh.triangles=front.ToArray();string platePath=Folder+"/original-front-plate.asset";
                    var savedPlate=AssetDatabase.LoadAssetAtPath<Mesh>(platePath);
                    if(savedPlate){EditorUtility.CopySerialized(plateMesh,savedPlate);Object.DestroyImmediate(plateMesh);plateMesh=savedPlate;}
                    else AssetDatabase.CreateAsset(plateMesh,platePath);
                    filter.sharedMesh=plateMesh;
                }
                if(p.name=="body")bodyTransform=original;
                continue;
            }
            var m=new Mesh{name=p.name,indexFormat=IndexFormat.UInt32};
            m.vertices=p.vertices;m.normals=p.normals;m.uv=p.uv;
            var triangles=(int[])p.triangles.Clone();
            // The exporter applies a rotation, not a reflection. Preserve winding.
            m.triangles=triangles;
            m.RecalculateBounds();m.RecalculateTangents();
            string path=Folder+"/mesh_"+(serial++)+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){EditorUtility.CopySerialized(m,saved);Object.DestroyImmediate(m);m=saved;}else AssetDatabase.CreateAsset(m,path);
            var go=new GameObject(p.name);go.transform.SetParent(p.group=="NativeTruckReference"?native:p.group=="PickupOnlyInfill"?pickup:common,false);
            go.AddComponent<MeshFilter>().sharedMesh=m;go.AddComponent<MeshRenderer>().sharedMaterial=materials[p.material];
            if(p.name=="body")bodyTransform=go.transform;
            else if(p.material=="body")painted.Add(go.transform);
        }
        if(!bodyTransform)throw new Exception("Original body paint root missing.");
        var paintGroup=Group(bodyTransform,"REBIRTHPickupOnlyPaint");
        foreach(var t in painted){t.SetParent(paintGroup,true);t.tag="LOD";}
        // Mirror VehiclePart.SetColors' includeInactive/LOD/single-material contract.
        // This proves native material sharing on the staged hierarchy without custom dye code.
        var renderers=bodyTransform.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).ToArray();
        foreach(var r in renderers.Skip(1))if(r.CompareTag("LOD")&&r.sharedMaterials.Length==1)r.sharedMaterial=renderers[0].sharedMaterial;
        var plate=root.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Original rear license plate");
        var report=new Report{status="TEXTURED_REVIEW_NOT_INSTALLED",meshes=serial,paintChildren=painted.Count,
            sharedDyeMaterial=renderers.All(r=>r.sharedMaterial==body),plateSeparate=plate.sharedMaterial!=body,
            sourceAtlasesRetained=true,paintShader=body.shader.name,plateTexture=AssetDatabase.GetAssetPath(plate.sharedMaterial.mainTexture),
            renders=new[]{"textured-original.png","textured-dye-red.png","textured-rear.png","textured-plate-detail.png"}};
        if(!report.sharedDyeMaterial||!report.plateSeparate)throw new Exception("Paint binding failure.");
        // A visual review prefab needs only the retained renderers. Remove references
        // to disabled camper/old prototype meshes without editing any source asset.
        foreach(var r in root.GetComponentsInChildren<Renderer>(true).Where(r=>!r.enabled).ToArray())
        {var filter=r.GetComponent<MeshFilter>();if(filter)Object.DestroyImmediate(filter);Object.DestroyImmediate(r);}
        foreach(var component in root.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(component);
        foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))if(component)Object.DestroyImmediate(component);
        PrefabUtility.SaveAsPrefabAsset(root,Folder+"/approved-pickup-textured.prefab");
        AssetDatabase.SaveAssets();
        SetupLighting();
        Render("textured-original",new Vector3(-5,3,-6),new Vector3(0,.85f,-.35f),2.17f);
        Render("textured-rear",new Vector3(0,.25f,-7),new Vector3(0,.84f,-.35f),1.15f);
        Render("textured-plate-detail",new Vector3(2,1,-6),new Vector3(0,.75f,-2.72f),.63f);
        body.color=new Color(190,45,35,1);
        Render("textured-dye-red",new Vector3(-5,3,-6),new Vector3(0,.85f,-.35f),2.17f);
        body.color=new Color(255,255,255,1);EditorUtility.SetDirty(body);AssetDatabase.SaveAssets();
        File.WriteAllText(Output+"/unity-texture-review.json",JsonUtility.ToJson(report,true));
        var changed=AssetDatabase.GetDependencies(Folder+"/approved-pickup-textured.prefab",true).Where(p=>p.StartsWith(Folder+"/")).ToArray();
        AssetDatabase.ExportPackage(changed,Output+"/approved-pickup-textured.unitypackage",ExportPackageOptions.Default);
        Debug.Log("APPROVED_TRUCK_TEXTURES_COMPLETE "+serial);
    }
    static void SetupLighting()
    {
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.55f,.59f,.65f);RenderSettings.ambientEquatorColor=new Color(.35f,.36f,.39f);RenderSettings.ambientGroundColor=new Color(.20f,.19f,.18f);
        RenderSettings.skybox=AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        var skycam=new GameObject("Reflection capture").AddComponent<Camera>();skycam.transform.position=new Vector3(0,100,0);skycam.cullingMask=0;skycam.clearFlags=CameraClearFlags.Skybox;
        var cube=new Cubemap(128,TextureFormat.RGBA32,true);skycam.RenderToCubemap(cube);RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflection=cube;Object.DestroyImmediate(skycam.gameObject);
        foreach(var spec in new[]{new Vector4(42,-35,0,1.15f),new Vector4(25,140,0,.65f),new Vector4(60,40,0,.45f)})
        {var l=new GameObject("Studio light").AddComponent<Light>();l.type=LightType.Directional;l.intensity=spec.w;l.transform.rotation=Quaternion.Euler(spec.x,spec.y,spec.z);l.shadows=LightShadows.Soft;}
        QualitySettings.antiAliasing=4;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;
    }
    static void Render(string name,Vector3 direction,Vector3 target,float size)
    {
        var cam=new GameObject("Review camera").AddComponent<Camera>();cam.transform.position=target+direction;cam.transform.LookAt(target);
        cam.orthographic=true;cam.orthographicSize=size;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.14f,.15f);cam.nearClipPlane=.05f;cam.farClipPlane=50;
        var rt=new RenderTexture(1800,1320,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();cam.targetTexture=rt;GL.invertCulling=false;cam.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());RenderTexture.active=previous;
        cam.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(cam.gameObject);
    }
}
