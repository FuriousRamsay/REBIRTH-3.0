using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class WoundSurfaceTextures {
  public string lining="",muscle="",bowel="",bone="",cartilage="";
  public string liningSmoothness="",muscleSmoothness="",bowelSmoothness="",boneSmoothness="",cartilageSmoothness="";
  public string GetSmoothness(string role){switch(role){case "lining":return liningSmoothness;case "muscle":return muscleSmoothness;case "bowel":return bowelSmoothness;case "bone":return boneSmoothness;case "cartilage":return cartilageSmoothness;default:throw new InvalidDataException("Unknown wound surface role");}}
  public void SetSmoothness(string role,string guid){switch(role){case "lining":liningSmoothness=guid;break;case "muscle":muscleSmoothness=guid;break;case "bowel":bowelSmoothness=guid;break;case "bone":boneSmoothness=guid;break;case "cartilage":cartilageSmoothness=guid;break;default:throw new InvalidDataException("Unknown wound surface role");}}
  public string Get(string role){switch(role){case "lining":return lining;case "muscle":return muscle;case "bowel":return bowel;case "bone":return bone;case "cartilage":return cartilage;default:throw new InvalidDataException("Unknown wound surface role");}}
  public void Set(string role,string guid){switch(role){case "lining":lining=guid;break;case "muscle":muscle=guid;break;case "bowel":bowel=guid;break;case "bone":bone=guid;break;case "cartilage":cartilage=guid;break;default:throw new InvalidDataException("Unknown wound surface role");}}
 }
 public static class RebirthWoundSurfaceTextures {
  static readonly string[] Roles={"lining","muscle","bowel","bone","cartilage"};
  static Texture2D Load(string guid,bool smoothness=false){
   string path=AssetDatabase.GUIDToAssetPath(guid);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
   if(texture==null)throw new FileNotFoundException("Wound surface texture is missing: "+guid);
   var importer=AssetImporter.GetAtPath(path) as TextureImporter;
   if(importer==null||importer.textureType!=TextureImporterType.Default||importer.sRGBTexture==smoothness)throw new InvalidDataException(smoothness?"Wound smoothness maps require a Default linear texture import (sRGB off); red channel, white smooth / black rough":"Wound colour textures require a Default sRGB texture import");
   return texture;
  }
  public static void Apply(BodyDeepWoundPart part,BodyDamageSettings settings,BodyDeepWound wound){
   var textures=settings.woundTextures;if(textures==null)return;
   string role=part.surfaceRole??(part.material.GetFloat("_UseTissue")<.5f?"bone":"lining");
   part.surfaceRole=role;
   string guid=textures.Get(role);
   if(!string.IsNullOrEmpty(guid)){
    part.material.SetTexture("_TissueTex",Load(guid));part.material.SetFloat("_UseTissue",1);
    part.material.SetColor("_Color",role=="bone"?wound.boneTint:wound.tissueTint);
   }
   string smoothness=textures.GetSmoothness(role);
   if(part.material.HasProperty("_UseSmoothnessMap")){part.material.SetFloat("_UseSmoothnessMap",0);part.material.DisableKeyword("_WOUND_SMOOTHNESS_MAP");}
   if(!string.IsNullOrEmpty(smoothness)){
    if(!part.material.HasProperty("_SmoothnessTex"))throw new InvalidOperationException("Install the wound shader with spatial smoothness support");
    part.material.SetTexture("_SmoothnessTex",Load(smoothness,true));part.material.SetFloat("_UseSmoothnessMap",1);part.material.EnableKeyword("_WOUND_SMOOTHNESS_MAP");
   }
   // Texture sampling and blood-coverage class are independent.
   if(!part.useBoneBloodCoverage.HasValue)part.useBoneBloodCoverage=role=="bone";
  }
  public static void Draw(WoundSurfaceTextures settings){
   EditorGUILayout.LabelField("Wound surface textures",EditorStyles.boldLabel);
   EditorGUILayout.LabelField("Optional, shared by all wounds. Empty slots retain the installed tissue materials. Blood colour and coating remain separate.",EditorStyles.wordWrappedMiniLabel);
   foreach(string role in Roles){
    string guid=settings.Get(role),path=AssetDatabase.GUIDToAssetPath(guid);
    var texture=string.IsNullOrEmpty(guid)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    EditorGUI.BeginChangeCheck();var selected=(Texture2D)EditorGUILayout.ObjectField(char.ToUpperInvariant(role[0])+role.Substring(1)+" albedo",texture,typeof(Texture2D),false);
    if(EditorGUI.EndChangeCheck())settings.Set(role,selected==null?"":AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(selected)));
    if(!string.IsNullOrEmpty(guid)){
     try{Load(guid);}catch(Exception e){EditorGUILayout.HelpBox(e.Message,MessageType.Warning);if(GUILayout.Button("Clear "+role+" texture")){settings.Set(role,"");GUI.changed=true;}}
    }
    string smoothness=settings.GetSmoothness(role);var map=string.IsNullOrEmpty(smoothness)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(smoothness));
    EditorGUI.BeginChangeCheck();var selectedMap=(Texture2D)EditorGUILayout.ObjectField(char.ToUpperInvariant(role[0])+role.Substring(1)+" smoothness",map,typeof(Texture2D),false);
    if(EditorGUI.EndChangeCheck())settings.SetSmoothness(role,selectedMap==null?"":AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(selectedMap)));
    if(!string.IsNullOrEmpty(smoothness))try{Load(smoothness,true);}catch(Exception e){EditorGUILayout.HelpBox(e.Message,MessageType.Warning);if(GUILayout.Button("Clear "+role+" smoothness")){settings.SetSmoothness(role,"");GUI.changed=true;}}
   }
   EditorGUILayout.LabelField("Smoothness: linear red channel; white retains the shine slider, black is rough. Blood wetness is applied afterward using the shared blood profile.",EditorStyles.wordWrappedMiniLabel);
  }
  public static Dictionary<Texture,Texture> Export(BodyDamageSettings saved,string folder){
   var result=new Dictionary<Texture,Texture>();var textures=saved.woundTextures;if(textures==null)return result;
   var guids=new Dictionary<string,string>();
   string Copy(string guid,string role,bool smoothness){
    if(string.IsNullOrEmpty(guid))return guid;
    var source=Load(guid,smoothness);
    string targetGuid;
    if(!guids.TryGetValue(guid,out targetGuid)){
     string path=AssetDatabase.GetAssetPath(source),target=folder+(smoothness?"/WoundSmoothness_":"/WoundAlbedo_")+role+Path.GetExtension(path);
     if(!AssetDatabase.CopyAsset(path,target))throw new IOException("Could not export wound colour texture: "+role);
     var copied=AssetDatabase.LoadAssetAtPath<Texture2D>(target);if(copied==null)throw new IOException("Exported wound colour texture is unreadable");
     targetGuid=AssetDatabase.AssetPathToGUID(target);guids.Add(guid,targetGuid);result.Add(source,copied);
    }
    return targetGuid;
   }
   foreach(string role in Roles){
    textures.Set(role,Copy(textures.Get(role),role,false));
    textures.SetSmoothness(role,Copy(textures.GetSmoothness(role),role,true));
   }
   return result;
  }
 }
}
