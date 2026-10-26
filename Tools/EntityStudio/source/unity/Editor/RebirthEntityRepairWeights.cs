using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityRepairWeights
    {
        public static void Interpolate(EntitySurfaceRepairInput input,BoneWeight[] weights)
        {
            if(input.repairWeightAnchors==null||input.repairWeightAnchors.Length!=weights.Length)throw new ArgumentException("Repair weight correspondence is missing.");
            int changed=0;
            for(int vertex=0;vertex<weights.Length;vertex++)
            {
                if(input.repairVertexIds[vertex]==0)continue;
                Vector3Int anchors=input.repairWeightAnchors[vertex];Vector3 bary=input.repairWeightBarycentrics[vertex];
                if(anchors.x<0||anchors.y<0||anchors.z<0||anchors.x>=weights.Length||anchors.y>=weights.Length||anchors.z>=weights.Length||input.repairVertexIds[anchors.x]!=0||input.repairVertexIds[anchors.y]!=0||input.repairVertexIds[anchors.z]!=0)throw new InvalidOperationException("Repair weights must refer only to original boundary vertices.");
                if(Mathf.Abs(bary.x+bary.y+bary.z-1)>1e-4f||Mathf.Min(bary.x,bary.y,bary.z)<0)throw new InvalidOperationException("Invalid repair barycentric weights.");
                var sums=new Dictionary<int,float>();Add(weights[anchors.x],bary.x);Add(weights[anchors.y],bary.y);Add(weights[anchors.z],bary.z);
                var ordered=new List<KeyValuePair<int,float>>(sums);ordered.Sort((a,b)=>{int compare=b.Value.CompareTo(a.Value);return compare!=0?compare:a.Key.CompareTo(b.Key);});
                float total=0;for(int i=0;i<Math.Min(4,ordered.Count);i++)total+=ordered[i].Value;
                if(total<=0)throw new InvalidOperationException("Repair boundary has no bone influences.");
                var result=new BoneWeight();for(int i=0;i<Math.Min(4,ordered.Count);i++)
                {int bone=ordered[i].Key;float amount=ordered[i].Value/total;if(i==0){result.boneIndex0=bone;result.weight0=amount;}else if(i==1){result.boneIndex1=bone;result.weight1=amount;}else if(i==2){result.boneIndex2=bone;result.weight2=amount;}else{result.boneIndex3=bone;result.weight3=amount;}}
                weights[vertex]=result;changed++;
                void Add(BoneWeight w,float scale){Accumulate(w.boneIndex0,w.weight0*scale);Accumulate(w.boneIndex1,w.weight1*scale);Accumulate(w.boneIndex2,w.weight2*scale);Accumulate(w.boneIndex3,w.weight3*scale);}
                void Accumulate(int bone,float amount){if(amount<=0)return;sums.TryGetValue(bone,out float prior);sums[bone]=prior+amount;}
            }
            Debug.Log("REPAIR WEIGHTS: interpolated "+changed+" cap vertices from original boundary weights.");
        }
    }
}
