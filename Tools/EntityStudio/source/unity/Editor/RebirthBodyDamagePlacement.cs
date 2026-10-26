using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 // Editor-only coverage authoring. Coverage stamps are not finished wound artwork.
 public static class RebirthBodyDamagePlacement {
  public static bool Pick(Mesh mesh,Ray ray,out Vector3 point,out Vector3 normal){
   point=normal=Vector3.zero;float nearest=float.PositiveInfinity;var p=mesh.vertices;var t=mesh.triangles;var n=mesh.normals;
   for(int k=0;k<t.Length;k+=3){int a=t[k],b=t[k+1],c=t[k+2];Vector3 e1=p[b]-p[a],e2=p[c]-p[a],h=Vector3.Cross(ray.direction,e2);float d=Vector3.Dot(e1,h);if(Mathf.Abs(d)<1e-9f)continue;float inv=1/d;Vector3 s=ray.origin-p[a];float u=inv*Vector3.Dot(s,h);if(u<0||u>1)continue;Vector3 q=Vector3.Cross(s,e1);float v=inv*Vector3.Dot(ray.direction,q);if(v<0||u+v>1)continue;float distance=inv*Vector3.Dot(e2,q);if(distance<0||distance>=nearest)continue;nearest=distance;point=ray.GetPoint(distance);normal=(n[a]*(1-u-v)+n[b]*u+n[c]*v).normalized;}
   return !float.IsPositiveInfinity(nearest);
  }
  static Texture2D CopyMask(string guid){
   if(string.IsNullOrEmpty(guid)){var blank=new Texture2D(1024,1024,TextureFormat.RGBA32,false,true);blank.SetPixels32(new Color32[1024*1024]);blank.Apply();return blank;}
   string path=AssetDatabase.GUIDToAssetPath(guid);var source=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
   if(source==null||importer==null||importer.sRGBTexture||importer.alphaIsTransparency)throw new InvalidDataException("Choose a linear body damage mask with alpha transparency disabled");
   var copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);var previous=RenderTexture.active;bool srgb=GL.sRGBWrite;var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
   try{GL.sRGBWrite=false;Graphics.Blit(source,rt);RenderTexture.active=rt;copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0);copy.Apply();return copy;}catch{UnityEngine.Object.DestroyImmediate(copy);throw;}finally{RenderTexture.active=previous;GL.sRGBWrite=srgb;RenderTexture.ReleaseTemporary(rt);}
  }
  static Texture2D Atlas(int shape){string filename=shape==5?"BloodSmearCoverage_v1":"InjuryShapeAtlas_v1";string path=AssetDatabase.FindAssets(filename+" t:Texture2D").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/BodyGeneratorData/"+filename+".png",StringComparison.Ordinal));var atlas=path==null?null:AssetDatabase.LoadAssetAtPath<Texture2D>(path);var importer=path==null?null:AssetImporter.GetAtPath(path) as TextureImporter;if(atlas==null||importer==null||!importer.isReadable||importer.sRGBTexture)throw new InvalidDataException("Install the readable linear injury-shape atlas");return atlas;}
  public static string Stamp(Mesh mesh,Vector3 center,Vector3 normal,float radius,float strength,string previousGuid,int channel,bool erase,int shape=0,float rotation=0,float aspect=1){
   if(mesh==null||channel<0||channel>3||float.IsNaN(radius)||float.IsInfinity(radius)||radius<=0||radius>.2f||float.IsNaN(strength)||float.IsInfinity(strength)||strength<=0||strength>1)throw new ArgumentException("Invalid injury brush");
   if(shape<0||shape>5||float.IsNaN(rotation)||float.IsInfinity(rotation)||float.IsNaN(aspect)||float.IsInfinity(aspect)||aspect<.25f||aspect>2)throw new ArgumentException("Invalid injury stamp shape");
   normal=normal.normalized;Vector3 right=Vector3.Cross(Mathf.Abs(Vector3.Dot(normal,Vector3.up))>.95f?Vector3.forward:Vector3.up,normal).normalized,up=Vector3.Cross(normal,right);float cosine=Mathf.Cos(rotation*Mathf.Deg2Rad),sine=Mathf.Sin(rotation*Mathf.Deg2Rad);var atlas=shape==0?null:Atlas(shape);float support=shape==0?radius:radius*Mathf.Sqrt(1+aspect*aspect);
   var texture=CopyMask(previousGuid);try{int w=texture.width,h=texture.height;var pixels=texture.GetPixels();var coverage=new float[pixels.Length];var p=mesh.vertices;var n=mesh.normals;var uv=mesh.uv;var tri=mesh.triangles;
    for(int k=0;k<tri.Length;k+=3){int a=tri[k],b=tri[k+1],c=tri[k+2];var bounds=new Bounds(p[a],Vector3.zero);bounds.Encapsulate(p[b]);bounds.Encapsulate(p[c]);if(bounds.SqrDistance(center)>support*support)continue;
     Vector2 e1=uv[b]-uv[a],e2=uv[c]-uv[a];float det=e1.x*e2.y-e1.y*e2.x;if(Mathf.Abs(det)<1e-12f)continue;
     int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(uv[a].x,Mathf.Min(uv[b].x,uv[c].x))*w),0,w-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(uv[a].x,Mathf.Max(uv[b].x,uv[c].x))*w),0,w-1);
     int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(uv[a].y,Mathf.Min(uv[b].y,uv[c].y))*h),0,h-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(uv[a].y,Mathf.Max(uv[b].y,uv[c].y))*h),0,h-1);
     for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){Vector2 q=new Vector2((x+.5f)/w,(y+.5f)/h)-uv[a];float u=(q.x*e2.y-q.y*e2.x)/det,v=(e1.x*q.y-e1.y*q.x)/det;if(u<0||v<0||u+v>1)continue;float t=1-u-v;if(Vector3.Dot((n[a]*t+n[b]*u+n[c]*v).normalized,normal)<.35f)continue;Vector3 delta=p[a]*t+p[b]*u+p[c]*v-center;float amount;
      if(shape==0){float distance=delta.magnitude/radius;if(distance>=1)continue;amount=1-Mathf.SmoothStep(0,1,distance);}
      else{if(Mathf.Abs(Vector3.Dot(delta,normal))>radius*.6f)continue;float dx=Vector3.Dot(delta,right),dy=Vector3.Dot(delta,up),sx=(dx*cosine+dy*sine)/(radius*aspect),sy=(-dx*sine+dy*cosine)/radius;if(Mathf.Abs(sx)>=1||Mathf.Abs(sy)>=1)continue;int col=shape==2||shape==4?1:0,row=shape<=2?1:0;amount=shape==5?Mathf.Clamp01((atlas.GetPixelBilinear(sx*.5f+.5f,sy*.5f+.5f).r-1f/255)/(1-1f/255)):atlas.GetPixelBilinear((col+(sx*.5f+.5f))*.5f,(row+(sy*.5f+.5f))*.5f).r;}
      amount*=strength;int index=y*w+x;coverage[index]=Mathf.Max(coverage[index],amount);}
    }
    bool changed=false;for(int i=0;i<pixels.Length;i++){if(coverage[i]<=0)continue;float value=erase?pixels[i][channel]*(1-coverage[i]):Mathf.Max(pixels[i][channel],coverage[i]);if(Mathf.Abs(value-pixels[i][channel])<1f/255)continue;var colour=pixels[i];colour[channel]=value;pixels[i]=colour;changed=true;}
    if(!changed)return previousGuid;
    texture.SetPixels(pixels);texture.Apply();string folder="Assets/BodyGeneratorGenerated/DamageCoverage";Directory.CreateDirectory(folder);string path=folder+"/Stamp_"+Guid.NewGuid().ToString("N")+".png";File.WriteAllBytes(path,texture.EncodeToPNG());AssetDatabase.ImportAsset(path);var output=(TextureImporter)AssetImporter.GetAtPath(path);output.sRGBTexture=false;output.alphaIsTransparency=false;output.alphaSource=TextureImporterAlphaSource.FromInput;output.textureCompression=TextureImporterCompression.Uncompressed;output.mipmapEnabled=false;output.wrapMode=TextureWrapMode.Clamp;output.maxTextureSize=Mathf.Max(w,h);output.SaveAndReimport();return AssetDatabase.AssetPathToGUID(path);
   }finally{UnityEngine.Object.DestroyImmediate(texture);}
  }
 }
}
