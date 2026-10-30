using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Reuses the approved armor's complete material/texture set. Geometry is unchanged.
public static class RebirthCamperMountFinish
{
 const string Folder="Assets/REBIRTHCamperMountFinish_20261019";
 const string Output="E:/_Haven/shared/REBIRTHStaged/CamperTurretRefresh_20261019/MountFinish";
 static readonly HashSet<string> Names=new HashSet<string>{"Circular fixed turret platform","Camper screw attachment plate","Welded horizontal shelf","Open welded diagonal bracket","Steel washer","Hex bolt or retaining nut"};
 public static void Run()
 {
  Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperTurretRigBuild.Folder+"/rebirthCamper80s.prefab");
  var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/REBIRTHCamperApprovedArmor_20261018_v4/plates.mat");
  if(!source||!material)throw new Exception("Approved source/material missing");
  var root=Object.Instantiate(source);root.name="rebirthCamper80s";int index=0;
  var report=new List<string>();
  // The added sphere was not part of the approved source turret and protruded
  // above the barrel. Remove it and its LOD references; retain the source cannon.
  var covers=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Cannon elevation bearing").ToArray();
  foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))
  {var levels=lod.GetLODs();for(int l=0;l<levels.Length;l++)levels[l].renderers=levels[l].renderers.Where(r=>r&&!covers.Contains(r.transform)).ToArray();lod.SetLODs(levels);}
  foreach(var cover in covers)Object.DestroyImmediate(cover.gameObject);
  report.Add("Removed "+covers.Length+" protruding added elevation-cover spheres; source cannon geometry retained.");
  foreach(var f in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>Names.Contains(f.name)))
  {
   var original=f.sharedMesh;if(original.subMeshCount!=1)throw new Exception("Unexpected mount material slots");
   var mesh=new Mesh{name="MountSteelUV"+index};
   var points=original.vertices;var normals=original.normals;var triangles=original.triangles;
   var vertices=new Vector3[triangles.Length];var ns=new Vector3[triangles.Length];var uv=new Vector2[triangles.Length];var indices=new int[triangles.Length];
   var bounds=original.bounds;var scale=f.transform.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
   var physicalSize=Vector3.Scale(bounds.size,scale);float span=Mathf.Max(physicalSize.x,physicalSize.y,physicalSize.z,.00001f);
   for(int tri=0;tri<triangles.Length;tri+=3)
   {
    var n=Vector3.Cross(points[triangles[tri+1]]-points[triangles[tri]],points[triangles[tri+2]]-points[triangles[tri]]).normalized;
    for(int j=0;j<3;j++)
    {
     int at=tri+j,v=triangles[at];var p=Vector3.Scale(points[v]-bounds.min,scale);Vector2 q;
     if(Mathf.Abs(n.x)>=Mathf.Abs(n.y)&&Mathf.Abs(n.x)>=Mathf.Abs(n.z))q=new Vector2(p.z,p.y)/span;
     else if(Mathf.Abs(n.y)>=Mathf.Abs(n.z))q=new Vector2(p.x,p.z)/span;
     else q=new Vector2(p.x,p.y)/span;
     vertices[at]=points[v];ns[at]=normals[v];indices[at]=at;
     // Square island interior and equal physical scale keep the rust grain undistorted.
     uv[at]=new Vector2(Mathf.Lerp(.010f,.080f,q.x),Mathf.Lerp(.015f,.085f,q.y));
    }
   }
   mesh.vertices=vertices;mesh.normals=ns;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateBounds();mesh.RecalculateTangents();
   for(int v=0;v<triangles.Length;v++)if(mesh.vertices[v]!=points[triangles[v]])throw new Exception("Triangle geometry changed");
   string path=Folder+"/Mount"+index+".asset";if(AssetDatabase.LoadAssetAtPath<Mesh>(path))AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(mesh,path);f.sharedMesh=mesh;
   f.GetComponent<Renderer>().sharedMaterial=material;index++;
  }
  if(index<24)throw new Exception("Mount renderer selection incomplete: "+index);
  string prefab=Folder+"/rebirthCamper80s.prefab";PrefabUtility.SaveAsPrefabAsset(root,prefab);AssetDatabase.SaveAssets();
  report.Add("PASS "+index+" mount renderers share approved armor plates material; all triangle corner positions unchanged; physically proportioned steel UVs.");
  RebirthCamperCampIntegration.SetMode(root,true);
  foreach(var t in root.GetComponentsInChildren<Transform>(true))
  {
   if(t.name.StartsWith("REBIRTHCamperTurretSlot")||new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow"}.Contains(t.name))t.gameObject.SetActive(true);
   if(new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"}.Contains(t.name))t.gameObject.SetActive(false);
  }
  foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.6f,.6f);
  var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(35,-30,0);
  Render(new Vector3(-3.4f,3.8f,-1.7f),new Vector3(-1.03f,2.90f,-3.0f),.76f,"mount-closeup");
  foreach(var pitch in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="TurretPitch"))pitch.localRotation=Quaternion.Euler(-75,0,0);
  Render(new Vector3(-3.4f,3.8f,-1.7f),new Vector3(-1.03f,2.90f,-3.0f),.76f,"cannon-up75");
  foreach(var pitch in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="TurretPitch"))pitch.localRotation=Quaternion.identity;
  Render(new Vector3(-6,5.4f,-8),new Vector3(0,1.9f,-1.2f),3.4f,"vehicle-rear-quarter");
  Object.DestroyImmediate(root);
  Directory.CreateDirectory(Output+"/Bundle");
  var manifest=RebirthAssetBundlePackaging.BuildAssetBundles(Output+"/Bundle",new[]{new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{prefab}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
  if(!manifest)throw new Exception("Bundle build failed");
  var bundle=AssetBundle.LoadFromFile(Output+"/Bundle/rebirth_camper80s.unity3d");if(!bundle)throw new Exception("Bundle reopen failed");
  try
  {
   var actual=bundle.LoadAsset<GameObject>("rebirthCamper80s");
   if(!actual||actual.GetComponentsInChildren<WheelCollider>(true).Length!=4)throw new Exception("Vehicle contract changed");
   var mounts=actual.GetComponentsInChildren<Renderer>(true).Where(r=>Names.Contains(r.name)).ToArray();
   if(mounts.Length!=index||mounts.Any(r=>r.sharedMaterial.name!="plates"))throw new Exception("Bundled mount materials differ");
   for(int c=0;c<4;c++)if(actual.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHCamperTurretSlot"+c).gameObject.activeSelf)throw new Exception("Uninstalled turret visible");
   report.Add("PASS reopened bundle: all mount materials plates; four hidden corner roots; four wheel colliders.");
  }
  finally{bundle.Unload(true);}
  File.WriteAllLines(Output+"/verification.txt",report);
 }
 static void Render(Vector3 eye,Vector3 target,float size,string name)
 {
  var c=new GameObject(name).AddComponent<Camera>();c.orthographic=true;c.orthographicSize=size;c.nearClipPlane=.01f;c.transform.position=eye;c.transform.LookAt(target);c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.07f,.07f,.09f);
  var rt=new RenderTexture(1400,1400,24){antiAliasing=4};c.targetTexture=rt;c.Render();RenderTexture.active=rt;var img=new Texture2D(1400,1400,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1400,1400),0,0);img.Apply();File.WriteAllBytes(Output+"/"+name+".png",img.EncodeToPNG());RenderTexture.active=null;c.targetTexture=null;Object.DestroyImmediate(img);Object.DestroyImmediate(rt);Object.DestroyImmediate(c.gameObject);
 }
}
