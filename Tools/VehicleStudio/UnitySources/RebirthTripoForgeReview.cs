using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class RebirthTripoForgeReview {
 const string Root="Assets/REBIRTHStationAddons/TripoForge_20261013";
 const string Stem="tripo_convert_08c75d75-538c-4067-b0d4-857ce4d47387";
 const string Output="E:/_Haven/shared/REBIRTHTemp/TripoForgeUnityReview_20261013";
 static Texture2D Map(string suffix,bool srgb,bool normal=false){
  string path=Root+"/"+Stem+".fbm/stone_forge_3d_model_"+suffix;
  var i=(TextureImporter)AssetImporter.GetAtPath(path);if(i==null)throw new Exception(path);
  i.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;i.sRGBTexture=srgb;i.maxTextureSize=2048;i.isReadable=true;i.textureCompression=TextureImporterCompression.Uncompressed;i.SaveAndReimport();
  return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
 }
 public static void RunBatch(){Directory.CreateDirectory(Output);try{
  AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  string path=Root+"/"+Stem+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
  importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.SaveAndReimport();
  var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));root.name="Tripo Forge supplied model";
  var albedo=Map("basecolor.JPEG",true);var normal=Map("normal.PNG",false,true);var rough=Map("roughness.JPEG",false);var metal=Map("metallic.JPEG",false);
  if(rough.width!=metal.width||rough.height!=metal.height)throw new Exception("Map dimension mismatch");
  var rp=rough.GetPixels();var mp=metal.GetPixels();var packed=new Texture2D(metal.width,metal.height,TextureFormat.RGBA32,false,true);
  for(int i=0;i<mp.Length;i++)mp[i]=new Color(mp[i].r,0,0,1-rp[i].r);packed.SetPixels(mp);packed.Apply();
  string packedPath=Root+"/metallic-smoothness.png";File.WriteAllBytes(packedPath,packed.EncodeToPNG());AssetDatabase.ImportAsset(packedPath);
  var ti=(TextureImporter)AssetImporter.GetAtPath(packedPath);ti.sRGBTexture=false;ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.SaveAndReimport();
  var mat=new Material(Shader.Find("Standard"));mat.name="Tripo Forge PBR";mat.SetTexture("_MainTex",albedo);mat.SetTexture("_BumpMap",normal);mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));mat.SetFloat("_GlossMapScale",1);mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICGLOSSMAP");AssetDatabase.CreateAsset(mat,Root+"/Forge.mat");
  var rs=root.GetComponentsInChildren<Renderer>();foreach(var r in rs){var slots=r.sharedMaterials;for(int s=0;s<slots.Length;s++)slots[s]=mat;r.sharedMaterials=slots;}
  PrefabUtility.SaveAsPrefabAsset(root,Root+"/TripoForge.prefab");var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.5f);RenderSettings.fog=false;
  var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.2f;key.transform.rotation=Quaternion.Euler(40,-35,0);key.shadows=LightShadows.Soft;
  var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.55f;fill.transform.rotation=Quaternion.Euler(30,145,0);
  var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.position=new Vector3(bounds.center.x,bounds.min.y-.005f,bounds.center.z);floor.transform.localScale=Vector3.one*bounds.size.magnitude;
  var ground=new Material(Shader.Find("Standard"));ground.color=new Color(.16f,.16f,.16f);ground.SetFloat("_Glossiness",0);floor.GetComponent<Renderer>().sharedMaterial=ground;
  var camera=new GameObject("Review Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.11f,.11f);camera.nearClipPlane=.001f;camera.farClipPlane=1000;camera.orthographic=true;camera.orthographicSize=bounds.size.magnitude*.48f;
  int index=0;foreach(var d in new[]{new Vector3(-1,.65f,-2),new Vector3(1,.65f,-2),new Vector3(-1,.65f,2),new Vector3(1,.65f,2)}){
   camera.transform.position=bounds.center+d.normalized*bounds.size.magnitude*2;camera.transform.LookAt(bounds.center);
   var rt=new RenderTexture(1448,1086,24);var image=new Texture2D(1448,1086,TextureFormat.RGB24,false);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1448,1086),0,0);image.Apply();File.WriteAllBytes(Output+"/quarter-"+(++index)+".png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
  }
  long triangles=0;foreach(var mf in root.GetComponentsInChildren<MeshFilter>())triangles+=mf.sharedMesh.triangles.Length/3;
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"/ForgeReview.unity");AssetDatabase.SaveAssets();File.WriteAllText(Output+"/receipt.txt","Imported supplied Tripo FBX; triangles="+triangles+"; bounds="+bounds+"; albedo="+albedo.width+"x"+albedo.height+"; normal="+normal.width+"x"+normal.height+"; colorSpace="+QualitySettings.activeColorSpace+"; no supplied emission map; no bundle or game installation.");
 }catch(Exception e){File.WriteAllText(Output+"/failure.txt",e.ToString());throw;}}
}
