using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntitySkinAudit
    {
        sealed class Usage {public int vertices;public float total;public Vector3 position;}
        public static void Write(RebirthEntityRigWorkspace workspace,string path)
        {
            var usage=new Dictionary<Transform,Usage>();
            foreach(var renderer in workspace.sourceSkinnedMeshes)
            {
                foreach(var bone in renderer.bones)if(!usage.ContainsKey(bone))usage.Add(bone,new Usage());
                Vector3[] points=renderer.sharedMesh.vertices;BoneWeight[] weights=renderer.sharedMesh.boneWeights;
                for(int i=0;i<points.Length;i++)
                {
                    BoneWeight w=weights[i];Vector3 point=renderer.transform.TransformPoint(points[i]);
                    Add(w.boneIndex0,w.weight0,point);Add(w.boneIndex1,w.weight1,point);Add(w.boneIndex2,w.weight2,point);Add(w.boneIndex3,w.weight3,point);
                }
                void Add(int index,float weight,Vector3 point){if(weight<=.000001f)return;var value=usage[renderer.bones[index]];value.vertices++;value.total+=weight;value.position+=point*weight;}
            }
            var rows=new List<string>{"bone,influencedVertices,totalWeight,boneX,boneY,boneZ,weightedMeshX,weightedMeshY,weightedMeshZ"};
            foreach(var pair in usage)
            {
                Vector3 position=pair.Key.position,center=pair.Value.total>0?pair.Value.position/pair.Value.total:Vector3.zero;
                rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R}",pair.Key.name,pair.Value.vertices,pair.Value.total,position.x,position.y,position.z,center.x,center.y,center.z));
            }
            File.WriteAllLines(path,rows);
        }
    }
}
