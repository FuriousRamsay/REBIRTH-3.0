using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using Object=UnityEngine.Object;
// Widen only the existing rear opening; retain outside body corners and the original rig.
public static class RebirthCamperEntryWidth
{
 static float X(float x){float a=Mathf.Abs(x);return Mathf.Sign(x)*(a<=.4f?a*1.3f:a<.9f?.52f+(a-.4f)*.76f:a);}
 public static void Apply(GameObject root,string folder)
 {
  var pivot=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHRearDoorPivot");
  var entries=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="camper_body"||f.name=="camper_interior"||f.name.StartsWith("RearDoor_")||f.name=="Approved door")
   .Select(f=>new{filter=f,points=f.sharedMesh.vertices.Select(p=>f.transform.TransformPoint(p)).ToArray(),normals=f.sharedMesh.normals.Select(n=>f.transform.TransformDirection(n)).ToArray()}).ToArray();
  pivot.localPosition+=new Vector3(.12f,0,0);var marker=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHRearDoorOpenPose");marker.localPosition=pivot.localPosition;marker.localRotation=Quaternion.Euler(0,-130,0);
  int index=0;
  foreach(var row in entries)
  {
   var mesh=Object.Instantiate(row.filter.sharedMesh);mesh.name+="_Entry104";var vs=row.points;var ns=row.normals;
   for(int i=0;i<vs.Length;i++){var p=vs[i];if(p.z< -3.15f){float a=Mathf.Abs(p.x);p.x=X(p.x);ns[i].x/=(a<=.4f?1.3f:a<.9f?.76f:1f);}vs[i]=row.filter.transform.InverseTransformPoint(p);ns[i]=row.filter.transform.InverseTransformDirection(ns[i]).normalized;}
   mesh.vertices=vs;mesh.normals=ns;mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/Entry104_"+(index++)+".asset");row.filter.sharedMesh=mesh;
  }
  foreach(var box in root.GetComponentsInChildren<BoxCollider>(true).Where(b=>b.name=="Camp rear jamb")){box.center=new Vector3(Mathf.Sign(box.center.x)*.71f,box.center.y,box.center.z);box.size=new Vector3(.38f,box.size.y,box.size.z);}
  // The separate primitive trim must follow the widened opening too.
  foreach(var t in root.GetComponentsInChildren<Transform>(true))
  {
   if(t.name=="Tall rear door jamb"){var p=t.localPosition;p.x=Mathf.Sign(p.x)*.54f;t.localPosition=p;}
   if(t.name=="Tall rear door header"||t.name=="Tall rear door threshold"){var scale=t.localScale;scale.x=1.12f;t.localScale=scale;}
  }
  var door=pivot.GetComponent<BoxCollider>();door.center=new Vector3(-.52f,door.center.y,door.center.z);door.size=new Vector3(1.04f,door.size.y,door.size.z);
 }
}
