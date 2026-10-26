using System;
using System.Collections.Generic;
using UnityEngine;
namespace RebirthEntityStudio {
 // Static groom/head contact during authoring, independent of runtime simulation.
 internal sealed class GuideHairSurfaceConstraint : IDisposable {
  readonly Mesh mesh;readonly bool ownsMesh;readonly Vector3[] points,normals;readonly int[] triangles;Vector3[] centers,faceNormals;
  sealed class Node {public Bounds bounds;public Node left,right;public int[] faces;}
  readonly Node tree;
  public int corrected;public float maximumCorrection;
  public GuideHairSurfaceConstraint(FaceTemplate template){
   points=new Vector3[template.positions.Length/3];for(int i=0;i<points.Length;i++)points[i]=new Vector3(template.positions[i*3],template.positions[i*3+1],template.positions[i*3+2]);
   triangles=template.triangles;ownsMesh=true;mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=points;mesh.triangles=triangles;mesh.RecalculateNormals();normals=mesh.normals;
   tree=Build(PrepareFaces());
  }
  public GuideHairSurfaceConstraint(FaceTemplate template,FaceRecipe recipe){
   ownsMesh=true;mesh=RebirthFaceGeneratorModel.Build(template,recipe);points=mesh.vertices;normals=mesh.normals;triangles=mesh.GetTriangles(0);
   tree=Build(PrepareFaces());
  }
  public GuideHairSurfaceConstraint(Mesh evaluatedHead){
   if(evaluatedHead==null)throw new ArgumentNullException(nameof(evaluatedHead));mesh=evaluatedHead;ownsMesh=false;points=mesh.vertices;normals=mesh.normals;triangles=mesh.GetTriangles(0);tree=Build(PrepareFaces());
  }
  int[] PrepareFaces(){
   int count=triangles.Length/3;var faces=new int[count];centers=new Vector3[count];faceNormals=new Vector3[count];
   for(int face=0;face<count;face++){int index=face*3;faces[face]=index;int a=triangles[index],b=triangles[index+1],c=triangles[index+2];centers[face]=(points[a]+points[b]+points[c])/3;var cross=Vector3.Cross(points[b]-points[a],points[c]-points[a]);float squared=cross.sqrMagnitude;if(squared<1e-20f)continue;var normal=cross/Mathf.Sqrt(squared);if(Vector3.Dot(normal,normals[a]+normals[b]+normals[c])<0)normal=-normal;faceNormals[face]=normal;}
   return faces;
  }
  Vector3 Center(int face)=>centers[face/3];
  Node Build(int[] faces){
   if(faces.Length==0)return null;var node=new Node{bounds=new Bounds(points[triangles[faces[0]]],Vector3.zero)};
   foreach(int face in faces)for(int k=0;k<3;k++)node.bounds.Encapsulate(points[triangles[face+k]]);
   if(faces.Length<=8){node.faces=faces;return node;}
   var size=node.bounds.size;int axis=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2;
   Array.Sort(faces,(a,b)=>Center(a)[axis].CompareTo(Center(b)[axis]));int middle=faces.Length/2;
   var left=new int[middle];var right=new int[faces.Length-middle];Array.Copy(faces,0,left,0,left.Length);Array.Copy(faces,middle,right,0,right.Length);
   node.left=Build(left);node.right=Build(right);return node;
  }
  void Nearest(Node node,Vector3 p,ref float best,ref Vector3 nearest,ref Vector3 normal,bool smooth=false){
   if(node==null||node.bounds.SqrDistance(p)>best)return;
   if(node.faces!=null){foreach(int index in node.faces){int ia=triangles[index],ib=triangles[index+1],ic=triangles[index+2];var faceNormal=faceNormals[index/3];if(faceNormal.sqrMagnitude<.5f)continue;var q=Closest(p,points[ia],points[ib],points[ic]);float d=(p-q).sqrMagnitude;if(d>=best)continue;best=d;nearest=q;normal=smooth?InterpolatedNormal(ia,ib,ic,q,faceNormal):faceNormal;}return;}
   bool leftFirst=node.left.bounds.SqrDistance(p)<=node.right.bounds.SqrDistance(p);Nearest(leftFirst?node.left:node.right,p,ref best,ref nearest,ref normal,smooth);Nearest(leftFirst?node.right:node.left,p,ref best,ref nearest,ref normal,smooth);
  }
  Vector3 InterpolatedNormal(int a,int b,int c,Vector3 p,Vector3 fallback){
   var ab=points[b]-points[a];var ac=points[c]-points[a];var ap=p-points[a];float aa=Vector3.Dot(ab,ab),bb=Vector3.Dot(ab,ac),cc=Vector3.Dot(ac,ac),d=Vector3.Dot(ap,ab),e=Vector3.Dot(ap,ac),denominator=aa*cc-bb*bb;
   if(Mathf.Abs(denominator)<1e-20f)return fallback;float v=(cc*d-bb*e)/denominator,w=(aa*e-bb*d)/denominator;var normal=normals[a]*(1-v-w)+normals[b]*v+normals[c]*w;return normal.sqrMagnitude<1e-12f?fallback:normal.normalized;
  }
  // Project shading normals only. Contact correction still uses oriented face
  // normals, so enabling this query cannot change generated vertex positions.
  public bool TrySmoothNormal(Vector3 p,out Vector3 normal){float best=.05f*.05f;var nearest=p;normal=Vector3.zero;Nearest(tree,p,ref best,ref nearest,ref normal,true);return normal.sqrMagnitude>.5f;}
  static Vector3 Segment(Vector3 p,Vector3 a,Vector3 b){var d=b-a;return a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(1e-12f,d.sqrMagnitude));}
  static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c){var ab=b-a;var ac=c-a;var n=Vector3.Cross(ab,ac);float area=n.sqrMagnitude;if(area<1e-20f)return Segment(p,a,b);var q=p-n*(Vector3.Dot(p-a,n)/area);var aq=q-a;float aa=Vector3.Dot(ab,ab),bb=Vector3.Dot(ab,ac),cc=Vector3.Dot(ac,ac),d=Vector3.Dot(aq,ab),e=Vector3.Dot(aq,ac),denom=aa*cc-bb*bb;float v=(cc*d-bb*e)/denom,w=(aa*e-bb*d)/denom;if(v>=0&&w>=0&&v+w<=1)return q;var x=Segment(p,a,b);var y=Segment(p,b,c);var z=Segment(p,c,a);return (p-x).sqrMagnitude<(p-y).sqrMagnitude?((p-x).sqrMagnitude<(p-z).sqrMagnitude?x:z):((p-y).sqrMagnitude<(p-z).sqrMagnitude?y:z);}
  public Vector3 Outside(Vector3 p,float clearance=.0012f){float best=.05f*.05f;var nearest=p;var normal=Vector3.zero;Nearest(tree,p,ref best,ref nearest,ref normal);
   if(normal.sqrMagnitude<.5f)return p;float signed=Vector3.Dot(p-nearest,normal);if(signed>=clearance)return p;var result=p+normal*(clearance-signed);corrected++;maximumCorrection=Mathf.Max(maximumCorrection,Vector3.Distance(p,result));return result;
  }
  public float SignedDistance(Vector3 p){float best=.05f*.05f;var nearest=p;var normal=Vector3.zero;Nearest(tree,p,ref best,ref nearest,ref normal);return normal.sqrMagnitude<.5f?float.NaN:Vector3.Dot(p-nearest,normal);}
  public Vector3 OnSurface(Vector3 p,float clearance){float best=.05f*.05f;var nearest=p;var normal=Vector3.zero;Nearest(tree,p,ref best,ref nearest,ref normal);if(normal.sqrMagnitude<.5f)throw new InvalidOperationException("Original short groom left its scalp projection range.");return nearest+normal*clearance;}
  public void Dispose(){if(ownsMesh)UnityEngine.Object.DestroyImmediate(mesh);}
 }
}
