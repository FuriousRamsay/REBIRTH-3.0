using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

// Read-only source survey for reference camera/contour fitting. No bundle build.
public static class RebirthCamperReferenceSurvey
{
    [Serializable] public class MeshRow { public string name; public Vector3[] vertices; public int[] triangles; }
    [Serializable] public class Survey { public MeshRow[] meshes; }
    public static void Run(){Export(false);}
    public static void RunningGear(){Export(true);}
    public static void RearDoor()
    {
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHSeatPelvis_20261017/rebirthCamper80s.prefab"));
        try {
            var rows=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("RearDoor_"))
                .Select(f=>new MeshRow{name=f.name,vertices=f.sharedMesh.vertices.Select(p=>f.transform.TransformPoint(p)).ToArray(),triangles=f.sharedMesh.triangles}).ToArray();
            File.WriteAllText("E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017/rear-door.json",JsonUtility.ToJson(new Survey{meshes=rows}));
        } finally {Object.DestroyImmediate(root);}
    }
    static void Export(bool runningGear)
    {
        const string source="Assets/REBIRTHSeatPelvis_20261017/rebirthCamper80s.prefab";
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        try {
            var rows=root.GetComponentsInChildren<MeshFilter>(true)
                .Where(f=>runningGear?(f.name=="parts"||f.name.StartsWith("80sCamperTruck_wheel")):new[]{"body","camper_body","windows","headlights_glass","mirrors"}.Contains(f.name))
                .Select(f=>new MeshRow{name=f.name,vertices=f.sharedMesh.vertices.Select(p=>f.transform.TransformPoint(p)).ToArray(),triangles=f.sharedMesh.triangles}).ToArray();
            const string folder="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017";
            Directory.CreateDirectory(folder);
            File.WriteAllText(folder+(runningGear?"/running-gear.json":"/surfaces.json"),JsonUtility.ToJson(new Survey{meshes=rows}));
            Debug.Log("REFERENCE_SURVEY_COMPLETE "+rows.Length+" meshes; no assets modified");
        } finally {Object.DestroyImmediate(root);}
    }
}
