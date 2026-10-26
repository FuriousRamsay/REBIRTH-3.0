using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class RebirthCamperApprovedArmor
{
 public const string Folder="Assets/REBIRTHCamperApprovedArmor_20261018_v4";
 public const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperApprovedArmor40Entry_20261018";
 const string Input="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017/Approved40Export_20261018";
 const string Source="Assets/REBIRTHCamperArmorPlow_20261017_v4/rebirthCamper80s.prefab";
 static GameObject Group(Transform parent,string name)
 {var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
 static Texture2D Import(string group,string channel,bool normal=false)
 {
  string path=Folder+"/"+group+"_"+channel+".png";File.Copy(Input+"/"+group+"_"+channel+".png",path,true);AssetDatabase.ImportAsset(path);
  var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;imp.sRGBTexture=channel=="albedo";imp.maxTextureSize=(channel=="albedo"||normal)?2048:256;
  imp.textureCompression=TextureImporterCompression.Compressed;imp.crunchedCompression=true;imp.compressionQuality=90;imp.mipmapEnabled=true;imp.isReadable=false;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
 }
 static Material Material(string group)
 {
  var mat=new Material(Shader.Find("Standard")){name="Reviewed "+group};
  mat.SetTexture("_MainTex",Import(group,"albedo"));mat.SetTexture("_BumpMap",Import(group,"normal",true));mat.EnableKeyword("_NORMALMAP");
  mat.SetTexture("_OcclusionMap",Import(group,"ao"));mat.SetFloat("_OcclusionStrength",1);
  // Channel packing is technical texture processing: red metallic, alpha smoothness.
  var metal=new Texture2D(2,2,TextureFormat.RGBA32,false,true);metal.LoadImage(File.ReadAllBytes(Input+"/"+group+"_metallic.png"));
  var rough=new Texture2D(2,2,TextureFormat.RGBA32,false,true);rough.LoadImage(File.ReadAllBytes(Input+"/"+group+"_roughness.png"));
  if(metal.width!=256||rough.width!=256)throw new Exception("Mask size mismatch");
  var pixels=metal.GetPixels32();var roughPixels=rough.GetPixels32();for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(pixels[i].r,0,0,(byte)(255-roughPixels[i].r));
  metal.SetPixels32(pixels);metal.Apply();string mask=Folder+"/"+group+"_metallic_smoothness.png";File.WriteAllBytes(mask,metal.EncodeToPNG());Object.DestroyImmediate(metal);Object.DestroyImmediate(rough);AssetDatabase.ImportAsset(mask);
  var importer=(TextureImporter)AssetImporter.GetAtPath(mask);importer.sRGBTexture=false;importer.maxTextureSize=256;importer.crunchedCompression=true;importer.compressionQuality=90;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
  mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(mask));mat.EnableKeyword("_METALLICGLOSSMAP");mat.SetFloat("_GlossMapScale",1);
  AssetDatabase.CreateAsset(mat,Folder+"/"+group+".mat");return mat;
 }
 static void Mesh(string label,Transform parent)
 {
  var go=Group(parent,"Approved "+label);var vertices=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();var indices=new List<int>();
  using(var br=new BinaryReader(File.OpenRead(Input+"/"+label+".meshbin")))
  {
   int count=br.ReadInt32(),tri=br.ReadInt32();var remap=new int[count];var unique=new Dictionary<string,int>();
   for(int i=0;i<count;i++)
   {
    Vector3 p=new Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle()),n=new Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle());var uv=new Vector2(br.ReadSingle(),br.ReadSingle());
    // Weld exact position/normal/UV matches only; do not simplify approved contours.
    string key=p.x.ToString("R")+","+p.y.ToString("R")+","+p.z.ToString("R")+","+n.x.ToString("R")+","+n.y.ToString("R")+","+n.z.ToString("R")+","+uv.x.ToString("R")+","+uv.y.ToString("R");
    if(!unique.TryGetValue(key,out int index)){index=vertices.Count;unique.Add(key,index);vertices.Add(go.transform.InverseTransformPoint(p));normals.Add(go.transform.InverseTransformDirection(n).normalized);uvs.Add(uv);}remap[i]=index;
   }
   for(int i=0;i<tri;i++)indices.Add(remap[br.ReadInt32()]);if(br.BaseStream.Position!=br.BaseStream.Length)throw new Exception("Unexpected mesh payload");
  }
  var mesh=new Mesh{name="Approved "+label,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();mesh.RecalculateTangents();mesh.OptimizeIndexBuffers();mesh.OptimizeReorderVertexBuffer();AssetDatabase.CreateAsset(mesh,Folder+"/"+label+".asset");
  go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Material(label);
 }
 public static void Author()
 {
  if(File.Exists(Folder+"/rebirthCamper80s.prefab"))throw new Exception("Preserve authored prefab; call Build for rebuild");
  Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
  var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));root.name="rebirthCamper80s";
  var m=root.transform.Find("GameObject/M");var pivot=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHRearDoorPivot");
  foreach(var t in new[]{m.Find("REBIRTHModArmor"),m.Find("REBIRTHModPlow"),pivot.Find("REBIRTHModArmorDoor")})if(t)Object.DestroyImmediate(t.gameObject);
  var armor=Group(m,"REBIRTHModArmor");var door=Group(pivot,"REBIRTHModArmorDoor");var plow=Group(m,"REBIRTHModPlow");
  Mesh("plates",armor.transform);Mesh("grilles",armor.transform);Mesh("door",door.transform);Mesh("plough",plow.transform);
  RebirthCamperEntryWidth.Apply(root,Folder);
  RebirthCamperPloughCollision.Apply(root,Folder+"/Collision",Input+"/collision-shapes.json");
  armor.SetActive(false);door.SetActive(false);plow.SetActive(false);
  PrefabUtility.SaveAsPrefabAsset(root,Folder+"/rebirthCamper80s.prefab");AssetDatabase.SaveAssets();Object.DestroyImmediate(root);Build();
 }
 public static void Build()
 {
  Directory.CreateDirectory(Output+"/Bundles");
  var build=new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{Folder+"/rebirthCamper80s.prefab"},addressableNames=new[]{"rebirthCamper80s"}};
  if(!BuildPipeline.BuildAssetBundles(Output+"/Bundles",new[]{build},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("Bundle build failed");
  Verify();
 }
 public static void Verify()
 {
  string bundlePath=Output+"/Bundles/rebirth_camper80s.unity3d";if(!BuildPipeline.GetCRCForAssetBundle(bundlePath,out uint crc))throw new Exception("No CRC");var bundle=AssetBundle.LoadFromFile(bundlePath,crc);if(!bundle)throw new Exception("CRC reopen failed");
  try
  {
   var prefab=bundle.LoadAsset<GameObject>("rebirthCamper80s");var root=Object.Instantiate(prefab);root.name=prefab.name;var transforms=root.GetComponentsInChildren<Transform>(true);
   foreach(string name in new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow","REBIRTHModPlowHull"})if(transforms.Single(t=>t.name==name).gameObject.activeSelf)throw new Exception("Default visible: "+name);
   var colliders=transforms.Single(t=>t.name=="REBIRTHModPlowHull").GetComponentsInChildren<MeshCollider>(true);if(colliders.Length!=27||colliders.Any(c=>!c.convex||c.isTrigger||c.gameObject.layer!=21||!c.CompareTag("E_VehicleStrong")))throw new Exception("Impact contract");
   if(root.GetComponentsInChildren<WheelCollider>(true).Length!=4||transforms.Count(t=>t.name.StartsWith("REBIRTH_Seat"))!=30)throw new Exception("Rig changed");
   foreach(var texture in bundle.LoadAllAssets<Texture2D>().Where(t=>new[]{"plates_","grilles_","door_","plough_"}.Any(p=>t.name.StartsWith(p))))
   {int limit=texture.name.EndsWith("albedo")||texture.name.EndsWith("normal")?2048:256;if(texture.width>limit||texture.height>limit||!texture.format.ToString().Contains("Crunched"))throw new Exception("Texture contract: "+texture.name+" "+texture.format);}
   var original=AssetDatabase.LoadAssetAtPath<GameObject>(Source);var old=original.GetComponentsInChildren<Transform>(true).Where(t=>!Addon(t)).ToArray();var now=transforms.Where(t=>!Addon(t)).ToArray();
   if(old.Length!=now.Length)throw new Exception("Original transform count changed");
   for(int i=0;i<old.Length;i++)if(old[i].name!=now[i].name||(!new[]{"REBIRTHRearDoorPivot","REBIRTHRearDoorOpenPose"}.Contains(old[i].name)&&(Vector3.Distance(old[i].localPosition,now[i].localPosition)>.00001f||Quaternion.Angle(old[i].localRotation,now[i].localRotation)>.001f||Vector3.Distance(old[i].localScale,now[i].localScale)>.00001f)))throw new Exception("Original rig changed: "+old[i].name);
   foreach(string name in new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow"})transforms.Single(t=>t.name==name).gameObject.SetActive(true);
   Render(root);Object.DestroyImmediate(root);File.WriteAllText(Output+"/verified.txt","PASS CRC="+crc+"; original rig transforms preserved except documented rear hinge/marker widening; 4 wheels; 30 markers; 27 convex impact colliders; hidden native mod roots; Crunch/texture dimensions. No driving/combat test.");
  }
  finally{bundle.Unload(true);}
 }
 static bool Addon(Transform t){for(var p=t;p!=null;p=p.parent)if(new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow","REBIRTHModPlowHull"}.Contains(p.name))return true;return false;}
 static void Render(GameObject root)
 {
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.6f,.6f);var key=new GameObject("ReviewKey").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.2f;key.transform.rotation=Quaternion.Euler(40,-35,0);
  var fill=new GameObject("ReviewFill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;fill.transform.rotation=Quaternion.Euler(25,135,0);
  var cam=new GameObject("ReviewCamera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=2.9f;cam.transform.position=new Vector3(6,3.6f,7);cam.transform.LookAt(new Vector3(0,1.6f,-.1f));cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.15f,.17f,.18f);
  var rt=new RenderTexture(1600,1100,24);var image=new Texture2D(1600,1100,TextureFormat.RGB24,false);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(Output+"/unity-both-mods.png",image.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;
  Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(cam.gameObject);Object.DestroyImmediate(key.gameObject);Object.DestroyImmediate(fill.gameObject);
 }
}





