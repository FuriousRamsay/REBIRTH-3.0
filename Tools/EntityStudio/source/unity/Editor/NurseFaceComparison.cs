using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class NurseFaceComparison {
 const string Out=NurseVisualOptimization.Out;
 public static void Run(){try{NurseFaceSculpt.Run();NurseEyeAlignment.Run();foreach(int angle in new[]{0,-30,30}){foreach(bool donor in new[]{true,false}){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  string path=donor?"Assets/NativeReferences/Moe/PrefabInstance/ZMoe.prefab":"Assets/EntityStudioGenerated/RebirthNurseNative/RebirthNurse_Authoring.prefab";
  var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));go.GetComponent<Animator>().enabled=false;go.GetComponent<LODGroup>().ForceLOD(0);
  go.transform.RotateAround(new Vector3(0,donor?1.72f:1.64f,0),Vector3.up,angle);
  NurseVisualOptimization.Render(go,(donor?"MoeFace_Quality_":"NurseFace_Distinct_")+angle,0,true);UnityEngine.Object.DestroyImmediate(go);
 }var a=Read("MoeFace_Quality_"+angle+".png");var b=Read("NurseFace_Distinct_"+angle+".png");var result=new Texture2D(a.width*2,a.height,TextureFormat.RGB24,false);result.SetPixels(0,0,a.width,a.height,a.GetPixels());result.SetPixels(a.width,0,b.width,b.height,b.GetPixels());result.Apply();File.WriteAllBytes(Out+"/Distinct_Female_Moe_Comparison_"+angle+".png",result.EncodeToPNG());UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);UnityEngine.Object.DestroyImmediate(result);}
 File.WriteAllText(Out+"/distinct-female-comparison.txt","Actual Unity renders: Moe left, distinct female Nurse right; front and +/-30 degrees, native shaders, identical key/fill/ambient lights. No donor face mesh or face texture is used by this comparison. Nurse has independent sculpt and generated female skin artwork; native blood surface effect is separate. Current source hair remains in these face-only captures; rebuilt-hair previews are separate. This records evidence, not a claim of quality equivalence.\n");Debug.Log("NURSE_DISTINCT_FACE_COMPARISON_READY");
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static Texture2D Read(string name){var t=new Texture2D(2,2);t.LoadImage(File.ReadAllBytes(Out+"/"+name));return t;}
 public static void Diagnose(){foreach(string mode in new[]{"NoNormal","Baseline","Clay","Unlit"}){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EntityStudioGenerated/RebirthNurseNative/RebirthNurse_Authoring.prefab"));go.GetComponent<Animator>().enabled=false;go.GetComponent<LODGroup>().ForceLOD(0);RebirthEntityStudio.RebirthNurseNativeShaderPreview.Bind(go);
  var body=go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="LOD0");
  if(mode=="Baseline")body.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(NurseVisualOptimization.Folder+"/"+NurseFaceSculpt.BaselinePrefix+"LOD0.asset");
  if(mode=="Clay")foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>{var clay=new Material(Shader.Find("Standard"));clay.color=new Color(.5f,.5f,.5f);clay.SetFloat("_Glossiness",.12f);return clay;}).ToArray();
  if(mode=="Unlit")foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>{var flat=new Material(Shader.Find("Unlit/Color"));flat.color=Color.white;return flat;}).ToArray();
  if(mode=="NoNormal")foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))foreach(var mat in r.sharedMaterials)if(mat.HasProperty("_Normal"))mat.SetTexture("_Normal",null);
  NurseVisualOptimization.Render(go,"Nurse_Diagnostic_"+mode,0,true,true);UnityEngine.Object.DestroyImmediate(go);
 }var rows=new System.Collections.Generic.List<string>();foreach(string prefix in new[]{"BeforeFaceSculptV1_","BeforeFaceSculptV3_"}){var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(NurseVisualOptimization.Folder+"/"+prefix+"LOD0.asset");rows.Add(prefix+": attributes="+String.Join(",",mesh.GetVertexAttributes().Select(a=>a.ToString()))+"; zeroNormals="+mesh.normals.Count(n=>n.sqrMagnitude<.01f)+" zeroTangents="+mesh.tangents.Count(t=>new Vector3(t.x,t.y,t.z).sqrMagnitude<.01f));}File.WriteAllLines(Out+"/face-shading-attribute-proof.txt",rows);Debug.Log("NURSE_FACE_SHADING_DIAGNOSIS_READY");}
}
