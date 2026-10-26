using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace RebirthEntityStudio {
[InitializeOnLoad] public static class RebirthNurseNativeShaderPreview {
 static readonly Dictionary<string,Shader> shaders=new Dictionary<string,Shader>();
 static readonly List<AssetBundle> bundles=new List<AssetBundle>();
 static RebirthNurseNativeShaderPreview(){EditorApplication.delayCall+=BindOpen;EditorSceneManager.sceneOpened+=(_,__)=>EditorApplication.delayCall+=BindOpen;}
 static void BindOpen(){foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>().Where(g=>g.scene.IsValid()&&g.name=="RebirthNurse"&&g.GetComponent<Animator>()!=null).ToArray())if(go.scene.path==RebirthNurseNativeAssembly.ScenePath)Bind(go);}
 public static void Bind(GameObject model){
  shaders["Game/Character"]=RebirthNativeShaderLibrary.Get("Game/Character");
  shaders["Game/Autodesk"]=RebirthNativeShaderLibrary.Get("Game/Autodesk");
  foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
   var original=PrefabUtility.GetCorrespondingObjectFromSource(renderer);var mats=original!=null?original.sharedMaterials:renderer.sharedMaterials;
   renderer.sharedMaterials=mats.Select(mat=>{if(mat==null)throw new Exception("Nurse material missing");var keywords=mat.shaderKeywords;bool hair=mat.name.StartsWith("EntityStudioHair_")||mat.name.Contains("MoeHair");var preview=new Material(mat){name=mat.name+" [native shader preview]",hideFlags=HideFlags.HideAndDontSave};preview.shader=shaders[(mat.name.Contains("FeralEye")||hair)?"Game/Autodesk":"Game/Character"];preview.shaderKeywords=hair?new[]{"_ALPHATEST_ON","_GLOSSYREFLECTIONS_OFF","_NORMALMAP","_SPECGLOSSMAP","_SPECULARHIGHLIGHTS_OFF"}:keywords;return preview;}).ToArray();
  }
  SceneView.RepaintAll();
 }
}
}
