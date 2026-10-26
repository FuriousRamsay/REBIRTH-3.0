using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class AuthoredHairStyle {public string key,texture;public int vertices,triangles;public float textureGain=1;}
 [Serializable] public sealed class AuthoredHairCatalog {public int version;public string templateHash;public AuthoredHairStyle[] styles;}
 [Serializable] public sealed class AuthoredHairMesh {public string name;public float[] positions,uv,bindingWeights;public int[] triangles,bindingIds,sourceIds,faceSizes,faceIndices;}
 public static class RebirthAuthoredHair {
  static string folder;static AuthoredHairCatalog catalog;static readonly Dictionary<string,AuthoredHairMesh> meshes=new Dictionary<string,AuthoredHairMesh>();
  static readonly string[] Keys={"","short01","short02","bob01","bob02","long01","ponytail01","ponytail01","short04","short01","afro01","short03","braid01","long01"};
  static void Load(){if(catalog!=null)return;string path=AssetDatabase.FindAssets("HairCatalog t:TextAsset").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/HairGeneratorData/HairCatalog.json",StringComparison.Ordinal));if(path==null)throw new FileNotFoundException("HairGeneratorData is missing. Install the hair data folder with the editor scripts.");folder=Path.GetDirectoryName(path).Replace('\\','/');catalog=JsonUtility.FromJson<AuthoredHairCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text);if(catalog.version!=1&&catalog.version!=2)throw new InvalidDataException("Unsupported hair catalog.");}
  public static Mesh Build(FaceTemplate template,FaceRecipe recipe){
   Load();if(catalog.templateHash!=template.contentHash)throw new InvalidOperationException("Hair data requires its matching face template.");var h=recipe.hair;string key=Keys[h.style];AuthoredHairMesh data;if(!meshes.TryGetValue(key,out data)){var text=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/"+key+".json");if(text==null)throw new FileNotFoundException("Missing hairstyle: "+key);data=JsonUtility.FromJson<AuthoredHairMesh>(text.text);meshes.Add(key,data);}
   var skin=RebirthFaceGeneratorModel.EvaluateSkinPoints(template,recipe);float shift=1.747f-skin.Max(q=>q.y);var points=new List<Vector3>();var uv=new List<Vector2>();
   for(int i=0;i<data.positions.Length/3;i++){var q=new Vector3(data.positions[i*3],data.positions[i*3+1],data.positions[i*3+2]);for(int k=0;k<3;k++){int b=data.bindingIds[i*3+k];var original=new Vector3(template.positions[b*3],template.positions[b*3+1],template.positions[b*3+2]);q+=(skin[b]-original)*data.bindingWeights[i*3+k];}points.Add(q+Vector3.up*shift);uv.Add(new Vector2(data.uv[i*2],data.uv[i*2+1]));}
   var triangles=data.triangles.ToList();if(h.surfaceVersion==2&&h.quality>0){if(data.sourceIds==null||data.faceSizes==null||data.faceIndices==null)throw new InvalidDataException("Curved hair surfaces require the updated HairGeneratorData folder.");var topology=new FaceTemplate{sourceIds=data.sourceIds,faceSizes=data.faceSizes,faceIndices=data.faceIndices};RebirthFaceGeneratorModel.CatmullClark(topology,points,uv,h.quality,out points,out uv,out triangles);}else for(int level=0;level<h.quality;level++)Refine(points,uv,ref triangles);
   float top=skin.Max(q=>q.y)+shift,low=points.Min(q=>q.y),referenceLength=Mathf.Max(.03f,top-low),scale=Mathf.Clamp(h.length/referenceLength,.25f,3f);var scalpCenter=new Vector3(0,top-.08f,.025f);
   var colors=new List<Color>();for(int i=0;i<points.Count;i++){
    Vector3 q=points[i];float drop=Mathf.Max(0,top-.10f-q.y),free=Mathf.SmoothStep(0,1,drop/.10f);float progress=Mathf.Clamp01((top-q.y)/Mathf.Max(.08f,referenceLength));
    q.y-=drop*(scale-1);q.x*=1+(h.volume-.30f)*.28f; q.z=scalpCenter.z+(q.z-scalpCenter.z)*(1+(h.volume-.30f)*.18f);
    float phase=q.y*23+h.seed*.131f,twist=Mathf.Atan2(q.x,q.z-scalpCenter.z);float wave=Mathf.Sin(phase)*(h.wave*.012f+h.curl*.008f)*free;q.x+=wave;q.z+=Mathf.Cos(phase*(1+h.curl*2)+twist)*h.curl*.012f*free;
    float front=Mathf.SmoothStep(0,1,(q.z-.025f)/.07f);q.x+=h.part*.015f*front*Mathf.Clamp01((q.y-(top-.15f))/.15f);q.z-=h.hairline*.012f*front;q.y-=h.fringe*.015f*front*(1-free);
    q+=new Vector3(Mathf.Sin(q.y*83+q.z*59+h.seed),Mathf.Sin(q.x*67+q.y*71),Mathf.Cos(q.x*93+q.y*65))*h.frizz*.002f*free;

    points[i]=q;colors.Add(new Color(1,(Mathf.Sin(q.x*91+q.z*31)+1)*.5f,progress,1));
   }
   var mesh=new Mesh{name="Hair_"+key,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(points);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();var normal=mesh.normals;var sums=new Dictionary<Vector3Int,Vector3>();for(int i=0;i<points.Count;i++){var k=Vector3Int.RoundToInt(points[i]*1000000);Vector3 n;sums.TryGetValue(k,out n);sums[k]=n+normal[i];}mesh.normals=points.Select(q=>sums[Vector3Int.RoundToInt(q*1000000)].normalized).ToArray();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
  }
  static void Refine(List<Vector3> p,List<Vector2> uv,ref List<int> triangles){var edges=new Dictionary<ulong,int>();Func<int,int,int> midpoint=(a,b)=>{ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);int id;if(edges.TryGetValue(key,out id))return id;id=p.Count;p.Add((p[a]+p[b])*.5f);uv.Add((uv[a]+uv[b])*.5f);edges.Add(key,id);return id;};var result=new List<int>();for(int t=0;t<triangles.Count;t+=3){int a=triangles[t],b=triangles[t+1],c=triangles[t+2],ab=midpoint(a,b),bc=midpoint(b,c),ca=midpoint(c,a);result.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});}triangles=result;}
  static readonly HashSet<string> configuredTextures=new HashSet<string>();
  static Texture2D Texture(string path){
   if(configuredTextures.Add(path)){
    var importer=AssetImporter.GetAtPath(path) as TextureImporter;
    if(importer==null)throw new FileNotFoundException("Missing hair texture: "+path);
    if(!importer.alphaIsTransparency||importer.textureCompression!=TextureImporterCompression.Uncompressed||importer.anisoLevel!=8||!importer.mipMapsPreserveCoverage||importer.wrapMode!=TextureWrapMode.Clamp){
     importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
     importer.maxTextureSize=2048;importer.anisoLevel=8;importer.mipmapEnabled=true;
     importer.mipMapsPreserveCoverage=true;importer.alphaTestReferenceValue=.32f;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
    }
   }
   return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  }
  public static Material Material(FaceHairSettings h){Load();string key=Keys[h.style];var style=catalog.styles.FirstOrDefault(s=>s.key==key);if(style==null)throw new FileNotFoundException("Missing authored hairstyle: "+key);return Material(h,style,folder);}
  internal static Material Material(FaceHairSettings h,AuthoredHairStyle style,string folder){var shader=Shader.Find("REBIRTH/AuthoredHair");if(shader==null)throw new FileNotFoundException("AuthoredHair.shader is missing.");var mat=new Material(shader){name="Authored hair",hideFlags=HideFlags.HideAndDontSave};mat.mainTexture=Texture(folder+"/"+style.texture);mat.SetColor("_Color",h.color);mat.SetColor("_RootColor",h.rootColor);mat.SetFloat("_TextureGain",style.textureGain);mat.SetFloat("_Grey",h.grey);mat.SetFloat("_Glossiness",h.shine);RebirthHairCondition.Apply(mat,h.condition);return mat;}
 }
}

