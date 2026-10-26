using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace RebirthEntityStudio {
// Native AssetBundle objects survive editor domain reloads. Their internal name
// need not match the source filename; recover their shaders before loading again.
public static class RebirthNativeShaderLibrary {
 static readonly Dictionary<string,Shader> cache=new Dictionary<string,Shader>();
 const string DirectoryPath="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Addressables/Standalone";
 static void Recover(){
  foreach(var bundle in AssetBundle.GetAllLoadedAssetBundles().ToArray())
   foreach(var shader in bundle.LoadAllAssets<Shader>())
    if(shader!=null&&(shader.name=="Game/Character"||shader.name=="Game/Autodesk"||shader.name=="Game/SDCS/Hair"))cache[shader.name]=shader;
 }
 public static Shader Get(string name){return Get(name,DirectoryPath);}
 public static Shader Get(string name,string directoryPath){
  if(cache.TryGetValue(name,out var shader)&&shader!=null)return shader;
  Recover();if(cache.TryGetValue(name,out shader)&&shader!=null)return shader;
  // Dependencies are only loaded for a fresh shader bundle. Reuse any already
  // loaded dependency with the same actual serialized-file identity.
  foreach(string path in Directory.GetFiles(directoryPath,"*unitybuiltinshaders.bundle")){
   string identity=Path.GetFileNameWithoutExtension(path);
   if(!AssetBundle.GetAllLoadedAssetBundles().Any(b=>b.name.IndexOf(identity,StringComparison.OrdinalIgnoreCase)>=0))AssetBundle.LoadFromFile(path);
  }
  var source=AssetBundle.LoadFromFile(directoryPath+"/shaders_assets_all.bundle");
  Recover();
  if(cache.TryGetValue(name,out shader)&&shader!=null)return shader;
  throw new InvalidOperationException("Native preview shader missing: "+name+"; source bundle "+(source==null?"not loadable":"loaded"));
 }
 public static void ClearCacheForReloadAudit(){cache.Clear();}
}
}


