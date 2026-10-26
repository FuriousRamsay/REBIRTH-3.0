using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class RebirthCamperWrapBundle
{
 [Serializable] public class Receipt {
  public string status="FAIL",sourcePrefab,prefab,bundle,sha256,error;
  public uint crc; public string[] assetNames;
  public string defaultPattern="none",cosmeticModifier="modVehiclePatternDisco";
  public string colorSpace;
  public string scope="Windows64 reference-wrap test candidate; no game launch or installation.";
 }
 public static void RunBatch() {
  string output=Arg("-rebirthWrapBundleReport"); Directory.CreateDirectory(output);
  var r=new Receipt{sourcePrefab=Arg("-rebirthWrapBundleSource")}; GameObject root=null; AssetBundle loaded=null;
  try {
   if(!Application.isBatchMode)throw new Exception("Dedicated batch editor required.");
   r.colorSpace=QualitySettings.activeColorSpace.ToString();
   if(QualitySettings.activeColorSpace!=ColorSpace.Linear)throw new Exception("Native game wrap must be built and reviewed in Linear color space.");
   var source=AssetDatabase.LoadAssetAtPath<GameObject>(r.sourcePrefab);
   if(!source)throw new Exception("Verified lighting source prefab missing.");
   root=UnityEngine.Object.Instantiate(source);root.name="rebirthCamper80s";
   int wheels=root.GetComponentsInChildren<WheelCollider>(true).Length;
   int lights=root.GetComponentsInChildren<Light>(true).Length;
   if(wheels!=4 || !root.transform.Find("Physics") || !root.transform.Find("GameObject/M/BodyMarkerLights"))throw new Exception("Native physics/model/lighting source contract missing.");
   bool preserveFinish=Environment.GetCommandLineArgs().Contains("-rebirthPreserveWrapFinish");
   string materialPrefab;
   if(preserveFinish) {
    string preservedFolder="Assets/REBIRTHCamperWrapGenerated/BundleCandidates/"+Guid.NewGuid().ToString("N");
    Directory.CreateDirectory(preservedFolder);AssetDatabase.Refresh();
    materialPrefab=preservedFolder+"/ReferencePurpleCamper.prefab";
    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true).Where(x=>x.name=="body"||x.name=="camper_body"))
     if(renderer.sharedMaterial.shader.name!="REBIRTH/CamperReferenceWrap" || renderer.sharedMaterial.GetFloat("_PatternEnabled")!=0)
      throw new Exception("Reviewed source must have native cosmetic shader and default None.");
    PrefabUtility.SaveAsPrefabAsset(root,materialPrefab);
   } else materialPrefab=RebirthCamperReferenceWrap.Apply(root,false);
   string folder=Path.GetDirectoryName(materialPrefab).Replace('\\','/');
   r.prefab=folder+"/rebirthCamper80s.prefab";
   PrefabUtility.SaveAsPrefabAsset(root,r.prefab);AssetDatabase.SaveAssets();
   var reopened=AssetDatabase.LoadAssetAtPath<GameObject>(r.prefab);
   if(reopened.GetComponentsInChildren<WheelCollider>(true).Length!=wheels || reopened.GetComponentsInChildren<Light>(true).Length!=lights)throw new Exception("Wrap changed physics/light counts.");
   foreach(var mesh in reopened.GetComponentsInChildren<MeshRenderer>(true).Where(x=>x.name=="body"||x.name=="camper_body"))
    if(mesh.sharedMaterial.shader.name!="REBIRTH/CamperReferenceWrap" || !mesh.sharedMaterial.GetTexture("_Artwork") || mesh.sharedMaterial.GetFloat("_PatternEnabled")!=0)throw new Exception("Native cosmetic wrap material/default None did not persist.");
   var build=new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{r.prefab}};
   string archive=Path.Combine(output,"UncompressedSource");Directory.CreateDirectory(archive);
   if(BuildPipeline.BuildAssetBundles(archive,new[]{build},BuildAssetBundleOptions.UncompressedAssetBundle,BuildTarget.StandaloneWindows64)==null)throw new Exception("Wrap bundle build failed.");
   string original=Path.Combine(archive,build.assetBundleName);
   if(!BuildPipeline.GetCRCForAssetBundle(original,out r.crc)||r.crc==0)throw new Exception("Native CRC unavailable.");
   r.bundle=Path.Combine(output,build.assetBundleName);
   var operation=AssetBundle.RecompressAssetBundleAsync(original,r.bundle,BuildCompression.LZ4Runtime,r.crc,UnityEngine.ThreadPriority.Normal);
   var deadline=DateTime.UtcNow.AddMinutes(12);
   while(!operation.isDone){if(DateTime.UtcNow>deadline)throw new TimeoutException("Wrap recompression timed out.");System.Threading.Thread.Sleep(20);}
   if(operation.result!=UnityEngine.AssetBundleLoadResult.Success)throw new Exception("Recompression failed: "+operation.humanReadableResult);
   loaded=AssetBundle.LoadFromFile(r.bundle,r.crc);if(!loaded)throw new Exception("Native bundle reopen failed.");
   r.assetNames=loaded.GetAllAssetNames();var bundled=loaded.LoadAsset<GameObject>("rebirthCamper80s");
   if(!bundled || bundled.GetComponentsInChildren<WheelCollider>(true).Length!=4 || bundled.GetComponentsInChildren<Light>(true).Length!=lights)throw new Exception("Bundled native prefab contract failed.");
   if(preserveFinish) foreach(string meshName in new[]{"body","camper_body"}) {
    var expected=root.GetComponentsInChildren<MeshRenderer>(true).Single(x=>x.name==meshName).sharedMaterial;
    var actual=bundled.GetComponentsInChildren<MeshRenderer>(true).Single(x=>x.name==meshName).sharedMaterial;
    foreach(string key in new[]{"_DiscoMetallic","_DiscoSmoothness","_PatternEnabled","_UseBandArtwork","_LowerArtworkOffsetY"})
     if(!actual.HasProperty(key)||Mathf.Abs(actual.GetFloat(key)-expected.GetFloat(key))>.0001f)throw new Exception("Bundled finish mismatch: "+meshName+" "+key);
    if(actual.GetColor("_DiscoCoat")!=expected.GetColor("_DiscoCoat"))throw new Exception("Bundled coat mismatch.");
    if(actual.GetVector("_BandVRange")!=expected.GetVector("_BandVRange"))throw new Exception("Bundled band mapping mismatch.");
    foreach(string textureKey in new[]{"_Artwork","_BandArtwork"}) {
     var sourceTexture=expected.GetTexture(textureKey); var bundleTexture=actual.GetTexture(textureKey);
     if(sourceTexture && (!bundleTexture || sourceTexture.name!=bundleTexture.name || sourceTexture.width!=bundleTexture.width || sourceTexture.height!=bundleTexture.height))throw new Exception("Bundled artwork missing or changed: "+textureKey);
    }
   }
   using(var stream=File.OpenRead(r.bundle))using(var hash=SHA256.Create())r.sha256=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
   r.status="PASS_WRAP_GAME_ASSET_NOT_GAME_TESTED";
  }catch(Exception e){r.error=e.ToString();}
  finally{if(loaded)loaded.Unload(true);if(root)UnityEngine.Object.DestroyImmediate(root);File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(r,true));}
  if(r.status.StartsWith("FAIL"))throw new Exception(r.error);
 }
 static string Arg(string key){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);if(i<0||i+1>=args.Length)throw new Exception("Required "+key);return args[i+1];}
}
