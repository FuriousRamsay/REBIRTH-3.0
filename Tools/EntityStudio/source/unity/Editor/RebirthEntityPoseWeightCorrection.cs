using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio
{
    // Diagnostic coordinate search between the saved and diffused fields.
    // Evaluate the actual four-influence weights in every pose, including the
    // pruning step; do not optimize an eight-influence skin Unity will not use.
    public static class RebirthEntityPoseWeightCorrection
    {
        public static void ApplyWorkspace(RebirthEntityRigWorkspace workspace)
        {
            foreach(var renderer in workspace.sourceSkinnedMeshes)
            {
                var input=workspace.surfaceRepairInputs.Single(value=>value.repairedMesh==renderer.sharedMesh);
                var baseline=UnityEngine.Object.Instantiate(renderer.sharedMesh);
                try
                {
                    var pinned=input.repairVertexIds.Select(id=>id>0).ToArray();
                    RebirthEntityHandWeights.Diffuse(renderer,80,.05f,pinned,name=>name=="Hips"||name.StartsWith("Spine",StringComparison.Ordinal)||name.Contains("Leg")||name=="Neck"||name=="Head",float.PositiveInfinity);
                    Apply(workspace,renderer,baseline,new EntitySurfaceRepairInput{repairedMesh=baseline,repairVertexIds=input.repairVertexIds,repairVertexParts=input.repairVertexParts});
                    UnityEditor.EditorUtility.SetDirty(renderer.sharedMesh);
                }
                finally{UnityEngine.Object.DestroyImmediate(baseline);}
            }
        }

        public static void CommitSavedFarmer()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            if(workspace.profile.refineBodyWeightsWithPoses)throw new InvalidOperationException("Pose correction already applied; regenerate from source before applying again.");
            var originalProfile=workspace.profile;workspace.profile=UnityEngine.Object.Instantiate(originalProfile);
            foreach(var renderer in workspace.sourceSkinnedMeshes)
            {
                var input=workspace.surfaceRepairInputs.Single(value=>value.repairedMesh==renderer.sharedMesh);
                renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);input.repairedMesh=renderer.sharedMesh;
            }
            ApplyWorkspace(workspace);
            var coverageShapes=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true);
            var coverageSamples=RebirthEntityColliderPoseAudit.Capture(workspace,coverageShapes,true,true);
            RebirthEntityColliderPoseAudit.WriteMisses(coverageSamples,coverageShapes,System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerCorrectedSkin_ColliderMisses.csv"));
            var originalRadii=coverageShapes.ToDictionary(shape=>shape,shape=>shape.radius);
            var originalHeights=coverageShapes.ToDictionary(shape=>shape,shape=>shape.height);
            RebirthEntityColliderPoseAudit.CoverAxialLimits(coverageSamples,coverageShapes,.002f,.004f);
            RebirthEntityColliderPoseAudit.CoverAll(coverageSamples,coverageShapes,.002f);
            foreach(var shape in coverageShapes)
            {
                Vector3 scale=shape.transform.lossyScale;float across=Mathf.Max(Mathf.Abs(scale[(shape.direction+1)%3]),Mathf.Abs(scale[(shape.direction+2)%3]));
                float growth=(shape.radius-originalRadii[shape])*across;
                if(growth>.00201f)throw new InvalidOperationException("Corrected skin requires more than 2mm world radius adjustment: "+shape.name);
                float heightGrowth=(shape.height-originalHeights[shape])*Mathf.Abs(scale[shape.direction]);
                if(heightGrowth>.00401f)throw new InvalidOperationException("Corrected skin requires more than 4mm total world height adjustment: "+shape.name);
                if(growth>0)Debug.Log("MEASURED SKIN COLLIDER ADJUSTMENT "+shape.name+" worldRadiusGrowth="+growth);
                if(heightGrowth>0)Debug.Log("MEASURED SKIN COLLIDER ADJUSTMENT "+shape.name+" totalWorldHeightGrowth="+heightGrowth);
                var fit=workspace.colliderFitReports.Single(value=>value.collider==shape);fit.radius=shape.radius*across;fit.height=shape.height*Mathf.Abs(scale[shape.direction]);
            }
            int covered=RebirthEntityColliderCandidate.Verify(workspace);
            // No existing mesh assets are overwritten. Coverage must pass before
            // the new skin is persisted or any LOD hierarchy is regenerated.
            foreach(var renderer in workspace.sourceSkinnedMeshes)
                UnityEditor.AssetDatabase.CreateAsset(renderer.sharedMesh,UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_PoseCorrectedSkin.asset"));
            workspace.profile.refineBodyWeightsWithPoses=true;workspace.profile.reviewedSkeletonMapping=false;workspace.profile.reviewedColliderFit=false;workspace.profile.reviewedLods=false;
            // Retain the old LOD assets for rollback; rebuild a fresh hierarchy.
            workspace.generatedAssetId=null;
            RebirthEntityRigBuilder.RebuildLods(workspace);
            var eyes=workspace.generatedSkeleton.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.CompareTag("E_BP_Eye")).Cast<Renderer>().ToArray();
            var levels=workspace.lodGroup.GetLODs();
            for(int i=0;i<levels.Length;i++)levels[i].renderers=levels[i].renderers.Concat(eyes).Distinct().ToArray();workspace.lodGroup.SetLODs(levels);
            RebirthEntitySkinnedBounds.FitRestPose(workspace);
            UnityEditor.EditorUtility.SetDirty(workspace.profile);UnityEditor.EditorUtility.SetDirty(workspace);UnityEditor.AssetDatabase.SaveAssets();
            RebirthEntityColliderCandidate.Verify(workspace);
            workspace.profileBuildFingerprint=RebirthEntityRigBuilder.ComputeProfileFingerprint(workspace.profile,workspace.sourceModelKey);
            var candidateProfile=workspace.profile;candidateProfile.name=originalProfile.name;UnityEditor.EditorUtility.CopySerialized(candidateProfile,originalProfile);workspace.profile=originalProfile;
            UnityEditor.EditorUtility.SetDirty(originalProfile);UnityEditor.AssetDatabase.SaveAssets();UnityEngine.Object.DestroyImmediate(candidateProfile);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save corrected skin.");
            Debug.Log("POSE-CORRECTED SKIN COMMITTED: posedSurfaceSamples="+covered+" LODs rebuilt; review flags remain false.");
        }

        sealed class Node {public readonly List<int> vertices=new List<int>();public readonly HashSet<int> neighbors=new HashSet<int>();public int level=10;public bool pinned;}
        public static void Apply(RebirthEntityRigWorkspace workspace,SkinnedMeshRenderer renderer,Mesh baseline,EntitySurfaceRepairInput input)
        {
            Mesh mesh=renderer.sharedMesh;Vector3[] points=mesh.vertices;BoneWeight[] original=baseline.boneWeights,current=mesh.boneWeights;
            Vector3[] restPoints=points.Select(renderer.transform.TransformPoint).ToArray();
            if(original.Length!=points.Length||input.repairedMesh!=baseline||input.repairVertexIds.Length!=points.Length)throw new InvalidOperationException("Pose correction needs matching baseline and repair provenance.");
            var ids=new int[points.Length];var nodes=new List<Node>();var lookup=new Dictionary<(Vector3Int,int),int>();
            for(int i=0;i<points.Length;i++)
            {
                var key=(Vector3Int.RoundToInt(renderer.transform.TransformPoint(points[i])*100000f),input.repairVertexParts[i]);
                if(!lookup.TryGetValue(key,out int id)){id=nodes.Count;lookup.Add(key,id);nodes.Add(new Node());}
                ids[i]=id;nodes[id].vertices.Add(i);if(input.repairVertexIds[i]>0)nodes[id].pinned=true;
            }
            foreach(var node in nodes)if(node.vertices.All(i=>original[i].Equals(current[i])))node.pinned=true;
            int[] triangles=mesh.triangles;
            for(int t=0;t<triangles.Length;t+=3){Edge(ids[triangles[t]],ids[triangles[t+1]]);Edge(ids[triangles[t+1]],ids[triangles[t+2]]);Edge(ids[triangles[t+2]],ids[triangles[t]]);}
            var weights=new BoneWeight[11][];var posed=new Vector3[11][][];
            try
            {
                for(int level=0;level<=10;level++)
                {
                    weights[level]=new BoneWeight[points.Length];for(int i=0;i<points.Length;i++)weights[level][i]=Blend(original[i],current[i],level*.1f);
                    mesh.boneWeights=weights[level];var samples=RebirthEntityColliderPoseAudit.Capture(workspace,Array.Empty<CapsuleCollider>());
                    posed[level]=new Vector3[samples.Count][];
                    for(int pose=0;pose<samples.Count;pose++)
                    {
                        posed[level][pose]=new Vector3[nodes.Count];var sample=samples[pose];var vertexPoints=new Vector3[points.Length];
                        for(int p=0;p<sample.points.Length;p++)if(sample.sourceRenderers[sample.sourceRendererIndices[p]]==renderer)vertexPoints[sample.sourceVertices[p]]=sample.points[p];
                        for(int node=0;node<nodes.Count;node++)
                        {
                            Vector3 value=vertexPoints[nodes[node].vertices[0]];posed[level][pose][node]=value;
                            if(!nodes[node].pinned)foreach(int vertex in nodes[node].vertices)
                                if(Vector3.Distance(value,vertexPoints[vertex])>.00002f)throw new InvalidOperationException("Welded pose candidates disagree.");
                        }
                    }
                }
                double initial=Energy();int changes=0;
                for(int pass=0;pass<8;pass++)
                {
                    int changed=0;double before=Energy();
                    for(int node=0;node<nodes.Count;node++)
                    {
                        if(nodes[node].pinned)continue;int best=nodes[node].level;double cost=Local(node,best);
                        for(int candidate=0;candidate<=10;candidate++)
                        {double value=Local(node,candidate);if(value+1e-9<cost){cost=value;best=candidate;}}
                        if(best!=nodes[node].level){nodes[node].level=best;changed++;}
                    }
                    double after=Energy();if(after>before+1e-5)throw new InvalidOperationException("Pose correction increased its objective.");
                    Debug.Log("POSE WEIGHT CORRECTION pass="+pass+" changedNodes="+changed+" energy="+before+"->"+after);changes+=changed;if(changed==0)break;
                }
                var result=(BoneWeight[])current.Clone();
                foreach(var node in nodes)if(!node.pinned)foreach(int vertex in node.vertices)result[vertex]=weights[node.level][vertex];
                for(int i=0;i<result.Length;i++)
                {
                    BoneWeight w=result[i];float sum=w.weight0+w.weight1+w.weight2+w.weight3;
                    if(float.IsNaN(sum)||Mathf.Abs(sum-1)>.0001f||Mathf.Min(w.weight0,w.weight1,w.weight2,w.weight3)<0)throw new InvalidOperationException("Invalid corrected weights.");
                    if(input.repairVertexIds[i]>0&&!w.Equals(current[i]))throw new InvalidOperationException("Protected repair weight changed.");
                }
                current=result;Debug.Log("POSE WEIGHT CORRECTION COMPLETE: energy="+initial+"->"+Energy()+" changes="+changes+" protectedRepairs="+input.repairVertexIds.Count(id=>id>0));
            }
            finally{mesh.boneWeights=current;}
            void Edge(int a,int b){if(a==b)return;float length=Vector3.Distance(restPoints[nodes[a].vertices[0]],restPoints[nodes[b].vertices[0]]);if(length<.0001f)return;nodes[a].neighbors.Add(b);nodes[b].neighbors.Add(a);}
            double Local(int node,int level){double value=.0005*(10-level)*(10-level)/100;foreach(int neighbor in nodes[node].neighbors)value+=Pair(node,level,neighbor,nodes[neighbor].level);return value;}
            double Energy(){double total=0;for(int i=0;i<nodes.Count;i++){total+=.0005*(10-nodes[i].level)*(10-nodes[i].level)/100;foreach(int j in nodes[i].neighbors)if(i<j)total+=Pair(i,nodes[i].level,j,nodes[j].level);}return total;}
            double Pair(int a,int la,int b,int lb)
            {
                float rest=Vector3.Distance(restPoints[nodes[a].vertices[0]],restPoints[nodes[b].vertices[0]]);double cost=0;
                for(int pose=0;pose<posed[0].Length;pose++)
                {
                    float length=Vector3.Distance(posed[la][pose][a],posed[lb][pose][b]),ratio=length/rest;
                    double stretch=Math.Max(0,ratio-1.8),collapse=Math.Max(0,.35-ratio),absolute=Math.Max(0,Math.Abs(length-rest)-.04)/Math.Max(rest,.01);
                    cost+=stretch*stretch+16*collapse*collapse+.5*absolute*absolute+.02*(ratio-1)*(ratio-1);
                }
                if(!nodes[a].pinned&&!nodes[b].pinned)cost+=.001*(la-lb)*(la-lb)/100;return cost;
            }
        }
        static BoneWeight Blend(BoneWeight a,BoneWeight b,float t)
        {
            if(t==0)return a;if(t==1)return b;
            var values=new Dictionary<int,float>();Add(a,1-t);Add(b,t);var order=values.OrderByDescending(p=>p.Value).ThenBy(p=>p.Key).Take(4).ToArray();float sum=order.Sum(p=>p.Value);
            int I(int n)=>n<order.Length?order[n].Key:0;float W(int n)=>n<order.Length?order[n].Value/sum:0;
            return new BoneWeight{boneIndex0=I(0),weight0=W(0),boneIndex1=I(1),weight1=W(1),boneIndex2=I(2),weight2=W(2),boneIndex3=I(3),weight3=W(3)};
            void Add(BoneWeight w,float scale){Acc(w.boneIndex0,w.weight0*scale);Acc(w.boneIndex1,w.weight1*scale);Acc(w.boneIndex2,w.weight2*scale);Acc(w.boneIndex3,w.weight3*scale);}
            void Acc(int bone,float amount){if(amount<=0)return;values.TryGetValue(bone,out float previous);values[bone]=previous+amount;}
        }
    }
}
