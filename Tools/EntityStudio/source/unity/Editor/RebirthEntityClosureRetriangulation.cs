using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityClosureRetriangulation
    {
        // Diagnostic only: retain all boundary positions, UVs and weights.
        public static void Apply(SkinnedMeshRenderer renderer,EntitySurfaceRepairInput input)
        {
            var mesh=renderer.sharedMesh;var points=mesh.vertices;var old=mesh.triangles;var output=new List<int>();int rebuilt=0;
            bool constrain=Array.IndexOf(Environment.GetCommandLineArgs(),"-avoidClosureCrossings")>=0;
            var world=Skin(renderer);var poseWorld=new List<Vector3[]>{world};
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-closureAllPoses")>=0)
            {
                var workspace=renderer.GetComponentInParent<RebirthEntityRigWorkspace>();
                var rotations=workspace.generatedSkeleton.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.localRotation);
                try
                {
                    foreach(bool reach in new[]{false,true})
                    {
                        foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                        foreach(string side in new[]{"Left","Right"})
                        {RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,side+"ForeArm").localRotation*=Quaternion.Euler(70,0,0);if(reach)RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,side+"Arm").localRotation*=Quaternion.Euler(25,0,side=="Left"?-35:35);}
                        poseWorld.Add(Skin(renderer));
                    }
                    if(Array.IndexOf(Environment.GetCommandLineArgs(),"-closureFingerPoses")>=0)
                    {
                        foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                        foreach(string side in new[]{"Left","Right"})foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Pinky"})for(int joint=1;joint<=3;joint++)
                        {var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,side+"Hand"+finger+joint);if(bone==null)throw new InvalidOperationException("Missing closure finger constraint bone.");bone.localRotation*=Quaternion.Euler(0,0,(joint==1?45:joint==2?30:20)*(side=="Left"?1:-1));}
                        poseWorld.Add(Skin(renderer));
                    }
                }
                finally{foreach(var pair in rotations)pair.Key.localRotation=pair.Value;}
            }
            var targets=new HashSet<int>();for(int i=0;i<points.Length;i++)if(input.repairVertexParts[i]>0&&input.repairVertexIds[i]>0)targets.Add(input.repairVertexIds[i]);
            for(int t=0;t<old.Length;t+=3)if(!targets.Contains(input.repairVertexIds[old[t]])){output.Add(old[t]);output.Add(old[t+1]);output.Add(old[t+2]);}
            foreach(int cap in targets)
            {
                var canonical=new Dictionary<Vector3Int,int>();var ids=new Dictionary<int,int>();
                for(int i=0;i<points.Length;i++)if(input.repairVertexIds[i]==cap)
                {var key=Vector3Int.RoundToInt(renderer.transform.TransformPoint(points[i])*100000f);if(!canonical.TryGetValue(key,out int id)){id=i;canonical.Add(key,id);}ids.Add(i,id);}
                var counts=new Dictionary<(int,int),int>();var directed=new Dictionary<(int,int),(int a,int b)>();
                for(int t=0;t<old.Length;t+=3)
                {
                    if(input.repairVertexIds[old[t]]!=cap)continue;
                    for(int e=0;e<3;e++){int a=ids[old[t+e]],b=ids[old[t+(e+1)%3]];if(a==b)continue;var key=(Math.Min(a,b),Math.Max(a,b));counts.TryGetValue(key,out int count);counts[key]=count+1;directed[key]=(a,b);}
                }
                var outgoing=new Dictionary<int,int>();foreach(var pair in counts)if(pair.Value==1){var edge=directed[pair.Key];if(outgoing.ContainsKey(edge.a))throw new InvalidOperationException("Branched closure boundary cap="+cap);outgoing.Add(edge.a,edge.b);}
                while(outgoing.Count>0)
                {
                    int first=outgoing.Keys.First(),current=first;var loop=new List<int>();
                    do{loop.Add(current);if(!outgoing.TryGetValue(current,out int next))throw new InvalidOperationException("Open closure boundary cap="+cap);outgoing.Remove(current);current=next;}while(current!=first);
                    int[] triangles;
                    try
                    {
                        int part=input.repairVertexParts[loop[0]];
                        var obstacleFrames=new List<(Vector3 a,Vector3 b,Vector3 c,Bounds bounds)>[poseWorld.Count];
                        var boundaryFrames=new Vector3[poseWorld.Count][];
                        for(int frame=0;frame<poseWorld.Count;frame++)
                        {
                            var posed=poseWorld[frame];var obstacles=new List<(Vector3 a,Vector3 b,Vector3 c,Bounds bounds)>();obstacleFrames[frame]=obstacles;boundaryFrames[frame]=loop.Select(i=>posed[i]).ToArray();
                            if(constrain)for(int t=0;t<old.Length;t+=3)
                            {
                                int ia=old[t];if(input.repairVertexParts[ia]!=part||input.repairVertexIds[ia]!=0)continue;
                                Vector3 a=posed[ia],b=posed[old[t+1]],c=posed[old[t+2]];var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);obstacles.Add((a,b,c,bounds));
                            }
                        }
                        var boundary=loop.Select(i=>world[i]).ToArray();
                        triangles=TriangulateSpatial(boundary,Allowed);
                        bool Allowed(int ia,int ib,int ic)
                        {
                            if(!constrain)return true;
                            for(int frame=0;frame<poseWorld.Count;frame++)
                            {
                                var posed=boundaryFrames[frame];Vector3 a=posed[ia],b=posed[ib],c=posed[ic];var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);
                                foreach(var obstacle in obstacleFrames[frame])
                                {
                                    if(!bounds.Intersects(obstacle.bounds))continue;
                                    if(Shared(a)||Shared(b)||Shared(c))continue;
                                    if(Hit(a,b,obstacle.a,obstacle.b,obstacle.c)||Hit(b,c,obstacle.a,obstacle.b,obstacle.c)||Hit(c,a,obstacle.a,obstacle.b,obstacle.c)||Hit(obstacle.a,obstacle.b,a,b,c)||Hit(obstacle.b,obstacle.c,a,b,c)||Hit(obstacle.c,obstacle.a,a,b,c))return false;
                                    bool Shared(Vector3 point)=>(point-obstacle.a).sqrMagnitude<1e-10f||(point-obstacle.b).sqrMagnitude<1e-10f||(point-obstacle.c).sqrMagnitude<1e-10f;
                                }
                            }
                            return true;
                        }
                    }
                    catch(Exception ex)
                    {
                        string folder=System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerClosureBoundary");System.IO.Directory.CreateDirectory(folder);
                        var rows=new List<string>{"vertex,x,y,z,u,v"};var uv=mesh.uv;
                        foreach(int id in loop){var p=renderer.transform.TransformPoint(points[id]);rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4:R},{5:R}",id,p.x,p.y,p.z,uv[id].x,uv[id].y));}
                        System.IO.File.WriteAllLines(System.IO.Path.Combine(folder,"Cap"+cap+".csv"),rows);
                        throw new InvalidOperationException("T-pose closure projection failed cap="+cap+" boundaryVertices="+loop.Count+"; saved boundary trace for reconstruction.",ex);
                    }
                    foreach(int i in triangles)output.Add(loop[i]);rebuilt++;
                }
            }
            mesh.triangles=output.ToArray();
            var normals=mesh.normals;var sums=new Dictionary<(Vector3Int,int),Vector3>();
            for(int t=0;t<output.Count;t+=3)
            {
                int a=output[t],b=output[t+1],c=output[t+2],cap=input.repairVertexIds[a];if(!targets.Contains(cap))continue;
                Vector3 normal=Vector3.Cross(points[b]-points[a],points[c]-points[a]);
                foreach(int id in new[]{a,b,c}){var key=(Vector3Int.RoundToInt(points[id]*100000f),cap);sums.TryGetValue(key,out Vector3 prior);sums[key]=prior+normal;}
            }
            for(int i=0;i<points.Length;i++)if(targets.Contains(input.repairVertexIds[i]))
            {var key=(Vector3Int.RoundToInt(points[i]*100000f),input.repairVertexIds[i]);if(sums.TryGetValue(key,out Vector3 normal)&&normal.sqrMagnitude>1e-18f)normals[i]=normal.normalized;}
            mesh.normals=normals;mesh.RecalculateBounds();mesh.RecalculateTangents();
            Debug.Log("CLOSURE RETRIANGULATION: loops="+rebuilt+" triangles="+old.Length/3+"->"+output.Count/3+" boundary positions/weights/UV unchanged; diagnostic only.");
        }

        // A disk triangulation on the ordered 3D loop. This avoids a mandatory
        // planar projection; it is a candidate, not proof against intersections.
        static bool Hit(Vector3 start,Vector3 end,Vector3 a,Vector3 b,Vector3 c)=>RebirthEntityClosureIntersectionAudit.Cross(start,end,a,b,c,out _);
        static Vector3[] Skin(SkinnedMeshRenderer renderer)
        {
            var mesh=renderer.sharedMesh;var points=mesh.vertices;var weights=mesh.boneWeights;var binds=mesh.bindposes;var matrices=renderer.bones.Select((bone,index)=>bone.localToWorldMatrix*binds[index]).ToArray();
            for(int i=0;i<points.Length;i++){var w=weights[i];var p=points[i];points[i]=matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;}return points;
        }
        static int[] TriangulateSpatial(Vector3[] boundary,Func<int,int,int,bool> allowed)
        {
            int n=boundary.Length;if(n<3)throw new ArgumentException("Closure needs three vertices.");
            var cost=new double[n,n];var split=new int[n,n];
            for(int width=2;width<n;width++)for(int a=0;a+width<n;a++)
            {
                int c=a+width;cost[a,c]=double.PositiveInfinity;split[a,c]=-1;
                for(int b=a+1;b<c;b++)
                {
                    Vector3 ab=boundary[b]-boundary[a],bc=boundary[c]-boundary[b],ca=boundary[a]-boundary[c];
                    double area=.5*Vector3.Cross(ab,-ca).magnitude;
                    double candidate=cost[a,b]+cost[b,c]+area+.05*(ab.sqrMagnitude+bc.sqrMagnitude+ca.sqrMagnitude);
                    if(candidate<cost[a,c]&&allowed(a,b,c)){cost[a,c]=candidate;split[a,c]=b;}
                }
            }
            var result=new List<int>();Emit(0,n-1);return result.ToArray();
            void Emit(int a,int c){if(c-a<2)return;int b=split[a,c];if(b<=a||b>=c)throw new InvalidOperationException("No finite spatial closure fill.");result.Add(a);result.Add(b);result.Add(c);Emit(a,b);Emit(b,c);}
        }
    }
}
