using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Object=UnityEngine.Object;
public static class RebirthNativeTurretSurvey
{
 public const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/NativeTurretPreview_20261018";
 public const string Bundle="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Addressables/Standalone/automatic_assets_entities/traps.bundle";
 public static void Run()
 {
  Directory.CreateDirectory(Output);var bundle=AssetBundle.LoadFromFile(Bundle);if(!bundle)throw new Exception("Native traps bundle load failed");var rows=new List<string>();
  try{File.WriteAllLines(Output+"/all-assets.txt",bundle.GetAllAssetNames());foreach(var name in bundle.GetAllAssetNames().Where(n=>n.ToLowerInvariant().Contains("autoturret")))rows.Add("ASSET "+name);
   foreach(string kind in new[]{"SMG","Shotgun","M60"}){var path=bundle.GetAllAssetNames().Single(n=>n.ToLowerInvariant().EndsWith("autoturret"+kind.ToLowerInvariant()+"prefab.prefab"));var prefab=bundle.LoadAsset<GameObject>(path);var root=Object.Instantiate(prefab);var rs=root.GetComponentsInChildren<Renderer>(true);rows.Add("PREFAB "+kind+" scale="+root.transform.localScale);foreach(var r in rs){rows.Add("RENDERER "+r.name+" bounds="+r.bounds.center.ToString("F4")+" size="+r.bounds.size.ToString("F4"));foreach(var m in r.sharedMaterials)if(m){rows.Add("MATERIAL "+m.name+" shader="+(m.shader?m.shader.name:"missing"));foreach(var prop in m.GetTexturePropertyNames()){var t=m.GetTexture(prop);rows.Add("TEXTURE "+prop+"="+(t?t.name:"NULL"));}}}Object.DestroyImmediate(root);}
   File.WriteAllLines(Output+"/native-turret-survey.txt",rows);
  }finally{bundle.Unload(true);}
 }
}

