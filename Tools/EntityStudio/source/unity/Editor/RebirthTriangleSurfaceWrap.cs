using UnityEngine;
namespace RebirthEntityStudio {
 // Rest/current triangle surface coordinates. Tangential offsets follow the
 // triangle's affine stretch; signed normal distance retains its rest thickness.
 // This is a local surface binding, not Blender/Maya's multi-face wrap solver.
 public static class RebirthTriangleSurfaceWrap {
  public static bool TryTransform(Vector3 point,Vector3 a,Vector3 b,Vector3 c,Vector3 x,Vector3 y,Vector3 z,Vector3 weights,out Vector3 transformed){
   transformed=point+(x-a)*weights.x+(y-b)*weights.y+(z-c)*weights.z;
   Vector3 e=b-a,f=c-a,normal=Vector3.Cross(e,f),u=y-x,v=z-x,newNormal=Vector3.Cross(u,v);
   float ee=Vector3.Dot(e,e),ff=Vector3.Dot(f,f),ef=Vector3.Dot(e,f),det=normal.sqrMagnitude;
   if(det<=ee*ff*1e-8f||normal.sqrMagnitude<1e-20f||newNormal.sqrMagnitude<1e-20f)return false;
   normal/=Mathf.Sqrt(normal.sqrMagnitude);newNormal/=Mathf.Sqrt(newNormal.sqrMagnitude);
   Vector3 delta=point-(a*weights.x+b*weights.y+c*weights.z);
   float de=Vector3.Dot(delta,e),df=Vector3.Dot(delta,f);
   float s=(de*ff-df*ef)/det,t=(df*ee-de*ef)/det;
   transformed=x*weights.x+y*weights.y+z*weights.z+u*s+v*t+newNormal*Vector3.Dot(delta,normal);
   return true;
  }
 }
}
