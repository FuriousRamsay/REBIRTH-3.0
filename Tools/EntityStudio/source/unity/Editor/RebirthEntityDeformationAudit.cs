using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityDeformationAudit
    {
        [Serializable] public sealed class Result
        {
            public string bonePrefix;
            public int triangles,stretchedEdges,collapsedEdges,measuredEdges;
            public float maxStretch=1,minLengthRatio=1,maxRestEdgeLength;
            public float maxAbsoluteLengthChange,totalRestEdgeLength,lengthWeightedAbsoluteStrain;
        }
        private sealed class Edge {public float stretch,rest,length;public int a,b;public BoneWeight weightA,weightB;public SkinnedMeshRenderer renderer;public Vector3 position;}

        public static Result WritePose(RebirthEntityRigWorkspace workspace,string path,string prefix,Dictionary<SkinnedMeshRenderer,bool[]> fixedSelection=null)
        {
            var result=new Result{bonePrefix=prefix};var edges=new List<Edge>();
            foreach(var renderer in workspace.sourceSkinnedMeshes)
            {
                Mesh mesh=renderer.sharedMesh;Vector3[] points=mesh.vertices;BoneWeight[] weights=mesh.boneWeights;Matrix4x4[] binds=mesh.bindposes;
                var matrices=new Matrix4x4[renderer.bones.Length];for(int i=0;i<matrices.Length;i++)matrices[i]=renderer.bones[i].localToWorldMatrix*binds[i];
                var posed=new Vector3[points.Length];var selected=new bool[points.Length];
                for(int i=0;i<points.Length;i++)
                {
                    BoneWeight weight=weights[i];
                    posed[i]=matrices[weight.boneIndex0].MultiplyPoint3x4(points[i])*weight.weight0+matrices[weight.boneIndex1].MultiplyPoint3x4(points[i])*weight.weight1+matrices[weight.boneIndex2].MultiplyPoint3x4(points[i])*weight.weight2+matrices[weight.boneIndex3].MultiplyPoint3x4(points[i])*weight.weight3;
                    selected[i]=Member(weight.boneIndex0,weight.weight0)||Member(weight.boneIndex1,weight.weight1)||Member(weight.boneIndex2,weight.weight2)||Member(weight.boneIndex3,weight.weight3);
                    if(fixedSelection!=null)selected[i]=fixedSelection[renderer][i];
                }
                bool Member(int bone,float weight)=>weight>.05f&&renderer.bones[bone].name.StartsWith(prefix,StringComparison.Ordinal);
                int[] triangles=mesh.triangles;var visited=new HashSet<ulong>();
                for(int triangle=0;triangle<triangles.Length;triangle+=3)
                {
                    int a=triangles[triangle],b=triangles[triangle+1],c=triangles[triangle+2];if(!selected[a]&&!selected[b]&&!selected[c])continue;result.triangles++;
                    foreach(var pair in new[]{new[]{a,b},new[]{b,c},new[]{c,a}})
                    {
                        int first=pair[0],second=pair[1];ulong key=((ulong)(uint)Math.Min(first,second)<<32)|(uint)Math.Max(first,second);if(!visited.Add(key))continue;
                        float rest=Vector3.Distance(renderer.transform.TransformPoint(points[first]),renderer.transform.TransformPoint(points[second]));if(rest<.0001f)continue;
                        float length=Vector3.Distance(posed[first],posed[second]),ratio=length/rest;
                        result.maxRestEdgeLength=Mathf.Max(result.maxRestEdgeLength,rest);result.maxStretch=Mathf.Max(result.maxStretch,ratio);result.minLengthRatio=Mathf.Min(result.minLengthRatio,ratio);
                        result.measuredEdges++;
                        result.totalRestEdgeLength+=rest;
                        result.maxAbsoluteLengthChange=Mathf.Max(result.maxAbsoluteLengthChange,Mathf.Abs(length-rest));
                        result.lengthWeightedAbsoluteStrain+=Mathf.Abs(length-rest);
                        if(ratio>2)result.stretchedEdges++;if(ratio<.25f)result.collapsedEdges++;
                        edges.Add(new Edge{stretch=ratio,rest=rest,length=length,a=first,b=second,weightA=weights[first],weightB=weights[second],renderer=renderer,position=renderer.transform.TransformPoint((points[first]+points[second])*.5f)});
                    }
                }
            }
            if(result.totalRestEdgeLength>0)result.lengthWeightedAbsoluteStrain/=result.totalRestEdgeLength;
            edges.Sort((a,b)=>Mathf.Max(b.stretch,1f/Mathf.Max(b.stretch,.0001f)).CompareTo(Mathf.Max(a.stretch,1f/Mathf.Max(a.stretch,.0001f))));
            var rows=new List<string>{"renderer,a,b,restLength,posedLength,ratio,restMidX,restMidY,restMidZ,bonesA,bonesB"};
            foreach(var edge in edges.GetRange(0,Math.Min(150,edges.Count)))
            {
                rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9},{10}",edge.renderer.name,edge.a,edge.b,edge.rest,edge.length,edge.stretch,edge.position.x,edge.position.y,edge.position.z,Bones(edge.renderer,edge.weightA),Bones(edge.renderer,edge.weightB)));
            }
            File.WriteAllText(path,JsonUtility.ToJson(result,true));File.WriteAllLines(Path.ChangeExtension(path,"csv"),rows);
            edges.Sort((a,b)=>Mathf.Abs(b.length-b.rest).CompareTo(Mathf.Abs(a.length-a.rest)));
            var absoluteRows=new List<string>{"renderer,a,b,restLength,posedLength,absoluteChange,restMidX,restMidY,restMidZ,bonesA,bonesB"};
            foreach(var edge in edges.GetRange(0,Math.Min(150,edges.Count)))
                absoluteRows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9},{10}",edge.renderer.name,edge.a,edge.b,edge.rest,edge.length,Mathf.Abs(edge.length-edge.rest),edge.position.x,edge.position.y,edge.position.z,Bones(edge.renderer,edge.weightA),Bones(edge.renderer,edge.weightB)));
            File.WriteAllLines(Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+"_Absolute.csv"),absoluteRows);
            var ownership=new List<string>{"renderer,a,b,capA,capB,partA,partB,absoluteChange,ratio"};
            foreach(var edge in edges.GetRange(0,Math.Min(150,edges.Count)))
            {
                var input=workspace.surfaceRepairInputs.Find(value=>value.repairedMesh==edge.renderer.sharedMesh);
                if(input==null)continue;
                ownership.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6},{7:R},{8:R}",edge.renderer.name,edge.a,edge.b,input.repairVertexIds[edge.a],input.repairVertexIds[edge.b],input.repairVertexParts[edge.a],input.repairVertexParts[edge.b],Mathf.Abs(edge.length-edge.rest),edge.stretch));
            }
            File.WriteAllLines(Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+"_Ownership.csv"),ownership);
            return result;
        }
        private static string Bones(SkinnedMeshRenderer renderer,BoneWeight weight)
        {
            return renderer.bones[weight.boneIndex0].name+":"+weight.weight0.ToString("F3",CultureInfo.InvariantCulture)+";"+renderer.bones[weight.boneIndex1].name+":"+weight.weight1.ToString("F3",CultureInfo.InvariantCulture)+";"+renderer.bones[weight.boneIndex2].name+":"+weight.weight2.ToString("F3",CultureInfo.InvariantCulture)+";"+renderer.bones[weight.boneIndex3].name+":"+weight.weight3.ToString("F3",CultureInfo.InvariantCulture);
        }
        public static void RunSavedFarmerHands()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");RebirthEntityRigWorkspace workspace=null;
            foreach(var root in scene.GetRootGameObjects())if(root.TryGetComponent(out RebirthEntityRigWorkspace found)){workspace=found;break;}
            if(workspace==null)throw new InvalidOperationException("Farmer workspace missing.");
            bool smoothed=Array.IndexOf(Environment.GetCommandLineArgs(),"-smoothHandWeights")>=0;
            bool euclidean=Array.IndexOf(Environment.GetCommandLineArgs(),"-euclideanHandWeights")>=0;
            bool chain=Array.IndexOf(Environment.GetCommandLineArgs(),"-chainHandWeights")>=0;
            if(smoothed)foreach(var renderer in workspace.sourceSkinnedMeshes){renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);RebirthEntityHandWeights.Smooth(renderer);}
            if(euclidean)foreach(var renderer in workspace.sourceSkinnedMeshes){renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);RebirthEntityHandWeights.RefitEuclidean(renderer);}
            if(chain)foreach(var renderer in workspace.sourceSkinnedMeshes){renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);RebirthEntityHandWeights.RefitEuclidean(renderer,true);}
            string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthFarmerReview");
            foreach(string side in new[]{"Left","Right"})
            {
                var rotations=new Dictionary<Transform,Quaternion>();
                try
                {
                    foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Pinky"})for(int joint=1;joint<=3;joint++)
                    {Transform bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,side+"Hand"+finger+joint);if(bone==null)continue;rotations[bone]=bone.localRotation;bone.localRotation*=Quaternion.Euler(0,0,(joint==1?45:joint==2?30:20)*(side=="Left"?1:-1));}
                    var report=WritePose(workspace,Path.Combine(output,"RebirthFarmer_"+side+"Hand_Deformation"+(smoothed?"_Smoothed":"")+(euclidean?"_Euclidean":"")+(chain?"_Chain":"")+".json"),side+"Hand");Debug.Log("HAND DEFORMATION "+JsonUtility.ToJson(report));
                }
                finally{foreach(var rotation in rotations)rotation.Key.localRotation=rotation.Value;}
            }
        }

        // Non-destructive whole-body diagnosis. These authoring poses expose
        // suspect edges; their thresholds are not visual acceptance criteria.
        public static void RunSavedFarmerBody()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            RebirthEntityRigWorkspace workspace=null;
            foreach(var root in scene.GetRootGameObjects())if(root.TryGetComponent(out RebirthEntityRigWorkspace found)){workspace=found;break;}
            if(workspace==null)throw new InvalidOperationException("Farmer workspace missing.");
            bool kneeCandidate=Array.IndexOf(Environment.GetCommandLineArgs(),"-continuousKneeWeights")>=0;
            bool kneeChain=Array.IndexOf(Environment.GetCommandLineArgs(),"-continuousKneeChain")>=0;
            bool bodyDiffuse=Array.IndexOf(Environment.GetCommandLineArgs(),"-diffuseBodyWeights")>=0;
            bool bodyAllEdges=Array.IndexOf(Environment.GetCommandLineArgs(),"-bodyAllEdges")>=0;
            bool capturePoses=Array.IndexOf(Environment.GetCommandLineArgs(),"-captureBodyPoses")>=0;
            bool poseCorrect=Array.IndexOf(Environment.GetCommandLineArgs(),"-correctBodyPoses")>=0;
            bool localRepair=Array.IndexOf(Environment.GetCommandLineArgs(),"-localRepairSkin")>=0;
            bool smoothRepair=Array.IndexOf(Environment.GetCommandLineArgs(),"-smoothRepairGeometry")>=0;
            bool rebuildClosure=Array.IndexOf(Environment.GetCommandLineArgs(),"-retriangulateClosure")>=0;
            if(poseCorrect&&!bodyDiffuse)throw new ArgumentException("Pose correction requires a diffused body candidate.");
            var originals=new Dictionary<SkinnedMeshRenderer,Mesh>();
            foreach(var renderer in workspace.sourceSkinnedMeshes)originals.Add(renderer,renderer.sharedMesh);
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerBodyDeformation"+(poseCorrect?"_PoseCorrected":bodyDiffuse?(bodyAllEdges?"_BodyDiffuseAllEdges":"_BodyDiffuse"):kneeChain?"_KneeChain":kneeCandidate?"_KneeCandidate":""));Directory.CreateDirectory(folder);
            var rotations=new Dictionary<Transform,Quaternion>();
            foreach(var bone in workspace.generatedSkeleton.GetComponentsInChildren<Transform>(true))rotations.Add(bone,bone.localRotation);
            if(localRepair){folder+="_LocalRepairSkin";Directory.CreateDirectory(folder);}
            if(smoothRepair){folder+="_SmoothRepairGeometry";Directory.CreateDirectory(folder);}
            if(rebuildClosure){folder+="_RetriangulatedClosure";Directory.CreateDirectory(folder);}
            try
            {
                if(rebuildClosure)foreach(var renderer in workspace.sourceSkinnedMeshes)
                {renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);RebirthEntityClosureRetriangulation.Apply(renderer,workspace.surfaceRepairInputs.Find(input=>input.repairedMesh==originals[renderer]));}
                if(smoothRepair)foreach(var renderer in workspace.sourceSkinnedMeshes)
                {renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);RebirthEntityLocalRepairSkin.SmoothGeometry(renderer,workspace.surfaceRepairInputs.Find(input=>input.repairedMesh==originals[renderer]));}
                if(localRepair)foreach(var renderer in workspace.sourceSkinnedMeshes)
                {renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);RebirthEntityLocalRepairSkin.Apply(renderer,workspace.surfaceRepairInputs.Find(input=>input.repairedMesh==originals[renderer]));}
                if(kneeCandidate||kneeChain)foreach(var renderer in workspace.sourceSkinnedMeshes)
                {renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);RebirthEntityKneeWeights.Apply(renderer,kneeChain?.5f:.22f,kneeChain);}
                if(bodyDiffuse)foreach(var renderer in workspace.sourceSkinnedMeshes)
                {
                    bool[] pinned=null;
                    foreach(var input in workspace.surfaceRepairInputs)if(input.repairedMesh==originals[renderer])
                    {pinned=new bool[input.repairVertexIds.Length];for(int i=0;i<pinned.Length;i++)pinned[i]=input.repairVertexIds[i]>0;break;}
                    if(workspace.profile.separateFusedArms&&pinned==null)throw new InvalidOperationException("Body diffusion requires repair correspondence.");
                    if(renderer.sharedMesh==originals[renderer])renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);
                    RebirthEntityHandWeights.Diffuse(renderer,80,.05f,pinned,name=>name=="Hips"||name.StartsWith("Spine",StringComparison.Ordinal)||name.Contains("Leg")||name=="Neck"||name=="Head",bodyAllEdges?float.PositiveInfinity:.02f);
                    if(poseCorrect)RebirthEntityPoseWeightCorrection.Apply(workspace,renderer,originals[renderer],workspace.surfaceRepairInputs.Find(input=>input.repairedMesh==originals[renderer]));
                }
                foreach(string pose in new[]{"rest","elbows","knees","shoulders","neckTurn","crouch","reach"})
                {
                    foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                    if(pose=="elbows"||pose=="reach")foreach(string side in new[]{"Left","Right"})Rotate(side+"ForeArm",new Vector3(70,0,0));
                    if(pose=="knees")foreach(string side in new[]{"Left","Right"})Rotate(side+"Leg",new Vector3(75,0,0));
                    if(pose=="shoulders"||pose=="reach"){Rotate("LeftArm",new Vector3(25,0,-35));Rotate("RightArm",new Vector3(25,0,35));}
                    if(pose=="neckTurn")Rotate("Neck",new Vector3(20,45,0));
                    if(pose=="crouch")foreach(string side in new[]{"Left","Right"}){Rotate(side+"UpLeg",new Vector3(-45,0,0));Rotate(side+"Leg",new Vector3(90,0,0));}
                    foreach(string region in new[]{"","Spine","Hips","LeftArm","RightArm","LeftForeArm","RightForeArm","LeftUpLeg","RightUpLeg","LeftLeg","RightLeg","LeftHand","RightHand"})
                    {
                        string label=string.IsNullOrEmpty(region)?"WholeBody":region;
                        Result report=WritePose(workspace,Path.Combine(folder,pose+"_"+label+".json"),region);
                        Debug.Log("BODY DEFORMATION "+pose+" "+label+" "+JsonUtility.ToJson(report));
                    }
                    if(capturePoses&&(pose=="rest"||pose=="knees"||pose=="crouch"||pose=="neckTurn"||((smoothRepair||rebuildClosure)&&(pose=="reach"||pose=="elbows"))))
                        RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,pose),false);
                }
            }
            finally
            {
                foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                foreach(var pair in originals)if(pair.Key.sharedMesh!=pair.Value)
                {UnityEngine.Object.DestroyImmediate(pair.Key.sharedMesh);pair.Key.sharedMesh=pair.Value;}
            }
            void Rotate(string name,Vector3 angles)
            {
                var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,name);
                if(bone==null)throw new InvalidOperationException("Required pose bone missing: "+name);
                bone.localRotation*=Quaternion.Euler(angles);
            }
        }

        public static void RunSavedHandWeightSearch()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            RebirthEntityRigWorkspace workspace=null;foreach(var root in scene.GetRootGameObjects())if(root.TryGetComponent(out RebirthEntityRigWorkspace found)){workspace=found;break;}
            if(workspace==null)throw new InvalidOperationException("Farmer workspace missing.");
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/HandWeightSearch");Directory.CreateDirectory(folder);
            var originals=new Dictionary<SkinnedMeshRenderer,Mesh>();foreach(var renderer in workspace.sourceSkinnedMeshes)originals.Add(renderer,renderer.sharedMesh);
            var selections=new Dictionary<string,Dictionary<SkinnedMeshRenderer,bool[]>>();
            foreach(string side in new[]{"Left","Right"})
            {
                var selection=new Dictionary<SkinnedMeshRenderer,bool[]>();selections.Add(side,selection);
                foreach(var pair in originals)
                {
                    BoneWeight[] weights=pair.Value.boneWeights;var selected=new bool[weights.Length];selection.Add(pair.Key,selected);
                    bool Member(int bone,float weight)=>weight>.05f&&pair.Key.bones[bone].name.StartsWith(side+"Hand",StringComparison.Ordinal);
                    for(int i=0;i<weights.Length;i++){BoneWeight w=weights[i];selected[i]=Member(w.boneIndex0,w.weight0)||Member(w.boneIndex1,w.weight1)||Member(w.boneIndex2,w.weight2)||Member(w.boneIndex3,w.weight3);}
                }
            }
            var baselines=new Dictionary<string,Result>();var best=new Dictionary<string,Result>();var rows=new List<string>{"side,passes,blend,maxAbsoluteChange,strain,stretched,collapsed,accepted"};
            try
            {
                foreach(int passes in new[]{0,1,2,4,8})foreach(float blend in passes==0?new[]{0f}:new[]{.1f,.25f,.5f})
                {
                    foreach(var pair in originals){if(pair.Key.sharedMesh!=pair.Value)UnityEngine.Object.DestroyImmediate(pair.Key.sharedMesh);pair.Key.sharedMesh=UnityEngine.Object.Instantiate(pair.Value);if(passes>0)RebirthEntityHandWeights.Smooth(pair.Key,passes,blend);}
                    foreach(string side in new[]{"Left","Right"})
                    {
                        var rotations=new Dictionary<Transform,Quaternion>();Result report;
                        try
                        {
                            foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Pinky"})for(int joint=1;joint<=3;joint++)
                            {var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,side+"Hand"+finger+joint);if(bone==null)continue;rotations.Add(bone,bone.localRotation);bone.localRotation*=Quaternion.Euler(0,0,(joint==1?45:joint==2?30:20)*(side=="Left"?1:-1));}
                            report=WritePose(workspace,Path.Combine(folder,side+"_"+passes+"_"+blend.ToString(CultureInfo.InvariantCulture)+".json"),side+"Hand",selections[side]);
                        }
                        finally{foreach(var pair in rotations)pair.Key.localRotation=pair.Value;}
                        if(passes==0){baselines.Add(side,report);best.Add(side,report);}
                        Result baseline=baselines[side];bool accepted=passes>0&&report.maxAbsoluteLengthChange<=baseline.maxAbsoluteLengthChange&&report.maxStretch<=baseline.maxStretch&&report.minLengthRatio>=baseline.minLengthRatio&&report.lengthWeightedAbsoluteStrain<best[side].lengthWeightedAbsoluteStrain&&report.stretchedEdges<=baseline.stretchedEdges&&report.collapsedEdges<=baseline.collapsedEdges;
                        if(accepted){best[side]=report;Debug.Log("HAND SEARCH CANDIDATE "+side+" passes="+passes+" blend="+blend+" "+JsonUtility.ToJson(report));}
                        rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3:R},{4:R},{5},{6},{7}",side,passes,blend,report.maxAbsoluteLengthChange,report.lengthWeightedAbsoluteStrain,report.stretchedEdges,report.collapsedEdges,accepted));
                    }
                }
                File.WriteAllLines(Path.Combine(folder,"Search.csv"),rows);Debug.Log("HAND WEIGHT SEARCH COMPLETE; no mesh or scene saved.");
            }
            finally{foreach(var pair in originals){if(pair.Key.sharedMesh!=pair.Value)UnityEngine.Object.DestroyImmediate(pair.Key.sharedMesh);pair.Key.sharedMesh=pair.Value;}}
        }
    }
}
