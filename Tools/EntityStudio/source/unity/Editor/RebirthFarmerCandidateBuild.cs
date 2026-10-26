using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    [Serializable]
    public sealed class RebirthEntityAuthoringReview
    {
        public string profileFingerprint,evidenceDirectory,reviewNotes;
        public bool skeletonMapping,colliderFit,lods,materials;
    }

    // A concrete authoring review is supplied as data, never inferred merely
    // because Unity compiled. Exact target-game verification remains separate.
    public static class RebirthFarmerCandidateBuild
    {
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
                foreach(string filename in new[]{"RebirthFarmer_Model.png","RebirthFarmer_Skeleton_Colliders.png","RebirthFarmer_ElbowPose.png","RebirthFarmer_RagdollPose.png","RebirthFarmer_PhysicsReview.json","RebirthFarmer_LodMetrics.json"})
                    if(!File.Exists(Path.Combine(review.evidenceDirectory,filename)))throw new FileNotFoundException("Review evidence is missing: "+filename);
                for(int level=0;level<4;level++)
                    foreach(string prefix in new[]{"RebirthFarmer_LOD","RebirthFarmer_KneeShoulderPose_LOD"})
                        if(!File.Exists(Path.Combine(review.evidenceDirectory,prefix+level+".png")))throw new FileNotFoundException("LOD review evidence is missing: "+prefix+level);
                var physics=JsonUtility.FromJson<RebirthEntityPhysicsReview.Result>(File.ReadAllText(Path.Combine(review.evidenceDirectory,"RebirthFarmer_PhysicsReview.json")));
                if(physics==null||physics.bodies<11||physics.joints!=physics.bodies-1||physics.maxJointSeparation>.25f||physics.maxSpeed>20f||physics.lowestColliderPoint<-.15f||physics.pelvisVerticalDrop<.1f)
                    throw new InvalidDataException("Ragdoll evidence does not pass the authoring stability checks.");
                var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity",OpenSceneMode.Single);
                var workspace=scene.GetRootGameObjects().Select(root=>root.GetComponent<RebirthEntityRigWorkspace>()).Single(value=>value!=null);
                RebirthEntityRigBuilder.BindWorkspaceReferences(workspace);
                if(workspace.profile.entityClass!="RebirthFarmer"||review.profileFingerprint!=workspace.profileBuildFingerprint||RebirthEntityRigValidator.HasStaleInputs(workspace))
                    throw new InvalidDataException("The authoring review must identify the current RebirthFarmer build fingerprint.");
                var profile=workspace.profile;profile.reviewedSkeletonMapping=review.skeletonMapping;profile.reviewedColliderFit=review.colliderFit;profile.reviewedLods=review.lods;profile.reviewedMaterials=review.materials;
                EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
                string prefabPath="Assets/EntityStudioGenerated/Candidates/"+Guid.NewGuid().ToString("N")+"/RebirthFarmer.prefab";
                RebirthEntityPrefabExporter.SaveGameTestCandidate(workspace,prefabPath);
                var receipt=RebirthEntityBundleBuilder.BuildAndVerify(prefabPath,output);
                File.WriteAllText(Path.Combine(output,"entityclasses.xml"),RebirthFarmerEntityConfig.BuildPatch(receipt,vanillaConfig));
                File.Copy(reviewPath,Path.Combine(output,"authoring-review.json"));
                Debug.Log("REBIRTH FARMER GAME TEST CANDIDATE: "+JsonUtility.ToJson(receipt));
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
