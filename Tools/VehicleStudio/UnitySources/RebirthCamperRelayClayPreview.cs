using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
public static class RebirthCamperRelayClayPreview
{
 const string Out="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RelayPlacement_20261020";
 static Material clay,relay;
 public static void Run()
 {
  Directory.CreateDirectory(Out);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperTurretProduction_20261020/WithLods/rebirthCamper80s.prefab");
  if(!asset)throw new Exception("Production camper missing");
  var root=Object.Instantiate(asset);root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;
  foreach(var g in root.GetComponentsInChildren<LODGroup>(true))g.ForceLOD(0);
  clay=new Material(Shader.Find("Standard"));clay.color=new Color(.56f,.57f,.59f);clay.SetFloat("_Glossiness",.18f);
  relay=new Material(clay);relay.color=new Color(.72f,.73f,.75f);
  foreach(var r in root.GetComponentsInChildren<Renderer>(true))
  {r.SetPropertyBlock(null);r.sharedMaterials=Enumerable.Repeat(clay,r.sharedMaterials.Length).ToArray();}
  var target=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHInteraction_battery_bank");
  var box=target.GetComponent<BoxCollider>();var center=target.TransformPoint(box.center);
  var report="Actual production camper with proposed relay proxy geometry; no prefab/bundle/game edits.\nBattery target="+center+"\n";
  // Exact opposite wall placement at the battery's longitudinal station.
  float x=Mathf.Abs(center.x)+.04f,y=center.y+.43f,z=center.z;
  for(int side=-1;side<=1;side+=2)
  {
   var p=new GameObject(side<0?"Proposed left relay":"Proposed right relay").transform;p.SetParent(root.transform,false);
   p.position=new Vector3(side*x,y,z);p.rotation=Quaternion.LookRotation(Vector3.right*side,Vector3.up);
   Part(p,PrimitiveType.Cube,"Wall mounting plate",Vector3.zero,new Vector3(.23f,.18f,.018f));
   foreach(float xx in new[]{-.065f,.065f})
   {
    Part(p,PrimitiveType.Cylinder,"Insulator",new Vector3(xx,0,.055f),new Vector3(.052f,.04f,.052f)).localRotation=Quaternion.Euler(90,0,0);
    foreach(float zz in new[]{.035f,.06f,.085f})Part(p,PrimitiveType.Cylinder,"Insulator rib",new Vector3(xx,0,zz),new Vector3(.072f,.006f,.072f)).localRotation=Quaternion.Euler(90,0,0);
    Part(p,PrimitiveType.Sphere,"Terminal",new Vector3(xx,0,.10f),Vector3.one*.038f);
   }
   foreach(float xx in new[]{-.09f,.09f})foreach(float yy in new[]{-.06f,.06f})Part(p,PrimitiveType.Cylinder,"Fixing bolt",new Vector3(xx,yy,.018f),new Vector3(.02f,.008f,.02f)).localRotation=Quaternion.Euler(90,0,0);
   report+=p.name+"="+p.position+"\n";
  }
  RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.52f,.52f,.52f);
  Light("Key",new Vector3(35,-45,0),1);Light("Fill",new Vector3(30,135,0),.65f);
  var cam=new GameObject("Clay camera").AddComponent<Camera>();cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.105f,.115f,.13f);cam.nearClipPlane=.01f;cam.farClipPlane=100;
  foreach(int side in new[]{-1,1})
  {
   Render(cam,new Vector3(side*9,y+.45f,z+1.5f),new Vector3(0,y-.30f,z),1.65f,side<0?"relay-left-clay":"relay-right-clay");
   Render(cam,new Vector3(side*7,y+.12f,z+.35f),new Vector3(side*x,y-.18f,z),.68f,side<0?"relay-left-detail":"relay-right-detail");
  }
  File.WriteAllText(Out+"/placement.txt",report);
 }
 static Transform Part(Transform p,PrimitiveType type,string name,Vector3 pos,Vector3 scale){var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=relay;Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;}
 static void Light(string n,Vector3 e,float power){var l=new GameObject(n).AddComponent<Light>();l.type=LightType.Directional;l.transform.rotation=Quaternion.Euler(e);l.intensity=power;}
 static void Render(Camera cam,Vector3 eye,Vector3 target,float size,string name){cam.transform.position=eye;cam.transform.LookAt(target);cam.orthographicSize=size;var rt=new RenderTexture(1500,1100,24){antiAliasing=4};var im=new Texture2D(1500,1100,TextureFormat.RGB24,false);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;im.ReadPixels(new Rect(0,0,1500,1100),0,0);im.Apply();File.WriteAllBytes(Out+"/"+name+".png",im.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(im);}
}
