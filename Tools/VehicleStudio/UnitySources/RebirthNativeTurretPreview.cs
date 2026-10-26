using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using Object=UnityEngine.Object;
public static class RebirthNativeTurretPreview
{
 const string Out=RebirthNativeTurretSurvey.Output;
 static Transform Find(GameObject r,string n)=>r.GetComponentsInChildren<Transform>(true).Single(t=>t.name==n);
 static Material steel;
 static GameObject Box(string n,Vector3 p,Vector3 s){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.position=p;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=steel;return g;}
 public static void Run(){
 Directory.CreateDirectory(Out);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
 var items=AssetBundle.LoadFromFile("C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Addressables/Standalone/automatic_assets_other/items.bundle");
 var bundle=AssetBundle.LoadFromFile(RebirthNativeTurretSurvey.Bundle);
 var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperDoorHardwareFix.Folder+"/rebirthCamper80s.prefab"));
 foreach(var n in new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow"})Find(root,n).gameObject.SetActive(true);
 foreach(var n in new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations"})Find(root,n).gameObject.SetActive(false);
 steel=new Material(Shader.Find("Standard"));steel.color=new Color(.18f,.17f,.15f);steel.SetFloat("_Metallic",.7f);steel.SetFloat("_Glossiness",.25f);
 var mats=new Dictionary<Material,Material>();var report=new List<string>{"Exact installed native turret prefabs, native scale 1. Preview maps native albedo/normal textures to Unity Standard because the game shader is unavailable in this editor. Concept mounts only; no install."};
 string[] types={"SMG","SMG","Shotgun","M60"};
 for(int i=0;i<4;i++){
 float x=i%2==0?-1.48f:1.48f,z=i<2?.58f:-3.25f;float yaw=i<2?(x<0?-45:45):(x<0?-135:135);
 Box("Outboard corner platform",new Vector3(x,3.14f,z),new Vector3(.91f,.10f,.91f));
 Box("Roof anchored outrigger",new Vector3(Mathf.Sign(x)*1.05f,3.04f,z),new Vector3(1.05f,.14f,.16f));
 var brace=Box("Gusset support",new Vector3(Mathf.Sign(x)*1.17f,2.93f,z),new Vector3(.63f,.10f,.13f));brace.transform.rotation=Quaternion.Euler(0,0,Mathf.Sign(x)*28);
 var path=bundle.GetAllAssetNames().Single(n=>n.ToLowerInvariant().EndsWith("autoturret"+types[i].ToLowerInvariant()+"prefab.prefab"));var turret=Object.Instantiate(bundle.LoadAsset<GameObject>(path));turret.name=types[i]+" corner "+i;
 foreach(var lod in turret.GetComponentsInChildren<LODGroup>(true)){lod.ForceLOD(0);lod.enabled=false;}
 foreach(var r in turret.GetComponentsInChildren<Renderer>(true)){
 if(r.name.Contains("turret_cone")||r.name.Contains("turret_laser")||r.name.EndsWith("LOD1")||r.name.EndsWith("LOD2")){r.enabled=false;continue;}r.enabled=true;
 r.sharedMaterials=r.sharedMaterials.Select(m=>{if(!m)return steel;if(!mats.ContainsKey(m)){var a=new Material(Shader.Find("Standard"));a.name=m.name+" preview";a.mainTexture=m.GetTexture("_MainTex");if(!a.mainTexture){var b=m.name.ToLowerInvariant().Contains("m60")?items:bundle;var tx=b.GetAllAssetNames().FirstOrDefault(n=>n.ToLowerInvariant().EndsWith(m.name.ToLowerInvariant()+".tga"));if(tx!=null)a.mainTexture=b.LoadAsset<Texture2D>(tx);}if(m.GetTexture("_Normal")){a.SetTexture("_BumpMap",m.GetTexture("_Normal"));a.EnableKeyword("_NORMALMAP");}a.SetFloat("_Metallic",.45f);a.SetFloat("_Glossiness",.25f);mats[m]=a;}return mats[m];}).ToArray();}
 turret.transform.position=new Vector3(x,3.19f,z);turret.transform.rotation=Quaternion.Euler(0,yaw,0);report.Add(types[i]+" "+turret.transform.position+" yaw="+yaw+" scale="+turret.transform.localScale);
 }
 RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.68f,.68f,.68f);var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.35f;key.transform.rotation=Quaternion.Euler(45,-35,0);var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.8f;fill.transform.rotation=Quaternion.Euler(35,145,0);
 var cam=new GameObject("Camera").AddComponent<Camera>();cam.orthographic=true;cam.nearClipPlane=.01f;cam.backgroundColor=new Color(.12f,.14f,.16f);cam.clearFlags=CameraClearFlags.SolidColor;
 Render(cam,new Vector3(7,6.5f,8),new Vector3(0,2,-.5f),3.5f,"front-quarter");Render(cam,new Vector3(-7,6.5f,-8),new Vector3(0,2,-.5f),3.5f,"rear-quarter");Render(cam,new Vector3(0,12,-.4f),new Vector3(0,0,-.4f),3.8f,"top");Render(cam,new Vector3(-4,5,-5),new Vector3(0,3.55f,-3.1f),1.65f,"rear-turrets-detail");File.WriteAllLines(Out+"/preview.txt",report);
 }
 static void Render(Camera c,Vector3 p,Vector3 target,float size,string n){c.transform.position=p;c.transform.LookAt(target);c.orthographicSize=size;var rt=new RenderTexture(1800,1400,24);c.targetTexture=rt;c.Render();RenderTexture.active=rt;var t=new Texture2D(1800,1400,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1800,1400),0,0);t.Apply();File.WriteAllBytes(Out+"/"+n+".png",t.EncodeToPNG());RenderTexture.active=null;c.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(t);}
}

