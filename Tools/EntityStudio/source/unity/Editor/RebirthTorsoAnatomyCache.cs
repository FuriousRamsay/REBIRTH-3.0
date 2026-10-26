using System;using System.IO;using System.Linq;using System.Security.Cryptography;using UnityEditor;using UnityEngine;
namespace RebirthEntityStudio {
 public static class RebirthTorsoAnatomyCache {
  static RebirthTorsoAnatomyLibrary resource;static string key;
  static RebirthTorsoAnatomyCache(){AssemblyReloadEvents.beforeAssemblyReload+=Clear;EditorApplication.quitting+=Clear;}
  public static void Clear(){resource?.Dispose();resource=null;key=null;}
  public static bool AssignDefault(BodyDamageSettings settings){
   if(!string.IsNullOrEmpty(settings.torsoLibraryGuid))return !string.IsNullOrEmpty(settings.torsoLibraryHash);
   string path=AssetDatabase.FindAssets("TorsoAnatomyLibrary_v1 t:TextAsset").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/BodyGeneratorData/TorsoAnatomy/TorsoAnatomyLibrary_v1.json",StringComparison.Ordinal));if(path==null)return false;
   using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())settings.torsoLibraryHash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();settings.torsoLibraryGuid=AssetDatabase.AssetPathToGUID(path);return true;
  }
  public static RebirthTorsoAnatomyLibrary Acquire(BodyTemplate template,BodyRecipe recipe){
   var settings=recipe.damage;string path=AssetDatabase.GUIDToAssetPath(settings.torsoLibraryGuid);if(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(settings.torsoLibraryHash))throw new InvalidOperationException("Torso anatomy source is missing; install the torso library before using rib or abdominal interiors");
   string next=path+"|"+settings.torsoLibraryHash+"|"+AssetDatabase.GetAssetDependencyHash(path)+"|"+template.contentHash+"|"+recipe.authoredBodyHash;
   if(resource==null||key!=next){Clear();resource=new RebirthTorsoAnatomyLibrary(path,settings.torsoLibraryHash,template.contentHash,recipe.authoredBodyHash);key=next;}return resource;
  }
  public static Vector3[] Cage(BodyTemplate template,BodyRecipe input,RebirthTorsoAnatomyLibrary library){
   var recipe=JsonUtility.FromJson<BodyRecipe>(JsonUtility.ToJson(input));
   foreach(var value in recipe.values)if(value.id=="muscle_mass"||value.id=="muscle_definition"||value.id.EndsWith("_fat",StringComparison.Ordinal)||value.id.EndsWith("_muscle",StringComparison.Ordinal)||value.id.StartsWith("breast_",StringComparison.Ordinal))value.value=0;
   Mesh mesh=RebirthBodyGeneratorModel.Build(template,recipe);try{return RebirthTorsoCage.Build(mesh,RebirthBodyAnatomicalJoints.Evaluate(template,recipe),library.definition.referenceGuides);}finally{UnityEngine.Object.DestroyImmediate(mesh);}
  }
 }
}
