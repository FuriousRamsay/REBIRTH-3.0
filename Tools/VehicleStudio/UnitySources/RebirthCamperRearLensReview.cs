using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Read-only geometry/UV audit. Does not save meshes, materials, prefabs or bundles.
public static class RebirthCamperRearLensReview
{
    [Serializable] public class Triangle
    {
        public int submesh, index, redSamples;
        public Vector3 a, b, c, center;
        public Vector2 uvA, uvB, uvC;
        public bool rearCandidate;
    }
    [Serializable] public class Result
    {
        public string kind = "CAMPER_REAR_LENS_GEOMETRY_REVIEW", status = "FAIL", error;
        public string rendererPath, texturePath;
        public int totalTriangles, redTriangles, rearTriangles, leftTriangles, rightTriangles;
        public Vector3 rearMin, rearMax;
        public Triangle[] triangles;
        public bool lampAcceptance = false;
    }

    public static void RunBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        string folder = args[Array.IndexOf(args, "-camperLensReport") + 1];
        Directory.CreateDirectory(folder);
        var result = new Result();
        GameObject root = null;
        Texture2D texture = null;
        try
        {
            if (Application.unityVersion != "2022.3.62f2") throw new Exception("Exact Unity version required.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperPlayableGenerated/8bfe6792d86a4397966e109c4ba17924/rebirthCamper80s.prefab");
            if (prefab == null) throw new Exception("Preserved playable candidate missing.");
            root = UnityEngine.Object.Instantiate(prefab);
            var renderer = root.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "lights");
            result.rendererPath = AnimationUtility.CalculateTransformPath(renderer.transform, root.transform);
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null) throw new Exception("Lamp mesh missing.");
            var vertices = mesh.vertices;
            var uv = mesh.uv;
            if (uv.Length != vertices.Length) throw new Exception("Lamp UV dimensions disagree.");
            var red = new List<Triangle>();
            var rear = new List<Vector3>();
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var material = renderer.sharedMaterials[submesh];
                if (material == null || material.name != "80sCamperTruck_lights_glass")
                    throw new Exception("Unexpected lamp material; refuse guessed atlas selection.");
                result.texturePath = AssetDatabase.GetAssetPath(material.mainTexture);
                if (string.IsNullOrEmpty(result.texturePath)) throw new Exception("Source lamp atlas missing.");
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(File.ReadAllBytes(result.texturePath))) throw new Exception("Lamp atlas cannot be decoded.");
                texture.wrapMode = TextureWrapMode.Repeat;
                int[] indices = mesh.GetTriangles(submesh);
                result.totalTriangles += indices.Length / 3;
                for (int offset = 0; offset < indices.Length; offset += 3)
                {
                    int ia = indices[offset], ib = indices[offset + 1], ic = indices[offset + 2];
                    Vector2 ua = uv[ia], ub = uv[ib], uc = uv[ic];
                    Vector2[] samples = { (ua+ub+uc)/3, ua*.6f+ub*.2f+uc*.2f,
                        ua*.2f+ub*.6f+uc*.2f, ua*.2f+ub*.2f+uc*.6f };
                    int votes = samples.Count(p => IsRed(texture.GetPixelBilinear(p.x,p.y)));
                    if (votes < 3) continue;
                    Vector3 a = root.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertices[ia]));
                    Vector3 b = root.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertices[ib]));
                    Vector3 c = root.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertices[ic]));
                    Vector3 center = (a+b+c)/3;
                    // Rear-lens proposal only. Export every red triangle, including those
                    // outside this region, so a reviewer can reject this classification.
                    bool candidate = center.z < -2.7f && center.y < 1.1f && Mathf.Abs(center.x) > .6f;
                    red.Add(new Triangle { submesh=submesh, index=offset/3, redSamples=votes,
                        a=a,b=b,c=c,center=center,uvA=ua,uvB=ub,uvC=uc,rearCandidate=candidate });
                    if (candidate)
                    {
                        rear.Add(a); rear.Add(b); rear.Add(c);
                        if (center.x < 0) result.leftTriangles++; else result.rightTriangles++;
                    }
                }
                UnityEngine.Object.DestroyImmediate(texture); texture = null;
            }
            result.redTriangles = red.Count;
            result.rearTriangles = rear.Count/3;
            result.triangles = red.ToArray();
            if (rear.Count == 0 || result.leftTriangles == 0 || result.rightTriangles == 0)
                throw new Exception("No measured red lens candidates on both rear sides.");
            result.rearMin = new Vector3(rear.Min(p=>p.x),rear.Min(p=>p.y),rear.Min(p=>p.z));
            result.rearMax = new Vector3(rear.Max(p=>p.x),rear.Max(p=>p.y),rear.Max(p=>p.z));
            result.status = "PASS_GEOMETRY_PROPOSAL_NOT_LIGHTING_ACCEPTANCE";
        }
        catch (Exception error) { result.error=error.ToString(); throw; }
        finally
        {
            File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(result,true));
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }
    private static bool IsRed(Color color)
    {
        // Measured amber atlas samples have G/R~.276; red lens samples ~.002.
        return color.r > .16f && color.g < color.r*.15f && color.b < color.r*.15f;
    }
}
