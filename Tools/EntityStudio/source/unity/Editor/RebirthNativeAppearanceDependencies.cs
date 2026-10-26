using System;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace RebirthEntityStudio {
 [Serializable] public sealed class NativeAppearanceMaterialBinding {public string materialGuid,shaderName;}
 [InitializeOnLoad] public static class RebirthNativeAppearanceDependencies {
  [Serializable] sealed class MaterialMetadata {public NativeAppearanceMaterialBinding[] materials;}
  static RebirthNativeAppearanceDependencies(){if(!Application.isBatchMode)EditorApplication.delayCall+=RestoreAll;}
  static void RestoreAll(){foreach(string guid in AssetDatabase.FindAssets("NativeAppearance t:TextAsset")){string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.EndsWith("/NativeAppearance.json",StringComparison.Ordinal))continue;try{Restore(Path.GetDirectoryName(path).Replace('\\','/'));}catch(Exception e){Debug.LogWarning("Native appearance dependencies unavailable: "+path+". "+e.Message);}}}
  public static void Restore(string folder){
   string directory=Path.Combine(Path.GetFullPath(folder),"NativeShaders");if(!File.Exists(Path.Combine(directory,"shaders_assets_all.bundle")))throw new FileNotFoundException("Native appearance shader bundles are missing.");
   var shader=RebirthNativeShaderLibrary.Get("Game/SDCS/Hair",directory);if(shader==null||!shader.isSupported)throw new InvalidOperationException("Native hair shader is unsupported on this graphics backend.");
   var metadata=JsonUtility.FromJson<MaterialMetadata>(File.ReadAllText(Path.Combine(folder,"NativeAppearance.json")));
   if(metadata?.materials!=null){
    foreach(var binding in metadata.materials){string path=AssetDatabase.GUIDToAssetPath(binding.materialGuid);if(string.IsNullOrEmpty(path)||!path.StartsWith(folder.TrimEnd('/')+"/",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Native material binding leaves its authoring export.");var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null)throw new FileNotFoundException("An exported native material is missing.");if(binding.shaderName=="Game/SDCS/Hair")RebirthNativeMaterialPersistence.Bind(material,shader);else if(binding.shaderName=="Standard")material.shader=Shader.Find("Standard");else throw new InvalidDataException("Unsupported native material shader: "+binding.shaderName);}return;
   }
   // Older single-material snapshots had no explicit role metadata.
   foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{folder})){string path=AssetDatabase.GUIDToAssetPath(guid);string name=Path.GetFileName(path);if(!name.StartsWith("NativeHair_",StringComparison.Ordinal)&&!name.StartsWith("NativeFacialHair_",StringComparison.Ordinal))continue;var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null&&(material.shader==null||material.shader.name!="Standard"))material.shader=shader;}
  }
 }
}
