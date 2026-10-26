using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RebirthEntityStudio
{
    public static class RebirthNurseReviewCapture
    {
        private const string ScenePath="Assets/EntityStudioGenerated/RebirthNurse/RebirthNurse_RigReview.unity";
        private const string ProfilePath="Assets/EntityStudioGenerated/RebirthNurse/RebirthNurse_RigProfile.asset";
        private const string ModelPath="Assets/EntityStudioInputs/RebirthNurseReplacement/tripo_convert_9d9a82eb-9b84-432d-8cef-81dc53e4769f.fbx";
        private const int Width=1200,Height=1600;

        public static void Run()
        {
            var temporary=new List<GameObject>();
            RenderTexture rt=null;
            Texture2D readback=null;
            var renderErrors=new List<string>();
            Application.logMessageReceived+=RecordError;
            try
            {
                bool replacement=true;
                string inputFolder=replacement?RebirthNurseReplacement.Input:"Assets/EntityStudioInputs/RebirthNurse";
                Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                RebirthEntityRigProfile profile=null;
                string modelPath=AssetDatabase.FindAssets("t:Model",new[]{inputFolder})
                    .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(path=>path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase));
                var model=string.IsNullOrEmpty(modelPath)?null:AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if(model==null)throw new InvalidDataException("Could not load unrigged model at "+ModelPath);
                if(profile==null)
                {
                    profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
                    profile.entityClass="RebirthNurse";profile.prefabFamily="zombieTemplateShort";
                    profile.sourceModelPath=modelPath;profile.sourceTexturePath=inputFolder;
                    profile.sourceUpAxis=EntityModelUpAxis.Z;profile.characterShader=Shader.Find("Standard");profile.InitializeBipedDefaults();
                    if(replacement)RebirthNurseReplacement.Landmarks(profile);else RebirthNurseReplacement.Landmarks(profile);
                    foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{inputFolder}))
                    {
                        string path=AssetDatabase.GUIDToAssetPath(guid);string lower=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if(texture==null)continue;
                        var usage=lower=="color"||lower.Contains("basecolor")?EntityTextureUsage.Albedo:lower.Contains("normal")?EntityTextureUsage.Normal:EntityTextureUsage.LinearData;
                        RebirthEntityTextureTools.Configure(texture,usage,usage==EntityTextureUsage.LinearData?512:2048);
                        if((lower.Contains("basecolor")||lower=="color"))profile.bakedAlbedo=texture;
                        else if(lower.Contains("normal"))profile.normalMap=texture;
                        else if(lower.Contains("roughness"))profile.sourceRoughness=texture;
                        else if(lower.Contains("metallic"))profile.sourceMetallic=texture;
                    }
                    RebirthEntityTextureTools.ConfigureProfileMaps(profile);
                }
                profile.sourceUpAxis=EntityModelUpAxis.Z;
                // LODs are derivatives; the supplied LOD0 geometry stays unchanged.
                profile.lod1Ratio=.70f;profile.lod2Ratio=.50f;profile.lod3Ratio=.34f;
                if(replacement)RebirthNurseReplacement.Landmarks(profile);else RebirthNurseReplacement.Landmarks(profile);
                // This entry point regenerates this model's review candidate from its source.
                if(!replacement)RebirthFarmerFullHumanoid.FitHandsToSource(profile,model);
                var savedProfile=AssetDatabase.LoadAssetAtPath<RebirthEntityRigProfile>(ProfilePath);
                if(savedProfile!=null)
                {
                    // Keep the GUID referenced by the saved scene if a later
                    // texture import or render fails during regeneration.
                    EditorUtility.CopySerialized(profile,savedProfile);UnityEngine.Object.DestroyImmediate(profile);profile=savedProfile;EditorUtility.SetDirty(profile);
                }
                else AssetDatabase.CreateAsset(profile,ProfilePath);
                profile.separateFusedArms=false;
                profile.compoundTerminalColliders=false;
                profile.wrapMissingTorsoTexture=false;
                profile.refineBodyWeightsWithPoses=false;
                var workspace=RebirthEntityRigBuilder.CreateWorkspace(model,profile,false);
                RebirthNurseReplacement.CleanNativeHelpers(workspace);
                RebirthEntityGeneratedMaps maps=RebirthEntityMaterialMaps.Generate(workspace);
                string avatarPath=AssetDatabase.GenerateUniqueAssetPath("Assets/EntityStudioGenerated/RebirthNurse/RebirthNurseAvatar.asset");
                RebirthEntityHumanoidBuilder.Build(workspace,avatarPath,false);
                // Preserve anatomical chain ownership on the unmodified source.
                // Surface smoothing across touching hand/thigh geometry mixes limbs.
                if(profile.refineBodyWeightsWithPoses)RebirthEntityPoseWeightCorrection.ApplyWorkspace(workspace);
                RebirthEntityRigBuilder.RebuildLods(workspace);
                RebirthEntityRigBuilder.BuildPhysics(workspace);
                RebirthNurseNativePhysics.Configure(workspace);
                if(workspace.generatedSkeleton.GetComponentsInChildren<Collider>(true).Length!=11)
                    throw new InvalidDataException("Farmer requires the inspected native 11-collider layout.");
                RebirthEntityRigBuilder.BindWorkspaceReferences(workspace);
                RebirthEntityEyeGeometry.Build(workspace,maps);
                RebirthEntitySkinnedBounds.FitRestPose(workspace);
                var sourceGeometry=model.GetComponentsInChildren<MeshFilter>(true).Select(value=>RebirthNurseSourcePreservation.Hash(value.sharedMesh)).OrderBy(value=>value);
                var rigGeometry=workspace.sourceSkinnedMeshes.Select(value=>RebirthNurseSourcePreservation.Hash(value.sharedMesh)).OrderBy(value=>value);
                if(!sourceGeometry.SequenceEqual(rigGeometry))
                    throw new InvalidDataException("Original LOD0 geometry changed during rig generation.");
                foreach(var renderer in profile.sourceSkinnedMeshes)
                {
                    Mesh mesh=renderer.sharedMesh;
                    if(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))
                        AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath("Assets/EntityStudioGenerated/RebirthNurse/"+mesh.name+".asset"));
                }
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene,ScenePath);
                Transform skeleton=workspace.generatedSkeleton;
                Renderer sourceRenderer=skeleton.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if(sourceRenderer==null)throw new InvalidDataException("Review scene has no rendered skinned mesh.");
                Bounds bounds=new Bounds();bool hasBounds=false;
                foreach(var renderer in profile.sourceSkinnedMeshes)foreach(Vector3 point in renderer.sharedMesh.vertices)
                {Vector3 world=renderer.transform.TransformPoint(point);if(!hasBounds){bounds=new Bounds(world,Vector3.zero);hasBounds=true;}else bounds.Encapsulate(world);}
                float size=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                Vector3 target=bounds.center;
                var cameraGo=new GameObject("__RebirthNurseReviewCamera");temporary.Add(cameraGo);
                Camera cam=cameraGo.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.13f,.16f,.19f,1f);
                cam.fieldOfView=32f;cam.nearClipPlane=.01f;cam.farClipPlane=20f;cam.aspect=(float)Width/Height;cam.allowHDR=false;cam.allowMSAA=true;
                cameraGo.transform.position=target+new Vector3(size*.42f,size*.04f,size*3.1f);cameraGo.transform.rotation=Quaternion.LookRotation(target-cameraGo.transform.position,Vector3.up);
                var keyGo=new GameObject("__RebirthNurseReviewKey");temporary.Add(keyGo);var key=keyGo.AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.25f;key.color=new Color(1f,.94f,.84f);keyGo.transform.rotation=Quaternion.Euler(35f,145f,0f);
                var fillGo=new GameObject("__RebirthNurseReviewFill");temporary.Add(fillGo);var fill=fillGo.AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.55f;fill.color=new Color(.72f,.82f,1f);fillGo.transform.rotation=Quaternion.Euler(20f,-32f,0f);
                Color oldAmbient=RenderSettings.ambientLight;AmbientMode oldMode=RenderSettings.ambientMode;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.43f,.43f,.43f);
                rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32){antiAliasing=2};rt.Create();cam.targetTexture=rt;
                string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs","EntityStudioValidation","RebirthNurseReview");
                Directory.CreateDirectory(output);
                var repairRows=new List<string>{"mesh,vertex,cap,part"};
                foreach(var input in workspace.surfaceRepairInputs)for(int i=0;i<input.repairVertexIds.Length;i++)repairRows.Add(input.repairedMesh.name+","+i+","+input.repairVertexIds[i]+","+input.repairVertexParts[i]);
                File.WriteAllLines(Path.Combine(output,"RebirthNurse_RepairVertices.csv"),repairRows);
                File.WriteAllLines(Path.Combine(output,"RebirthNurse_PackedMaps.txt"),maps.packedAssetPaths);
                File.WriteAllText(Path.Combine(output,"RebirthNurse_PhysicsBodies.xml"),RebirthEntityPhysicsLayoutExporter.BuildPatch(skeleton,profile.entityClass,profile.colliderModes.ToArray()));
                var vertexRows=new List<string>{"x,y,z"};
                foreach(var renderer in profile.sourceSkinnedMeshes)
                    foreach(Vector3 vertex in renderer.sharedMesh.vertices)
                    {
                        Vector3 world=renderer.transform.TransformPoint(vertex);
                        vertexRows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:R},{1:R},{2:R}",world.x,world.y,world.z));
                    }
                File.WriteAllLines(Path.Combine(output,"RebirthNurse_WorldVertices.csv"),vertexRows);
                RebirthEntitySkinAudit.Write(workspace,Path.Combine(output,"RebirthNurse_BoneWeights.csv"));
                RebirthEntityCoverageReview.Write(workspace,Path.Combine(output,"RebirthNurse_Coverage.json"));
                RenderAndSave(cam,rt,Path.Combine(output,"RebirthNurse_Model.png"),ref readback);
                if(workspace.lodGroup!=null)
                {
                    var levels=workspace.lodGroup.GetLODs();var counts=new List<string>();
                    for(int level=0;level<levels.Length;level++)
                    {
                        workspace.lodGroup.ForceLOD(level);RenderAndSave(cam,rt,Path.Combine(output,"RebirthNurse_LOD"+level+".png"),ref readback);
                        int triangles=0;foreach(var renderer in levels[level].renderers.OfType<SkinnedMeshRenderer>())for(int sub=0;sub<renderer.sharedMesh.subMeshCount;sub++)triangles+=(int)renderer.sharedMesh.GetIndexCount(sub)/3;
                        counts.Add(triangles.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    workspace.lodGroup.ForceLOD(0);File.WriteAllText(Path.Combine(output,"RebirthNurse_LodMetrics.json"),"{\"triangles\":["+string.Join(",",counts)+"]}");
                }
                foreach(ZombieTier tier in new[]{ZombieTier.Feral,ZombieTier.Radiated,ZombieTier.Charged,ZombieTier.Infernal})
                {RebirthEntityRigBuilder.ApplyTierMaterials(workspace,tier);RenderAndSave(cam,rt,Path.Combine(output,"RebirthNurse_"+tier+".png"),ref readback);}
                Vector3 fullPosition=cameraGo.transform.position;Quaternion fullRotation=cameraGo.transform.rotation;float fullFov=cam.fieldOfView;
                RebirthEntityRigBuilder.ApplyTierMaterials(workspace,ZombieTier.Normal);
                foreach(string side in new[]{"Left","Right"})
                {
                    FrameHand(cam,skeleton,side,profile.estimatedHeight);
                    RenderPosedLod(cam,rt,Path.Combine(output,"RebirthNurse_"+side+"Hand_Rest.png"),skeleton,profile.sourceSkinnedMeshes,ref readback);
                    var handRotations=new Dictionary<Transform,Quaternion>();
                    foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Pinky"})for(int joint=1;joint<=3;joint++)
                    {
                        Transform bone=RebirthEntityRigBuilder.FindBone(skeleton,side+"Hand"+finger+joint);if(bone==null)continue;
                        handRotations[bone]=bone.localRotation;float bend=(joint==1?45:joint==2?30:20)*(side=="Left"?1:-1);
                        bone.localRotation=bone.localRotation*Quaternion.Euler(0,0,bend);
                    }
                    RenderPosedLod(cam,rt,Path.Combine(output,"RebirthNurse_"+side+"Hand_Bent.png"),skeleton,profile.sourceSkinnedMeshes,ref readback);
                    RebirthEntityDeformationAudit.WritePose(workspace,Path.Combine(output,"RebirthNurse_"+side+"Hand_Deformation.json"),side+"Hand");
                    foreach(var pair in handRotations)pair.Key.localRotation=pair.Value;
                }
                cameraGo.transform.SetPositionAndRotation(fullPosition,fullRotation);cam.fieldOfView=fullFov;
                Vector3 face=workspace.transform.position+new Vector3(0f,profile.estimatedHeight*.934f,.02f);
                cameraGo.transform.position=face+new Vector3(.03f,.015f,.85f);cameraGo.transform.LookAt(face);cam.fieldOfView=28f;
                RebirthEntityRigBuilder.ApplyTierMaterials(workspace,ZombieTier.Feral);RenderAndSave(cam,rt,Path.Combine(output,"RebirthNurse_Feral_Face.png"),ref readback);
                RebirthEntityRigBuilder.ApplyTierMaterials(workspace,ZombieTier.Normal);RenderAndSave(cam,rt,Path.Combine(output,"RebirthNurse_Normal_Face.png"),ref readback);
                cameraGo.transform.SetPositionAndRotation(fullPosition,fullRotation);cam.fieldOfView=fullFov;
                Transform elbow=RebirthEntityRigBuilder.FindBone(skeleton,"LeftForeArm");
                Quaternion rest=elbow.localRotation;elbow.localRotation=rest*Quaternion.Euler(70f,0f,0f);
                var poseObjects=new List<GameObject>();
                foreach(var renderer in skeleton.GetComponentsInChildren<SkinnedMeshRenderer>(true))renderer.forceRenderingOff=true;
                foreach(var renderer in profile.sourceSkinnedMeshes)
                {
                    Mesh mesh=BakePoseForReview(renderer);
                    var go=new GameObject("__BakedPose");go.transform.SetParent(renderer.transform,false);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
                    renderer.enabled=false;poseObjects.Add(go);
                }
                RenderAndSave(cam,rt,Path.Combine(output,"RebirthNurse_ElbowPose.png"),ref readback);
                foreach(var go in poseObjects){UnityEngine.Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(go);}
                foreach(var renderer in profile.sourceSkinnedMeshes)renderer.enabled=true;
                foreach(var renderer in skeleton.GetComponentsInChildren<SkinnedMeshRenderer>(true))renderer.forceRenderingOff=false;
                if(workspace.lodGroup!=null)
                {
                    var levels=workspace.lodGroup.GetLODs();
                    for(int level=1;level<levels.Length;level++)RenderPosedLod(cam,rt,Path.Combine(output,"RebirthNurse_ElbowPose_LOD"+level+".png"),skeleton,levels[level].renderers.OfType<SkinnedMeshRenderer>(),ref readback);
                }
                elbow.localRotation=rest;
                Transform knee=RebirthEntityRigBuilder.FindBone(skeleton,"LeftLeg");
                Transform shoulder=RebirthEntityRigBuilder.FindBone(skeleton,"RightArm");
                Quaternion kneeRest=knee.localRotation,shoulderRest=shoulder.localRotation;
                knee.localRotation=kneeRest*Quaternion.Euler(75f,0f,0f);
                shoulder.localRotation=shoulderRest*Quaternion.Euler(25f,0f,35f);
                if(workspace.lodGroup!=null)
                {
                    var levels=workspace.lodGroup.GetLODs();
                    for(int level=0;level<levels.Length;level++)RenderPosedLod(cam,rt,Path.Combine(output,"RebirthNurse_KneeShoulderPose_LOD"+level+".png"),skeleton,levels[level].renderers.OfType<SkinnedMeshRenderer>(),ref readback);
                }
                knee.localRotation=kneeRest;shoulder.localRotation=shoulderRest;
                CreateOverlay(skeleton,temporary);
                RenderAndSave(cam,rt,Path.Combine(output,"RebirthNurse_Skeleton_Colliders.png"),ref readback);
                var lodGroup=skeleton.GetComponent<LODGroup>();
                Debug.Log("REBIRTH FARMER REVIEW CAPTURES: model="+Path.Combine(output,"RebirthNurse_Model.png")+"; skeleton+colliders="+Path.Combine(output,"RebirthNurse_Skeleton_Colliders.png")+"; profileBones="+profile.bones.Count+"; hierarchyNodes="+skeleton.GetComponentsInChildren<Transform>(true).Length+"; colliders="+skeleton.GetComponentsInChildren<Collider>(true).Length+"; lodLevels="+(lodGroup!=null?lodGroup.GetLODs().Length:0));
                var bonePositions=new List<string>();
                foreach(EntityBoneSpec spec in profile.bones)
                {
                    Transform bone=RebirthEntityRigBuilder.FindBone(skeleton,spec.name);
                    if(bone!=null)bonePositions.Add(spec.name+"="+bone.position.ToString("F3"));
                }
                var colliderFits=new List<string>();
                foreach(EntityColliderFitReport fit in workspace.colliderFitReports)
                    colliderFits.Add(fit.colliderName+":shape="+fit.collider.GetType().Name+",n="+fit.sampledVertices+",r="+fit.radius.ToString("F3")+",h="+fit.height.ToString("F3")+",box="+fit.boxSize.ToString("F3")+",fallback="+fit.usedProportionalFallback);
                Debug.Log("REBIRTH FARMER FIT DATA: bounds="+profile.overallBoundsCenter.ToString("F3")+" size="+profile.overallBoundsSize.ToString("F3")+" bones="+string.Join(";",bonePositions)+" colliders="+string.Join(";",colliderFits));
                foreach(var go in temporary)if(go!=null&&go.name=="__RebirthNurseRigOverlay")go.SetActive(false);
                var physicsReview=RebirthEntityPhysicsReview.Simulate(workspace,temporary);
                File.WriteAllText(Path.Combine(output,"RebirthNurse_PhysicsReview.json"),JsonUtility.ToJson(physicsReview,true));
                poseObjects.Clear();
                Bounds posedBounds=new Bounds();bool hasPosedBounds=false;
                foreach(var renderer in skeleton.GetComponentsInChildren<SkinnedMeshRenderer>(true))renderer.forceRenderingOff=true;
                foreach(var renderer in profile.sourceSkinnedMeshes)
                {
                    Mesh mesh=BakePoseForReview(renderer);var go=new GameObject("__RagdollBakedPose");go.transform.SetParent(renderer.transform,false);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var display=go.AddComponent<MeshRenderer>();display.sharedMaterials=renderer.sharedMaterials;poseObjects.Add(go);
                    if(!hasPosedBounds){posedBounds=display.bounds;hasPosedBounds=true;}else posedBounds.Encapsulate(display.bounds);
                }
                target=posedBounds.center;cameraGo.transform.position=target+new Vector3(2.2f,2.4f,3.2f);cameraGo.transform.LookAt(target);
                RenderAndSave(cam,rt,Path.Combine(output,"RebirthNurse_RagdollPose.png"),ref readback);
                foreach(var go in poseObjects){UnityEngine.Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(go);}
                if(renderErrors.Count>0)throw new InvalidDataException("Unity emitted "+renderErrors.Count+" errors during capture: "+renderErrors[0]);
                RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
            finally
            {
                Application.logMessageReceived-=RecordError;
                if(readback!=null)UnityEngine.Object.DestroyImmediate(readback);
                if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                foreach(GameObject go in temporary)if(go!=null)UnityEngine.Object.DestroyImmediate(go);
            }
            void RecordError(string message,string stack,LogType type)
            {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)renderErrors.Add(message);}
        }

        public static void CaptureColliderCandidate(RebirthEntityRigWorkspace workspace,string outputPrefix,bool overlay=true,bool allViews=false,string handFocus=null)
        {
            var temporary=new List<GameObject>();RenderTexture target=null;Texture2D pixels=null;
            Color ambient=RenderSettings.ambientLight;AmbientMode mode=RenderSettings.ambientMode;
            try
            {
                Bounds bounds=new Bounds();bool has=false;
                foreach(var renderer in workspace.sourceSkinnedMeshes)foreach(Vector3 vertex in renderer.sharedMesh.vertices)
                {Vector3 point=renderer.transform.TransformPoint(vertex);if(!has){bounds=new Bounds(point,Vector3.zero);has=true;}else bounds.Encapsulate(point);}
                if(!has)throw new InvalidOperationException("Collider review has no mesh.");
                if(handFocus!=null)
                {
                    if(handFocus!="Left"&&handFocus!="Right")throw new ArgumentException("Hand focus must be Left or Right.");
                    bounds=HandBounds(workspace.generatedSkeleton,handFocus,workspace.profile.estimatedHeight);bounds.Expand(.08f);
                }
                var cameraObject=new GameObject("__ColliderCandidateCamera");temporary.Add(cameraObject);var camera=cameraObject.AddComponent<Camera>();
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.19f);camera.nearClipPlane=.01f;camera.farClipPlane=30;camera.aspect=(float)Width/Height;camera.fieldOfView=28;
                var lightObject=new GameObject("__ColliderCandidateLight");temporary.Add(lightObject);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.25f;lightObject.transform.rotation=Quaternion.Euler(35,145,0);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.43f,.43f,.43f);
                target=new RenderTexture(Width,Height,24);target.Create();camera.targetTexture=target;
                if(overlay)CreateOverlay(workspace.generatedSkeleton,temporary);
                foreach(string view in allViews?new[]{"Front","Side","Back","LeftSide"}:new[]{"Front","Side"})
                {
                    float width=view=="Front"||view=="Back"?bounds.size.x:bounds.size.z;
                    float distance=Mathf.Max(bounds.size.y,width/camera.aspect)*.62f/Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
                    Vector3 direction=view=="Front"?Vector3.forward:view=="Back"?Vector3.back:view=="LeftSide"?Vector3.left:Vector3.right;
                    camera.transform.position=bounds.center+direction*distance;camera.transform.LookAt(bounds.center);
                    RenderPosedLod(camera,target,outputPrefix+"_"+view+".png",workspace.generatedSkeleton,workspace.sourceSkinnedMeshes,ref pixels);
                }
            }
            finally
            {
                RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=mode;
                foreach(var go in temporary)if(go!=null&&go.TryGetComponent<Camera>(out var camera))camera.targetTexture=null;
                if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);if(target!=null){if(RenderTexture.active==target)RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
                foreach(var go in temporary)if(go!=null)UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void RenderAndSave(Camera cam,RenderTexture rt,string path,ref Texture2D readback)
        {
            cam.Render();RenderTexture.active=rt;
            if(readback==null)readback=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            readback.ReadPixels(new Rect(0,0,Width,Height),0,0,false);readback.Apply(false,false);
            File.WriteAllBytes(path,readback.EncodeToPNG());RenderTexture.active=null;
        }

        private static Mesh BakePoseForReview(SkinnedMeshRenderer renderer)
        {
            Mesh result=UnityEngine.Object.Instantiate(renderer.sharedMesh);
            Vector3[] vertices=result.vertices,normals=result.normals;BoneWeight[] weights=result.boneWeights;
            Matrix4x4[] bindposes=result.bindposes,matrices=new Matrix4x4[renderer.bones.Length];
            for(int i=0;i<matrices.Length;i++)matrices[i]=renderer.transform.worldToLocalMatrix*renderer.bones[i].localToWorldMatrix*bindposes[i];
            for(int i=0;i<vertices.Length;i++)
            {
                BoneWeight w=weights[i];Vector3 point=vertices[i];
                vertices[i]=matrices[w.boneIndex0].MultiplyPoint3x4(point)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(point)*w.weight1+
                    matrices[w.boneIndex2].MultiplyPoint3x4(point)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(point)*w.weight3;
                if(normals.Length==vertices.Length)
                {
                    Vector3 normal=normals[i];
                    normals[i]=(matrices[w.boneIndex0].inverse.transpose.MultiplyVector(normal)*w.weight0+matrices[w.boneIndex1].inverse.transpose.MultiplyVector(normal)*w.weight1+
                        matrices[w.boneIndex2].inverse.transpose.MultiplyVector(normal)*w.weight2+matrices[w.boneIndex3].inverse.transpose.MultiplyVector(normal)*w.weight3).normalized;
                }
            }
            result.vertices=vertices;if(normals.Length==vertices.Length)result.normals=normals;result.RecalculateBounds();return result;
        }

        public static void RunSavedMaterialReview()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var workspace=scene.GetRootGameObjects().Select(root=>root.GetComponent<RebirthEntityRigWorkspace>()).Single(value=>value!=null);
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerSavedMaterialReview");Directory.CreateDirectory(folder);
            ZombieTier original=workspace.activeTier;
            try
            {
                foreach(ZombieTier tier in Enum.GetValues(typeof(ZombieTier)))
                {
                    RebirthEntityRigBuilder.ApplyTierMaterials(workspace,tier);
                    CaptureColliderCandidate(workspace,Path.Combine(folder,tier.ToString()),false,true);
                }
                RebirthEntityRigBuilder.ApplyTierMaterials(workspace,ZombieTier.Normal);
                CaptureColliderCandidate(workspace,Path.Combine(folder,"Skeleton_Colliders"));
                Debug.Log("SAVED MATERIAL REVIEW COMPLETE: five tiers and actual skeleton/collider hierarchy; no assets saved.");
            }
            finally{RebirthEntityRigBuilder.ApplyTierMaterials(workspace,original);}
        }

        public static void RunSavedHandReview()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var workspace=scene.GetRootGameObjects().Select(root=>root.GetComponent<RebirthEntityRigWorkspace>()).Single(value=>value!=null);
            RebirthEntityRigBuilder.BindWorkspaceReferences(workspace);var animator=workspace.GetComponent<Animator>();if(animator!=null)animator.enabled=false;
            bool smoothReview=Array.IndexOf(Environment.GetCommandLineArgs(),"-smoothHandReview")>=0;
            bool diffuseReview=Array.IndexOf(Environment.GetCommandLineArgs(),"-diffuseHandReview")>=0;
            bool pinRepairReview=Array.IndexOf(Environment.GetCommandLineArgs(),"-pinRepairHandReview")>=0;
            var baselineMeshes=new Dictionary<SkinnedMeshRenderer,Mesh>();
            var baselineSelections=new Dictionary<string,Dictionary<SkinnedMeshRenderer,bool[]>>();
            if(diffuseReview)
            {
                foreach(var renderer in workspace.sourceSkinnedMeshes)baselineMeshes.Add(renderer,renderer.sharedMesh);
                foreach(string side in new[]{"Left","Right"})
                {
                    var selection=new Dictionary<SkinnedMeshRenderer,bool[]>();baselineSelections.Add(side,selection);
                    foreach(var pair in baselineMeshes)
                    {
                        BoneWeight[] weights=pair.Value.boneWeights;var members=new bool[weights.Length];selection.Add(pair.Key,members);
                        bool Member(int bone,float weight)=>weight>.05f&&pair.Key.bones[bone].name.StartsWith(side+"Hand",StringComparison.Ordinal);
                        for(int i=0;i<weights.Length;i++){BoneWeight w=weights[i];members[i]=Member(w.boneIndex0,w.weight0)||Member(w.boneIndex1,w.weight1)||Member(w.boneIndex2,w.weight2)||Member(w.boneIndex3,w.weight3);}
                    }
                }
            }
            bool capsOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"-onlyRepairCaps")>=0;
            bool originalOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"-onlyOriginalSurface")>=0;
            if(capsOnly&&originalOnly)throw new ArgumentException("Choose one isolated surface category.");
            if(smoothReview)foreach(var renderer in workspace.sourceSkinnedMeshes){renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);RebirthEntityHandWeights.Smooth(renderer,8,.5f);}
            if(diffuseReview)
            {
                var repairIds=new Dictionary<string,HashSet<int>>();
                if(pinRepairReview)
                {
                    string metadata=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthNurseReview/RebirthNurse_RepairVertices.csv");
                    foreach(string row in File.ReadAllLines(metadata).Skip(1)){string[] columns=row.Split(',');if(!repairIds.TryGetValue(columns[0],out var set)){set=new HashSet<int>();repairIds.Add(columns[0],set);}if(int.Parse(columns[2])>0)set.Add(int.Parse(columns[1]));}
                }
                foreach(var renderer in workspace.sourceSkinnedMeshes)
                {
                    bool[] pins=null;
                    if(pinRepairReview){if(!repairIds.TryGetValue(renderer.sharedMesh.name,out var set))throw new InvalidDataException("Repair metadata missing for hand mesh.");pins=new bool[renderer.sharedMesh.vertexCount];foreach(int vertex in set){if(vertex<0||vertex>=pins.Length)throw new InvalidDataException("Repair metadata does not match hand mesh.");pins[vertex]=true;}}
                    renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);RebirthEntityHandWeights.Diffuse(renderer,pinnedVertices:pins);
                }
            }
            if(capsOnly||originalOnly)
            {
                string metadata=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthNurseReview/RebirthNurse_RepairVertices.csv");
                var ids=new Dictionary<string,Dictionary<int,int>>();
                foreach(string row in File.ReadAllLines(metadata).Skip(1)){string[] columns=row.Split(',');if(!ids.TryGetValue(columns[0],out var map)){map=new Dictionary<int,int>();ids.Add(columns[0],map);}map.Add(int.Parse(columns[1]),int.Parse(columns[2]));}
                foreach(var renderer in workspace.sourceSkinnedMeshes)
                {
                    Mesh original=renderer.sharedMesh;if(!ids.TryGetValue(original.name,out var map)||map.Count!=original.vertexCount)throw new InvalidDataException("Repair vertex metadata does not match saved mesh.");
                    Mesh clone=UnityEngine.Object.Instantiate(original);var triangles=new List<int>();int[] source=original.triangles;
                    for(int i=0;i<source.Length;i+=3)if((map[source[i]]>0)==capsOnly){triangles.Add(source[i]);triangles.Add(source[i+1]);triangles.Add(source[i+2]);}
                    clone.triangles=triangles.ToArray();renderer.sharedMesh=clone;
                }
            }
            var cameraObject=new GameObject("__HandReviewCamera");var lightObject=new GameObject("__HandReviewLight");RenderTexture target=null;Texture2D pixels=null;
            try
            {
                var camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.19f);camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.aspect=(float)Width/Height;
                var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.25f;lightObject.transform.rotation=Quaternion.Euler(35,145,0);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.43f,.43f,.43f);
                target=new RenderTexture(Width,Height,24);target.Create();camera.targetTexture=target;
                string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthNurseReview");
                if(smoothReview){output=Path.Combine(output,"SmoothingTrial_8_05");Directory.CreateDirectory(output);}
                if(diffuseReview){output=Path.Combine(output,"FullInfluenceDiffusion");Directory.CreateDirectory(output);}
                if(pinRepairReview){output=Path.Combine(output,"PinnedRepairs");Directory.CreateDirectory(output);}
                if(capsOnly){output=Path.Combine(output,"RepairCapsOnly");Directory.CreateDirectory(output);}
                if(originalOnly){output=Path.Combine(output,"OriginalSurfaceOnly");Directory.CreateDirectory(output);}
                foreach(string side in new[]{"Left","Right"})
                {
                    FrameHand(camera,workspace.generatedSkeleton,side,workspace.profile.estimatedHeight);
                    RenderPosedLod(camera,target,Path.Combine(output,"RebirthNurse_"+side+"Hand_Complete_Rest.png"),workspace.generatedSkeleton,workspace.sourceSkinnedMeshes,ref pixels);
                    RenderHandDirections(camera,target,workspace,side,"Rest",output,ref pixels);
                    var rotations=new Dictionary<Transform,Quaternion>();
                    try
                    {
                        foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Pinky"})for(int joint=1;joint<=3;joint++)
                        {var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,side+"Hand"+finger+joint);if(bone==null)continue;rotations[bone]=bone.localRotation;bone.localRotation*=Quaternion.Euler(0,0,(joint==1?45:joint==2?30:20)*(side=="Left"?1:-1));}
                        RenderPosedLod(camera,target,Path.Combine(output,"RebirthNurse_"+side+"Hand_Complete_Bent.png"),workspace.generatedSkeleton,workspace.sourceSkinnedMeshes,ref pixels);
                        RenderHandDirections(camera,target,workspace,side,"Bent",output,ref pixels);
                        RebirthEntityDeformationAudit.WritePose(workspace,Path.Combine(output,"RebirthNurse_"+side+"Hand_Deformation.json"),side+"Hand",diffuseReview?baselineSelections[side]:null);
                        if(diffuseReview)
                        {
                            var candidates=new Dictionary<SkinnedMeshRenderer,Mesh>();
                            try
                            {
                                foreach(var pair in baselineMeshes){candidates.Add(pair.Key,pair.Key.sharedMesh);pair.Key.sharedMesh=pair.Value;}
                                RebirthEntityDeformationAudit.WritePose(workspace,Path.Combine(output,"RebirthNurse_"+side+"Hand_Baseline.json"),side+"Hand",baselineSelections[side]);
                            }
                            finally{foreach(var pair in candidates)pair.Key.sharedMesh=pair.Value;}
                        }
                    }
                    finally{foreach(var pair in rotations)pair.Key.localRotation=pair.Value;}
                }
                Debug.Log("SAVED HAND REVIEW COMPLETE: "+output);
            }
            finally{if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}UnityEngine.Object.DestroyImmediate(lightObject);UnityEngine.Object.DestroyImmediate(cameraObject);}
        }

        private static void FrameHand(Camera camera,Transform skeleton,string side,float height)
        {
            Bounds bounds=HandBounds(skeleton,side,height);camera.fieldOfView=28;
            float distance=Mathf.Max(bounds.size.x/camera.aspect,bounds.size.y,height*.10f)*.62f/Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            camera.transform.position=bounds.center+new Vector3(0,.18f,.45f).normalized*distance;camera.transform.LookAt(bounds.center);
        }

        private static Bounds HandBounds(Transform skeleton,string side,float height)
        {
            Transform wrist=RebirthEntityRigBuilder.FindBone(skeleton,side+"Hand");var bounds=new Bounds(wrist.position,Vector3.one*height*.025f);
            foreach(var bone in wrist.GetComponentsInChildren<Transform>(true))bounds.Encapsulate(bone.position);bounds.Expand(height*.025f);return bounds;
        }

        private static void RenderHandDirections(Camera camera,RenderTexture target,RebirthEntityRigWorkspace workspace,string side,string pose,string output,ref Texture2D pixels)
        {
            Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
            Bounds bounds=HandBounds(workspace.generatedSkeleton,side,workspace.profile.estimatedHeight);
            float distance=Mathf.Max(bounds.size.x/camera.aspect,bounds.size.z,workspace.profile.estimatedHeight*.12f)*.65f/Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            try
            {
                foreach(bool palm in new[]{false,true})
                {
                    camera.transform.position=bounds.center+Vector3.up*(palm?-distance:distance);camera.transform.rotation=Quaternion.LookRotation(bounds.center-camera.transform.position,Vector3.forward);
                    string prefix=Path.Combine(output,"RebirthNurse_"+side+"Hand_"+(palm?"Palm":"Back")+"_"+pose);
                    RenderPosedLod(camera,target,prefix+".png",workspace.generatedSkeleton,workspace.sourceSkinnedMeshes,ref pixels);
                    var overlay=new GameObject("__HandBoneOverlay");Material material=LineMaterial(new Color(.05f,.9f,1f));
                    try
                    {
                        Transform wrist=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,side+"Hand");
                        foreach(var bone in wrist.GetComponentsInChildren<Transform>(true))if(bone!=wrist&&bone.parent!=null){AddLine(overlay.transform,material,new[]{bone.parent.position,bone.position},.001f,false);AddJoint(overlay.transform,material,bone.position,.0025f);}
                        RenderPosedLod(camera,target,prefix+"_Bones.png",workspace.generatedSkeleton,workspace.sourceSkinnedMeshes,ref pixels);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(overlay);UnityEngine.Object.DestroyImmediate(material);}
                }
            }
            finally{camera.transform.SetPositionAndRotation(position,rotation);}
        }

        private static void RenderPosedLod(Camera camera,RenderTexture rt,string path,Transform skeleton,IEnumerable<SkinnedMeshRenderer> renderers,ref Texture2D readback)
        {
            var displays=new List<GameObject>();
            var enabled=new Dictionary<SkinnedMeshRenderer,bool>();var group=skeleton.GetComponentInParent<LODGroup>();bool groupEnabled=group!=null&&group.enabled;if(group!=null)group.enabled=false;
            foreach(var renderer in skeleton.GetComponentsInChildren<SkinnedMeshRenderer>(true)){enabled[renderer]=renderer.enabled;renderer.enabled=false;}
            try
            {
                foreach(var renderer in renderers)
                {
                    var go=new GameObject("__LodPose");go.transform.SetParent(renderer.transform,false);go.AddComponent<MeshFilter>().sharedMesh=BakePoseForReview(renderer);
                    go.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;displays.Add(go);
                }
                RenderAndSave(camera,rt,path,ref readback);
            }
            finally
            {
                foreach(var go in displays){UnityEngine.Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(go);}
                foreach(var pair in enabled)pair.Key.enabled=pair.Value;if(group!=null)group.enabled=groupEnabled;
            }
        }

        public static void CreateOverlay(Transform skeleton,List<GameObject> temporary)
        {
            Material bones=LineMaterial(new Color(.05f,.9f,1f,1f));Material colliders=LineMaterial(new Color(1f,.49f,.08f,1f));
            var root=new GameObject("__RebirthNurseRigOverlay");temporary.Add(root);
            var deformBones=new HashSet<Transform>();
            foreach(var renderer in skeleton.GetComponentsInChildren<SkinnedMeshRenderer>(true))foreach(var bone in renderer.bones)if(bone!=null)deformBones.Add(bone);
            foreach(Transform t in deformBones)
            {
                if(t==skeleton)continue;
                if(IsIgnoredBranch(t,skeleton))continue;
                if(t.parent!=null&&deformBones.Contains(t.parent))AddLine(root.transform,bones,new[]{t.parent.position,t.position},.0032f,false);
                AddJoint(root.transform,bones,t.position,.006f);
            }
            foreach(CapsuleCollider c in skeleton.GetComponentsInChildren<CapsuleCollider>(true))
                AddCapsule(root.transform,colliders,c);
            foreach(BoxCollider c in skeleton.GetComponentsInChildren<BoxCollider>(true))
                AddBox(root.transform,colliders,c);
        }

        private static void AddBox(Transform root,Material material,BoxCollider collider)
        {
            var corners=new Vector3[8];
            for(int i=0;i<8;i++)corners[i]=collider.transform.TransformPoint(collider.center+Vector3.Scale(collider.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
            for(int i=0;i<8;i++)for(int bit=0;bit<3;bit++){int other=i^(1<<bit);if(other>i)AddLine(root,material,new[]{corners[i],corners[other]},.0025f,false);}
        }

        private static bool IsIgnoredBranch(Transform t,Transform skeleton)
        {
            for(Transform p=t;p!=null&&p!=skeleton;p=p.parent)
                if(p.name=="SourceVisual_Unrigged"||p.name=="PhysicsRig"||p.name=="LODParent")return true;
            return false;
        }

        private static void AddJoint(Transform root,Material material,Vector3 center,float radius)
        {
            const int steps=10;var points=new Vector3[steps+1];
            for(int i=0;i<=steps;i++){float a=Mathf.PI*2f*i/steps;points[i]=center+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0f);}
            AddLine(root,material,points,.0018f,true);
        }

        private static void AddCapsule(Transform root,Material material,CapsuleCollider collider)
        {
            Transform t=collider.transform;Vector3 axis=collider.direction==0?Vector3.right:collider.direction==1?Vector3.up:Vector3.forward;
            Vector3 worldAxis=t.TransformDirection(axis).normalized;Vector3 center=t.TransformPoint(collider.center);
            float scale=collider.direction==0?Mathf.Max(t.lossyScale.y,t.lossyScale.z):collider.direction==1?Mathf.Max(t.lossyScale.x,t.lossyScale.z):Mathf.Max(t.lossyScale.x,t.lossyScale.y);
            float radius=collider.radius*scale;float axisScale=collider.direction==0?t.lossyScale.x:collider.direction==1?t.lossyScale.y:t.lossyScale.z;
            float straight=Mathf.Max(0f,collider.height*axisScale*.5f-radius);Vector3 top=center+worldAxis*straight,bottom=center-worldAxis*straight;
            Vector3 u=Vector3.Cross(worldAxis,Mathf.Abs(Vector3.Dot(worldAxis,Vector3.up))>.94f?Vector3.right:Vector3.up).normalized;Vector3 v=Vector3.Cross(worldAxis,u).normalized;
            const int steps=24;var topRing=new Vector3[steps+1];var bottomRing=new Vector3[steps+1];
            for(int i=0;i<=steps;i++){float a=2f*Mathf.PI*i/steps;Vector3 radial=(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius;topRing[i]=top+radial;bottomRing[i]=bottom+radial;}
            AddLine(root,material,topRing,.0025f,true);AddLine(root,material,bottomRing,.0025f,true);
            for(int meridian=0;meridian<8;meridian++)
            {
                float a=2f*Mathf.PI*meridian/8f;Vector3 radial=(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius;
                var points=new List<Vector3>(25);
                for(int i=0;i<=8;i++){float theta=-Mathf.PI*.5f+(Mathf.PI*.5f*i/8f);points.Add(bottom+radial*Mathf.Cos(theta)+worldAxis*(radius*Mathf.Sin(theta)));}
                points.Add(top+radial);
                for(int i=1;i<=8;i++){float theta=Mathf.PI*.5f*i/8f;points.Add(top+radial*Mathf.Cos(theta)+worldAxis*(radius*Mathf.Sin(theta)));}
                AddLine(root,material,points.ToArray(),.0023f,false);
            }
        }

        private static void AddLine(Transform parent,Material material,Vector3[] points,float width,bool loop)
        {
            var go=new GameObject("__RigLine");go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=points.Length;line.SetPositions(points);line.startWidth=width;line.endWidth=width;line.loop=loop;line.material=material;line.numCapVertices=2;line.numCornerVertices=2;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
        }

        private static Material LineMaterial(Color color)
        {Shader shader=Shader.Find("Hidden/REBIRTH/RigOverlay");if(shader==null)shader=Shader.Find("Unlit/Color");var m=new Material(shader){color=color};return m;}
    }
}
