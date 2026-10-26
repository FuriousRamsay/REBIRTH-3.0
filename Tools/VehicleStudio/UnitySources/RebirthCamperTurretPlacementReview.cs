using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using Object=UnityEngine.Object;
public static class RebirthCamperTurretPlacementReview
{
 const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperTurretPositions_20261018";
 static readonly Vector3[] Points={new Vector3(0,3.015f,.22f),new Vector3(-.53f,3.015f,-2.92f),new Vector3(.53f,3.015f,-2.92f)};
 static readonly Color[] Colors={new Color(.1f,.85f,.95f),new Color(1,.7f,.08f),new Color(.75f,.35f,1)};
 static Transform Find(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
 public static void Run()
 {
  Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperDoorHardwareFix.Folder+"/rebirthCamper80s.prefab"));root.name="Latest approved armored camper";
  foreach(string name in new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow"})Find(root,name).gameObject.SetActive(true);
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.68f,.68f,.68f);var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.4f;key.transform.rotation=Quaternion.Euler(45,-35,0);var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.7f;fill.transform.rotation=Quaternion.Euler(35,145,0);
  var markers=new List<GameObject>();var labels=new List<Transform>();for(int i=0;i<Points.Length;i++)
  {
   var mat=new Material(Shader.Find("Unlit/Color"));mat.color=Colors[i];var marker=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Object.DestroyImmediate(marker.GetComponent<Collider>());marker.transform.position=Points[i];marker.transform.localScale=new Vector3(.50f,.012f,.50f);marker.GetComponent<Renderer>().sharedMaterial=mat;markers.Add(marker);
   var label=new GameObject("Position "+(char)('A'+i));label.transform.position=Points[i]+Vector3.up*.32f;var text=label.AddComponent<TextMesh>();text.text=((char)('A'+i)).ToString();text.fontSize=72;text.characterSize=.065f;text.anchor=TextAnchor.MiddleCenter;text.color=Colors[i];labels.Add(label.transform);
  }
  var camera=new GameObject("ReviewCamera").AddComponent<Camera>();camera.orthographic=true;camera.nearClipPlane=.01f;camera.backgroundColor=new Color(.12f,.14f,.16f);camera.clearFlags=CameraClearFlags.SolidColor;
  Find(root,"REBIRTHCampRoofStations").gameObject.SetActive(false);Find(root,"REBIRTHCampInteriorStations").gameObject.SetActive(false);Render(camera,new Vector3(6,5.6f,7),new Vector3(0,1.75f,-.25f),3.2f,"front-quarter",labels);Render(camera,new Vector3(-6,5.5f,-7),new Vector3(0,1.75f,-.25f),3.2f,"rear-quarter",labels);Render(camera,new Vector3(0,10,-.3f),new Vector3(0,0,-.3f),3.5f,"top-travel",labels);
  Find(root,"REBIRTHCampRoofStations").gameObject.SetActive(true);Render(camera,new Vector3(0,10,-.3f),new Vector3(0,0,-.3f),3.5f,"top-camp-clearance",labels);
  var lines=new List<string>{"Concept markers only. 0.50m diameter illustrative footprints; native turret prefab dimensions/sweep not validated. Latest armor+plough prefab retained; no vehicle asset or gameplay modification."};foreach(var r in Find(root,"REBIRTHCampRoofStations").GetComponentsInChildren<Renderer>(true))lines.Add(r.name+" center="+r.bounds.center.ToString("F3")+" size="+r.bounds.size.ToString("F3"));File.WriteAllLines(Output+"/placement-review.txt",lines);Object.DestroyImmediate(root);
 }
 static void Render(Camera cam,Vector3 eye,Vector3 target,float size,string name,List<Transform> labels)
 {
  cam.transform.position=eye;cam.transform.LookAt(target);cam.orthographicSize=size;foreach(var label in labels)label.rotation=cam.transform.rotation;var rt=new RenderTexture(1600,1200,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
 }
}
