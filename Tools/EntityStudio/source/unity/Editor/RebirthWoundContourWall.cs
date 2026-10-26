using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
namespace RebirthEntityStudio {
 internal static class RebirthWoundContourWall {
  internal static Mesh Build(BodyWoundSurface skin,Texture2D field,Mesh floor,Vector3 aperture,Vector3 right,Vector3 up,Vector3 normal,Vector2 radii,string name){
   const int columns=49,rows=97;
   var floorPoints=floor.vertices;
   if(floorPoints.Length!=columns*rows)throw new InvalidDataException("Contour wall needs the matching lining grid");
   var vertices=new List<Vector3>();var triangles=new List<int>();
   var indices=new Dictionary<Vector2Int,int>();var degree=new Dictionary<Vector2Int,int>();
   Vector2Int Key(Vector2 uv){return new Vector2Int(Mathf.RoundToInt(uv.x*1000000),Mathf.RoundToInt(uv.y*1000000));}
   Vector3 FloorPoint(Vector2 uv){
    float x=Mathf.Clamp01(uv.x)*(columns-1),y=Mathf.Clamp01(uv.y)*(rows-1);
    int ix=Mathf.Min(columns-2,Mathf.FloorToInt(x)),iy=Mathf.Min(rows-2,Mathf.FloorToInt(y));
    float u=x-ix,v=y-iy;int a=iy*columns+ix,b=a+1,c=a+columns,d=c+1;
    return v<=u?floorPoints[a]*(1-u)+floorPoints[b]*(u-v)+floorPoints[d]*v:floorPoints[a]*(1-v)+floorPoints[d]*u+floorPoints[c]*(v-u);
   }
   int Vertex(Vector2 uv){
    var key=Key(uv);int index;if(indices.TryGetValue(key,out index))return index;
    Vector3 plane=aperture+right*((uv.x*2-1)*radii.x)+up*((uv.y*2-1)*radii.y);BodyWoundHit hit;
    if(!skin.Pick(new Ray(plane+normal*.2f,-normal),out hit))throw new InvalidOperationException("Wound contour leaves the skin; move or resize it");
    index=vertices.Count;indices.Add(key,index);vertices.Add(hit.point+normal*.00005f);vertices.Add(FloorPoint(uv));return index;
   }
   void Cross(Vector2 a,Vector2 b,float fa,float fb,ref int count,ref Vector2 first,ref Vector2 second){
    if((fa>0)==(fb>0))return;
    // Canonical edge order makes shared-cell intersections identical.
    if(a.x>b.x||(a.x==b.x&&a.y>b.y)){var swap=a;a=b;b=swap;float value=fa;fa=fb;fb=value;}
    Vector2 point=Vector2.Lerp(a,b,fa/(fa-fb));if(count==0)first=point;else second=point;count++;
   }
   void Emit(Vector2 a,Vector2 b,Vector2 c,float fa,float fb,float fc){
    int positives=(fa>0?1:0)+(fb>0?1:0)+(fc>0?1:0);if(positives==0||positives==3)return;
    int count=0;Vector2 first=Vector2.zero,second=Vector2.zero;
    Cross(a,b,fa,fb,ref count,ref first,ref second);Cross(b,c,fb,fc,ref count,ref first,ref second);Cross(c,a,fc,fa,ref count,ref first,ref second);
    if(count!=2||Key(first)==Key(second))return;
    Vector2 interior=((fa>0?a:Vector2.zero)+(fb>0?b:Vector2.zero)+(fc>0?c:Vector2.zero))/positives;
    Vector2 delta=second-first;if(Vector2.Dot(new Vector2(-delta.y,delta.x),interior-(first+second)*.5f)<0){var swap=first;first=second;second=swap;}
    int start=Vertex(first),end=Vertex(second);
    triangles.AddRange(new[]{start,end,end+1,start,end+1,start+1});
    foreach(var key in new[]{Key(first),Key(second)}){int value;degree.TryGetValue(key,out value);degree[key]=value+1;}
   }
   int width=field.width,height=field.height;var values=new float[width*height];
   // Unity's CPU bilinear lookup addresses texel x at x/width, whereas the
   // GPU addresses its centre at (x+.5)/width. Extract the contour in GPU UVs
   // from the actual texel values, not the shifted CPU-normalized footprint.
   for(int y=0;y<height;y++)for(int x=0;x<width;x++)values[y*width+x]=field.GetPixelBilinear(x/(float)width,y/(float)height).r;
   for(int y=0;y<height-1;y++)for(int x=0;x<width-1;x++){
    int a=y*width+x,b=a+1,c=a+width,d=c+1;
    Vector2 ua=new Vector2((x+.5f)/width,(y+.5f)/height),ub=new Vector2((x+1.5f)/width,ua.y),uc=new Vector2(ua.x,(y+1.5f)/height),ud=new Vector2(ub.x,uc.y);
    Emit(ua,ub,ud,values[a],values[b],values[d]);Emit(ua,ud,uc,values[a],values[d],values[c]);
   }
   if(triangles.Count==0)throw new InvalidDataException("Wound field has no closed contour");
   foreach(int value in degree.Values)if(value!=2)throw new InvalidDataException("Wound contour has an open or branching boundary");
   var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.HideAndDontSave};
   mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
  }
 }
}
