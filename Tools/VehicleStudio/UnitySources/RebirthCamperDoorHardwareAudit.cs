using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Object=UnityEngine.Object;
public static class RebirthCamperDoorHardwareAudit
{
 public const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperDoorHardware_20261018";
 public class Component {public int Sub;public List<int> Indices=new List<int>();public Bounds Bounds;}
 static int Root(Dictionary<int,int> p,int a){if(p[a]!=a)p[a]=Root(p,p[a]);return p[a];}
 public static List<Component> Components(MeshFilter f)
 {
  var m=f.sharedMesh;var vs=m.vertices.Select(v=>f.transform.TransformPoint(v)).ToArray();var weld=new Dictionary<string,int>();var canonical=new int[vs.Length];var parents=new Dictionary<int,int>();
  for(int i=0;i<vs.Length;i++){var v=vs[i];string key=Mathf.RoundToInt(v.x*10000)+","+Mathf.RoundToInt(v.y*10000)+","+Mathf.RoundToInt(v.z*10000);if(!weld.TryGetValue(key,out int id)){id=i;weld.Add(key,id);parents[id]=id;}canonical[i]=id;}
  for(int s=0;s<m.subMeshCount;s++){var ts=m.GetTriangles(s);for(int i=0;i<ts.Length;i+=3){int a=Root(parents,canonical[ts[i]]);for(int j=1;j<3;j++)parents[Root(parents,canonical[ts[i+j]])]=a;}}
  var groups=new Dictionary<string,Component>();for(int s=0;s<m.subMeshCount;s++){var ts=m.GetTriangles(s);for(int i=0;i<ts.Length;i+=3){string key=s+":"+Root(parents,canonical[ts[i]]);if(!groups.TryGetValue(key,out var c)){c=new Component{Sub=s,Bounds=new Bounds(vs[ts[i]],Vector3.zero)};groups.Add(key,c);}for(int j=0;j<3;j++){c.Indices.Add(ts[i+j]);c.Bounds.Encapsulate(vs[ts[i+j]]);}}}return groups.Values.ToList();
 }
 public static bool InOpening(Component c)=>c.Bounds.min.x>-.49f&&c.Bounds.max.x<.49f&&c.Bounds.min.y>.74f&&c.Bounds.max.y<2.70f&&c.Bounds.min.z> -3.48f&&c.Bounds.max.z< -3.12f;
 public static void Run()
 {
  Directory.CreateDirectory(Output);var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperRearFit.Folder+"/rebirthCamper80s.prefab"));var rows=new List<string>();
  foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
  {
   if(f.GetComponentsInParent<Transform>(true).Any(t=>t.name=="REBIRTHRearDoorPivot"))continue;
   foreach(var c in Components(f).Where(InOpening))rows.Add(f.name+" sub="+c.Sub+" tris="+c.Indices.Count/3+" center="+c.Bounds.center.ToString("F5")+" size="+c.Bounds.size.ToString("F5"));
  }
  File.WriteAllLines(Output+"/opening-components.txt",rows);Object.DestroyImmediate(root);
 }
}
