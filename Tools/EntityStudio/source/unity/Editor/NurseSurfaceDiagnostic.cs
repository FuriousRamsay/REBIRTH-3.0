using System;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class NurseSurfaceDiagnostic {
 public static void Run(){foreach(string mode in new[]{"Native","Unlit","NoNormal","Clay"}){
 EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EntityStudioGenerated/RebirthNurseNative/RebirthNurse_Authoring.prefab"));go.GetComponent<Animator>().enabled=false;go.GetComponent<LODGroup>().ForceLOD(0);RebirthEntityStudio.RebirthNurseNativeShaderPreview.Bind(go);
 foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){r.sharedMaterials=r.sharedMaterials.Select(original=>{var m=new Material(original);if(mode=="Unlit"){var t=m.HasProperty("_Albedo")?m.GetTexture("_Albedo"):m.GetTexture("_MainTex");m=new Material(Shader.Find("Unlit/Texture"));m.mainTexture=t;}if(mode=="NoNormal")foreach(string key in new[]{"_Normal","_BumpMap"})if(m.HasProperty(key))m.SetTexture(key,null);if(mode=="Clay"){m=new Material(Shader.Find("Standard"));m.color=new Color(.5f,.5f,.5f);m.SetFloat("_Glossiness",.2f);}return m;}).ToArray();}
 NurseVisualOptimization.Render(go,"SurfaceDiagnostic_"+mode,0,true,true);UnityEngine.Object.DestroyImmediate(go);
 }}
}
