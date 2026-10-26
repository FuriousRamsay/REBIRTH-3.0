using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityArmSurfaceAudit
    {
        public static void RunSavedFarmer()
        {
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerArmSurface");Directory.CreateDirectory(folder);
            var sections=new List<string>{"renderer,bone,bin,count,repairCount,minY,maxY,minZ,maxZ,boneX,boneY,boneZ,endX,endY,endZ"};
            var components=new List<string>{"renderer,part,component,triangles,repairTriangles,area,minX,minY,minZ,maxX,maxY,maxZ"};
            var seams=new List<string>{"renderer,part,vertex,pairedVertex,cap,pairedCap,x,y,z,weightL1"};
            var boundaries=new List<string>{"renderer,part,a,b,incidence,capA,capB,ax,ay,az,bx,by,bz,length"};
            foreach(var renderer in workspace.sourceSkinnedMeshes)
            {
                var input=workspace.surfaceRepairInputs.Single(value=>value.repairedMesh==renderer.sharedMesh);
                Vector3[] points=renderer.sharedMesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
                if(input.repairVertexParts.Length!=points.Length)throw new InvalidOperationException("Repair correspondence mismatch.");
                var seamGroups=new Dictionary<(Vector3Int,int),List<int>>();
                var skin=renderer.sharedMesh.boneWeights;
                for(int i=0;i<points.Length;i++)
                {
                    var key=(Vector3Int.RoundToInt(points[i]*100000f),input.repairVertexParts[i]);
                    if(!seamGroups.TryGetValue(key,out var list)){list=new List<int>();seamGroups.Add(key,list);}list.Add(i);
                }
                foreach(var list in seamGroups.Values)for(int a=0;a<list.Count;a++)for(int b=a+1;b<list.Count;b++)
                {
                    int i=list[a],j=list[b];if(input.repairVertexIds[i]==input.repairVertexIds[j])continue;
                    var delta=new Dictionary<int,float>();Accumulate(skin[i],1);Accumulate(skin[j],-1);
                    float distance=delta.Values.Sum(value=>Mathf.Abs(value));if(distance<.0001f)continue;
                    Vector3 p=points[i];seams.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6:R},{7:R},{8:R},{9:R}",renderer.name,input.repairVertexParts[i],i,j,input.repairVertexIds[i],input.repairVertexIds[j],p.x,p.y,p.z,distance));
                    void Accumulate(BoneWeight w,float sign)
                    {Add(w.boneIndex0,w.weight0*sign);Add(w.boneIndex1,w.weight1*sign);Add(w.boneIndex2,w.weight2*sign);Add(w.boneIndex3,w.weight3*sign);}
                    void Add(int bone,float value){delta.TryGetValue(bone,out float prior);delta[bone]=prior+value;}
                }
                foreach(string side in new[]{"Left","Right"})foreach(string suffix in new[]{"Arm","ForeArm","Hand"})
                {
                    string name=side+suffix,endName=side+(suffix=="Arm"?"ForeArm":suffix=="ForeArm"?"Hand":"HandMiddle4");
                    var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,name);var end=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,endName);
                    if(bone==null||end==null)throw new InvalidOperationException("Missing arm sample chain.");
                    Vector3 axis=end.position-bone.position;float length=axis.magnitude;axis/=length;
                    for(int bin=0;bin<10;bin++)
                    {
                        var ids=new List<int>();for(int i=0;i<points.Length;i++)
                        {if(input.repairVertexParts[i]!=(side=="Left"?1:2))continue;float t=Vector3.Dot(points[i]-bone.position,axis)/length;if(t>=bin*.1f&&t<(bin+1)*.1f)ids.Add(i);}
                        if(ids.Count==0)continue;
                        sections.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5:R},{6:R},{7:R},{8:R},{9:R},{10:R},{11:R},{12:R},{13:R},{14:R}",renderer.name,name,bin,ids.Count,ids.Count(i=>input.repairVertexIds[i]>0),ids.Min(i=>points[i].y),ids.Max(i=>points[i].y),ids.Min(i=>points[i].z),ids.Max(i=>points[i].z),bone.position.x,bone.position.y,bone.position.z,end.position.x,end.position.y,end.position.z));
                    }
                }
                int[] parent=Enumerable.Range(0,points.Length).ToArray();var weld=new Dictionary<(Vector3Int,int),int>();
                for(int i=0;i<points.Length;i++){var key=(Vector3Int.RoundToInt(points[i]*100000f),input.repairVertexParts[i]);if(weld.TryGetValue(key,out int prior))Join(i,prior);else weld.Add(key,i);}
                int[] triangles=renderer.sharedMesh.triangles;
                var geometricIds=new int[points.Length];var geometricLookup=new Dictionary<(Vector3Int,int),int>();
                for(int i=0;i<points.Length;i++){var key=(Vector3Int.RoundToInt(points[i]*100000f),input.repairVertexParts[i]);if(!geometricLookup.TryGetValue(key,out int id)){id=i;geometricLookup.Add(key,id);}geometricIds[i]=id;}
                var incidence=new Dictionary<(int,int),int>();
                for(int t=0;t<triangles.Length;t+=3)for(int edge=0;edge<3;edge++)
                {int a=geometricIds[triangles[t+edge]],b=geometricIds[triangles[t+(edge+1)%3]];if(a==b)continue;var key=(Math.Min(a,b),Math.Max(a,b));incidence.TryGetValue(key,out int count);incidence[key]=count+1;}
                foreach(var pair in incidence)
                {
                    if(pair.Value==2)continue;int a=pair.Key.Item1,b=pair.Key.Item2;Vector3 p=points[a],q=points[b];
                    boundaries.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6},{7:R},{8:R},{9:R},{10:R},{11:R},{12:R},{13:R}",renderer.name,input.repairVertexParts[a],a,b,pair.Value,input.repairVertexIds[a],input.repairVertexIds[b],p.x,p.y,p.z,q.x,q.y,q.z,Vector3.Distance(p,q)));
                }
                for(int t=0;t<triangles.Length;t+=3){Join(triangles[t],triangles[t+1]);Join(triangles[t],triangles[t+2]);}
                var groups=new Dictionary<int,List<int>>();
                for(int t=0;t<triangles.Length;t+=3){int root=Root(triangles[t]);if(!groups.TryGetValue(root,out var list)){list=new List<int>();groups.Add(root,list);}list.Add(t);}
                foreach(var pair in groups)
                {
                    float area=0;int repairs=0;Bounds bounds=new Bounds(points[triangles[pair.Value[0]]],Vector3.zero);
                    foreach(int t in pair.Value){Vector3 a=points[triangles[t]],b=points[triangles[t+1]],c=points[triangles[t+2]];area+=Vector3.Cross(b-a,c-a).magnitude*.5f;bounds.Encapsulate(a);bounds.Encapsulate(b);bounds.Encapsulate(c);if(input.repairVertexIds[triangles[t]]>0)repairs++;}
                    Vector3 lo=bounds.min,hi=bounds.max;
                    components.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5:R},{6:R},{7:R},{8:R},{9:R},{10:R},{11:R}",renderer.name,input.repairVertexParts[triangles[pair.Value[0]]],pair.Key,pair.Value.Count,repairs,area,lo.x,lo.y,lo.z,hi.x,hi.y,hi.z));
                }
                int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
                void Join(int a,int b){a=Root(a);b=Root(b);if(a!=b)parent[b]=a;}
            }
            File.WriteAllLines(Path.Combine(folder,"Sections.csv"),sections);File.WriteAllLines(Path.Combine(folder,"Components.csv"),components);
            File.WriteAllLines(Path.Combine(folder,"SeamWeights.csv"),seams);
            File.WriteAllLines(Path.Combine(folder,"BoundaryEdges.csv"),boundaries);
            Debug.Log("ARM SURFACE AUDIT COMPLETE: sections="+(sections.Count-1)+" components="+(components.Count-1)+" no assets saved.");
        }
    }
}
