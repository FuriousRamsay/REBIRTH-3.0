using System;
using System.IO;
using UnityEngine;
namespace RebirthEntityStudio {
 // Corner-angle weighted smooth normals. No source geometry is changed.
 public static class RebirthCornerAngleNormals {
  public static Vector3[] Calculate(Vector3[] points,int[] triangles,out int degenerateFaces){
   if(points==null||triangles==null||triangles.Length%3!=0)throw new InvalidDataException("Invalid normal source arrays");
   foreach(var p in points)if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z))throw new InvalidDataException("Nonfinite normal source");
   var x=new double[points.Length];var y=new double[points.Length];var z=new double[points.Length];degenerateFaces=0;
   for(int i=0;i<triangles.Length;i+=3){
    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];if(a<0||b<0||c<0||a>=points.Length||b>=points.Length||c>=points.Length)throw new InvalidDataException("Normal triangle outside vertex range");
    double ux=(double)points[b].x-points[a].x,uy=(double)points[b].y-points[a].y,uz=(double)points[b].z-points[a].z;
    double vx=(double)points[c].x-points[a].x,vy=(double)points[c].y-points[a].y,vz=(double)points[c].z-points[a].z;
    double nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx,length=Math.Sqrt(nx*nx+ny*ny+nz*nz);
    if(length<=1e-30){degenerateFaces++;continue;}nx/=length;ny/=length;nz/=length;
    for(int corner=0;corner<3;corner++){
     int index=triangles[i+corner],next=triangles[i+(corner+1)%3],last=triangles[i+(corner+2)%3];
     ux=(double)points[next].x-points[index].x;uy=(double)points[next].y-points[index].y;uz=(double)points[next].z-points[index].z;
     vx=(double)points[last].x-points[index].x;vy=(double)points[last].y-points[index].y;vz=(double)points[last].z-points[index].z;
     double cx=uy*vz-uz*vy,cy=uz*vx-ux*vz,cz=ux*vy-uy*vx;
     double angle=Math.Atan2(Math.Sqrt(cx*cx+cy*cy+cz*cz),ux*vx+uy*vy+uz*vz);
     x[index]+=nx*angle;y[index]+=ny*angle;z[index]+=nz*angle;
    }
   }
   var result=new Vector3[points.Length];
   for(int i=0;i<result.Length;i++){double length=Math.Sqrt(x[i]*x[i]+y[i]*y[i]+z[i]*z[i]);if(length>1e-30)result[i]=new Vector3((float)(x[i]/length),(float)(y[i]/length),(float)(z[i]/length));}
   return result;
  }
 }
}
