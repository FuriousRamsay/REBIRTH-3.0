using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 public static class RebirthAuthoredHairExport {
  public static void Attach(GameObject root,string folder,Mesh head,FaceTemplate template,FaceRecipe recipe){
   Mesh mesh=null;Material[] sources=null;
   try{
    mesh=RebirthHairGenerator.Build(head,recipe.hair,template,recipe);sources=RebirthHairGenerator.Materials(recipe.hair,recipe);
    if(mesh.subMeshCount!=sources.Length)throw new InvalidDataException("Hair material/submesh layout differs.");
    mesh.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(mesh,folder+"/Hair.asset");
    var materials=new List<Material>();var textures=new Dictionary<Texture,Texture>();
    for(int i=0;i<sources.Length;i++){
     var material=new Material(sources[i]){hideFlags=HideFlags.None};
     try{
      foreach(string property in material.GetTexturePropertyNames()){
       var texture=material.GetTexture(property);if(texture==null)continue;
       if(!textures.TryGetValue(texture,out var copy)){
        string input=AssetDatabase.GetAssetPath(texture);
        if(string.IsNullOrEmpty(input)||!input.StartsWith("Assets/",StringComparison.Ordinal))throw new InvalidDataException("Authored hair texture must be a project asset.");
        string output=AssetDatabase.GenerateUniqueAssetPath(folder+"/"+Path.GetFileName(input));
        if(!AssetDatabase.CopyAsset(input,output))throw new IOException("Could not copy hair texture.");
        copy=AssetDatabase.LoadAssetAtPath<Texture>(output);textures.Add(texture,copy);
       }
       material.SetTexture(property,copy);
      }
      RebirthFaceAppearance.CopyShader(material,folder);
      AssetDatabase.CreateAsset(material,folder+(i==0?"/Hair.mat":"/Hair_"+i+".mat"));materials.Add(material);
     }catch{if(!AssetDatabase.Contains(material))UnityEngine.Object.DestroyImmediate(material);throw;}
    }
    var child=new GameObject("Hair");child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();
   }finally{if(mesh!=null&&!AssetDatabase.Contains(mesh))UnityEngine.Object.DestroyImmediate(mesh);if(sources!=null)foreach(var material in sources)if(material!=null)UnityEngine.Object.DestroyImmediate(material);}
  }
 }
}
