using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;
public static class RebirthWrapSwitchRegression
{
 static void Check(bool ok,string message){if(!ok)throw new Exception("SWITCH_REGRESSION: "+message);}
 static bool ColorNear(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b),Mathf.Abs(a.a-b.a))<.002f;
 static void NativeColors(GameObject root,Color color,List<string> notes){
  // Exact native VehiclePart.SetColors material operations from installed IL.
  var body=root.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="body");
  var paint=body.GetComponentsInChildren<Renderer>(true);
  Check(paint.Length>1,"actual truck paint subtree lacks LODs");
  var material=body.material;material.color=color;
  foreach(var renderer in paint.Skip(1))if(renderer.CompareTag("LOD")&&renderer.sharedMaterials.Length==1)renderer.material=material;
  notes.Add("native paint subtree="+paint.Length+" aliased="+paint.Count(r=>ReferenceEquals(r.sharedMaterial,material))+" main="+material.name);
 }
 static object BindingFor(Renderer renderer){
  var table=typeof(RebirthCamperWrapAppearance).GetField("Bindings",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
  var args=new object[]{renderer,null};Check((bool)table.GetType().GetMethod("TryGetValue").Invoke(table,args),"missing binding");return args[1];
 }
 static bool OwnedSlot(Material material,object binding)=> (bool)typeof(RebirthCamperWrapAppearance).GetMethod("IsOwnedSlot",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{material,binding,0});
 static void SharedVehicleControls(GameObject prefab,List<string> notes){
  var a=Object.Instantiate(prefab);var b=Object.Instantiate(prefab);
  try{
   var ea=new EntityVehicle{ModelTransform=a.transform.Find("GameObject")};var eb=new EntityVehicle{ModelTransform=b.transform.Find("GameObject")};
   var ra=a.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="body");var rb=b.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="body");
   var shared=ra.sharedMaterial;Check(ReferenceEquals(shared,rb.sharedMaterial),"negative control did not start with shared original");
   Color initial=shared.color;
   RebirthCamperPattern.Value="MidnightCartographer";RebirthCamperPatternAppearance.Apply(ea);RebirthCamperPatternAppearance.Apply(eb);
   NativeColors(a,new Color(70,160,220,1),notes);RebirthCamperPatternAppearance.Apply(ea);
   Check(ColorNear(shared.color,initial),"mutated shared original on recolor");
   Check(ColorNear(rb.sharedMaterial.color,initial),"tinted second wrapped vehicle");
   RebirthCamperPattern.Value="none";RebirthCamperPatternAppearance.Apply(ea);
   Check(ColorNear(ra.sharedMaterial.color,new Color(70,160,220,1)),"private restored base lost tint");
   Check(!ReferenceEquals(ra.sharedMaterial,shared),"retinted original was not made private");
   Check(ColorNear(shared.color,initial)&&ColorNear(rb.sharedMaterial.color,initial),"removal tinted other vehicle/shared asset");
   Object.DestroyImmediate(a);a=null;
   Check(rb.sharedMaterial!=null&&ColorNear(rb.sharedMaterial.color,initial),"first vehicle cleanup damaged second");
   RebirthCamperPattern.Value="none";RebirthCamperPatternAppearance.Apply(eb);
   Check(ReferenceEquals(rb.sharedMaterial,shared),"untinted second vehicle did not retain exact shared original");
   notes.Add("PASS two vehicles share exact original; first recolor/removal/destroy never changes second or shared asset");
   RebirthCamperPattern.Value="MidnightCartographer";RebirthCamperPatternAppearance.Apply(eb);
   var bound=BindingFor(rb);
   var foreign=new Material(Shader.Find("Standard")){name="DestroyedForeignSentinel"};Object.DestroyImmediate(foreign);
   Check(foreign==null&&!ReferenceEquals(foreign,null),"negative control lacks destroyed Unity wrapper");
   Check(!OwnedSlot(foreign,bound),"recognized unrelated destroyed wrapper as owned");
   var own=rb.sharedMaterial;
   var lod=rb.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="REBIRTHDistanceLOD1");var lodBinding=BindingFor(lod);
   Object.DestroyImmediate(own);
   Check(OwnedSlot(own,lodBinding),"lost weak ownership of destroyed native alias");
   notes.Add("PASS destroyed foreign wrapper rejected; registered destroyed alias recognized");
   RebirthCamperPattern.Value="none";RebirthCamperPatternAppearance.Apply(eb);
  }finally{if(a)Object.DestroyImmediate(a);Object.DestroyImmediate(b);}
 }
 public static void Run(GameObject prefab,string output){
  var notes=new List<string>();
  // Tags in an AssetBundle depend on the host project's TagManager. Recreate
  // the game's LOD tag in this disposable editor project, then mark only the
  // native body's two explicitly named distance meshes for the LOD alias case.
  var manager=new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
  var tags=manager.FindProperty("tags");bool hasTag=false;
  for(int i=0;i<tags.arraySize;i++)if(tags.GetArrayElementAtIndex(i).stringValue=="LOD")hasTag=true;
  if(!hasTag){tags.InsertArrayElementAtIndex(tags.arraySize);tags.GetArrayElementAtIndex(tags.arraySize-1).stringValue="LOD";manager.ApplyModifiedProperties();}

  var root=Object.Instantiate(prefab);root.name="SwitchRegression";
  var nativeBody=root.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="body");
  foreach(var r in nativeBody.GetComponentsInChildren<Renderer>(true))if(r.name=="REBIRTHDistanceLOD1"||r.name=="REBIRTHDistanceLOD2")r.gameObject.tag="LOD";
  notes.Add("Fixture LOD tags assigned only to native distance meshes; no production prefab changes.");
  var actor=new EntityVehicle{ModelTransform=root.transform.Find("GameObject")};
  var original=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>m&&m.HasProperty("_PatternEnabled"))).ToDictionary(r=>r,r=>r.sharedMaterials);
  string[] ids={"CaptStress","none","CaptStress","GenmaSaotome","MidnightCartographer","GenmaSaotome","UndeadChill","UndeadChillAfterHours","UndeadChill","MidnightCartographer","disco","UndeadChillAfterHours","none","MidnightCartographer","none"};
  var arguments=Environment.GetCommandLineArgs();int selectedAt=Array.IndexOf(arguments,"-wrapIds");
  if(selectedAt>=0&&selectedAt+1<arguments.Length){
   var selected=arguments[selectedAt+1].Split(',').Where(RebirthCamperWrapCatalog.Contains).Distinct();
   ids=selected.SelectMany(id=>new[]{id,"MidnightCartographer",id,"none"}).Concat(ids).ToArray();
  }
  try{
   foreach(var id in ids){
    // Emulate native SetColors before cosmetic observer, as native item changes do.
    NativeColors(root,new Color(120,180,210,1),notes);
    RebirthCamperPattern.Value=id;RebirthCamperPatternAppearance.Apply(actor);
    foreach(var pair in original){
     var current=pair.Key.sharedMaterials;
     for(int i=0;i<current.Length;i++){
      Check(current[i]!=null,id+" destroyed/null material at "+pair.Key.name+" slot="+i);
      if(RebirthCamperWrapCatalog.Contains(id)){
       Check(current[i].name.EndsWith("_Wrap_"+id),id+" wrong binding "+pair.Key.name+" "+current[i].name);
       Check(current[i].GetTexture("_WrapAtlas")!=null,id+" missing atlas "+pair.Key.name);
       if(RebirthCamperWrapAppearance.UsesNativeBodyLayout(id))Check(current[i].GetFloat("_WrapLayoutVariant")==RebirthCamperWrapAppearance.NativeBodyLayoutVersion(id),id+" wrong native template version");
      }else Check(!current[i].shader.name.EndsWith("Collection"),id+" retained collection shader "+pair.Key.name);
     }
    }
    if(id=="none"||id=="disco")Check(ColorNear(nativeBody.sharedMaterial.color,new Color(120,180,210,1)),id+" lost native tint");
    notes.Add("PASS selector="+id+" paintRenderers="+original.Count+" cachedAtlas="+RebirthCamperWrapAppearance.CachedAtlasCount);
    // Also cover a native recolor while selector stays unchanged.
    NativeColors(root,new Color(210,130,90,1),notes);
    RebirthCamperPatternAppearance.Apply(actor);
    foreach(var renderer in original.Keys){
      Check(renderer.sharedMaterials.All(m=>m!=null),id+" null after unchanged-selector recolor "+renderer.name);
      if(RebirthCamperWrapCatalog.Contains(id))Check(renderer.sharedMaterials.All(m=>m.name.EndsWith("_Wrap_"+id)),id+" unstable material identity after recolor "+renderer.name);
    }
    Check(ColorNear(nativeBody.sharedMaterial.color,new Color(210,130,90,1)),id+" recolor not preserved actual="+nativeBody.sharedMaterial.color.ToString("F6")+" property="+nativeBody.sharedMaterial.GetColor("_Color").ToString("F6"));
   }
   // A material supplied by another appearance feature is not our clone.
   RebirthCamperPattern.Value="MidnightCartographer";RebirthCamperPatternAppearance.Apply(actor);
   var foreign=new Material(Shader.Find("Standard")){name="ForeignAppearanceSentinel"};
   nativeBody.sharedMaterial=foreign;
   RebirthCamperPattern.Value="none";RebirthCamperPatternAppearance.Apply(actor);
   Check(ReferenceEquals(nativeBody.sharedMaterial,foreign),"overwrote unrelated replacement");
   Check(RebirthCamperWrapAppearance.CachedAtlasCount==0,"atlas retained after all removals");
   notes.Add("PASS unrelated replacement preserved; cache empty after removal");
   Object.DestroyImmediate(foreign);
   SharedVehicleControls(prefab,notes);
   notes.Add("SWITCH_REGRESSION_PASS");Debug.Log("SWITCH_REGRESSION_PASS");
  }catch(Exception e){notes.Add(e.ToString());throw;}
  finally{File.WriteAllLines(Path.Combine(output,"switch-regression.txt"),notes);Object.DestroyImmediate(root);ModEvents.WorldShuttingDown.Shutdown();}
 }
}
