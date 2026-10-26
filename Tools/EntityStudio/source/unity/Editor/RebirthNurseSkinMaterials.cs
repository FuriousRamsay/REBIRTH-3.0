using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;
namespace RebirthEntityStudio {
public static class RebirthNurseSkinMaterials {
 public const string Folder="Assets/EntityStudioGenerated/RebirthNurseSkin";
 const string Output="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Tools/EntityStudio/out/NurseSkinOverlay";
 static Texture2D Read(Texture source){var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source)));return texture;}
 static Color Sample(Texture2D t,Vector2 uv){return t.GetPixelBilinear(uv.x,uv.y);}
 static float Luma(Color c){return .2126f*c.r+.7152f*c.g+.0722f*c.b;}
 static bool NeutralSkin(Color c){float hi=Mathf.Max(c.r,Mathf.Max(c.g,c.b)),lo=Mathf.Min(c.r,Mathf.Min(c.g,c.b));return Luma(c)>.08f&&Luma(c)<.85f&&(hi-lo)/Mathf.Max(hi,.001f)<.48f&&c.b<c.r*1.12f&&!(c.r>c.g*1.5f&&c.r>c.b*1.4f);}
 static float[] Mask(SkinnedMeshRenderer renderer,int width,int height,bool nurse){
  var mesh=renderer.sharedMesh;var p=mesh.vertices;var uv=mesh.uv;var normals=mesh.normals;var weights=mesh.boneWeights;var flags=new bool[p.Length];
  for(int i=0;i<p.Length;i++){
   if(nurse){var world=renderer.transform.TransformPoint(p[i]);flags[i]=(world.y>1.46f&&world.y<1.76f&&Mathf.Abs(world.x)<.105f&&world.z>.015f)||(Mathf.Abs(world.x)>.36f&&world.y>1.15f)||world.y<.13f;}
   else{var b=renderer.bones[weights[i].boneIndex0];string name=b!=null?b.name:"";flags[i]=name.Contains("ForeArm")||name.Contains("Hand")||name=="Head";}
  }
  var result=new float[width*height];var indices=mesh.triangles;
  for(int t=0;t<indices.Length;t+=3){int ia=indices[t],ib=indices[t+1],ic=indices[t+2];if(!flags[ia]&&!flags[ib]&&!flags[ic])continue;
   Vector2 a=Vector2.Scale(uv[ia],new Vector2(width,height)),b=Vector2.Scale(uv[ib],new Vector2(width,height)),c=Vector2.Scale(uv[ic],new Vector2(width,height));
   float den=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(den)<.000001f)continue;
   int minX=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,width-1),maxX=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,width-1);
   int minY=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,height-1),maxY=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,height-1);
   for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++){float u=((b.y-c.y)*(x+.5f-c.x)+(c.x-b.x)*(y+.5f-c.y))/den,v=((c.y-a.y)*(x+.5f-c.x)+(a.x-c.x)*(y+.5f-c.y))/den,w=1-u-v;if(u<0||v<0||w<0)continue;float amount=u*(flags[ia]?1:0)+v*(flags[ib]?1:0)+w*(flags[ic]?1:0);result[y*width+x]=Mathf.Max(result[y*width+x],Mathf.SmoothStep(0,1,amount));}
  }
  return result;
 }
 public static void Run(){try{Bake();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static void Bake(){
  Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
  var nurse=AssetDatabase.LoadAssetAtPath<GameObject>(RebirthNurseNativeAssembly.PrefabPath).GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="LOD0");
  var moe=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NativeReferences/Moe/PrefabInstance/ZMoe.prefab").GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="LOD0");
  var profile=AssetDatabase.LoadAssetAtPath<RebirthEntityRigProfile>("Assets/EntityStudioGenerated/RebirthNurse/RebirthNurse_RigProfile.asset");var sourceNormal=profile.generatedTierMaterials.First(m=>m.name.Contains("_Normal_Slot0"));
  var original=Read(sourceNormal.GetTexture(sourceNormal.HasProperty("_Albedo")?"_Albedo":"_MainTex"));var reference=Read(moe.sharedMaterial.GetTexture("_Albedo"));var packed=Read(moe.sharedMaterial.GetTexture("_RMOE"));
  var nativeMask=Mask(moe,reference.width,reference.height,false);Color referenceMean=Color.clear,packedMean=Color.clear;float samples=0;
  for(int y=0;y<reference.height;y+=2)for(int x=0;x<reference.width;x+=2){int i=y*reference.width+x;var uv=new Vector2((x+.5f)/reference.width,(y+.5f)/reference.height);var color=Sample(reference,uv);if(nativeMask[i]>.8f&&NeutralSkin(color)){referenceMean+=color;packedMean+=Sample(packed,uv);samples++;}}
  if(samples<10)throw new Exception("Not enough native exposed-skin samples");referenceMean/=samples;packedMean/=samples;
  int size=2048;var mask=Mask(nurse,size,size,true);Color originalMean=Color.clear;float ownSamples=0;
  for(int y=0;y<size;y++)for(int x=0;x<size;x++){int i=y*size+x;var c=Sample(original,new Vector2((x+.5f)/size,(y+.5f)/size));if(!NeutralSkin(c))mask[i]=0;if(mask[i]>.8f){originalMean+=c;ownSamples++;}}
  if(ownSamples<10)throw new Exception("Nurse skin mask empty");originalMean/=ownSamples;
  Vector3 gain=new Vector3(Mathf.Clamp(referenceMean.r/originalMean.r,.6f,1.1f),Mathf.Clamp(referenceMean.g/originalMean.g,.6f,1.1f),Mathf.Clamp(referenceMean.b/originalMean.b,.55f,1.1f));
  // Preserve wounds/cloth, normal detail and high-frequency skin texture; adjust only the sampled skin population.
  foreach(string tier in new[]{"Normal","Feral"}){
   var mat=profile.generatedTierMaterials.First(m=>m.name.Contains("_"+tier+"_Slot0"));var source=Read(mat.HasProperty("_Albedo")?mat.GetTexture("_Albedo"):mat.GetTexture("_MainTex"));var pixels=new Color[size*size];
   for(int y=0;y<size;y++)for(int x=0;x<size;x++){int i=y*size+x;var c=Sample(source,new Vector2((x+.5f)/size,(y+.5f)/size));var adjusted=new Color(c.r*gain.x,c.g*gain.y,c.b*gain.z,c.a);pixels[i]=Color.Lerp(c,adjusted,mask[i]);}
   Save(pixels,size,Folder+"/RebirthNurse_Albedo_BaseGame"+(tier=="Normal"?"":"_Feral")+".png",true);
  }
  foreach(string tier in new[]{"Normal","Feral","Special"}){
   var source=Read(profile.nativeRmoeMaps.Single(t=>t.name.EndsWith("_"+tier)));int n=512;var pixels=new Color[n*n];
   for(int y=0;y<n;y++)for(int x=0;x<n;x++){var uv=new Vector2((x+.5f)/n,(y+.5f)/n);var c=Sample(source,uv);float amount=0;for(int dy=0;dy<4;dy++)for(int dx=0;dx<4;dx++)amount+=mask[(y*4+dy)*size+x*4+dx];amount/=16;
    c.r=Mathf.Lerp(c.r,Mathf.Max(c.r,Mathf.Clamp(packedMean.r+.06f,.65f,.86f)),amount*.85f);c.g=Mathf.Lerp(c.g,Mathf.Min(packedMean.g,.012f),amount);c.b=Mathf.Lerp(c.b,Mathf.Clamp(packedMean.b,.7f,.95f),amount*.65f);pixels[y*n+x]=c;}
   Save(pixels,n,Folder+"/RebirthNurse_RMOE_"+tier+".png",false);
  }
  foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{RebirthNurseNativeAssembly.Folder})){var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));string tier=System.Text.RegularExpressions.Regex.Match(mat.name,@"_(Normal|Feral|Radiated|Charged|Infernal)(?:_Slot\d+)?(?: \d+)?$").Groups[1].Value;UseCorrectedMaps(mat,tier);EditorUtility.SetDirty(mat);}
  string path=RebirthEntityPrefabExporter.ManifestPath(RebirthNurseNativeAssembly.PrefabPath);var manifest=JsonUtility.FromJson<RebirthEntityPrefabManifest>(File.ReadAllText(path));manifest.nativeTextureAssetPaths=MapPaths();File.WriteAllText(path,JsonUtility.ToJson(manifest,true));AssetDatabase.Refresh();AssetDatabase.SaveAssets();
  File.WriteAllText(Output+"/skin-profile.txt","Native samples="+samples+"; Nurse samples="+ownSamples+"\nMoe skin RGB="+referenceMean+"; Nurse original RGB="+originalMean+"\nSkin channel gain="+gain+"\nNative skin RMOE="+packedMean+"\nNormals, geometry, skeleton and collider assets untouched.");Debug.Log("NURSE_SKIN_PROFILE_APPLIED");
 }
 static void Save(Color[] colors,int size,string path,bool srgb){var t=new Texture2D(size,size,TextureFormat.RGBA32,false);t.SetPixels(colors);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=srgb;importer.maxTextureSize=size;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();UnityEngine.Object.DestroyImmediate(t);}
 public static string[] MapPaths(){return new[]{"Normal","Feral","Special"}.Select(t=>Folder+"/RebirthNurse_RMOE_"+t+".png").ToArray();}
 public static void UseCorrectedMaps(Material mat,string tier){if(!mat.HasProperty("_Albedo")||!File.Exists(Folder+"/RebirthNurse_Albedo_BaseGame.png"))return;mat.SetTexture("_Albedo",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/RebirthNurse_Albedo_BaseGame"+(tier=="Feral"?"_Feral":"")+".png"));mat.SetTexture("_RMOE",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/RebirthNurse_RMOE_"+(tier=="Normal"?"Normal":tier=="Feral"?"Feral":"Special")+".png"));}
}
}
