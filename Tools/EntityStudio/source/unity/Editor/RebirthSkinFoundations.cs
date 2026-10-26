using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio {
 [Serializable] public sealed class SkinFoundationProfile { public string key,name,file,sha256; }
 [Serializable] sealed class SkinFoundationCatalog { public int version; public SkinFoundationProfile[] profiles; }
 // Colour atlases are selected independently of anatomy. No demographic
 // inference, geometry changes or synthetic normal maps are performed here.
 public static class RebirthSkinFoundations {
  static string CatalogPath(){
   string path=AssetDatabase.FindAssets("SkinFoundationCatalog_v1").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/FaceAppearanceData/Skins/SkinFoundationCatalog_v1.json",StringComparison.Ordinal));
   if(path==null)throw new FileNotFoundException("The authored skin foundation library is missing");return path;
  }
  public static SkinFoundationProfile[] Profiles {
   get {var catalog=JsonUtility.FromJson<SkinFoundationCatalog>(File.ReadAllText(CatalogPath()));if(catalog==null||catalog.version!=1||catalog.profiles==null)throw new InvalidDataException("Unsupported skin foundation catalog");return catalog.profiles;}
  }
  public static void Select(FaceSkinSettings settings,string key){
   if(string.IsNullOrEmpty(key)){settings.foundationKey="";settings.foundationHash="";return;}
   var profile=Profiles.Single(p=>p.key==key);
   // Resolve and validate before modifying the caller's recipe.
   Load(profile.key,profile.sha256);settings.foundationKey=profile.key;settings.foundationHash=profile.sha256;
  }
  public static Texture2D Load(string key,string expectedHash){
   var profile=Profiles.Single(p=>p.key==key);
   if(profile.sha256!=expectedHash||string.IsNullOrEmpty(profile.file)||Path.GetFileName(profile.file)!=profile.file)throw new InvalidDataException("Skin foundation identity does not match the saved recipe");
   string path=Path.GetDirectoryName(CatalogPath()).Replace('\\','/')+"/"+profile.file;
   using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=expectedHash)throw new InvalidDataException("Skin foundation texture hash mismatch");
   var importer=AssetImporter.GetAtPath(path) as TextureImporter;
   if(importer==null||importer.textureType!=TextureImporterType.Default||!importer.sRGBTexture)throw new InvalidDataException("Skin foundation must be imported as a Default sRGB colour texture");
   var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(texture==null)throw new FileNotFoundException("Skin foundation texture is unavailable",path);return texture;
  }
 }
}
