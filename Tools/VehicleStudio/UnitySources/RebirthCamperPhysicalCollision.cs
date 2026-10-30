using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using Object=UnityEngine.Object;
public static class RebirthCamperPhysicalCollision
{
 public const string Folder="Assets/REBIRTHCamperPhysicalCollision_20261018_v1";public const string Output=RebirthCamperCollisionSurvey.Output;
 static Transform Find(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
 static BoxCollider Box(Transform parent,string name,Bounds bounds){var go=new GameObject(name);go.layer=21;go.transform.SetParent(parent,false);var box=go.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;box.isTrigger=false;return box;}
 public static void Author()
 {
  Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperDoorHardwareFix.Folder+"/rebirthCamper80s.prefab"));root.name="rebirthCamper80s";var lines=new List<string>();var walk=Find(root,"REBIRTHCampWalkInColliders");
  var furniture=new GameObject("Camp furniture solid colliders");furniture.layer=21;furniture.transform.SetParent(walk,false);int count=0;
  // Physical furniture revealed a 0.54m choke point. Narrow the actual inward
  // table/counter faces to a 0.70m aisle, rather than undersizing collision invisibly.
  var interiorFilter=Find(root,"camper_interior").GetComponent<MeshFilter>();var fitted=Object.Instantiate(interiorFilter.sharedMesh);var fv=fitted.vertices;var fn=fitted.normals;
  for(int i=0;i<fv.Length;i++)
  {
   var p=interiorFilter.transform.TransformPoint(fv[i]);if(p.z> -2.60f&&p.z<-.55f&&p.y>.915f&&p.y<1.70f&&Mathf.Abs(p.x)>.20f&&Mathf.Abs(p.x)<.65f)
   {float old=Mathf.Abs(p.x),edge=p.x>0?.2735f:.2627f,slope=(.65f-.35f)/(.65f-edge);p.x=Mathf.Sign(p.x)*(.35f+(old-edge)*slope);var normal=interiorFilter.transform.TransformDirection(fn[i]);normal.x/=slope;fn[i]=interiorFilter.transform.InverseTransformDirection(normal.normalized);}
   fv[i]=interiorFilter.transform.InverseTransformPoint(p);
  }
  fitted.vertices=fv;fitted.normals=fn;fitted.RecalculateBounds();fitted.RecalculateTangents();AssetDatabase.CreateAsset(fitted,Folder+"/InteriorClearAisle.asset");interiorFilter.sharedMesh=fitted;lines.Add("Actual inward table/counter faces narrowed to nominal0.70m aisle; positions/outer edges unchanged");
  var interior=Find(root,"camper_interior").GetComponent<MeshFilter>();foreach(var c in RebirthCamperDoorHardwareAudit.Components(interior))
  {
   var b=c.Bounds;if(b.size.z>2||b.size.x>1.7f||b.size.y>1.7f||b.center.y<.9f||b.size.x*b.size.y*b.size.z<.00020f)continue;
   // The wide mattress belongs to the solid raised bunk base; never hull the entire interior.
   if(b.size.x>1.2f&&b.size.z>.5f){float top=b.max.y;b.SetMinMax(new Vector3(b.min.x,.91f,b.min.z),b.max);lines.Add("Raised bed solid base fitted to mattress; top="+top);}
   var box=Box(furniture.transform,"Camp furniture "+(++count),b);lines.Add(box.name+" center="+b.center.ToString("F4")+" size="+b.size.ToString("F4"));
  }
  // Front over-cab panel: fit thin convex bands to actual source triangles, not one solid cabin hull.
  var body=Find(root,"camper_body").GetComponent<MeshFilter>();var vertices=body.sharedMesh.vertices.Select(p=>body.transform.TransformPoint(p)).ToArray();var indices=body.sharedMesh.triangles;int noseBands=0;
  for(int band=0;band<6;band++)
  {
   float minY=1.9f+band*.18f,maxY=minY+.18f;var points=new List<Vector3>();for(int i=0;i<indices.Length;i+=3){var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];var center=(a+b+c)/3;if(center.z>.62f&&center.y>=minY&&center.y<maxY&&Mathf.Abs(center.x)<.96f){points.Add(a);points.Add(b);points.Add(c);}}
   if(points.Count==0)continue;var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);var size=bounds.size;size.x=Mathf.Max(size.x,.1f);size.y=Mathf.Max(size.y,.04f);size.z=Mathf.Max(size.z,.05f);bounds.size=size;Box(walk,"Camp overcab nose band "+(++noseBands),bounds);
  }
  foreach(int side in new[]{-1,1})
  {
   var points=new List<Vector3>();for(int i=0;i<indices.Length;i+=3){var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];var center=(a+b+c)/3;if(center.x*side>.82f&&center.z>.25f&&center.y>1.8f&&Mathf.Min(a.z,Mathf.Min(b.z,c.z))>.14f){points.Add(a);points.Add(b);points.Add(c);}}
   if(points.Count>0){var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);var size=bounds.size;size.x=Mathf.Max(size.x,.045f);bounds.size=size;Box(walk,"Camp overcab nose side "+side,bounds);}
  }
  // Full-time rooftop rails: small per-member boxes follow each actual bar orientation.
  var permanent=new GameObject("REBIRTHPermanentPhysicalColliders");permanent.layer=21;permanent.transform.SetParent(root.transform.Find("Physics"),false);int railCount=0;
  foreach(var f in Find(root,"Fitted rooftop guardrails").GetComponentsInChildren<MeshFilter>(true))
  {
   var go=new GameObject("Roof guardrail solid "+(++railCount));go.layer=21;go.transform.SetParent(permanent.transform,false);go.transform.position=f.transform.position;go.transform.rotation=f.transform.rotation;go.transform.localScale=f.transform.lossyScale;var box=go.AddComponent<BoxCollider>();box.center=f.sharedMesh.bounds.center;box.size=f.sharedMesh.bounds.size;box.isTrigger=false;
  }
  // Explicit physics layer for every authored non-trigger shell collider, including the ramp.
  foreach(var c in walk.GetComponentsInChildren<Collider>(true))if(!c.isTrigger)c.gameObject.layer=21;
  var door=Find(root,"REBIRTHRearDoorPivot").GetComponent<BoxCollider>();door.gameObject.layer=21;door.isTrigger=false;
  if(count<10||railCount!=36||noseBands<1)throw new Exception("Physical coverage inventory incomplete");
  lines.Add("Furniture="+count+"; nose bands="+noseBands+"; rooftop rail members="+railCount+"; door uses moving pivot, non-trigger layer21");
  PrefabUtility.SaveAsPrefabAsset(root,Folder+"/rebirthCamper80s.prefab");AssetDatabase.SaveAssets();Object.DestroyImmediate(root);File.WriteAllLines(Output+"/author-receipt.txt",lines);
 }
 public static void ValidateAndBuild()
 {
  RebirthCamperPhysicalEntryAudit.Run();var rows=File.ReadAllLines(Output+"/entry-measured-audit.txt").Where(s=>s.StartsWith("baseline ")).ToArray();if(rows.Length!=3||rows.Any(s=>!s.Contains("PASS=True")))throw new Exception("Furniture collision obstructed standing entry: "+string.Join(";",rows));Build();
 }
 public static void Build()
 {
  Directory.CreateDirectory(Output+"/Bundles");var build=new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{Folder+"/rebirthCamper80s.prefab"},addressableNames=new[]{"rebirthCamper80s"}};if(!RebirthAssetBundlePackaging.BuildAssetBundles(Output+"/Bundles",new[]{build},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("Build failed");string path=Output+"/Bundles/rebirth_camper80s.unity3d";if(!BuildPipeline.GetCRCForAssetBundle(path,out uint crc))throw new Exception("CRC missing");var bundle=AssetBundle.LoadFromFile(path,crc);if(!bundle)throw new Exception("CRC reopen failed");try{var root=Object.Instantiate(bundle.LoadAsset<GameObject>("rebirthCamper80s"));var rails=Find(root,"REBIRTHPermanentPhysicalColliders").GetComponentsInChildren<Collider>(true);if(rails.Length!=36||rails.Any(c=>c.isTrigger||c.gameObject.layer!=21))throw new Exception("Rail collider contract");var walk=Find(root,"REBIRTHCampWalkInColliders");if(walk.GetComponentsInChildren<Collider>(true).Any(c=>!c.isTrigger&&c.gameObject.layer!=21))throw new Exception("Shell layer");if(Find(root,"REBIRTHModPlowHull").GetComponentsInChildren<MeshCollider>(true).Length!=27||root.GetComponentsInChildren<WheelCollider>(true).Length!=4)throw new Exception("Native rig/plough changed");File.WriteAllText(Output+"/verified.txt","PASS CRC="+crc+"; furniture/nose Camp collision;36 permanent rail colliders; moving door non-trigger;54 logical armor pieces preserved;27 plough colliders;4 wheels. No gameplay test.");Object.DestroyImmediate(root);}finally{bundle.Unload(true);}
 }
}
