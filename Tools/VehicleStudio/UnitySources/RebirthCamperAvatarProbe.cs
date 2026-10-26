using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Asset inspection in an unsaved batch scene; no Addressables/game initialization.
public static class RebirthCamperAvatarProbe
{
    [Serializable] public class RigBone { public string path; public Vector3 position,scale; public Quaternion rotation; }
    [Serializable] public class RigHierarchy { public RigBone[] bones; }
    [Serializable] public class Catalog { public string[] m_InternalIds,m_ProviderIds,m_InternalIdPrefixes; public string m_BucketDataString,m_EntryDataString; }
    [Serializable] public class PoseEvidence { public int requestedPose,stateHash; public string[] sampledClips; public string image; public Vector3 hips,leftKnee,leftFoot,rightKnee,rightFoot; }
    [Serializable] public class AssetEvidence { public string path,type; public string[] children,animatorParameters,controllerStates; public int meshes,bones,missingScripts; public bool avatarValid,avatarHuman,rigBound; public PoseEvidence[] poses; }
    [Serializable] public class Report { public string status="FAIL",error,scope="Read-only native player asset probe, not game or six-occupant fit validation"; public string[] bundles,images,logWarnings; public AssetEvidence[] assets; }
    static readonly string[] Targets={
        "Assets/AssetBundles/Player/Common/BaseRigs/baseRigPrefab.prefab",
        "Assets/AssetBundles/Player/Common/AnimControllers/3PPlayerMaleController.controller",
        "Assets/AssetBundles/Player/Male/Common/Meshes/playerMale.fbx",
        "Assets/AssetBundles/Player/Female/Common/Meshes/playerFemale.fbx"};
    public static void RunBatch()
    {
        string game=Arg("-rebirthAvatarGameRoot","C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die");
        string output=Arg("-rebirthAvatarReport","E:/_Haven/shared/REBIRTHTemp/CamperAvatarProbe");
        Directory.CreateDirectory(output);var report=new Report();var bundles=new List<AssetBundle>();var owned=new List<UnityEngine.Object>();var warnings=new List<string>();
        Application.LogCallback capture=(message,stack,type)=>{if(type==LogType.Warning || type==LogType.Error || type==LogType.Exception){if(warnings.Count<60)warnings.Add(message);}};
        Application.logMessageReceived+=capture;
        try
        {
            if(!Application.isBatchMode || Application.isPlaying)throw new Exception("Dedicated non-playing Unity batch required.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(game,"7DaysToDie_Data/StreamingAssets/aa/catalog.json")));
            if(catalog.m_InternalIdPrefixes!=null && catalog.m_InternalIdPrefixes.Length!=0)throw new Exception("Compressed catalog IDs unsupported: fail closed before loading bundles.");
            var entry=Convert.FromBase64String(catalog.m_EntryDataString);var bucket=Convert.FromBase64String(catalog.m_BucketDataString);
            int count=I(entry,0),bucketCount=I(bucket,0),cursor=4;var buckets=new int[bucketCount][];
            for(int b=0;b<bucketCount;b++){cursor+=4;int size=I(bucket,cursor);cursor+=4;if(size<0 || size>count)throw new Exception("Invalid catalog bucket");buckets[b]=new int[size];for(int n=0;n<size;n++){buckets[b][n]=I(bucket,cursor);cursor+=4;}}
            var order=new List<int>();var visited=new HashSet<int>();
            Action<int> visit=null;visit=index=>{if(index<0 || index>=count)throw new Exception("Catalog entry out of range");if(!visited.Add(index))return;int dep=I(entry,4+28*index+8);if(dep>=0){if(dep>=buckets.Length)throw new Exception("Invalid dependency bucket");foreach(int child in buckets[dep])visit(child);}order.Add(index);};
            var closureTargets=Targets.AsEnumerable();
            if(Arg("-rebirthStandardVehicleFit","0")=="1" || !string.IsNullOrEmpty(Arg("-rebirthAvatarCamperPrefab",""))) closureTargets=closureTargets.Concat(new[]{
                "Assets/AssetBundles/Player/Male/Heads/White/01/Meshes/playerMaleWhite01.fbx",
                "Assets/AssetBundles/Player/Female/Heads/White/01/Meshes/playerFemaleWhite01.fbx"});
            foreach(string target in closureTargets)
            {bool found=false;for(int n=0;n<count;n++)if(string.Equals(catalog.m_InternalIds[I(entry,4+28*n)],target,StringComparison.OrdinalIgnoreCase)){visit(n);found=true;}if(!found)throw new Exception("Catalog target missing: "+target);}
            var paths=order.Select(n=>catalog.m_InternalIds[I(entry,4+28*n)]).Where(p=>p.EndsWith(".bundle",StringComparison.OrdinalIgnoreCase))
                .Select(p=>p.Replace("{AddressableFolders.RootDir}",game).Replace("{BundleTags.Tag}","").Replace('\\',Path.DirectorySeparatorChar)).Distinct().ToArray();
            if(paths.Length==0 || paths.Length>64)throw new Exception("Unexpected dependency closure size: "+paths.Length);
            report.bundles=paths;
            foreach(string path in paths){if(!File.Exists(path))throw new Exception("Dependency absent: "+path);var bundle=AssetBundle.LoadFromFile(path);if(bundle==null)throw new Exception("Unity cannot load native bundle: "+path);bundles.Add(bundle);}
            var assets=new List<AssetEvidence>();var images=new List<string>();RuntimeAnimatorController controller=null;GameObject rigSource=null;GameObject maleBound=null,femaleBound=null;
            foreach(string target in Targets)
            {
                var bundle=bundles.FirstOrDefault(b=>b.GetAllAssetNames().Any(n=>string.Equals(n,target,StringComparison.OrdinalIgnoreCase)));
                if(bundle==null)throw new Exception("Loaded closure does not contain "+target);
                string exact=bundle.GetAllAssetNames().First(n=>string.Equals(n,target,StringComparison.OrdinalIgnoreCase));
                var asset=bundle.LoadAsset<UnityEngine.Object>(exact);if(asset==null)throw new Exception("Asset failed to deserialize: "+exact);
                var evidence=new AssetEvidence{path=exact,type=asset.GetType().FullName};assets.Add(evidence);report.assets=assets.ToArray();
                if(asset is RuntimeAnimatorController){controller=(RuntimeAnimatorController)asset;evidence.children=controller.animationClips.Select(c=>c.name).ToArray();var ac=asset as AnimatorController;if(ac!=null){var states=new List<string>();foreach(var layer in ac.layers)States(layer.stateMachine,layer.name,states);evidence.controllerStates=states.ToArray();}continue;}
                var prefab=asset as GameObject;if(prefab==null)continue;
                if(target.EndsWith("baseRigPrefab.prefab",StringComparison.OrdinalIgnoreCase))rigSource=prefab;
                var instance=UnityEngine.Object.Instantiate(prefab);owned.Add(instance);instance.name="NativeAssetProbe_"+Path.GetFileNameWithoutExtension(target);
                evidence.children=instance.GetComponentsInChildren<Transform>(true).Select(t=>t.name).ToArray();
                if(target.EndsWith("baseRigPrefab.prefab",StringComparison.OrdinalIgnoreCase))
                    File.WriteAllText(Path.Combine(output,"rig_hierarchy.json"),JsonUtility.ToJson(new RigHierarchy{bones=instance.GetComponentsInChildren<Transform>(true).Select(t=>new RigBone{path=AnimationUtility.CalculateTransformPath(t,instance.transform),position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray()},true));
                evidence.missingScripts=instance.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                // Disable present behavior components; unresolved game scripts are recorded.
                foreach(var behavior in instance.GetComponentsInChildren<MonoBehaviour>(true))if(behavior!=null)behavior.enabled=false;
                var renderers=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);evidence.meshes=renderers.Count(r=>r.sharedMesh!=null);evidence.bones=renderers.Sum(r=>r.bones.Count(b=>b!=null));
                if(evidence.meshes>0 && rigSource!=null && controller!=null)
                {
                    instance=BindRealSkin(instance,rigSource,controller,owned);evidence.rigBound=true;
                    renderers=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    evidence.children=instance.GetComponentsInChildren<Transform>(true).Select(t=>t.name).ToArray();
                    evidence.missingScripts=instance.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                }
                var animator=instance.GetComponentInChildren<Animator>(true);
                if(animator!=null){evidence.avatarValid=animator.avatar!=null&&animator.avatar.isValid;evidence.avatarHuman=animator.avatar!=null&&animator.avatar.isHuman;if(controller!=null)animator.runtimeAnimatorController=controller;evidence.animatorParameters=animator.parameters.Select(p=>p.name+":"+p.type).ToArray();}
                if(evidence.meshes>0 && evidence.bones>0)
                {
                    if(Arg("-rebirthSeatTargetAuthoring","0")=="1")
                    {
                        if(evidence.rigBound){if(target.EndsWith("playerMale.fbx",StringComparison.OrdinalIgnoreCase))maleBound=instance;else if(target.EndsWith("playerFemale.fbx",StringComparison.OrdinalIgnoreCase))femaleBound=instance;}
                        instance.SetActive(false);
                        continue;
                    }
                    // Rendering actual native skin, never a proxy. Instanced Standard
                    // materials are a geometry visibility aid, not native appearance proof.
                    foreach(var renderer in renderers){var originals=renderer.sharedMaterials;var visible=new Material[originals.Length];for(int m=0;m<visible.Length;m++){visible[m]=new Material(Shader.Find("Standard"));owned.Add(visible[m]);visible[m].color=new Color(.63f,.51f,.43f);}renderer.sharedMaterials=visible;}
                    Capture(instance,output,Path.GetFileNameWithoutExtension(target)+"_native_mesh",images,owned);
                    if(animator!=null && evidence.avatarValid && controller!=null)
                    {
                        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;animator.Rebind();animator.Update(0);
                        var pose=animator.parameters.FirstOrDefault(p=>p.type==AnimatorControllerParameterType.Int && p.name.Equals("VehiclePose",StringComparison.OrdinalIgnoreCase));
                        if(pose!=null)
                        {
                            var proofs=new List<PoseEvidence>();foreach(int id in NativeVehiclePoses(game))
                            {
                                animator.Rebind();animator.Update(0);
                                animator.SetInteger(pose.name,id);
                                var nativeBuilder=Arg("-rebirthAvatarNativeRigging","0")=="1"?PrepareNativeRigging(instance):null;
                                var previewGraph=default(PlayableGraph);var previewController=default(AnimatorControllerPlayable);
                                if(nativeBuilder!=null)
                                {
                                    ((Behaviour)nativeBuilder).enabled=true;
                                    nativeBuilder.GetType().GetMethod("Clear").Invoke(nativeBuilder,null);
                                    nativeBuilder.GetType().GetMethod("StartPreview").Invoke(nativeBuilder,null);
                                    previewGraph=PlayableGraph.Create("NativeVehiclePosePreview");previewGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                                    previewController=AnimatorControllerPlayable.Create(previewGraph,controller);
                                    var chain=(Playable)nativeBuilder.GetType().GetMethod("BuildPreviewGraph").Invoke(nativeBuilder,new object[]{previewGraph,(Playable)previewController});
                                    if(chain.Equals((Playable)previewController))throw new Exception("Native preview returned controller unchanged; rig constraints were not connected");
                                    var animationOutput=AnimationPlayableOutput.Create(previewGraph,"NativePoseAndRig",animator);animationOutput.SetSourcePlayable(chain);
                                    previewController.SetInteger(pose.name,id);previewGraph.Play();
                                }
                                // Building the graph rebinds the Animator. Set the
                                // parameter afterwards and advance its controller
                                // before evaluating the rig's previous-input stream.
                                animator.SetInteger(pose.name,id);
                                for(int step=0;step<60;step++)
                                {
                                    if(nativeBuilder==null)animator.Update(1f/30f);
                                    else{nativeBuilder.GetType().GetMethod("UpdatePreviewGraph").Invoke(nativeBuilder,new object[]{previewGraph});previewGraph.Evaluate(1f/30f);}
                                }
                                string label=Path.GetFileNameWithoutExtension(target)+"_vehicle_pose_"+id;Capture(instance,output,label,images,owned);
                                if(nativeBuilder!=null)
                                {
                                    var diagnostics=new System.Text.StringBuilder();
                                    foreach(var component in instance.GetComponentsInChildren<Component>(true))
                                        if(component!=null && component.GetType().Namespace=="UnityEngine.Animations.Rigging")
                                        {
                                            diagnostics.AppendLine(component.GetType().FullName+" "+AnimationUtility.CalculateTransformPath(component.transform,instance.transform)+"\n"+EditorJsonUtility.ToJson(component,true));
                                            var serialized=new SerializedObject(component);var iterator=serialized.GetIterator();
                                            while(iterator.Next(true))if(iterator.propertyType==SerializedPropertyType.ObjectReference)
                                                diagnostics.AppendLine("REFERENCE "+iterator.propertyPath+" = "+(iterator.objectReferenceValue==null?"NULL":iterator.objectReferenceValue.name+"#"+iterator.objectReferenceValue.GetInstanceID()));
                                        }
                                    foreach(var transform in instance.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Hand")||t.name.Contains("Foot")||t.name.Contains("IK")))
                                        diagnostics.AppendLine(AnimationUtility.CalculateTransformPath(transform,instance.transform)+" local="+transform.localPosition+" world="+transform.position);
                                    File.WriteAllText(Path.Combine(output,label+"_rig.txt"),diagnostics.ToString());
                                }
                                var clips=new List<string>();for(int layer=0;layer<(nativeBuilder==null?animator.layerCount:previewController.GetLayerCount());layer++)clips.AddRange((nativeBuilder==null?animator.GetCurrentAnimatorClipInfo(layer):previewController.GetCurrentAnimatorClipInfo(layer)).Select(info=>info.clip.name+" weight="+info.weight));
                                proofs.Add(new PoseEvidence{requestedPose=id,stateHash=animator.GetCurrentAnimatorStateInfo(0).fullPathHash,sampledClips=clips.ToArray(),image=Path.Combine(output,label+".png"),hips=Bone(instance,"Hips"),leftKnee=Bone(instance,"LeftLeg"),leftFoot=Bone(instance,"LeftFoot"),rightKnee=Bone(instance,"RightLeg"),rightFoot=Bone(instance,"RightFoot")});
                                if(nativeBuilder!=null){nativeBuilder.GetType().GetMethod("StopPreview").Invoke(nativeBuilder,null);previewGraph.Destroy();}
                            }
                            evidence.poses=proofs.ToArray();
                        }
                        if(Arg("-rebirthAvatarBedReview","0")=="1")
                        {
                            var authoredRoot=new GameObject("BedAuthoringReview");owned.Add(authoredRoot);
                            var holder=new GameObject("M");holder.transform.SetParent(authoredRoot.transform,false);
                            string folder="Assets/REBIRTHCamperBedReviewGenerated/"+Guid.NewGuid().ToString("N");
                            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
                            // The authoring helper lives in a separate editor assembly.
                            var author=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RebirthCamperBedPoseAuthoring",false)).FirstOrDefault(t=>t!=null);
                            if(author==null)throw new Exception("Native bed authoring editor assembly unavailable.");
                            var bedClip=(AnimationClip)author.GetMethod("Apply",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{authoredRoot,folder});
                            var graph=PlayableGraph.Create("NativeGenericBedReview");
                            try
                            {
                                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                                var playable=AnimationClipPlayable.Create(graph,bedClip);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);playable.SetTime(.5);playable.SetSpeed(0);
                                var animationOutput=AnimationPlayableOutput.Create(graph,"LyingBody",animator);animationOutput.SetSourcePlayable(playable);graph.Play();graph.Evaluate(0);
                                Capture(instance,output,Path.GetFileNameWithoutExtension(target)+"_generic_bed",images,owned);
                            }
                            finally{graph.Destroy();}
                        }
                    }
                }
                if(evidence.rigBound){if(target.EndsWith("playerMale.fbx",StringComparison.OrdinalIgnoreCase))maleBound=instance;else if(target.EndsWith("playerFemale.fbx",StringComparison.OrdinalIgnoreCase))femaleBound=instance;}
                instance.SetActive(false);
            }
            string camperPrefab=Arg("-rebirthAvatarCamperPrefab","");
            if(Arg("-rebirthSeatTargetAuthoring","0")=="1")
            {
                if(!maleBound)throw new Exception("Native body missing for contact targets");
                if(Arg("-rebirthTorsoOnly","0")=="1") CheckTorso(maleBound,output);
                else if(Arg("-rebirthSeatPelvisUpgrade","0")=="1") RebirthVehicleSeatUpgrade.Author(maleBound);
                else RebirthVehicleSeatTargets.Author(maleBound);
            }
            if(Arg("-rebirthStandardVehicleFit","0")=="1")
            {
                if(maleBound==null || femaleBound==null)throw new Exception("Native bodies missing for standard vehicle fit.");
                var standard=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RebirthStandardVehicleSeatingReview",false)).FirstOrDefault(t=>t!=null);
                if(standard==null)throw new Exception("Standard vehicle seating helper missing.");
                standard.GetMethod("Review").Invoke(null,new object[]{maleBound,femaleBound,Path.Combine(output,"standard_fit")});
            }
            if(!string.IsNullOrEmpty(camperPrefab))
            {
                if(maleBound==null || femaleBound==null)throw new Exception("Both real bound native bodies are required for camper fit review.");
                var reviewer=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RebirthCamperSeatingReview",false)).FirstOrDefault(t=>t!=null);
                if(reviewer==null)throw new Exception("Camper seating review helper is unavailable.");
                reviewer.GetMethod("Review",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{camperPrefab,Arg("-rebirthAvatarCamperXml",""),Path.Combine(output,"camper_fit"),maleBound,femaleBound});
            }
            report.assets=assets.ToArray();report.images=images.ToArray();report.status=assets.Count==4?"PASS_ASSET_INSPECTION_NOT_SEATING_PROOF":"FAIL";
        }
        catch(Exception error){report.error=error.ToString();Debug.LogError(report.error);}
        finally{report.logWarnings=warnings.ToArray();Application.logMessageReceived-=capture;File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(report,true));foreach(var obj in owned.AsEnumerable().Reverse())if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);foreach(var bundle in bundles.AsEnumerable().Reverse())if(bundle!=null)bundle.Unload(true);}
        EditorApplication.Exit(report.status.StartsWith("PASS")?0:1);
    }
    static GameObject BindRealSkin(GameObject meshSource,GameObject baseRig,RuntimeAnimatorController controller,List<UnityEngine.Object> owned)
    {
        var rig=UnityEngine.Object.Instantiate(baseRig);owned.Add(rig);rig.name="NativeBoundRig_"+meshSource.name;
        foreach(var behavior in rig.GetComponentsInChildren<MonoBehaviour>(true))if(behavior!=null)behavior.enabled=false;
        var groups=rig.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.ToArray());
        foreach(var skin in meshSource.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mapped=new Transform[skin.bones.Length];for(int n=0;n<mapped.Length;n++)
            {var old=skin.bones[n];Transform[] matches;if(old==null || !groups.TryGetValue(old.name,out matches)||matches.Length!=1)throw new Exception("Native skin bone absent/ambiguous: "+(old==null?"null":old.name));mapped[n]=matches[0];}
            Transform[] rootMatches;if(skin.rootBone==null || !groups.TryGetValue(skin.rootBone.name,out rootMatches)||rootMatches.Length!=1)throw new Exception("Native skin root bone absent/ambiguous");
            skin.transform.SetParent(rig.transform,false);skin.bones=mapped;skin.rootBone=rootMatches[0];skin.updateWhenOffscreen=true;
        }
        var animator=rig.GetComponent<Animator>();if(animator==null || animator.avatar==null || !animator.avatar.isValid)throw new Exception("Native base rig Animator/avatar missing");animator.runtimeAnimatorController=controller;
        meshSource.SetActive(false);return rig;
    }
    static Vector3 Bone(GameObject root,string name)
    {var matches=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name==name).ToArray();if(matches.Length!=1)throw new Exception("Evidence bone absent/ambiguous: "+name);return root.transform.InverseTransformPoint(matches[0].position);}
    static void States(AnimatorStateMachine machine,string prefix,List<string> states){foreach(var state in machine.states)states.Add(prefix+"/"+state.state.name+" motion="+(state.state.motion==null?"null":state.state.motion.name));foreach(var child in machine.stateMachines)States(child.stateMachine,prefix+"/"+child.stateMachine.name,states);}
    static void Capture(GameObject body,string output,string label,List<string> images,List<UnityEngine.Object> owned)
    {
        var bounds=new Bounds();bool first=true;
        var snapshots=new List<GameObject>();var hidden=new List<SkinnedMeshRenderer>();
        foreach(var renderer in body.GetComponentsInChildren<Renderer>(true))
        {
            var skin=renderer as SkinnedMeshRenderer;
            if(skin!=null)
            {
                var baked=new Mesh();skin.BakeMesh(baked);
                foreach(var vertex in baked.vertices){var p=skin.transform.TransformPoint(vertex);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                // Batch Camera.Render may display stale GPU skinning despite
                // manually evaluated bones. Render the exact CPU-baked snapshot.
                owned.Add(baked);var snapshot=new GameObject("EvaluatedSkinSnapshot");owned.Add(snapshot);snapshots.Add(snapshot);
                snapshot.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);snapshot.transform.localScale=skin.transform.lossyScale;
                snapshot.AddComponent<MeshFilter>().sharedMesh=baked;snapshot.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                if(skin.enabled){hidden.Add(skin);skin.enabled=false;}
            }
            else{if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
        }
        if(first){foreach(var snapshot in snapshots)snapshot.SetActive(false);foreach(var skin in hidden)skin.enabled=true;return;}
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.6f,.6f);
        var cameraGo=new GameObject("ProbeCamera");owned.Add(cameraGo);var camera=cameraGo.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.2f);camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.transform.position=bounds.center+new Vector3(1,.3f,2)*Mathf.Max(.7f,bounds.size.magnitude);camera.transform.LookAt(bounds.center);
        var lightGo=new GameObject("ProbeLight");owned.Add(lightGo);var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;lightGo.transform.rotation=Quaternion.Euler(35,210,0);
        var target=new RenderTexture(1024,1024,24);owned.Add(target);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(1024,1024,TextureFormat.RGB24,false);owned.Add(pixels);pixels.ReadPixels(new Rect(0,0,1024,1024),0,0);pixels.Apply();string path=Path.Combine(output,label+".png");File.WriteAllBytes(path,pixels.EncodeToPNG());images.Add(path);RenderTexture.active=previous;camera.targetTexture=null;cameraGo.SetActive(false);lightGo.SetActive(false);
        foreach(var snapshot in snapshots)snapshot.SetActive(false);foreach(var skin in hidden)skin.enabled=true;
    }
    static int I(byte[] bytes,int offset){if(offset<0||offset+4>bytes.Length)throw new Exception("Catalog byte bounds invalid");return BitConverter.ToInt32(bytes,offset);}
    static void CheckTorso(GameObject body,string output)
    {
        body.SetActive(true);EvaluateNativePoseSnapshot(body,30);
        var animator=body.GetComponent<Animator>();animator.enabled=true;
        var targets=new Dictionary<string,Transform>();var owned=new List<GameObject>();
        foreach(string side in new[]{"Left","Right"})
        {
            var hand=body.GetComponentsInChildren<Transform>(true).Single(t=>t.name==side+"Hand");
            var go=new GameObject("TorsoTestHand"+side);owned.Add(go);go.transform.SetPositionAndRotation(hand.position,hand.rotation);
            targets.Add(side=="Left"?"IKHandL":"IKHandR",go.transform);
        }
        var builder=PrepareNativeRigging(body,targets);var type=builder.GetType();type.GetMethod("Clear").Invoke(builder,null);
        var graph=PlayableGraph.Create("NativeTorsoPriorityCheck");
        try
        {
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var controller=AnimatorControllerPlayable.Create(graph,animator.runtimeAnimatorController);
            controller.SetInteger("VehiclePose",30);
            AnimationPlayableOutput.Create(graph,"NativeController",animator).SetSourcePlayable(controller);
            if(!(bool)type.GetMethod("Build",new[]{typeof(PlayableGraph)}).Invoke(builder,new object[]{graph}))throw new Exception("Native test rig build failed");
            var lean=new Rebirth.Vehicles.Authoring.SeatPoseTorso(animator);
            Action evaluate=()=>{type.GetMethod("SyncLayers").Invoke(builder,null);graph.Evaluate(1f/30f);};
            graph.Play();for(int i=0;i<60;i++)evaluate();
            var spine=body.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Spine");var baseline=spine.rotation;
            lean.Apply(25,graph);for(int i=0;i<5;i++)evaluate();
            float angle=Quaternion.Angle(baseline,spine.rotation);
            if(Mathf.Abs(angle-25)>.5f)throw new Exception("Torso job did not apply 25 degrees: "+angle);
            var lines=new List<string>{"PASS native generic pose30 torso change="+angle};
            foreach(string side in new[]{"Left","Right"})
            {
                var hand=body.GetComponentsInChildren<Transform>(true).Single(t=>t.name==side+"Hand");float error=Vector3.Distance(hand.position,targets[side=="Left"?"IKHandL":"IKHandR"].position);
                if(error>.02f)throw new Exception("Torso before-IK wrist error "+side+" "+error);
                lines.Add("PASS leaned "+side+" wrist target error="+error);
            }
            lean.Clear();for(int i=0;i<5;i++)evaluate();
            if(Quaternion.Angle(baseline,spine.rotation)>.5f)throw new Exception("Torso reset did not restore native animation");
            lines.Add("PASS torso reset restores native animated spine; graph remains native, no replacement pose clip.");
            File.WriteAllLines(Path.Combine(output,"torso-verified.txt"),lines);
        }
        finally{if(graph.IsValid())graph.Destroy();type.GetMethod("Clear").Invoke(builder,null);foreach(var go in owned)UnityEngine.Object.DestroyImmediate(go);}
    }
    public static string[] EvaluateNativePoseSnapshot(GameObject instance,int pose,Dictionary<string,Transform> vehicleTargets=null)
    {
        var animator=instance.GetComponent<Animator>();animator.enabled=true;
        var builder=PrepareNativeRigging(instance,vehicleTargets);
        var graph=PlayableGraph.Create("NativeSeatSnapshot");
        try
        {
            ((Behaviour)builder).enabled=true;builder.GetType().GetMethod("Clear").Invoke(builder,null);
            builder.GetType().GetMethod("StartPreview").Invoke(builder,null);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var controller=AnimatorControllerPlayable.Create(graph,animator.runtimeAnimatorController);
            var chain=(Playable)builder.GetType().GetMethod("BuildPreviewGraph").Invoke(builder,new object[]{graph,(Playable)controller});
            if(chain.Equals((Playable)controller))throw new Exception("Native seat constraints not connected");
            AnimationPlayableOutput.Create(graph,"NativeSeat",animator).SetSourcePlayable(chain);
            controller.SetInteger("VehiclePose",pose);graph.Play();
            for(int n=0;n<60;n++){builder.GetType().GetMethod("UpdatePreviewGraph").Invoke(builder,new object[]{graph});graph.Evaluate(1f/30f);}
            var clips=new List<string>();for(int n=0;n<controller.GetLayerCount();n++)clips.AddRange(controller.GetCurrentAnimatorClipInfo(n).Select(c=>c.clip.name+" weight="+c.weight));
            var transforms=instance.GetComponentsInChildren<Transform>(true);var positions=transforms.Select(t=>t.localPosition).ToArray();var rotations=transforms.Select(t=>t.localRotation).ToArray();
            builder.GetType().GetMethod("StopPreview").Invoke(builder,null);graph.Destroy();((Behaviour)builder).enabled=false;animator.enabled=false;
            for(int n=0;n<transforms.Length;n++){transforms[n].localPosition=positions[n];transforms[n].localRotation=rotations[n];}
            return clips.ToArray();
        }
        finally{if(graph.IsValid()){builder.GetType().GetMethod("StopPreview").Invoke(builder,null);graph.Destroy();}}
    }
    public static void EvaluateAuthoredPoseSnapshot(GameObject instance,AnimationClip clip,Dictionary<string,Transform> vehicleTargets,float finalSteeringAngle=0)
    {
        var animator=instance.GetComponent<Animator>();animator.enabled=true;
        var builder=PrepareNativeRigging(instance,vehicleTargets);var graph=PlayableGraph.Create("AuthoredUpstreamNativeRigPreview");
        var constraintEvidence=new List<string>();
        foreach(var constraint in instance.GetComponentsInChildren<Component>(true).Where(c=>c!=null&&c.GetType().FullName=="UnityEngine.Animations.Rigging.TwoBoneIKConstraint"))
        {
            var serialized=new SerializedObject(constraint);var target=serialized.FindProperty("m_Data.m_Target").objectReferenceValue as Transform;
            bool vehicleHand=vehicleTargets!=null&&vehicleTargets.Values.Contains(target);
            serialized.FindProperty("m_Weight").floatValue=vehicleHand?1:0;serialized.ApplyModifiedPropertiesWithoutUndo();
            constraintEvidence.Add(constraint.name+" target="+(target?target.name:"null")+" weight="+(vehicleHand?1:0));
        }
        File.WriteAllLines(Path.Combine(Arg("-rebirthAvatarReport","E:/_Haven/shared/REBIRTHTemp"),"front-constraints-"+(vehicleTargets!=null?"driver":"passenger")+".txt"),constraintEvidence);
        try{
            ((Behaviour)builder).enabled=true;builder.GetType().GetMethod("Clear").Invoke(builder,null);builder.GetType().GetMethod("StartPreview").Invoke(builder,null);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var input=AnimationClipPlayable.Create(graph,clip);input.SetApplyFootIK(false);input.SetApplyPlayableIK(false);input.SetTime(.5);input.SetSpeed(0);
            var chain=(Playable)builder.GetType().GetMethod("BuildPreviewGraph").Invoke(builder,new object[]{graph,(Playable)input});
            if(chain.Equals((Playable)input))throw new Exception("Native rig jobs missing after authored pose");
            AnimationPlayableOutput.Create(graph,"AuthoredThenNativeIK",animator).SetSourcePlayable(chain);graph.Play();
            Action evaluate=()=>{for(int i=0;i<4;i++){builder.GetType().GetMethod("UpdatePreviewGraph").Invoke(builder,new object[]{graph});graph.Evaluate(1f/30f);}};
            evaluate();
            if(vehicleTargets!=null&&Arg("-rebirthAvatarCalibrateGrip","0")=="1")RebirthCamperHandGripCalibration.Calibrate(instance,vehicleTargets,evaluate,Arg("-rebirthAvatarReport","E:/_Haven/shared/REBIRTHTemp"));
            if(vehicleTargets!=null)
            {
                var joint=vehicleTargets.Values.First().parent;var rotation=joint.localRotation;var lines=new List<string>();
                foreach(float angle in new[]{-60f,0f,60f,0f})
                {
                    joint.localRotation=rotation*Quaternion.Euler(0,angle,0);
                    for(int i=0;i<4;i++){builder.GetType().GetMethod("UpdatePreviewGraph").Invoke(builder,new object[]{graph});graph.Evaluate(1f/30f);}
                    foreach(string side in new[]{"Left","Right"})
                    {
                        var hand=instance.GetComponentsInChildren<Transform>(true).Single(t=>t.name==side+"Hand");var target=vehicleTargets[side=="Left"?"IKHandL":"IKHandR"];
                        float distance=Vector3.Distance(hand.position,target.position);
                        lines.Add("steeringY="+angle+" "+side+" wristError="+distance.ToString("F6")+" hand="+hand.position.ToString("F4")+" target="+target.position.ToString("F4"));
                    }
                }
                joint.localRotation=rotation*Quaternion.Euler(0,finalSteeringAngle,0);
                File.WriteAllLines(Path.Combine(Arg("-rebirthAvatarReport","E:/_Haven/shared/REBIRTHTemp"),"front-steering-preview.txt"),lines);
            }
            for(int i=0;i<4;i++){builder.GetType().GetMethod("UpdatePreviewGraph").Invoke(builder,new object[]{graph});graph.Evaluate(1f/30f);}
            var frozenTrace=new List<string>();
            Action<string> trace=label=>{if(vehicleTargets==null)return;foreach(string side in new[]{"Left","Right"}){var hand=instance.GetComponentsInChildren<Transform>(true).Single(t=>t.name==side+"Hand");var target=vehicleTargets[side=="Left"?"IKHandL":"IKHandR"];float error=Vector3.Distance(hand.position,target.position);frozenTrace.Add(label+" angle="+finalSteeringAngle+" "+side+" error="+error.ToString("F6")+" hand="+hand.position.ToString("F5")+" target="+target.position.ToString("F5"));if(error>.003f)throw new Exception("Frozen authored steering snapshot missed target: "+label+" "+side+" "+error);}};
            trace("beforeFreeze");
            var ts=instance.GetComponentsInChildren<Transform>(true);var ps=ts.Select(t=>t.localPosition).ToArray();var qs=ts.Select(t=>t.localRotation).ToArray();
            builder.GetType().GetMethod("StopPreview").Invoke(builder,null);graph.Destroy();((Behaviour)builder).enabled=false;animator.enabled=false;
            for(int i=0;i<ts.Length;i++){ts[i].localPosition=ps[i];ts[i].localRotation=qs[i];}
            trace("afterFreeze");if(vehicleTargets!=null)File.WriteAllLines(Path.Combine(Arg("-rebirthAvatarReport","E:/_Haven/shared/REBIRTHTemp"),"front-frozen-"+finalSteeringAngle+".txt"),frozenTrace);
        }finally{if(graph.IsValid()){builder.GetType().GetMethod("StopPreview").Invoke(builder,null);graph.Destroy();}}
    }
    static Component PrepareNativeRigging(GameObject instance,Dictionary<string,Transform> vehicleTargets=null)
    {
        RestoreNativeBindings(instance);
        if(vehicleTargets!=null)
        {
            var applied=new HashSet<string>();
            foreach(var constraint in instance.GetComponentsInChildren<Component>(true).Where(c=>c!=null&&c.GetType().FullName=="UnityEngine.Animations.Rigging.TwoBoneIKConstraint"))
            {
                var serialized=new SerializedObject(constraint);
                var target=serialized.FindProperty("m_Data.m_Target");
                var original=target.objectReferenceValue as Transform;
                if(original==null)throw new Exception("Native constraint target missing before vehicle binding");
                Transform replacement;
                if(!vehicleTargets.TryGetValue(original.name,out replacement))continue;
                if(!replacement)throw new Exception("Vehicle IK target missing: "+original.name);
                target.objectReferenceValue=replacement;
                serialized.FindProperty("m_Weight").floatValue=1;
                if(original.name=="IKFootL"||original.name=="IKFootR"||original.name=="IKHandL"||original.name=="IKHandR")
                {
                    string hintName=original.name.StartsWith("IKFoot")?(original.name.EndsWith("L")?"IKKneeL":"IKKneeR"):(original.name.EndsWith("L")?"IKElbowL":"IKElbowR");Transform hint;
                    if(vehicleTargets.TryGetValue(hintName,out hint)){serialized.FindProperty("m_Data.m_Hint").objectReferenceValue=hint;serialized.FindProperty("m_Data.m_HintWeight").floatValue=1;applied.Add(hintName);}
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();applied.Add(original.name);
            }
            if(applied.Count!=vehicleTargets.Count)throw new Exception("Unmapped vehicle IK targets: "+string.Join(",",vehicleTargets.Keys.Where(k=>!applied.Contains(k))));
        }
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Animations.Rigging.RigBuilder")).FirstOrDefault(t=>t!=null);
        if(type==null)throw new Exception("Exact native rigging assembly not loaded");
        var builder=instance.GetComponent(type);
        if(builder==null)throw new Exception("Native prefab RigBuilder missing; refuse synthetic replacement");
        var layers=(System.Collections.IEnumerable)type.GetProperty("layers").GetValue(builder);
        int validLayers=0;
        foreach(var layer in layers)
        {
            var rig=layer.GetType().GetProperty("rig").GetValue(layer) as UnityEngine.Object;
            if(rig==null)throw new Exception("Native RigBuilder layer lost its serialized Rig reference; cannot validate pose fidelity");
            validLayers++;
        }
        if(validLayers==0)throw new Exception("Native RigBuilder has no rig layers");
        // BindRealSkin disables gameplay behaviours. Restore only native rig
        // layers/constraints; disabled constraints are omitted by graph Build.
        foreach(var behavior in instance.GetComponentsInChildren<MonoBehaviour>(true))
            if(behavior!=null && behavior!=builder && behavior.GetType().Namespace=="UnityEngine.Animations.Rigging")behavior.enabled=true;
        if(!(bool)type.GetMethod("Build",Type.EmptyTypes).Invoke(builder,null))throw new Exception("Native rigging graph failed to build");
        var graph=(PlayableGraph)type.GetProperty("graph").GetValue(builder);
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        return builder;
    }
    [Serializable] public class NativeBindings {public string source,sha256;public NativeBinding[] bindings;}
    [Serializable] public class NativeBinding {public string kind,path;public float weight;public NativeData data;public NativeLayer[] layers;}
    [Serializable] public class NativeLayer {public string path;public bool active;}
    [Serializable] public class NativeData {public string m_Root,m_Mid,m_Tip,m_Target,m_Hint;public float m_TargetPositionWeight,m_TargetRotationWeight,m_HintWeight;public int m_MaintainTargetPositionOffset,m_MaintainTargetRotationOffset;}
    static void RestoreNativeBindings(GameObject instance)
    {
        string file=Arg("-rebirthAvatarRigBindings","");
        if(string.IsNullOrEmpty(file))throw new Exception("Exact extracted native rig binding file required");
        var records=JsonUtility.FromJson<NativeBindings>(File.ReadAllText(file));
        string bundle=Path.Combine(Arg("-rebirthAvatarGameRoot","C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die"),"Data/Addressables/Standalone/player_assets_entities/player/common.bundle");
        using(var stream=File.OpenRead(bundle))using(var sha=System.Security.Cryptography.SHA256.Create())
            if(!string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""),records.sha256,StringComparison.OrdinalIgnoreCase))
                throw new Exception("Native rig bindings are stale for the installed game bundle");
        Func<string,Transform> resolve=path=>{
            if(string.IsNullOrEmpty(path))return null;
            if(path=="baseRigPrefab")return instance.transform;
            if(!path.StartsWith("baseRigPrefab/",StringComparison.Ordinal))throw new Exception("Wrong native rig binding root");
            var target=instance.transform.Find(path.Substring("baseRigPrefab/".Length));
            if(target==null)throw new Exception("Native rig binding path absent: "+path);
            return target;
        };
        int constraints=0,builders=0;
        foreach(var row in records.bindings.Where(r=>r.path=="baseRigPrefab"||r.path.StartsWith("baseRigPrefab/",StringComparison.Ordinal)))
        {
            var owner=resolve(row.path);
            string componentName=row.kind=="builder"?"RigBuilder":"TwoBoneIKConstraint";
            var component=owner.GetComponents<Component>().Single(c=>c!=null&&c.GetType().FullName=="UnityEngine.Animations.Rigging."+componentName);
            var serialized=new SerializedObject(component);
            if(row.kind=="builder")
            {
                builders++;var layers=serialized.FindProperty("m_RigLayers");layers.arraySize=row.layers.Length;
                for(int n=0;n<row.layers.Length;n++)
                {var layer=layers.GetArrayElementAtIndex(n);layer.FindPropertyRelative("m_Rig").objectReferenceValue=resolve(row.layers[n].path).GetComponents<Component>().Single(c=>c!=null&&c.GetType().FullName=="UnityEngine.Animations.Rigging.Rig");layer.FindPropertyRelative("m_Active").boolValue=row.layers[n].active;}
            }
            else
            {
                constraints++;serialized.FindProperty("m_Weight").floatValue=row.weight;
                foreach(string name in new[]{"m_Root","m_Mid","m_Tip","m_Target","m_Hint"})serialized.FindProperty("m_Data."+name).objectReferenceValue=resolve((string)typeof(NativeData).GetField(name).GetValue(row.data));
                foreach(string name in new[]{"m_TargetPositionWeight","m_TargetRotationWeight","m_HintWeight"})serialized.FindProperty("m_Data."+name).floatValue=(float)typeof(NativeData).GetField(name).GetValue(row.data);
                foreach(string name in new[]{"m_MaintainTargetPositionOffset","m_MaintainTargetRotationOffset"})serialized.FindProperty("m_Data."+name).boolValue=(int)typeof(NativeData).GetField(name).GetValue(row.data)!=0;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        if(constraints!=4||builders!=1)throw new Exception("Exact native rig binding count mismatch");
    }
    // Enumerate exact installed vehicle configuration, never guess pose IDs or
    // mistake a successful parameter assignment for a distinct animation.
    static int[] NativeVehiclePoses(string game)
    {
        if(Arg("-rebirthAvatarAllVehiclePoses","0")!="1")return new[]{40,41};
        var xml=new System.Xml.XmlDocument();xml.Load(Path.Combine(game,"Data/Config/vehicles.xml"));
        var ids=new SortedSet<int>();
        foreach(System.Xml.XmlNode node in xml.SelectNodes("//property[@name='pose']"))
        {int value;if(!int.TryParse(node.Attributes["value"]?.Value,out value))throw new Exception("Invalid native vehicle pose ID");ids.Add(value);}
        if(ids.Count==0)throw new Exception("Installed vehicle XML contains no pose IDs");
        return ids.ToArray();
    }
    static string Arg(string key,string fallback){var args=Environment.GetCommandLineArgs();int n=Array.IndexOf(args,key);return n>=0&&n+1<args.Length?args[n+1]:fallback;}
}

