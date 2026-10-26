using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class RebirthCamperTurretBundle
{
 public static void Export()
 {
  string prefab=RebirthCamperTurretRigBuild.Folder+"/rebirthCamper80s.prefab";
  Check(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
  string path=RebirthCamperTurretRigBuild.Output+"/REBIRTH_CamperTurrets_20261019.unitypackage";
  AssetDatabase.ExportPackage(prefab,path,ExportPackageOptions.IncludeDependencies|ExportPackageOptions.Recurse);
  if(!File.Exists(path)||new FileInfo(path).Length==0)throw new Exception("Unity package export missing");
  Debug.Log("PASS turret Unity package exported: "+path);
 }
 public static void Run()
 {
  string prefab=RebirthCamperTurretRigBuild.Folder+"/rebirthCamper80s.prefab",output=RebirthCamperTurretRigBuild.Output+"/Bundle";Directory.CreateDirectory(output);
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);Check(source);
  var report=new List<string>{"Target: Windows64. Source: roof-fitted ladder/current open bracket candidate. Installed baseline SHA2562AD18875AEA515B723C1A26A0F98F4F37E3269842EB45C64673402451A807BDE (unchanged by build)."};
  foreach(string path in AssetDatabase.GetDependencies(prefab,true).Where(p=>p.Contains("REBIRTHTripoTurret_20261018")))
  {
   var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(!texture)continue;
   var importer=AssetImporter.GetAtPath(path) as TextureImporter;int cap=path.Contains("Albedo")?2048:path.Contains("Normal")?1024:256;
   if(texture.width>cap||texture.height>cap||importer==null||!importer.crunchedCompression)throw new Exception("Turret texture budget/Crunch mismatch: "+path);
   report.Add("PASS texture "+path+" "+texture.width+"x"+texture.height+" "+texture.format);
  }
  var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{prefab}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
  if(!manifest)throw new Exception("Turret camper bundle build failed");
  var bundle=AssetBundle.LoadFromFile(output+"/rebirth_camper80s.unity3d");if(!bundle)throw new Exception("Bundle reopen failed");
  try
  {
   var actual=bundle.LoadAsset<GameObject>("rebirthCamper80s");Check(actual);
   var sr=source.GetComponentsInChildren<Renderer>(true);var ar=actual.GetComponentsInChildren<Renderer>(true);if(sr.Length!=ar.Length)throw new Exception("Renderer count changed during bundling");
   for(int i=0;i<sr.Length;i++)
   {
    var sm=sr[i].sharedMaterials;var am=ar[i].sharedMaterials;if(sm.Length!=am.Length)throw new Exception("Material slots changed");
    for(int j=0;j<sm.Length;j++)if(sm[j])foreach(var property in sm[j].GetTexturePropertyNames())
    {var a=sm[j].GetTexture(property) as Texture2D;if(!a)continue;var b=am[j].GetTexture(property) as Texture2D;if(!b||a.width!=b.width||a.height!=b.height||a.format!=b.format)throw new Exception("Bundled texture differs: "+sr[i].name);}
   }
   report.Add("PASS bundle reopen: native vehicle hierarchy/physics/wheels; four default-hidden slot roots; fixed/yaw/pitch/muzzle nodes; renderer materials and texture dimensions/formats match source.");
  }
  finally{bundle.Unload(true);}
  File.WriteAllLines(RebirthCamperTurretRigBuild.Output+"/bundle-verification.txt",report);
 }
 static void Check(GameObject root)
 {
  if(!root||!root.transform.Find("GameObject/M/VisualBody")||!root.transform.Find("Physics")||root.GetComponentsInChildren<WheelCollider>(true).Length!=4)throw new Exception("Native camper contract missing");
  if(root.GetComponentsInChildren<MonoBehaviour>(true).Any(m=>m))throw new Exception("Game scripts must not be embedded in bundle");
  for(int i=0;i<4;i++)
  {
   var slot=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHCamperTurretSlot"+i);if(slot.gameObject.activeSelf)throw new Exception("Uninstalled turret visible by default");
   foreach(string name in new[]{"TurretYaw","TurretPitch","TurretMuzzle"})if(slot.GetComponentsInChildren<Transform>(true).Count(t=>t.name==name)!=1)throw new Exception("Articulation marker missing");
  }
 }
}
