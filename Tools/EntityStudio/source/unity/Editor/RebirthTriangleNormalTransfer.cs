using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
namespace RebirthEntityStudio {
 // Triangle frames: Sumner & Popovic, SIGGRAPH 2004, equations 1-4.
 // This transfers shading normals over an already evaluated mesh; it is not
 // their global deformation-transfer solver and does not alter vertex positions.
 public static class RebirthTriangleNormalTransfer {
  struct D {
   public double x,y,z;public D(double x,double y,double z){this.x=x;this.y=y;this.z=z;}public D(Vector3 v){x=v.x;y=v.y;z=v.z;}
   public static D operator +(D a,D b)=>new D(a.x+b.x,a.y+b.y,a.z+b.z);
   public static D operator -(D a,D b)=>new D(a.x-b.x,a.y-b.y,a.z-b.z);
   public static D operator *(D a,double b)=>new D(a.x*b,a.y*b,a.z*b);
   public static D operator /(D a,double b)=>a*(1/b);
   public double Length=>Math.Sqrt(Dot(this,this));public Vector3 Vector=>new Vector3((float)x,(float)y,(float)z);
   public static double Dot(D a,D b)=>a.x*b.x+a.y*b.y+a.z*b.z;
   public static D Cross(D a,D b)=>new D(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
  }
  static bool Finite(Vector3 p)=>!float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude);
  static void Frame(Vector3 a,Vector3 b,Vector3 c,out D x,out D y,out D z){
   if(!Finite(a)||!Finite(b)||!Finite(c))throw new InvalidDataException("Nonfinite anatomy triangle");
   x=new D(b)-new D(a);y=new D(c)-new D(a);double scale=Math.Max(x.Length,y.Length);
   if(scale<=1e-12)throw new InvalidDataException("Collapsed anatomy triangle");
   x/=scale;y/=scale;var cross=D.Cross(x,y);double area=cross.Length;
   if(area<=1e-8)throw new InvalidDataException("Ill-conditioned anatomy normal frame");
   z=cross/Math.Sqrt(area);
  }
  static double Angle(D a,D b)=>Math.Atan2(D.Cross(a,b).Length,D.Dot(a,b));
  public static Mesh Build(Vector3[] rest,Vector3[] posed,int[] triangles,Vector3[] cornerNormals,string name){
   if(rest==null||posed==null||rest.Length!=posed.Length||triangles==null||triangles.Length==0||triangles.Length%3!=0||cornerNormals==null||cornerNormals.Length!=triangles.Length)throw new InvalidDataException("Invalid source normal topology");
   var points=new List<Vector3>();var sums=new List<D>();var indices=new int[triangles.Length];
   var keys=new Dictionary<Tuple<int,float,float,float>,int>();
   for(int i=0;i<triangles.Length;i+=3){
    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
    if(a<0||b<0||c<0||a>=rest.Length||b>=rest.Length||c>=rest.Length)throw new InvalidDataException("Invalid source normal index");
    D rx,ry,rz,px,py,pz;Frame(rest[a],rest[b],rest[c],out rx,out ry,out rz);Frame(posed[a],posed[b],posed[c],out px,out py,out pz);
    var cx=D.Cross(py,pz);var cy=D.Cross(pz,px);var cz=D.Cross(px,py);
    for(int j=0;j<3;j++){
     int source=triangles[i+j];var n=cornerNormals[i+j];
     if(!Finite(n)||Mathf.Abs(n.magnitude-1)>.001f||!Finite(posed[source]))throw new InvalidDataException("Invalid supplied anatomy normal/position");
     // Q^-T n = posedFrame^-T * restFrame^T * n. Positive determinant
     // and uniform frame scale cancel when the result is normalized.
     var nd=new D(n);var transferred=cx*D.Dot(rx,nd)+cy*D.Dot(ry,nd)+cz*D.Dot(rz,nd);
     if(double.IsNaN(transferred.Length)||double.IsInfinity(transferred.Length)||transferred.Length<1e-20)throw new InvalidDataException("Undefined transferred anatomy normal");
     var key=Tuple.Create(source,n.x,n.y,n.z);int index;
     if(!keys.TryGetValue(key,out index)){index=points.Count;keys.Add(key,index);points.Add(posed[source]);sums.Add(new D());}
     int next=triangles[i+(j+1)%3],last=triangles[i+(j+2)%3];
     sums[index]+=(transferred/transferred.Length)*Angle(new D(rest[next])-new D(rest[source]),new D(rest[last])-new D(rest[source]));indices[i+j]=index;
    }
   }
   var outputNormals=new List<Vector3>();foreach(var sum in sums){if(double.IsNaN(sum.Length)||double.IsInfinity(sum.Length)||sum.Length<1e-20)throw new InvalidDataException("Opposing transferred shading normals");outputNormals.Add((sum/sum.Length).Vector);}
   var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.HideAndDontSave};
   try{mesh.SetVertices(points);mesh.SetNormals(outputNormals);mesh.triangles=indices;mesh.RecalculateBounds();return mesh;}
   catch{UnityEngine.Object.DestroyImmediate(mesh);throw;}
  }
 }
}
