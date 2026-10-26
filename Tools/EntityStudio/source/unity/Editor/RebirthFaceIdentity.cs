using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class FaceIdentityData {public string sourceCommit,templateHash;public FaceMorph[] morphs;public FaceOralMesh[] meshes;}
 public static class RebirthFaceIdentity {
  public static void Attach(FaceTemplate template,string folder){
   var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/FaceIdentityMacros.json");if(asset==null)return;
   var data=JsonUtility.FromJson<FaceIdentityData>(asset.text);if(data.sourceCommit!=template.sourceCommit||data.templateHash!=template.contentHash)throw new InvalidDataException("Anatomical identity data does not match the face template.");
   template.morphs=template.morphs.Concat(data.morphs).ToArray();
   foreach(var addition in data.meshes){var target=addition.name==template.eyeSurface?.name?template.eyeSurface:template.oral?.meshes.FirstOrDefault(m=>m.name==addition.name);if(target==null)throw new InvalidDataException("Missing identity accessory: "+addition.name);target.morphs=target.morphs.Concat(addition.morphs).ToArray();}
   using(var sha=System.Security.Cryptography.SHA256.Create())template.identityHash=BitConverter.ToString(sha.ComputeHash(asset.bytes)).Replace("-","").ToLowerInvariant();
  }
  public static void Validate(FaceRecipe recipe,FaceTemplate template){
   foreach(float value in new[]{recipe.africanShape,recipe.asianShape})if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("Invalid anatomical shape blend.");
   recipe.africanShape=Mathf.Clamp01(recipe.africanShape);recipe.asianShape=Mathf.Clamp01(recipe.asianShape);float sum=recipe.africanShape+recipe.asianShape;if(sum>1){recipe.africanShape/=sum;recipe.asianShape/=sum;}
   if(recipe.africanShape+recipe.asianShape<=0)return;
   if(string.IsNullOrEmpty(template.identityHash))throw new InvalidOperationException("Install FaceIdentityMacros.json to use additional anatomical bases.");
   if(!string.IsNullOrEmpty(recipe.identityHash)&&recipe.identityHash!=template.identityHash)throw new InvalidOperationException("These settings require a different anatomical identity data revision.");recipe.identityHash=template.identityHash;
  }
  public static void Apply(Vector3[] positions,Dictionary<string,FaceMorph> targets,FaceRecipe recipe){
   float female=1-recipe.masculine,age=recipe.age;
   var families=new[]{"caucasian","african","asian"};var weights=new[]{1-recipe.africanShape-recipe.asianShape,recipe.africanShape,recipe.asianShape};
   for(int group=0;group<families.Length;group++){
    if(weights[group]<=0)continue;
    string prefix="macrodetails/"+families[group];
    RebirthFaceGeneratorModel.Apply(positions,targets[prefix+"-female-young"],female*(1-age)*weights[group]);
    RebirthFaceGeneratorModel.Apply(positions,targets[prefix+"-female-old"],female*age*weights[group]);
    RebirthFaceGeneratorModel.Apply(positions,targets[prefix+"-male-young"],(1-female)*(1-age)*weights[group]);
    RebirthFaceGeneratorModel.Apply(positions,targets[prefix+"-male-old"],(1-female)*age*weights[group]);
   }
  }
 }
}
