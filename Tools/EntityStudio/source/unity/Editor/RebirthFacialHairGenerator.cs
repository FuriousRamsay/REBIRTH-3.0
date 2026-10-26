using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class FaceFacialHairSettings {
  public HairConditionSettings condition=new HairConditionSettings();
  public int style;public Color colour=new Color(.09f,.055f,.03f,1);public float grey,shine=.22f;
  public void Validate(){if(condition==null)condition=new HairConditionSettings();condition.Validate();style=Mathf.Clamp(style,0,5);foreach(float n in new[]{colour.r,colour.g,colour.b,grey,shine})if(float.IsNaN(n)||float.IsInfinity(n))throw new InvalidOperationException("Invalid facial-hair appearance.");colour=new Color(Mathf.Clamp01(colour.r),Mathf.Clamp01(colour.g),Mathf.Clamp01(colour.b),1);grey=Mathf.Clamp01(grey);shine=Mathf.Clamp01(shine);}
 }
 public sealed class FacialHairResult : IDisposable {
  public Mesh mesh;public Material[] materials=new Material[0];
  public void Dispose(){if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);foreach(var m in materials)if(m!=null)UnityEngine.Object.DestroyImmediate(m);}
 }
 public static class RebirthFacialHairGenerator {
  public static readonly string[] Styles={"None","Pointed beard","Full beard","Moustache","Full beard + moustache","Pointed beard + moustache"};
  static string folder;static AuthoredHairCatalog catalog;static readonly Dictionary<string,AuthoredHairMesh> meshes=new Dictionary<string,AuthoredHairMesh>();
  static readonly string[][] StyleKeys={new string[0],new[]{"beard_pointed"},new[]{"beard_full"},new[]{"moustache"},new[]{"beard_full","moustache"},new[]{"beard_pointed","moustache"}};
  public static Material[] Materials(FaceTemplate template,FaceRecipe recipe){
   var settings=recipe.facialHair;settings.Validate();if(settings.style==0)return new Material[0];Load(template);var result=new List<Material>();
   try{foreach(string key in StyleKeys[settings.style]){var style=catalog.styles.First(s=>s.key==key);var appearance=new FaceHairSettings{color=settings.colour,rootColor=settings.colour,grey=settings.grey,shine=settings.shine,condition=settings.condition};result.Add(RebirthAuthoredHair.Material(appearance,style,folder));}return result.ToArray();}
   catch{foreach(var m in result)UnityEngine.Object.DestroyImmediate(m);throw;}
  }
  static void Load(FaceTemplate template){if(catalog==null){string path=AssetDatabase.FindAssets("FacialHairCatalog t:TextAsset").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/FacialHairData/FacialHairCatalog.json",StringComparison.Ordinal));if(path==null)throw new FileNotFoundException("FacialHairData is missing.");folder=Path.GetDirectoryName(path).Replace('\\','/');catalog=JsonUtility.FromJson<AuthoredHairCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text);}if(catalog.version!=2||catalog.templateHash!=template.contentHash)throw new InvalidDataException("Facial-hair data requires its matching anatomical template.");}
  public static FacialHairResult Build(FaceTemplate template,FaceRecipe recipe){
   var result=new FacialHairResult();var settings=recipe.facialHair;settings.Validate();if(settings.style==0)return result;Load(template);
   var keys=StyleKeys;
   var skin=RebirthFaceGeneratorModel.EvaluateSkinPoints(template,recipe);float shift=1.747f-skin.Max(v=>v.y);var positions=new List<Vector3>();var uv=new List<Vector2>();var colours=new List<Color>();var triangles=new List<int[]>();var materials=new List<Material>();
   try{foreach(string key in keys[settings.style]){
    AuthoredHairMesh data;if(!meshes.TryGetValue(key,out data)){var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/"+key+".json");if(asset==null)throw new FileNotFoundException("Missing facial hair: "+key);data=JsonUtility.FromJson<AuthoredHairMesh>(asset.text);meshes.Add(key,data);}
    int start=positions.Count;for(int i=0;i<data.positions.Length/3;i++){
     var point=new Vector3(data.positions[i*3],data.positions[i*3+1],data.positions[i*3+2]);for(int k=0;k<3;k++){int id=data.bindingIds[i*3+k];var original=new Vector3(template.positions[id*3],template.positions[id*3+1],template.positions[id*3+2]);point+=(skin[id]-original)*data.bindingWeights[i*3+k];}
     positions.Add(point+Vector3.up*shift);uv.Add(new Vector2(data.uv[i*2],data.uv[i*2+1]));colours.Add(Color.white);
    }
    triangles.Add(data.triangles.Select(i=>i+start).ToArray());var style=catalog.styles.First(s=>s.key==key);var appearance=new FaceHairSettings{color=settings.colour,rootColor=settings.colour,grey=settings.grey,shine=settings.shine,condition=settings.condition};materials.Add(RebirthAuthoredHair.Material(appearance,style,folder));
   }
   result.materials=materials.ToArray();result.mesh=new Mesh{name="Facial hair",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};result.mesh.SetVertices(positions);result.mesh.SetUVs(0,uv);result.mesh.SetColors(colours);result.mesh.subMeshCount=triangles.Count;for(int i=0;i<triangles.Count;i++)result.mesh.SetTriangles(triangles[i],i);result.mesh.RecalculateNormals();result.mesh.RecalculateTangents();result.mesh.RecalculateBounds();return result;
   }catch{foreach(var material in materials)UnityEngine.Object.DestroyImmediate(material);if(result.mesh!=null)UnityEngine.Object.DestroyImmediate(result.mesh);throw;}
  }
  public static void Export(GameObject root,string folder,FaceTemplate template,FaceRecipe recipe){
   using(var result=Build(template,recipe)){
    if(result.mesh==null)return;
    var savedMesh=UnityEngine.Object.Instantiate(result.mesh);savedMesh.hideFlags=HideFlags.None;
    AssetDatabase.CreateAsset(savedMesh,folder+"/FacialHair.asset");
    var saved=new List<Material>();
    for(int i=0;i<result.materials.Length;i++){
     var material=new Material(result.materials[i]){hideFlags=HideFlags.None};
     try{
      foreach(string property in material.GetTexturePropertyNames()){
       var texture=material.GetTexture(property);if(texture==null)continue;
       string input=AssetDatabase.GetAssetPath(texture);
       if(string.IsNullOrEmpty(input)||!input.StartsWith("Assets/",StringComparison.Ordinal))throw new InvalidDataException("Facial hair texture is not a project asset.");
       string output=AssetDatabase.GenerateUniqueAssetPath(folder+"/Facial_"+Path.GetFileName(input));
       if(!AssetDatabase.CopyAsset(input,output))throw new IOException("Could not copy facial hair texture.");
       material.SetTexture(property,AssetDatabase.LoadAssetAtPath<Texture>(output));
      }
      RebirthFaceAppearance.CopyShader(material,folder);
      AssetDatabase.CreateAsset(material,folder+"/FacialHair_"+i+".mat");saved.Add(material);
     }catch{if(!AssetDatabase.Contains(material))UnityEngine.Object.DestroyImmediate(material);throw;}
    }
    var child=new GameObject("Facial hair");child.transform.SetParent(root.transform,false);
    child.AddComponent<MeshFilter>().sharedMesh=savedMesh;child.AddComponent<MeshRenderer>().sharedMaterials=saved.ToArray();
   }
  }
  public static void Draw(PreviewRenderUtility preview,FacialHairResult result){if(result?.mesh==null)return;for(int i=0;i<result.materials.Length;i++)RebirthNativePreviewLighting.Draw(preview,result.mesh,Matrix4x4.identity,result.materials[i],i);}
 }
}

