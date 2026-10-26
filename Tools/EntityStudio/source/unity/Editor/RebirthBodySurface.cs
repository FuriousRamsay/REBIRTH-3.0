using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class BodySurfaceSettings {
  public string normalGuid="",masksGuid="",presetGuid="";public float normalStrength=1,smoothness=.22f;
  public string microHeightGuid="",microNormalGuid="",microRegionsGuid="",microSourceGuid="";public float microStrength,microTileSize=.06f,microRoughness=.15f,microTorso=.5f,microLimbs=.35f,microExtremities=.2f;
  public void Validate(){foreach(float value in new[]{normalStrength,smoothness,microStrength,microTileSize,microRoughness,microTorso,microLimbs,microExtremities})if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Invalid body surface setting");normalStrength=Mathf.Clamp(normalStrength,0,2);smoothness=Mathf.Clamp01(smoothness);microStrength=Mathf.Clamp01(microStrength);microTileSize=Mathf.Clamp(microTileSize,.01f,.2f);microRoughness=Mathf.Clamp01(microRoughness);microTorso=Mathf.Clamp01(microTorso);microLimbs=Mathf.Clamp01(microLimbs);microExtremities=Mathf.Clamp01(microExtremities);}
 }
 [Serializable] public sealed class BodySurfacePreset {public int version=1;public string label,templateHash,sourceReportGuid;public BodySurfaceSettings surface;}
 public static class RebirthBodySurface {
  static double refreshAt;static string[] presetPaths=new string[0];
  static string[] PresetPaths(){if(EditorApplication.timeSinceStartup>=refreshAt){presetPaths=AssetDatabase.FindAssets("BodySurfacePreset t:TextAsset").Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".json",StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(p).StartsWith("BodySurfacePreset_",StringComparison.Ordinal)).ToArray();refreshAt=EditorApplication.timeSinceStartup+1;}return presetPaths;}
  static BodySurfacePreset ReadPreset(string path){try{var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(path);return asset==null?null:JsonUtility.FromJson<BodySurfacePreset>(asset.text);}catch(ArgumentException){return null;}}
  static Texture2D Load(string guid,string label){if(string.IsNullOrEmpty(guid))return null;var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));if(texture==null)throw new FileNotFoundException("Body "+label+" texture is missing");return texture;}
  // Provenance identifies the originating preset; equality describes all visible settings.
  public static bool MatchesPreset(BodySurfaceSettings current,BodySurfaceSettings preset){
   if(current==null||preset==null)return false;
   var a=JsonUtility.FromJson<BodySurfaceSettings>(JsonUtility.ToJson(current));
   var b=JsonUtility.FromJson<BodySurfaceSettings>(JsonUtility.ToJson(preset));
   a.presetGuid=b.presetGuid="";
   return JsonUtility.ToJson(a)==JsonUtility.ToJson(b);
  }
  public static void Apply(Material material,BodySurfaceSettings settings){
   settings.Validate();var normal=Load(settings.normalGuid,"normal");var masks=Load(settings.masksGuid,"surface mask");
   if(normal!=null){var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;if(importer!=null&&importer.textureType!=TextureImporterType.NormalMap)throw new InvalidDataException("Import the body normal texture as a Normal map first");material.SetTexture("_BumpMap",normal);}
   if(masks!=null){var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(masks)) as TextureImporter;if(importer!=null&&importer.sRGBTexture)throw new InvalidDataException("Body surface masks require sRGB disabled (linear data)");material.SetTexture("_SurfaceMasks",masks);}
   material.SetFloat("_BumpScale",settings.normalStrength);material.SetFloat("_Glossiness",settings.smoothness);material.SetFloat("_UseSurfaceMasks",masks!=null?1:0);
   RebirthBodyMicrodetail.Apply(material,settings);
  }
  public static BodySurfaceSettings Draw(BodySurfaceSettings settings,string templateHash){
   var profiles=PresetPaths().Select(p=>new{path=p,data=ReadPreset(p)}).Where(p=>p.data!=null&&p.data.version==1&&p.data.templateHash==templateHash&&p.data.surface!=null).ToArray();
   int current=Array.FindIndex(profiles,p=>MatchesPreset(settings,p.data.surface))+1;
   EditorGUI.BeginChangeCheck();int chosen=EditorGUILayout.Popup("Surface preset",current,new[]{"Custom / current"}.Concat(profiles.Select(p=>string.IsNullOrEmpty(p.data.label)?Path.GetFileNameWithoutExtension(p.path):p.data.label)).ToArray());
   if(chosen!=current&&chosen>0){settings=JsonUtility.FromJson<BodySurfaceSettings>(JsonUtility.ToJson(profiles[chosen-1].data.surface));settings.presetGuid=AssetDatabase.AssetPathToGUID(profiles[chosen-1].path);}
   var normal=(Texture2D)EditorGUILayout.ObjectField("Body detail normal",Load(settings.normalGuid,"normal"),typeof(Texture2D),false);var masks=(Texture2D)EditorGUILayout.ObjectField("Body surface masks",Load(settings.masksGuid,"surface mask"),typeof(Texture2D),false);
   float strength=EditorGUILayout.Slider("Skin detail strength",settings.normalStrength,0,2),smoothness=EditorGUILayout.Slider("Skin smoothness",settings.smoothness,0,1);
   EditorGUILayout.LabelField("Maps must match the body UVs. Masks: roughness R, metalness G, occlusion B; alpha reserved. Source game UVs cannot be assigned directly.",EditorStyles.wordWrappedMiniLabel);
   if(!EditorGUI.EndChangeCheck())return null;
   string normalGuid=normal==null?"":AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(normal)),masksGuid=masks==null?"":AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(masks));var edited=JsonUtility.FromJson<BodySurfaceSettings>(JsonUtility.ToJson(settings));edited.normalGuid=normalGuid;edited.masksGuid=masksGuid;edited.normalStrength=strength;edited.smoothness=smoothness;if(!MatchesPreset(edited,settings))edited.presetGuid="";return edited;
  }
  public static BodySurfaceSettings DrawMicrodetail(BodySurfaceSettings settings)=>RebirthBodyMicrodetail.Draw(settings);
  public static void Export(Material material,BodySurfaceSettings settings,string folder){
   RebirthBodyMicrodetail.Export(material,settings,folder);
   foreach(bool normal in new[]{true,false}){string guid=normal?settings.normalGuid:settings.masksGuid;if(string.IsNullOrEmpty(guid))continue;string source=AssetDatabase.GUIDToAssetPath(guid),target=folder+"/"+(normal?"BodyDetailNormal":"BodySurfaceMasks")+Path.GetExtension(source);if(!AssetDatabase.CopyAsset(source,target))throw new IOException("Could not copy body surface map");material.SetTexture(normal?"_BumpMap":"_SurfaceMasks",AssetDatabase.LoadAssetAtPath<Texture2D>(target));if(normal)settings.normalGuid=AssetDatabase.AssetPathToGUID(target);else settings.masksGuid=AssetDatabase.AssetPathToGUID(target);}
   if(!string.IsNullOrEmpty(settings.presetGuid)){var source=AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(settings.presetGuid));if(source==null)throw new FileNotFoundException("Body surface provenance preset is missing");var preset=JsonUtility.FromJson<BodySurfacePreset>(source.text);if(!string.IsNullOrEmpty(preset.sourceReportGuid)){string report=folder+"/BodySurfaceSourceReport.json";if(!AssetDatabase.CopyAsset(AssetDatabase.GUIDToAssetPath(preset.sourceReportGuid),report))throw new IOException("Could not copy body surface provenance report");preset.sourceReportGuid=AssetDatabase.AssetPathToGUID(report);}preset.surface=JsonUtility.FromJson<BodySurfaceSettings>(JsonUtility.ToJson(settings));preset.surface.presetGuid="";string target=folder+"/BodySurfacePreset.json";File.WriteAllText(target,JsonUtility.ToJson(preset,true));AssetDatabase.ImportAsset(target);settings.presetGuid=AssetDatabase.AssetPathToGUID(target);}
  }
 }
}
