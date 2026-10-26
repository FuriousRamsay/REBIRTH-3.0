using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

// Read-only source prefab export for clay inspection. Never saves or alters assets.
public static class RebirthOriginalCamperClayExport
{
    [Serializable] class Part { public string path; public Vector3[] vertices; public Vector3[] normals; public int[] triangles; }
    [Serializable] class Survey { public string source; public List<Part> parts=new List<Part>(); }
    public static void Export()
    {
        const string source="Assets/Perfectvertex/80sCamperTruck/Prefabs/assembled_vehicle.prefab";
        const string folder="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/OriginalCamperClay_20261020";
        Directory.CreateDirectory(folder);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        try {
            var excluded=new HashSet<Renderer>();
            foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))
                foreach(var level in lod.GetLODs().Skip(1)) foreach(var r in level.renderers) if(r)excluded.Add(r);
            var survey=new Survey{source=source};
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(false)) {
                var r=f.GetComponent<Renderer>();
                if(!r||!r.enabled||excluded.Contains(r)||!f.sharedMesh)continue;
                var path=AnimationUtility.CalculateTransformPath(f.transform,root.transform);
                if(path.ToLowerInvariant().Contains("shadow_plane"))continue;
                var m=f.sharedMesh;
                survey.parts.Add(new Part{path=path,vertices=m.vertices.Select(v=>f.transform.TransformPoint(v)).ToArray(),normals=m.normals.Select(n=>f.transform.TransformDirection(n)).ToArray(),triangles=m.triangles});
            }
            File.WriteAllText(folder+"/original-camper.json",JsonUtility.ToJson(survey));
            File.WriteAllText(folder+"/source-manifest.txt",source+"\n"+string.Join("\n",survey.parts.Select(p=>p.path+" vertices="+p.vertices.Length+" triangles="+p.triangles.Length/3)));
            Debug.Log("ORIGINAL_CAMPER_EXPORT_COMPLETE parts="+survey.parts.Count);
        } finally {Object.DestroyImmediate(root);}
    }
}
