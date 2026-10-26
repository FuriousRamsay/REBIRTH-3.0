using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityHandWeights
    {
        public static void DiffuseWorkspace(RebirthEntityRigWorkspace workspace)
        {
            foreach(var renderer in workspace.sourceSkinnedMeshes)
            {
                bool hasHand=false;foreach(var bone in renderer.bones)if(bone.name.Contains("Hand")){hasHand=true;break;}if(!hasHand)continue;
                bool[] pinned=null;
                foreach(var input in workspace.surfaceRepairInputs)
                    if(input.repairedMesh==renderer.sharedMesh){pinned=new bool[input.repairVertexIds.Length];for(int i=0;i<pinned.Length;i++)pinned[i]=input.repairVertexIds[i]>0;break;}
                if(workspace.profile.separateFusedArms&&pinned==null)throw new InvalidOperationException("Hand diffusion requires current repair correspondence; regenerate from the source model.");
                Diffuse(renderer,pinnedVertices:pinned);UnityEditor.EditorUtility.SetDirty(renderer.sharedMesh);
            }
        }
        // Diagnostic candidate: diffuse complete influence vectors on the welded
        // surface. Repeatedly pruning to four influences introduces discontinuities
        // before the diffusion converges. Keep the source field as a soft constraint.
        public static void Diffuse(SkinnedMeshRenderer renderer,int passes=40,float sourceStrength=.15f,bool[] pinnedVertices=null,Func<string,bool> selection=null,float maximumEdgeLength=.02f)
        {
            Mesh mesh=renderer.sharedMesh;Vector3[] points=mesh.vertices;BoneWeight[] original=mesh.boneWeights;
            if(pinnedVertices!=null&&pinnedVertices.Length!=points.Length)throw new ArgumentException("Pinned vertex mask must match the mesh.");
            if(passes<1||sourceStrength<=0||sourceStrength>1||float.IsNaN(sourceStrength))throw new ArgumentOutOfRangeException("Diffusion needs positive passes and source strength in (0,1].");
            if(float.IsNaN(maximumEdgeLength)||maximumEdgeLength<=0)throw new ArgumentOutOfRangeException(nameof(maximumEdgeLength));
            var ids=new int[points.Length];var lookup=new Dictionary<(Vector3Int,int),int>();var members=new List<List<int>>();var world=new List<Vector3>();
            for(int i=0;i<points.Length;i++)
            {
                Vector3 point=renderer.transform.TransformPoint(points[i]);BoneWeight initial=original[i];float left=0,right=0;
                Accumulate(initial.boneIndex0,initial.weight0);Accumulate(initial.boneIndex1,initial.weight1);Accumulate(initial.boneIndex2,initial.weight2);Accumulate(initial.boneIndex3,initial.weight3);
                void Accumulate(int bone,float weight){string name=renderer.bones[bone].name;if(!name.Contains("Hand")&&!name.Contains("Arm")&&!name.Contains("Shoulder"))return;if(name.StartsWith("Left",StringComparison.Ordinal))left+=weight;else if(name.StartsWith("Right",StringComparison.Ordinal))right+=weight;}
                // Coincident vertices on the anatomical cut belong to separate
                // surfaces. Never weld a hand/arm back to the body at that seam.
                int region=left>.5f?1:right>.5f?2:0;var key=(Vector3Int.RoundToInt(point*100000f),region);
                if(!lookup.TryGetValue(key,out int id)){id=members.Count;lookup.Add(key,id);members.Add(new List<int>());world.Add(point);}ids[i]=id;members[id].Add(i);
            }
            int count=members.Count,bones=renderer.bones.Length;var neighbors=new HashSet<int>[count];var selected=new bool[count];var seed=new float[count][];var field=new float[count][];
            for(int i=0;i<count;i++)
            {
                neighbors[i]=new HashSet<int>();seed[i]=new float[bones];
                foreach(int vertex in members[i])
                {
                    BoneWeight w=original[vertex];Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
                    void Add(int bone,float value){seed[i][bone]+=value/members[i].Count;if(value>.05f&&(selection!=null?selection(renderer.bones[bone].name):renderer.bones[bone].name.Contains("Hand")))selected[i]=true;}
                }
                if(pinnedVertices!=null)foreach(int vertex in members[i])if(pinnedVertices[vertex]){selected[i]=false;break;}
                field[i]=(float[])seed[i].Clone();
            }
            int[] triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)for(int edge=0;edge<3;edge++)
            {
                int a=ids[triangles[i+edge]],b=ids[triangles[i+(edge+1)%3]];
                if(a==b||Vector3.Distance(world[a],world[b])>maximumEdgeLength)continue;neighbors[a].Add(b);neighbors[b].Add(a);
            }
            for(int pass=0;pass<passes;pass++)
            {
                var next=new float[count][];
                for(int i=0;i<count;i++)
                {
                    next[i]=(float[])seed[i].Clone();if(!selected[i]||neighbors[i].Count==0)continue;
                    float total=0;var average=new float[bones];
                    foreach(int neighbor in neighbors[i])
                    {
                        float weight=1f/Mathf.Max(Vector3.Distance(world[i],world[neighbor]),.0005f);total+=weight;
                        for(int bone=0;bone<bones;bone++)average[bone]+=field[neighbor][bone]*weight;
                    }
                    for(int bone=0;bone<bones;bone++)next[i][bone]=sourceStrength*seed[i][bone]+(1-sourceStrength)*average[bone]/total;
                }
                field=next;
            }
            var result=(BoneWeight[])original.Clone();
            for(int i=0;i<count;i++)
            {
                if(!selected[i])continue;var order=new List<int>();for(int bone=0;bone<bones;bone++)order.Add(bone);int node=i;
                order.Sort((a,b)=>field[node][b].CompareTo(field[node][a]));float total=0;for(int slot=0;slot<4;slot++)total+=field[i][order[slot]];
                if(total<=0)throw new InvalidOperationException("Hand diffusion lost normalized support.");
                var weight=new BoneWeight{boneIndex0=order[0],weight0=field[i][order[0]]/total,boneIndex1=order[1],weight1=field[i][order[1]]/total,boneIndex2=order[2],weight2=field[i][order[2]]/total,boneIndex3=order[3],weight3=field[i][order[3]]/total};
                foreach(int vertex in members[i])result[vertex]=weight;
            }
            int changed=0,pinnedCount=0;
            for(int vertex=0;vertex<result.Length;vertex++)
            {
                BoneWeight w=result[vertex];float sum=w.weight0+w.weight1+w.weight2+w.weight3;
                if(float.IsNaN(sum)||Mathf.Abs(sum-1)>.0001f||Mathf.Min(w.weight0,w.weight1,w.weight2,w.weight3)<0)throw new InvalidOperationException("Diffusion produced invalid skin weights.");
                if(pinnedVertices!=null&&pinnedVertices[vertex]){pinnedCount++;if(!w.Equals(original[vertex]))throw new InvalidOperationException("Diffusion changed a protected repair vertex.");}
                if(!w.Equals(original[vertex]))changed++;
            }
            mesh.boneWeights=result;
            Debug.Log("HAND FULL-INFLUENCE DIFFUSION: renderer="+renderer.name+" changed="+changed+" protectedRepairs="+pinnedCount+" normalized=true");
        }

        public static void RefitEuclidean(SkinnedMeshRenderer renderer,bool singleFingerChain=false)
        {
            Mesh mesh=renderer.sharedMesh;Vector3[] points=mesh.vertices;BoneWeight[] weights=mesh.boneWeights;
            foreach(string side in new[]{"Left","Right"})
            {
                string prefix=side+"Hand";var candidates=new List<int>();var ends=new Dictionary<int,Vector3>();
                for(int bone=0;bone<renderer.bones.Length;bone++)
                {
                    Transform transform=renderer.bones[bone];if(!transform.name.StartsWith(prefix,StringComparison.Ordinal))continue;
                    string childName=transform.name==prefix?prefix+"Middle1":transform.name.Substring(0,transform.name.Length-1)+(char)(transform.name[transform.name.Length-1]+1);
                    Transform child=transform.Find(childName);if(child==null)continue;
                    candidates.Add(bone);ends.Add(bone,child.position);
                }
                for(int vertex=0;vertex<points.Length;vertex++)
                {
                    BoneWeight prior=weights[vertex];float hand=Amount(prior.boneIndex0,prior.weight0)+Amount(prior.boneIndex1,prior.weight1)+Amount(prior.boneIndex2,prior.weight2)+Amount(prior.boneIndex3,prior.weight3);
                    if(hand<.8f)continue;
                    Vector3 point=renderer.transform.TransformPoint(points[vertex]);var distances=new List<KeyValuePair<int,float>>();
                    foreach(int bone in candidates)
                    {
                        Vector3 start=renderer.bones[bone].position,segment=ends[bone]-start;
                        float t=segment.sqrMagnitude>1e-12f?Mathf.Clamp01(Vector3.Dot(point-start,segment)/segment.sqrMagnitude):0;
                        distances.Add(new KeyValuePair<int,float>(bone,Mathf.Max((point-start-segment*t).sqrMagnitude,1e-8f)));
                    }
                    distances.Sort((a,b)=>a.Value.CompareTo(b.Value));if(distances.Count<5)throw new InvalidOperationException("Complete hand chains required.");
                    if(singleFingerChain)
                    {
                        string chain=null;
                        foreach(var candidate in distances)
                        {string name=renderer.bones[candidate.Key].name;if(name==prefix)continue;chain=name.Substring(0,name.Length-1);break;}
                        distances.RemoveAll(candidate=>renderer.bones[candidate.Key].name!=prefix&&!renderer.bones[candidate.Key].name.StartsWith(chain,StringComparison.Ordinal));
                        if(distances.Count!=4)throw new InvalidOperationException("Each finger requires three deforming bones and a palm influence.");
                    }
                    var values=new float[4];float total=0,cutoff=distances.Count>4?1f/distances[4].Value:0;
                    for(int i=0;i<4;i++){values[i]=Mathf.Max(0,1f/distances[i].Value-cutoff);total+=values[i];}
                    if(total<=0)continue;
                    weights[vertex]=new BoneWeight{boneIndex0=distances[0].Key,weight0=values[0]/total,boneIndex1=distances[1].Key,weight1=values[1]/total,boneIndex2=distances[2].Key,weight2=values[2]/total,boneIndex3=distances[3].Key,weight3=values[3]/total};
                }
                float Amount(int bone,float value)=>renderer.bones[bone].name.StartsWith(prefix,StringComparison.Ordinal)?value:0;
            }
            mesh.boneWeights=weights;
        }

        public static void Smooth(SkinnedMeshRenderer renderer,int passes=3,float blend=.35f)
        {
            Mesh mesh=renderer.sharedMesh;var points=mesh.vertices;var weights=mesh.boneWeights;var triangles=mesh.triangles;
            var welded=new Dictionary<Vector3Int,List<int>>();var neighbors=new HashSet<int>[points.Length];var hand=new bool[points.Length];
            for(int i=0;i<points.Length;i++)
            {
                points[i]=renderer.transform.TransformPoint(points[i]);neighbors[i]=new HashSet<int>();
                Vector3Int key=Vector3Int.RoundToInt(points[i]*100000f);if(!welded.TryGetValue(key,out var members)){members=new List<int>();welded[key]=members;}members.Add(i);
                BoneWeight w=weights[i];hand[i]=IsHand(w.boneIndex0,w.weight0)||IsHand(w.boneIndex1,w.weight1)||IsHand(w.boneIndex2,w.weight2)||IsHand(w.boneIndex3,w.weight3);
            }
            bool IsHand(int index,float weight)=>weight>.05f&&renderer.bones[index].name.Contains("Hand");
            for(int i=0;i<triangles.Length;i+=3)for(int edge=0;edge<3;edge++)
            {
                int a=triangles[i+edge],b=triangles[i+(edge+1)%3];
                if(Vector3.Distance(points[a],points[b])>.02f)continue;neighbors[a].Add(b);neighbors[b].Add(a);
            }
            foreach(var members in welded.Values)
            {
                var all=new HashSet<int>();foreach(int member in members)all.UnionWith(neighbors[member]);foreach(int member in members){neighbors[member].UnionWith(all);foreach(int other in members)if(other!=member)neighbors[member].Add(other);}
            }
            for(int pass=0;pass<passes;pass++)
            {
                var next=(BoneWeight[])weights.Clone();
                for(int i=0;i<weights.Length;i++)
                {
                    if(!hand[i]||neighbors[i].Count==0)continue;var sums=new Dictionary<int,float>();Add(weights[i],1f-blend);
                    foreach(int neighbor in neighbors[i])Add(weights[neighbor],blend/neighbors[i].Count);
                    var ordered=new List<KeyValuePair<int,float>>(sums);ordered.Sort((a,b)=>b.Value.CompareTo(a.Value));float total=0;for(int slot=0;slot<Math.Min(4,ordered.Count);slot++)total+=ordered[slot].Value;
                    var w=new BoneWeight();for(int slot=0;slot<Math.Min(4,ordered.Count);slot++){int bone=ordered[slot].Key;float amount=ordered[slot].Value/total;if(slot==0){w.boneIndex0=bone;w.weight0=amount;}else if(slot==1){w.boneIndex1=bone;w.weight1=amount;}else if(slot==2){w.boneIndex2=bone;w.weight2=amount;}else{w.boneIndex3=bone;w.weight3=amount;}}next[i]=w;
                    void Add(BoneWeight value,float scale){Accumulate(value.boneIndex0,value.weight0*scale);Accumulate(value.boneIndex1,value.weight1*scale);Accumulate(value.boneIndex2,value.weight2*scale);Accumulate(value.boneIndex3,value.weight3*scale);}
                    void Accumulate(int bone,float amount){if(amount<=0)return;sums.TryGetValue(bone,out float prior);sums[bone]=prior+amount;}
                }
                weights=next;
            }
            mesh.boneWeights=weights;
        }
    }
}
