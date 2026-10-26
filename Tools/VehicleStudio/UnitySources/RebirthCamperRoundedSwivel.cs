using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

// Approved rounded-body concept: fixed fork, whole-body pitch and compact rear.
public static class RebirthCamperRoundedSwivel
{
 static readonly string Folder=RebirthCamperWholeBodyRig.Folder;
 static readonly Dictionary<string,Mesh> Shared=new Dictionary<string,Mesh>();
 static Material Finish()
 {
  string path=Folder+"/SwivelRust.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
  m=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/REBIRTHTripoTurret_20261018/Turret.mat"));m.name="SwivelRust";
  foreach(string property in m.GetTexturePropertyNames()){m.SetTextureScale(property,new Vector2(.10f,.10f));m.SetTextureOffset(property,new Vector2(.18f,.025f));}
  AssetDatabase.CreateAsset(m,path);return m;
 }
 static Mesh Save(string name,List<Vector3> vs,List<int> ts,List<Vector2> uv)
 {
  if(Shared.TryGetValue(name,out var existing)&&existing)return existing;
  var mesh=new Mesh{name=name};mesh.SetVertices(vs);mesh.SetTriangles(ts,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
  string path=Folder+"/"+name+".asset";if(AssetDatabase.LoadAssetAtPath<Mesh>(path))AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(mesh,path);Shared[name]=mesh;return mesh;
 }
 static GameObject Part(Transform parent,string name,Mesh mesh,Material material)
 {var g=new GameObject(name);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;return g;}
 static Mesh Belly()
 {
  const int segments=32;const float radius=.18f,halfWidth=.155f;
  var vs=new List<Vector3>();var ts=new List<int>();var uv=new List<Vector2>();
  for(int i=0;i<=segments;i++)
  {
   float a=Mathf.PI*i/segments;
   for(int side=-1;side<=1;side+=2){vs.Add(new Vector3(side*halfWidth,-radius*Mathf.Sin(a),(.24f)*Mathf.Cos(a)));uv.Add(new Vector2(side<0?0:1,(float)i/segments));}
   if(i>0){int p=(i-1)*2;ts.AddRange(new[]{p,p+2,p+1,p+1,p+2,p+3});}
  }
  for(int side=-1;side<=1;side+=2)
  {
   int center=vs.Count;vs.Add(Vector3.right*(side*halfWidth));uv.Add(new Vector2(.5f,1));
   for(int i=0;i<=segments;i++){float a=Mathf.PI*i/segments;vs.Add(new Vector3(side*halfWidth,-radius*Mathf.Sin(a),(.24f)*Mathf.Cos(a)));uv.Add(new Vector2(.5f+.5f*Mathf.Cos(a),1-Mathf.Sin(a)));if(i>0){int end=vs.Count-1;if(side<0)ts.AddRange(new[]{center,end,end-1});else ts.AddRange(new[]{center,end-1,end});}}
  }
  int top=vs.Count;vs.AddRange(new[]{new Vector3(-halfWidth,0,-.24f),new Vector3(halfWidth,0,-.24f),new Vector3(halfWidth,0,.24f),new Vector3(-halfWidth,0,.24f)});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});ts.AddRange(new[]{top,top+2,top+1,top,top+3,top+2});
  return Save("RoundedLowerBody",vs,ts,uv);
 }
 static Mesh Arm()
 {
  // Side profile: broad bent mounting foot, narrow spine, rounded bearing head.
  var profile=new List<Vector2>{new Vector2(-.065f,-.23f),new Vector2(.12f,-.23f),new Vector2(.12f,-.21f),new Vector2(.085f,-.18f),new Vector2(.060f,-.14f),new Vector2(.060f,0)};
  for(int i=1;i<=8;i++){float a=Mathf.PI*i/8;profile.Add(new Vector2(.060f*Mathf.Cos(a),.060f*Mathf.Sin(a)));}
  profile.Add(new Vector2(-.060f,-.19f));
  var vs=new List<Vector3>();var ts=new List<int>();var uv=new List<Vector2>();
  var center=new Vector2(.012f,-.14f);
  // Beveled edge rings avoid the squared primitive-block appearance.
  for(int ring=0;ring<4;ring++)for(int i=0;i<profile.Count;i++)
  {var p=(ring==0||ring==3)?Vector2.Lerp(profile[i],center,.06f):profile[i];float x=new[]{-.022f,-.016f,.016f,.022f}[ring];vs.Add(new Vector3(x,p.y,p.x));uv.Add(new Vector2((p.x+.065f)/.23f,(p.y+.23f)/.29f));}
  int n=profile.Count;
  for(int ring=0;ring<3;ring++)for(int i=0;i<n;i++){int a=ring*n+i,b=ring*n+(i+1)%n,c=(ring+1)*n+i,d=(ring+1)*n+(i+1)%n;ts.AddRange(new[]{a,c,b,b,c,d});}
  var remaining=Enumerable.Range(0,n).ToList();int guard=0;
  Func<Vector2,Vector2,Vector2,float> cross=(a,b,c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
  while(remaining.Count>2&&guard++<n*n)
  {
   bool found=false;
   for(int i=0;i<remaining.Count;i++)
   {
    int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];
    if(cross(profile[a],profile[b],profile[c])<=.000001f)continue;
    if(remaining.Any(p=>p!=a&&p!=b&&p!=c&&cross(profile[a],profile[b],profile[p])>=0&&cross(profile[b],profile[c],profile[p])>=0&&cross(profile[c],profile[a],profile[p])>=0))continue;
    ts.AddRange(new[]{a,b,c,a+3*n,c+3*n,b+3*n});remaining.RemoveAt(i);found=true;break;
   }
   if(!found)throw new Exception("Swivel profile triangulation failed");
  }
  return Save("ApprovedSwivelBracket",vs,ts,uv);
 }
 public static Renderer[] Build(Transform yaw,Transform pitch,Vector3 pivot,Vector3 baseCentre)
 {
  var material=Finish();var parts=new List<Renderer>();var bowl=Part(pitch,"Rounded turret underside",Belly(),material);bowl.transform.localPosition=Vector3.right*.027f;parts.Add(bowl.GetComponent<Renderer>());
    var collar=GameObject.CreatePrimitive(PrimitiveType.Cylinder);collar.name="Whole-body yaw collar";collar.transform.SetParent(yaw,false);collar.transform.localPosition=new Vector3(0,.115f-baseCentre.y,0);collar.transform.localScale=new Vector3(.52f,.025f,.52f);Object.DestroyImmediate(collar.GetComponent<Collider>());collar.GetComponent<Renderer>().sharedMaterial=material;parts.Add(collar.GetComponent<Renderer>());
  var armMesh=Arm();
  for(int side=-1;side<=1;side+=2)
  {
   var arm=Part(yaw,"Approved whole-body swivel bracket",armMesh,material);arm.transform.localPosition=new Vector3(pivot.x+side*.19f,.38f-baseCentre.y,pivot.z-baseCentre.z);parts.Add(arm.GetComponent<Renderer>());
   foreach(var spec in new[]{new Vector3(.10f,.05f,0),new Vector3(.07f,.012f,.055f),new Vector3(.036f,.012f,.071f)})
   {
    var cap=GameObject.CreatePrimitive(PrimitiveType.Cylinder);cap.name="Swivel bearing and axle hardware";cap.transform.SetParent(yaw,false);cap.transform.localPosition=arm.transform.localPosition+Vector3.right*side*spec.z;cap.transform.localRotation=Quaternion.Euler(0,0,90);cap.transform.localScale=new Vector3(spec.x,spec.y,spec.x);Object.DestroyImmediate(cap.GetComponent<Collider>());cap.GetComponent<Renderer>().sharedMaterial=material;parts.Add(cap.GetComponent<Renderer>());
   }
  }
  return parts.ToArray();
 }
 public static string Audit(Transform pitch,Vector3 baseCentre)
 {
  var samples=pitch.GetComponentsInChildren<MeshFilter>(true).Where(f=>!f.name.Contains("LOD1")&&!f.name.Contains("LOD2")).SelectMany(f=>f.sharedMesh.vertices.Select(v=>pitch.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
  int first=999,hits=0;Vector3 point=Vector3.zero;
  for(int angle=-75;angle<=75;angle++)
  {
   var rotation=Quaternion.Euler(angle,0,0);
   foreach(var v in samples){var p=rotation*v+pitch.localPosition;if(p.x*p.x+p.z*p.z<.26f*.26f&&p.y>.09f-baseCentre.y&&p.y<.14f-baseCentre.y){hits++;if(first==999){first=angle;point=p;}}}
  }
  return "Yaw collar pitch sweep -75..75 in1-degree increments: intersecting vertex samples="+hits+" first pitch="+first+" point="+point+". Review diagnostic; not an exhaustive triangle collision proof.";
 }
}



