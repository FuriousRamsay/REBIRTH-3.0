using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using Object=UnityEngine.Object;
public static class RebirthCamperDoorHardwareFix
{
 public const string Folder="Assets/REBIRTHCamperDoorHardware_20261018_v1";
 public const string Output=RebirthCamperDoorHardwareAudit.Output;
 static Transform Find(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
 public static void Author()
 {
  Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperRearFit.Folder+"/rebirthCamper80s.prefab"));root.name="rebirthCamper80s";
  var source=Find(root,"REBIRTHArmorSurface_grilles").GetComponent<MeshFilter>();var components=RebirthCamperDoorHardwareAudit.Components(source).Where(RebirthCamperDoorHardwareAudit.InOpening).ToArray();if(components.Length!=8||components.Sum(c=>c.Indices.Count)!=5376)throw new Exception("Unexpected orphan inventory");
  var selected=new HashSet<int>(components.SelectMany(c=>c.Indices));var old=source.sharedMesh;var oldIndices=old.triangles;var kept=new List<int>();var moved=new List<int>();
  foreach(Transform range in source.transform)
  {
   var a=range.name.Split('_');int start=kept.Count;int count=int.Parse(a[4]),begin=int.Parse(a[3]);
   for(int i=begin;i<begin+count;i+=3){bool remove=selected.Contains(oldIndices[i]);for(int j=0;j<3;j++)if(remove)moved.Add(oldIndices[i+j]);else kept.Add(oldIndices[i+j]);}
   range.name=$"ArmorRange_{a[1]}_{a[2]}_{start}_{kept.Count-start}";
  }
  var staticMesh=Object.Instantiate(old);staticMesh.name="Stationary grilles without door fasteners";staticMesh.SetTriangles(kept,0);AssetDatabase.CreateAsset(staticMesh,Folder+"/StaticGrilles.asset");source.sharedMesh=staticMesh;
  var go=new GameObject("REBIRTHArmorSurface_doorHardware");go.transform.SetParent(Find(root,"REBIRTHModArmorDoor"),false);go.layer=source.gameObject.layer;
  var remap=new Dictionary<int,int>();var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();var ov=old.vertices;var on=old.normals;var ot=old.uv;
  foreach(int index in moved)
  {
   if(!remap.TryGetValue(index,out int mapped))
   {
    mapped=vertices.Count;remap.Add(index,mapped);var point=source.transform.TransformPoint(ov[index]);var n=source.transform.TransformDirection(on[index]);point.x*=1.3f;n.x/=1.3f;
    if(point.y>2.2f&&point.y<2.85f){float y=point.y;point.y+=y<=2.6f?.16f*(y-2.2f)/.4f:.16f*(2.85f-y)/.25f;n.y/=y<=2.6f?1.4f:.36f;}
    vertices.Add(go.transform.InverseTransformPoint(point));normals.Add(go.transform.InverseTransformDirection(n.normalized));uv.Add(ot[index]);
   }
   indices.Add(mapped);
  }
  var mesh=new Mesh{name="Four rear door bolt and washer pairs"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,Folder+"/DoorHardware.asset");go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=source.GetComponent<MeshRenderer>().sharedMaterial;
  var marker=new GameObject("ArmorRange_15_71_0_"+indices.Count);marker.transform.SetParent(go.transform,false);
  if(kept.Count+indices.Count!=oldIndices.Length)throw new Exception("Triangle conservation failed");
  PrefabUtility.SaveAsPrefabAsset(root,Folder+"/rebirthCamper80s.prefab");AssetDatabase.SaveAssets();
  RebirthCamperCampIntegration.SetMode(root,true);foreach(string name in new[]{"REBIRTHModArmor","REBIRTHModArmorDoor"})Find(root,name).gameObject.SetActive(true);
  var pivot=Find(root,"REBIRTHRearDoorPivot");pivot.localRotation=Find(root,"REBIRTHRearDoorOpenPose").localRotation;
  if(RebirthCamperDoorHardwareAudit.Components(source).Any(RebirthCamperDoorHardwareAudit.InOpening))throw new Exception("Stationary geometry still floats in opening");
  Render(new Vector3(0,1.85f,-1.9f),new Vector3(0,1.75f,-3.5f),"inside-open");Render(new Vector3(1.7f,2,-5.6f),new Vector3(.25f,1.9f,-3.35f),"outside-open-hardware");pivot.localRotation=Quaternion.identity;Render(new Vector3(0,1.85f,-1.9f),new Vector3(0,1.75f,-3.5f),"inside-closed");
  Object.DestroyImmediate(root);File.WriteAllText(Output+"/author-proof.txt","PASS: four washer/bolt pairs (8 components,1792 triangles) moved from stationary grilles to moving door, adjusted to widened/taller guard; original texture UVs/material preserved; triangles conserved; zero stationary components inside opening; damage group15 at71% shared with rear-door grille. Four armor surfaces but54 logical damage pieces.");Build();
 }
 public static void Build()
 {
  Directory.CreateDirectory(Output+"/Bundles");var build=new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{Folder+"/rebirthCamper80s.prefab"},addressableNames=new[]{"rebirthCamper80s"}};if(!BuildPipeline.BuildAssetBundles(Output+"/Bundles",new[]{build},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("Build failed");
  string path=Output+"/Bundles/rebirth_camper80s.unity3d";if(!BuildPipeline.GetCRCForAssetBundle(path,out uint crc))throw new Exception("CRC missing");var bundle=AssetBundle.LoadFromFile(path,crc);if(!bundle)throw new Exception("CRC reopen failed");
  try{var root=Object.Instantiate(bundle.LoadAsset<GameObject>("rebirthCamper80s"));var ts=root.GetComponentsInChildren<Transform>(true);var ranges=ts.Where(t=>t.name.StartsWith("ArmorRange_")).ToArray();if(ranges.Length!=55||ranges.Select(t=>t.name.Split('_')[1]).Distinct().Count()!=54)throw new Exception("Damage IDs");if(Find(root,"REBIRTHModPlowHull").GetComponentsInChildren<MeshCollider>(true).Length!=27||root.GetComponentsInChildren<WheelCollider>(true).Length!=4)throw new Exception("Rig/collision changed");if(!Find(root,"REBIRTHArmorSurface_doorHardware").IsChildOf(Find(root,"REBIRTHRearDoorPivot")))throw new Exception("Wrong hardware parent");Object.DestroyImmediate(root);File.WriteAllText(Output+"/verified.txt","PASS CRC="+crc+"; 54 logical damage pieces/55 ranges; hardware follows door and grille damage; 27 plough colliders; 4 wheels; no gameplay test.");}finally{bundle.Unload(true);}
 }
 static void Render(Vector3 eye,Vector3 target,string label)
 {
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.7f,.7f,.7f);var light=new GameObject().AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(35,155,0);var cam=new GameObject().AddComponent<Camera>();cam.nearClipPlane=.01f;cam.fieldOfView=65;cam.transform.position=eye;cam.transform.LookAt(target);cam.backgroundColor=new Color(.18f,.22f,.25f);cam.clearFlags=CameraClearFlags.SolidColor;var rt=new RenderTexture(1280,1000,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var img=new Texture2D(1280,1000,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,1000),0,0);img.Apply();File.WriteAllBytes(Output+"/"+label+".png",img.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;Object.DestroyImmediate(img);Object.DestroyImmediate(rt);Object.DestroyImmediate(cam.gameObject);Object.DestroyImmediate(light.gameObject);
 }
}
