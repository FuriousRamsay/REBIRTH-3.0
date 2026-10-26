using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

public static class RebirthCamperCampFadeBuild
{
    const string Folder = "Assets/REBIRTHCamperTallIntegrated_20261014";
    const string Prefab = Folder + "/rebirthCamper80s.prefab";
    const string Output = "E:/_Haven/shared/REBIRTHTemp/CamperTallIntegrated_20261014";
    static readonly string[] Groups = { "REBIRTHCampRoofStations", "REBIRTHCampInteriorStations", "REBIRTHCampPowerEquipment", "REBIRTHCampStairVisuals" };
    static readonly Dictionary<string,string> Ids = new Dictionary<string,string> {
        {"stone forge","forge"},{"rusty fire pit","campfire"},{"tool bench","workbench"},{"cement mixer","cement_mixer"},
        {"industrial oven","iron_oven"},{"circular saw","circular_saw"},{"vintage sewing machine","outfit_designer"},
        {"recycling station","recycling_station"},{"shredder compact roof module","shredder"},{"ammo recycler","ammo_recycler"},
        {"rusted camping stove","gas_stovetop"},{"stone mortar pestle","mortar_pestle"},{"wooden toolbox","toolbox"},
        {"steampunk chemistry kit","chemistry_station"},{"steampunk distillation","distiller"},{"steampunk notebook","research_table"},
        {"industrial battery pack","battery_bank"},{"rusty industrial generator","generator"},{"solar panel","solar_bank"} };
    static Transform Find(GameObject root,string name) => root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    static Bounds BoundsOf(GameObject root) { var rr=root.GetComponentsInChildren<Renderer>(true); var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);return b; }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent)+"/"+t.name;
    static BoxCollider Target(Transform parent,string id) {
        var child=new GameObject("REBIRTHInteraction_"+id); child.transform.SetParent(parent,false);child.layer=0;
        var rr=parent.GetComponentsInChildren<Renderer>(true);var points=new List<Vector3>();
        foreach(var r in rr) {var b=r.bounds;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            points.Add(child.transform.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));}
        var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
        var c=child.AddComponent<BoxCollider>();c.center=bounds.center;c.size=bounds.size;c.isTrigger=true;c.enabled=false;return c;
    }
    public static void Build() {
        Directory.CreateDirectory(Output+"/Bundle");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperTallCandidate_20261014/SidePowerPlacementTrial.prefab"));root.name="rebirthCamper80s";
        var visual=Find(root,"VisualBody");var stairs=new GameObject(Groups[3]);stairs.transform.SetParent(visual,false);
        foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Stair tread"||t.name=="Stair stringer").ToArray())t.SetParent(stairs.transform,true);
        var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperPowerIntegrated_20261014/rebirthCamper80s.prefab");
        var solar=Object.Instantiate(Find(original,"solar panel").gameObject,Find(root,Groups[2]));solar.name="solar panel";solar.SetActive(true);
        var sb=BoundsOf(solar);solar.transform.localScale*=1.0f/sb.size.x;sb=BoundsOf(solar);
        solar.transform.position+=new Vector3(-sb.center.x,2.70f-sb.min.y,-3.57f-sb.center.z);
        var shader=Shader.Find("REBIRTH/CamperCampFade");if(shader==null)throw new Exception("Missing fade shader");
        int matIndex=0;var cloned=new Dictionary<Material,Material>();var manifest=new List<string>();int count=0;
        foreach(var gn in Groups) {
            var group=Find(root,gn);
            if(gn!=Groups[3])foreach(Transform station in group) {
                var key=station.name.Replace("(Clone)","").Trim();
                if(!Ids.TryGetValue(key,out var id))throw new Exception("Unknown station "+key);
                station.name=key;var c=Target(station,id);manifest.Add(id+"\t"+PathOf(c.transform)+"\tlocal center="+c.center+" size="+c.size);count++;
            }
            foreach(var renderer in group.GetComponentsInChildren<Renderer>(true)) {
                var mats=renderer.sharedMaterials;
                for(int i=0;i<mats.Length;i++) {
                    if(!cloned.TryGetValue(mats[i],out var copy)) {
                        copy=new Material(mats[i]);copy.name="CampFade_"+matIndex;copy.shader=shader;
                        copy.SetFloat("_CampVisibility",1);copy.renderQueue=-1;copy.SetOverrideTag("RenderType","Opaque");
                        copy.DisableKeyword("_ALPHABLEND_ON");copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");copy.DisableKeyword("_ALPHATEST_ON");
                        if(copy.GetTexture("_MetallicGlossMap")!=null)copy.EnableKeyword("_METALLICGLOSSMAP");else copy.DisableKeyword("_METALLICGLOSSMAP");
                        AssetDatabase.CreateAsset(copy,Folder+"/FadeMaterial_"+(matIndex++)+".mat");cloned.Add(mats[i],copy);
                    }mats[i]=copy;
                }renderer.sharedMaterials=mats;
            }
        }
        if(count!=19)throw new Exception("Expected 19 stations/power, got "+count);
        var door=Find(root,"REBIRTHRearDoorPivot");var dc=Target(door,"rear_door");manifest.Add("rear_door\t"+PathOf(dc.transform));
        SetPresentation(root,0,false);PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();
        File.WriteAllLines(Output+"/interaction-targets.tsv",manifest);
        var result=BuildPipeline.BuildAssetBundles(Output+"/Bundle",new[]{new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{Prefab}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        if(result==null)throw new Exception("Bundle build failed");
        File.WriteAllText(Output+"/build-receipt.txt","Built source prefab + Windows bundle; 19 station/power targets + rear door; source and installed original preserved.\n"+DateTime.UtcNow.ToString("O"));
    }
    static void SetPresentation(GameObject root,float opacity,bool camp) {
        RebirthCamperCampIntegration.SetMode(root,camp);
        foreach(var gn in Groups) {var group=Find(root,gn);group.gameObject.SetActive(opacity>0);
            foreach(var r in group.GetComponentsInChildren<Renderer>(true)) {var b=new MaterialPropertyBlock();b.SetFloat("_CampVisibility",opacity);r.SetPropertyBlock(b);}}
        foreach(var c in root.GetComponentsInChildren<Collider>(true).Where(c=>c.name.StartsWith("REBIRTHInteraction_")))c.enabled=camp;
    }
    public static void Verify() {
        Directory.CreateDirectory(Output+"/Renders");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var bundle=AssetBundle.LoadFromFile(Output+"/Bundle/rebirth_camper80s.unity3d");if(!bundle)throw new Exception("Bundle reopen failed");
        var root=Object.Instantiate(bundle.LoadAsset<GameObject>("rebirthCamper80s"));var targets=root.GetComponentsInChildren<Collider>(true).Where(c=>c.name.StartsWith("REBIRTHInteraction_")).ToArray();
        if(targets.Length!=20||targets.Any(c=>!c.isTrigger||c.enabled))throw new Exception("Target defaults wrong");
        if(root.GetComponentsInChildren<MonoBehaviour>(true).Any(m=>m==null))throw new Exception("Missing scripts");
        foreach(var gn in Groups) {
            var g=Find(root,gn);if(g.gameObject.activeSelf)throw new Exception("Not Travel default: "+gn);
            foreach(var r in g.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials) {
                if(!m.HasProperty("_CampVisibility")||!m.shader.isSupported)throw new Exception("Fade shader failure");
                foreach(var n in m.GetTexturePropertyNames()){var t=m.GetTexture(n);if(t&&(t.width>2048||t.height>2048))throw new Exception("Texture exceeds 2K: "+t.name);}
            }
        }
        if(Find(root,"shadow_plane").GetComponent<Renderer>().enabled)throw new Exception("Shadow plane reenabled");
        var rails=Find(root,"Fitted rooftop guardrails");if(!rails.gameObject.activeInHierarchy)throw new Exception("Rails hidden");
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
        var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.transform.rotation=Quaternion.Euler(45,-35,0);
        var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.55f;fill.transform.rotation=Quaternion.Euler(25,135,0);
        var cam=new GameObject("Camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.14f,.16f);cam.nearClipPlane=.01f;cam.orthographic=true;cam.orthographicSize=3.5f;
        foreach(var r in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="body"||r.name=="camper_body")){var b=new MaterialPropertyBlock();b.SetFloat("_PatternEnabled",1);r.SetPropertyBlock(b);}
        foreach(var phase in new[]{0,.5f,1}) {SetPresentation(root,phase,phase==1);Save(cam,new Vector3(-5,4.2f,-6),"phase-"+phase.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if(targets.Any(c=>c.enabled!=(phase==1)))throw new Exception("Interaction phase mismatch");}
        Save(cam,new Vector3(5,4.2f,-6),"camp-opposite");
        cam.orthographic=false;cam.fieldOfView=75;Save(cam,new Vector3(0,1.7f,-3.65f),"camp-inside",new Vector3(0,1.6f,-.3f));
        File.WriteAllText(Output+"/verify-receipt.txt","PASS reopened Windows bundle: 19 station/power targets + door; trigger defaults off; 2K textures; all fade shaders supported; Travel/half/Camp rendering and collider gating; rails visible; shadow plane off; no missing scripts. Unity preview only, no gameplay validation.\n"+DateTime.UtcNow.ToString("O"));
    }
    static void Save(Camera c,Vector3 eye,string name,Vector3? target=null) {
        c.transform.position=eye;c.transform.LookAt(target??new Vector3(0,1.8f,-1.2f));var rt=new RenderTexture(1600,1200,24);c.targetTexture=rt;c.Render();RenderTexture.active=rt;
        var im=new Texture2D(1600,1200,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,1600,1200),0,0);im.Apply();File.WriteAllBytes(Output+"/Renders/"+name+".png",im.EncodeToPNG());c.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(im);Object.DestroyImmediate(rt);
    }
}
