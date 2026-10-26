using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthFarmerSkinInspection
    {
        public static void Run()
        {
            try
            {
                var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
                var ws=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(value=>value!=null);
                string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthFarmerReview");
                Directory.CreateDirectory(folder);
                var rows=new List<string>{"vertex,x,y,z,bone0,weight0,bone1,weight1,bone2,weight2,bone3,weight3,nx,ny,nz"};
                var edges=new List<string>{"a,b,regionA,regionB,restLength"};
                foreach(var renderer in ws.sourceSkinnedMeshes)
                {
                    var mesh=renderer.sharedMesh;var points=mesh.vertices;var weights=mesh.boneWeights;
                    TraceLowerBody(renderer,folder);
                    for(int i=0;i<points.Length;i++)
                    {
                        var p=renderer.transform.TransformPoint(points[i]);var w=weights[i];
                        var normal=renderer.transform.TransformDirection(mesh.normals[i]);
                        rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4},{5:R},{6},{7:R},{8},{9:R},{10},{11:R},{12:R},{13:R},{14:R}",i,p.x,p.y,p.z,renderer.bones[w.boneIndex0].name,w.weight0,renderer.bones[w.boneIndex1].name,w.weight1,renderer.bones[w.boneIndex2].name,w.weight2,renderer.bones[w.boneIndex3].name,w.weight3,normal.x,normal.y,normal.z));
                    }
                    var seen=new HashSet<ulong>();var triangles=mesh.triangles;
                    for(int t=0;t<triangles.Length;t+=3)for(int e=0;e<3;e++)
                    {
                        int a=triangles[t+e],b=triangles[t+(e+1)%3];ulong key=((ulong)(uint)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);if(!seen.Add(key))continue;
                        int ra=Region(renderer.bones[weights[a].boneIndex0].name),rb=Region(renderer.bones[weights[b].boneIndex0].name);
                        if(ra==rb||ra==0||rb==0)continue;
                        float length=Vector3.Distance(renderer.transform.TransformPoint(points[a]),renderer.transform.TransformPoint(points[b]));
                        edges.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4:R}",a,b,ra,rb,length));
                    }
                }
                File.WriteAllLines(Path.Combine(folder,"OriginalMesh_SkinVertices.csv"),rows);
                File.WriteAllLines(Path.Combine(folder,"OriginalMesh_CrossRegionEdges.csv"),edges);
                Aim("LeftArm","LeftForeArm",Vector3.left);Aim("LeftForeArm","LeftHand",Vector3.left);
                Aim("RightArm","RightForeArm",Vector3.right);Aim("RightForeArm","RightHand",Vector3.right);
                RebirthFarmerReviewCapture.CaptureColliderCandidate(ws,Path.Combine(folder,"OriginalMesh_TPose"),true,true);
                Debug.Log("SOURCE SKIN INSPECTION: vertices="+(rows.Count-1)+" crossRegionEdges="+(edges.Count-1)+"; temporary skeletal pose only, scene not saved.");
                void Aim(string start,string end,Vector3 direction)
                {
                    var a=RebirthEntityRigBuilder.FindBone(ws.generatedSkeleton,start);var b=RebirthEntityRigBuilder.FindBone(ws.generatedSkeleton,end);
                    a.rotation=Quaternion.FromToRotation(b.position-a.position,direction)*a.rotation;
                }
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        private static int Region(string name)
        {
            if(name.Contains("Arm")||name.Contains("Hand"))return name.StartsWith("Left",StringComparison.Ordinal)?1:2;
            if(name.Contains("Leg")||name.Contains("Foot")||name.Contains("Toe"))return name.StartsWith("Left",StringComparison.Ordinal)?3:4;
            return 0;
        }

        private static void TraceLowerBody(SkinnedMeshRenderer renderer,string folder)
        {
            var mesh=renderer.sharedMesh;var p=mesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
            var neighbors=new List<int>[p.Length];for(int i=0;i<p.Length;i++)neighbors[i]=new List<int>();
            var positions=new Dictionary<Vector3Int,int>();
            for(int i=0;i<p.Length;i++)
            {
                if(p[i].y>=1.1f)continue;
                var key=Vector3Int.RoundToInt(p[i]*100000f);
                if(positions.TryGetValue(key,out int other)){neighbors[i].Add(other);neighbors[other].Add(i);}else positions.Add(key,i);
            }
            var triangles=mesh.triangles;
            var parent=Enumerable.Range(0,p.Length).ToArray();
            int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            for(int t=0;t<triangles.Length;t+=3)for(int e=0;e<3;e++)
            {int a=triangles[t+e],b=triangles[t+(e+1)%3];if(p[a].y<1.1f&&p[b].y<1.1f)parent[Root(a)]=Root(b);}
            var componentRows=new List<string>{"component,count,minX,maxX,minY,maxY,minZ,maxZ,armVertices,legVertices"};
            var weights=mesh.boneWeights;
            foreach(var component in Enumerable.Range(0,p.Length).Where(i=>p[i].y<1.1f).GroupBy(Root))
            {
                var bounds=new Bounds(p[component.First()],Vector3.zero);int arms=0,legs=0;
                foreach(int i in component){bounds.Encapsulate(p[i]);int region=Region(renderer.bones[weights[i].boneIndex0].name);if(region==1||region==2)arms++;if(region==3||region==4)legs++;}
                componentRows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8},{9}",component.Key,component.Count(),bounds.min.x,bounds.max.x,bounds.min.y,bounds.max.y,bounds.min.z,bounds.max.z,arms,legs));
            }
            File.WriteAllLines(Path.Combine(folder,"OriginalMesh_UnweldedComponents.csv"),componentRows);
            for(int t=0;t<triangles.Length;t+=3)for(int e=0;e<3;e++)
            {int a=triangles[t+e],b=triangles[t+(e+1)%3];if(p[a].y<1.1f&&p[b].y<1.1f){neighbors[a].Add(b);neighbors[b].Add(a);}}
            var rows=new List<string>{"side,step,vertex,x,y,z"};
            foreach(int side in new[]{-1,1})
            {
                int Nearest(Vector3 target)=>Enumerable.Range(0,p.Length).OrderBy(i=>(p[i]-target).sqrMagnitude).First();
                int hand=Nearest(new Vector3(side*.28f,.85f,0)),thigh=Nearest(new Vector3(side*.13f,.85f,.12f));
                var previous=Enumerable.Repeat(-1,p.Length).ToArray();var queue=new Queue<int>();previous[hand]=hand;queue.Enqueue(hand);
                while(queue.Count>0&&previous[thigh]<0){int current=queue.Dequeue();foreach(int next in neighbors[current])if(previous[next]<0){previous[next]=current;queue.Enqueue(next);}}
                var path=new List<int>();if(previous[thigh]>=0){int current=thigh;while(current!=hand){path.Add(current);current=previous[current];}path.Add(hand);path.Reverse();}
                for(int i=0;i<path.Count;i++){int v=path[i];rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3:R},{4:R},{5:R}",side,i,v,p[v].x,p[v].y,p[v].z));}
                Debug.Log("ORIGINAL LOWER BODY CONNECTIVITY side="+side+" handSeed="+hand+" at="+p[hand].ToString("F4")+" thighSeed="+thigh+" at="+p[thigh].ToString("F4")+" connectedBelow1.1m="+(path.Count>0)+" pathVertices="+path.Count);
            }
            File.WriteAllLines(Path.Combine(folder,"OriginalMesh_HandThighPaths.csv"),rows);
        }
    }
}
