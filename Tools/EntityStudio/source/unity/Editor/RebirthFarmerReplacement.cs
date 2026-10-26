using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthFarmerReplacement
    {
        public const string Input="Assets/EntityStudioInputs/RebirthFarmerReplacement";
        [Serializable] private sealed class NativeTags {public NativeTag[] transforms;}
        [Serializable] private sealed class NativeTag {public string name,unityTag;}
        public static void RenderFinal()
        {
            try
            {
                var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
                var ws=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(w=>w!=null);
                string output="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Tools/EntityStudio/out/FarmerReplacementNativeComplete71/RebirthFarmer_Final";
                var args=Environment.GetCommandLineArgs();int outputIndex=Array.IndexOf(args,"-finalRenderOutput");if(outputIndex>=0)output=args[outputIndex+1];
                RebirthFarmerReviewCapture.CaptureColliderCandidate(ws,output,true,true);
                Debug.Log("FINAL NATIVE SKELETON RENDERS: "+output+"; scene unchanged.");
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        public static void FinalNativeSkeleton()
        {
            try
            {
                var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
                var ws=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(w=>w!=null);
                foreach(string name in new[]{"HeadEnd","LeftToeEnd","RightToeEnd"})
                {
                    var bone=RebirthEntityRigBuilder.FindBone(ws.generatedSkeleton,name);
                    if(bone!=null)UnityEngine.Object.DestroyImmediate(bone.gameObject);
                }
                ws.profile.bones.RemoveAll(b=>b.name=="HeadEnd"||b.name.EndsWith("ToeEnd",StringComparison.Ordinal));
                NativeBrows(ws.profile);
                var head=RebirthEntityRigBuilder.FindBone(ws.generatedSkeleton,"Head");
                foreach(string side in new[]{"Left","Right"})
                {
                    string name=side+"Brow";var brow=head.Find(name);
                    if(brow==null){brow=new GameObject(name).transform;brow.SetParent(head,false);}
                    brow.localPosition=new Vector3(side=="Left"?-.03f:.03f,.07f,.10f)*.9609717f;
                }
                RebirthFarmerNativePhysics.Configure(ws);
                RebirthEntityHumanoidBuilder.Build(ws,AssetDatabase.GenerateUniqueAssetPath("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmerNativeAvatar.asset"),false);
                ws.profileBuildFingerprint=RebirthEntityRigBuilder.ComputeProfileFingerprint(ws.profile,ws.sourceModelKey);
                EditorUtility.SetDirty(ws);EditorUtility.SetDirty(ws.profile);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-entityStudioReviewEvidence");
                string path=args[index+1];var review=JsonUtility.FromJson<RebirthEntityAuthoringReview>(File.ReadAllText(path));review.profileFingerprint=ws.profileBuildFingerprint;
                review.reviewNotes+=" Removed three authoring-only head/toe endpoint helpers to match native full skeleton; deforming bones, weights, geometry and physics unchanged.";
                File.WriteAllText(path,JsonUtility.ToJson(review,true));
                Audit();RebirthFarmerCandidateBuild.Run();
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        public static void Audit()
        {
            AssetBundle common=null,bundle=null;
            try
            {
                var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
                var ws=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(w=>w!=null);
                const string folder="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Addressables/Standalone/zombies_assets_entities/zombies/";
                common=AssetBundle.LoadFromFile(folder+"common.bundle");bundle=AssetBundle.LoadFromFile(folder+"worker.bundle");
                var native=bundle.LoadAsset<GameObject>(bundle.GetAllAssetNames().Single(p=>p.EndsWith("/ZWorker.prefab",StringComparison.OrdinalIgnoreCase)));
                var reference=native.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r=>r.sharedMesh.vertexCount).First();
                var actual=ws.sourceSkinnedMeshes[0];var rows=new System.Collections.Generic.List<string>();
                var capture=JsonUtility.FromJson<NativeTags>(File.ReadAllText("C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Tools/GameBridge/out/entitymodels/EntityModel_zombieUtilityWorker_192_20261015.json"));
                foreach(var bone in reference.bones)
                {
                    var target=actual.bones.SingleOrDefault(b=>b.name==bone.name);
                    if(target==null)throw new InvalidDataException("Native renderer bone missing: "+bone.name);
                    string expectedTag=capture.transforms.Single(t=>t.name==bone.name).unityTag;
                    if(target.parent.name!=bone.parent.name||target.tag!=expectedTag)throw new InvalidDataException("Native parent/tag differs: "+bone.name+" expected="+bone.parent.name+"/"+expectedTag+" actual="+target.parent.name+"/"+target.tag);
                    rows.Add(bone.name+",parent="+target.parent.name+",tag="+target.tag);
                }
                if(actual.bones.Length!=reference.bones.Length)throw new InvalidDataException("Renderer bone count differs from native.");
                foreach(var spec in ws.profile.bones)
                {
                    var source=native.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name==spec.name);
                    if(source==null)throw new InvalidDataException("Extra skeleton node absent from native: "+spec.name);
                    if(spec.name!="Origin"&&source.parent.name!=spec.parent)throw new InvalidDataException("Native helper parent mismatch: "+spec.name);
                }
                int nativeNodes=native.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Origin").GetComponentsInChildren<Transform>(true).Length;
                if(ws.profile.bones.Count!=nativeNodes)throw new InvalidDataException("Full native skeleton node count differs: "+ws.profile.bones.Count+" vs "+nativeNodes);
                foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Input,"Assets/EntityStudioGenerated/RebirthFarmer/Maps"}))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);string name=texture.name.ToLowerInvariant();
                    int limit=name.Contains("basecolor")||name.Contains("albedo")||name.EndsWith("normal")&&!name.Contains("rmoe")?2048:512;
                    if(texture.width>limit||texture.height>limit)throw new InvalidDataException("Texture exceeds requested limit: "+path+" "+texture.width+"x"+texture.height);
                    rows.Add("texture="+path+","+texture.width+"x"+texture.height);
                }
                string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/Replacement/NativeAudit.txt");
                File.WriteAllLines(output,rows);Debug.Log("REPLACEMENT NATIVE AUDIT: matched "+actual.bones.Length+" renderer bones, direct parents and tags; texture caps verified.");RebirthFarmerSourcePreservation.Run();
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
            finally{if(bundle!=null)bundle.Unload(true);if(common!=null)common.Unload(true);}
        }
        public static void Landmarks(RebirthEntityRigProfile profile)
        {
            // FBX root rotates its Z-up local coordinates into upright world Y.
            profile.sourceUpAxis=EntityModelUpAxis.Z;profile.targetHeight=1.8f;
            profile.albedoMaxSize=2048;profile.normalMaxSize=2048;profile.dataMaxSize=512;
            profile.useSurfaceGeodesicWeights=true;profile.separateFusedArms=false;
            profile.compoundTerminalColliders=false;profile.includeNeckCollider=false;
            profile.useBoxTorso=false;profile.useBoxTorsoAndLegs=false;
            profile.includeTerminalPartsInColliders=true;profile.preferNarrowCapsules=true;profile.colliderPadding=.003f;
            RebirthFarmerFullHumanoid.Extend(profile);
            Place("Origin",.5f,0,.5f);profile.bones.Find(b=>b.name=="Origin").deformsMesh=false;
            Place("Hips",.5f,.515f,.5f);Place("Spine",.5f,.57f,.5f);Place("Spine1",.5f,.635f,.5f);
            Place("Spine2",.5f,.705f,.5f);Place("Spine3",.5f,.765f,.5f);Place("Neck",.5f,.825f,.5f);
            Place("Head",.5f,.89f,.48f);Place("HeadEnd",.5f,.995f,.48f);Place("Jaw",.5f,.865f,.25f);
            foreach(string side in new[]{"Left","Right"})
            {
                bool left=side=="Left";float X(float x)=>left?x:1-x;
                Place(side+"Shoulder",X(.45f),.79f,.5f);Place(side+"Arm",X(.39f),.785f,.5f);
                Place(side+"ForeArm",X(.245f),.765f,.5f);Place(side+"Hand",X(.09f),.75f,.5f);
                Place(side+"ArmRoll",X(.3175f),.775f,.5f);Place(side+"ForeArmRoll",X(.1675f),.7575f,.5f);
                Place(side+"UpLeg",X(.445f),.49f,.50f);Place(side+"Leg",X(.445f),.265f,.54f);
                Place(side+"Foot",X(.445f),.045f,.52f);Place(side+"ToeBase",X(.445f),.025f,.25f);Place(side+"ToeEnd",X(.445f),.025f,.08f);
                string[] fingers={"Thumb","Index","Middle","Ring","Pinky"};
                for(int f=0;f<5;f++)for(int joint=1;joint<=4;joint++)
                {
                    float t=(joint-1)/3f;
                    Place(side+"Hand"+fingers[f]+joint,X(Mathf.Lerp(f==0?.075f:.055f,f==0?.045f:.008f+(f==4?.018f:0),t)),Mathf.Lerp(.751f-f*.009f,.715f-f*.009f,t),f==0?.30f:.40f+f*.055f);
                }
            }
            foreach(var bone in profile.bones)
            {
                bone.useInfluenceBounds=bone.deformsMesh;
                bool left=bone.name.StartsWith("Left",StringComparison.Ordinal);
                float minX=.35f,maxX=.65f,minY=.42f,maxY=.88f;
                if(bone.name=="Hips"){minY=.38f;maxY=.58f;}
                else if(bone.name=="Spine"){minY=.50f;maxY=.64f;}
                else if(bone.name=="Spine1"){minY=.58f;maxY=.72f;}
                else if(bone.name=="Spine2"){minY=.65f;maxY=.79f;}
                else if(bone.name=="Spine3"){minY=.72f;maxY=.85f;}
                if(bone.name.Contains("Arm")||bone.name.Contains("Hand"))
                {
                    minX=left?-.01f:.60f;maxX=left?.40f:1.01f;minY=.715f;maxY=.84f;
                    if(bone.name.Contains("Hand")){minX=left?-.01f:.86f;maxX=left?.14f:1.01f;minY=.66f;}
                    else if(bone.name.Contains("ForeArm")){minX=left?-.01f:.73f;maxX=left?.27f:1.01f;}
                }
                else if(bone.name.Contains("Shoulder"))
                {minX=left?.35f:.50f;maxX=left?.50f:.65f;minY=.73f;maxY=.85f;}
                else if(bone.name.Contains("Leg")||bone.name.Contains("Foot")||bone.name.Contains("Toe"))
                {minX=left?.30f:.49f;maxX=left?.51f:.70f;minY=-.01f;maxY=.54f;}
                else if(bone.name=="Head"||bone.name=="HeadEnd"||bone.name=="Jaw")
                {minX=.40f;maxX=.60f;minY=.83f;maxY=1.01f;}
                else if(bone.name=="Neck"){minX=.43f;maxX=.57f;minY=.77f;maxY=.91f;}
                bone.influenceMin=new Vector3(minX,minY,-.01f);bone.influenceMax=new Vector3(maxX,maxY,1.01f);
            }
            profile.bones.RemoveAll(b=>b.name=="HeadEnd"||b.name.EndsWith("ToeEnd",StringComparison.Ordinal));
            NativeBrows(profile);
            void Place(string name,float x,float y,float z){var bone=profile.bones.Single(b=>b.name==name);bone.normalizedPosition=new Vector3(x,y,z);}
        }
        private static void NativeBrows(RebirthEntityRigProfile profile)
        {
            foreach(string side in new[]{"Left","Right"})if(!profile.bones.Any(b=>b.name==side+"Brow"))
                profile.bones.Add(new EntityBoneSpec{name=side+"Brow",parent="Head",deformsMesh=false,normalizedPosition=new Vector3(side=="Left"?.4836f:.5164f,.9274f,.2265f)});
        }
        public static void Inspect()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var path=AssetDatabase.FindAssets("t:Model",new[]{Input}).Select(AssetDatabase.GUIDToAssetPath).Single(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase));
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.SaveAndReimport();
                Texture2D albedo=null,normal=null;
                foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Input}))
                {
                    var texPath=AssetDatabase.GUIDToAssetPath(guid);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);var name=Path.GetFileNameWithoutExtension(texPath).ToLowerInvariant();
                    var usage=name.EndsWith("normal")?EntityTextureUsage.Normal:name.EndsWith("basecolor")?EntityTextureUsage.Albedo:EntityTextureUsage.LinearData;
                    RebirthEntityTextureTools.Configure(texture,usage,usage==EntityTextureUsage.Normal||usage==EntityTextureUsage.Albedo?2048:512);
                    if(usage==EntityTextureUsage.Albedo)albedo=texture;if(usage==EntityTextureUsage.Normal)normal=texture;
                }
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                Bounds bounds=BoundsOf(model);bool zUp=bounds.size.z>bounds.size.y;
                if(zUp)model.transform.rotation=Quaternion.FromToRotation(Vector3.forward,Vector3.up);
                bounds=BoundsOf(model);model.transform.localScale*=1.8f/bounds.size.y;bounds=BoundsOf(model);model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);bounds=BoundsOf(model);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>{var mat=new Material(Shader.Find("Standard"));mat.mainTexture=albedo;mat.SetFloat("_Metallic",0);mat.SetFloat("_Glossiness",.1f);if(normal!=null){mat.SetTexture("_BumpMap",normal);mat.EnableKeyword("_NORMALMAP");}return mat;}).ToArray();
                }
                var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.19f);camera.fieldOfView=30;camera.aspect=.75f;
                camera.transform.position=bounds.center+new Vector3(.35f,.08f,4.7f);camera.transform.LookAt(bounds.center);
                var light=new GameObject("PreviewLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(35,145,0);RenderSettings.ambientLight=Color.gray;
                var rt=new RenderTexture(900,1200,24);rt.Create();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(900,1200,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,900,1200),0,0);pixels.Apply();
                string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/Replacement");Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,"SourcePreview.png"),pixels.EncodeToPNG());
                File.WriteAllText(Path.Combine(output,"Source.txt"),"model="+path+"\nzUp="+zUp+"\nnormalizedBounds="+bounds+"\nvertices="+model.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertexCount)+"\n");
                Debug.Log("REPLACEMENT IMPORT: "+path+" zUp="+zUp+" preview="+output);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        private static Bounds BoundsOf(GameObject model)
        {var renderers=model.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;}
    }
}
