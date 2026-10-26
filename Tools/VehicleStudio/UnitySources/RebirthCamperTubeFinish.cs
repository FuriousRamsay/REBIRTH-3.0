using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public static class RebirthCamperTubeFinish
{
 static int serial;
 public static GameObject Tube(Transform parent,string name,List<Vector3> path,float radius,Material material,string folder)
 {
  if(path==null||path.Count<2||radius<=0)throw new System.ArgumentException("Tube requires a path and positive radius.");
  var v=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();const int sides=24;const int ring=sides+1;
  float distance=0;Vector3 previousU=Vector3.zero;
  for(int i=0;i<path.Count;i++)
  {
   var tangent=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(i-1,0)]).normalized;
   var u=i==0?Vector3.Cross(tangent,Mathf.Abs(tangent.x)<.8f?Vector3.right:Vector3.forward).normalized:Vector3.ProjectOnPlane(previousU,tangent).normalized;
   if(u.sqrMagnitude<.5f)u=Vector3.Cross(tangent,Vector3.up).normalized;
   previousU=u;var w=Vector3.Cross(tangent,u);
   if(i>0)distance+=Vector3.Distance(path[i-1],path[i]);
   // Duplicate the UV seam; preserve physical texel density through bends.
   for(int j=0;j<=sides;j++){float a=j*Mathf.PI*2/sides;var normal=u*Mathf.Cos(a)+w*Mathf.Sin(a);v.Add(path[i]+radius*normal);normals.Add(normal);uv.Add(new Vector2((float)j/sides,distance/(2*Mathf.PI*radius)));}
   if(i>0)for(int j=0;j<sides;j++){int a=(i-1)*ring+j,b=a+1,c=i*ring+j,d=c+1;tr.AddRange(new[]{a,b,c,b,d,c});}
  }
  var m=new Mesh();m.SetVertices(v);m.SetNormals(normals);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateTangents();m.RecalculateBounds();
  AssetDatabase.CreateAsset(m,folder+"/ContinuousTube"+(++serial)+".asset");
  var g=new GameObject(name);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=material;return g;
 }
}
