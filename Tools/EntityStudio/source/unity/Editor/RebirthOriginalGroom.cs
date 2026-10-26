using System;
using System.Collections.Generic;
using UnityEngine;
namespace RebirthEntityStudio {
 // Versioned artist-authored curve recipe. Only the growth bindings come from
 // the CC0 source; none of its strand positions are copied into this groom.
 public static class RebirthOriginalGroom {
  public const string LegacySweptCropKey="rebirth_swept_crop_v1";
  public const string SparseSweptCropKey="rebirth_swept_crop_v2";
  public const string ExpandedSweptCropKey="rebirth_swept_crop_v3";
  public const string EarClearanceCropKey="rebirth_ear_clearance_crop_v7";
  public const string FeatheredCropKey="rebirth_feathered_crop_v6";
  public const string SurfaceCropKey="rebirth_surface_crop_v5";
  public const string SweptCropKey="rebirth_swept_crop_v4";
  static Vector3 Point(float[] values,int id)=>new Vector3(values[id*3],values[id*3+1],values[id*3+2]);
  public static GuideHairAsset SweptCrop(FaceTemplate template,GuideHairAsset footprint,int revision=4){
   float[] boundary=revision>=6?BoundaryDistances(template,GrowthFaces(template,footprint,revision>=7)):null;
   if(revision>1)footprint=DistributeRoots(template,footprint,revision==2?1600:revision>=5?3600:2200,revision>=3,revision>=7);
   int count=footprint.bindingIds.Length/3;
   if(count==0||footprint.bindingWeights.Length!=count*3)throw new ArgumentException("Original groom needs valid scalp bindings.");
   float top=float.MinValue;for(int i=0;i<template.positions.Length/3;i++)top=Mathf.Max(top,template.positions[i*3+1]);
   var positions=new List<float>();var offsets=new List<int>{0};var density=boundary==null?null:new float[count];
   using(var surface=revision>=4?new GuideHairSurfaceConstraint(template):null){
   for(int guide=0;guide<count;guide++){
    int a=footprint.bindingIds[guide*3],b=footprint.bindingIds[guide*3+1],c=footprint.bindingIds[guide*3+2];
    var pa=Point(template.positions,a);var pb=Point(template.positions,b);var pc=Point(template.positions,c);
    var cross=Vector3.Cross(pb-pa,pc-pa);var normal=cross.sqrMagnitude>1e-20f?cross/Mathf.Sqrt(cross.sqrMagnitude):Vector3.zero;if(normal.sqrMagnitude<.5f)throw new ArgumentException("Original groom scalp triangle collapsed.");
    var root=pa*footprint.bindingWeights[guide*3]+pb*footprint.bindingWeights[guide*3+1]+pc*footprint.bindingWeights[guide*3+2];
    float edgeBlend=1;if(boundary!=null){float distance=boundary[a]*footprint.bindingWeights[guide*3]+boundary[b]*footprint.bindingWeights[guide*3+1]+boundary[c]*footprint.bindingWeights[guide*3+2];edgeBlend=Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/.018f));density[guide]=edgeBlend;}
    float sides=Mathf.Clamp01((top-root.y-.045f)/.10f);
    var comb=Vector3.Lerp(Vector3.back,Vector3.down,sides)+Vector3.right*.25f;
    var flow=Vector3.ProjectOnPlane(comb,normal).normalized;
    if(flow.sqrMagnitude<.5f)flow=Vector3.ProjectOnPlane(Vector3.right,normal).normalized;
    // Stable artist variation, independent of Unity's global random state.
    float variation=.82f+.18f*Mathf.Sin(guide*2.39996323f);
    for(int sample=0;sample<=12;sample++){
     float t=sample/12f;
     var point=root+normal*(.0015f+.012f*Mathf.Sin(t*Mathf.PI*.8f))+flow*(.045f*variation*t*(boundary==null?1:Mathf.Lerp(.15f,1,edgeBlend)));
     if(surface!=null)point=surface.OnSurface(point,.0015f+.003f*Mathf.Sin(t*Mathf.PI)*(boundary==null?1:Mathf.Lerp(.25f,1,edgeBlend)));
     positions.Add(point.x);positions.Add(point.y);positions.Add(point.z);
    }
    offsets.Add(positions.Count/3);
   }
   return new GuideHairAsset{version=1,name=revision==1?LegacySweptCropKey:revision==2?SparseSweptCropKey:revision==3?ExpandedSweptCropKey:revision>=7?EarClearanceCropKey:revision>=6?FeatheredCropKey:revision>=5?SurfaceCropKey:SweptCropKey,rootDensity=density,growthDistance=boundary,positions=positions.ToArray(),curveOffsets=offsets.ToArray(),bindingIds=(int[])footprint.bindingIds.Clone(),bindingWeights=(float[])footprint.bindingWeights.Clone()};
   }
  }
  // Euclidean distance to the growth footprint's seam-welded boundary.
  // This is a grooming field, not an anatomical measurement or geodesic solver.
  static float[] BoundaryDistances(FaceTemplate template,int[] faces){
   int count=template.positions.Length/3;var canonical=new int[count];var weld=new Dictionary<Vector3Int,int>();
   for(int i=0;i<count;i++){var key=Vector3Int.RoundToInt(Point(template.positions,i)*100000);if(!weld.TryGetValue(key,out int id))weld[key]=id=i;canonical[i]=id;}
   var edges=new Dictionary<Tuple<int,int>,int>();var included=new HashSet<int>();
   for(int i=0;i<faces.Length;i+=3)for(int k=0;k<3;k++){int a=canonical[faces[i+k]],b=canonical[faces[i+(k+1)%3]];included.Add(a);if(a==b)continue;var edge=Tuple.Create(Mathf.Min(a,b),Mathf.Max(a,b));edges.TryGetValue(edge,out int n);edges[edge]=n+1;}
   var boundary=new List<Tuple<Vector3,Vector3>>();foreach(var edge in edges)if(edge.Value==1)boundary.Add(Tuple.Create(Point(template.positions,edge.Key.Item1),Point(template.positions,edge.Key.Item2)));
   if(boundary.Count==0)throw new ArgumentException("Growth footprint has no boundary.");var result=new float[count];
   for(int i=0;i<count;i++){if(!included.Contains(canonical[i]))continue;var p=Point(template.positions,i);float best=float.PositiveInfinity;foreach(var edge in boundary){var delta=edge.Item2-edge.Item1;float t=Mathf.Clamp01(Vector3.Dot(p-edge.Item1,delta)/Mathf.Max(1e-12f,delta.sqrMagnitude));best=Mathf.Min(best,Vector3.Distance(p,edge.Item1+delta*t));}result[i]=best;}
   return result;
  }
  static int[] GrowthFaces(FaceTemplate template,GuideHairAsset footprint,bool excludeEars=false){
   var earVertices=new HashSet<int>();if(excludeEars){foreach(var morph in template.morphs)if(morph.id.StartsWith("ears/",StringComparison.Ordinal)&&morph.id.EndsWith("-ear-flap-incr",StringComparison.Ordinal))earVertices.UnionWith(morph.indices);if(earVertices.Count==0)throw new ArgumentException("This groom requires the installed extended ear regions.");}
   int count=template.positions.Length/3;var neighbours=new HashSet<int>[count];for(int i=0;i<count;i++)neighbours[i]=new HashSet<int>();
   for(int i=0;i<template.triangles.Length;i+=3)for(int k=0;k<3;k++){int a=template.triangles[i+k],b=template.triangles[i+(k+1)%3];neighbours[a].Add(b);neighbours[b].Add(a);}
   var seams=new Dictionary<Vector3Int,List<int>>();
   for(int i=0;i<count;i++){var key=Vector3Int.RoundToInt(Point(template.positions,i)*100000);if(!seams.TryGetValue(key,out var ids))seams[key]=ids=new List<int>();ids.Add(i);}
   foreach(var ids in seams.Values)foreach(int a in ids)foreach(int b in ids)if(a!=b)neighbours[a].Add(b);
   var distance=new float[count];for(int i=0;i<count;i++)distance[i]=float.PositiveInfinity;
   var queue=new SortedSet<Tuple<float,int>>();foreach(int id in footprint.bindingIds){distance[id]=0;queue.Add(Tuple.Create(0f,id));}
   const float border=.012f;
   while(queue.Count>0){var next=queue.Min;queue.Remove(next);int id=next.Item2;if(next.Item1>distance[id]||next.Item1>border)continue;
    foreach(int other in neighbours[id]){float candidate=next.Item1+Vector3.Distance(Point(template.positions,id),Point(template.positions,other));if(candidate>=distance[other]||candidate>border+.015f)continue;distance[other]=candidate;queue.Add(Tuple.Create(candidate,other));}
   }
   var result=new List<int>();for(int i=0;i<template.triangles.Length;i+=3){int a=template.triangles[i],b=template.triangles[i+1],c=template.triangles[i+2];if(Mathf.Max(distance[a],Mathf.Max(distance[b],distance[c]))<=border&&(!excludeEars||((earVertices.Contains(a)?1:0)+(earVertices.Contains(b)?1:0)+(earVertices.Contains(c)?1:0)<2)))result.AddRange(new[]{a,b,c});}
   return result.ToArray();
  }
  static GuideHairAsset DistributeRoots(FaceTemplate template,GuideHairAsset footprint,int count,bool expand,bool excludeEars=false){
   var faces=new List<int[]>();var cumulative=new List<float>();var seen=new HashSet<string>();float area=0;
   var source=expand?GrowthFaces(template,footprint,excludeEars):footprint.bindingIds;
   for(int i=0;i<source.Length;i+=3){
    int a=source[i],b=source[i+1],c=source[i+2];var sorted=new[]{a,b,c};Array.Sort(sorted);
    if(!seen.Add(string.Join(",",sorted)))continue;
    float value=Vector3.Cross(Point(template.positions,b)-Point(template.positions,a),Point(template.positions,c)-Point(template.positions,a)).magnitude*.5f;
    if(value<1e-12f)continue;area+=value;faces.Add(new[]{a,b,c});cumulative.Add(area);
   }
   if(area<=0)throw new ArgumentException("Original groom growth footprint has no surface area.");
   var ids=new int[count*3];var weights=new float[count*3];int face=0;
   for(int i=0;i<count;i++){
    float target=(i+.5f)/count*area;while(face<cumulative.Count-1&&cumulative[face]<target)face++;
    float u=Mathf.Repeat((i+1)*.61803398875f,1),v=Mathf.Repeat((i+1)*.41421356237f,1),s=Mathf.Sqrt(u);
    ids[i*3]=faces[face][0];ids[i*3+1]=faces[face][1];ids[i*3+2]=faces[face][2];weights[i*3]=1-s;weights[i*3+1]=s*(1-v);weights[i*3+2]=s*v;
   }
   return new GuideHairAsset{bindingIds=ids,bindingWeights=weights};
  }
 }
}
