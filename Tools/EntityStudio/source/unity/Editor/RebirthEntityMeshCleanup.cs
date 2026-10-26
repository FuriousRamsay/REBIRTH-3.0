using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityMeshCleanup
    {
        private struct Vertex
        {
            public Vector3 position,normal,normalized;public Vector2 uv;public int repair;
            public Vector3Int anchors;public Vector3 barycentric;
            public static Vertex Lerp(Vertex a,Vertex b,float t){return new Vertex{position=Vector3.Lerp(a.position,b.position,t),normal=Vector3.Lerp(a.normal,b.normal,t).normalized,normalized=Vector3.Lerp(a.normalized,b.normalized,t),uv=Vector2.Lerp(a.uv,b.uv,t),repair=a.repair==b.repair?a.repair:0,anchors=a.anchors,barycentric=Vector3.Lerp(a.barycentric,b.barycentric,t)};}
        }
        private struct Edge{public int a,b,wa,wb;public Edge(int a,int b,int wa,int wb){this.a=a;this.b=b;this.wa=wa;this.wb=wb;}}

        public static int[] SeparateFusedArms(Mesh mesh,Transform source,Bounds worldBounds,Vector2? handRange=null,List<EntitySurfaceRepairInput> repairs=null,float handCutInset=0,float wristCutInset=0)
        {
            if(float.IsNaN(handCutInset)||handCutInset<0||handCutInset>.08f)throw new ArgumentOutOfRangeException(nameof(handCutInset));
            if(float.IsNaN(wristCutInset)||wristCutInset<0||wristCutInset>.08f)throw new ArgumentOutOfRangeException(nameof(wristCutInset));
            float Side(Vector3 position,int side)=>CutSide(position,side,handCutInset,wristCutInset);
            bool OnCut(Vector3 a,Vector3 b)=>IsOnCut(a,b,handCutInset,wristCutInset);
            if(mesh.subMeshCount!=1)throw new InvalidOperationException("Fused-arm cleanup requires one baked material atlas.");
            Vector3[] sourcePoints=mesh.vertices,sourceNormals=mesh.normals;Vector2[] sourceUv=mesh.uv;
            if(sourcePoints.Length!=sourceNormals.Length||sourcePoints.Length!=sourceUv.Length)throw new InvalidOperationException("Complete normals and UVs required.");
            var input=new Vertex[sourcePoints.Length];
            for(int i=0;i<input.Length;i++)input[i]=new Vertex{position=sourcePoints[i],normal=sourceNormals[i],uv=sourceUv[i],normalized=RebirthEntityRigBuilder.NormalizeBonePositionInBounds(worldBounds,source.TransformPoint(sourcePoints[i]))};
            var vertices=new List<Vertex>();var groups=new List<int>();var triangles=new List<int>();
            var mapped=new Dictionary<string,int>();var weldCells=new Dictionary<Vector4,List<int>>();var weldPoints=new List<Vector3>();var ids=new List<int>();
            float[] bands={-.01f,.35f,.51f,.57f,.85f,1.01f};int armTriangles=0;
            int[] original=mesh.triangles;
            for(int i=0;i<original.Length;i+=3)
                for(int band=0;band<bands.Length-1;band++)
                {
                    var polygon=new List<Vertex>{input[original[i]],input[original[i+1]],input[original[i+2]]};
                    polygon=Clip(polygon,p=>p.normalized.y-bands[band],true);
                    polygon=Clip(polygon,p=>p.normalized.y-bands[band+1],false);
                    if(polygon.Count<3)continue;
                    if(band==0||band==4){AddPolygon(polygon,0);continue;}
                    AddPolygon(Clip(polygon,p=>Side(p.normalized,0),false),1);
                    AddPolygon(Clip(polygon,p=>Side(p.normalized,1),true),2);
                    AddPolygon(Clip(Clip(polygon,p=>Side(p.normalized,0),true),p=>Side(p.normalized,1),false),0);
                }
            RemoveIsolatedArmFragments();
            EntitySurfaceRepairInput repairInput=null;
            if(repairs!=null)
            {
                repairInput=new EntitySurfaceRepairInput{repairedMesh=mesh,sourcePositionsWorld=new Vector3[vertices.Count],sourceUv=new Vector2[vertices.Count],sourceTriangles=triangles.ToArray(),sourceTriangleParts=new int[triangles.Count/3]};
                for(int i=0;i<vertices.Count;i++){repairInput.sourcePositionsWorld[i]=source.TransformPoint(vertices[i].position);repairInput.sourceUv[i]=vertices[i].uv;}
                for(int i=0;i<triangles.Count;i+=3)repairInput.sourceTriangleParts[i/3]=groups[triangles[i]];
            }
            var edges=new Dictionary<ulong,List<Edge>>();
            for(int i=0;i<triangles.Count;i+=3)
                for(int j=0;j<3;j++)
                {
                    int a=triangles[i+j],b=triangles[i+(j+1)%3];if(ids[a]==ids[b])continue;
                    ulong key=Key(ids[a],ids[b]);if(!edges.TryGetValue(key,out var list)){list=new List<Edge>();edges[key]=list;}list.Add(new Edge(a,b,ids[a],ids[b]));
                }
            var boundary=new List<Edge>();
            foreach(var pair in edges)
                if(pair.Value.Count==1)boundary.Add(pair.Value[0]);
            var outgoing=new Dictionary<int,List<int>>();
            for(int i=0;i<boundary.Count;i++){if(!outgoing.TryGetValue(boundary[i].wa,out var list)){list=new List<int>();outgoing[boundary[i].wa]=list;}list.Add(i);}
            var used=new HashSet<int>();int capped=0;
            for(int first=0;first<boundary.Count;first++)
            {
                if(used.Contains(first))continue;var loop=new List<Edge>();int current=first;bool closed=false;
                for(int step=0;step<=boundary.Count;step++)
                {
                    if(!used.Add(current))break;Edge edge=boundary[current];loop.Add(edge);
                    if(edge.wb==boundary[first].wa){closed=true;break;}
                    if(!outgoing.TryGetValue(edge.wb,out var next))break;current=-1;
                    foreach(int candidate in next)if(!used.Contains(candidate)){current=candidate;break;}
                    if(current<0)break;
                }
                bool touchesCut=false;
                foreach(var edge in loop)if(OnCut(vertices[edge.a].normalized,vertices[edge.b].normalized))touchesCut=true;
                // A cut may meet an existing tiny source opening. Follow that opening
                // to close this cut loop, while preserving unrelated original holes.
                if(!touchesCut)continue;
                if(!closed)
                {
                    var rows=new List<string>{"group,cut,ax,ay,az,bx,by,bz"};
                    foreach(var pair in edges)if(pair.Value.Count==1)
                    {
                        Edge edge=pair.Value[0];Vector3 a=vertices[edge.a].normalized,b=vertices[edge.b].normalized;
                        rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R}",groups[edge.a],OnCut(a,b)?1:0,a.x,a.y,a.z,b.x,b.y,b.z));
                    }
                    string failurePath=System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName,"Logs","EntityStudioValidation","RebirthFarmer_CutBoundaryFailure.csv");
                    System.IO.File.WriteAllLines(failurePath,rows);
                    throw new InvalidOperationException("An anatomical cut has an open/nonmanifold boundary. Trace: "+failurePath+" start="+vertices[loop[0].a].normalized.ToString("F6")+" end="+vertices[loop[loop.Count-1].b].normalized.ToString("F6"));
                }
                if(loop.Count<3)continue;
                var boundaryPoints=new Vector3[loop.Count];
                for(int n=0;n<loop.Count;n++)boundaryPoints[n]=vertices[loop[n].a].position;
                int[] cap=RebirthEntityCapTriangulation.Triangulate(boundaryPoints);
                int group=groups[loop[0].a];
                for(int n=0;n<cap.Length;n+=3)
                {
                    // Reverse the original surface boundary winding. There is no
                    // central vertex joining a wrist directly to the shoulder.
                    Vertex a=vertices[loop[cap[n]].a],b=vertices[loop[cap[n+2]].a],c=vertices[loop[cap[n+1]].a];
                    a.repair=capped+1;b.repair=capped+1;c.repair=capped+1;
                    var anchors=new Vector3Int(loop[cap[n]].a,loop[cap[n+2]].a,loop[cap[n+1]].a);
                    a.anchors=anchors;b.anchors=anchors;c.anchors=anchors;
                    a.barycentric=Vector3.right;b.barycentric=Vector3.up;c.barycentric=Vector3.forward;
                    Vector3 normal=Vector3.Cross(b.position-a.position,c.position-a.position).normalized;
                    a.normal=normal;b.normal=normal;c.normal=normal;
                    int at=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);groups.Add(group);groups.Add(group);groups.Add(group);
                    triangles.Add(at);triangles.Add(at+1);triangles.Add(at+2);
                }
                capped++;
            }
            if(armTriangles==0)throw new InvalidOperationException("The configured cut found no arm surface.");
            RefineHandTriangles(vertices,groups,triangles,source,worldBounds.size.y*.007f,handRange??new Vector2(.405f,.49f));
            var points=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();
            foreach(var vertex in vertices){points.Add(vertex.position);normals.Add(vertex.normal);uv.Add(vertex.uv);}
            mesh.Clear();if(points.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(points);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateTangents();mesh.RecalculateBounds();
            if(repairInput!=null)
            {
                repairInput.repairPositionsWorld=new Vector3[vertices.Count];repairInput.repairVertexIds=new int[vertices.Count];repairInput.repairVertexParts=groups.ToArray();
                repairInput.repairWeightAnchors=new Vector3Int[vertices.Count];repairInput.repairWeightBarycentrics=new Vector3[vertices.Count];
                for(int i=0;i<vertices.Count;i++){repairInput.repairPositionsWorld[i]=source.TransformPoint(vertices[i].position);repairInput.repairVertexIds[i]=vertices[i].repair;repairInput.repairWeightAnchors[i]=vertices[i].anchors;repairInput.repairWeightBarycentrics[i]=vertices[i].barycentric;}
                repairs.Add(repairInput);
            }
            Debug.Log("REBIRTH FUSED-ARM CLEANUP: partitionedArmTriangles="+armTriangles+" cappedLoops="+capped+" vertices="+points.Count);
            return groups.ToArray();

            void RemoveIsolatedArmFragments()
            {
                var parent=new int[vertices.Count];for(int i=0;i<parent.Length;i++)parent[i]=i;
                int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
                void Join(int a,int b){a=Find(a);b=Find(b);if(a!=b)parent[b]=a;}
                var welded=new Dictionary<(Vector3Int,int),int>();
                for(int i=0;i<vertices.Count;i++)
                {
                    var key=(Vector3Int.RoundToInt(source.TransformPoint(vertices[i].position)*100000f),groups[i]);
                    if(welded.TryGetValue(key,out int prior))Join(i,prior);else welded.Add(key,i);
                }
                for(int i=0;i<triangles.Count;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
                var areas=new Dictionary<int,float>();var largest=new int[]{-1,-1,-1};
                for(int i=0;i<triangles.Count;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2],root=Find(a);if(groups[a]==0)continue;
                    float area=Vector3.Cross(source.TransformPoint(vertices[b].position)-source.TransformPoint(vertices[a].position),source.TransformPoint(vertices[c].position)-source.TransformPoint(vertices[a].position)).magnitude*.5f;
                    areas.TryGetValue(root,out float prior);areas[root]=prior+area;
                }
                foreach(var pair in areas){int part=groups[pair.Key];if(largest[part]<0||pair.Value>areas[largest[part]])largest[part]=pair.Key;}
                var kept=new List<int>();int removed=0;
                for(int i=0;i<triangles.Count;i+=3)
                {
                    int part=groups[triangles[i]],root=Find(triangles[i]);
                    if(part>0&&root!=largest[part])
                    {
                        if(areas[root]>areas[largest[part]]*.02f)throw new InvalidOperationException("An anatomical cut separated a substantial arm component; automatic fragment removal refused.");
                        removed++;continue;
                    }
                    kept.Add(triangles[i]);kept.Add(triangles[i+1]);kept.Add(triangles[i+2]);
                }
                if(removed==0)return;
                var remap=new Dictionary<int,int>();var compactVertices=new List<Vertex>();var compactGroups=new List<int>();var compactIds=new List<int>();
                for(int i=0;i<kept.Count;i++)
                {
                    int old=kept[i];if(!remap.TryGetValue(old,out int next)){next=compactVertices.Count;remap.Add(old,next);compactVertices.Add(vertices[old]);compactGroups.Add(groups[old]);compactIds.Add(ids[old]);}kept[i]=next;
                }
                vertices=compactVertices;groups=compactGroups;ids=compactIds;triangles=kept;
                Debug.Log("REBIRTH ARM FRAGMENTS: removed "+removed+" triangles from isolated components below 2% of the main arm area.");
            }

            void AddPolygon(List<Vertex> polygon,int group)
            {
                if(polygon.Count<3)return;
                for(int i=1;i<polygon.Count-1;i++)
                {
                    if(Vector3.Cross(polygon[i].position-polygon[0].position,polygon[i+1].position-polygon[0].position).sqrMagnitude<1e-15f)continue;
                    triangles.Add(Map(polygon[0],group));triangles.Add(Map(polygon[i],group));triangles.Add(Map(polygon[i+1],group));if(group!=0)armTriangles++;
                }
            }
            int Map(Vertex vertex,int group)
            {
                Vector3Int p=Vector3Int.RoundToInt(vertex.position*1000000f),n=Vector3Int.RoundToInt(vertex.normal*100000f);Vector2Int t=Vector2Int.RoundToInt(vertex.uv*1000000f);
                string key=group+":"+p.x+","+p.y+","+p.z+":"+n.x+","+n.y+","+n.z+":"+t.x+","+t.y;
                if(mapped.TryGetValue(key,out int index))return index;
                index=vertices.Count;mapped[key]=index;vertices.Add(vertex);groups.Add(group);
                Vector3 q=vertex.normalized;Vector3Int cell=Vector3Int.FloorToInt(q*100000f);int id=-1;
                for(int x=-1;x<=1&&id<0;x++)for(int y=-1;y<=1&&id<0;y++)for(int z=-1;z<=1&&id<0;z++)
                    if(weldCells.TryGetValue(new Vector4(cell.x+x,cell.y+y,cell.z+z,group),out var candidates))
                        foreach(int candidate in candidates)if((weldPoints[candidate]-q).sqrMagnitude<1e-10f){id=candidate;break;}
                if(id<0)
                {
                    id=weldPoints.Count;weldPoints.Add(q);var weldKey=new Vector4(cell.x,cell.y,cell.z,group);
                    if(!weldCells.TryGetValue(weldKey,out var bucket)){bucket=new List<int>();weldCells[weldKey]=bucket;}bucket.Add(id);
                }
                ids.Add(id);return index;
            }
        }
        private static List<Vertex> Clip(List<Vertex> polygon,Func<Vertex,float> distance,bool positive)
        {
            var result=new List<Vertex>();if(polygon.Count==0)return result;
            Vertex previous=polygon[polygon.Count-1];float before=distance(previous);bool previousInside=positive?before>=0f:before<=0f;
            foreach(var current in polygon)
            {
                float after=distance(current);bool currentInside=positive?after>=0f:after<=0f;
                if(currentInside!=previousInside){float denominator=before-after;if(Mathf.Abs(denominator)>1e-12f)result.Add(Vertex.Lerp(previous,current,before/denominator));}
                if(currentInside)result.Add(current);previous=current;before=after;previousInside=currentInside;
            }
            return result;
        }
        private static ulong Key(int a,int b){uint x=(uint)Mathf.Min(a,b),y=(uint)Mathf.Max(a,b);return ((ulong)x<<32)|y;}
        private static void RefineHandTriangles(List<Vertex> vertices,List<int> groups,List<int> triangles,Transform source,float maximumEdge,Vector2 handRange)
        {
            int before=triangles.Count/3;
            for(int pass=0;pass<8;pass++)
            {
                var midpoints=new Dictionary<ulong,int>();
                for(int i=0;i<triangles.Count;i+=3)for(int edge=0;edge<3;edge++)
                {
                    int a=triangles[i+edge],b=triangles[i+(edge+1)%3];
                    bool handA=groups[a]>0&&vertices[a].normalized.y>handRange.x&&vertices[a].normalized.y<handRange.y;
                    bool handB=groups[b]>0&&vertices[b].normalized.y>handRange.x&&vertices[b].normalized.y<handRange.y;
                    if(!handA&&!handB)continue;ulong key=((ulong)(uint)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);
                    if(midpoints.ContainsKey(key)||Vector3.Distance(source.TransformPoint(vertices[a].position),source.TransformPoint(vertices[b].position))<=maximumEdge)continue;
                    midpoints[key]=vertices.Count;vertices.Add(Vertex.Lerp(vertices[a],vertices[b],.5f));groups.Add(groups[a]);
                }
                if(midpoints.Count==0)break;var refined=new List<int>();
                for(int i=0;i<triangles.Count;i+=3)
                {
                    int count=0,first=0,missing=0;
                    for(int edge=0;edge<3;edge++)if(midpoints.ContainsKey(EdgeKey(triangles[i+edge],triangles[i+(edge+1)%3]))){count++;first=edge;}else missing=edge;
                    if(count==0){Add(triangles[i],triangles[i+1],triangles[i+2]);continue;}
                    int rotation=count==2?(missing+1)%3:first;
                    int a=triangles[i+rotation],b=triangles[i+(rotation+1)%3],c=triangles[i+(rotation+2)%3],ab=midpoints[EdgeKey(a,b)];
                    if(count==1){Add(a,ab,c);Add(ab,b,c);}
                    else{int bc=midpoints[EdgeKey(b,c)];if(count==2){Add(a,ab,c);Add(ab,bc,c);Add(ab,b,bc);}else{int ca=midpoints[EdgeKey(c,a)];Add(a,ab,ca);Add(ab,b,bc);Add(ca,bc,c);Add(ab,bc,ca);}}
                }
                triangles.Clear();triangles.AddRange(refined);
                void Add(int a,int b,int c){refined.Add(a);refined.Add(b);refined.Add(c);}
            }
            Debug.Log("REBIRTH HAND TOPOLOGY: triangles="+before+" -> "+triangles.Count/3+" maxHandEdge="+maximumEdge);
            ulong EdgeKey(int a,int b)=>((ulong)(uint)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);
        }

        private static float CutSide(Vector3 position,int side,float inset,float wrist)
        {float left=Mathf.Lerp(.145f,.17f,Mathf.InverseLerp(.51f,.57f,position.y))-inset*(1-Mathf.InverseLerp(.35f,.51f,position.y))-wrist*Mathf.InverseLerp(.35f,.51f,position.y)*Mathf.InverseLerp(.57f,.51f,position.y);return position.x-(side==0?left:1f-left);}
        private static bool IsOnCut(Vector3 a,Vector3 b,float inset,float wrist)
        {
            const float epsilon=.00003f;
            if(Mathf.Abs(CutSide(a,0,inset,wrist))<epsilon&&Mathf.Abs(CutSide(b,0,inset,wrist))<epsilon||Mathf.Abs(CutSide(a,1,inset,wrist))<epsilon&&Mathf.Abs(CutSide(b,1,inset,wrist))<epsilon)return true;
            foreach(float y in new[]{.35f,.85f})if(Mathf.Abs(a.y-y)<epsilon&&Mathf.Abs(b.y-y)<epsilon)return true;
            return false;
        }
    }
}
