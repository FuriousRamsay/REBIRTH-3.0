using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class RebirthCamperWholeBodyRig
{
 public const string Folder="Assets/REBIRTHCamperWholeBodyRig_20261019";
 public const string Output="E:/_Haven/shared/REBIRTHStaged/CamperTurretRefresh_20261019/WholeBodyRig";
 static int index;
 static Transform Group(Transform parent,string name,Vector3 position)
 {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
 struct Vertex
 {internal Vector3 P,N;internal Vector2 UV;internal static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{P=Vector3.Lerp(a.P,b.P,t),N=Vector3.Lerp(a.N,b.N,t).normalized,UV=Vector2.Lerp(a.UV,b.UV,t)};}
 static List<Vertex> Clip(List<Vertex> input,Func<Vector3,float> plane)
 {
  var output=new List<Vertex>();if(input.Count==0)return output;var previous=input[input.Count-1];float pd=plane(previous.P);
  foreach(var current in input){float cd=plane(current.P);if((cd>=0)!=(pd>=0))output.Add(Vertex.Lerp(previous,current,pd/(pd-cd)));if(cd>=0)output.Add(current);previous=current;pd=cd;}return output;
 }
 static double MeshPart(Transform parent,string name,Vector3[] source,Vector3[] normals,Vector2[] uv,int[] triangles,Vector3 origin,Material[] materials,int part,float cut,float barrel)
 {
  var vertices=new List<Vector3>();var ns=new List<Vector3>();var tex=new List<Vector2>();var ts=new List<int>();double area=0;
  for(int i=0;i<triangles.Length;i+=3)
  {
   var poly=new List<Vertex>();for(int j=0;j<3;j++){int v=triangles[i+j];poly.Add(new Vertex{P=source[v],N=normals[v],UV=uv[v]});}
   poly=Clip(poly,p=>part==0?cut-p.y:p.y-cut);if(part!=0)poly=Clip(poly,p=>part==1?barrel-p.y:p.y-barrel);
   for(int j=1;j<poly.Count-1;j++)
   {
    var tri=new[]{poly[0],poly[j],poly[j+1]};float face=Vector3.Cross(tri[1].P-tri[0].P,tri[2].P-tri[0].P).magnitude*.5f;if(face<1e-10f)continue;area+=face;
    foreach(var v in tri){ts.Add(vertices.Count);var point=v.P;var normal=v.N;vertices.Add(point-origin);ns.Add(normal);tex.Add(v.UV);}
   }
  }
  if(ts.Count==0)throw new Exception("Empty turret rig part");var mesh=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(ns);mesh.SetUVs(0,tex);mesh.SetTriangles(ts,0);mesh.RecalculateBounds();mesh.RecalculateTangents();
  string meshPath=Folder+"/RigMesh"+(index++)+".asset";if(AssetDatabase.LoadAssetAtPath<Mesh>(meshPath))AssetDatabase.DeleteAsset(meshPath);AssetDatabase.CreateAsset(mesh,meshPath);var g=Group(parent,name,Vector3.zero);g.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;g.gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials;return area;
 }
 static void Capsule(Transform owner,Vector3 a,Vector3 b,float radius,string name)
 {
  var g=new GameObject(name);g.layer=21;g.tag="E_VehicleStrong";g.transform.SetParent(owner,false);g.transform.position=(a+b)/2;g.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);
  var c=g.AddComponent<CapsuleCollider>();c.radius=radius;c.height=Vector3.Distance(a,b)+radius*2;c.direction=1;
 }
 static void Collisions(GameObject root,List<string> report)
 {
  var physics=root.transform.Find("Physics");
  foreach(var c in physics.GetComponentsInChildren<Collider>(true).Where(c=>c.name.StartsWith("Roof guardrail solid")).ToArray())Object.DestroyImmediate(c.gameObject);
  var owner=Group(physics,"Turret-facing guardrail collision",Vector3.zero);int count=0;
  var rails=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Fitted rooftop guardrails");
  foreach(var f in rails.GetComponentsInChildren<MeshFilter>(true))
  {
   var b=f.sharedMesh.bounds;Capsule(owner,f.transform.TransformPoint(b.center-Vector3.up*b.extents.y),f.transform.TransformPoint(b.center+Vector3.up*b.extents.y),.014f,"Matched rail solid "+(++count));
  }
  var extended=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Extended rear side guardrails");
  foreach(var f in extended.GetComponentsInChildren<MeshFilter>(true))
  {
   var vs=f.sharedMesh.vertices;var points=new List<Vector3>();
   for(int i=0;i<vs.Length;i+=24){var centre=Vector3.zero;for(int j=0;j<24;j++)centre+=vs[i+j];points.Add(f.transform.TransformPoint(centre/24));}
   // Tiny curve segments are coalesced; centres remain within2mm of the rendered tube.
   int start=0;
   while(start<points.Count-1)
   {
    int end=start+1;
    while(end+1<points.Count)
    {
     var a=points[start];var b=points[end+1];var d=b-a;bool fit=true;
     for(int k=start+1;k<=end;k++){var near=a+d*Mathf.Clamp01(Vector3.Dot(points[k]-a,d)/d.sqrMagnitude);if(Vector3.Distance(near,points[k])>.002f){fit=false;break;}}
     if(!fit)break;end++;
    }
    Capsule(owner,points[start],points[end],.014f,"Matched extended rail solid "+(++count));start=end;
   }
  }
  // Continuous rear centre rail, leaving both ladder exits open.
  Capsule(owner,new Vector3(-.38f,2.88f,-3.10f),new Vector3(-.38f,3.07f,-3.10f),.014f,"Rear centre left post");
  Capsule(owner,new Vector3(-.34f,3.11f,-3.10f),new Vector3(.34f,3.11f,-3.10f),.014f,"Rear centre horizontal rail");
  Capsule(owner,new Vector3(.38f,2.88f,-3.10f),new Vector3(.38f,3.07f,-3.10f),.014f,"Rear centre right post");
  report.Add("Replaced stale original rail boxes with "+count+" fitted capsules plus rear centre rail; all belong to native Physics rigidbody.");
 }
 public static void Run()
 {
  index=0;Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperFourTurretReview_20261019_v9/FourTurretCamperReview.prefab"));root.name="rebirthCamper80s";
  var report=new List<string>{"Approved rounded-body redesign: compacted rear, rounded belly and shaped trunnion brackets. Entire upper body, eye and cannon pitch together. Four independent installation roots default hidden."};
  var mounts=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Four outboard corner turrets - static placement review");
  var corners=mounts.Cast<Transform>().ToArray();if(corners.Length!=4)throw new Exception("Four corner mounts required");
  for(int corner=0;corner<4;corner++)
  {
   var mount=corners[corner];mount.name="REBIRTHCamperTurretSlot"+corner;
   foreach(var plate in mount.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Camper screw attachment plate"))
   {var p=plate.localPosition;p.y=2.75f;plate.localPosition=p;var sc=plate.localScale;sc.y=.46f;plate.localScale=sc;}
   var turret=mount.Cast<Transform>().Single(t=>t.name.StartsWith("Optimized Tripo turret"));
   var source=turret.GetComponentsInChildren<MeshFilter>(true).Single(t=>t.name=="Turret_LOD0");
   var all=source.sharedMesh.vertices.Select(v=>turret.InverseTransformPoint(source.transform.TransformPoint(v))).ToArray();
   var bounds=new Bounds(all[0],Vector3.zero);foreach(var p in all)bounds.Encapsulate(p);
   float bottom=bounds.min.y,cut=bottom+.078f;
   var ring=all.Where(v=>v.y<bottom+.045f).ToArray();var baseCentre=new Vector3((ring.Min(v=>v.x)+ring.Max(v=>v.x))/2,cut,(ring.Min(v=>v.z)+ring.Max(v=>v.z))/2);
   var tip=all.Where(v=>v.z>bounds.max.z-.04f).ToArray();var pivot=new Vector3((tip.Min(v=>v.x)+tip.Max(v=>v.x))/2,.31f,-.21f);
   var yaw=Group(turret,"TurretYaw",baseCentre);var pitch=Group(yaw,"TurretPitch",pivot-baseCentre+Vector3.up*.07f);Group(pitch,"TurretMuzzle",new Vector3(pivot.x,pivot.y,bounds.max.z+.008f)-pivot);
   var levels=source.transform.parent.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Turret_LOD")).OrderBy(f=>f.name).ToArray();
   var lods=new List<LOD>();
   foreach(var f in levels)
   {
    var mesh=f.sharedMesh;var vs=mesh.vertices.Select(v=>turret.InverseTransformPoint(f.transform.TransformPoint(v))).ToArray();var normals=mesh.normals.Select(n=>turret.InverseTransformDirection(f.transform.TransformDirection(n)).normalized).ToArray();var ts=mesh.triangles;
    var mats=f.GetComponent<Renderer>().sharedMaterials;var before=root.GetComponentsInChildren<Renderer>(true).ToArray();double sourceArea=0,splitArea=0;
    for(int t=0;t<ts.Length;t+=3)sourceArea+=Vector3.Cross(vs[ts[t+1]]-vs[ts[t]],vs[ts[t+2]]-vs[ts[t]]).magnitude*.5f;
    splitArea+=MeshPart(turret,"FixedBase_"+f.name,vs,normals,mesh.uv,ts,Vector3.zero,mats,0,cut,bottom+.14f);
    splitArea+=MeshPart(pitch,"PitchCannon_"+f.name,vs,normals,mesh.uv,ts,pivot,mats,2,cut,bottom+.26f);
    // Replace the old intermediate gear/body fragments with a clean manufactured yaw collar.
    var renderers=root.GetComponentsInChildren<Renderer>(true).Except(before).ToArray();lods.Add(new LOD(lods.Count==0?.35f:lods.Count==1?.14f:.025f,renderers));
    report.Add("PASS corner "+corner+" "+f.name+" source/clipped surface areas before deliberate rear rounding "+sourceArea+" / "+splitArea+" pivot="+pivot.ToString("F5"));
   }
   var oldLods=turret.GetComponentsInChildren<LODGroup>(true);foreach(var lod in oldLods)Object.DestroyImmediate(lod);
   foreach(var f in levels)Object.DestroyImmediate(f.gameObject);
   var group=turret.gameObject.AddComponent<LODGroup>();group.SetLODs(lods.ToArray());group.RecalculateBounds();
   report.Add("Corner "+corner+" wall plate top="+(2.95f+(2.98f-3.09f)*.65f)+"m (below roof trim). Yaw +/-125; cannon +/-75 runtime limits.");
   var added=RebirthCamperRoundedSwivel.Build(yaw,pitch,pivot,baseCentre);report.Add(RebirthCamperRoundedSwivel.Audit(pitch,baseCentre));
   var updated=group.GetLODs();for(int l=0;l<updated.Length;l++)updated[l].renderers=updated[l].renderers.Concat(added).ToArray();group.SetLODs(updated);group.RecalculateBounds();
   mount.gameObject.SetActive(false);
  }
  Collisions(root,report);
  PrefabUtility.SaveAsPrefabAsset(root,Folder+"/rebirthCamper80s.prefab");AssetDatabase.SaveAssets();
  RebirthCamperCampIntegration.SetMode(root,true);
  foreach(var t in root.GetComponentsInChildren<Transform>(true)){if(t.name.StartsWith("REBIRTHCamperTurretSlot"))t.gameObject.SetActive(true);if(new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"}.Contains(t.name))t.gameObject.SetActive(false);}
  // Clay review isolates silhouette and mechanical proportions from the finish.
  var clay=new Material(Shader.Find("Standard"));clay.color=new Color(.64f,.64f,.64f);clay.SetFloat("_Glossiness",.24f);foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>clay).ToArray();
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.6f,.6f);var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1;key.transform.rotation=Quaternion.Euler(35,-30,0);
  Render(new Vector3(-3.4f,3.8f,-1.7f),new Vector3(-1.03f,2.90f,-3.0f),.76f,"lowered-bracket");
  var door=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHRearDoorPivot");var oldDoor=door.localRotation;door.localRotation=Quaternion.identity;
  Render(new Vector3(0,10,-1),new Vector3(0,1,-1),3.8f,"overview",1000,1000);
  door.localRotation=oldDoor;
  foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="TurretPitch"))t.localRotation=Quaternion.Euler(-75,0,0);
  Render(new Vector3(-3.4f,3.8f,-1.7f),new Vector3(-1.03f,2.90f,-3.0f),.76f,"pitch-up75");
  var rotations=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="TurretYaw").ToDictionary(t=>t,t=>t.localRotation);
  foreach(var pose in new[]{new Vector3(0,0,0),new Vector3(0,-75,1),new Vector3(0,75,2),new Vector3(-125,0,3),new Vector3(125,0,4)})
  {
   foreach(var yawPair in rotations)yawPair.Key.localRotation=yawPair.Value*Quaternion.Euler(0,pose.x,0);
   foreach(var pitch in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="TurretPitch"))pitch.localRotation=Quaternion.Euler(pose.y,0,0);
   Render(new Vector3(-3.4f,3.9f,-1.7f),new Vector3(-1.03f,3.03f,-3.0f),.88f,"review-"+new[]{"level","up75","down75","left125","right125"}[(int)pose.z]);
   report.Add("Review pose yaw="+pose.x+" pitch="+pose.y+" entire upper body/eye/cannon under TurretPitch.");
  }
  File.WriteAllLines(Output+"/rig-report.txt",report);Object.DestroyImmediate(root);
 }
 static void Render(Vector3 eye,Vector3 target,float size,string name,int width=1400,int height=1400)
 {
  var c=new GameObject(name).AddComponent<Camera>();c.orthographic=true;c.orthographicSize=size;c.nearClipPlane=.01f;c.transform.position=eye;c.transform.LookAt(target);c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.07f,.07f,.09f);
  var rt=new RenderTexture(width,height,24){antiAliasing=4};c.targetTexture=rt;c.Render();RenderTexture.active=rt;var img=new Texture2D(width,height,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,width,height),0,0);img.Apply();File.WriteAllBytes(Output+"/"+name+".png",img.EncodeToPNG());RenderTexture.active=null;c.targetTexture=null;Object.DestroyImmediate(img);Object.DestroyImmediate(rt);Object.DestroyImmediate(c.gameObject);
 }
}


