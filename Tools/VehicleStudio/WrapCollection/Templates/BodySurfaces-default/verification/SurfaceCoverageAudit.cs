using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// GPU-only native geometry audit. Never reads discarded native mesh CPU buffers.
// Red = native paint with no current-template ink; green = covered; gray = native exclusion.
public static class SurfaceCoverageAudit {
 static string Arg(string k){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,k);if(i<0)throw new Exception(k);return a[i+1];}
 [Serializable] public class Gap {public int x,y;public Vector3 position;}
 [Serializable] public class View {public string name;public int uncoveredPixels,coveredPixels;public Gap[] gapSamples;}
 [Serializable] public class Report {public string status="DIAGNOSTIC_NOT_APPROVAL";public View[] views;}
 [Serializable] public class Source {public string target,sha256;}
 [Serializable] public class CompileProof {public string status="PASS";public int compiledShaderFamilies=4;public Source[] mappingFiles;}
 static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
 static readonly string[] Ids={"hood","left-continuous","right-continuous","camper-front-continuous","camper-rear-fixed-continuous","rear-door","camper-roof","cab-roof","truck-front-face","cab-back","pickup-tailgate","pickup-bed-interior-unfolded"};
 static readonly string[] Props={"_ConceptHoodAtlas","_ConceptSceneAtlas","_ConceptBotanicalAtlas","_ConceptGardenCorner","_ConceptMusicCorner","_WrapLeftBandSharp","_WrapRightBandSharp","_WrapRearSharp","_WrapFrontSharp","_WrapRearDoorArt","_WrapRearLeftArt","_WrapRearRightArt"};
 public static int Role(string name){
  if(name=="Tailgate inner pressed sheet"||name=="Tailgate threshold")return 15;
  if(name=="Tailgate folded outer shell"||name=="Formed rear lower sill")return 11;
  if(name=="Continuous bed front wall stamping"||name.StartsWith("Painted bed front corner"))return 14;
  if(name=="Continuous inner bedside -1"||name=="Formed inner rear bed jamb -1")return 12;
  if(name=="Continuous inner bedside 1"||name=="Formed inner rear bed jamb 1")return 13;
  return 0;
 }
 public static void Run(){
  if(Environment.GetCommandLineArgs().Contains("-coverageRegenerateReleaseMasks")){RebirthNativeCutoutTemplates.OutputOverride=Arg("-coverageMasks");RebirthNativeCutoutTemplates.CombinedOverride=true;RebirthNativeCutoutTemplates.Run();}
  if(Environment.GetCommandLineArgs().Contains("-coverageGenerateCandidate")){
   SurfaceCoverageCandidateTemplates.OutputOverride=Path.Combine(Path.GetDirectoryName(Arg("-coverageMasks")),"Templates/NativeCutouts-v3");SurfaceCoverageCandidateTemplates.CombinedOverride=false;SurfaceCoverageCandidateTemplates.Run();
   SurfaceCoverageCandidateTemplates.OutputOverride=Arg("-coverageMasks");SurfaceCoverageCandidateTemplates.CombinedOverride=true;SurfaceCoverageCandidateTemplates.Run();
  }
  string output=Arg("-wrapReviewOutput"),masks=Arg("-coverageMasks");Directory.CreateDirectory(output);
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/SurfaceCoverageAudit.shader");
  if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Audit shader invalid");
  bool integration=Environment.GetCommandLineArgs().Contains("-coverageIntegration");
  var families=new Dictionary<string,Shader>();
  if(integration){
   var sources=new List<Source>();
   foreach(string file in new[]{"RebirthCamperReferenceWrapCollection.shader","RebirthPickupOriginalFinishCollection.shader","RebirthCamperTallReferenceWrapCollection.shader","RebirthCamperAlignedStripeCollection.shader"}){
    var compiled=AssetDatabase.LoadAssetAtPath<Shader>("Assets/REBIRTHCamperWrapCollection/Shaders/"+file);if(!compiled||ShaderUtil.ShaderHasError(compiled))throw new Exception("Release shader failed: "+file);
    var witness=file=="RebirthCamperReferenceWrapCollection.shader"?shader:AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/Final-"+file);if(!witness||ShaderUtil.ShaderHasError(witness))throw new Exception("Family witness failed: "+file);
    families[compiled.name.Replace("Collection","")]=witness;sources.Add(new Source{target=file,sha256=Hash("Assets/REBIRTHCamperWrapCollection/Shaders/"+file)});
   }
   File.WriteAllText(Path.Combine(output,"compiled-shader-families.json"),JsonUtility.ToJson(new CompileProof{mappingFiles=sources.ToArray()},true));
  }
  var bundle=AssetBundle.LoadFromFile(Arg("-wrapVehicleBundle"));if(!bundle)throw new Exception("Native bundle missing");
  var root=Object.Instantiate(bundle.LoadAsset<GameObject>("rebirthCamper80s"));root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;
  bool grid=Environment.GetCommandLineArgs().Contains("-coverageGrid");Shader.SetGlobalFloat("_AuditGrid",grid?1:0);
  var textures=Ids.Select(id=>{var t=new Texture2D(2,2,TextureFormat.RGBA32,false,true);ImageConversion.LoadImage(t,File.ReadAllBytes(Path.Combine(masks,id+(grid?"-grid.png":"-paintable.png"))));t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Point;return t;}).ToArray();
  var model=root.transform.Find("GameObject");if(!model)throw new Exception("Native model frame missing");
  var clones=new List<Material>();
  foreach(var l in root.GetComponentsInChildren<LODGroup>(true))l.ForceLOD(0);
  foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
   r.SetPropertyBlock(null);var row=model.worldToLocalMatrix*r.localToWorldMatrix;
   var mats=r.sharedMaterials.Select(m=>{
    if(!m||!m.HasProperty("_PatternEnabled"))return m;
    var n=new Material(m);n.shader=integration&&families.ContainsKey(m.shader.name)?families[m.shader.name]:shader;n.SetFloat("_PatternEnabled",1);n.SetFloat("_WrapLayoutVariant",3);n.SetFloat("_WrapFullCoverage",1);
    n.SetFloat("_NativeSurfaceRole",Role(r.name));
    n.SetFloat("_WrapRearDoorPanel",r.name=="RearDoor_camper_body"?1:0);
    for(int k=0;k<3;k++){n.SetVector("_VehicleRow"+k,row.GetRow(k));n.SetVector("_WrapDoorRow"+k,row.GetRow(k));}
    if(integration&&r.name=="RearDoor_camper_body"){
     var bind=typeof(RebirthCamperWrapAppearance).GetMethod("SetRearDoorMapping",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);if(bind==null)throw new Exception("Runtime door binder missing");bind.Invoke(null,new object[]{n,r,m});
    }
    for(int k=0;k<Ids.Length;k++)n.SetTexture(Props[k],textures[k]);clones.Add(n);return n;
   }).ToArray();r.sharedMaterials=mats;
   for(int k=0;k<mats.Length;k++)r.SetPropertyBlock(null,k);
   if(r.name.Contains("REBIRTHDistanceLOD"))r.enabled=false;
  }
  RenderSettings.ambientLight=Color.black;
  var camera=new GameObject("Coverage witness").AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.01f;camera.farClipPlane=30;camera.allowHDR=false;camera.allowMSAA=false;
  var views=new List<View>();
  Action<string,Vector3,Vector3,float> render=(name,pos,target,size)=>{
   camera.transform.position=pos;camera.transform.LookAt(target,Mathf.Abs((target-pos).normalized.y)>.99f?Vector3.forward:Vector3.up);camera.orthographicSize=size;camera.aspect=1.6f;
   var rt=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
   var t=new Texture2D(1600,1000,TextureFormat.RGB24,false,true);t.ReadPixels(new Rect(0,0,1600,1000),0,0);t.Apply();var colors=t.GetPixels32();var v=new View{name=name};
   if(!grid)foreach(var c in colors){if(c.r>200&&c.g<40)v.uncoveredPixels++;if(c.g>200&&c.r<40)v.coveredPixels++;}
   File.WriteAllBytes(Path.Combine(output,name+".png"),t.EncodeToPNG());
   if(v.uncoveredPixels>0 && Environment.GetCommandLineArgs().Contains("-coverageCoordinates")){
    var frt=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);camera.targetTexture=frt;Shader.SetGlobalFloat("_AuditCoordinates",1);camera.Render();RenderTexture.active=frt;
    var ft=new Texture2D(1600,1000,TextureFormat.RGBAFloat,false,true);ft.ReadPixels(new Rect(0,0,1600,1000),0,0);ft.Apply();var fc=ft.GetPixels();var gaps=new List<Gap>();int seen=0,stride=Math.Max(1,v.uncoveredPixels/500);
    for(int k=0;k<colors.Length;k++)if(colors[k].r>200&&colors[k].g<40&&seen++%stride==0){var c=fc[k];gaps.Add(new Gap{x=k%1600,y=k/1600,position=new Vector3((c.r-.5f)*10,(c.g-.5f)*10,(c.b-.5f)*10)});}
    v.gapSamples=gaps.ToArray();Shader.SetGlobalFloat("_AuditCoordinates",0);Object.DestroyImmediate(ft);frt.Release();Object.DestroyImmediate(frt);
   }
   views.Add(v);RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(t);rt.Release();Object.DestroyImmediate(rt);
  };
  foreach(bool pickup in new[]{false,true}){
   foreach(var tr in root.GetComponentsInChildren<Transform>(true)){
    if(tr.name=="REBIRTHCamperBodyModule")tr.gameObject.SetActive(!pickup);
    if(tr.name=="REBIRTHPickupOnlyPaint"||tr.name=="REBIRTHPickupOnlyBodywork")tr.gameObject.SetActive(pickup);
    if(new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment","REBIRTHCampStairVisuals","REBIRTHCampWalkInColliders"}.Contains(tr.name))tr.gameObject.SetActive(false);
   }
   string p=pickup?"pickup-":"camper-";
   render(p+"left",new Vector3(-8,2,-.5f),new Vector3(0,1.7f,-.5f),1.95f);render(p+"right",new Vector3(8,2,-.5f),new Vector3(0,1.7f,-.5f),1.95f);
   render(p+"front",new Vector3(0,2,7),new Vector3(0,1.7f,0),1.9f);render(p+"rear",new Vector3(0,2,-8),new Vector3(0,1.7f,-1.5f),1.9f);
   render(p+"top",new Vector3(0,9,-.6f),new Vector3(0,0,-.6f),3.2f);
   foreach(int sign in new[]{-1,1})render(p+"hood-edge-"+sign,new Vector3(sign*2,2.5f,3),new Vector3(sign*.75f,1.25f,1.2f),.85f);
   if(Environment.GetCommandLineArgs().Contains("-coverageSweep"))foreach(float height in new[]{.85f,2.5f,5f})for(int angle=0;angle<12;angle++){float a=angle*Mathf.PI/6;render(p+"sweep-"+height+"-"+angle,new Vector3(Mathf.Sin(a)*7,height,Mathf.Cos(a)*7-.6f),new Vector3(0,1.5f,-.6f),2.4f);}
   if(!pickup){
    var door=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="REBIRTHRearDoorPivot");var pose=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="REBIRTHRearDoorOpenPose");var closed=door.localRotation;
    render("camper-door-closed-detail",new Vector3(0,1.7f,-7),new Vector3(0,1.7f,-3.3f),1.35f);
    door.localRotation=pose.localRotation;render("camper-door-open-detail",new Vector3(-4,2.3f,-7),new Vector3(0,1.7f,-2.5f),1.9f);door.localRotation=closed;
   }
   if(pickup){render(p+"bed-left",new Vector3(1,2.3f,-1.57f),new Vector3(-.78f,.9f,-1.57f),1.1f);render(p+"bed-right",new Vector3(-1,2.3f,-1.57f),new Vector3(.78f,.9f,-1.57f),1.1f);render(p+"cab-back",new Vector3(0,2,-2),new Vector3(0,1.3f,-.65f),.9f);render(p+"tailgate",new Vector3(0,1.5f,-5),new Vector3(0,.9f,-2.7f),1);render(p+"bed-front",new Vector3(0,2.7f,-3),new Vector3(0,.9f,-.43f),1.2f);render(p+"bed-rear",new Vector3(0,2.7f,-.5f),new Vector3(0,.9f,-2.7f),1.2f);}
  }
  if(integration){int index=0;foreach(var family in families.Values){foreach(var material in clones)material.shader=family;render("shader-family-"+(index++),new Vector3(-5,3,5),new Vector3(0,1.3f,-.6f),2.2f);}}
  File.WriteAllText(Path.Combine(output,"coverage.json"),JsonUtility.ToJson(new Report{status=grid?"GRID_ORIENTATION_WITNESS_NOT_COVERAGE":"DIAGNOSTIC_NOT_APPROVAL",views=views.ToArray()},true));Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(root);foreach(var m in clones)Object.DestroyImmediate(m);foreach(var t in textures)Object.DestroyImmediate(t);bundle.Unload(true);Debug.Log("SURFACE_COVERAGE_AUDIT_COMPLETE views="+views.Count);
 }
}
