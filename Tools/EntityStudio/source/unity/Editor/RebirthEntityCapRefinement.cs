using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityCapRefinement
    {
        public static void RenderSavedFarmerComparison()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            bool rounded=Array.IndexOf(Environment.GetCommandLineArgs(),"-roundArmCaps")>=0;
            string folder=System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/"+(rounded?"FarmerRoundedArmCaps":"FarmerCapRefinement"));System.IO.Directory.CreateDirectory(folder);
            var originals=workspace.sourceSkinnedMeshes.ToDictionary(r=>r,r=>r.sharedMesh);
            var refined=new Dictionary<SkinnedMeshRenderer,Mesh>();
            var rotations=workspace.generatedSkeleton.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.localRotation);
            try
            {
                foreach(var renderer in workspace.sourceSkinnedMeshes)
                    refined.Add(renderer,Refine(renderer,workspace.surfaceRepairInputs.Single(input=>input.repairedMesh==renderer.sharedMesh),capBulge:rounded?.015f:0));
                foreach(string pose in new[]{"rest","elbows","reach"})
                {
                    foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                    if(pose!="rest")foreach(string side in new[]{"Left","Right"})Rotate(side+"ForeArm",new Vector3(70,0,0));
                    if(pose=="reach"){Rotate("LeftArm",new Vector3(25,0,-35));Rotate("RightArm",new Vector3(25,0,35));}
                    foreach(bool candidate in new[]{false,true})
                    {
                        foreach(var pair in originals)pair.Key.sharedMesh=candidate?refined[pair.Key]:pair.Value;
                        string prefix=System.IO.Path.Combine(folder,pose+(candidate?"_Candidate":"_Baseline"));
                        RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,prefix,false,rounded);
                        RebirthEntityDeformationAudit.WritePose(workspace,prefix+".json","");
                    }
                }
            }
            finally
            {
                foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                foreach(var pair in originals)pair.Key.sharedMesh=pair.Value;
                foreach(var mesh in refined.Values)UnityEngine.Object.DestroyImmediate(mesh);
            }
            void Rotate(string name,Vector3 angles)
            {var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,name);if(bone==null)throw new InvalidOperationException("Missing pose bone "+name);bone.localRotation*=Quaternion.Euler(angles);}
        }

        // Conforming refinement: a cap edge also subdivides any original face
        // sharing its geometric edge. UV/normal seams retain separate vertices.
        public static Mesh Refine(SkinnedMeshRenderer renderer,EntitySurfaceRepairInput input,float maximumEdge=.025f,float capBulge=0)
        {
            Mesh source=renderer.sharedMesh;
            if(source!=input.repairedMesh||input.repairVertexIds.Length!=source.vertexCount)throw new InvalidOperationException("Verified repair correspondence required.");
            if(source.subMeshCount!=1||float.IsNaN(maximumEdge)||maximumEdge<=0)throw new ArgumentException("One submesh and a positive edge limit required.");
            if(float.IsNaN(capBulge)||capBulge<0||capBulge>.03f)throw new ArgumentOutOfRangeException(nameof(capBulge));
            var points=new List<Vector3>(source.vertices);var normals=new List<Vector3>(source.normals);
            var weights=new List<BoneWeight>(source.boneWeights);var caps=new List<int>(input.repairVertexIds);var parts=new List<int>(input.repairVertexParts);
            var channels=new List<Vector4>[8];for(int channel=0;channel<8;channel++){channels[channel]=new List<Vector4>();source.GetUVs(channel,channels[channel]);}
            if(normals.Count!=points.Count||weights.Count!=points.Count)throw new InvalidOperationException("Complete normals and weights required.");
            var triangles=new List<int>(source.triangles);int originalCount=points.Count;
            for(int pass=0;pass<10;pass++)
            {
                var split=new HashSet<(Vector3Int,Vector3Int,int)>();
                for(int t=0;t<triangles.Count;t+=3)
                {
                    int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                    if(caps[a]<=0||caps[a]!=caps[b]||caps[a]!=caps[c])continue;
                    Mark(a,b);Mark(b,c);Mark(c,a);
                }
                if(split.Count==0)break;
                var mids=new Dictionary<(int,int),int>();var next=new List<int>();
                for(int t=0;t<triangles.Count;t+=3)
                {
                    int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                    int m=Mid(a,b),n=Mid(b,c),p=Mid(c,a),mask=(m>=0?1:0)|(n>=0?2:0)|(p>=0?4:0);
                    switch(mask)
                    {
                        case 0:Add(a,b,c);break;
                        case 1:Add(a,m,c);Add(m,b,c);break;
                        case 2:Add(b,n,a);Add(n,c,a);break;
                        case 4:Add(c,p,b);Add(p,a,b);break;
                        case 3:Add(b,n,m);Add(a,m,c);Add(m,n,c);break;
                        case 6:Add(c,p,n);Add(b,n,a);Add(n,p,a);break;
                        case 5:Add(a,m,p);Add(c,p,b);Add(p,m,b);break;
                        case 7:Add(a,m,p);Add(m,b,n);Add(p,n,c);Add(m,n,p);break;
                    }
                }
                triangles=next;
                if(points.Count>500000)throw new InvalidOperationException("Cap refinement exceeded authoring vertex budget.");
                if(pass==9)throw new InvalidOperationException("Cap refinement did not converge.");
                void Mark(int a,int b){if(Vector3.Distance(renderer.transform.TransformPoint(points[a]),renderer.transform.TransformPoint(points[b]))>maximumEdge)split.Add(Key(a,b));}
                int Mid(int a,int b)
                {
                    if(!split.Contains(Key(a,b)))return -1;
                    var edge=(Math.Min(a,b),Math.Max(a,b));if(mids.TryGetValue(edge,out int existing))return existing;
                    int id=points.Count;mids.Add(edge,id);points.Add((points[a]+points[b])*.5f);normals.Add((normals[a]+normals[b]).normalized);
                    weights.Add(Blend(weights[a],weights[b]));caps.Add(caps[a]==caps[b]?caps[a]:0);parts.Add(parts[a]);
                    foreach(var channel in channels)if(channel.Count>0)channel.Add((channel[a]+channel[b])*.5f);
                    return id;
                }
                void Add(int a,int b,int c){next.Add(a);next.Add(b);next.Add(c);}
            }
            if(capBulge>0)RoundArmInteriors(renderer,points,normals,caps,parts,triangles,capBulge);
            var result=UnityEngine.Object.Instantiate(source);result.name=source.name+"_RefinedCaps";
            if(points.Count>65535)result.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            result.SetVertices(points);result.SetNormals(normals);result.boneWeights=weights.ToArray();
            for(int channel=0;channel<8;channel++)if(channels[channel].Count>0)result.SetUVs(channel,channels[channel]);
            result.SetTriangles(triangles,0);result.RecalculateTangents();result.RecalculateBounds();
            Debug.Log("CONFORMING CAP REFINEMENT: vertices="+originalCount+"->"+points.Count+" triangles="+source.triangles.Length/3+"->"+triangles.Count/3);
            return result;
            (Vector3Int,Vector3Int,int) Key(int a,int b)
            {
                Vector3Int x=Vector3Int.RoundToInt(renderer.transform.TransformPoint(points[a])*100000f),y=Vector3Int.RoundToInt(renderer.transform.TransformPoint(points[b])*100000f);
                if(x.x>y.x||x.x==y.x&&(x.y>y.y||x.y==y.y&&x.z>y.z)){var swap=x;x=y;y=swap;}
                return(x,y,parts[a]);
            }
        }
        static void RoundArmInteriors(SkinnedMeshRenderer renderer,List<Vector3> points,List<Vector3> normals,List<int> caps,List<int> parts,List<int> triangles,float maximum)
        {
            var lookup=new Dictionary<(Vector3Int,int),int>();var members=new List<List<int>>();var world=new List<Vector3>();
            var ids=new int[points.Count];for(int i=0;i<ids.Length;i++)ids[i]=-1;
            for(int i=0;i<points.Count;i++)
            {
                if(caps[i]<=0||parts[i]==0)continue;
                Vector3 point=renderer.transform.TransformPoint(points[i]);var key=(Vector3Int.RoundToInt(point*100000f),caps[i]);
                if(!lookup.TryGetValue(key,out int id)){id=members.Count;lookup.Add(key,id);members.Add(new List<int>());world.Add(point);}ids[i]=id;members[id].Add(i);
            }
            var edges=new Dictionary<(int,int),int>();var neighbors=new HashSet<int>[members.Count];var areaNormals=new Vector3[members.Count];
            for(int i=0;i<neighbors.Length;i++)neighbors[i]=new HashSet<int>();
            for(int t=0;t<triangles.Count;t+=3)
            {
                int a=ids[triangles[t]],b=ids[triangles[t+1]],c=ids[triangles[t+2]];
                if(a<0||b<0||c<0||caps[triangles[t]]!=caps[triangles[t+1]]||caps[triangles[t]]!=caps[triangles[t+2]])continue;
                Edge(a,b);Edge(b,c);Edge(c,a);Vector3 normal=Vector3.Cross(world[b]-world[a],world[c]-world[a]);areaNormals[a]+=normal;areaNormals[b]+=normal;areaNormals[c]+=normal;
            }
            var distance=new float[members.Count];for(int i=0;i<distance.Length;i++)distance[i]=float.PositiveInfinity;
            var queue=new Queue<int>();var boundary=new bool[members.Count];
            foreach(var pair in edges)if(pair.Value==1){Seed(pair.Key.Item1);Seed(pair.Key.Item2);}
            while(queue.Count>0)
            {
                int at=queue.Dequeue();foreach(int next in neighbors[at])
                {float candidate=distance[at]+Vector3.Distance(world[at],world[next]);if(candidate+1e-7f>=distance[next])continue;distance[next]=candidate;queue.Enqueue(next);}
            }
            int moved=0;float maxMove=0;
            for(int i=0;i<members.Count;i++)
            {
                if(boundary[i])continue;if(float.IsInfinity(distance[i]))throw new InvalidOperationException("Arm cap has no boundary path.");
                float amount=maximum*(1-Mathf.Exp(-distance[i]/maximum));
                Vector3 delta=renderer.transform.InverseTransformVector(areaNormals[i].normalized*amount);
                foreach(int vertex in members[i])points[vertex]+=delta;
                moved+=members[i].Count;maxMove=Mathf.Max(maxMove,amount);
            }
            Array.Clear(areaNormals,0,areaNormals.Length);
            for(int t=0;t<triangles.Count;t+=3)
            {
                int va=triangles[t],vb=triangles[t+1],vc=triangles[t+2],a=ids[va],b=ids[vb],c=ids[vc];
                if(a<0||b<0||c<0||caps[va]!=caps[vb]||caps[va]!=caps[vc])continue;
                Vector3 normal=Vector3.Cross(points[vb]-points[va],points[vc]-points[va]);areaNormals[a]+=normal;areaNormals[b]+=normal;areaNormals[c]+=normal;
            }
            for(int i=0;i<members.Count;i++)foreach(int vertex in members[i])normals[vertex]=areaNormals[i].normalized;
            Debug.Log("ROUNDED ARM CAPS: movedVertices="+moved+" maxWorldDisplacement="+maxMove+" fixedBoundaryNodes="+boundary.Count(value=>value));
            void Edge(int a,int b){if(a==b)return;var key=(Math.Min(a,b),Math.Max(a,b));edges.TryGetValue(key,out int count);edges[key]=count+1;neighbors[a].Add(b);neighbors[b].Add(a);}
            void Seed(int id){if(boundary[id])return;boundary[id]=true;distance[id]=0;queue.Enqueue(id);}
        }
        static BoneWeight Blend(BoneWeight a,BoneWeight b)
        {
            var sums=new Dictionary<int,float>();Add(a);Add(b);var order=sums.OrderByDescending(p=>p.Value).ThenBy(p=>p.Key).Take(4).ToArray();float sum=order.Sum(p=>p.Value);
            int I(int n)=>n<order.Length?order[n].Key:0;float W(int n)=>n<order.Length?order[n].Value/sum:0;
            return new BoneWeight{boneIndex0=I(0),weight0=W(0),boneIndex1=I(1),weight1=W(1),boneIndex2=I(2),weight2=W(2),boneIndex3=I(3),weight3=W(3)};
            void Add(BoneWeight w){Acc(w.boneIndex0,w.weight0);Acc(w.boneIndex1,w.weight1);Acc(w.boneIndex2,w.weight2);Acc(w.boneIndex3,w.weight3);}
            void Acc(int bone,float value){if(value<=0)return;sums.TryGetValue(bone,out float previous);sums[bone]=previous+value*.5f;}
        }
    }
}
