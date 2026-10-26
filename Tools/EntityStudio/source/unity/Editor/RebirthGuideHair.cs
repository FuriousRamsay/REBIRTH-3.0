using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class GuideHairAsset {public int version;public string name;public float[] positions,bindingWeights,widthAxes,halfWidths,rootDensity,growthDistance,pointBindingWeights;public int[] curveOffsets,bindingIds,strandCounts,pointBindingIds;}
 public static class RebirthGuideHair {
  public static readonly string[] ScalpKeys={"","short_clustered",RebirthOriginalGroom.EarClearanceCropKey,RebirthOriginalGroom.FeatheredCropKey,RebirthOriginalGroom.SurfaceCropKey,RebirthOriginalGroom.SweptCropKey, "basic_short_hair","straight_hair_to_shoulder","shoulder_clustered","hedgehog","back_pigtail"};
  public static readonly string[] ScalpLabels={"Converted card library","Authored short strand groups (preview)","Original crop with ear clearance (preview)","Original feathered crop (preview)","Original surface crop (preview)","Original swept crop (preview)","Short guide groom","Shoulder guide groom","Clustered shoulder groom","Spiked guide groom","Gathered guide groom"};
  static readonly Dictionary<string,GuideHairAsset> cache=new Dictionary<string,GuideHairAsset>();static string folder;
  static GuideHairAsset Load(FaceTemplate template,string key){
   GuideHairAsset data;if(cache.TryGetValue(key,out data))return data;
   if(key==RebirthOriginalGroom.EarClearanceCropKey||key==RebirthOriginalGroom.FeatheredCropKey||key==RebirthOriginalGroom.SurfaceCropKey||key==RebirthOriginalGroom.SweptCropKey||key==RebirthOriginalGroom.LegacySweptCropKey||key==RebirthOriginalGroom.SparseSweptCropKey||key==RebirthOriginalGroom.ExpandedSweptCropKey){data=RebirthOriginalGroom.SweptCrop(template,Load(template,"basic_short_hair"),key==RebirthOriginalGroom.LegacySweptCropKey?1:key==RebirthOriginalGroom.SparseSweptCropKey?2:key==RebirthOriginalGroom.ExpandedSweptCropKey?3:key==RebirthOriginalGroom.EarClearanceCropKey?7:key==RebirthOriginalGroom.FeatheredCropKey?6:key==RebirthOriginalGroom.SurfaceCropKey?5:4);cache.Add(key,data);return data;}
   string path=AssetDatabase.FindAssets("GuideCatalog t:TextAsset").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/GuideHairData/GuideCatalog.json",StringComparison.Ordinal));if(path==null)throw new FileNotFoundException("GuideHairData is missing.");folder=Path.GetDirectoryName(path).Replace('\\','/');var catalog=JsonUtility.FromJson<AuthoredHairCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text);if(catalog.version!=1||catalog.templateHash!=template.contentHash)throw new InvalidDataException("Guide hair requires its pinned face template.");
   var text=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/"+key+".json");if(text==null)throw new FileNotFoundException("Missing hair guides: "+key);data=JsonUtility.FromJson<GuideHairAsset>(text.text);if(data.version!=1)throw new InvalidDataException("Unsupported guide data.");cache.Add(key,data);return data;
  }
  static Vector3 Point(float[] source,int i){return new Vector3(source[i*3],source[i*3+1],source[i*3+2]);}
  public static Mesh BuildWithCoverage(FaceTemplate template,FaceRecipe recipe,Mesh head){
   Mesh cards=null,scalp=null;var result=new Mesh{name="Guide groom with root coverage",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
   try{
    var h=recipe.hair;cards=Build(template,recipe,h.guideKey,h.guideWidth,(h.guideKey==RebirthOriginalGroom.EarClearanceCropKey||h.guideKey==RebirthOriginalGroom.FeatheredCropKey||h.guideKey==RebirthOriginalGroom.SurfaceCropKey||h.guideKey==RebirthOriginalGroom.SweptCropKey||h.guideKey==RebirthOriginalGroom.SparseSweptCropKey||h.guideKey==RebirthOriginalGroom.ExpandedSweptCropKey)?6:12,h.guideLayers,h.guideLength,h.guideTaper,head);scalp=BuildScalp(template,recipe,h.guideKey,head);
    result.CombineMeshes(new[]{new CombineInstance{mesh=cards,subMeshIndex=0,transform=Matrix4x4.identity},new CombineInstance{mesh=scalp,subMeshIndex=0,transform=Matrix4x4.identity}},false,false);result.RecalculateBounds();return result;
   }catch{UnityEngine.Object.DestroyImmediate(result);throw;}finally{if(cards!=null)UnityEngine.Object.DestroyImmediate(cards);if(scalp!=null)UnityEngine.Object.DestroyImmediate(scalp);}
  }
  static Vector3[] Frame(Vector3 a,Vector3 b,Vector3 c){var edge=b-a;var cross=Vector3.Cross(edge,c-a);var tangent=edge.sqrMagnitude>1e-20f?edge/Mathf.Sqrt(edge.sqrMagnitude):Vector3.zero;var normal=cross.sqrMagnitude>1e-20f?cross/Mathf.Sqrt(cross.sqrMagnitude):Vector3.zero;if(tangent.sqrMagnitude<.5f||normal.sqrMagnitude<.5f)throw new InvalidDataException("Degenerate guide root triangle.");return new[]{tangent,Vector3.Cross(normal,tangent).normalized,normal};}
  static Vector3 Sample(Vector3[] points,float t){float u=Mathf.Clamp01(t)*(points.Length-1);int i=Mathf.Min(points.Length-2,(int)u);float f=u-i;Vector3 a=points[Mathf.Max(0,i-1)],b=points[i],c=points[i+1],d=points[Mathf.Min(points.Length-1,i+2)];return .5f*((2*b)+(-a+c)*f+(2*a-5*b+4*c-d)*f*f+(-a+3*b-3*c+d)*f*f*f);}
  static Vector3 SurfaceSample(FaceTemplate template,Vector3[] skin,GuideHairAsset asset,int point,out Vector3 widthAxis){
   int offset=point*3,a=asset.pointBindingIds[offset],b=asset.pointBindingIds[offset+1],c=asset.pointBindingIds[offset+2];
   var oldFrame=Frame(Point(template.positions,a),Point(template.positions,b),Point(template.positions,c));var currentFrame=Frame(skin[a],skin[b],skin[c]);
   var oldAnchor=Vector3.zero;var anchor=Vector3.zero;for(int k=0;k<3;k++){int id=asset.pointBindingIds[offset+k];float w=asset.pointBindingWeights[offset+k];oldAnchor+=Point(template.positions,id)*w;anchor+=skin[id]*w;}
   var delta=Point(asset.positions,point)-oldAnchor;var axis=Point(asset.widthAxes,point);widthAxis=Vector3.zero;var result=anchor;
   for(int k=0;k<3;k++){result+=currentFrame[k]*Vector3.Dot(delta,oldFrame[k]);widthAxis+=currentFrame[k]*Vector3.Dot(axis,oldFrame[k]);}widthAxis.Normalize();return result;
  }
  public static Mesh Build(FaceTemplate template,FaceRecipe recipe,string key,float width= .014f,int segments=12,int layers=2,float length=0,float taper=.6f,Mesh evaluatedHead=null){
   var asset=Load(template,key);var skin=RebirthFaceGeneratorModel.EvaluateSkinPoints(template,recipe);float shift=1.747f-skin.Max(v=>v.y);var vertices=new List<Vector3>();var uv=new List<Vector2>();var colours=new List<Color>();var triangles=new List<int>();
   bool surfaceBound=asset.pointBindingIds!=null&&asset.pointBindingIds.Length>0;
   if(surfaceBound&&(asset.pointBindingIds.Length!=asset.positions.Length||asset.pointBindingWeights==null||asset.pointBindingWeights.Length!=asset.positions.Length||asset.widthAxes==null||asset.widthAxes.Length!=asset.positions.Length))throw new InvalidDataException("Incomplete per-sample hair surface bindings.");
   using(var contact=evaluatedHead==null?new GuideHairSurfaceConstraint(template,recipe):new GuideHairSurfaceConstraint(evaluatedHead)){
   for(int curve=0;curve<asset.curveOffsets.Length-1;curve++){
    int[] ids={asset.bindingIds[curve*3],asset.bindingIds[curve*3+1],asset.bindingIds[curve*3+2]};var original=ids.Select(id=>Point(template.positions,id)).ToArray();var current=ids.Select(id=>skin[id]).ToArray();var oldFrame=Frame(original[0],original[1],original[2]);var newFrame=Frame(current[0],current[1],current[2]);var oldAnchor=Vector3.zero;var anchor=Vector3.zero;
    for(int k=0;k<3;k++){float w=asset.bindingWeights[curve*3+k];oldAnchor+=original[k]*w;anchor+=current[k]*w;}
    int curveSegments=segments;int startPoint=asset.curveOffsets[curve],count=asset.curveOffsets[curve+1]-startPoint;var points=new Vector3[count];var warpedAxes=surfaceBound?new Vector3[count]:null;var warpedRoot=Vector3.zero;
    for(int i=0;i<count;i++){
     if(surfaceBound){var p=SurfaceSample(template,skin,asset,startPoint+i,out warpedAxes[i]);if(i==0)warpedRoot=p;points[i]=warpedRoot+(p-warpedRoot)*(1+Mathf.Clamp(length,-.5f,1));points[i].y+=shift;points[i]=contact.Outside(points[i]);continue;}
     Vector3 root=Point(asset.positions,startPoint);
     Vector3 delta=root-oldAnchor+(Point(asset.positions,startPoint+i)-root)*(1+Mathf.Clamp(length,-.5f,1));
     points[i]=anchor+newFrame[0]*Vector3.Dot(delta,oldFrame[0])+newFrame[1]*Vector3.Dot(delta,oldFrame[1])+newFrame[2]*Vector3.Dot(delta,oldFrame[2]);
     points[i].y+=shift;points[i]=contact.Outside(points[i]);
    }
    var centres=Enumerable.Range(0,curveSegments+1).Select(i=>Sample(points,i/(float)curveSegments)).ToArray();var across=new Vector3[curveSegments+1];Vector3 previous=Vector3.zero;
    for(int i=0;i<centres.Length;i++){var tangent=(centres[Mathf.Min(curveSegments,i+1)]-centres[Mathf.Max(0,i-1)]).normalized;var axis=i==0?Vector3.Cross(tangent,newFrame[2]):previous-Vector3.Dot(previous,tangent)*tangent;if(axis.sqrMagnitude<1e-8f)axis=Vector3.Cross(tangent,Mathf.Abs(tangent.y)<.9f?Vector3.up:Vector3.right);previous=axis.normalized;across[i]=previous;}
    if(asset.halfWidths!=null&&asset.halfWidths.Length>0){curveSegments=count-1;centres=points;across=new Vector3[count];for(int i=0;i<count;i++){var axis=Point(asset.widthAxes,startPoint+i);across[i]=surfaceBound?warpedAxes[i]:(newFrame[0]*Vector3.Dot(axis,oldFrame[0])+newFrame[1]*Vector3.Dot(axis,oldFrame[1])+newFrame[2]*Vector3.Dot(axis,oldFrame[2])).normalized;}layers=Mathf.Clamp(layers,1,3);}
    float rootDensity=asset.rootDensity!=null?asset.rootDensity[curve]:1;
    for(int layer=0;layer<layers;layer++){if(layer>0&&rootDensity<.5f)continue;
     int begin=vertices.Count;bool surfaceCrop=key==RebirthOriginalGroom.EarClearanceCropKey||key==RebirthOriginalGroom.SurfaceCropKey||key==RebirthOriginalGroom.FeatheredCropKey;int column=key=="short_clustered"?Mathf.Min(layer,2):asset.strandCounts!=null&&asset.strandCounts.Length>curve?(asset.strandCounts[curve]>100?1:asset.strandCounts[curve]>60?2:asset.strandCounts[curve]>35?3:asset.strandCounts[curve]>20?4:5):(layer==0?(surfaceCrop?curve%3:0):3+curve%3);
     if(rootDensity<.3f)column=7;else if(rootDensity<.6f)column=5;
     for(int i=0;i<centres.Length;i++){float t=i/(float)curveSegments;var tangent=(centres[Mathf.Min(curveSegments,i+1)]-centres[Mathf.Max(0,i-1)]).normalized;var axis=Quaternion.AngleAxis(key=="short_clustered"?(layer-(layers-1)*.5f)*6f:surfaceCrop?(layer-(layers-1)*.5f)*12f:layer*180f/Mathf.Max(1,layers),tangent)*across[i];float half=asset.halfWidths!=null&&asset.halfWidths.Length>0?asset.halfWidths[startPoint+i]*(width/.014f)*(1-Mathf.Clamp01(taper)*t*t*t*t):width*.5f*(1-Mathf.Clamp01(taper)*t*t*t*t);for(int side=0;side<2;side++){var outward=Vector3.Cross(axis,tangent).normalized;if(Vector3.Dot(outward,newFrame[2])<0)outward=-outward;vertices.Add(contact.Outside(centres[i]+axis*((side==0?-1:1)*half*Mathf.Lerp(.2f,1,rootDensity))+outward*(layer==0?0:.0012f)));uv.Add(new Vector2((column+(side==0?.04f:.96f))/8f,1-t));colours.Add(new Color(1,1,t,1));}}
     for(int i=0;i<curveSegments;i++){int a=begin+i*2;triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
    }
   }
   Debug.Log("REBIRTH_GUIDE_CONTACT corrected="+contact.corrected+" maxMetres="+contact.maximumCorrection);
   var mesh=new Mesh{name="Guided "+key,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colours);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;}
  }
  // Coverage is attached to the evaluated skin. Distances follow mesh edges, so the
  // footprint cannot jump through the skull to paint the face or opposite ear.
  public static Mesh BuildScalp(FaceTemplate template,FaceRecipe recipe,string key,Mesh head=null){
   var asset=Load(template,key);var points=RebirthFaceGeneratorModel.EvaluateSkinPoints(template,recipe);
   int count=points.Length;
   var neighbours=Enumerable.Range(0,count).Select(i=>new HashSet<int>()).ToArray();
   for(int i=0;i<template.triangles.Length;i+=3)for(int k=0;k<3;k++){int a=template.triangles[i+k],b=template.triangles[i+(k+1)%3];neighbours[a].Add(b);neighbours[b].Add(a);}
   var coincident=new Dictionary<Vector3Int,List<int>>();
   for(int i=0;i<count;i++){var p=Point(template.positions,i);var cell=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));List<int> ids;if(!coincident.TryGetValue(cell,out ids))coincident[cell]=ids=new List<int>();ids.Add(i);}
   foreach(var ids in coincident.Values)foreach(int a in ids)foreach(int b in ids)if(a!=b)neighbours[a].Add(b);
   var distance=Enumerable.Repeat(float.PositiveInfinity,count).ToArray();var pending=new SortedSet<Tuple<float,int>>();
   foreach(int id in asset.bindingIds.Distinct()){distance[id]=0;pending.Add(Tuple.Create(0f,id));}
   const float border=.019f;
   while(pending.Count>0){var entry=pending.Min;pending.Remove(entry);float d=entry.Item1;int id=entry.Item2;if(d>distance[id]||d>border)continue;foreach(int next in neighbours[id]){float candidate=d+Vector3.Distance(points[id],points[next]);if(candidate>=distance[next]||candidate>border+.015f)continue;distance[next]=candidate;pending.Add(Tuple.Create(candidate,next));}}
   // Interpolate the coverage field with the exact head subdivision topology.
   // The undercoat then uses the evaluated skin itself, avoiding coarse facets
   // intersecting a separately subdivided head.
   var field=distance.Select((d,i)=>new Vector3((1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.007f,border,d)))*(asset.growthDistance==null?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(asset.growthDistance[i]/.012f))),0,0)).ToList();
   var coords=Enumerable.Range(0,count).Select(i=>new Vector2(template.uv[i*2],template.uv[i*2+1])).ToList();
   var fieldTriangles=template.triangles.ToList();
   if(recipe.resolution>0)RebirthFaceGeneratorModel.CatmullClark(template,field,coords,recipe.resolution,out field,out coords,out fieldTriangles);
   bool ownHead=head==null;if(ownHead)head=RebirthFaceGeneratorModel.Build(template,recipe);
   try{
    var surface=head.vertices;var normal=head.normals;
    if(field.Count>surface.Length||normal.Length!=surface.Length)throw new InvalidDataException("Scalp coverage and evaluated head layouts differ.");
    var selected=new List<int>();var map=new Dictionary<int,int>();var vertices=new List<Vector3>();var colours=new List<Color>();var uv=new List<Vector2>();
    var skinTriangles=head.GetTriangles(0);
    for(int i=0;i<skinTriangles.Length;i+=3){int a=skinTriangles[i],b=skinTriangles[i+1],c=skinTriangles[i+2];
     if(a>=field.Count||b>=field.Count||c>=field.Count||Mathf.Max(field[a].x,Mathf.Max(field[b].x,field[c].x))<=.001f)continue;
     foreach(int id in new[]{a,b,c}){if(!map.TryGetValue(id,out int target)){target=vertices.Count;map.Add(id,target);vertices.Add(surface[id]+normal[id]*.0006f);colours.Add(new Color(1,1,1,Mathf.Clamp01(field[id].x)));uv.Add(coords[id]);}selected.Add(target);}
    }
    var mesh=new Mesh{name="Evaluated guide root coverage",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetColors(colours);mesh.SetUVs(0,uv);mesh.SetTriangles(selected,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
   }finally{if(ownHead)UnityEngine.Object.DestroyImmediate(head);}
  }
  public static Material ScalpMaterial(Color colour){var shader=Shader.Find("REBIRTH/GuideHairScalp");if(shader==null)throw new FileNotFoundException("GuideHairScalp.shader is missing.");var material=new Material(shader){name="Guide root coverage"};material.SetColor("_Color",colour*.55f);return material;}
  public static Material ScalpMaterial(FaceHairSettings settings){var material=ScalpMaterial(Color.Lerp(settings.rootColor,new Color(.5f,.51f,.48f),settings.grey));material.SetFloat("_Coverage",settings.guideCoverage);RebirthHairCondition.Apply(material,settings.condition);return material;}
  public static Material Material(FaceHairSettings settings){
   // Locate the original atlas without relying on an earlier mesh build.
   if(string.IsNullOrEmpty(folder)){string path=AssetDatabase.FindAssets("GuideCatalog t:TextAsset").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/GuideHairData/GuideCatalog.json",StringComparison.Ordinal));if(path==null)throw new FileNotFoundException("GuideHairData is missing.");folder=Path.GetDirectoryName(path).Replace('\\','/');}
   var material=Material(settings.color);material.SetColor("_RootColor",settings.rootColor);material.SetFloat("_Glossiness",settings.shine);material.SetFloat("_Grey",settings.grey);RebirthHairCondition.Apply(material,settings.condition);return material;
  }
  public static Material Material(Color colour){
   string albedo=folder+"/StrandAtlas_Albedo.png",normal=folder+"/StrandAtlas_Normal.png";var importer=(TextureImporter)AssetImporter.GetAtPath(albedo);if(importer==null)throw new FileNotFoundException("Strand atlas is missing.");if(!importer.alphaIsTransparency||importer.textureCompression!=TextureImporterCompression.Uncompressed){importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.mipMapsPreserveCoverage=true;importer.alphaTestReferenceValue=.35f;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.anisoLevel=8;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();}var normalImporter=(TextureImporter)AssetImporter.GetAtPath(normal);if(normalImporter.textureType!=TextureImporterType.NormalMap){normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.convertToNormalmap=false;normalImporter.textureCompression=TextureImporterCompression.Uncompressed;normalImporter.maxTextureSize=2048;normalImporter.SaveAndReimport();}
   string controls=folder+"/StrandAtlas_Control.png";var controlImporter=(TextureImporter)AssetImporter.GetAtPath(controls);if(controlImporter==null)throw new FileNotFoundException("Strand lighting controls are missing.");if(controlImporter.sRGBTexture||controlImporter.textureCompression!=TextureImporterCompression.Uncompressed){controlImporter.sRGBTexture=false;controlImporter.textureCompression=TextureImporterCompression.Uncompressed;controlImporter.mipmapEnabled=true;controlImporter.wrapMode=TextureWrapMode.Clamp;controlImporter.maxTextureSize=2048;controlImporter.anisoLevel=8;controlImporter.SaveAndReimport();}
   var shader=Shader.Find("REBIRTH/GuideHairCards");if(shader==null)throw new FileNotFoundException("GuideHairCards.shader is missing.");var material=new Material(shader){name="Guided hair"};material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));material.SetTexture("_StrandControl",AssetDatabase.LoadAssetAtPath<Texture2D>(controls));material.SetColor("_Color",colour);material.SetColor("_RootColor",colour*.5f);return material;
  }
 }
}





