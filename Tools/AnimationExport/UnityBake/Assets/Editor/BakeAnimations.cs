using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Playables;
using UnityEngine.Animations;
public static class BakeAnimations {
 public static void ValidateExports() {
  try {
   foreach(string label in new[]{"Zombie","Player"}) {
    string path="Assets/Validation/Vanilla_"+label+"_Animations.fbx";
    if(!File.Exists(path)) continue;
    var clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
    int expected=label=="Zombie"?376:947;
    if(clips.Length!=expected) throw new Exception(label+" import count="+clips.Length+" expected="+expected);
    var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
    int bound=clips.Sum(c=>AnimationUtility.GetCurveBindings(c).Length);
    Debug.Log("FBX_IMPORT_PASS "+label+" takes="+clips.Length+" transforms="+model.GetComponentsInChildren<Transform>(true).Length+" bindings="+bound);
   }
   EditorApplication.Exit(0);
  } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
 }
 static string Root { get { return Path.GetFullPath(Path.Combine(Application.dataPath,"../../../..")); } }
 static void Vec(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
 static void Quat(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
 public static void Run() {
  try {
   string game=Path.GetFullPath(Path.Combine(Root,"../.."));
   string addr=Path.Combine(game,"Data/Addressables/Standalone");
   if(!File.Exists(Path.Combine(Root,"Tools/AnimationExport/output/zombie.baked"))) Bake(Path.Combine(addr,"animations_assets_animations/zombie.bundle"),"ZAnimBase","zombie");
   AssetBundle.LoadFromFile(Path.Combine(addr,"player_assets_entities/player/male/common.bundle"));
   AssetBundle.LoadFromFile(Path.Combine(addr,"player_assets_entities/player/female/common.bundle"));
   Bake(Path.Combine(addr,"player_assets_entities/player/common.bundle"),"baseRig","player");
   EditorApplication.Exit(0);
  } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
 }
 static void Bake(string source,string rigName,string label) {
  var bundle=AssetBundle.LoadFromFile(source);
  if(!bundle) throw new Exception("Cannot load "+source);
  var clips=bundle.LoadAllAssets<AnimationClip>().Concat(bundle.LoadAllAssets<RuntimeAnimatorController>().SelectMany(c=>c.animationClips)).Where(c=>c).Distinct().ToArray();
  if(label=="player") clips=AssetBundle.GetAllLoadedAssetBundles().SelectMany(b=>b.LoadAllAssets<AnimationClip>().Concat(b.LoadAllAssets<RuntimeAnimatorController>().SelectMany(c=>c.animationClips))).Where(c=>c).Distinct().ToArray();
  var prefabs=bundle.LoadAllAssets<GameObject>();
  Debug.Log("BAKE "+label+" clips="+clips.Length+" prefabs="+prefabs.Length);
  File.WriteAllLines(Path.Combine(Root,"Tools/AnimationExport/"+label+".unity-assets.txt"),bundle.GetAllAssetNames());
  var prefab=prefabs.FirstOrDefault(g=>g.name==rigName);
  if(!prefab) throw new Exception("Missing rig "+rigName+" available: "+string.Join(",",prefabs.Select(g=>g.name)));
  var go=UnityEngine.Object.Instantiate(prefab); go.name=rigName;
  var animator=go.GetComponent<Animator>();
  if(!animator) animator=go.AddComponent<Animator>();
  animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
  animator.applyRootMotion=true;
  var bones=go.GetComponentsInChildren<Transform>(true);
  var pos=bones.Select(t=>t.localPosition).ToArray();
  var rot=bones.Select(t=>t.localRotation).ToArray();
  var scale=bones.Select(t=>t.localScale).ToArray();
  using(var w=new BinaryWriter(File.Create(Path.Combine(Root,"Tools/AnimationExport/output/"+label+".baked")))) {
   w.Write("REBIRTH_ANIMATION_BAKE_1"); w.Write(bones.Length);
   for(int i=0;i<bones.Length;i++) { w.Write(bones[i].name); w.Write(Array.IndexOf(bones,bones[i].parent)); Vec(w,pos[i]); Quat(w,rot[i]); Vec(w,scale[i]); }
   w.Write(clips.Length);
   var names=new System.Collections.Generic.HashSet<string>();
   foreach(var clip in clips.OrderBy(c=>c.name,StringComparer.Ordinal)) {
    for(int i=0;i<bones.Length;i++) { bones[i].localPosition=pos[i]; bones[i].localRotation=rot[i]; bones[i].localScale=scale[i]; }
    string name=clip.name; int duplicate=1; while(!names.Add(name)) name=clip.name+"__"+(duplicate++);
    float rate=clip.frameRate>0?clip.frameRate:30;
    int frames=Math.Max(1,Mathf.CeilToInt(clip.length*rate));
    w.Write(name); w.Write(clip.name); w.Write(clip.length); w.Write(rate); w.Write(clip.isHumanMotion); w.Write(frames+1);
    var graph=PlayableGraph.Create("ExportBake");
    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
    var playable=AnimationClipPlayable.Create(graph,clip); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
    var output=AnimationPlayableOutput.Create(graph,"Pose",animator); output.SetSourcePlayable(playable); graph.Play();
    for(int f=0;f<=frames;f++) {
     float time=Math.Min(f/rate,clip.length);
     if(f==0) { playable.SetTime(0); graph.Evaluate(0); }
     else graph.Evaluate(time-Math.Min((f-1)/rate,clip.length));
     w.Write(time);
     foreach(var t in bones) { Vec(w,t.localPosition); Quat(w,t.localRotation); Vec(w,t.localScale); }
    }
    graph.Destroy(); w.Flush();
    Debug.Log("BAKED "+label+" "+name+" duration="+clip.length+" frames="+(frames+1)+" human="+clip.isHumanMotion);
   }
  }
  UnityEngine.Object.DestroyImmediate(go); bundle.Unload(true);
 }
}
