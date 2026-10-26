using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio {
 [Serializable] public sealed class AuthoredBodyData {
  public int version;public string label,license,templateHash,sourceUrl,sourceObjSha256,sourceProxySha256,credits;
  public float referenceHeight,referenceAge,referenceMasculine;
  public int[] sourceIds,faceSizes,faceIndices,mapIndices;
  public float[] positions,uv,mapWeights,ownership;
  public int[] surfaceMapIndices;public float[] surfaceMapWeights;public string surfaceBindingMethod,surfaceBindingReferenceHash;
 }
 public static class RebirthAuthoredBody {
  [Serializable] public sealed class ImageSurface {
   public string foundationHash;public float referenceHeight;public bool surfaceFrames;
   public float[] positions,referencePositions,mapWeights;public int[] mapIndices;
   public RebirthBodySubdivisionCache.Sampling sampling;
  }
  sealed class Entry {public string dependency,hash;public AuthoredBodyData data;public readonly Dictionary<int,float[]> fields=new Dictionary<int,float[]>();}
  static readonly Dictionary<string,Entry> cache=new Dictionary<string,Entry>();
  static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
  static Entry Read(BodyRecipe recipe){
   string path=AssetDatabase.GUIDToAssetPath(recipe.authoredBodyGuid);var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(path);
   if(asset==null)throw new FileNotFoundException("The saved authored body foundation is missing");
   string dependency=AssetDatabase.GetAssetDependencyHash(path).ToString();Entry entry;
   if(!cache.TryGetValue(recipe.authoredBodyGuid,out entry)||entry.dependency!=dependency){
    var d=JsonUtility.FromJson<AuthoredBodyData>(asset.text);int count=d?.sourceIds?.Length??0;
    if(d==null||d.version!=1||d.license!="CC0-1.0"||count<10||d.positions==null||d.positions.Length!=count*3||d.uv==null||d.uv.Length!=count*2||d.mapIndices==null||d.mapIndices.Length!=count*3||d.mapWeights==null||d.mapWeights.Length!=count*3||d.ownership==null||d.ownership.Length!=count||d.faceSizes==null||d.faceIndices==null||d.faceSizes.Any(v=>v<3)||d.faceSizes.Sum()!=d.faceIndices.Length||d.faceIndices.Any(v=>v<0||v>=count)||d.mapIndices.Any(v=>v<0)||d.sourceIds.Any(v=>v<0))throw new InvalidDataException("Invalid authored body topology or correspondence");
    if(!Finite(d.referenceHeight)||d.referenceHeight<.5f||d.referenceHeight>3||!Finite(d.referenceAge)||d.referenceAge<0||d.referenceAge>1||!Finite(d.referenceMasculine)||d.referenceMasculine<0||d.referenceMasculine>1||d.positions.Concat(d.uv).Any(v=>!Finite(v))||d.mapWeights.Any(v=>!Finite(v)||Mathf.Abs(v)>2)||d.ownership.Any(v=>!Finite(v)||v<0||v>1))throw new InvalidDataException("Invalid authored body coordinates or weights");
    if(d.surfaceMapIndices!=null&&d.surfaceMapIndices.Length>0){if(d.surfaceMapIndices.Length!=count*3||d.surfaceMapWeights==null||d.surfaceMapWeights.Length!=count*3||d.surfaceMapIndices.Any(i=>i<0)||d.surfaceMapWeights.Any(w=>!Finite(w)||w<0||w>1)||string.IsNullOrEmpty(d.surfaceBindingReferenceHash)||d.surfaceBindingReferenceHash.Length!=64)throw new InvalidDataException("Invalid authored body surface binding");for(int i=0;i<count;i++)if(Mathf.Abs(d.surfaceMapWeights[i*3]+d.surfaceMapWeights[i*3+1]+d.surfaceMapWeights[i*3+2]-1)>1e-4f)throw new InvalidDataException("Surface weights must sum to one");}
    for(int i=0;i<count;i++)if(Mathf.Abs(d.mapWeights[i*3]+d.mapWeights[i*3+1]+d.mapWeights[i*3+2]-1)>1e-4f)throw new InvalidDataException("Authored body correspondence weights must sum to one");
    using(var sha=System.Security.Cryptography.SHA256.Create())entry=new Entry{dependency=dependency,hash=BitConverter.ToString(sha.ComputeHash(asset.bytes)).Replace("-","").ToLowerInvariant(),data=d};cache[recipe.authoredBodyGuid]=entry;
   }
   if(!string.IsNullOrEmpty(recipe.authoredBodyHash)&&recipe.authoredBodyHash!=entry.hash)throw new InvalidDataException("Saved body requires its original authored foundation revision");recipe.authoredBodyHash=entry.hash;return entry;
  }
  public static bool HasSurfaceBinding(BodyRecipe recipe){var d=Read(recipe).data;return d.surfaceMapIndices!=null&&d.surfaceMapIndices.Length>0;}
  static FaceTemplate Layout(AuthoredBodyData d)=>new FaceTemplate{sourceIds=d.sourceIds,uv=d.uv,faceSizes=d.faceSizes,faceIndices=d.faceIndices};
  public static ImageSurface ImageSampling(BodyTemplate template,BodyRecipe recipe,bool? surfaceFrameOverride=null){
   bool surfaceFrames=surfaceFrameOverride??recipe.authoredSurfaceFrames;
   var entry=Read(recipe);var d=entry.data;if(d.templateHash!=template.contentHash)throw new InvalidDataException("Authored body image fit requires the matching template");
   var layout=new BodyTemplate{sourceIds=d.sourceIds,uv=d.uv,topology=new BodyTopology{faceSizes=d.faceSizes,faceIndices=d.faceIndices}};
   if(surfaceFrames&&(d.surfaceMapIndices==null||d.surfaceMapIndices.Length==0))throw new InvalidDataException("Surface-frame sampling requires actual rest-triangle bindings");
   var reference=RebirthBodyGeneratorModel.Build(template,new BodyRecipe{height=recipe.height,age=d.referenceAge,masculine=d.referenceMasculine});
   try{return new ImageSurface{surfaceFrames=surfaceFrames,foundationHash=entry.hash,referenceHeight=d.referenceHeight,positions=d.positions,referencePositions=reference.vertices.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(),mapIndices=surfaceFrames?d.surfaceMapIndices:d.mapIndices,mapWeights=surfaceFrames?d.surfaceMapWeights:d.mapWeights,sampling=RebirthBodySubdivisionCache.ExportSampling(layout,recipe.subdivision)};}finally{UnityEngine.Object.DestroyImmediate(reference);}
  }
  public static float[] Ownership(BodyRecipe recipe,int vertexCount){var entry=Read(recipe);float[] values;if(!entry.fields.TryGetValue(recipe.subdivision,out values)){var d=entry.data;List<Vector3> points;List<Vector2> uv;List<int> triangles;RebirthFaceGeneratorModel.CatmullClark(Layout(d),d.ownership.Select(v=>new Vector3(v,0,0)).ToList(),Enumerable.Range(0,d.sourceIds.Length).Select(i=>new Vector2(d.uv[i*2],d.uv[i*2+1])).ToList(),recipe.subdivision,out points,out uv,out triangles);values=points.Select(p=>Mathf.Clamp01(p.x)).ToArray();entry.fields[recipe.subdivision]=values;}if(values.Length!=vertexCount)throw new InvalidDataException("Authored body neck ownership differs from mesh topology");return (float[])values.Clone();}
  public static Mesh Build(BodyTemplate template,BodyRecipe recipe,bool? surfaceFrameOverride=null){
   bool rotateSurfaceOffsets=surfaceFrameOverride??recipe.authoredSurfaceFrames;
   var d=Read(recipe).data;if(d.templateHash!=template.contentHash)throw new InvalidDataException("Authored body correspondence requires its matching body template");
   if(recipe.authoredSurfaceFrames&&(d.surfaceMapIndices==null||d.surfaceMapIndices.Length==0))throw new InvalidDataException("Selected authored body has no complete surface binding");
   var currentRecipe=JsonUtility.FromJson<BodyRecipe>(JsonUtility.ToJson(recipe));currentRecipe.authoredBodyGuid="";currentRecipe.authoredBodyHash="";currentRecipe.authoredSurfaceFrames=false;currentRecipe.subdivision=0;
   Mesh current=null,reference=null;try{
    current=RebirthBodyGeneratorModel.Build(template,currentRecipe);reference=RebirthBodyGeneratorModel.Build(template,new BodyRecipe{height=recipe.height,age=d.referenceAge,masculine=d.referenceMasculine});var actual=current.vertices;var neutral=reference.vertices;if(d.mapIndices.Any(i=>i>=actual.Length))throw new InvalidDataException("Authored body correspondence exceeds base vertex count");
    var wrapIds=d.surfaceMapIndices!=null&&d.surfaceMapIndices.Length>0?d.surfaceMapIndices:d.mapIndices;var wrapWeights=d.surfaceMapIndices!=null&&d.surfaceMapIndices.Length>0?d.surfaceMapWeights:d.mapWeights;if(rotateSurfaceOffsets&&wrapIds.Any(i=>i>=actual.Length))throw new InvalidDataException("Surface binding exceeds the base mesh");
    int wrapFallbacks=0;var points=new List<Vector3>(d.sourceIds.Length);for(int i=0;i<d.sourceIds.Length;i++){var point=new Vector3(d.positions[i*3],d.positions[i*3+1],d.positions[i*3+2])*(recipe.height/d.referenceHeight);int offset=i*3;if(rotateSurfaceOffsets){int a=wrapIds[offset],b=wrapIds[offset+1],c=wrapIds[offset+2];Vector3 transformed;bool bound=RebirthTriangleSurfaceWrap.TryTransform(point,neutral[a],neutral[b],neutral[c],actual[a],actual[b],actual[c],new Vector3(wrapWeights[offset],wrapWeights[offset+1],wrapWeights[offset+2]),out transformed);point=transformed;if(!bound)wrapFallbacks++;}else for(int j=0;j<3;j++){int k=offset+j;point+=(actual[d.mapIndices[k]]-neutral[d.mapIndices[k]])*d.mapWeights[k];}points.Add(point);}if(rotateSurfaceOffsets)Debug.Log("REBIRTH_BODY_SURFACE_WRAP vertices="+points.Count+" degenerateFallbacks="+wrapFallbacks);
    var coordinates=Enumerable.Range(0,points.Count).Select(i=>new Vector2(d.uv[i*2],d.uv[i*2+1])).ToList();List<Vector3> refined;List<Vector2> uv;List<int> triangles;RebirthFaceGeneratorModel.CatmullClark(Layout(d),points,coordinates,recipe.subdivision,out refined,out uv,out triangles);
    var mesh=new Mesh{name=recipe.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(refined);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();var normals=mesh.normals;foreach(var group in Enumerable.Range(0,refined.Count).GroupBy(i=>Vector3Int.RoundToInt(refined[i]*1000000))){var normal=group.Aggregate(Vector3.zero,(sum,i)=>sum+normals[i]).normalized;foreach(int i in group)normals[i]=normal;}mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
   }finally{if(current!=null)UnityEngine.Object.DestroyImmediate(current);if(reference!=null)UnityEngine.Object.DestroyImmediate(reference);}
  }
  public static void Export(BodyRecipe recipe,string folder){if(string.IsNullOrEmpty(recipe.authoredBodyGuid))return;Read(recipe);string target=folder+"/AuthoredBodyFoundation.json";if(!AssetDatabase.CopyAsset(AssetDatabase.GUIDToAssetPath(recipe.authoredBodyGuid),target))throw new IOException("Could not copy authored body foundation");recipe.authoredBodyGuid=AssetDatabase.AssetPathToGUID(target);}
 }
}

