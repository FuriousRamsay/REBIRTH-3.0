using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
namespace RebirthEntityStudio {
public static class RebirthNurseNativeAssembly {
 public const string Folder="Assets/EntityStudioGenerated/RebirthNurseNative";
 public const string ScenePath=Folder+"/RebirthNurse_NativeReview.unity";
 public const string PrefabPath=Folder+"/RebirthNurse.prefab";
 static readonly string Output="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Tools/EntityStudio/out/NurseNativeCorrection";
 static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
 public static void Run(){try{Build();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static Dictionary<Material,Material> CreateMaterials(RebirthEntityRigProfile profile){
  var result=new Dictionary<Material,Material>();
  var template=AssetDatabase.LoadAssetAtPath<Material>("Assets/NativeReferences/Nurse/Material/HD_NurseZ.mat");
  var eyeTemplate=AssetDatabase.LoadAssetAtPath<Material>("Assets/NativeReferences/Nurse/Material/dynamic_feral_eye.mat");
  foreach(var old in profile.generatedTierMaterials.Where(m=>m!=null).Distinct()){
   bool eye=old.name.Contains("FeralEye");string tier=System.Text.RegularExpressions.Regex.Match(old.name,@"_(Normal|Feral|Radiated|Charged|Infernal)(?:_Slot\d+)?(?: \d+)?$").Groups[1].Value;
   var mat=new Material(eye?eyeTemplate:template){name=old.name};
   Texture albedo=old.GetTexture("_MainTex"), normal=old.GetTexture("_BumpMap");
   if(eye){mat.SetTexture("_MainTex",albedo);mat.SetTexture("_EmissionMap",old.GetTexture("_EmissionMap"));mat.SetColor("_EmissionColor",old.GetColor("_EmissionColor"));}
   else{mat.SetTexture("_Albedo",albedo);mat.SetTexture("_Normal",normal);string map=tier=="Normal"?"Normal":tier=="Feral"?"Feral":"Special";mat.SetTexture("_RMOE",profile.nativeRmoeMaps.Single(t=>t.name.EndsWith("_"+map)));}
   if(tier!="Normal"){
    Color color=new Color(1,.55f,.08f,1);
    if(tier!="Feral"){
     var xml=System.Xml.Linq.XDocument.Load("C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Config/entityclasses.xml");
     var parts=((string)xml.Descendants("color").Single(e=>(string)e.Attribute("name")==tier.ToLowerInvariant()).Attribute("value")).Split(',').Select(v=>float.Parse(v,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
     color=new Color(parts[0],parts[1],parts[2],parts[3]);
    }
    if(!eye){mat.SetColor("_EmissiveColor",color);mat.EnableKeyword("_EMISSION");if(tier!="Feral")mat.EnableKeyword("_IRRADIATED_ON");}
   }
   RebirthNurseSkinMaterials.UseCorrectedMaps(mat,tier);
   AssetDatabase.CreateAsset(mat,Folder+"/"+mat.name+".mat");result.Add(old,mat);
  }
  return result;
 }
 static void Build(){
  if(AssetDatabase.IsValidFolder(Folder))AssetDatabase.DeleteAsset(Folder); Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
  var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthNurse/RebirthNurse_RigReview.unity");
  var ws=scene.GetRootGameObjects().Select(g=>g.GetComponent<RebirthEntityRigWorkspace>()).Single(w=>w!=null);
  RebirthEntityRigBuilder.BindWorkspaceReferences(ws);
  var old=ws.generatedSkeleton.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
  var reference=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NativeReferences/Nurse/PrefabInstance/ZNurse.prefab");Require(reference!=null,"Native Nurse prefab missing");
  var model=UnityEngine.Object.Instantiate(reference);model.name="RebirthNurse";model.GetComponent<Animator>().enabled=false;
  model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);model.transform.localScale=Vector3.one;model.tag="E_Enemy";
  var origin=model.transform.Find("Origin");Require(origin!=null,"Native Origin missing");
  var sourceBones=reference.transform.Find("Origin").GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);
  var bones=origin.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);
  var nativeRot=bones.ToDictionary(p=>p.Key,p=>p.Value.rotation);
  foreach(var t in origin.GetComponentsInChildren<Transform>(true)){
   Require(old.ContainsKey(t.name),"Native node absent from mapping: "+t.name);
   t.position=ws.transform.InverseTransformPoint(old[t.name].position); t.gameObject.tag=old[t.name].tag;
  }
  // Retain native roll/axis conventions; align the native segment to this model's anatomy.
  foreach(var t in origin.GetComponentsInChildren<Transform>(true)){
   var src=sourceBones[t.name];var child=src.Cast<Transform>().FirstOrDefault(c=>bones.ContainsKey(c.name)&&!c.name.EndsWith("Gore")&&!c.name.Contains("Roll"));
   string end=t.name.EndsWith("ForeArm")?t.name.Replace("ForeArm","Hand"):t.name.EndsWith("Arm")?t.name.Replace("Arm","ForeArm"):t.name.EndsWith("UpLeg")?t.name.Replace("UpLeg","Leg"):t.name.EndsWith("Leg")?t.name.Replace("Leg","Foot"):null;
   if(end!=null&&sourceBones.ContainsKey(end))child=sourceBones[end];
   Quaternion q=nativeRot[t.name];
   if(child!=null){var a=child.position-src.position;var b=bones[child.name].position-t.position;if(a.sqrMagnitude>1e-8f&&b.sqrMagnitude>1e-8f)q=Quaternion.FromToRotation(a,b)*q;}
   var positions=t.GetComponentsInChildren<Transform>(true).ToDictionary(x=>x,x=>x.position);t.rotation=q;foreach(var pair in positions)pair.Key.position=pair.Value;
  }
  foreach(var rb in origin.GetComponentsInChildren<Rigidbody>(true))rb.isKinematic=true;
  foreach(var cap in origin.GetComponentsInChildren<CapsuleCollider>(true)){
   var fit=old[cap.name].GetComponent<CapsuleCollider>();Require(fit!=null,"Missing fitted capsule "+cap.name);
   Vector3 centre=ws.transform.InverseTransformPoint(fit.transform.TransformPoint(fit.center));
   float across=Mathf.Max(Mathf.Abs(fit.transform.lossyScale[(fit.direction+1)%3]),Mathf.Abs(fit.transform.lossyScale[(fit.direction+2)%3]));
   float axisScale=Mathf.Abs(cap.transform.lossyScale[cap.direction]);float crossScale=Mathf.Max(Mathf.Abs(cap.transform.lossyScale[(cap.direction+1)%3]),Mathf.Abs(cap.transform.lossyScale[(cap.direction+2)%3]));
   cap.center=cap.transform.InverseTransformPoint(centre);cap.radius=fit.radius*across/crossScale;
   cap.height=Mathf.Max(2*cap.radius,fit.height*Mathf.Abs(fit.transform.lossyScale[fit.direction])/axisScale);
  }
  foreach(var joint in origin.GetComponentsInChildren<CharacterJoint>(true)){
   joint.autoConfigureConnectedAnchor=false;
   var src=sourceBones[joint.name].GetComponent<CharacterJoint>();
   joint.axis=src.axis;joint.swingAxis=src.swingAxis;
   joint.anchor=Vector3.zero;joint.connectedAnchor=joint.connectedBody.transform.InverseTransformPoint(joint.transform.position);
  }
  // Remove native geometry only; its skeleton, native physics and blocker remain.
  foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))UnityEngine.Object.DestroyImmediate(renderer.gameObject);
  foreach(var particle in model.GetComponentsInChildren<ParticleSystem>(true)){var render=particle.GetComponent<ParticleSystemRenderer>();if(render!=null)UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(particle);}
  foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))if(script!=null)UnityEngine.Object.DestroyImmediate(script);
  foreach(var node in model.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
  var blocker=model.GetComponentsInChildren<CapsuleCollider>(true).Single(c=>!c.transform.IsChildOf(origin));
  blocker.gameObject.tag="LargeEntityBlocker";
  var lods=ws.lodGroup.GetLODs();var bodyLevels=new List<Renderer[]>();
  for(int level=0;level<3;level++){
   var list=new List<Renderer>();int index=0;
   foreach(var oldRenderer in lods[level].renderers.OfType<SkinnedMeshRenderer>().Where(r=>r.name.IndexOf("Eye",StringComparison.OrdinalIgnoreCase)<0)){
    var go=new GameObject(index==0?"LOD"+level:"LOD"+level+"_"+index);go.transform.SetParent(model.transform,false);
    go.transform.SetPositionAndRotation(ws.transform.InverseTransformPoint(oldRenderer.transform.position),Quaternion.Inverse(ws.transform.rotation)*oldRenderer.transform.rotation);go.transform.localScale=oldRenderer.transform.lossyScale;
    var renderer=go.AddComponent<SkinnedMeshRenderer>();EditorUtility.CopySerialized(oldRenderer,renderer);
    renderer.bones=oldRenderer.bones.Select(b=>bones[b.name]).ToArray();renderer.rootBone=origin;
    var mesh=UnityEngine.Object.Instantiate(oldRenderer.sharedMesh);mesh.name="RebirthNurse_LOD"+level+"_"+index;
    mesh.bindposes=renderer.bones.Select(b=>b.worldToLocalMatrix*go.transform.localToWorldMatrix).ToArray();
    if(level==0&&index==0)Require(RebirthNurseSourcePreservation.Hash(mesh)==RebirthNurseSourcePreservation.Hash(oldRenderer.sharedMesh),"LOD0 geometry changed");
    AssetDatabase.CreateAsset(mesh,Folder+"/"+mesh.name+".asset");renderer.sharedMesh=mesh;renderer.updateWhenOffscreen=true;
    renderer.localBounds=oldRenderer.localBounds;renderer.enabled=true;go.tag="LOD";list.Add(renderer);index++;
   }
   bodyLevels.Add(list.ToArray());
  }
  foreach(var eye in ws.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.IndexOf("Eye",StringComparison.OrdinalIgnoreCase)>=0).GroupBy(r=>r.name).Select(g=>g.First())){
   var go=UnityEngine.Object.Instantiate(eye.gameObject,model.transform);go.name=eye.name.Contains("Feral")?"FeralEye":"NormalEye";go.tag="E_BP_Eye";go.SetActive(go.name=="NormalEye");
   go.transform.SetPositionAndRotation(ws.transform.InverseTransformPoint(eye.transform.position),Quaternion.Inverse(ws.transform.rotation)*eye.transform.rotation);go.transform.localScale=eye.transform.lossyScale;
   var renderer=go.GetComponent<SkinnedMeshRenderer>();renderer.bones=eye.bones.Select(b=>bones[b.name]).ToArray();renderer.rootBone=origin;
   var mesh=UnityEngine.Object.Instantiate(eye.sharedMesh);mesh.name=go.name+"_NativeBind";mesh.bindposes=renderer.bones.Select(b=>b.worldToLocalMatrix*go.transform.localToWorldMatrix).ToArray();AssetDatabase.CreateAsset(mesh,Folder+"/"+mesh.name+".asset");renderer.sharedMesh=mesh;
  }
  var group=model.GetComponent<LODGroup>();var nativeLevels=reference.GetComponent<LODGroup>().GetLODs();Require(nativeLevels.Length==3,"Native Nurse LOD count changed");
  group.SetLODs(Enumerable.Range(0,3).Select(i=>new LOD(nativeLevels[i].screenRelativeTransitionHeight,bodyLevels[i])).ToArray());group.RecalculateBounds();
  var fittedRotations=bones.ToDictionary(p=>p.Key,p=>p.Value.rotation);
  var animator=model.GetComponent<Animator>();var description=reference.GetComponent<Animator>().avatar.humanDescription;
  description.skeleton=model.GetComponentsInChildren<Transform>(true).Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
  var avatar=AvatarBuilder.BuildHumanAvatar(model,description);avatar.name="RebirthNurseNativeAvatar";Require(avatar.isValid&&avatar.isHuman,"Native-derived humanoid avatar invalid");AssetDatabase.CreateAsset(avatar,Folder+"/RebirthNurseNativeAvatar.asset");animator.avatar=avatar;animator.applyRootMotion=true;
  foreach(var t in origin.GetComponentsInChildren<Transform>(true)){t.rotation=fittedRotations[t.name];t.position=ws.transform.InverseTransformPoint(old[t.name].position);}
  Debug.Log("NATIVE_FITTED_POSE LeftArm="+bones["LeftArm"].position+" LeftForeArm="+bones["LeftForeArm"].position+" LeftHand="+bones["LeftHand"].position);
  var head=bones["Head"].GetComponent<CapsuleCollider>();Vector3 headAxis=head.transform.TransformDirection(head.direction==0?Vector3.right:head.direction==1?Vector3.up:Vector3.forward);float headRadius=head.radius*Mathf.Max(Mathf.Abs(head.transform.lossyScale[(head.direction+1)%3]),Mathf.Abs(head.transform.lossyScale[(head.direction+2)%3]));float headScale=Mathf.Abs(head.transform.lossyScale[head.direction]);Vector3 headCenter=head.transform.TransformPoint(head.center);float low=headCenter.y-Mathf.Abs(headAxis.y)*(head.height*headScale*.5f-headRadius)-headRadius;float missing=low-(bones["Neck"].position.y-.008f);if(missing>0&&Mathf.Abs(headAxis.y)>.7f){float extra=missing/Mathf.Abs(headAxis.y);head.height+=extra/headScale;head.center-=head.transform.InverseTransformVector(headAxis*Mathf.Sign(headAxis.y)*extra*.5f);}
  Physics.SyncTransforms();
  var chest=bones["Spine2"].GetComponent<CapsuleCollider>();for(int fit=0;fit<60&&!Physics.ComputePenetration(head,head.transform.position,head.transform.rotation,chest,chest.transform.position,chest.transform.rotation,out var overlapDir,out var overlapDepth);fit++){head.height+=.002f/headScale;head.center-=head.transform.InverseTransformVector(headAxis*Mathf.Sign(headAxis.y)*.001f);Physics.SyncTransforms();}
  Require(Physics.ComputePenetration(head,head.transform.position,head.transform.rotation,chest,chest.transform.position,chest.transform.rotation,out var checkDir,out var checkDepth),"Cannot join native head/chest capsules through neck");
  foreach(var joint in model.GetComponentsInChildren<CharacterJoint>(true)){joint.autoConfigureConnectedAnchor=false;joint.anchor=Vector3.zero;joint.connectedAnchor=joint.connectedBody.transform.InverseTransformPoint(joint.transform.position);Debug.Log("NATIVE_JOINT_REST "+joint.name+" separation="+Vector3.Distance(joint.transform.TransformPoint(joint.anchor),joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)));}
  foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){renderer.sharedMesh.bindposes=renderer.bones.Select(b=>b.worldToLocalMatrix*renderer.transform.localToWorldMatrix).ToArray();EditorUtility.SetDirty(renderer.sharedMesh);}
  Require(animator.runtimeAnimatorController!=null&&animator.runtimeAnimatorController.animationClips.Length>0,"Native controller unavailable");
  RebirthNurseAnatomyFit.Apply(model);
  var materialMap=CreateMaterials(ws.profile);
  foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materialMap[m]).ToArray();
  var authoring=PrefabUtility.SaveAsPrefabAsset(model,Folder+"/RebirthNurse_Authoring.prefab");
  // Native controller and callbacks are bound from the installed game by the existing runtime adapter.
  // Reference-only Editor stubs and recovered game clips must not ship in our owned asset bundle.
  animator.runtimeAnimatorController=null;animator.enabled=true;
  PrefabUtility.SaveAsPrefabAsset(model,PrefabPath);
  string meshFolder=Folder+"/RebirthNurse_Meshes";Directory.CreateDirectory(meshFolder);
  string tags=meshFolder+"/RebirthNurse_Tags.json";
  File.WriteAllText(tags,JsonUtility.ToJson(new RebirthEntityTagMap{entries=model.GetComponentsInChildren<Transform>(true).Select(t=>new RebirthEntityTagEntry{path=AnimationUtility.CalculateTransformPath(t,model.transform),tag=t.tag}).ToArray()},true));
  File.WriteAllText(meshFolder+"/entity.json",JsonUtility.ToJson(new RebirthEntityPrefabManifest{entityClass="RebirthNurse",targetVersion="3.3.0 b18 reference / 3.0 b259 target",gameTestCandidate=true,pendingFindings=new[]{"Exact 3.0 b259 compatibility not verified"},colliderModes=ws.profile.colliderModes.ToArray(),tierMaterialAssetPaths=materialMap.Values.Select(AssetDatabase.GetAssetPath).Distinct().ToArray(),nativeTextureAssetPaths=File.Exists(RebirthNurseSkinMaterials.Folder+"/RebirthNurse_RMOE_Normal.png")?RebirthNurseSkinMaterials.MapPaths():ws.profile.nativeRmoeMaps.Where(t=>t!=null).Select(AssetDatabase.GetAssetPath).Distinct().ToArray(),tagMapAssetPath=tags},true));
  AssetDatabase.Refresh();AssetDatabase.SaveAssets();
  var cleanScene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var instance=(GameObject)PrefabUtility.InstantiatePrefab(authoring);instance.name="RebirthNurse";
  var lighting=new GameObject("Scene Lighting");lighting.tag="EditorOnly";
  foreach(var pair in new[]{new Vector2(35,145),new Vector2(20,-35)}){var light=new GameObject("Directional Light").AddComponent<Light>();light.transform.SetParent(lighting.transform);light.type=LightType.Directional;light.intensity=pair.y==145?1.05f:.6f;light.transform.rotation=Quaternion.Euler(pair.x,pair.y,0);}
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.42f,.42f,.42f);
  EditorSceneManager.SaveScene(cleanScene,ScenePath);
  File.WriteAllText(Output+"/assembly-built.txt","Native-derived authoring prefab and clean game prefab built. Native Nurse LOD thresholds: "+string.Join(",",nativeLevels.Select(l=>l.screenRelativeTransitionHeight)));
  Debug.Log("NURSE_NATIVE_ASSEMBLY_BUILT");
 }
}
}

