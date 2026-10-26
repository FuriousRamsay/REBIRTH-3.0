using System;using System.Collections.Generic;using System.IO;using System.Linq;using UnityEditor;using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class BodyTopology {
  public int version;public string templateHash,sourceCommit,sourceObjSha256,license;public int[] faceSizes,faceIndices;
 }
 public static class RebirthBodySubdivision {
  public static void Attach(BodyTemplate template){
   string path=AssetDatabase.FindAssets("BodyTopology t:TextAsset").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/BodyGeneratorData/BodyTopology.json",StringComparison.Ordinal));if(path==null)return;
   var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(path);var data=JsonUtility.FromJson<BodyTopology>(asset.text);if(data.version!=1||data.license!="CC0-1.0"||data.templateHash!=template.contentHash||data.sourceCommit!=template.sourceCommit)throw new InvalidDataException("Body quad topology requires its matching base template");
   if(data.faceSizes==null||data.faceIndices==null||data.faceSizes.Any(s=>s<3)||data.faceSizes.Sum()!=data.faceIndices.Length||data.faceIndices.Any(i=>i<0||i>=template.sourceIds.Length))throw new InvalidDataException("Invalid body quad topology");
   var triangles=new List<int>();int start=0;foreach(int count in data.faceSizes){for(int i=1;i<count-1;i++)triangles.AddRange(new[]{data.faceIndices[start],data.faceIndices[start+i],data.faceIndices[start+i+1]});start+=count;}if(!triangles.SequenceEqual(template.triangles))throw new InvalidDataException("Body quads do not reproduce the base triangle mesh");template.topology=data;using(var sha=System.Security.Cryptography.SHA256.Create())template.topologyHash=BitConverter.ToString(sha.ComputeHash(asset.bytes)).Replace("-","").ToLowerInvariant();
  }
  public static void Validate(BodyTemplate template,BodyRecipe recipe){
   if(recipe.subdivision<0||recipe.subdivision>2)throw new InvalidDataException("Unsupported body subdivision level");if(recipe.subdivision==0)return;if(template.topology==null)throw new FileNotFoundException("Install BodyTopology.json to enable smooth body surfaces");if(!string.IsNullOrEmpty(recipe.topologyHash)&&recipe.topologyHash!=template.topologyHash)throw new InvalidDataException("Saved smooth body requires its original quad topology");recipe.topologyHash=template.topologyHash;
  }
  public static void Evaluate(BodyTemplate template,BodyRecipe recipe,ref Vector3[] points,ref Vector2[] uv,out int[] triangles){
   Validate(template,recipe);triangles=template.triangles;if(recipe.subdivision==0)return;var topology=template.topology;
   Vector3[] refined;Vector2[] refinedUv;RebirthBodySubdivisionCache.Evaluate(template,recipe.subdivision,points,out refined,out refinedUv,out triangles);points=refined;uv=refinedUv;
  }
 }
}
