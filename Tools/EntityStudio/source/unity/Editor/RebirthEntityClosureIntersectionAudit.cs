using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityClosureIntersectionAudit
    {
        public static void RunSavedFarmer()
        {
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            var rotations=workspace.generatedSkeleton.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.localRotation);
            var originalMeshes=workspace.sourceSkinnedMeshes.ToDictionary(r=>r,r=>r.sharedMesh);
            var inputs=workspace.sourceSkinnedMeshes.ToDictionary(r=>r,r=>workspace.surfaceRepairInputs.Single(value=>value.repairedMesh==r.sharedMesh));
            bool spatial=Array.IndexOf(Environment.GetCommandLineArgs(),"-spatialClosure")>=0;
            bool localSkin=Array.IndexOf(Environment.GetCommandLineArgs(),"-localClosureSkin")>=0;
            bool humanoidFingers=Array.IndexOf(Environment.GetCommandLineArgs(),"-humanoidFingerPoses")>=0;
            bool fingerChains=Array.IndexOf(Environment.GetCommandLineArgs(),"-closureFingerChains")>=0;
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerClosureIntersections");Directory.CreateDirectory(folder);
            if(spatial){folder+="_Spatial";Directory.CreateDirectory(folder);}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-avoidClosureCrossings")>=0){folder+="_Constrained";Directory.CreateDirectory(folder);}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-closureAllPoses")>=0){folder+="_MultiPose";Directory.CreateDirectory(folder);}
            bool extended=Array.IndexOf(Environment.GetCommandLineArgs(),"-extendedClosureAudit")>=0;
            if(extended){folder+="_Extended";Directory.CreateDirectory(folder);}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-closureFingerPoses")>=0){folder+="_FingerConstrained";Directory.CreateDirectory(folder);}
            if(localSkin){folder+="_LocalSkin";Directory.CreateDirectory(folder);}
            if(humanoidFingers){folder+="_HumanoidFingers";Directory.CreateDirectory(folder);}
            if(fingerChains){folder+="_FingerChains";Directory.CreateDirectory(folder);}
            var rows=new List<string>{"pose,renderer,part,triangleA,triangleB,capA,capB,x,y,z"};
            try
            {
                if(spatial)foreach(var renderer in workspace.sourceSkinnedMeshes){renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);RebirthEntityClosureRetriangulation.Apply(renderer,inputs[renderer]);}
                if(localSkin)foreach(var renderer in workspace.sourceSkinnedMeshes){if(renderer.sharedMesh==originalMeshes[renderer])renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);RebirthEntityLocalRepairSkin.Apply(renderer,inputs[renderer]);}
                if(fingerChains)foreach(var renderer in workspace.sourceSkinnedMeshes){if(renderer.sharedMesh==originalMeshes[renderer])renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);RebirthEntityLocalRepairSkin.ApplyFingerChains(renderer,inputs[renderer]);}
                foreach(string pose in extended?new[]{"rest","elbows","reach","shoulders","knees","crouch","neckTurn","leftFingers","rightFingers"}:new[]{"rest","elbows","reach"})
                {
                    foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                    if(pose=="elbows"||pose=="reach")foreach(string side in new[]{"Left","Right"})Rotate(side+"ForeArm",new Vector3(70,0,0));
                    if(pose=="shoulders"||pose=="reach")foreach(string side in new[]{"Left","Right"})Rotate(side+"Arm",new Vector3(25,0,side=="Left"?-35:35));
                    if(pose=="knees")foreach(string side in new[]{"Left","Right"})Rotate(side+"Leg",new Vector3(75,0,0));
                    if(pose=="crouch")foreach(string side in new[]{"Left","Right"}){Rotate(side+"UpLeg",new Vector3(-45,0,0));Rotate(side+"Leg",new Vector3(90,0,0));}
                    if(pose=="neckTurn")Rotate("Neck",new Vector3(20,45,0));
                    if(pose.EndsWith("Fingers",StringComparison.Ordinal))
                    {string side=pose=="leftFingers"?"Left":"Right";if(humanoidFingers)RebirthEntityHumanoidPoseProbe.ApplyFingerCurl(workspace,side);else foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Pinky"})for(int joint=1;joint<=3;joint++)Rotate(side+"Hand"+finger+joint,new Vector3(0,0,(joint==1?45:joint==2?30:20)*(side=="Left"?1:-1)));}
                    int before=rows.Count;
                    foreach(var renderer in workspace.sourceSkinnedMeshes)
                    {
                        var mesh=renderer.sharedMesh;var source=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes;
                        var transforms=renderer.bones.Select((bone,index)=>bone.localToWorldMatrix*bind[index]).ToArray();
                        var points=new Vector3[source.Length];
                        for(int i=0;i<points.Length;i++){var w=weights[i];var p=source[i];points[i]=transforms[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+transforms[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+transforms[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+transforms[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;}
                        var input=inputs[renderer];var triangles=mesh.triangles;int count=triangles.Length/3;
                        var bounds=new Bounds[count];var parts=new int[count];var caps=new int[count];
                        for(int t=0;t<count;t++){int a=triangles[t*3];bounds[t]=new Bounds(points[a],Vector3.zero);bounds[t].Encapsulate(points[triangles[t*3+1]]);bounds[t].Encapsulate(points[triangles[t*3+2]]);parts[t]=input.repairVertexParts[a];caps[t]=input.repairVertexIds[a];}
                        for(int a=0;a<count;a++)
                        {
                            if(parts[a]==0)continue;
                            for(int b=a+1;b<count;b++)
                            {
                                if(parts[a]!=parts[b]||(caps[a]==0&&caps[b]==0)||!bounds[a].Intersects(bounds[b]))continue;
                                bool shared=false;for(int i=0;i<3;i++)for(int j=0;j<3;j++)if((points[triangles[a*3+i]]-points[triangles[b*3+j]]).sqrMagnitude<1e-10f)shared=true;if(shared)continue;
                                Vector3 hit=Vector3.zero;bool crossed=false;
                                for(int edge=0;edge<3&&!crossed;edge++)crossed=Cross(points[triangles[a*3+edge]],points[triangles[a*3+(edge+1)%3]],points[triangles[b*3]],points[triangles[b*3+1]],points[triangles[b*3+2]],out hit);
                                for(int edge=0;edge<3&&!crossed;edge++)crossed=Cross(points[triangles[b*3+edge]],points[triangles[b*3+(edge+1)%3]],points[triangles[a*3]],points[triangles[a*3+1]],points[triangles[a*3+2]],out hit);
                                if(crossed)rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6},{7:R},{8:R},{9:R}",pose,renderer.name,parts[a],a,b,caps[a],caps[b],hit.x,hit.y,hit.z));
                            }
                        }
                    }
                    Debug.Log("CLOSURE INTERSECTION AUDIT pose="+pose+" strictCrossingPairs="+(rows.Count-before));
                    if(Array.IndexOf(Environment.GetCommandLineArgs(),"-fingerCoverage")>=0&&pose.EndsWith("Fingers",StringComparison.Ordinal))
                    {
                        var shapes=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true);var fingerSamples=RebirthEntityColliderPoseAudit.Capture(workspace,shapes,true,true);var fingerReport=RebirthEntityColliderPoseAudit.Compare(fingerSamples,shapes);
                        File.WriteAllText(Path.Combine(folder,"Coverage_"+pose+".json"),JsonUtility.ToJson(fingerReport,true));RebirthEntityColliderPoseAudit.WriteMisses(fingerSamples,shapes,Path.Combine(folder,"Misses_"+pose+".csv"));
                        Debug.Log("BASELINE FINGER COVERAGE pose="+pose+" samples="+fingerReport.poses.Sum(value=>value.samples)+" outside="+fingerReport.poses.Sum(value=>value.candidateOutside));
                    }
                    if(Array.IndexOf(Environment.GetCommandLineArgs(),"-captureClosure")>=0&&(pose=="rest"||pose=="reach"||pose.EndsWith("Fingers",StringComparison.Ordinal)))RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,pose),false,true);
                }
                File.WriteAllLines(Path.Combine(folder,"Crossings.csv"),rows);
                if(extended)
                {
                    foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                    var capsules=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true);
                    var samples=RebirthEntityColliderPoseAudit.Capture(workspace,capsules,true,true);var coverage=RebirthEntityColliderPoseAudit.Compare(samples,capsules);
                    File.WriteAllText(Path.Combine(folder,"ColliderCoverage.json"),JsonUtility.ToJson(coverage,true));
                    Debug.Log("CLOSURE CANDIDATE COVERAGE: samples="+coverage.poses.Sum(p=>p.samples)+" outside="+coverage.poses.Sum(p=>p.candidateOutside));
                }
            }
            finally{foreach(var pair in rotations)pair.Key.localRotation=pair.Value;foreach(var pair in originalMeshes)if(pair.Key.sharedMesh!=pair.Value){UnityEngine.Object.DestroyImmediate(pair.Key.sharedMesh);pair.Key.sharedMesh=pair.Value;}}
            void Rotate(string name,Vector3 angle){var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,name);if(bone==null)throw new InvalidOperationException("Missing pose bone "+name);bone.localRotation*=Quaternion.Euler(angle);}
        }
        internal static bool Cross(Vector3 start,Vector3 end,Vector3 a,Vector3 b,Vector3 c,out Vector3 hit)
        {
            hit=Vector3.zero;Vector3 direction=end-start,e1=b-a,e2=c-a,p=Vector3.Cross(direction,e2);double determinant=Vector3.Dot(e1,p);
            if(Math.Abs(determinant)<1e-14)return false;double inverse=1/determinant;Vector3 s=start-a;double u=Vector3.Dot(s,p)*inverse;if(u<=1e-5||u>=1-1e-5)return false;
            Vector3 q=Vector3.Cross(s,e1);double v=Vector3.Dot(direction,q)*inverse;if(v<=1e-5||u+v>=1-1e-5)return false;double t=Vector3.Dot(e2,q)*inverse;if(t<=1e-5||t>=1-1e-5)return false;
            hit=start+direction*(float)t;return true;
        }
    }
}
