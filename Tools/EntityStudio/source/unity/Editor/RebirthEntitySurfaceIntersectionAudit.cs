using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio
{
    // Explicit editor probe. Strict edge/triangle crossings exclude adjacent
    // faces, coplanar overlap and contact; zero is not a full manifold proof.
    public static class RebirthEntitySurfaceIntersectionAudit
    {
        public static void WriteArmReport(SkinnedMeshRenderer renderer,string folder,string pose)
        {
            var mesh=renderer.sharedMesh;
            if(mesh.subMeshCount!=3)throw new InvalidOperationException("Arm intersection audit requires body/left/right submeshes.");
            var transforms=renderer.bones.Select((bone,i)=>bone.localToWorldMatrix*mesh.bindposes[i]).ToArray();
            var points=mesh.vertices;var weights=mesh.boneWeights;
            for(int i=0;i<points.Length;i++)
            {
                var p=points[i];var w=weights[i];
                points[i]=transforms[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+transforms[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+transforms[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+transforms[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
            }
            var rows=new List<string>{"submesh,triangleA,triangleB,x,y,z"};
            for(int part=1;part<=2;part++)
            {
                int[] triangles=mesh.GetTriangles(part);int count=triangles.Length/3;
                var bounds=new Bounds[count];var cells=new Dictionary<Vector3Int,List<int>>();
                const float cellSize=.025f;
                for(int t=0;t<count;t++)
                {
                    var box=new Bounds(points[triangles[t*3]],Vector3.zero);box.Encapsulate(points[triangles[t*3+1]]);box.Encapsulate(points[triangles[t*3+2]]);bounds[t]=box;
                    var low=Vector3Int.FloorToInt(box.min/cellSize);var high=Vector3Int.FloorToInt(box.max/cellSize);
                    for(int x=low.x;x<=high.x;x++)for(int y=low.y;y<=high.y;y++)for(int z=low.z;z<=high.z;z++)
                    {var key=new Vector3Int(x,y,z);if(!cells.TryGetValue(key,out var members))cells.Add(key,members=new List<int>());members.Add(t);}
                }
                var tested=new HashSet<ulong>();int crossings=0;
                foreach(var members in cells.Values)for(int ai=0;ai<members.Count;ai++)for(int bi=ai+1;bi<members.Count;bi++)
                {
                    int a=Math.Min(members[ai],members[bi]),b=Math.Max(members[ai],members[bi]);
                    ulong key=((ulong)(uint)a<<32)|(uint)b;if(!tested.Add(key)||!bounds[a].Intersects(bounds[b]))continue;
                    bool adjacent=false;for(int i=0;i<3&&!adjacent;i++)for(int j=0;j<3;j++)if((points[triangles[a*3+i]]-points[triangles[b*3+j]]).sqrMagnitude<1e-10f){adjacent=true;break;}
                    if(adjacent)continue;
                    Vector3 hit=Vector3.zero;bool crossed=false;
                    for(int edge=0;edge<3&&!crossed;edge++)crossed=RebirthEntityClosureIntersectionAudit.Cross(points[triangles[a*3+edge]],points[triangles[a*3+(edge+1)%3]],points[triangles[b*3]],points[triangles[b*3+1]],points[triangles[b*3+2]],out hit);
                    for(int edge=0;edge<3&&!crossed;edge++)crossed=RebirthEntityClosureIntersectionAudit.Cross(points[triangles[b*3+edge]],points[triangles[b*3+(edge+1)%3]],points[triangles[a*3]],points[triangles[a*3+1]],points[triangles[a*3+2]],out hit);
                    if(!crossed)continue;crossings++;
                    rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3:R},{4:R},{5:R}",part,a,b,hit.x,hit.y,hit.z));
                }
                Debug.Log("ARM SURFACE INTERSECTIONS pose="+pose+" part="+part+" strictCrossingPairs="+crossings+" trianglePairsExamined="+tested.Count);
            }
            File.WriteAllLines(Path.Combine(folder,"SurfaceCrossings_"+pose+".csv"),rows);
        }
    }
}
