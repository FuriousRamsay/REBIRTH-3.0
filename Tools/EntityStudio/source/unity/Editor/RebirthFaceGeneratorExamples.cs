using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace RebirthEntityStudio {
 public static class RebirthFaceGeneratorExamples {
  // Generates real authoring examples and preview images; no game assets are installed.
  public static void Generate(){try{
   var template=RebirthFaceGeneratorWindow.ReadTemplate();const string folder="Assets/FaceGeneratorExamples";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","FaceGeneratorExamples");
   const string output="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Tools/EntityStudio/out/FaceGenerator";Directory.CreateDirectory(output);
   for(int index=0;index<template.presets.Length;index++){
    var recipe=FaceRecipe.FromPreset(template,index);recipe.resolution=1;
    var material=new Material(Shader.Find("Standard"));material.color=new Color(.42f,.42f,.42f);material.SetFloat("_Glossiness",.18f);
    RebirthFaceGeneratorWindow.Export(template,recipe,folder+"/"+recipe.name+".prefab",material);
    var mesh=RebirthFaceGeneratorModel.Build(template,recipe);var eye=new Material(material);eye.color=new Color(.24f,.25f,.24f);
    var preview=new PreviewRenderUtility();try{foreach(int angle in new[]{0,30}){
     preview.BeginStaticPreview(new Rect(0,0,1024,1024));RebirthFaceGeneratorWindow.ConfigurePreview(preview,new Vector2(angle,0),.19f,false);preview.DrawMesh(mesh,Matrix4x4.identity,material,0);preview.DrawMesh(mesh,Matrix4x4.identity,eye,1);preview.Render();var image=preview.EndStaticPreview();File.WriteAllBytes(output+"/"+recipe.name.Replace(' ','_')+"_"+angle+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
    }}finally{preview.Cleanup();UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(eye);}
   }
   File.WriteAllText(output+"/generation.txt","Generated five preset authoring prefabs and actual Unity front/three-quarter previews from the same face generator used by the Editor window. No game or Nurse asset changed. This does not constitute an interactive UI check or final sculpt-quality claim.\n");Debug.Log("REBIRTH_FACE_GENERATOR_EXAMPLES_READY");
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}
