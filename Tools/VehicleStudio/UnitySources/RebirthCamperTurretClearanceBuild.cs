using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class RebirthCamperTurretClearanceBuild
{
 public const string Folder="Assets/REBIRTHCamperTurretClearance_20261019";
 public static void Run()
 {
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperTurrets_20261019/rebirthCamper80s.prefab"));root.name="rebirthCamper80s";
  for(int i=2;i<4;i++)
  {
   int side=i%2==0?-1:1;
   var slot=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHCamperTurretSlot"+i);
   foreach(var child in slot.Cast<Transform>().ToArray())
   {
    if(child.name.StartsWith("Optimized Tripo turret")||child.name=="Circular fixed turret platform")child.position+=Vector3.right*(side*.35f);
    else if(child.name=="Welded horizontal shelf"||child.name=="Open welded diagonal bracket")
    {
     var mf=child.GetComponent<MeshFilter>();var mesh=UnityEngine.Object.Instantiate(mf.sharedMesh);var vertices=mesh.vertices;
     // Retain the wall weld and stretch only the outward span to the platform.
     // The exact same map is used on the shelf and brace so their ends stay welded.
     for(int v=0;v<vertices.Length;v++)
     {
      var p=child.TransformPoint(vertices[v]);float distance=Mathf.Abs(p.x)-.930f;
      p.x+=side*.35f*Mathf.Clamp01(distance/.22f);vertices[v]=child.InverseTransformPoint(p);
     }
     mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
     string path=Folder+"/"+i+"_"+child.name.Replace(" ","_")+"_"+child.GetSiblingIndex()+".asset";
     AssetDatabase.CreateAsset(mesh,path);mf.sharedMesh=mesh;
    }
   }
  }
  PrefabUtility.SaveAsPrefabAsset(root,Folder+"/rebirthCamper80s.prefab");AssetDatabase.SaveAssets();
  Debug.Log("PASS rear outrigger candidate: platforms/turrets shift35cm outward, wall attachments and heights unchanged.");
 }
 public static void Review()
 {
  RebirthCamperTurretClearanceAudit.Run();
  var root=UnityEngine.Object.FindObjectsOfType<Transform>().First(t=>t.name.StartsWith("rebirthCamper80s")&&t.parent==null).gameObject;
  foreach(var t in root.GetComponentsInChildren<Transform>(true))
  {
   if(t.name.StartsWith("REBIRTHCamperTurretSlot"))t.gameObject.SetActive(true);
   if(new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"}.Contains(t.name))t.gameObject.SetActive(false);
  }
  foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
  var clay=new Material(Shader.Find("Standard"));clay.color=new Color(.50f,.51f,.52f);clay.SetFloat("_Glossiness",.22f);
  foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=Enumerable.Repeat(clay,r.sharedMaterials.Length).ToArray();
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.30f,.30f,.30f);
  var light=new GameObject("Review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.0f;light.transform.rotation=Quaternion.Euler(45,-35,0);light.shadows=LightShadows.Soft;
  var camera=new GameObject("Review camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.085f,.095f);camera.orthographic=true;camera.orthographicSize=1.05f;camera.nearClipPlane=.01f;camera.farClipPlane=40;
  var target=new Vector3(.9f,2.65f,-2.9f);var offsets=new[]{new Vector3(3,2,-3),new Vector3(0,2,-4),new Vector3(4,1,0)};
  for(int i=0;i<offsets.Length;i++)
  {
   camera.transform.position=target+offsets[i];camera.transform.LookAt(target);
   var rt=new RenderTexture(1400,1000,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
   var image=new Texture2D(1400,1000,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();
   File.WriteAllBytes("E:/_Haven/shared/REBIRTHStaged/CamperTurretRefresh_20261019/Artifacts/rear_clearance_clay_"+i+".png",image.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
  }
 }
 public static void Bundle()
 {
  string prefab=Folder+"/rebirthCamper80s.prefab",output="E:/_Haven/shared/REBIRTHStaged/CamperTurretRefresh_20261019/Bundle";Directory.CreateDirectory(output);
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
  var baseline=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperTurrets_20261019/rebirthCamper80s.prefab");
  if(source.GetComponentsInChildren<Collider>(true).Length!=baseline.GetComponentsInChildren<Collider>(true).Length||source.GetComponentsInChildren<Renderer>(true).Length!=baseline.GetComponentsInChildren<Renderer>(true).Length)throw new Exception("Component counts changed");
  for(int i=0;i<4;i++)
  {
   var slot=source.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHCamperTurretSlot"+i);
   if(slot.gameObject.activeSelf||!slot.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="TurretMuzzle"))throw new Exception("Turret installation contract changed");
  }
  var manifest=RebirthAssetBundlePackaging.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{prefab}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
  if(!manifest)throw new Exception("Bundle failed");
  var bundle=AssetBundle.LoadFromFile(output+"/rebirth_camper80s.unity3d");if(!bundle||!bundle.LoadAsset<GameObject>("rebirthCamper80s"))throw new Exception("Bundle reopen failed");bundle.Unload(true);
  AssetDatabase.ExportPackage(prefab,output+"/CamperTurretClearance.unitypackage",ExportPackageOptions.IncludeDependencies|ExportPackageOptions.Recurse);
  Debug.Log("PASS candidate bundle and source package exported; component and hidden-slot contracts preserved.");
 }
}
