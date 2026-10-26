using System;
using UnityEngine;
namespace RebirthEntityStudio {
 public static class RebirthWoundApertureOcclusion {
  // Cosine-weighted visibility of the opening from each interior vertex.
  // This approximates the surrounding skin as a plane with the actual SDF
  // aperture. It does not account for mutual occlusion between internal organs.
  public static void Apply(Mesh mesh,Texture2D field,Vector3 aperture,Vector3 right,Vector3 up,Vector3 outward,Vector2 radii){
   if(mesh==null||field==null||radii.x<=0||radii.y<=0)throw new ArgumentException("Invalid wound aperture");
   var positions=mesh.vertices;var normals=mesh.normals;
   if(normals.Length!=positions.Length)throw new ArgumentException("Wound occlusion requires normals");
   var pixels=field.GetPixels();int width=field.width,height=field.height;
   var colours=new Color[positions.Length];const int samples=64;
   for(int i=0;i<positions.Length;i++){
    Vector3 p=positions[i],n=normals[i].normalized;
    float depth=Vector3.Dot(aperture-p,outward),visible=0;
    if(depth<=.0001f){colours[i]=Color.white;continue;}
    Vector3 tangent=Vector3.Cross(Mathf.Abs(n.y)<.95f?Vector3.up:Vector3.right,n).normalized,bitangent=Vector3.Cross(n,tangent);
    for(int j=0;j<samples;j++){
     float r=Mathf.Sqrt((j+.5f)/samples),angle=j*2.39996323f;
     Vector3 direction=tangent*(r*Mathf.Cos(angle))+bitangent*(r*Mathf.Sin(angle))+n*Mathf.Sqrt(1-r*r);
     float cosine=Vector3.Dot(direction,outward);if(cosine<=1e-6f)continue;
     Vector3 hit=p+direction*(depth/cosine)-aperture;
     float u=Vector3.Dot(hit,right)/radii.x*.5f+.5f,v=Vector3.Dot(hit,up)/radii.y*.5f+.5f;
     if(u<=0||u>=1||v<=0||v>=1)continue;
     float x=u*(width-1),y=v*(height-1);int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);
     float a=Mathf.Lerp(pixels[iy*width+ix].r,pixels[iy*width+ix+1].r,x-ix),b=Mathf.Lerp(pixels[(iy+1)*width+ix].r,pixels[(iy+1)*width+ix+1].r,x-ix);
     if(Mathf.Lerp(a,b,y-iy)>0)visible++;
    }
    float visibility=visible/samples;colours[i]=new Color(visibility,visibility,visibility,1);
   }
   mesh.colors=colours;
  }
 }
}
