using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio
{
    // Non-saving candidate. Original surfaces and coincident boundary weights
    // remain fixed; interior closure vertices borrow local same-part support.
    public static class RebirthEntityLocalRepairSkin
    {
        public static void ApplyFingerChains(SkinnedMeshRenderer renderer,EntitySurfaceRepairInput input)
        {
            var mesh=renderer.sharedMesh;var original=mesh.boneWeights;var points=mesh.vertices;var boundary=new HashSet<(Vector3Int,int)>();
            for(int i=0;i<points.Length;i++)if(input.repairVertexIds[i]==0)boundary.Add((Vector3Int.RoundToInt(renderer.transform.TransformPoint(points[i])*100000f),input.repairVertexParts[i]));
            RebirthEntityHandWeights.RefitEuclidean(renderer,true);var candidate=mesh.boneWeights;int changed=0;
            for(int i=0;i<points.Length;i++)
            {
                bool fixedVertex=input.repairVertexIds[i]==0||input.repairVertexParts[i]==0||boundary.Contains((Vector3Int.RoundToInt(renderer.transform.TransformPoint(points[i])*100000f),input.repairVertexParts[i]));
                if(fixedVertex)candidate[i]=original[i];else if(!candidate[i].Equals(original[i]))changed++;
            }
            mesh.boneWeights=candidate;Debug.Log("FINGER CHAIN CLOSURE candidate changed="+changed+" original and coincident seam weights fixed; no geometry change.");
        }
        public static void SmoothGeometry(SkinnedMeshRenderer renderer,EntitySurfaceRepairInput input)
        {
            var mesh=renderer.sharedMesh;var original=mesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
            var ids=new int[original.Length];var groups=new List<List<int>>();var nodes=new List<Vector3>();var lookup=new Dictionary<(Vector3Int,int),int>();
            for(int i=0;i<ids.Length;i++)
            {var key=(Vector3Int.RoundToInt(original[i]*100000f),input.repairVertexParts[i]);if(!lookup.TryGetValue(key,out int id)){id=nodes.Count;lookup.Add(key,id);nodes.Add(original[i]);groups.Add(new List<int>());}ids[i]=id;groups[id].Add(i);}
            var selected=new bool[nodes.Count];var adjacent=nodes.Select(p=>new HashSet<int>()).ToArray();
            for(int i=0;i<ids.Length;i++)if(input.repairVertexParts[i]>0&&input.repairVertexIds[i]>0)selected[ids[i]]=true;
            var triangles=mesh.triangles;
            for(int t=0;t<triangles.Length;t+=3)for(int e=0;e<3;e++)
            {int a=ids[triangles[t+e]],b=ids[triangles[t+(e+1)%3]];if(a==b||(nodes[a]-nodes[b]).sqrMagnitude>.0009f)continue;adjacent[a].Add(b);adjacent[b].Add(a);}
            var source=nodes.ToArray();var current=nodes.ToArray();
            for(int pass=0;pass<8;pass++)
            {
                var next=(Vector3[])current.Clone();
                for(int i=0;i<current.Length;i++)
                {if(!selected[i]||adjacent[i].Count<3)continue;Vector3 average=Vector3.zero;foreach(int neighbor in adjacent[i])average+=current[neighbor];average/=adjacent[i].Count;Vector3 target=Vector3.Lerp(current[i],average,.35f);next[i]=source[i]+Vector3.ClampMagnitude(target-source[i],.01f);}
                current=next;
            }
            var result=new Vector3[original.Length];float maximum=0;int moved=0;
            for(int i=0;i<result.Length;i++){Vector3 p=current[ids[i]];float distance=Vector3.Distance(p,original[i]);maximum=Mathf.Max(maximum,distance);if(distance>1e-6f)moved++;result[i]=renderer.transform.InverseTransformPoint(p);}
            mesh.vertices=result;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            Debug.Log("REPAIR GEOMETRY candidate moved="+moved+" maximumWorldDisplacement="+maximum+" welded anatomical seams retained; not saved.");
        }
        public static void Apply(SkinnedMeshRenderer renderer,EntitySurfaceRepairInput input)
        {
            var mesh=renderer.sharedMesh;var points=mesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
            var original=mesh.boneWeights;var result=(BoneWeight[])original.Clone();int changed=0;
            for(int i=0;i<points.Length;i++)
            {
                if(input.repairVertexIds[i]==0||input.repairVertexParts[i]==0)continue;
                int part=input.repairVertexParts[i];
                var nearest=new List<(int id,float d)>();
                for(int j=0;j<points.Length;j++)
                {
                    if(input.repairVertexIds[j]!=0||input.repairVertexParts[j]!=part)continue;
                    float d=(points[i]-points[j]).sqrMagnitude;
                    if(nearest.Count==8&&d>=nearest[7].d)continue;
                    nearest.Add((j,d));nearest.Sort((a,b)=>a.d.CompareTo(b.d));if(nearest.Count>8)nearest.RemoveAt(8);
                }
                if(nearest.Count==0)throw new InvalidOperationException("Missing original same-part skin support.");
                if(nearest[0].d<1e-10f)continue;
                var sums=new Dictionary<int,float>();
                foreach(var entry in nearest)
                {
                    var w=original[entry.id];float factor=1f/Mathf.Max(entry.d,1e-8f);
                    Add(w.boneIndex0,w.weight0*factor);Add(w.boneIndex1,w.weight1*factor);Add(w.boneIndex2,w.weight2*factor);Add(w.boneIndex3,w.weight3*factor);
                }
                var order=sums.OrderByDescending(pair=>pair.Value).ThenBy(pair=>pair.Key).Take(4).ToArray();float total=order.Sum(pair=>pair.Value);
                var value=new BoneWeight();for(int slot=0;slot<order.Length;slot++)
                {int bone=order[slot].Key;float amount=order[slot].Value/total;if(slot==0){value.boneIndex0=bone;value.weight0=amount;}else if(slot==1){value.boneIndex1=bone;value.weight1=amount;}else if(slot==2){value.boneIndex2=bone;value.weight2=amount;}else{value.boneIndex3=bone;value.weight3=amount;}}
                result[i]=value;changed++;
                void Add(int bone,float amount){if(amount<=0)return;sums.TryGetValue(bone,out float prior);sums[bone]=prior+amount;}
            }
            mesh.boneWeights=result;Debug.Log("LOCAL REPAIR SKIN candidate changed="+changed+" original surfaces and boundaries retained.");
        }
    }
}
