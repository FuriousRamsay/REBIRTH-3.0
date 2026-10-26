using System;
using System.IO;
using System.Globalization;
using UnityEditor;
using UnityEngine;
public static class RebirthCamperRoofMeasure
{
    public static void RunBatch()
    {
        const string folder="E:/_Haven/shared/REBIRTHTemp/CamperRoofPrototype_20261012";
        Directory.CreateDirectory(folder);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperWrapGenerated/FinishCandidates/d976037983224f3e98a789d95227988a/rebirthCamper80s.prefab");
        if(source==null)throw new InvalidDataException("Approved camper source missing");
        var model=UnityEngine.Object.Instantiate(source);int offset=0;
        try
        {
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            using(var writer=new StreamWriter(folder+"/camper-reference.obj"))
            {
                foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
                {
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
                    if(!filter.sharedMesh.isReadable)continue; // Roof sampling uses editor colliders; never alter original import flags.
                    writer.WriteLine("o "+filter.name.Replace(' ','_'));
                    foreach(var vertex in filter.sharedMesh.vertices)
                    {var p=filter.transform.TransformPoint(vertex);writer.WriteLine(string.Format(CultureInfo.InvariantCulture,"v {0:R} {1:R} {2:R}",p.x,p.y,p.z));}
                    var indices=filter.sharedMesh.triangles;
                    for(int i=0;i<indices.Length;i+=3)writer.WriteLine("f "+(offset+indices[i]+1)+" "+(offset+indices[i+1]+1)+" "+(offset+indices[i+2]+1));
                    offset+=filter.sharedMesh.vertexCount;
                }
            }
            Physics.SyncTransforms();
            using(var writer=new StreamWriter(folder+"/roof-surface.csv"))
            {
                writer.WriteLine("x,y,z");
                for(float x=-1.4f;x<=1.4f;x+=.1f)for(float z=-4;z<=3.5f;z+=.1f)
                {RaycastHit hit;if(Physics.Raycast(new Vector3(x,10,z),Vector3.down,out hit,20) && hit.point.y>2)
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0:R},{1:R},{2:R}",hit.point.x,hit.point.y,hit.point.z));}
            }
            Debug.Log("ROOF_SURFACE_AND_BLENDER_REFERENCE_EXPORTED");
        }
        finally{UnityEngine.Object.DestroyImmediate(model);}
    }
}
