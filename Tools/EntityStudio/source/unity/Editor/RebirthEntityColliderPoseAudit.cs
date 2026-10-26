using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    // Authoring probes, not a substitute for the target game's animation clips.
    // Snapshot both skinned points and collider frames before any fitting so
    // candidate parameters can be compared against precisely the same poses.
    public static class RebirthEntityColliderPoseAudit
    {
        public sealed class Sample
        {
            public string name;
            public Vector3[] points;
            public Frame[] frames;
            public bool[] baselineCovered;
            public int[] pointRegions,allowedRegionMasks,capsuleRegions;
            public bool respectDamageRegions;
            public List<SkinnedMeshRenderer> sourceRenderers;
            public int[] sourceRendererIndices,sourceVertices;
        }
        public struct Frame
        {
            public Matrix4x4 localToWorld;
            public Vector3 axis;
            public float along,across;
        }
        [Serializable] public sealed class Result
        {
            public string pose;
            public int samples,baselineOutside,candidateOutside,newMisses;
        }
        [Serializable] public sealed class Report { public List<Result> poses=new List<Result>(); }

        // Adjacent upper/lower shapes may share coverage within one limb, but a
        // head sample must never be accepted solely because the torso covers it.
        private static int DamageRegion(Transform bone)
        {
            for(Transform node=bone;node!=null;node=node.parent)
            {
                string tag=node.tag;
                if(tag=="E_BP_Head")return 1;
                if(tag=="E_BP_LArm"||tag=="E_BP_LLowerArm")return 2;
                if(tag=="E_BP_RArm"||tag=="E_BP_RLowerArm")return 3;
                if(tag=="E_BP_LLeg"||tag=="E_BP_LLowerLeg")return 4;
                if(tag=="E_BP_RLeg"||tag=="E_BP_RLowerLeg")return 5;
                if(tag=="E_BP_Body")return 0;
            }
            return 0;
        }
        public static bool Allows(Sample sample,int point,int capsule)
        {return !sample.respectDamageRegions||(sample.allowedRegionMasks[point]&(1<<sample.capsuleRegions[capsule]))!=0;}

        public static bool Contains(CapsuleCollider capsule,Frame frame,Vector3 point)
        {
            float radius=capsule.radius*frame.across;
            float half=Mathf.Max(0,capsule.height*frame.along*.5f-radius);
            Vector3 delta=point-frame.localToWorld.MultiplyPoint3x4(capsule.center);
            Vector3 residual=delta-frame.axis*Mathf.Clamp(Vector3.Dot(delta,frame.axis),-half,half);
            return residual.sqrMagnitude<=radius*radius+1e-8f;
        }

        public static void CoverAll(List<Sample> samples,CapsuleCollider[] capsules,float maximumRadiusGrowth,Dictionary<CapsuleCollider,float> referenceRadii=null)
        {
            var initial=new float[capsules.Length];for(int i=0;i<capsules.Length;i++)initial[i]=referenceRadii!=null&&referenceRadii.TryGetValue(capsules[i],out float reference)?reference:capsules[i].radius;
            int repaired=0;
            foreach(var sample in samples)for(int pointIndex=0;pointIndex<sample.points.Length;pointIndex++)
            {
                Vector3 point=sample.points[pointIndex];bool covered=false;for(int i=0;i<capsules.Length;i++)if(Allows(sample,pointIndex,i)&&Contains(capsules[i],sample.frames[i],point)){covered=true;break;}if(covered)continue;
                int chosen=-1;float chosenRadius=0,bestGrowth=float.PositiveInfinity;
                for(int i=0;i<capsules.Length;i++)
                {
                    if(!Allows(sample,pointIndex,i))continue;
                    var capsule=capsules[i];float original=capsule.radius,low=original,high=initial[i]+maximumRadiusGrowth;
                    try
                    {
                        capsule.radius=high;if(!Contains(capsule,sample.frames[i],point))continue;
                        for(int step=0;step<20;step++){float middle=(low+high)*.5f;capsule.radius=middle;if(Contains(capsule,sample.frames[i],point))high=middle;else low=middle;}
                        float growth=(high-original)*sample.frames[i].across;
                        if(growth<bestGrowth){bestGrowth=growth;chosen=i;chosenRadius=high;}
                    }
                    finally{capsule.radius=original;}
                }
                if(chosen<0)
                {
                    int vertex=sample.sourceVertices[pointIndex];var renderer=sample.sourceRenderers[sample.sourceRendererIndices[pointIndex]];
                    string source=renderer.name+" surfaceSample="+vertex;
                    if(vertex>=0)
                    {
                        BoneWeight w=renderer.sharedMesh.boneWeights[vertex];
                        source=renderer.name+" vertex="+vertex+" weights="+renderer.bones[w.boneIndex0].name+":"+w.weight0+","+renderer.bones[w.boneIndex1].name+":"+w.weight1+","+renderer.bones[w.boneIndex2].name+":"+w.weight2+","+renderer.bones[w.boneIndex3].name+":"+w.weight3;
                    }
                    throw new InvalidOperationException("Pose "+sample.name+" pointIndex="+pointIndex+" region="+sample.pointRegions[pointIndex]+" world="+point.ToString("F6")+" source="+source+" requires more than "+maximumRadiusGrowth+" local radius growth; automatic expansion refused.");
                }
                capsules[chosen].radius=chosenRadius;repaired++;
            }
            Debug.Log("COLLIDER POSE COVERAGE SEED: repairedSamples="+repaired+" maximumLocalRadiusGrowth="+maximumRadiusGrowth);
        }

        // Radius growth cannot reach beyond a capsule's fixed axial ends. Extend
        // only a shape needed by an actual sample, within an independent budget.
        public static void CoverAxialLimits(List<Sample> samples,CapsuleCollider[] capsules,float maximumRadiusGrowth,float maximumHeightGrowth)
        {
            var initialHeight=new float[capsules.Length];for(int i=0;i<capsules.Length;i++)initialHeight[i]=capsules[i].height;
            foreach(var sample in samples)for(int point=0;point<sample.points.Length;point++)
            {
                bool alreadyCovered=false;for(int i=0;i<capsules.Length;i++)if(Allows(sample,point,i)&&Contains(capsules[i],sample.frames[i],sample.points[point])){alreadyCovered=true;break;}
                if(alreadyCovered)continue;
                bool possible=false;
                for(int i=0;i<capsules.Length;i++)
                {
                    if(!Allows(sample,point,i))continue;float radius=capsules[i].radius;
                    try{capsules[i].radius=radius+maximumRadiusGrowth;if(Contains(capsules[i],sample.frames[i],sample.points[point])){possible=true;break;}}
                    finally{capsules[i].radius=radius;}
                }
                if(possible)continue;
                int chosen=-1;float chosenHeight=0,best=float.PositiveInfinity;
                for(int i=0;i<capsules.Length;i++)
                {
                    if(!Allows(sample,point,i))continue;var shape=capsules[i];float radius=shape.radius,height=shape.height;
                    try
                    {
                        float low=height,high=initialHeight[i]+maximumHeightGrowth;shape.radius=radius+maximumRadiusGrowth;shape.height=high;
                        if(!Contains(shape,sample.frames[i],sample.points[point]))continue;
                        for(int step=0;step<20;step++){float middle=(low+high)*.5f;shape.height=middle;if(Contains(shape,sample.frames[i],sample.points[point]))high=middle;else low=middle;}
                        float growth=(high-height)*sample.frames[i].along;if(growth<best){best=growth;chosen=i;chosenHeight=high;}
                    }
                    finally{shape.radius=radius;shape.height=height;}
                }
                if(chosen<0)throw new InvalidOperationException("Pose "+sample.name+" point "+point+" exceeds bounded axial/radial fitting budgets.");
                capsules[chosen].height=chosenHeight;
            }
        }

        public static List<Sample> Capture(RebirthEntityRigWorkspace workspace,CapsuleCollider[] capsules,bool respectDamageRegions=false,bool includeSurfaceSamples=false)
        {
            var rotations=new Dictionary<Transform,Quaternion>();
            foreach(var bone in workspace.generatedSkeleton.GetComponentsInChildren<Transform>(true))rotations.Add(bone,bone.localRotation);
            var result=new List<Sample>();
            try
            {
                foreach(string pose in new[]{"rest","elbows","knees","shoulders","neckTurn","crouch","reach"})
                {
                    foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                    if(pose=="elbows"||pose=="reach")foreach(string side in new[]{"Left","Right"})Rotate(side+"ForeArm",new Vector3(70,0,0));
                    if(pose=="knees")foreach(string side in new[]{"Left","Right"})Rotate(side+"Leg",new Vector3(75,0,0));
                    if(pose=="shoulders"||pose=="reach"){Rotate("LeftArm",new Vector3(25,0,-35));Rotate("RightArm",new Vector3(25,0,35));}
                    if(pose=="neckTurn")Rotate("Neck",new Vector3(20,45,0));
                    if(pose=="crouch")foreach(string side in new[]{"Left","Right"}){Rotate(side+"UpLeg",new Vector3(-45,0,0));Rotate(side+"Leg",new Vector3(90,0,0));}
                    var points=new List<Vector3>();var regions=new List<int>();var masks=new List<int>();var sourceRenderers=new List<int>();var sourceVertices=new List<int>();int rendererIndex=0;
                    foreach(var renderer in workspace.sourceSkinnedMeshes)
                    {
                        Mesh mesh=renderer.sharedMesh;Vector3[] vertices=mesh.vertices;BoneWeight[] weights=mesh.boneWeights;
                        int offset=points.Count;var support=new float[vertices.Length][];
                        Matrix4x4[] binds=mesh.bindposes;var matrices=new Matrix4x4[renderer.bones.Length];
                        for(int i=0;i<matrices.Length;i++)matrices[i]=renderer.bones[i].localToWorldMatrix*binds[i];
                        for(int i=0;i<vertices.Length;i++)
                        {
                            BoneWeight w=weights[i];Vector3 p=vertices[i];
                            points.Add(matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3);
                            var amounts=new float[6];Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
                            void Add(int bone,float weight){amounts[DamageRegion(renderer.bones[bone])]+=weight;}
                            support[i]=amounts;AddRegion(amounts);sourceRenderers.Add(rendererIndex);sourceVertices.Add(i);
                        }
                        void AddRegion(float[] amounts)
                        {
                            int region=0;for(int candidate=1;candidate<amounts.Length;candidate++)if(amounts[candidate]>amounts[region])region=candidate;regions.Add(region);
                            // A vertex blended across a joint belongs to both
                            // substantially influencing regions. An exclusive
                            // majority vote is unstable around 50/50 seams.
                            int mask=1<<region;for(int candidate=0;candidate<amounts.Length;candidate++)if(amounts[candidate]>=.20f)mask|=1<<candidate;masks.Add(mask);
                        }
                        if(includeSurfaceSamples)
                        {
                            var edges=new HashSet<ulong>();int[] triangles=mesh.triangles;
                            for(int triangle=0;triangle<triangles.Length;triangle+=3)
                            {
                                int a=triangles[triangle],b=triangles[triangle+1],c=triangles[triangle+2];AddSample(a,b,c,false);
                                Edge(a,b);Edge(b,c);Edge(c,a);
                            }
                            void Edge(int a,int b){ulong key=((ulong)(uint)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);if(edges.Add(key))AddSample(a,b,b,true);}
                            void AddSample(int a,int b,int c,bool edge)
                            {
                                float divisor=edge?2:3;points.Add(edge?(points[offset+a]+points[offset+b])/divisor:(points[offset+a]+points[offset+b]+points[offset+c])/divisor);
                                var amounts=new float[6];for(int region=0;region<6;region++)amounts[region]=(support[a][region]+support[b][region]+(edge?0:support[c][region]))/divisor;
                                AddRegion(amounts);sourceRenderers.Add(rendererIndex);sourceVertices.Add(edge?-2:-1);
                            }
                        }
                        rendererIndex++;
                    }
                    var sample=new Sample{name=pose,points=points.ToArray(),pointRegions=regions.ToArray(),allowedRegionMasks=masks.ToArray(),sourceRendererIndices=sourceRenderers.ToArray(),sourceVertices=sourceVertices.ToArray(),capsuleRegions=new int[capsules.Length],respectDamageRegions=respectDamageRegions,sourceRenderers=new List<SkinnedMeshRenderer>(workspace.sourceSkinnedMeshes),frames=new Frame[capsules.Length],baselineCovered=new bool[points.Count]};
                    for(int i=0;i<capsules.Length;i++)
                    {
                        var capsule=capsules[i];Vector3 scale=capsule.transform.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
                        sample.capsuleRegions[i]=DamageRegion(capsule.transform);
                        Vector3 axis=capsule.direction==0?Vector3.right:capsule.direction==1?Vector3.up:Vector3.forward;
                        sample.frames[i]=new Frame{localToWorld=capsule.transform.localToWorldMatrix,axis=capsule.transform.TransformDirection(axis).normalized,along=scale[capsule.direction],across=capsule.direction==0?Mathf.Max(scale.y,scale.z):capsule.direction==1?Mathf.Max(scale.x,scale.z):Mathf.Max(scale.x,scale.y)};
                    }
                    for(int point=0;point<sample.points.Length;point++)for(int i=0;i<capsules.Length;i++)
                        if(Allows(sample,point,i)&&Contains(capsules[i],sample.frames[i],sample.points[point])){sample.baselineCovered[point]=true;break;}
                    result.Add(sample);
                }
            }
            finally{foreach(var pair in rotations)pair.Key.localRotation=pair.Value;}
            return result;

            void Rotate(string name,Vector3 euler)
            {
                Transform bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,name);
                if(bone==null)throw new InvalidOperationException("Pose audit bone missing: "+name);
                bone.localRotation*=Quaternion.Euler(euler);
            }
        }

        public static Report Compare(List<Sample> samples,CapsuleCollider[] capsules)
        {
            var report=new Report();
            foreach(var sample in samples)
            {
                var result=new Result{pose=sample.name,samples=sample.points.Length};
                for(int point=0;point<sample.points.Length;point++)
                {
                    if(!sample.baselineCovered[point])result.baselineOutside++;
                    bool covered=false;for(int i=0;i<capsules.Length;i++)if(Allows(sample,point,i)&&Contains(capsules[i],sample.frames[i],sample.points[point])){covered=true;break;}
                    if(!covered){result.candidateOutside++;if(sample.baselineCovered[point])result.newMisses++;}
                }
                report.poses.Add(result);
            }
            return report;
        }

        public static void WriteMisses(List<Sample> samples,CapsuleCollider[] capsules,string path)
        {
            var rows=new List<string>{"pose,point,renderer,sourceVertex,allowedMask,x,y,z,nearestAllowedCapsule,worldGap"};
            foreach(var sample in samples)for(int point=0;point<sample.points.Length;point++)
            {
                bool covered=false;float best=float.PositiveInfinity;int nearest=-1;
                for(int i=0;i<capsules.Length;i++)
                {
                    if(!Allows(sample,point,i))continue;
                    if(Contains(capsules[i],sample.frames[i],sample.points[point])){covered=true;break;}
                    var frame=sample.frames[i];float radius=capsules[i].radius*frame.across,half=Mathf.Max(0,capsules[i].height*frame.along*.5f-radius);
                    Vector3 delta=sample.points[point]-frame.localToWorld.MultiplyPoint3x4(capsules[i].center);
                    float gap=(delta-frame.axis*Mathf.Clamp(Vector3.Dot(delta,frame.axis),-half,half)).magnitude-radius;
                    if(gap<best){best=gap;nearest=i;}
                }
                if(covered)continue;Vector3 p=sample.points[point];
                rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5:R},{6:R},{7:R},{8},{9:R}",sample.name,point,sample.sourceRenderers[sample.sourceRendererIndices[point]].name,sample.sourceVertices[point],sample.allowedRegionMasks[point],p.x,p.y,p.z,nearest>=0?capsules[nearest].name:"none",best));
            }
            System.IO.File.WriteAllLines(path,rows);
        }
    }
}
