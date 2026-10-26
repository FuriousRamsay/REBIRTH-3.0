using System;using System.IO;using System.Linq;using System.Security.Cryptography;using UnityEditor;using UnityEngine;
namespace RebirthEntityStudio {
 public static class RebirthNativeMaterialRepair {
  [Serializable]sealed class Metadata {public NativeAppearanceMaterialBinding[] materials;}
  [Serializable]sealed class Record {public string path,sha256;}
  [Serializable]sealed class Receipt {public string folder,backup;public Record[] originals;public int repaired;}
  static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
  [MenuItem("Tools/REBIRTH/Repair selected native appearance export")]
  static void RepairSelected(){try{string path=AssetDatabase.GetAssetPath(Selection.activeObject);string folder=path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase)?Path.GetDirectoryName(path).Replace('\\','/')+"/"+Path.GetFileNameWithoutExtension(path)+"_Assets":Path.GetDirectoryName(path)?.Replace('\\','/');Debug.Log(RepairFolder(folder));}catch(Exception e){Debug.LogError("Native appearance repair stopped: "+e.Message);}}
  public static string RepairFolder(string folder){
   if(string.IsNullOrEmpty(folder)||!folder.StartsWith("Assets/",StringComparison.Ordinal)||!File.Exists(folder+"/NativeAppearance.json"))throw new ArgumentException("Select an exported head prefab or its native material/metadata asset");
   var data=JsonUtility.FromJson<Metadata>(File.ReadAllText(folder+"/NativeAppearance.json"));if(data?.materials==null)throw new InvalidDataException("This older export has no explicit material roles; re-export it from its saved settings");
   var paths=data.materials.Where(m=>m.shaderName=="Game/SDCS/Hair").Select(m=>AssetDatabase.GUIDToAssetPath(m.materialGuid)).Distinct().ToArray();
   foreach(string path in paths)if(string.IsNullOrEmpty(path)||!path.StartsWith(folder.TrimEnd('/')+"/",StringComparison.Ordinal)||!path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase)||!File.Exists(path))throw new InvalidDataException("Native material reference leaves the selected export or is missing");
   if(paths.Length==0)return "Selected export has no native hair materials to repair";
   string backup=Path.GetFullPath("EntityStudioBackups/NativeMaterial_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffff")+"_"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(backup);
   var receipt=new Receipt{folder=folder,backup=backup,originals=paths.Select(p=>new Record{path=p,sha256=Hash(p)}).ToArray()};
   foreach(string path in paths){string target=Path.Combine(backup,path);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(path,target);if(File.Exists(path+".meta"))File.Copy(path+".meta",target+".meta");}
   string report=Path.Combine(backup,"Receipt.json");File.WriteAllText(report,JsonUtility.ToJson(receipt,true));
   RebirthNativeMaterialPersistence.CopyStorageShader(folder);RebirthNativeAppearanceDependencies.Restore(folder);
   foreach(string path in paths){var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null)throw new FileNotFoundException("Native material could not be loaded: "+path);RebirthNativeMaterialPersistence.SaveRegistered(material);receipt.repaired++;File.WriteAllText(report,JsonUtility.ToJson(receipt,true));}
   return "Repaired "+receipt.repaired+" native material shader references. Original materials and metadata backed up: "+backup;
  }
 }
}
