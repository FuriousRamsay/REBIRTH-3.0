using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio {
 [Serializable] public sealed class FaceEyeSettings {
  public int mode,colour;public float cloudiness=.75f,smoothness=.65f;
  public Color irisTint=Color.white,scleraTint=Color.white;
  public void Validate(){mode=Mathf.Clamp(mode,0,2);colour=Mathf.Clamp(colour,0,8);foreach(float v in new[]{cloudiness,smoothness,irisTint.r,irisTint.g,irisTint.b,scleraTint.r,scleraTint.g,scleraTint.b})if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidOperationException("Invalid eye appearance.");cloudiness=Mathf.Clamp01(cloudiness);smoothness=Mathf.Clamp01(smoothness);}
 }
 [Serializable] public sealed class FaceSkinSettings {
  public bool tintEnabled,foundation;
  public string foundationKey="",foundationHash="";
  public Color tint=Color.white;
  public void Validate(){foreach(float v in new[]{tint.r,tint.g,tint.b})if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidOperationException("Invalid skin tint.");tint=new Color(Mathf.Clamp(tint.r,.01f,2),Mathf.Clamp(tint.g,.01f,2),Mathf.Clamp(tint.b,.01f,2),1);}
 }
 public static class RebirthFaceAppearance {
  public static void FoundationSkin(Material material,FaceSkinSettings settings=null){if(settings!=null&&!string.IsNullOrEmpty(settings.foundationKey)){material.mainTexture=RebirthSkinFoundations.Load(settings.foundationKey,settings.foundationHash);material.color=Color.white;material.SetFloat("_Glossiness",.2f);return;}string path=AssetDatabase.FindAssets("SkinFoundation t:Texture2D").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/FaceGeneratorData/SkinFoundation.png",StringComparison.Ordinal));if(path==null)throw new FileNotFoundException("SkinFoundation.png is missing.");material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);material.color=Color.white;material.SetFloat("_Glossiness",.2f);}
  public static readonly string[] EyeModes={"Reference eyes","Natural eyes","Clouded eyes"};
  public static readonly string[] EyeColours={"Brown","Light brown","Blue","Deep blue","Light blue","Blue-green","Green","Grey","Ice"};
  static readonly string[] EyeFiles={"brown_eye","brownlight_eye","blue_eye","deepblue_eye","lightblue_eye","bluegreen_eye","green_eye","grey_eye","ice_eye"};
  public static Material EyeMaterial(FaceRecipe recipe){
   var settings=recipe.eyes;
   if(settings.mode==0){var source=string.IsNullOrEmpty(recipe.generatedEyeMaterialGuid)?null:AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(recipe.generatedEyeMaterialGuid));var result=source!=null?new Material(source):new Material(Shader.Find("Standard"));if(source==null){result.color=new Color(.24f,.25f,.24f);result.SetFloat("_Glossiness",.45f);}return result;}
   string key=EyeFiles[settings.colour];string path=AssetDatabase.FindAssets(key+" t:Texture2D").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/FaceAppearanceData/Eyes/"+key+".png",StringComparison.Ordinal));
   if(path==null)throw new FileNotFoundException("Authored eye assets are missing. Install FaceAppearanceData with the editor scripts.");
   var shader=Shader.Find("REBIRTH/AnatomicalEye");if(shader==null)throw new FileNotFoundException("AnatomicalEye.shader is missing.");
   var material=new Material(shader){name="Authored eyes"};material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);material.SetColor("_IrisTint",settings.irisTint);material.SetColor("_ScleraTint",settings.scleraTint);material.SetFloat("_Cloudiness",settings.mode==2?settings.cloudiness:0);material.SetFloat("_Glossiness",settings.smoothness);return material;
  }
  public static string SaveReferenceEyes(FaceRecipe recipe,string folder){
   var source=string.IsNullOrEmpty(recipe.generatedEyeMaterialGuid)?null:AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(recipe.generatedEyeMaterialGuid));if(source==null)return null;
   var copy=new Material(source){name="Reference eyes",hideFlags=HideFlags.None};
   foreach(string property in copy.GetTexturePropertyNames()){var texture=copy.GetTexture(property);if(texture==null)continue;string input=AssetDatabase.GetAssetPath(texture);if(!input.StartsWith("Assets/",StringComparison.Ordinal))continue;string output=AssetDatabase.GenerateUniqueAssetPath(folder+"/Reference_"+Path.GetFileName(input));if(!AssetDatabase.CopyAsset(input,output))throw new IOException("Could not copy reference eye texture.");copy.SetTexture(property,AssetDatabase.LoadAssetAtPath<Texture>(output));}
   CopyShader(copy,folder);string path=folder+"/ReferenceEyes.mat";AssetDatabase.CreateAsset(copy,path);return AssetDatabase.AssetPathToGUID(path);
  }
  public static void CopyShader(Material material,string folder){
   string input=AssetDatabase.GetAssetPath(material.shader);if(!input.StartsWith("Assets/",StringComparison.Ordinal)||!input.EndsWith(".shader",StringComparison.OrdinalIgnoreCase))return;
   string output=AssetDatabase.GenerateUniqueAssetPath(folder+"/"+Path.GetFileName(input));
   // This project include is required by body blood materials in an exported
   // snapshot. Keep it beside the shader, and refuse conflicting snapshots.
   string sourceText=File.ReadAllText(input);
   foreach(string include in new[]{"RebirthBlood.cginc","RebirthBloodLighting.cginc"})if(sourceText.Contains("\""+include+"\"")){
    string dependency=Path.GetDirectoryName(input).Replace('\\','/')+"/"+include,target=folder+"/"+include;
    if(!File.Exists(dependency))throw new FileNotFoundException("Shared blood shader include is missing",dependency);
    if(File.Exists(target)){if(File.ReadAllText(target)!=File.ReadAllText(dependency))throw new IOException("Conflicting shared blood shader include in export");}
    else if(!AssetDatabase.CopyAsset(dependency,target))throw new IOException("Could not export shared blood shader include");
   }
   if(!AssetDatabase.CopyAsset(input,output))throw new IOException("Could not copy exported shader.");
   // A snapshot must not register the source shader's name again: otherwise
   // Shader.Find can resolve an older export after the tool's shader is updated.
   string text=File.ReadAllText(output);var declaration=new System.Text.RegularExpressions.Regex("Shader\\s+\"[^\"]+\"");
   text=declaration.Replace(text,"Shader \"REBIRTH/Exported/"+Guid.NewGuid().ToString("N")+"\"",1);File.WriteAllText(output,text);AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceUpdate);material.shader=AssetDatabase.LoadAssetAtPath<Shader>(output);
  }
  public static readonly string[] SkinTints={"Reference / unchanged","Warm light","Warm medium","Warm deep","Neutral deep","Ashen"};
  static readonly Color[] Tints={Color.white,new Color(1.05f,.94f,.84f),new Color(.70f,.49f,.34f),new Color(.34f,.19f,.115f),new Color(.25f,.19f,.16f),new Color(.69f,.73f,.65f)};
  public static void SkinPreset(FaceSkinSettings settings,int index){index=Mathf.Clamp(index,0,Tints.Length-1);settings.tintEnabled=index!=0;settings.tint=Tints[index];}
  public static void ApplySkin(Material material,FaceRecipe recipe){
   if(material==null||recipe==null)return;
   if(recipe.skin.tintEnabled&&material.HasProperty("_Color"))material.color*=recipe.skin.tint;
   RebirthFaceBlood.Apply(material,recipe,0);
  }
 }
}

