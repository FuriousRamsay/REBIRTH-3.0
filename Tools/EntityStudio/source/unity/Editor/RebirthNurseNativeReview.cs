using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
namespace RebirthEntityStudio {
public static class RebirthNurseNativeReview {
 static readonly string Output="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Tools/EntityStudio/out/NurseNativeCorrection";
 static void Check(bool v,string message){if(!v)throw new InvalidDataException(message);}
 public static void Run(){try{OptimizeLods();Review();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 public static void Finish(){Run();BuildBundle();}
 static void OptimizeLods(){
  var native=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NativeReferences/Nurse/PrefabInstance/ZNurse.prefab").GetComponent<LODGroup>().GetLODs();
  Func<LOD,int> count=l=>l.renderers.OfType<SkinnedMeshRenderer>().Sum(r=>r.sharedMesh.triangles.Length/3);
  var source=AssetDatabase.LoadAssetAtPath<Mesh>(RebirthNurseNativeAssembly.Folder+"/RebirthNurse_LOD0_0.asset");
  // Split UV/normal seam vertices must share skin weights to stay together in motion.
  // This changes weights only; source positions, UVs, normals and triangles remain intact.
  var vertices=source.vertices;var weights=source.boneWeights;
  foreach(var cluster in Enumerable.Range(0,vertices.Length).GroupBy(i=>new Vector3Int(Mathf.RoundToInt(vertices[i].x*100000),Mathf.RoundToInt(vertices[i].y*100000),Mathf.RoundToInt(vertices[i].z*100000))).Where(g=>g.Count()>1)){
   var totals=new Dictionary<int,float>();foreach(int i in cluster){var w=weights[i];int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] values={w.weight0,w.weight1,w.weight2,w.weight3};for(int k=0;k<4;k++){if(!totals.ContainsKey(ids[k]))totals[ids[k]]=0;totals[ids[k]]+=values[k];}}
   var top=totals.OrderByDescending(p=>p.Value).Take(4).ToArray();float sum=top.Sum(p=>p.Value);var blended=new BoneWeight();for(int k=0;k<top.Length;k++){int id=top[k].Key;float value=top[k].Value/sum;if(k==0){blended.boneIndex0=id;blended.weight0=value;}if(k==1){blended.boneIndex1=id;blended.weight1=value;}if(k==2){blended.boneIndex2=id;blended.weight2=value;}if(k==3){blended.boneIndex3=id;blended.weight3=value;}}
   foreach(int i in cluster)weights[i]=blended;
  }
  source.boneWeights=weights;EditorUtility.SetDirty(source);
  var rows=new List<string>();
  for(int i=1;i<3;i++){
   float ratio=(float)count(native[i])/count(native[0]);
   var options=UnityMeshSimplifier.SimplificationOptions.Default;options.EnableSmartLink=true;options.PreserveBorderEdges=i==1;options.PreserveUVSeamEdges=i==1;options.PreserveUVFoldoverEdges=true;
   var simplifier=new UnityMeshSimplifier.MeshSimplifier(source);simplifier.SimplificationOptions=options;simplifier.SimplifyMesh(ratio);var mesh=simplifier.ToMesh();mesh.bindposes=source.bindposes;
   var target=AssetDatabase.LoadAssetAtPath<Mesh>(RebirthNurseNativeAssembly.Folder+"/RebirthNurse_LOD"+i+"_0.asset");string name=target.name;EditorUtility.CopySerialized(mesh,target);target.name=name;EditorUtility.SetDirty(target);
   rows.Add("LOD"+i+" native ratio="+ratio+"; triangles="+target.triangles.Length/3+"; vertices="+target.vertexCount);UnityEngine.Object.DestroyImmediate(mesh);
  }
  AssetDatabase.SaveAssets();File.WriteAllLines(Output+"/LodReduction.txt",rows);
  foreach(string path in new[]{RebirthNurseNativeAssembly.PrefabPath,RebirthNurseNativeAssembly.Folder+"/RebirthNurse_Authoring.prefab"}){
   var prefab=PrefabUtility.LoadPrefabContents(path);foreach(var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))renderer.enabled=true;prefab.GetComponent<Animator>().enabled=true;
   PrefabUtility.SaveAsPrefabAsset(prefab,path);PrefabUtility.UnloadPrefabContents(prefab);
  }
 }
 static void Review(){
  Directory.CreateDirectory(Output+"/Review");var rows=new List<string>();
  var scene=EditorSceneManager.OpenScene(RebirthNurseNativeAssembly.ScenePath);var model=scene.GetRootGameObjects().Single(g=>g.name=="RebirthNurse");
  var native=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NativeReferences/Nurse/PrefabInstance/ZNurse.prefab");var origin=model.transform.Find("Origin");
  var bones=origin.GetComponentsInChildren<Transform>(true);var nativeBones=native.transform.Find("Origin").GetComponentsInChildren<Transform>(true);
  Check(bones.Length==nativeBones.Length,"Full native skeleton node count differs");
  foreach(var source in nativeBones){var target=bones.Single(t=>t.name==source.name);Check(source==native.transform.Find("Origin")||target.parent.name==source.parent.name,"Native parent mismatch "+source.name);}
  var fittedProfile=AssetDatabase.LoadAssetAtPath<RebirthEntityRigProfile>("Assets/EntityStudioGenerated/RebirthNurse/RebirthNurse_RigProfile.asset");
  var xml=System.Xml.Linq.XDocument.Load("C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Config/entityclasses.xml");
  foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{RebirthNurseNativeAssembly.Folder})){
   var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
   foreach(string tier in new[]{"Radiated","Charged","Infernal"})if(mat.name.Contains("_"+tier)&&mat.HasProperty("_EmissiveColor")){
    var values=((string)xml.Descendants("color").Single(e=>(string)e.Attribute("name")==tier.ToLowerInvariant()).Attribute("value")).Split(',').Select(v=>float.Parse(v,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    mat.SetColor("_EmissiveColor",new Color(values[0],values[1],values[2],values[3]));EditorUtility.SetDirty(mat);
   }
  }
  AssetDatabase.SaveAssets();
  foreach(var spec in fittedProfile.bones){var bone=bones.Single(t=>t.name==spec.name);if(spec.name.EndsWith("ForeArm")||spec.name.EndsWith("Hand"))Check(bone.position.y>1.3f,"Rest skeleton dropped away from supplied T-pose: "+bone.name);}
  var colliders=model.GetComponentsInChildren<CapsuleCollider>(true);var joints=model.GetComponentsInChildren<CharacterJoint>(true);
  var head=colliders.Single(c=>c.name=="Head");var chest=colliders.Single(c=>c.name=="Spine2");
  Check(Physics.ComputePenetration(head,head.transform.position,head.transform.rotation,chest,chest.transform.position,chest.transform.rotation,out var direction,out var depth),"Head and chest capsules leave a neck gap");
  Check(colliders.Length==12&&joints.Length==10&&origin.GetComponentsInChildren<Rigidbody>(true).Length==11,"Native physics count mismatch");
  Check(model.GetComponentsInChildren<BoxCollider>(true).Length==0,"Unexpected box collider");Check(model.transform.position==Vector3.zero&&model.transform.rotation==Quaternion.identity&&model.transform.localScale==Vector3.one,"Entity root is not identity");
  foreach(var joint in joints){var source=nativeBones.Single(t=>t.name==joint.name).GetComponent<CharacterJoint>();Check(joint.axis==source.axis&&joint.swingAxis==source.swingAxis,"Native joint axes changed "+joint.name);Check(Vector3.Distance(joint.transform.TransformPoint(joint.anchor),joint.connectedBody.transform.TransformPoint(joint.connectedAnchor))<.0001f,"Disconnected rest joint "+joint.name+" distance="+Vector3.Distance(joint.transform.TransformPoint(joint.anchor),joint.connectedBody.transform.TransformPoint(joint.connectedAnchor))+" anchor="+joint.anchor+" connected="+joint.connectedAnchor);}
  var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);Check(skins.Length==5,"Expected three body LODs and two independent native eye renderers");Check(skins.Count(s=>s.CompareTag("E_BP_Eye"))==2,"Native eye tags missing");Check(skins.All(s=>s.rootBone==origin),"Renderer rootBone must be native Origin");
  var body=skins.Single(s=>s.name=="LOD0");var sourceModel=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.FindAssets("t:Model",new[]{RebirthNurseReplacement.Input}).Select(AssetDatabase.GUIDToAssetPath).Single(p=>p.EndsWith(".fbx")));
  Check(sourceModel.GetComponentsInChildren<MeshFilter>(true).Any(f=>RebirthNurseSourcePreservation.Hash(f.sharedMesh)==RebirthNurseSourcePreservation.Hash(body.sharedMesh)),"Original LOD0 geometry hash mismatch");
  var group=model.GetComponent<LODGroup>();var levels=group.GetLODs();var nativeLevels=native.GetComponent<LODGroup>().GetLODs();Check(levels.Length==nativeLevels.Length,"LOD count mismatch");
  for(int i=0;i<levels.Length;i++)Check(levels[i].screenRelativeTransitionHeight==nativeLevels[i].screenRelativeTransitionHeight,"Native LOD threshold mismatch");
  var animator=model.GetComponent<Animator>();Check(animator.avatar.isValid&&animator.avatar.isHuman&&animator.runtimeAnimatorController!=null,"Native animation setup incomplete");var clips=animator.runtimeAnimatorController.animationClips.Distinct().ToArray();Check(clips.Length==208,"Native clip count differs");
  rows.Add("SkeletonNodes="+bones.Length+"; bodyRenderBones="+body.bones.Length+"; rootBone="+body.rootBone.name);
  rows.Add("NativeHierarchy=PASS; NativeJointAxes=PASS; JointRestAnchors=PASS; OriginalMeshGeometry=PASS; RootIdentity=PASS");
  rows.Add("Capsules="+colliders.Length+" (11 ragdoll + 1 LargeEntityBlocker); Rigidbodies=11; CharacterJoints="+joints.Length);
  rows.Add("Controller="+animator.runtimeAnimatorController.name+"; Clips="+clips.Length+"; Avatar="+animator.avatar.name+" valid="+animator.avatar.isValid);
  foreach(var c in colliders)rows.Add("Collider="+AnimationUtility.CalculateTransformPath(c.transform,model.transform)+" tag="+c.tag+" radius="+c.radius+" height="+c.height+" center="+c.center+" min="+c.bounds.min+" max="+c.bounds.max);
  animator.enabled=true;group.ForceLOD(0);RebirthNurseNativeShaderPreview.Bind(model);
  Render(model,"Rest",false);Render(model,"Skeleton_Colliders",true);
  var start=bones.ToDictionary(b=>b,b=>b.position);var restMesh=new Mesh();body.BakeMesh(restMesh);var restVertices=restMesh.vertices;
  foreach(string category in new[]{"walkForward","melee","Pain"}){
   var clip=clips.FirstOrDefault(c=>c.name.StartsWith(category,StringComparison.OrdinalIgnoreCase)&&c.length>.2f);
   if(clip==null&&category=="death")clip=clips.FirstOrDefault(c=>c.name.IndexOf("dead",StringComparison.OrdinalIgnoreCase)>=0||c.name.IndexOf("knock",StringComparison.OrdinalIgnoreCase)>=0);
   Check(clip!=null,"Native animation category missing "+category);
   AnimationMode.StartAnimationMode();AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(model,clip,clip.length*.4f);AnimationMode.EndSampling();
   float movement=start.Max(p=>Vector3.Distance(p.Key.position,p.Value));Check(movement>.005f,"No actual motion in "+clip.name);
   var baked=new Mesh();body.BakeMesh(baked);Check(baked.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)),"Non-finite animated skin");
   float skinMotion=baked.vertices.Select((v,i)=>Vector3.Distance(v,restVertices[i])).Max();Check(skinMotion>.005f,"Native clip did not deform the skin: "+clip.name);
   rows.Add("NativeAnimation="+clip.name+"; maxBoneMotion="+movement+"; maxSkinMotion="+skinMotion+"; bakedVertices="+baked.vertexCount);Render(model,category,false);UnityEngine.Object.DestroyImmediate(baked);AnimationMode.StopAnimationMode();
  }
  for(int i=0;i<3;i++){group.ForceLOD(i);Render(model,"LOD"+i,false);}group.ForceLOD(-1);
  foreach(var material in skins.SelectMany(s=>PrefabUtility.GetCorrespondingObjectFromSource(s).sharedMaterials).Distinct())rows.Add("Material="+material.name+"; shader="+material.shader.name);
  var profile=AssetDatabase.LoadAssetAtPath<RebirthEntityRigProfile>("Assets/EntityStudioGenerated/RebirthNurse/RebirthNurse_RigProfile.asset");
  var helper=model.AddComponent<RebirthEntityRigWorkspace>();helper.generatedSkeleton=origin;helper.profile=profile;
  model.GetComponentsInChildren<CapsuleCollider>(true).Single(c=>c.CompareTag("LargeEntityBlocker")).enabled=false;
  var temporary=new List<GameObject>();var result=RebirthEntityPhysicsReview.Simulate(helper,temporary);File.WriteAllText(Output+"/Review/Ragdoll.json",JsonUtility.ToJson(result,true));Render(model,"Ragdoll",true);
  File.WriteAllLines(Output+"/Review/NativeConformance.txt",rows);Debug.Log("NURSE_NATIVE_REVIEW_PASSED");
 }
 static void Render(GameObject model,string label,bool overlays){
  // A synchronous batch Camera.Render can reuse the GPU skin buffer from the last
  // editor frame. Render the sampled CPU skin so the captured pose is explicit.
  var group=model.GetComponent<LODGroup>();int level=label.StartsWith("LOD")?int.Parse(label.Substring(3)):0;
  var lodRenderers=group.GetLODs().SelectMany(l=>l.renderers).ToArray();var selected=group.GetLODs()[level].renderers;
  var skinned=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);var bakedObjects=new List<GameObject>();var bakedMeshes=new List<Mesh>();
  foreach(var renderer in skinned){
   if(renderer.gameObject.activeInHierarchy&&renderer.enabled&&(!lodRenderers.Contains(renderer)||selected.Contains(renderer))){
    var mesh=new Mesh();renderer.BakeMesh(mesh);var go=new GameObject("__SampledSkin");go.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);go.transform.localScale=renderer.transform.lossyScale;
    go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;bakedObjects.Add(go);bakedMeshes.Add(mesh);
   }
   renderer.forceRenderingOff=true;
  }
  var camera=new GameObject("__ReviewCamera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.19f);camera.fieldOfView=30;camera.aspect=1;
  camera.transform.position=new Vector3(0,1.05f,4.3f);camera.transform.LookAt(new Vector3(0,.95f,0));camera.nearClipPlane=.03f;
  if(bakedObjects.Count>0){var bounds=bakedObjects[0].GetComponent<Renderer>().bounds;foreach(var go in bakedObjects.Skip(1))bounds.Encapsulate(go.GetComponent<Renderer>().bounds);camera.transform.position=bounds.center+Vector3.forward*Mathf.Max(2.2f,Mathf.Max(bounds.size.y,bounds.size.x)*2.2f);camera.transform.LookAt(bounds.center);}
  Material lines=null;Camera.CameraCallback callback=null;
  if(overlays){lines=new Material(Shader.Find("Hidden/Internal-Colored"));lines.SetInt("_ZTest",(int)UnityEngine.Rendering.CompareFunction.Always);lines.SetInt("_ZWrite",0);lines.SetInt("_Cull",0);
   callback=cam=>{if(cam!=camera)return;lines.SetPass(0);GL.PushMatrix();GL.MultMatrix(Matrix4x4.identity);GL.Begin(GL.LINES);GL.Color(Color.cyan);
    var origin=model.transform.Find("Origin");foreach(var bone in origin.GetComponentsInChildren<Transform>(true))if(bone!=origin){GL.Vertex(bone.position);GL.Vertex(bone.parent.position);}
    GL.Color(new Color(1,.5f,0));foreach(var cap in model.GetComponentsInChildren<CapsuleCollider>(true))DrawCapsule(cap);GL.End();GL.PopMatrix();};Camera.onPostRender+=callback;
  }
  var rt=new RenderTexture(1200,1200,24);rt.Create();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1200,1200,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,1200),0,0);pixels.Apply();File.WriteAllBytes(Output+"/Review/RebirthNurse_"+label+".png",pixels.EncodeToPNG());
  RenderTexture.active=null;camera.targetTexture=null;if(callback!=null)Camera.onPostRender-=callback;UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);if(lines!=null)UnityEngine.Object.DestroyImmediate(lines);
  foreach(var go in bakedObjects)UnityEngine.Object.DestroyImmediate(go);foreach(var mesh in bakedMeshes)UnityEngine.Object.DestroyImmediate(mesh);foreach(var renderer in skinned)renderer.forceRenderingOff=false;
 }
 static void DrawCapsule(CapsuleCollider cap){
  Vector3 axis=cap.direction==0?Vector3.right:cap.direction==1?Vector3.up:Vector3.forward;
  Vector3 u=cap.direction==0?Vector3.up:Vector3.right,v=Vector3.Cross(axis,u);float half=Mathf.Max(0,cap.height*.5f-cap.radius);
  Action<Vector3,Vector3> line=(a,b)=>{GL.Vertex(cap.transform.TransformPoint(cap.center+a));GL.Vertex(cap.transform.TransformPoint(cap.center+b));};
  for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;var p=(u*Mathf.Cos(a)+v*Mathf.Sin(a))*cap.radius;var q=(u*Mathf.Cos(b)+v*Mathf.Sin(b))*cap.radius;line(axis*half+p,axis*half+q);line(-axis*half+p,-axis*half+q);}
  foreach(var radial in new[]{u,-u,v,-v}){line(axis*half+radial*cap.radius,-axis*half+radial*cap.radius);for(int i=0;i<24;i++){float a=i*Mathf.PI/48,b=(i+1)*Mathf.PI/48;line(axis*(half+cap.radius*Mathf.Sin(a))+radial*(cap.radius*Mathf.Cos(a)),axis*(half+cap.radius*Mathf.Sin(b))+radial*(cap.radius*Mathf.Cos(b)));line(-axis*(half+cap.radius*Mathf.Sin(a))+radial*(cap.radius*Mathf.Cos(a)),-axis*(half+cap.radius*Mathf.Sin(b))+radial*(cap.radius*Mathf.Cos(b)));}}
 }
 public static void BuildBundle(){try{
  string dest=Output+"/BuildFinal";var receipt=RebirthEntityBundleBuilder.BuildAndVerify(RebirthNurseNativeAssembly.PrefabPath,dest);
  File.WriteAllText(dest+"/entityclasses.xml",RebirthNurseEntityConfig.BuildPatch(receipt,"C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Config/entityclasses.xml"));
  var bundle=AssetBundle.LoadFromFile(receipt.bundlePath);var textures=new HashSet<Texture2D>(bundle.LoadAllAssets<Texture2D>());
  foreach(var material in bundle.LoadAllAssets<Material>())foreach(string property in material.GetTexturePropertyNames())if(material.GetTexture(property) is Texture2D t)textures.Add(t);
  var rows=new List<string>();foreach(var t in textures){bool large=t.name.ToLowerInvariant()=="color"||t.name.ToLowerInvariant()=="normal"||t.name.ToLowerInvariant().Contains("albedo");int limit=large?2048:512;Check(t.width<=limit&&t.height<=limit,"Texture exceeds cap: "+t.name);rows.Add(t.name+" "+t.width+"x"+t.height);}bundle.Unload(true);File.WriteAllLines(dest+"/TextureAudit.txt",rows);Debug.Log("NURSE_NATIVE_BUNDLE_BUILT");
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
}
