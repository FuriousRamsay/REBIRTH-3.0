using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

// Asset authoring/build only. Does not launch or exercise gameplay.
public static class RebirthCamperOwnerCorrections
{
    const string Folder="Assets/REBIRTHCamperOwnerCorrections_20261020_v3";
    const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/OwnerCorrections_20261020";
    static int serial;
    static readonly Dictionary<int,LODGroup> bodyLods=new Dictionary<int,LODGroup>();
    static Material Steel()
    {
        var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/REBIRTHCamperApprovedArmor_20261018_v4/plates.mat");
        var material=new Material(original){name="Camper ladder close-detail steel"};
        foreach(string property in new[]{"_MainTex","_BumpMap","_MetallicGlossMap","_OcclusionMap"})
        {
            var source=original.GetTexture(property) as Texture2D;if(!source)continue;
            var path=AssetDatabase.GetAssetPath(source);if(!path.EndsWith(".png",StringComparison.OrdinalIgnoreCase))continue;
            var decoded=new Texture2D(2,2,TextureFormat.RGBA32,false,property!="_MainTex");decoded.LoadImage(File.ReadAllBytes(path));
            int x=Mathf.RoundToInt(decoded.width*.01f),y=Mathf.RoundToInt(decoded.height*.015f),w=Mathf.RoundToInt(decoded.width*.07f),h=Mathf.RoundToInt(decoded.height*.07f);
            var tile=new Texture2D(w,h,TextureFormat.RGBA32,false,property!="_MainTex");tile.SetPixels(decoded.GetPixels(x,y,w,h));tile.Apply();
            string output=Folder+"/steel"+property+".png";File.WriteAllBytes(output,tile.EncodeToPNG());Object.DestroyImmediate(decoded);Object.DestroyImmediate(tile);
            AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(output);importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=16;importer.filterMode=FilterMode.Trilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;importer.sRGBTexture=property=="_MainTex";
            if(property=="_BumpMap")importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();
            material.SetTexture(property,AssetDatabase.LoadAssetAtPath<Texture2D>(output));
        }
        AssetDatabase.CreateAsset(material,Folder+"/LadderSteel.mat");return material;
    }
    static void Reproject(MeshFilter filter,Material material)
    {
        var source=filter.sharedMesh;if(!source)return;
        var vertices=source.vertices;var normals=source.normals;var triangles=source.triangles;
        var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var faces=new List<int>();
        var bounds=source.bounds;int axis=bounds.size.x>bounds.size.y?(bounds.size.x>bounds.size.z?0:2):(bounds.size.y>bounds.size.z?1:2);
        int u=(axis+1)%3,w=(axis+2)%3;float radius=Mathf.Max(.01f,Mathf.Min(bounds.size[u],bounds.size[w])*.5f);
        bool tube=filter.name.IndexOf("ladder",StringComparison.OrdinalIgnoreCase)>=0||filter.name.IndexOf("rung",StringComparison.OrdinalIgnoreCase)>=0;
        for(int i=0;i<triangles.Length;i+=3)
        {
            var coords=new Vector2[3];
            for(int j=0;j<3;j++)
            {
                var p=vertices[triangles[i+j]]-bounds.center;
                coords[j]=tube?new Vector2(Mathf.Atan2(p[w],p[u])/(2*Mathf.PI)+.5f,p[axis]/(2*Mathf.PI*radius)):new Vector2(p[u]/.14f,p[axis]/.14f);
            }
            if(tube&&coords.Max(c=>c.x)-coords.Min(c=>c.x)>.5f)for(int j=0;j<3;j++)if(coords[j].x<.5f)coords[j].x+=1;
            for(int j=0;j<3;j++){int index=triangles[i+j];faces.Add(v.Count);v.Add(vertices[index]);n.Add(normals.Length==vertices.Length?normals[index]:Vector3.up);uv.Add(coords[j]);}
        }
        var mesh=new Mesh{name=source.name+" detailed steel UV"};if(v.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(faces,0);mesh.RecalculateTangents();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Folder+"/SteelMesh"+(serial++)+".asset");filter.sharedMesh=mesh;filter.GetComponent<Renderer>().sharedMaterial=material;
    }
    static void BodyRoots(GameObject root)
    {
        var visual=root.transform.Find("GameObject/M/VisualBody");var module=new GameObject("REBIRTHCamperBodyModule").transform;module.SetParent(visual,false);
        foreach(var child in visual.Cast<Transform>().ToArray())if(child!=module&&child.name!="80sCamperTruck"&&child.name!="shadow_plane")child.SetParent(module,true);
        var truck=visual.Find("80sCamperTruck");
        foreach(string name in new[]{"camper_body","camper_interior"}){var child=truck.Find(name);if(child)child.SetParent(module,true);}
        // Keep the native cab glass and lights. Separate rear/camper triangles
        // from shared renderers without changing their vertex positions/materials.
        foreach(string name in new[]{"windows","lights","parts"})
        {
            var part=truck.Find(name);if(!part)continue;
            foreach(var filter in part.GetComponentsInChildren<MeshFilter>(true).ToArray())SplitRear(filter,module,visual);
        }
        var hull=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="CamperHull");if(hull)hull.SetParent(module,true);
        module.gameObject.SetActive(false);
    }
    static void FitConnections(GameObject root)
    {
        var body=root.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="camper_body");
        var probe=new GameObject("Owner model body surface probe");probe.transform.SetPositionAndRotation(body.transform.position,body.transform.rotation);probe.transform.localScale=body.transform.lossyScale;
        var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=body.sharedMesh;Physics.SyncTransforms();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Front ladder rigid fixing foot"))
        {
            var mesh=Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;var centre=filter.GetComponent<Renderer>().bounds.center;
            if(!collider.Raycast(new Ray(centre+Vector3.forward*.4f,Vector3.back),out var middle,1))throw new Exception("Fixing foot misses body skin");
            var normal=middle.normal;if(normal.z<0)normal=-normal;
            for(int i=0;i<vertices.Length;i++)
            {
                var point=filter.transform.TransformPoint(vertices[i]);float depth=Vector3.Dot(point-centre,normal);
                var projected=point-normal*Vector3.Dot(point-middle.point,normal);
                if(!collider.Raycast(new Ray(projected+normal*.2f,-normal),out var hit,.4f))throw new Exception("Fixing foot corner misses body skin");
                vertices[i]=filter.transform.InverseTransformPoint(hit.point+normal*(depth>=0?.0185f:.0005f));
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/FittedFoot"+(serial++)+".asset");filter.sharedMesh=mesh;
        }
        Object.DestroyImmediate(probe);
        var visual=root.transform.Find("GameObject/M/VisualBody");
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Split saddle on ladder left rail"||f.name=="Saddle connecting spine"))
        {
            var centre=filter.GetComponent<Renderer>().bounds.center;if(visual.InverseTransformPoint(centre).z> -2)continue;
            var rotation=Quaternion.FromToRotation(visual.up,visual.TransformDirection(new Vector3(0,1,.12f/1.26f).normalized));
            var mesh=Object.Instantiate(filter.sharedMesh);mesh.vertices=mesh.vertices.Select(v=>filter.transform.InverseTransformPoint(centre+rotation*(filter.transform.TransformPoint(v)-centre))).ToArray();
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/AlignedSaddle"+(serial++)+".asset");filter.sharedMesh=mesh;
        }
    }
    static void SplitRear(MeshFilter filter,Transform module,Transform visual)
    {
        var mesh=filter.sharedMesh;if(!mesh)return;var vertices=mesh.vertices;var cab=Object.Instantiate(mesh);var rear=Object.Instantiate(mesh);bool any=false;
        for(int s=0;s<mesh.subMeshCount;s++)
        {
            var a=new List<int>();var b=new List<int>();var triangles=mesh.GetTriangles(s);
            for(int i=0;i<triangles.Length;i+=3)
            {
                var centre=visual.InverseTransformPoint(filter.transform.TransformPoint((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3));
                var list=centre.z<.70f?b:a;if(list==b)any=true;list.Add(triangles[i]);list.Add(triangles[i+1]);list.Add(triangles[i+2]);
            }
            cab.SetTriangles(a,s);rear.SetTriangles(b,s);
        }
        if(!any){Object.DestroyImmediate(cab);Object.DestroyImmediate(rear);return;}
        AssetDatabase.CreateAsset(cab,Folder+"/TruckShared"+(serial++)+".asset");AssetDatabase.CreateAsset(rear,Folder+"/CamperShared"+(serial++)+".asset");
        var copy=new GameObject("Camper body "+filter.name);copy.transform.SetParent(module,false);copy.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);copy.transform.localScale=filter.transform.lossyScale;
        copy.AddComponent<MeshFilter>().sharedMesh=rear;var renderer=copy.AddComponent<MeshRenderer>();renderer.sharedMaterials=filter.GetComponent<Renderer>().sharedMaterials;filter.sharedMesh=cab;
        var original=filter.GetComponent<Renderer>();
        var sourceGroup=filter.GetComponentsInParent<LODGroup>(true).FirstOrDefault(g=>g.GetLODs().Any(l=>l.renderers.Contains(original)));
        if(sourceGroup)
        {
            if(!bodyLods.TryGetValue(sourceGroup.GetInstanceID(),out var group))
            {
                var go=new GameObject("Camper shared-part LODs");go.transform.SetParent(module,false);group=go.AddComponent<LODGroup>();
                group.SetLODs(sourceGroup.GetLODs().Select(l=>new LOD(l.screenRelativeTransitionHeight,Array.Empty<Renderer>()){fadeTransitionWidth=l.fadeTransitionWidth}).ToArray());bodyLods.Add(sourceGroup.GetInstanceID(),group);
            }
            var sourceLevels=sourceGroup.GetLODs();var levels=group.GetLODs();
            for(int i=0;i<levels.Length;i++)if(sourceLevels[i].renderers.Contains(original))levels[i].renderers=levels[i].renderers.Concat(new[]{renderer}).ToArray();
            group.SetLODs(levels);group.RecalculateBounds();
        }
    }
    public static void Build()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperTurretProduction_20261020/WithLods/rebirthCamper80s.prefab"));
        FitConnections(root);var material=Steel();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var r=filter.GetComponent<Renderer>();if(!r||!r.sharedMaterial||r.sharedMaterial.name!="plates")continue;
            string name=filter.name.ToLowerInvariant();if(new[]{"ladder","rung","saddle","stand off","fixing foot","welded","brace","platform","socket"}.Any(name.Contains))Reproject(filter,material);
        }
        BodyRoots(root);root.name="rebirthCamper80s";
        string prefab=Folder+"/rebirthCamper80s.prefab";PrefabUtility.SaveAsPrefabAsset(root,prefab);AssetDatabase.SaveAssets();Object.DestroyImmediate(root);
        RebirthAssetBundlePackaging.BuildAssetBundles(Output,new[]{new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{prefab}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        File.WriteAllText(Output+"/build-complete.txt",prefab);
        RenderBodyIcon(prefab);
    }
    public static void RenderBareClay(){RenderBodyIcon(Folder+"/rebirthCamper80s.prefab",true,true);}
    public static void RenderBareTruck(){RenderBodyIcon(Folder+"/rebirthCamper80s.prefab",true);}
    public static void RenderLatestIcon(){RenderBodyIcon(Folder+"/rebirthCamper80s.prefab");}
    static void RenderBodyIcon(string prefab,bool bare=false,bool clay=false)
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
        var module=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="REBIRTHCamperBodyModule");module.gameObject.SetActive(!bare);
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))if(bare?renderer.transform.IsChildOf(module):!renderer.transform.IsChildOf(module))renderer.enabled=false;
        foreach(var group in module.GetComponentsInChildren<LODGroup>(true))group.ForceLOD(0);
        foreach(var group in root.GetComponentsInChildren<LODGroup>(true))group.ForceLOD(0);
        var renderers=(bare?root.transform:module).GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
        if(bare)File.WriteAllLines(Output+"/bare-truck-meshes.txt",root.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.name+" active="+f.gameObject.activeInHierarchy+" bounds="+f.GetComponent<Renderer>()?.bounds));var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        if(clay){var clayMaterial=new Material(Shader.Find("Standard"));clayMaterial.color=new Color(.57f,.59f,.61f);clayMaterial.SetFloat("_Glossiness",.18f);foreach(var renderer in renderers)renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>clayMaterial).ToArray();}
        var camera=new GameObject("Camper body inventory camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=bounds.size.magnitude*.43f;camera.nearClipPlane=.01f;camera.farClipPlane=100;
        camera.transform.position=bounds.center+new Vector3(-7,4,-8);camera.transform.LookAt(bounds.center);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=clay?new Color(.12f,.14f,.16f):Color.clear;
        RenderSettings.ambientLight=new Color(.6f,.6f,.6f);RenderSettings.fog=false;
        var light=new GameObject("Inventory key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(35,-35,0);
        var fill=new GameObject("Inventory fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.65f;fill.transform.rotation=Quaternion.Euler(25,145,0);
        int resolution=clay?1400:512;var texture=new RenderTexture(resolution,resolution,24,RenderTextureFormat.ARGB32);camera.targetTexture=texture;camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;
        var png=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,resolution,resolution),0,0);png.Apply();File.WriteAllBytes(Output+(clay?"/bare-truck-clay-reference.png":bare?"/bare-truck-before.png":"/modVehicleCamperBody.png"),png.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(texture);Object.DestroyImmediate(png);Object.DestroyImmediate(root);
    }
}
