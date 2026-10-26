using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace RebirthEntityStudio {
// Fits the existing mesh in its supplied rest pose. Never edits vertex attributes or topology.
public static class RebirthNurseAnatomyFit {
 public const string Output="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Tools/EntityStudio/out/NurseAnatomyFit";
 const string Folder=RebirthNurseNativeAssembly.Folder;
 static float Q(IEnumerable<float> values,float q){var a=values.OrderBy(x=>x).ToArray();if(a.Length<4)throw new InvalidDataException("Insufficient anatomy samples");return a[Mathf.RoundToInt((a.Length-1)*q)];}
 static Vector3 Mid(IEnumerable<Vector3> vertices){var a=vertices.ToArray();return new Vector3((Q(a.Select(v=>v.x),.1f)+Q(a.Select(v=>v.x),.9f))*.5f,(Q(a.Select(v=>v.y),.1f)+Q(a.Select(v=>v.y),.9f))*.5f,(Q(a.Select(v=>v.z),.1f)+Q(a.Select(v=>v.z),.9f))*.5f);}
 static void Check(bool v,string m){if(!v)throw new InvalidDataException(m);}
 static Vector3[] Vertices(GameObject root){var r=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(x=>x.name=="LOD0");return r.sharedMesh.vertices.Select(r.transform.TransformPoint).ToArray();}
 public static void HeadFinish(){foreach(string path in new[]{Folder+"/RebirthNurse_Authoring.prefab",RebirthNurseNativeAssembly.PrefabPath}){var model=PrefabUtility.LoadPrefabContents(path);var c=model.GetComponentsInChildren<CapsuleCollider>(true).Single(x=>x.name=="Head");c.radius=.105f;c.center=c.transform.InverseTransformPoint(new Vector3(0,1.64f,.012f));PrefabUtility.SaveAsPrefabAsset(model,path);PrefabUtility.UnloadPrefabContents(model);}AssetDatabase.SaveAssets();var scene=EditorSceneManager.OpenScene(RebirthNurseNativeAssembly.ScenePath);EditorSceneManager.SaveScene(scene);global::NurseAnatomyRender.Run();}
 public static void Finish(){Run();global::NurseAnatomyRender.Run();}
 public static void Run(){try{
  Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  string path=Folder+"/RebirthNurse_Authoring.prefab",baseline=Folder+"/RebirthNurse_BeforeAnatomy.prefab";
  if(!File.Exists(baseline))Check(AssetDatabase.CopyAsset(path,baseline),"Cannot preserve original rig");
  var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(baseline));model.name="RebirthNurse";
  var before=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r=>r.name,r=>RebirthNurseSourcePreservation.Hash(r.sharedMesh));
  Apply(model);
  foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))Check(before[r.name]==RebirthNurseSourcePreservation.Hash(r.sharedMesh),"Mesh geometry changed: "+r.name);
  var animator=model.GetComponent<Animator>();animator.enabled=true;
  var authoring=PrefabUtility.SaveAsPrefabAsset(model,path);
  var controller=animator.runtimeAnimatorController;animator.runtimeAnimatorController=null;PrefabUtility.SaveAsPrefabAsset(model,RebirthNurseNativeAssembly.PrefabPath);animator.runtimeAnimatorController=controller;
  UnityEngine.Object.DestroyImmediate(model);AssetDatabase.SaveAssets();
  var scene=EditorSceneManager.OpenScene(RebirthNurseNativeAssembly.ScenePath);var previous=scene.GetRootGameObjects().Single(g=>g.name=="RebirthNurse");UnityEngine.Object.DestroyImmediate(previous);
  var current=(GameObject)PrefabUtility.InstantiatePrefab(authoring);current.name="RebirthNurse";EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  string build=Output+"/Build_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");var receipt=RebirthEntityBundleBuilder.BuildAndVerify(RebirthNurseNativeAssembly.PrefabPath,build);File.WriteAllText(build+"/entityclasses.xml",RebirthNurseEntityConfig.BuildPatch(receipt,"C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Config/entityclasses.xml"));
  Debug.Log("NURSE_ANATOMY_FIT_DONE");
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 public static void Apply(GameObject model){
  Directory.CreateDirectory(Output);model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);model.transform.localScale=Vector3.one;model.GetComponent<Animator>().enabled=false;
  var b=model.transform.Find("Origin").GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);var old=b.ToDictionary(p=>p.Key,p=>p.Value.position);var oldRot=b.ToDictionary(p=>p.Key,p=>p.Value.rotation);
  var native=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NativeReferences/Moe/PrefabInstance/ZMoe.prefab"));native.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
  var nb=native.transform.Find("Origin").GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);var frames=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NativeReferences/Nurse/PrefabInstance/ZNurse.prefab").transform.Find("Origin").GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);var verts=Vertices(model);var nv=Vertices(native);var target=new Dictionary<string,Vector3>(old);var rows=new List<string>();
  Action<string,Vector3> set=(n,p)=>target[n]=p;
  Func<float,float> centreZ=y=>{var slab=verts.Where(v=>Mathf.Abs(v.y-y)<.018f&&Mathf.Abs(v.x)<.105f).ToArray();return (Q(slab.Select(v=>v.z),.08f)+Q(slab.Select(v=>v.z),.92f))*.5f;};
  set("Origin",Vector3.zero);set("Hips",new Vector3(0,.95f,centreZ(.95f)));
  foreach(var p in new[]{new KeyValuePair<string,float>("Spine",1.025f),new KeyValuePair<string,float>("Spine1",1.115f),new KeyValuePair<string,float>("Spine2",1.225f),new KeyValuePair<string,float>("Spine3",1.34f)})set(p.Key,new Vector3(0,p.Value,centreZ(p.Value)));
  set("Neck",new Vector3(0,1.525f,-.045f));set("Head",new Vector3(0,1.625f,-.028f));
  foreach(string side in new[]{"Left","Right"}){
   float sign=side=="Left"?-1:1;
   Func<float,Vector3> arm=x=>{var v=verts.Where(p=>Mathf.Abs(p.x-sign*x)<.018f&&p.y>1.355f&&p.y<1.50f);var c=Mid(v);c.x=sign*x;return c;};
   Func<float,Vector3> leg=y=>{var v=verts.Where(p=>Mathf.Abs(p.y-y)<.016f&&p.x*sign>.015f&&p.x*sign<.27f);var c=Mid(v);c.y=y;return c;};
   set(side+"Arm",arm(.185f));set(side+"ForeArm",arm(.435f));set(side+"Hand",arm(.675f));
   set(side+"Shoulder",new Vector3(sign*.065f,target[side+"Arm"].y+.005f,target["Spine3"].z));
   set(side+"ArmRoll",Vector3.Lerp(target[side+"Arm"],target[side+"ForeArm"],.5f));set(side+"ForeArmRoll",Vector3.Lerp(target[side+"ForeArm"],target[side+"Hand"],.5f));
   set(side+"UpLeg",leg(.895f));set(side+"Leg",leg(.505f));set(side+"Foot",leg(.10f));
   var toe=Mid(verts.Where(p=>p.x*sign>.025f&&p.x*sign<.25f&&p.y<.085f&&p.z>.025f));toe.y=.045f;set(side+"ToeBase",toe);
   foreach(var name in b.Keys.Where(n=>n.StartsWith(side+"Hand")&&n!=side+"Hand")){
    var delta=old[name]-old[side+"Hand"];var p=target[side+"Hand"]+new Vector3(delta.x,delta.y*.7f,delta.z*.65f);
    // Source fingers bend down; place depth from this particular hand, not mirrored guessed Z.
    var samples=verts.Where(v=>Mathf.Abs(v.x-p.x)<.018f&&Mathf.Abs(v.y-p.y)<.035f&&v.x*sign>.68f).ToArray();
    if(samples.Length>8)p.z=Mathf.Clamp(p.z,Q(samples.Select(v=>v.z),.18f),Q(samples.Select(v=>v.z),.82f));set(name,p);
   }
  }
  // Preserve native helper roles, but carry anchors with their new anatomical parent.
  foreach(var t in b.Values)if(t.name.EndsWith("Gore")){
   string n=t.name;string key=n=="HeadGore"?"Neck":n.Contains("LowerArm")?n.Substring(0,n.IndexOf("LowerArm"))+"ForeArm":n.Contains("UpperArm")?n.Substring(0,n.IndexOf("UpperArm"))+"Arm":n.Contains("LowerLeg")?n.Substring(0,n.IndexOf("LowerLeg"))+"Leg":n.Contains("UpperLeg")?n.Substring(0,n.IndexOf("UpperLeg"))+"UpLeg":null;
   if(key!=null)set(n,target[key]);
  }
  foreach(var t in b.Values.Where(t=>t.name.Contains("Brow")||t.name=="Jaw"||t.name=="JawRotate"||t.name=="blood_drip"||t.name.StartsWith("Hair_")))set(t.name,old[t.name]+target["Head"]-old["Head"]);
  foreach(var t in b.Values.Where(t=>t.name.StartsWith("Hair_"))){var p=target[t.name];var slab=verts.Where(v=>Mathf.Abs(v.y-p.y)<.02f&&Mathf.Abs(v.x)<.07f).ToArray();p.z=Q(slab.Select(v=>v.z),.08f)+.014f;target[t.name]=p;}
  foreach(var t in b.Values)t.position=target[t.name];
  Action<Transform,Quaternion> rotate=(t,q)=>{var ps=t.GetComponentsInChildren<Transform>(true).ToDictionary(c=>c,c=>c.position);t.rotation=q;foreach(var p in ps)p.Key.position=p.Value;};
  foreach(var t in b.Values){string n=t.name,end=null;if(n.EndsWith("ForeArm"))end=n.Replace("ForeArm","Hand");else if(n.EndsWith("Arm"))end=n.Replace("Arm","ForeArm");else if(n.EndsWith("ArmRoll"))end=n.Replace("ArmRoll","ForeArm");else if(n.EndsWith("ForeArmRoll"))end=n.Replace("ForeArmRoll","Hand");else if(n.EndsWith("UpLeg"))end=n.Replace("UpLeg","Leg");else if(n.EndsWith("Leg"))end=n.Replace("Leg","Foot");else if(n.EndsWith("Foot"))end=n.Replace("Foot","ToeBase");
   if(end==null&&frames.ContainsKey(n)){var child=frames[n].Cast<Transform>().FirstOrDefault(t=>b.ContainsKey(t.name)&&!t.name.EndsWith("Gore"));if(n.EndsWith("Hand"))end=n+"Middle1";else if(child!=null)end=child.name;} Quaternion q=oldRot[n];if(frames.ContainsKey(n)){q=frames[n].rotation;if(end!=null&&frames.ContainsKey(end))q=Quaternion.FromToRotation(frames[end].position-frames[n].position,target[end]-target[n])*q;}
   if(n=="Hips"||n.StartsWith("Spine")||n=="Neck"||n=="Head")q=Quaternion.identity;
   rotate(t,q);
  }
  Func<string,Vector3[],Vector3,Vector3,float> surface=(name,points,a,z)=>{
   var axis=(z-a).normalized;float length=Vector3.Distance(a,z);float sign=name.StartsWith("Left")?-1:1;
   var ds=new List<float>();foreach(var p in points){float along=Vector3.Dot(p-a,axis)/length;if(along<.18f||along>.82f)continue;
    if(name.Contains("Leg")&&(p.x*sign<.018f||Mathf.Abs(p.x)>.30f))continue; if(name.EndsWith("Arm")&&!name.EndsWith("ForeArm")&&points==verts&&p.y<1.355f)continue;
    if((name=="Hips"||name=="Spine2")&&Mathf.Abs(p.x)>.27f)continue;
    float d=(p-a-axis*(along*length)).magnitude;if(d<.30f)ds.Add(d);
   }return Q(ds,.85f);
  };
  var nativeRenderer=native.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="LOD0");
  Func<string,CapsuleCollider,float> nativeSurface=(name,cap)=>{
   var weights=nativeRenderer.sharedMesh.boneWeights;var points=new List<Vector3>();
   for(int i=0;i<weights.Length;i++){var w=weights[i];int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] ws={w.weight0,w.weight1,w.weight2,w.weight3};float total=0;
    for(int j=0;j<4;j++){var bone=nativeRenderer.bones[ids[j]];if(bone==null)continue;string n=bone.name;bool match=n==name||n==name+"Roll"||(name=="Spine2"&&n.StartsWith("Spine"));if(match)total+=ws[j];}
    if(total>.35f)points.Add(nv[i]);
   }
   Vector3 axis=cap.transform.TransformDirection(cap.direction==0?Vector3.right:Vector3.up),center=cap.transform.TransformPoint(cap.center);
   return surface(name,points.ToArray(),center-axis*cap.height*.5f,center+axis*cap.height*.5f);
  };
  foreach(var c in b["Origin"].GetComponentsInChildren<CapsuleCollider>(true)){
   string name=c.name;var src=nb[name].GetComponent<CapsuleCollider>();Vector3 a,z,na,nz;string end=null;
   if(name.EndsWith("ForeArm"))end=name.Replace("ForeArm","Hand");else if(name.EndsWith("Arm"))end=name.Replace("Arm","ForeArm");else if(name.EndsWith("UpLeg"))end=name.Replace("UpLeg","Leg");else if(name.EndsWith("Leg"))end=name.Replace("Leg","Foot");
   float radius,height;Vector3 center,axis;
   if(end!=null){a=target[name];z=target[end];na=nb[name].position;nz=nb[end].position;
    float sourceSurface=nativeSurface(name,src),targetSurface=surface(name,verts,a,z);float ratio=src.radius/sourceSurface;
    radius=targetSurface*ratio;
    // Terminal forearm includes the palm in the native arrangement; no added hand collider.
    axis=(z-a).normalized;float extension=name.EndsWith("ForeArm")?.065f:.012f; if(name.EndsWith("Leg")&&!name.EndsWith("UpLeg"))extension=Mathf.Max(.012f,src.height*Vector3.Distance(a,z)/Vector3.Distance(na,nz)-Vector3.Distance(a,z)-.018f);
    float proximal=name.EndsWith("Arm")&&!name.EndsWith("ForeArm")?.040f:.018f; height=Vector3.Distance(a,z)+extension+proximal;center=(a+z)*.5f+axis*(extension-proximal)*.5f;
    rows.Add(name+" sourceSurface85="+sourceSurface+" meshSurface85="+targetSurface+" nativeRadiusFraction="+ratio);
   }else if(name=="Hips"||name=="Spine2"){
    bool hips=name=="Hips";a=new Vector3(0,hips?.85f:1.13f,hips?target["Hips"].z:target["Spine2"].z);z=new Vector3(0,hips?1.10f:1.43f,a.z);
    na=nb[name].position+Vector3.up*(hips?-.13f:-.10f);nz=nb[name].position+Vector3.up*(hips?.12f:.20f);
    float sourceSurface=nativeSurface(name,src),targetSurface=surface(name,verts,a,z);radius=targetSurface*src.radius/sourceSurface;
    axis=Vector3.up;center=new Vector3(0,hips?.985f:1.32f,(a.z+z.z)*.5f);height=hips?.435f:.490f;
    rows.Add(name+" sourceSurface85="+sourceSurface+" meshSurface85="+targetSurface+" nativeRadiusFraction="+src.radius/sourceSurface);
   }else{
    // Skull excludes the loose hair silhouette. The native head capsule extends down over the neck.
    axis=Vector3.up;center=new Vector3(0,1.64f,.012f);radius=.105f;height=.325f;
   }
   c.center=c.transform.InverseTransformPoint(center);c.radius=radius;c.height=Mathf.Max(radius*2,height);c.direction=src.direction;
   // Orient bone frames, never add collider-only transforms absent from the native rig.
   var localAxis=c.direction==0?Vector3.right:Vector3.up;if(Vector3.Dot(c.transform.TransformDirection(localAxis),axis)<0)axis=-axis;rotate(c.transform,Quaternion.FromToRotation(c.transform.TransformDirection(localAxis),axis)*c.transform.rotation); c.center=c.transform.InverseTransformPoint(center);
  }
  // One native large movement blocker; keep its baseline clearance while fitting the new body height.
  var blocker=model.GetComponentsInChildren<CapsuleCollider>(true).Single(c=>!c.transform.IsChildOf(b["Origin"]));blocker.radius=.19f;blocker.height=1.40f;blocker.center=new Vector3(0,.90f,-.025f);
  foreach(var joint in model.GetComponentsInChildren<CharacterJoint>(true)){joint.autoConfigureConnectedAnchor=false;joint.anchor=Vector3.zero;joint.connectedAnchor=joint.connectedBody.transform.InverseTransformPoint(joint.transform.position);}
  var fitted=b.ToDictionary(p=>p.Key,p=>p.Value.localPosition);var rotations=b.ToDictionary(p=>p.Key,p=>p.Value.localRotation);
  var animator=model.GetComponent<Animator>();var desc=animator.avatar.humanDescription;desc.skeleton=model.GetComponentsInChildren<Transform>(true).Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
  var avatar=AvatarBuilder.BuildHumanAvatar(model,desc);Check(avatar.isValid&&avatar.isHuman,"Corrected humanoid avatar invalid");string avatarPath=Folder+"/RebirthNurseNativeAvatar.asset";var existing=AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);EditorUtility.CopySerialized(avatar,existing);existing.name="RebirthNurseNativeAvatar";animator.avatar=existing;EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(avatar);
  foreach(var t in b.Values){t.localPosition=fitted[t.name];t.localRotation=rotations[t.name];}
  foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){r.sharedMesh.bindposes=r.bones.Select(t=>t.worldToLocalMatrix*r.transform.localToWorldMatrix).ToArray();EditorUtility.SetDirty(r.sharedMesh);r.localBounds=r.sharedMesh.bounds;}
  if(File.Exists(RebirthNurseDetailFit.Folder+"/NurseFingerLandmarks.json"))RebirthNurseDetailFit.Apply(model);
  Physics.SyncTransforms();
  foreach(var t in b.Values)rows.Add("Bone="+t.name+" from="+old[t.name].ToString("F5")+" to="+t.position.ToString("F5"));
  foreach(var c in model.GetComponentsInChildren<CapsuleCollider>(true))rows.Add("Collider="+c.name+" r="+c.radius+" h="+c.height+" centerWorld="+c.transform.TransformPoint(c.center).ToString("F5")+" axisWorld="+c.transform.TransformDirection(c.direction==0?Vector3.right:Vector3.up).ToString("F5"));
  foreach(var j in model.GetComponentsInChildren<CharacterJoint>(true))Check(Vector3.Distance(j.transform.TransformPoint(j.anchor),j.connectedBody.transform.TransformPoint(j.connectedAnchor))<.0001f,"Disconnected joint: "+j.name);
  Check(b.Count==87&&model.GetComponentsInChildren<CapsuleCollider>(true).Length==12&&model.GetComponentsInChildren<CharacterJoint>(true).Length==10,"Native structure altered");
  File.WriteAllLines(Output+"/fit-measurements.txt",rows);UnityEngine.Object.DestroyImmediate(native);AssetDatabase.SaveAssets();
 }
}
}






