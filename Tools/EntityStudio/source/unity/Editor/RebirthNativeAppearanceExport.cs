using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace RebirthEntityStudio {
 public static class RebirthNativeAppearanceExport {
  [Serializable] sealed class Derivation {public string kind="Native-derived local authoring snapshot";public string sourceHash;public FaceNativeAppearanceSettings settings;public NativeAppearanceStyle[] styles;public NativeAppearanceMaterialBinding[] materials;}
  public static string Attach(GameObject root,string folder,Mesh head,FaceNativeAppearanceSettings settings){RebirthNativeAppearanceLibrary.ValidateSource(settings);SaveShaderDependencies(folder);var copied=new Dictionary<UnityEngine.Object,UnityEngine.Object>();var bindings=new List<NativeAppearanceMaterialBinding>();
   if(!string.IsNullOrEmpty(settings.hairKey)){var mesh=RebirthNativeAppearanceLibrary.Hair(head,settings);var materials=RebirthNativeAppearanceLibrary.Materials(settings.hairKey,settings);try{SavePart(root,folder,"NativeHair",mesh,materials,copied,bindings);}finally{foreach(var material in materials)UnityEngine.Object.DestroyImmediate(material);}}
   using(var facial=RebirthNativeAppearanceLibrary.Facial(head,settings)){if(facial.mesh!=null){SavePart(root,folder,"NativeFacialHair",facial.mesh,facial.materials,copied,bindings);facial.mesh=null;}}
   var keys=new[]{settings.hairKey}.Concat(RebirthNativeAppearanceLibrary.FacialKeys(settings)).Where(k=>!string.IsNullOrEmpty(k));if(!string.IsNullOrEmpty(settings.hairKey)&&RebirthNativeAuthoredHairBlend.Active(settings))keys=keys.Concat(RebirthNativeAuthoredHairBlend.Sources(RebirthNativeAppearanceLibrary.Style(settings.hairKey)));var sourceKeys=keys.Distinct().ToArray();var savedSettings=JsonUtility.FromJson<FaceNativeAppearanceSettings>(JsonUtility.ToJson(settings));savedSettings.facialAttachmentGuid=NativeFacialRootAttachment.CopyForExport(settings.facialAttachmentGuid,folder);File.WriteAllText(folder+"/NativeAppearance.json",JsonUtility.ToJson(new Derivation{sourceHash=RebirthNativeAppearanceLibrary.Catalog.sourceHash,settings=savedSettings,styles=sourceKeys.Select(RebirthNativeAppearanceLibrary.Style).ToArray(),materials=bindings.ToArray()},true));AssetDatabase.ImportAsset(folder+"/NativeAppearance.json");return savedSettings.facialAttachmentGuid;
  }
  static void SavePart(GameObject root,string folder,string name,Mesh mesh,Material[] materials,Dictionary<UnityEngine.Object,UnityEngine.Object> copied,List<NativeAppearanceMaterialBinding> bindings){if(materials.Length!=mesh.subMeshCount)throw new InvalidDataException("Exported native material/submesh layout differs.");mesh.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+name+".asset"));var snapshots=new List<Material>();for(int i=0;i<materials.Length;i++){var saved=new Material(materials[i]){name=name+"_"+i,hideFlags=HideFlags.None};foreach(string property in saved.GetTexturePropertyNames()){var texture=saved.GetTexture(property);if(texture==null)continue;UnityEngine.Object snapshot;if(!copied.TryGetValue(texture,out snapshot)){snapshot=SnapshotTexture(texture);snapshot.name=texture.name;snapshot.hideFlags=HideFlags.None;string file=Clean(texture.name)+"_NativeReference.asset";AssetDatabase.CreateAsset(snapshot,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+file));copied[texture]=snapshot;}saved.SetTexture(property,(Texture)snapshot);}RebirthNativeMaterialPersistence.AssignShader(saved,materials[i].shader.name=="Game/SDCS/Hair"?RebirthNativeMaterialPersistence.StorageShader(folder):materials[i].shader);AssetDatabase.CreateAsset(saved,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+name+"_"+i+".mat"));RebirthNativeMaterialPersistence.Bind(saved,materials[i].shader);snapshots.Add(saved);bindings.Add(new NativeAppearanceMaterialBinding{materialGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(saved)),shaderName=materials[i].shader.name});}var child=new GameObject(name);child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterials=snapshots.ToArray();}
  static void SaveShaderDependencies(string folder){RebirthNativeMaterialPersistence.CopyStorageShader(folder);string source=Path.Combine(RebirthNativeAppearanceImport.Configuration().gameRoot,"Data","Addressables","Standalone");string target=Path.Combine(folder,"NativeShaders");Directory.CreateDirectory(target);foreach(string path in Directory.GetFiles(source,"*unitybuiltinshaders.bundle"))CopyDependency(path,Path.Combine(target,Path.GetFileName(path)));CopyDependency(Path.Combine(source,"shaders_assets_all.bundle"),Path.Combine(target,"shaders_assets_all.bundle"));}
  static void CopyDependency(string source,string target){if(File.Exists(target)){using(var sha=System.Security.Cryptography.SHA256.Create()){string a=Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(source)));string b=Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(target)));if(a!=b)throw new IOException("Export contains a different native shader revision: "+target);}return;}File.Copy(source,target);}
  static Texture SnapshotTexture(Texture source){
   var original=source as Texture2D;
   if(original==null)throw new NotSupportedException("Native appearance export requires Texture2D: "+source.name);
   bool srgb=UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(original.graphicsFormat);
   var target=RenderTexture.GetTemporary(original.width,original.height,0,RenderTextureFormat.ARGB32,srgb?RenderTextureReadWrite.sRGB:RenderTextureReadWrite.Linear);
   var previous=RenderTexture.active;bool previousWrite=GL.sRGBWrite;Texture2D result=null;
   try{GL.sRGBWrite=srgb;Graphics.Blit(original,target);RenderTexture.active=target;result=new Texture2D(original.width,original.height,TextureFormat.RGBA32,original.mipmapCount,!srgb);result.ReadPixels(new Rect(0,0,original.width,original.height),0,0,false);result.Apply(original.mipmapCount>1,false);result.wrapModeU=original.wrapModeU;result.wrapModeV=original.wrapModeV;result.wrapModeW=original.wrapModeW;result.mipMapBias=original.mipMapBias;result.filterMode=original.filterMode;result.anisoLevel=original.anisoLevel;return result;}
   catch{if(result!=null)UnityEngine.Object.DestroyImmediate(result);throw;}
   finally{RenderTexture.active=previous;GL.sRGBWrite=previousWrite;RenderTexture.ReleaseTemporary(target);}
  }
  static string Clean(string value){foreach(char c in Path.GetInvalidFileNameChars())value=value.Replace(c,'_');return value;}
 }
}











