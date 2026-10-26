using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    // Changes skin weights only. Source topology and UV seams remain intact.
    public static class RebirthFarmerIslandSkin
    {
        public static void Run()
        {
            try
            {
                var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
                var ws=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(value=>value!=null);
                foreach(var renderer in ws.sourceSkinnedMeshes)
                {
                    renderer.sharedMesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);
                    string before=RebirthFarmerSourcePreservation.Hash(renderer.sharedMesh);
                    Apply(renderer,ws.profile.estimatedHeight);
                    if(before!=RebirthFarmerSourcePreservation.Hash(renderer.sharedMesh))throw new InvalidDataException("Weight correction altered source geometry.");
                }
                string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthFarmerReview");
                Aim("LeftArm","LeftForeArm",Vector3.left);Aim("LeftForeArm","LeftHand",Vector3.left);
                Aim("RightArm","RightForeArm",Vector3.right);Aim("RightForeArm","RightHand",Vector3.right);
                RebirthFarmerReviewCapture.CaptureColliderCandidate(ws,Path.Combine(folder,"OriginalMesh_IslandSkinTPose"),false,true);
                Debug.Log("ISLAND SKIN TRIAL: geometry unchanged; no scene or mesh asset saved.");
                void Aim(string start,string end,Vector3 direction)
                {var a=RebirthEntityRigBuilder.FindBone(ws.generatedSkeleton,start);var b=RebirthEntityRigBuilder.FindBone(ws.generatedSkeleton,end);a.rotation=Quaternion.FromToRotation(b.position-a.position,direction)*a.rotation;}
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }

        public static void Apply(SkinnedMeshRenderer renderer,float height)
        {
            var mesh=renderer.sharedMesh;var vertices=mesh.vertices;var points=vertices.Select(renderer.transform.TransformPoint).ToArray();
            float cutoff=height*.6111111f;
            var parent=Enumerable.Range(0,points.Length).ToArray();int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            var triangles=mesh.triangles;
            for(int t=0;t<triangles.Length;t+=3)for(int e=0;e<3;e++)
            {int a=triangles[t+e],b=triangles[t+(e+1)%3];if(points[a].y<cutoff&&points[b].y<cutoff)parent[Root(a)]=Root(b);}
            var bones=renderer.bones;var weights=mesh.boneWeights;
            var starts=bones.Select(b=>b.position).ToArray();var ends=new Vector3[bones.Length];var parents=new int[bones.Length];
            bool IsArm(int i)=>bones[i].name.Contains("Arm")||bones[i].name.Contains("Hand")||bones[i].name.Contains("Shoulder");
            for(int i=0;i<bones.Length;i++)
            {
                parents[i]=Array.IndexOf(bones,bones[i].parent);ends[i]=starts[i];
                foreach(var child in bones)if(child.parent==bones[i]){ends[i]=child.position;break;}
            }
            int changed=0;
            foreach(var component in Enumerable.Range(0,points.Length).Where(i=>points[i].y<cutoff).GroupBy(Root))
            {
                float minX=component.Min(i=>points[i].x),maxX=component.Max(i=>points[i].x);
                bool arm=minX< -height*.1333333f||maxX>height*.1333333f;
                bool left=component.Average(i=>points[i].x)<0;
                var eligible=new bool[bones.Length];
                for(int b=0;b<bones.Length;b++)eligible[b]=arm?IsArm(b)&&bones[b].name.StartsWith(left?"Left":"Right",StringComparison.Ordinal):!IsArm(b);
                foreach(int i in component)
                {
                    var w=weights[i];float sum=0;var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var amounts=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                    for(int j=0;j<4;j++){if(!eligible[ids[j]])amounts[j]=0;sum+=amounts[j];}
                    if(sum>.0001f)weights[i]=new BoneWeight{boneIndex0=ids[0],weight0=amounts[0]/sum,boneIndex1=ids[1],weight1=amounts[1]/sum,boneIndex2=ids[2],weight2=amounts[2]/sum,boneIndex3=ids[3],weight3=amounts[3]/sum};
                    else weights[i]=RebirthEntityRigBuilder.CalculateConnectedBoneWeight(points[i],starts,ends,parents,4f,null,eligible);
                    if(!weights[i].Equals(w))changed++;
                }
            }
            mesh.boneWeights=weights;Debug.Log("SOURCE ISLAND OWNERSHIP: changedWeights="+changed+" vertices="+vertices.Length+"; positions/triangles/UVs untouched.");
        }
    }
}
