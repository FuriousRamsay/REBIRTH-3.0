using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityColliderUnionFit
    {
        public static bool Contains(CapsuleCollider capsule,Vector3 point)
        {
            Vector3 axis=capsule.direction==0?Vector3.right:capsule.direction==1?Vector3.up:Vector3.forward;
            Vector3 scale=capsule.transform.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            float along=scale[capsule.direction],across=capsule.direction==0?Mathf.Max(scale.y,scale.z):capsule.direction==1?Mathf.Max(scale.x,scale.z):Mathf.Max(scale.x,scale.y);
            float radius=capsule.radius*across,half=Mathf.Max(0,capsule.height*along*.5f-radius);
            axis=capsule.transform.TransformDirection(axis).normalized;Vector3 delta=point-capsule.transform.TransformPoint(capsule.center);
            Vector3 residual=delta-axis*Mathf.Clamp(Vector3.Dot(delta,axis),-half,half);
            return residual.sqrMagnitude<=radius*radius+1e-8f;
        }

        public static void RunFarmer()
        {
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(root=>root.GetComponent<RebirthEntityRigWorkspace>()).Single(value=>value!=null);
            bool splitTorso=Array.IndexOf(Environment.GetCommandLineArgs(),"-splitTorso")>=0;
            var referenceRadii=splitTorso?SplitTorso(workspace):null;
            var capsules=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true);
            bool damageRegions=Array.IndexOf(Environment.GetCommandLineArgs(),"-fitDamageRegions")>=0;
            bool surfaceSamples=Array.IndexOf(Environment.GetCommandLineArgs(),"-sampleSurfaces")>=0;
            var poseSamples=RebirthEntityColliderPoseAudit.Capture(workspace,capsules,damageRegions,surfaceSamples);
            string auditFolder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthFarmerReview");
            Directory.CreateDirectory(auditFolder);
            RebirthEntityColliderPoseAudit.WriteMisses(poseSamples,capsules,Path.Combine(auditFolder,"Native11_BaselinePoseMisses.csv"));
            var axialBounds=new Dictionary<CapsuleCollider,Vector2>();
            foreach(var capsule in capsules)axialBounds.Add(capsule,new Vector2(capsule.center[capsule.direction]-capsule.height*.5f,capsule.center[capsule.direction]+capsule.height*.5f));
            if(damageRegions)foreach(var pose in RebirthEntityColliderPoseAudit.Compare(poseSamples,capsules).poses)Debug.Log("COLLIDER REGION BASELINE: "+JsonUtility.ToJson(pose));
            var points=new List<Vector3>();foreach(var renderer in workspace.sourceSkinnedMeshes)foreach(Vector3 point in renderer.sharedMesh.vertices)points.Add(renderer.transform.TransformPoint(point));
            var initial=new Dictionary<CapsuleCollider,float>();foreach(var capsule in capsules)initial.Add(capsule,capsule.radius);
            bool constrainPoses=Array.IndexOf(Environment.GetCommandLineArgs(),"-fitAllPoses")>=0;
            bool fitSilhouette=Array.IndexOf(Environment.GetCommandLineArgs(),"-fitSilhouette")>=0;
            var silhouette=fitSilhouette?new RebirthEntitySilhouetteFit(workspace):null;
            if(constrainPoses)
            {
                // A point beyond a rounded axial end cannot be reached by radius
                // growth alone. Seed length separately before shrinking the union.
                RebirthEntityColliderPoseAudit.CoverAxialLimits(poseSamples,capsules,.025f,.12f);
                RebirthEntityColliderPoseAudit.CoverAll(poseSamples,capsules,.025f,referenceRadii);
            }
            var covered=new bool[points.Count];int baselineOutside=0;
            for(int i=0;i<points.Count;i++){covered[i]=capsules.Any(capsule=>Contains(capsule,points[i]));if(!covered[i])baselineOutside++;}
            for(int pass=0;pass<(fitSilhouette?5:3);pass++)foreach(var capsule in capsules)
            {
                var required=new List<Vector3>();
                var poseRequired=new List<(Vector3 point,RebirthEntityColliderPoseAudit.Frame frame)>();
                int capsuleIndex=Array.IndexOf(capsules,capsule);
                for(int i=0;i<points.Count;i++)
                {
                    if(!covered[i])continue;bool other=false;
                    foreach(var candidate in capsules)if(candidate!=capsule&&Contains(candidate,points[i])){other=true;break;}
                    if(!other)required.Add(points[i]);
                }
                if(constrainPoses)foreach(var sample in poseSamples)
                {
                    Matrix4x4 toRest=capsule.transform.localToWorldMatrix*sample.frames[capsuleIndex].localToWorld.inverse;
                    for(int point=0;point<sample.points.Length;point++)
                    {
                        if(!RebirthEntityColliderPoseAudit.Allows(sample,point,capsuleIndex))continue;
                        bool other=false;for(int i=0;i<capsules.Length;i++)if(i!=capsuleIndex&&RebirthEntityColliderPoseAudit.Allows(sample,point,i)&&RebirthEntityColliderPoseAudit.Contains(capsules[i],sample.frames[i],sample.points[point])){other=true;break;}
                        if(other)continue;
                        required.Add(toRest.MultiplyPoint3x4(sample.points[point]));poseRequired.Add((sample.points[point],sample.frames[capsuleIndex]));
                    }
                }
                bool ContainsRequired()
                {
                    foreach(Vector3 point in required)if(!Contains(capsule,point))return false;
                    foreach(var sample in poseRequired)if(!RebirthEntityColliderPoseAudit.Contains(capsule,sample.frame,sample.point))return false;
                    return true;
                }
                float low=.015f,high=capsule.radius;
                for(int iteration=0;iteration<16;iteration++)
                {
                    float trial=(low+high)*.5f;capsule.radius=trial;bool contains=ContainsRequired();
                    if(contains)high=trial;else low=trial;
                }
                capsule.radius=high;
                if(required.Count>=8)
                {
                    float oldRadius=capsule.radius,oldHeight=capsule.height;Vector3 oldCenter=capsule.center;
                    Vector2Int priorFootprint=fitSilhouette&&pass>=2?silhouette.Footprint(capsule):Vector2Int.zero;
                    Vector3 localAxis=capsule.direction==0?Vector3.right:capsule.direction==1?Vector3.up:Vector3.forward;
                    Vector3 axis=capsule.transform.TransformDirection(localAxis).normalized;
                    Vector3 u=Vector3.Cross(axis,Mathf.Abs(Vector3.Dot(axis,Vector3.up))>.95f?Vector3.right:Vector3.up).normalized,v=Vector3.Cross(axis,u);
                    Vector3 center=capsule.transform.TransformPoint(oldCenter),scale=capsule.transform.lossyScale;
                    float along=Mathf.Abs(scale[capsule.direction]),across=capsule.direction==0?Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)):capsule.direction==1?Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z)):Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y));
                    var axial=new List<float>();var radialPlane=new List<Vector2>();
                    foreach(Vector3 point in required){Vector3 delta=point-center;axial.Add(Vector3.Dot(delta,axis));radialPlane.Add(new Vector2(Vector3.Dot(delta,u),Vector3.Dot(delta,v)));}
                    var circle=RebirthEntityRigBuilder.MinimumRadialCircle(radialPlane);var radial=new List<float>();foreach(Vector2 point in radialPlane)radial.Add(Vector2.Distance(point,circle.center));
                    var fit=RebirthEntityRigBuilder.FitCapsuleSamples(axial,radial,.0001f,true);
                    capsule.radius=fit.radius/across;capsule.height=fit.height/along;
                    capsule.center=capsule.transform.InverseTransformPoint(center+u*circle.center.x+v*circle.center.y+axis*fit.centerAxial);
                    float Volume(float radius,float height)=>Mathf.PI*radius*radius*Mathf.Max(0,height-2*radius)+4f/3f*Mathf.PI*radius*radius*radius;
                    bool valid=Volume(capsule.radius*across,capsule.height*along)<Volume(oldRadius*across,oldHeight*along);
                    Vector2 span=axialBounds[capsule];
                    valid&=capsule.center[capsule.direction]-capsule.height*.5f>=span.x-.005f&&capsule.center[capsule.direction]+capsule.height*.5f<=span.y+.005f;
                    if(valid)valid=ContainsRequired();
                    if(valid&&fitSilhouette&&pass>=2){Vector2Int footprint=silhouette.Footprint(capsule);valid=footprint.x<=priorFootprint.x&&footprint.y<=priorFootprint.y;}
                    if(!valid){capsule.radius=oldRadius;capsule.height=oldHeight;capsule.center=oldCenter;}
                }
                if(fitSilhouette&&pass>=2)
                {
                    float reference=referenceRadii!=null&&referenceRadii.TryGetValue(capsule,out float value)?value:initial[capsule];
                    silhouette.Optimize(capsule,capsules,ContainsRequired,reference+.025f,axialBounds[capsule]);
                }
            }
            int finalOutside=0,newMisses=0;
            for(int i=0;i<points.Count;i++)if(!capsules.Any(capsule=>Contains(capsule,points[i]))){finalOutside++;if(covered[i])newMisses++;}
            if(newMisses>0)throw new InvalidOperationException("Union fit lost previously covered mesh samples: "+newMisses);
            Physics.SyncTransforms();
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthFarmerReview");
            var rows=new List<string>{"bone,initialRadius,trialRadius,trialHeight,centerX,centerY,centerZ"};foreach(var capsule in capsules)rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R}",capsule.name,initial[capsule],capsule.radius,capsule.height,capsule.center.x,capsule.center.y,capsule.center.z));
            string label=constrainPoses?"RebirthFarmer_PoseUnionFit":"RebirthFarmer_UnionFit";
            if(damageRegions)label+="_DamageRegions";
            if(splitTorso)label+="_CompoundTorso";
            if(surfaceSamples)label+="_Surfaces";
            if(fitSilhouette)label+="_Silhouette";
            File.WriteAllLines(Path.Combine(folder,label+".csv"),rows);
            var poseReport=RebirthEntityColliderPoseAudit.Compare(poseSamples,capsules);
            File.WriteAllText(Path.Combine(folder,label+"_Poses.json"),JsonUtility.ToJson(poseReport,true));
            foreach(var pose in poseReport.poses)Debug.Log("COLLIDER POSE TRIAL: "+JsonUtility.ToJson(pose));
            if(constrainPoses&&poseReport.poses.Any(pose=>pose.candidateOutside>0))throw new InvalidOperationException("Constrained fit leaves uncovered pose samples.");
            RebirthEntityCoverageReview.Write(workspace,Path.Combine(folder,label+"_Coverage.json"));
            RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,label));
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-saveCandidate")>=0)
            {
                if(!constrainPoses||!damageRegions||!surfaceSamples||!fitSilhouette)throw new InvalidOperationException("Saving requires pose, region, surface and silhouette constraints.");
                RebirthEntityColliderCandidate.CommitFarmer(workspace);
            }
            Debug.Log("COLLIDER UNION TRIAL: samples="+points.Count+" baselineOutside="+baselineOutside+" finalOutside="+finalOutside+" newMisses="+newMisses+"; scene not saved; assigned-body fit reports not altered.");
        }

        private static Dictionary<CapsuleCollider,float> SplitTorso(RebirthEntityRigWorkspace workspace)
        {
            var references=new Dictionary<CapsuleCollider,float>();
            foreach(string name in new[]{"Hips","Spine2"})
            {
                Transform bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,name);var source=bone.GetComponent<CapsuleCollider>();
                if(source==null||bone.GetComponent<Rigidbody>()==null)throw new InvalidOperationException("Torso compound requires the existing native body: "+name);
                int widthAxis=-1;float alignment=-1;
                for(int axis=0;axis<3;axis++)if(axis!=source.direction)
                {
                    Vector3 direction=axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward;
                    float candidate=Mathf.Abs(Vector3.Dot(bone.TransformDirection(direction).normalized,workspace.transform.right));
                    if(candidate>alignment){alignment=candidate;widthAxis=axis;}
                }
                float radius=source.radius;references.Add(source,radius);source.radius=radius*.62f;
                foreach(int side in new[]{-1,1})
                {
                    var go=new GameObject("EntityStudio_Compound_"+name+(side<0?"LeftHit":"RightHit"));go.transform.SetParent(bone,false);go.tag=bone.tag;go.layer=bone.gameObject.layer;
                    var capsule=go.AddComponent<CapsuleCollider>();capsule.direction=source.direction;capsule.radius=source.radius;capsule.height=source.height;Vector3 center=source.center;center[widthAxis]+=side*radius*.55f;capsule.center=center;
                    capsule.sharedMaterial=source.sharedMaterial;capsule.isTrigger=source.isTrigger;capsule.enabled=source.enabled;
                    references.Add(capsule,radius);
                    if(capsule.attachedRigidbody!=source.attachedRigidbody)throw new InvalidOperationException("Torso shape must share its existing Rigidbody.");
                }
            }
            return references;
        }
    }
}
