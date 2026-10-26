using System;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityKneeWeights
    {
        public static void RenderSavedComparison()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            RebirthEntityRigWorkspace workspace=null;
            foreach(var root in scene.GetRootGameObjects())if(root.TryGetComponent(out RebirthEntityRigWorkspace found)){workspace=found;break;}
            if(workspace==null)throw new InvalidOperationException("Farmer workspace missing.");
            string folder=System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerKneeComparison");System.IO.Directory.CreateDirectory(folder);
            var originals=new System.Collections.Generic.Dictionary<SkinnedMeshRenderer,Mesh>();
            var rotations=new System.Collections.Generic.Dictionary<Transform,Quaternion>();
            foreach(var renderer in workspace.sourceSkinnedMeshes)originals.Add(renderer,renderer.sharedMesh);
            try
            {
                foreach(string side in new[]{"Left","Right"})
                {var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,side+"Leg");if(bone==null)throw new InvalidOperationException("Missing calf.");rotations.Add(bone,bone.localRotation);bone.localRotation*=Quaternion.Euler(75,0,0);}
                RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,System.IO.Path.Combine(folder,"Baseline"),false);
                foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                foreach(var renderer in workspace.sourceSkinnedMeshes){renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);Apply(renderer,.5f,true);}
                foreach(var pair in rotations)pair.Key.localRotation=pair.Value*Quaternion.Euler(75,0,0);
                RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,System.IO.Path.Combine(folder,"Candidate"),false);
            }
            finally
            {
                foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                foreach(var pair in originals)if(pair.Key.sharedMesh!=pair.Value){UnityEngine.Object.DestroyImmediate(pair.Key.sharedMesh);pair.Key.sharedMesh=pair.Value;}
            }
        }

        // Preserve the total thigh/calf influence and every other influence.
        // A continuous longitudinal blend avoids binary influence-box seams.
        public static void Apply(SkinnedMeshRenderer renderer,float bandFraction=.22f,bool replaceChainField=false)
        {
            if(float.IsNaN(bandFraction)||bandFraction<=0||bandFraction>.5f)throw new ArgumentOutOfRangeException(nameof(bandFraction));
            Mesh mesh=renderer.sharedMesh;Vector3[] vertices=mesh.vertices;BoneWeight[] weights=mesh.boneWeights;
            foreach(string side in new[]{"Left","Right"})
            {
                int thigh=Array.FindIndex(renderer.bones,b=>b.name==side+"UpLeg"),calf=Array.FindIndex(renderer.bones,b=>b.name==side+"Leg"),foot=Array.FindIndex(renderer.bones,b=>b.name==side+"Foot");
                if(thigh<0||calf<0||foot<0)throw new InvalidOperationException("Missing knee chain "+side);
                Vector3 knee=renderer.bones[calf].position;
                Vector3 axis=(renderer.bones[foot].position-renderer.bones[thigh].position).normalized;
                float band=Mathf.Min(Vector3.Distance(knee,renderer.bones[thigh].position),Vector3.Distance(knee,renderer.bones[foot].position))*bandFraction;
                if(band<.001f)throw new InvalidOperationException("Degenerate knee chain.");
                int changed=0;
                for(int i=0;i<vertices.Length;i++)
                {
                    BoneWeight w=weights[i];float total=Get(w,thigh)+Get(w,calf);
                    if(total<.8f)continue;
                    float along=Vector3.Dot(renderer.transform.TransformPoint(vertices[i])-knee,axis);
                    if(!replaceChainField&&Mathf.Abs(along)>=band)continue;
                    float t=Mathf.SmoothStep(0,1,(along+band)/(2*band));
                    // Fade correction to zero at both ends of the local band.
                    float blend=replaceChainField?1:1-Mathf.SmoothStep(.65f,1,Mathf.Abs(along)/band);
                    float lower=Mathf.Lerp(Get(w,calf),total*t,blend),upper=total-lower;
                    var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                    int upperSlot=Array.IndexOf(ids,thigh),lowerSlot=Array.IndexOf(ids,calf);
                    if(upperSlot<0)upperSlot=Free();if(upperSlot<0)continue;ids[upperSlot]=thigh;values[upperSlot]=upper;
                    if(lowerSlot<0)lowerSlot=Free();if(lowerSlot<0)continue;ids[lowerSlot]=calf;values[lowerSlot]=lower;
                    weights[i]=new BoneWeight{boneIndex0=ids[0],weight0=values[0],boneIndex1=ids[1],weight1=values[1],boneIndex2=ids[2],weight2=values[2],boneIndex3=ids[3],weight3=values[3]};changed++;
                    int Free(){for(int slot=0;slot<4;slot++)if(values[slot]<=.000001f&&slot!=upperSlot&&slot!=lowerSlot)return slot;return -1;}
                }
                Debug.Log("KNEE CONTINUOUS BLEND "+side+" changed="+changed+" band="+band+" knee="+knee.ToString("F6")+" replaceChainField="+replaceChainField);
            }
            foreach(var w in weights)
            {
                float sum=w.weight0+w.weight1+w.weight2+w.weight3;
                if(float.IsNaN(sum)||float.IsInfinity(sum)||Mathf.Abs(sum-1)>.0001f||Mathf.Min(w.weight0,w.weight1,w.weight2,w.weight3)<0)
                    throw new InvalidOperationException("Knee correction produced invalid skin weights.");
            }
            mesh.boneWeights=weights;
        }
        static float Get(BoneWeight w,int index)=>(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+(w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
    }
}
