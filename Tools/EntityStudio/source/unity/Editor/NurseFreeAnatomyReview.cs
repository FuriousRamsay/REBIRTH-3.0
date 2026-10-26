using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Isolated clay comparison only. Does not write source prefabs, rig or game assets.
public static class NurseFreeAnatomyReview {
 [Serializable] public class Part {public string name;public float[] positions,normals;public int[] triangles;}
 [Serializable] public class Input {public Part[] parts;}
 const string Out="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Tools/EntityStudio/out/NurseReconstructionV2";
 public static void Run(){try{
  var input=JsonUtility.FromJson<Input>(File.ReadAllText(Out+"/clay-unity-input.json"));
  foreach(int angle in new[]{0,-30,30}){
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var material=new Material(Shader.Find("Standard"));material.color=new Color(.42f,.42f,.42f);material.SetFloat("_Glossiness",.18f);
   var model=new GameObject("Nurse_FreeAnatomy_Clay");
   foreach(var part in input.parts){var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=Enumerable.Range(0,part.positions.Length/3).Select(i=>new Vector3(part.positions[i*3],part.positions[i*3+1],part.positions[i*3+2])).ToArray();mesh.normals=Enumerable.Range(0,part.normals.Length/3).Select(i=>new Vector3(part.normals[i*3],part.normals[i*3+1],part.normals[i*3+2])).ToArray();mesh.triangles=part.triangles;mesh.RecalculateBounds();var go=new GameObject(part.name);go.transform.SetParent(model.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;}
   var moe=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NativeReferences/Moe/PrefabInstance/ZMoe.prefab"));moe.GetComponent<Animator>().enabled=false;moe.GetComponent<LODGroup>().ForceLOD(0);
   foreach(var r in moe.GetComponentsInChildren<SkinnedMeshRenderer>(true)){if(r.name!="LOD0"&&r.name!="NormalEye"){r.enabled=false;continue;}var go=new GameObject("MoeClay");go.AddComponent<MeshFilter>().sharedMesh=NurseAnatomyRender.WorldSkin(r);go.AddComponent<MeshRenderer>().sharedMaterials=Enumerable.Repeat(material,r.sharedMesh.subMeshCount).ToArray();go.transform.position=new Vector3(-.20f,-.079f,.05f);go.transform.RotateAround(new Vector3(-.20f,1.638f,.10f),Vector3.up,angle);r.enabled=false;}
   model.transform.position=new Vector3(.20f,0,0);model.transform.RotateAround(new Vector3(.20f,1.638f,.13f),Vector3.up,angle);
   var camera=new GameObject("ReviewCamera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.20f;camera.aspect=2;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.19f);camera.transform.position=new Vector3(0,1.635f,2);camera.transform.LookAt(new Vector3(0,1.635f,.1f));
   var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.1f;key.transform.rotation=Quaternion.Euler(35,145,0);var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.65f;fill.transform.rotation=Quaternion.Euler(15,-35,0);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.48f,.48f);
   var rt=new RenderTexture(2048,1024,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(2048,1024,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,2048,1024),0,0);texture.Apply();File.WriteAllBytes(Out+"/Unity_Clay_Moe_Nurse_"+angle+".png",texture.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);
  }
  File.WriteAllText(Out+"/unity-clay-review.txt","Actual Unity clay diagnostics: CC0 female anatomical candidate on image left, native Moe on image right (front-facing camera reverses world X). Same material, lighting, orthographic scale and pixels/metre. Eye-height alignment and face-centered rotation. No portrait texture, fine sculpt/normal bake or installed asset changes. NOT COMPLETE.\n");Debug.Log("NURSE_FREE_ANATOMY_CLAY_REVIEW_READY");
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
