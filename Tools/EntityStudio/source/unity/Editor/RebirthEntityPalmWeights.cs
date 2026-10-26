using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityPalmWeights
    {
        public static void Apply(SkinnedMeshRenderer renderer,int firstVertex)
        {
            var mesh=renderer.sharedMesh;var weights=mesh.boneWeights;var points=mesh.vertices;int changed=0;
            var bones=renderer.bones.Select((bone,index)=>(bone,index)).ToDictionary(pair=>pair.bone.name,pair=>pair.index);
            for(int i=firstVertex;i<points.Length;i++)
            {
                var old=weights[i];var sums=new Dictionary<int,float>();var point=renderer.transform.TransformPoint(points[i]);bool moved=false;
                Add(old.boneIndex0,old.weight0);Add(old.boneIndex1,old.weight1);Add(old.boneIndex2,old.weight2);Add(old.boneIndex3,old.weight3);
                if(!moved)continue;
                var order=sums.Where(pair=>pair.Value>0).OrderByDescending(pair=>pair.Value).ThenBy(pair=>pair.Key).Take(4).ToArray();float total=order.Sum(pair=>pair.Value);var result=new BoneWeight();
                for(int slot=0;slot<order.Length;slot++){int bone=order[slot].Key;float amount=order[slot].Value/total;if(slot==0){result.boneIndex0=bone;result.weight0=amount;}else if(slot==1){result.boneIndex1=bone;result.weight1=amount;}else if(slot==2){result.boneIndex2=bone;result.weight2=amount;}else{result.boneIndex3=bone;result.weight3=amount;}}
                weights[i]=result;changed++;
                void Add(int bone,float amount)
                {
                    if(amount<=0)return;string name=renderer.bones[bone].name;string side=name.StartsWith("LeftHand",StringComparison.Ordinal)?"Left":name.StartsWith("RightHand",StringComparison.Ordinal)?"Right":null;
                    if(side!=null&&name!=side+"Hand")
                    {
                        string chain=name.Substring(0,name.Length-1);if(bones.TryGetValue(chain+"1",out int root)&&bones.TryGetValue(chain+"2",out int next))
                        {
                            var axis=(renderer.bones[next].position-renderer.bones[root].position).normalized;float along=Vector3.Dot(point-renderer.bones[root].position,axis);float factor=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.01f,.01f,along));
                            if(factor<.99999f){Acc(bones[side+"Hand"],amount*(1-factor));amount*=factor;moved=true;}
                        }
                    }
                    Acc(bone,amount);
                }
                void Acc(int bone,float amount){sums.TryGetValue(bone,out float prior);sums[bone]=prior+amount;}
            }
            mesh.boneWeights=weights;Debug.Log("PALM WEIGHT CANDIDATE changed="+changed+" original body weights untouched; no assets saved.");
        }
    }
}
