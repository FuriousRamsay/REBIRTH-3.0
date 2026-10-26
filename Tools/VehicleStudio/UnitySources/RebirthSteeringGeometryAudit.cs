using System;
using System.IO;
using System.Globalization;
using UnityEditor;
using UnityEngine;

// Export actual baked geometry; derive the shaft/rim axis before changing a candidate.
public static class RebirthSteeringGeometryAudit
{
    public static void RunBatch()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHStandardVehiclesGenerated/BuggyFinal_v1/rebirthBuggy.prefab");
        if(prefab==null)throw new InvalidDataException("Candidate missing");
        var root=UnityEngine.Object.Instantiate(prefab);
        try
        {
            var steering=root.transform.Find("M/Steering");
            if(steering==null)throw new InvalidDataException("Steering missing");
            string output="E:/_Haven/shared/REBIRTHTemp/BuggyBikeAudit_20261012/steering-vertices.csv";
            using(var writer=new StreamWriter(output))
            {
                writer.WriteLine("x,y,z");
                foreach(var filter in steering.GetComponentsInChildren<MeshFilter>())
                foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var p=root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0:R},{1:R},{2:R}",p.x,p.y,p.z));
                }
            }
            Debug.Log("STEERING_GEOMETRY_EXPORTED "+output);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
