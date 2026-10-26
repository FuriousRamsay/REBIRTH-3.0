using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityColliderCandidate
    {
        public static string Signature(RebirthEntityRigWorkspace workspace)
        {
            var text=new StringBuilder("posed-region-union-v1|");
            foreach(var renderer in workspace.sourceSkinnedMeshes)
            {
                if(renderer==null||renderer.sharedMesh==null){text.Append("missing mesh|");continue;}
                string path=AssetDatabase.GetAssetPath(renderer.sharedMesh);
                text.Append(path).Append('|').Append(AssetDatabase.GetAssetDependencyHash(path)).Append('|');Matrix(renderer.transform.localToWorldMatrix);
                foreach(var bone in renderer.bones){if(bone==null)text.Append("missing bone|");else Matrix(bone.localToWorldMatrix);}
            }
            foreach(var capsule in workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true))
            {
                text.Append(AnimationUtility.CalculateTransformPath(capsule.transform,workspace.generatedSkeleton)).Append('|').Append(capsule.tag).Append('|').Append(capsule.direction).Append('|');
                Matrix(capsule.transform.localToWorldMatrix);Number(capsule.center.x);Number(capsule.center.y);Number(capsule.center.z);Number(capsule.radius);Number(capsule.height);
            }
            return Hash128.Compute(text.ToString()).ToString();
            void Number(float value){text.Append(value.ToString("F5",CultureInfo.InvariantCulture)).Append('|');}
            void Matrix(Matrix4x4 value){for(int i=0;i<16;i++)Number(value[i]);}
        }

        // Replays geometry, rather than trusting stored coverage numbers. This
        // verifies native-tag region unions, not independent per-shape coverage.
        public static int Verify(RebirthEntityRigWorkspace workspace)
        {
            var capsules=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true);
            var samples=RebirthEntityColliderPoseAudit.Capture(workspace,capsules,true,true);
            var report=RebirthEntityColliderPoseAudit.Compare(samples,capsules);
            if(report.poses.Any(pose=>pose.candidateOutside!=0))throw new InvalidDataException("Collider candidate leaves uncovered posed surface samples: "+JsonUtility.ToJson(report));
            int count=report.poses.Sum(pose=>pose.samples);
            foreach(var fit in workspace.colliderFitReports)if(fit.usesPoseUnionCoverage){fit.poseUnionSampleCount=count;fit.poseUnionCoverage=1f;}
            workspace.poseColliderProofSignature=Signature(workspace);
            return count;
        }

        public static void CommitFarmer(RebirthEntityRigWorkspace workspace)
        {
            if(!workspace.profile.compoundTerminalColliders)throw new InvalidOperationException("Compound body ownership must be enabled before committing this candidate.");
            var capsules=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true);
            var old=workspace.colliderFitReports;
            workspace.colliderFitReports=new System.Collections.Generic.List<EntityColliderFitReport>();
            foreach(var capsule in capsules)
            {
                var prior=old.Find(fit=>fit.collider==capsule);
                Vector3 scale=capsule.transform.lossyScale;
                float across=Mathf.Max(Mathf.Abs(scale[(capsule.direction+1)%3]),Mathf.Abs(scale[(capsule.direction+2)%3]));
                workspace.colliderFitReports.Add(new EntityColliderFitReport{collider=capsule,colliderName=prior!=null?prior.colliderName:capsule.name,
                    radius=capsule.radius*across,height=capsule.height*Mathf.Abs(scale[capsule.direction]),
                    baseGameReferenceRadius=capsule.name.StartsWith("EntityStudio_Compound_",StringComparison.Ordinal)?0:RebirthEntityRigBuilder.BaseGameReferenceRadius(capsule.name,workspace.profile.estimatedHeight),
                    usesPoseUnionCoverage=true,poseUnionCoverage=0,poseUnionSampleCount=0,
                    sampledVertices=0,estimatedSurfaceCoverage=0,usedProportionalFallback=false});
            }
            int samples=Verify(workspace);
            RebirthFarmerNativePhysics.Configure(workspace);
            workspace.profile.reviewedColliderFit=false;
            workspace.poseColliderProofSignature=Signature(workspace);
            EditorUtility.SetDirty(workspace);EditorUtility.SetDirty(workspace.profile);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(workspace.gameObject.scene);EditorSceneManager.SaveScene(workspace.gameObject.scene);
            Debug.Log("COLLIDER CANDIDATE SAVED: shapes="+capsules.Length+" regionUnionPoseSamples="+samples+"; per-shape assigned-vertex coverage not claimed; review remains pending.");
        }

        public static void ApplyMeasuredFarmer()
        {
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(root=>root.GetComponent<RebirthEntityRigWorkspace>()).Single(value=>value!=null);
            if(RebirthEntityRigValidator.HasStaleInputs(workspace))throw new InvalidDataException("Source/profile inputs changed since the saved authoring build.");
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthFarmerReview");
            string csv=Path.Combine(folder,"RebirthFarmer_PoseUnionFit_DamageRegions_CompoundTorso_Surfaces_Silhouette.csv");
            string baseline=Path.Combine(folder,"RebirthFarmer_BeforeMeasuredFit_Coverage.json");
            RebirthEntityCoverageReview.Write(workspace,baseline);
            foreach(string row in File.ReadAllLines(csv).Skip(1))
            {
                string[] columns=row.Split(',');if(columns.Length!=7)throw new InvalidDataException("Unexpected measured collider row.");string name=columns[0];
                var matches=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true).Where(value=>value.name==name).ToArray();CapsuleCollider capsule;
                if(matches.Length==1)capsule=matches[0];
                else if(matches.Length==0&&new[]{"EntityStudio_Compound_HipsLeftHit","EntityStudio_Compound_HipsRightHit","EntityStudio_Compound_Spine2LeftHit","EntityStudio_Compound_Spine2RightHit"}.Contains(name))
                {
                    string parent=name.Contains("Spine2")?"Spine2":"Hips";Transform bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,parent);var reference=bone.GetComponent<CapsuleCollider>();
                    var go=new GameObject(name);go.transform.SetParent(bone,false);go.tag=bone.tag;go.layer=bone.gameObject.layer;
                    capsule=go.AddComponent<CapsuleCollider>();capsule.direction=reference.direction;capsule.sharedMaterial=reference.sharedMaterial;
                    if(capsule.attachedRigidbody!=reference.attachedRigidbody)throw new InvalidOperationException("Compound capsule body mismatch.");
                }
                else throw new InvalidDataException("Measured collider cannot uniquely resolve: "+name);
                float Value(int index)=>float.Parse(columns[index],CultureInfo.InvariantCulture);
                capsule.radius=Value(2);capsule.height=Value(3);capsule.center=new Vector3(Value(4),Value(5),Value(6));
            }
            Verify(workspace);
            string after=Path.Combine(folder,"RebirthFarmer_AfterMeasuredFit_Coverage.json");RebirthEntityCoverageReview.Write(workspace,after);
            var beforeReport=JsonUtility.FromJson<RebirthEntityCoverageReview.Report>(File.ReadAllText(baseline));var afterReport=JsonUtility.FromJson<RebirthEntityCoverageReview.Report>(File.ReadAllText(after));
            foreach(var view in afterReport.views)
            {
                var prior=beforeReport.views.Single(value=>value.view==view.view);
                if(view.missedMeshRays!=0||view.neckMisses!=0||view.colliderOnlyRays>prior.colliderOnlyRays)throw new InvalidDataException("Measured fit fails actual silhouette/coverage comparison: "+view.view);
            }
            CommitFarmer(workspace);
            RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,"RebirthFarmer_SavedPoseColliderFit"));
        }
    }
}
