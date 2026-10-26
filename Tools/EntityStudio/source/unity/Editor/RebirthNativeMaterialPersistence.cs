using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio {
 // AssetBundle shaders do not have project asset GUIDs. Keep their native
 // shader role in export metadata and use an export-local storage shader on disk.
 // The existing dependency adapter restores the actual preview shader in memory.
 public static class RebirthNativeMaterialPersistence {
  sealed class Binding {public Material material;public Shader shader,storage;public string beforeSave;}
  static readonly Dictionary<string,Binding> bindings=new Dictionary<string,Binding>(StringComparer.OrdinalIgnoreCase);
  static readonly HashSet<string> pending=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  static bool queued;

  static string sourcePath;
  static string SourcePath(){if(string.IsNullOrEmpty(sourcePath)||!File.Exists(sourcePath))sourcePath=AssetDatabase.FindAssets("NativeMaterialStorage t:Shader").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/REBIRTHEntityStudio/Shaders/NativeMaterialStorage.shader",StringComparison.Ordinal));return sourcePath;}
  public static void CopyStorageShader(string folder){string source=SourcePath();if(source==null)throw new FileNotFoundException("Install NativeMaterialStorage.shader");string target=folder+"/NativeMaterialStorage.shader";if(!File.Exists(target)&&!AssetDatabase.CopyAsset(source,target))throw new IOException("Could not copy native material storage shader");}
  public static Shader StorageShader(string folder){string path=folder.Replace('\\','/').TrimEnd('/')+"/NativeMaterialStorage.shader";if(!File.Exists(path))path=SourcePath();var shader=path==null?null:AssetDatabase.LoadAssetAtPath<Shader>(path);if(shader==null)throw new InvalidOperationException("Persistent native-material storage shader is unavailable");return shader;}
  public static void AssignShader(Material material,Shader shader){int queue=material.renderQueue;var keywords=material.shaderKeywords;bool instancing=material.enableInstancing,doubleSided=material.doubleSidedGI;var gi=material.globalIlluminationFlags;material.shader=shader;material.renderQueue=queue;material.shaderKeywords=keywords;material.enableInstancing=instancing;material.doubleSidedGI=doubleSided;material.globalIlluminationFlags=gi;}
  public static void Bind(Material material,Shader shader){
   if(material==null||shader==null)throw new ArgumentException("Native material and shader are required");
   string path=AssetDatabase.GetAssetPath(material);if(string.IsNullOrEmpty(path)||!path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Native material binding requires an exported project material");
   pending.Remove(path);bool dirty=EditorUtility.IsDirty(material);AssignShader(material,shader);if(!dirty)EditorUtility.ClearDirty(material);
   bindings[path]=new Binding{material=material,shader=shader,storage=StorageShader(Path.GetDirectoryName(path))};
  }
  public static void BeforeSave(string[] paths){
   foreach(string path in paths){Binding binding;if(pending.Contains(path)||!bindings.TryGetValue(path,out binding)||binding.material==null||binding.shader==null||binding.shader.name!="Game/SDCS/Hair"||binding.material.shader!=binding.shader)continue;
    binding.beforeSave=EditorJsonUtility.ToJson(binding.material);AssignShader(binding.material,binding.storage);pending.Add(path);
   }
   if(pending.Count>0&&!queued){queued=true;EditorApplication.delayCall+=RestoreAfterSave;}
  }
  public static void RestoreAfterSave(){
   queued=false;foreach(string path in pending){Binding binding;if(!bindings.TryGetValue(path,out binding)||binding.material==null||binding.shader==null)continue;
    // Do not clear unrelated edits made between saving and this delayed callback.
    AssignShader(binding.material,binding.shader);
    if(EditorJsonUtility.ToJson(binding.material)==binding.beforeSave)EditorUtility.ClearDirty(binding.material);
   }pending.Clear();
  }
  public static void SaveRegistered(Material material){
   string path=AssetDatabase.GetAssetPath(material);Binding binding;if(!bindings.TryGetValue(path,out binding)||binding.shader==null||binding.shader.name!="Game/SDCS/Hair")throw new InvalidOperationException("Material is not registered as native hair");
   string state=EditorJsonUtility.ToJson(material);AssignShader(material,binding.storage);EditorUtility.SetDirty(material);bool saved=false;
   try{AssetDatabase.SaveAssetIfDirty(material);saved=true;}finally{AssignShader(material,binding.shader);if(saved&&EditorJsonUtility.ToJson(material)==state)EditorUtility.ClearDirty(material);}
  }
 }
 public sealed class RebirthNativeMaterialSaveHook:AssetModificationProcessor {
  static string[] OnWillSaveAssets(string[] paths){RebirthNativeMaterialPersistence.BeforeSave(paths);return paths;}
 }
}
