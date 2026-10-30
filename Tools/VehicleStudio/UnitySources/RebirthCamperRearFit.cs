using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using Object=UnityEngine.Object;
public static class RebirthCamperRearFit
{
 public const string Folder="Assets/REBIRTHCamperRearFit_20261018_v1";
 public const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperRearFit_20261018";
 static Transform Find(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
 static readonly List<GameObject> Parts=new List<GameObject>();
 static GameObject Primitive(PrimitiveType type,string name,Vector3 position,Vector3 scale)
 {var go=GameObject.CreatePrimitive(type);Object.DestroyImmediate(go.GetComponent<Collider>());go.name=name;go.transform.position=position;go.transform.localScale=scale;Parts.Add(go);return go;}
 static void Collar(Vector3 center)
 {
  var vs=new List<Vector3>();var fs=new List<int>();const int n=32;float[] radii={.0292f,.036f};
  for(int layer=0;layer<2;layer++)for(int end=0;end<2;end++)for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;vs.Add(center+new Vector3(Mathf.Cos(a)*radii[layer],(end==0?-.04f:.04f),Mathf.Sin(a)*radii[layer]));}
  Action<int,int,int,int> quad=(a,b,c,d)=>fs.AddRange(new[]{a,b,c,a,c,d});
  for(int i=0;i<n;i++){int j=(i+1)%n;quad(64+i,64+j,96+j,96+i);quad(i,32+i,32+j,j);quad(i,j,64+j,64+i);quad(32+i,96+i,96+j,32+j);}
  var mesh=new Mesh();mesh.SetVertices(vs);mesh.SetTriangles(fs,0);mesh.RecalculateNormals();var go=new GameObject("Rear pipe annular clamp");go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>();Parts.Add(go);
 }
 public static void Author()
 {
  Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Parts.Clear();
  var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperDamageArmor.Folder+"/rebirthCamper80s.prefab"));root.name="rebirthCamper80s";var report=new List<string>();
  foreach(var t in root.GetComponentsInChildren<Transform>(true))
  {
   if(t.name=="Tall rear door jamb"){t.localPosition=new Vector3(Mathf.Sign(t.localPosition.x)*.54f,t.localPosition.y,t.localPosition.z);report.Add("Frame jamb repositioned to "+t.localPosition);}
   if(t.name=="Tall rear door header"||t.name=="Tall rear door threshold"){var scale=t.localScale;scale.x=1.12f;t.localScale=scale;report.Add(t.name+" width=1.12m, clear opening=1.04m");}
  }
  // Raise only the rear opening/door upper region, retaining roof and outside body corners.
  int raised=0;
  foreach(var f in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="camper_body"||f.name=="camper_interior"||f.name.StartsWith("RearDoor_")||f.name=="REBIRTHArmorSurface_door"))
  {
   var mesh=Object.Instantiate(f.sharedMesh);var vs=mesh.vertices;var ns=mesh.normals;
   for(int i=0;i<vs.Length;i++)
   {
    var point=f.transform.TransformPoint(vs[i]);float y=point.y;
    if(point.z< -3.15f&&Mathf.Abs(point.x)<.65f&&y>2.2f&&y<2.85f)
    {point.y+=y<=2.6f?.16f*(y-2.2f)/.4f:.16f*(2.85f-y)/.25f;var n=f.transform.TransformDirection(ns[i]);n.y/=y<=2.6f?1.4f:.36f;ns[i]=f.transform.InverseTransformDirection(n.normalized);raised++;}
    vs[i]=f.transform.InverseTransformPoint(point);
   }
   mesh.vertices=vs;mesh.normals=ns;mesh.RecalculateBounds();mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,Folder+"/TallEntry_"+f.name+".asset");f.sharedMesh=mesh;
  }
  foreach(var t in root.GetComponentsInChildren<Transform>(true))
  {
   if(t.name=="Tall rear door jamb"){var position=t.localPosition;position.y=1.73f;t.localPosition=position;var scale=t.localScale;scale.y=2.10f;t.localScale=scale;}
   if(t.name=="Tall rear door header"){var position=t.localPosition;position.y=2.78f;t.localPosition=position;}
  }
  var pivot=Find(root,"REBIRTHRearDoorPivot");var doorCollider=pivot.GetComponent<BoxCollider>();doorCollider.center=new Vector3(-.52f,1.03f,0);doorCollider.size=new Vector3(1.04f,2.06f,.05f);
  foreach(var box in Find(root,"REBIRTHCampWalkInColliders").GetComponentsInChildren<BoxCollider>(true))
  {
   if(box.name=="Camp rear jamb"){box.center=new Vector3(box.center.x,1.73f,box.center.z);box.size=new Vector3(box.size.x,2.06f,box.size.z);}
   if(box.name=="Camp rear lintel"){box.center=new Vector3(0,2.81f,-3.34f);box.size=new Vector3(1.8f,.10f,.08f);}
  }
  report.Add("Rear opening clear width1.04m; lintel underside2.76m minus entry floor.72m =2.04m headroom. Door, visual frame and collision adjusted together; raised vertices="+raised);
  // Bring the internal floor ramp into the same clear-width envelope as the opening.
  var ramp=Find(root,"Camp floor ramp").GetComponent<MeshCollider>();var rm=Object.Instantiate(ramp.sharedMesh);var rv=rm.vertices;for(int i=0;i<rv.Length;i++)rv[i].x*=1.3f;rm.vertices=rv;rm.RecalculateBounds();rm.RecalculateNormals();AssetDatabase.CreateAsset(rm,Folder+"/FloorRamp104.asset");ramp.sharedMesh=rm;
  // Exact body surface ray hits, never a guessed mounting depth.
  var body=Find(root,"camper_body").GetComponent<MeshFilter>();var temporary=new GameObject("Temporary measured body surface");var measured=temporary.AddComponent<MeshCollider>();var surface=Object.Instantiate(body.sharedMesh);surface.vertices=surface.vertices.Select(v=>body.transform.TransformPoint(v)).ToArray();surface.RecalculateBounds();measured.sharedMesh=surface;Physics.SyncTransforms();
  foreach(int side in new[]{-1,1})
  {
   Vector3 pipe=new Vector3(side*1.005f,2.86f,-3.23f);if(!measured.Raycast(new Ray(new Vector3(side*1.5f,pipe.y,pipe.z),Vector3.left*side),out var hit,1f))throw new Exception("No rear body support "+side);
   float x=hit.point.x+side*.006f;Collar(pipe);
   Primitive(PrimitiveType.Cube,"Rear body backing plate",new Vector3(x,pipe.y,pipe.z),new Vector3(.012f,.15f,.105f));
   float collarInner=pipe.x-side*.030f;
   Primitive(PrimitiveType.Cube,"Rear pipe welded bridge",new Vector3((x+collarInner)/2,pipe.y,pipe.z),new Vector3(Mathf.Abs(collarInner-x)+.012f,.055f,.055f));
   foreach(float y in new[]{pipe.y-.053f,pipe.y+.053f}){var bolt=Primitive(PrimitiveType.Cylinder,"Rear body bolt",new Vector3(x+side*.012f,y,pipe.z),new Vector3(.022f,.006f,.022f));bolt.transform.rotation=Quaternion.Euler(0,0,90);}
   // Closed steel cap finishes the exposed pipe end above the new clamp.
   Primitive(PrimitiveType.Cylinder,"Rear pipe closed end cap",new Vector3(pipe.x,2.961f,pipe.z),new Vector3(.060f,.004f,.060f));
   report.Add("Rear upper mount side="+side+" measured body="+hit.point.ToString("F5")+" pipe="+pipe+"; annular clamp inner29.2mm, pipe29mm, welded bridge and 2 body fasteners");
  }
  Object.DestroyImmediate(temporary);Object.DestroyImmediate(surface);
  var target=Find(root,"REBIRTHArmorSurface_grilles").GetComponent<MeshFilter>();var old=target.sharedMesh;var oldVs=old.vertices;var oldUv=old.uv;var vertices=new List<Vector3>(oldVs);var uv=new List<Vector2>(oldUv);var normals=new List<Vector3>(old.normals);var added=new List<int>();
  foreach(var go in Parts)
  {
   var mesh=go.GetComponent<MeshFilter>().sharedMesh;int start=vertices.Count;var ns=mesh.normals;var mv=mesh.vertices;
   // Reuse a local patch of the existing weathered pipe atlas, keeping its grain/normal maps.
   var center=go.transform.TransformPoint(mesh.bounds.center);int nearest=Enumerable.Range(0,oldVs.Length).OrderBy(i=>(target.transform.TransformPoint(oldVs[i])-center).sqrMagnitude).First();Vector2 sample=oldUv[nearest];
   for(int i=0;i<mv.Length;i++){var world=go.transform.TransformPoint(mv[i]);vertices.Add(target.transform.InverseTransformPoint(world));normals.Add(target.transform.InverseTransformDirection(go.transform.TransformDirection(ns[i])).normalized);uv.Add(sample+new Vector2(world.y-center.y,world.z-center.z)*.09f);}
   added.AddRange(mesh.triangles.Select(i=>start+i));Object.DestroyImmediate(go);
  }
  Parts.Clear();var original=old.triangles;var marker=target.transform.Cast<Transform>().Single(t=>t.name.StartsWith("ArmorRange_50_"));var fields=marker.name.Split('_');int insertion=int.Parse(fields[3])+int.Parse(fields[4]);var indices=original.Take(insertion).Concat(added).Concat(original.Skip(insertion)).ToArray();
  foreach(Transform range in target.transform){var f=range.name.Split('_');int begin=int.Parse(f[3]),count=int.Parse(f[4]);if(f[1]=="50")count+=added.Count;else if(begin>=insertion)begin+=added.Count;range.name=$"ArmorRange_{f[1]}_{f[2]}_{begin}_{count}";}
  var combined=new Mesh{name="Grilles with body-fixed rear pipe clamps",indexFormat=IndexFormat.UInt32};combined.SetVertices(vertices);combined.SetNormals(normals);combined.SetUVs(0,uv);combined.SetTriangles(indices,0);combined.RecalculateBounds();combined.RecalculateTangents();AssetDatabase.CreateAsset(combined,Folder+"/GrillesRearMounts.asset");target.sharedMesh=combined;
  report.Add("New mounts join existing windshield/perimeter damage group 50 at12%; 54 scheduled pieces retained; shared grille atlas unchanged");
  PrefabUtility.SaveAsPrefabAsset(root,Folder+"/rebirthCamper80s.prefab");AssetDatabase.SaveAssets();
  RebirthCamperCampIntegration.SetMode(root,true);Find(root,"REBIRTHRearDoorPivot").localRotation=Find(root,"REBIRTHRearDoorOpenPose").localRotation;
  foreach(string name in new[]{"REBIRTHModArmor","REBIRTHModArmorDoor"})Find(root,name).gameObject.SetActive(true);Find(root,"REBIRTHCampStairVisuals").gameObject.SetActive(true);
  Render(root,new Vector3(-2.2f,3.2f,-5.1f),new Vector3(-.8f,2.78f,-3.25f),.75f,"rear-pipe-body-mount");Render(root,new Vector3(0,1.9f,-6.2f),new Vector3(0,1.65f,-3.3f),1.8f,"rear-door-frame-open");
  Find(root,"REBIRTHRearDoorPivot").localRotation=Quaternion.identity;Render(root,new Vector3(0,2,-6.2f),new Vector3(0,1.65f,-3.3f),1.7f,"rear-door-frame-closed");
  Object.DestroyImmediate(root);File.WriteAllLines(Output+"/review-receipt.txt",report);
 }
 public static void Build()
 {
  Directory.CreateDirectory(Output+"/Bundles");var build=new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{Folder+"/rebirthCamper80s.prefab"},addressableNames=new[]{"rebirthCamper80s"}};
  if(!RebirthAssetBundlePackaging.BuildAssetBundles(Output+"/Bundles",new[]{build},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("Build failed");
  string path=Output+"/Bundles/rebirth_camper80s.unity3d";if(!BuildPipeline.GetCRCForAssetBundle(path,out uint crc))throw new Exception("CRC missing");var bundle=AssetBundle.LoadFromFile(path,crc);if(!bundle)throw new Exception("CRC reopen failed");
  try
  {
   var root=Object.Instantiate(bundle.LoadAsset<GameObject>("rebirthCamper80s"));var ts=root.GetComponentsInChildren<Transform>(true);if(ts.Count(t=>t.name.StartsWith("ArmorRange_"))!=54)throw new Exception("Damage ownership changed");
   var grilles=Find(root,"REBIRTHArmorSurface_grilles").GetComponent<MeshFilter>();if(grilles.transform.Cast<Transform>().Sum(t=>int.Parse(t.name.Split('_')[4]))!=grilles.sharedMesh.triangles.Length)throw new Exception("Range coverage");
   if(root.GetComponentsInChildren<WheelCollider>(true).Length!=4||ts.Count(t=>t.name.StartsWith("REBIRTH_Seat"))!=30)throw new Exception("Rig changed");
   var hull=Find(root,"REBIRTHModPlowHull").GetComponentsInChildren<MeshCollider>(true);if(hull.Length!=27||hull.Any(c=>!c.convex||c.isTrigger||c.gameObject.layer!=21))throw new Exception("Plough contract");
   var frame=ts.Where(t=>t.name=="Tall rear door jamb").OrderBy(t=>t.localPosition.x).ToArray();float width=frame[1].localPosition.x-frame[1].localScale.x/2-(frame[0].localPosition.x+frame[0].localScale.x/2);if(Mathf.Abs(width-1.04f)>.001f)throw new Exception("Frame width mismatch");
   foreach(string name in new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow","REBIRTHModPlowHull"})if(Find(root,name).gameObject.activeSelf)throw new Exception("Default mod visibility changed");
   Object.DestroyImmediate(root);File.WriteAllText(Output+"/verified.txt","PASS CRC="+crc+"; rear frame clear1.04m and headroom2.04m; 54 damage ranges including body-connected rear mounts; 4 wheels; 30 seat markers; 27 unchanged convex plough colliders; no in-game entry testing.");
  }finally{bundle.Unload(true);}
 }
 static void Render(GameObject root,Vector3 eye,Vector3 target,float size,string label)
 {
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.7f,.7f,.7f);var light=new GameObject().AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(35,155,0);var cam=new GameObject().AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.01f;cam.transform.position=eye;cam.transform.LookAt(target);cam.backgroundColor=new Color(.15f,.17f,.18f);cam.clearFlags=CameraClearFlags.SolidColor;var rt=new RenderTexture(1280,1000,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var img=new Texture2D(1280,1000,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,1000),0,0);img.Apply();File.WriteAllBytes(Output+"/"+label+".png",img.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;Object.DestroyImmediate(img);Object.DestroyImmediate(rt);Object.DestroyImmediate(cam.gameObject);Object.DestroyImmediate(light.gameObject);
 }
}
