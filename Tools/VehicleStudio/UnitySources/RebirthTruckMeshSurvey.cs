using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

// Read-only export of the existing review's ORIGINAL truck geometry for fitting.
public static class RebirthTruckMeshSurvey
{
    [Serializable] class Part { public string path; public Vector3[] vertices; public Vector3[] normals; public Vector2[] uv; public int[] triangles; }
    [Serializable] class Survey { public string coordinates="Unity metres: X lateral, Y up, Z forward"; public List<Part> parts=new List<Part>(); }
    public static void Export()
    {
        string output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TruckBodyworkDetailed_20261020";
        Directory.CreateDirectory(output);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHBareTruckBodywork_20261020_v3/rebirthCamper80s.prefab"));
        var excluded=new HashSet<Renderer>();
        foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))
        {
            var levels=lod.GetLODs();
            foreach(var level in levels.Skip(1)) foreach(var r in level.renderers) if(r)excluded.Add(r);
        }
        var survey=new Survey();
        foreach(var f in root.GetComponentsInChildren<MeshFilter>(false))
        {
            var r=f.GetComponent<Renderer>(); if(!r||excluded.Contains(r)||!f.sharedMesh)continue;
            string path=AnimationUtility.CalculateTransformPath(f.transform,root.transform);
            if(path.Contains("REBIRTHPickupOnlyBodywork")||path.Contains("REBIRTHTruckCommonChassis")||path.Contains("shadow_plane")||path.Contains("DistanceLOD"))continue;
            var mesh=f.sharedMesh;
            survey.parts.Add(new Part{path=path,vertices=mesh.vertices.Select(v=>f.transform.TransformPoint(v)).ToArray(),normals=mesh.normals.Select(n=>f.transform.TransformDirection(n)).ToArray(),uv=mesh.uv,triangles=mesh.triangles});
        }
        File.WriteAllText(output+"/native-truck.json",JsonUtility.ToJson(survey));
        File.WriteAllText(output+"/survey.txt",string.Join("\n",survey.parts.Select(p=>p.path+" vertices="+p.vertices.Length+" triangles="+p.triangles.Length/3)));
        Object.DestroyImmediate(root);
    }
}
