using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RebirthEntityStudio
{
    public static class RebirthFarmerModelInspection
    {
        private static string InputFolder="Assets/EntityStudioInputs/RebirthFarmer";
        private static string OutputFile="Logs/EntityStudioValidation/RebirthFarmerModelInspection.json";

        public static void PrepareReviewScene()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                string modelPath=AssetDatabase.FindAssets("t:Model",new[]{InputFolder}).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase));
                GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if(model==null)throw new InvalidDataException("Could not load the RebirthFarmer FBX.");
                string folder="Assets/EntityStudioGenerated/RebirthFarmer";
                EnsureFolder(folder);
                string profilePath=folder+"/RebirthFarmer_RigProfile.asset";
                string scenePath=folder+"/RebirthFarmer_RigReview.unity";
                if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(profilePath)!=null||File.Exists(Path.GetFullPath(scenePath)))
                    throw new IOException("RebirthFarmer profile or review scene already exists; refusing to overwrite.");
                var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
                profile.entityClass="RebirthFarmer";profile.prefabFamily="zombieTemplateMale";
                profile.sourceUpAxis=EntityModelUpAxis.Z;
                profile.sourceModelPath=modelPath;profile.sourceTexturePath=InputFolder;
                profile.characterShader=Shader.Find("Standard");
                profile.InitializeBipedDefaults();
                ApplyRebirthFarmerLandmarks(profile);
                profile.bakedAlbedo=FindTexture("basecolor");profile.normalMap=FindTexture("normal");
                // The package supplies separate metallic/roughness and an RM image. Keep the
                // latter archived as input until its channel contract is verified for 7DTD.
                profile.rmoeMap=null;
                AssetDatabase.CreateAsset(profile,profilePath);
                RebirthEntityTextureTools.ConfigureProfileMaps(profile);
                Texture2D metal=FindTexture("metallic"),roughness=FindTexture("roughness");
                if(metal!=null)RebirthEntityTextureTools.Configure(metal,EntityTextureUsage.LinearData);
                if(roughness!=null)RebirthEntityTextureTools.Configure(roughness,EntityTextureUsage.LinearData);
                Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var workspace=RebirthEntityRigBuilder.CreateWorkspace(model,profile);
                Selection.activeGameObject=workspace.gameObject;
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,scenePath))throw new IOException("Unity failed to save the RebirthFarmer rig review scene.");
                AssetDatabase.SaveAssets();AssetDatabase.Refresh();
                Debug.Log("RebirthFarmer rig review scene saved: "+scenePath+"; mesh renderers="+profile.sourceSkinnedMeshes.Count+
                    ", skeleton nodes="+workspace.generatedSkeleton.GetComponentsInChildren<Transform>(true).Length+
                    ", ragdoll colliders="+workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true).Length+
                    ", LODs="+(workspace.lodGroup!=null?workspace.lodGroup.GetLODs().Length:0));
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }

        public static void Run()
        {
            try
            {
                string[] args=Environment.GetCommandLineArgs();
                for(int i=0;i<args.Length;i++)
                {
                    if(args[i].StartsWith("-inputFolder=",StringComparison.Ordinal))InputFolder=args[i].Substring("-inputFolder=".Length).Trim('"');
                    else if(args[i]=="-inputFolder"&&i+1<args.Length)InputFolder=args[++i];
                    else if(args[i].StartsWith("-outputFile=",StringComparison.Ordinal))OutputFile=args[i].Substring("-outputFile=".Length).Trim('"');
                    else if(args[i]=="-outputFile"&&i+1<args.Length)OutputFile=args[++i].Trim('"');
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                string modelPath=AssetDatabase.FindAssets("t:Model",new[]{InputFolder}).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase));
                if(string.IsNullOrEmpty(modelPath))throw new FileNotFoundException("No FBX model found under "+InputFolder);
                GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if(model==null)throw new InvalidDataException("Unity imported no root GameObject from "+modelPath);
                var rows=new List<string>();Bounds worldBounds=new Bounds();bool hasBounds=false;int vertexCount=0,triangles=0,skinned=0;
                foreach(Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    var mf=t.GetComponent<MeshFilter>();var mr=t.GetComponent<MeshRenderer>();var smr=t.GetComponent<SkinnedMeshRenderer>();Mesh mesh=mf!=null?mf.sharedMesh:smr!=null?smr.sharedMesh:null;
                    if(mesh==null)continue;
                    vertexCount+=mesh.vertexCount;for(int sub=0;sub<mesh.subMeshCount;sub++)triangles+=mesh.GetTriangles(sub).Length/3;if(smr!=null)skinned++;
                    var renderer=(Renderer)(smr!=null?(Renderer)smr:mr);
                    Bounds b=renderer!=null?renderer.bounds:mesh.bounds;if(!hasBounds){worldBounds=b;hasBounds=true;}else worldBounds.Encapsulate(b);
                    rows.Add("{\"name\":"+J(t.name)+",\"path\":"+J(AnimationUtility.CalculateTransformPath(t,model.transform))+",\"vertices\":"+mesh.vertexCount+",\"submeshes\":"+mesh.subMeshCount+",\"localBoundsCenter\":"+V(mesh.bounds.center)+",\"localBoundsSize\":"+V(mesh.bounds.size)+",\"materials\":["+string.Join(",",renderer.sharedMaterials.Select(m=>m==null?"null":J(MaterialData(m))).ToArray())+"]}");
                }
                string[] texturePaths=AssetDatabase.FindAssets("t:Texture2D",new[]{InputFolder}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
                var textureRows=texturePaths.Select(p=>{var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(p);var imp=AssetImporter.GetAtPath(p) as TextureImporter;return "{\"path\":"+J(p)+",\"width\":"+(tex!=null?tex.width:0)+",\"height\":"+(tex!=null?tex.height:0)+",\"importerType\":"+J(imp!=null?imp.textureType.ToString():"<none>")+",\"sRGB\":"+(imp!=null&&imp.sRGBTexture?"true":"false")+"}";}).ToArray();
                string json="{\"modelPath\":"+J(modelPath)+",\"rootName\":"+J(model.name)+",\"vertexCount\":"+vertexCount+",\"triangleCount\":"+triangles+",\"skinnedRendererCount\":"+skinned+",\"worldBoundsCenter\":"+(hasBounds?V(worldBounds.center):"null")+",\"worldBoundsSize\":"+(hasBounds?V(worldBounds.size):"null")+",\"renderers\":["+string.Join(",",rows.ToArray())+"],\"textures\":["+string.Join(",",textureRows)+"]}";
                string absolute=Path.GetFullPath(OutputFile);Directory.CreateDirectory(Path.GetDirectoryName(absolute));File.WriteAllText(absolute,json);
                Debug.Log("RebirthFarmer inspection written: "+absolute+" vertices="+vertexCount+" triangles="+triangles+" renderers="+rows.Count);
                if(vertexCount==0||rows.Count==0)throw new InvalidDataException("FBX import produced no renderable mesh geometry.");
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }

        private static string MaterialData(Material m)
        {
            var props=new List<string>();foreach(string n in new[]{"_MainTex","_BaseMap","_Color","_BaseColor","_BumpMap","_NormalMap","_MetallicGlossMap","_MetallicMap","_OcclusionMap","_EmissionMap","_RoughnessMap","_MaskMap","_RMOEMap"})
            {
                if(m.HasProperty(n)){Texture tex=m.GetTexture(n);props.Add(J(n)+":"+(tex!=null?J(AssetDatabase.GetAssetPath(tex)):"<null>"));}
            }
            return m.name+" shader="+(m.shader!=null?m.shader.name:"<none>")+" props="+string.Join(";",props.ToArray());
        }
        private static Texture2D FindTexture(string hint)
        {string guid=AssetDatabase.FindAssets("t:Texture2D",new[]{InputFolder}).FirstOrDefault(g=>AssetDatabase.GUIDToAssetPath(g).IndexOf(hint,StringComparison.OrdinalIgnoreCase)>=0);return string.IsNullOrEmpty(guid)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));}
        private static void EnsureFolder(string folder)
        {if(AssetDatabase.IsValidFolder(folder))return;int slash=folder.LastIndexOf('/');string parent=folder.Substring(0,slash),name=folder.Substring(slash+1);EnsureFolder(parent);AssetDatabase.CreateFolder(parent,name);}
        private static string V(Vector3 v){return "{\"x\":"+v.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"y\":"+v.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"z\":"+v.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"}";}
        private static string J(string s){return "\""+(s??"").Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\"";}

        public static void ApplyRebirthFarmerLandmarks(RebirthEntityRigProfile profile)
        {
            if(profile==null||profile.bones==null)throw new ArgumentNullException("profile");
            profile.bones.Find(bone=>bone.name=="Origin").deformsMesh=false;
            profile.targetHeight=1.8f;
            profile.useSurfaceGeodesicWeights=true;
            profile.separateFusedArms=false;
            profile.useBoxTorsoAndLegs=false;profile.useBoxTorso=false;profile.includeNeckCollider=false;
            profile.includeTerminalPartsInColliders=true;profile.preferNarrowCapsules=true;
            profile.bones.Find(bone=>bone.name=="Neck").gameTag="E_BP_Body";
            profile.colliderPadding=.005f;
            Place("Hips",.50f,.52f);Place("Spine",.50f,.57f);Place("Spine1",.50f,.62f);
            Place("Spine2",.50f,.69f);Place("Spine3",.50f,.77f);Place("Neck",.50f,.85f);Place("Head",.50f,.89f);
            Place("LeftShoulder",.22f,.81f);Place("LeftArm",.10f,.79f);Place("LeftForeArm",.07f,.635f);Place("LeftHand",.08f,.535f);
            Place("RightShoulder",.78f,.81f);Place("RightArm",.90f,.79f);Place("RightForeArm",.93f,.635f);Place("RightHand",.92f,.535f);
            Place("LeftUpLeg",.32f,.50f);Place("LeftLeg",.31f,.26f);Place("LeftFoot",.30f,.055f);
            Place("RightUpLeg",.68f,.50f);Place("RightLeg",.69f,.26f);Place("RightFoot",.70f,.055f);
            foreach(var bone in profile.bones)
            {
                Vector3 position=bone.normalizedPosition;
                if(bone.name.Contains("UpLeg"))position.z=.50f;
                else if(bone.name.Contains("Leg"))position.z=.58f;
                else if(bone.name.Contains("Foot"))position.z=.52f;
                else if(bone.name.Contains("Hand"))position.z=.465f;
                else if(bone.name.Contains("Arm"))position.z=.60f;
                else if(bone.name=="Head")position.z=.40f;
                bone.normalizedPosition=position;
                if(!bone.deformsMesh)continue;
                bone.useInfluenceBounds=true;
                float minX=-.01f,maxX=1.01f,minY=-.01f,maxY=1.01f;
                if(bone.name=="Hips"){minX=.17f;maxX=.83f;minY=.40f;maxY=.60f;}
                else if(bone.name.StartsWith("Spine",StringComparison.Ordinal))
                {
                    minX=.17f;maxX=.83f;
                    if(bone.name=="Spine"){minY=.53f;maxY=.65f;}
                    else if(bone.name=="Spine1"){minY=.58f;maxY=.72f;}
                    else if(bone.name=="Spine2"){minY=.65f;maxY=.80f;}
                    else {minY=.73f;maxY=.88f;}
                }
                else if(bone.name=="Neck"){minX=.33f;maxX=.67f;minY=.80f;maxY=.93f;}
                else if(bone.name=="Head"){minX=.28f;maxX=.72f;minY=.88f;}
                else if(bone.name.Contains("Shoulder"))
                {minX=bone.name.StartsWith("Left",StringComparison.Ordinal)?.08f:.64f;maxX=bone.name.StartsWith("Left",StringComparison.Ordinal)?.36f:.92f;minY=.75f;maxY=.87f;}
                else if(bone.name.Contains("Arm")||bone.name.Contains("Hand"))
                {
                    bool left=bone.name.StartsWith("Left",StringComparison.Ordinal);
                    minX=left?-.01f:.83f;maxX=left?.17f:1.01f;minY=.39f;maxY=.86f;
                    if(bone.name.Contains("ForeArm")){minY=.43f;maxY=.68f;}
                    else if(bone.name.Contains("Hand")){minY=.35f;maxY=.53f;}
                    else minY=.60f;
                }
                else if(bone.name.Contains("Leg")||bone.name.Contains("Foot"))
                {
                    bool left=bone.name.StartsWith("Left",StringComparison.Ordinal);
                    minX=left?.15f:.49f;maxX=left?.51f:.85f;maxY=.51f;
                    if(bone.name.Contains("UpLeg"))minY=.23f;
                    else if(bone.name.Contains("Foot")){maxY=.12f;minX=left?-.01f:.49f;maxX=left?.51f:1.01f;}
                    else {maxY=.29f;minX=left?-.01f:.49f;maxX=left?.51f:1.01f;}
                }
                bone.influenceMin=new Vector3(minX,minY,-.01f);bone.influenceMax=new Vector3(maxX,maxY,1.01f);
            }
            RebirthFarmerFullHumanoid.Extend(profile);
            void Place(string name,float x,float y)
            {
                EntityBoneSpec bone=profile.bones.Find(candidate=>candidate!=null&&candidate.name==name);
                if(bone==null)throw new InvalidDataException("RebirthFarmer profile has no bone named "+name);
                bone.normalizedPosition=new Vector3(x,y,.5f);
            }
        }
    }
}
