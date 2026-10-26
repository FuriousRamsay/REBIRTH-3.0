using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    // A concrete authoring review is supplied as data, never inferred merely
    // because Unity compiled. Exact target-game verification remains separate.
    public static class RebirthNurseCandidateBuild
    {
        public static void FinalizeModel(){RebirthNurseReplacement.PrepareNativeHierarchy();RebirthNurseReplacement.Audit();Run();RebirthNurseReplacement.RenderFinal();}
        public static void Run()
        {
            try
            {
                string reviewPath=Argument("-entityStudioReviewEvidence");
                string output=Argument("-entityStudioCandidateOutput");
                string vanillaConfig=Argument("-entityStudioVanillaEntityConfig");
                if(!File.Exists(reviewPath))throw new FileNotFoundException("Authoring review JSON is required.",reviewPath);
                var review=JsonUtility.FromJson<RebirthEntityAuthoringReview>(File.ReadAllText(reviewPath));
                if(review==null||string.IsNullOrWhiteSpace(review.reviewNotes))
                    throw new InvalidDataException("Authoring review notes are required, including any unfinished work.");
                foreach(string filename in new[]{"RebirthNurse_Model.png","RebirthNurse_Skeleton_Colliders.png","RebirthNurse_ElbowPose.png","RebirthNurse_RagdollPose.png","RebirthNurse_PhysicsReview.json","RebirthNurse_LodMetrics.json"})
                    if(!File.Exists(Path.Combine(review.evidenceDirectory,filename)))throw new FileNotFoundException("Review evidence is missing: "+filename);
                for(int level=0;level<4;level++)
                    foreach(string prefix in new[]{"RebirthNurse_LOD","RebirthNurse_KneeShoulderPose_LOD"})
                        if(!File.Exists(Path.Combine(review.evidenceDirectory,prefix+level+".png")))throw new FileNotFoundException("LOD review evidence is missing: "+prefix+level);
                var physics=JsonUtility.FromJson<RebirthEntityPhysicsReview.Result>(File.ReadAllText(Path.Combine(review.evidenceDirectory,"RebirthNurse_PhysicsReview.json")));
                if(physics==null||physics.bodies<11||physics.joints!=physics.bodies-1||physics.maxJointSeparation>.25f||physics.maxSpeed>20f||physics.lowestColliderPoint<-.15f||physics.pelvisVerticalDrop<.1f)
                    throw new InvalidDataException("Ragdoll evidence does not pass the authoring stability checks.");
                var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthNurse/RebirthNurse_RigReview.unity",OpenSceneMode.Single);
                var workspace=scene.GetRootGameObjects().Select(root=>root.GetComponent<RebirthEntityRigWorkspace>()).Single(value=>value!=null);
                RebirthEntityRigBuilder.BindWorkspaceReferences(workspace);
                if(workspace.profile.entityClass!="RebirthNurse"||review.profileFingerprint!=workspace.profileBuildFingerprint||RebirthEntityRigValidator.HasStaleInputs(workspace))
                    throw new InvalidDataException("The authoring review must identify the current RebirthNurse build fingerprint.");
                var profile=workspace.profile;profile.reviewedSkeletonMapping=review.skeletonMapping;profile.reviewedColliderFit=review.colliderFit;profile.reviewedLods=review.lods;profile.reviewedMaterials=review.materials;
                EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
                string prefabPath="Assets/EntityStudioGenerated/Candidates/"+Guid.NewGuid().ToString("N")+"/RebirthNurse.prefab";
                RebirthEntityPrefabExporter.SaveGameTestCandidate(workspace,prefabPath);
                var receipt=RebirthEntityBundleBuilder.BuildAndVerify(prefabPath,output);
                File.WriteAllText(Path.Combine(output,"entityclasses.xml"),RebirthNurseEntityConfig.BuildPatch(receipt,vanillaConfig));
                File.Copy(reviewPath,Path.Combine(output,"authoring-review.json"));
                Debug.Log("REBIRTH FARMER GAME TEST CANDIDATE: "+JsonUtility.ToJson(receipt));
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        public static void DeliveryAudit()
        {
            try
            {
                string output=Argument("-entityStudioCandidateOutput");var receipt=JsonUtility.FromJson<RebirthEntityBundleReceipt>(File.ReadAllText(Path.Combine(output,"receipt.json")));
                var bundle=AssetBundle.LoadFromFile(receipt.bundlePath);
                try
                {
                    var rows=new System.Collections.Generic.List<string>();
                    var textureSet=new System.Collections.Generic.HashSet<Texture2D>(bundle.LoadAllAssets<Texture2D>());
                    foreach(var material in bundle.LoadAllAssets<Material>())foreach(string property in material.GetTexturePropertyNames()){var tex=material.GetTexture(property) as Texture2D;if(tex!=null)textureSet.Add(tex);}
                    foreach(var texture in textureSet)
                    {
                        string name=texture.name.ToLowerInvariant();int limit=name=="color"||name.Contains("albedo")||name=="normal"?2048:512;
                        if(texture.width>limit||texture.height>limit)throw new InvalidDataException("Bundled texture exceeds cap: "+texture.name);
                        rows.Add(texture.name+","+texture.width+"x"+texture.height);
                    }
                    var model=bundle.LoadAsset<GameObject>("RebirthNurse");
                    if(model.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="GoreAnchors"||t.name.StartsWith("L_")&&t.name.EndsWith("Gore")))throw new InvalidDataException("Generic authoring gore helper was exported.");
                    File.WriteAllLines(Path.Combine(output,"bundled-textures.txt"),rows);
                }
                finally{bundle.Unload(true);}
                var assets=AssetDatabase.GetDependencies(new[]{"Assets/EntityStudioGenerated/RebirthNurse/RebirthNurse_RigReview.unity",receipt.prefabAssetPath},true).Where(p=>p.StartsWith("Assets/"));
                var files=new System.Collections.Generic.HashSet<string>(assets);
                foreach(string folder in new[]{"Assets/REBIRTHEntityStudio","Assets/EntityStudioInputs/RebirthNurseReplacement"})foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))files.Add(file.Replace('\\','/'));
                foreach(string file in files.ToArray())if(File.Exists(file+".meta"))files.Add(file+".meta");
                foreach(string folder in new[]{"ProjectSettings","Packages"})foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))files.Add(file.Replace('\\','/'));
                File.WriteAllLines(Path.Combine(output,"authoring-project-files.txt"),files.Where(File.Exists).OrderBy(p=>p));
                Debug.Log("NURSE DELIVERY AUDIT: bundled texture caps and absence of generic gore helpers verified; authoring dependency list saved.");
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        static string Argument(string name)
        {
            string[] args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]==name)return args[i+1];
            throw new ArgumentException("Required batch argument: "+name);
        }
    }
}
