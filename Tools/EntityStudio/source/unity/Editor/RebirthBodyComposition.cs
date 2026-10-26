using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio {
 [Serializable] public sealed class BodyExtendedData {
  public int version;public string templateHash,sourceCommit,license;
  public FaceControl[] controls;public FaceMorph[] morphs,jointMorphs;
 }
 public static class RebirthBodyComposition {
  // This exact revision only appends default-zero controls. Existing targets,
  // joint targets and controls were compared entry-for-entry before release.
  public static bool CompatibleExtension(string saved,string current)=>saved==current||
   (saved=="c3e67ccf454673ae403b8f6805b4ce49f233d8ff911efd2c06337b45fbb7c51c"&&
    current=="9367eb3565fa6b369a2515a0b2fd067f47e5b573111571410f2b92a8abb5b68d");
  public static string DataPath()=>AssetDatabase.FindAssets("BodyExtendedTargets t:TextAsset").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/BodyGeneratorData/BodyExtendedTargets.json",StringComparison.Ordinal));
  public static void Attach(BodyTemplate template){
   string path=DataPath();if(path==null)return;
   var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(path);var extra=JsonUtility.FromJson<BodyExtendedData>(asset.text);
   if(extra.version!=1||extra.templateHash!=template.contentHash||extra.sourceCommit!=template.sourceCommit||extra.license!="CC0-1.0")throw new InvalidDataException("Body regional targets do not match their anatomical template");
   template.controls=template.controls.Concat(extra.controls).ToArray();template.morphs=template.morphs.Concat(extra.morphs).ToArray();template.jointMorphs=template.jointMorphs.Concat(extra.jointMorphs).ToArray();
   if(template.controls.Select(c=>c.id).Distinct().Count()!=template.controls.Length||template.morphs.Select(m=>m.id).Distinct().Count()!=template.morphs.Length)throw new InvalidDataException("Duplicate body control or target");
   using(var sha=System.Security.Cryptography.SHA256.Create())template.extensionHash=BitConverter.ToString(sha.ComputeHash(asset.bytes)).Replace("-","").ToLowerInvariant();
  }
  static float Value(BodyRecipe recipe,string id)=>recipe.values.FirstOrDefault(v=>v.id==id)?.value??0;
  static float Weight(float value,int index)=>index==0?Mathf.Max(0,-value):index==1?1-Mathf.Abs(value):Mathf.Max(0,value);
  public static void Apply(Vector3[] points,Dictionary<string,FaceMorph> targets,BodyRecipe recipe){
   float breastSize=Value(recipe,"breast_size"),firmness=Value(recipe,"breast_firmness");
   if(breastSize!=0||firmness!=0){
    var cups=new[]{"mincup","averagecup","maxcup"};var firms=new[]{"minfirmness","averagefirmness","maxfirmness"};
    foreach(string age in new[]{"young","old"})for(int c=0;c<3;c++)for(int f=0;f<3;f++){
     float amount=(1-recipe.masculine)*(age=="young"?1-recipe.age:recipe.age)*(Weight(breastSize,c)*Weight(firmness,f)-(c==1&&f==1?1:0));
     if(amount==0)continue;FaceMorph target;
     if(targets.TryGetValue("breast/female-"+age+"-averagemuscle-averageweight-"+cups[c]+"-"+firms[f],out target))RebirthFaceGeneratorModel.Apply(points,target,amount);
    }
   }
   float muscle=Value(recipe,"muscle_mass"),fat=Value(recipe,"body_fat");if(muscle==0&&fat==0)return;
   var muscleNames=new[]{"minmuscle","averagemuscle","maxmuscle"};var fatNames=new[]{"minweight","averageweight","maxweight"};
   foreach(string sex in new[]{"female","male"})foreach(string age in new[]{"young","old"}){
    float identity=(sex=="female"?1-recipe.masculine:recipe.masculine)*(age=="young"?1-recipe.age:recipe.age);
    if(identity==0)continue;string prefix="macrodetails/universal-"+sex+"-"+age+"-";
    for(int m=0;m<3;m++)for(int f=0;f<3;f++){
     float amount=identity*(Weight(muscle,m)*Weight(fat,f)-(m==1&&f==1?1:0));
     if(amount==0)continue;FaceMorph target;if(!targets.TryGetValue(prefix+muscleNames[m]+"-"+fatNames[f],out target))throw new InvalidDataException("Missing body composition target");
     RebirthFaceGeneratorModel.Apply(points,target,amount);
    }
   }
  }
 }
}
