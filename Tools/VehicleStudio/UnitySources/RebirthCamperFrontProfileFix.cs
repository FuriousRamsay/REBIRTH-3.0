using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Additive geometry-only candidate. Never writes installed Resources or originals.
public static class RebirthCamperFrontProfileFix
{
    const string Input = "Assets/REBIRTHCamperPlate_20261015/rebirthCamper80s.prefab";
    const string Folder = "Assets/REBIRTHCamperFrontProfile_20261015_v7";
    const string Output = "E:/_Haven/shared/REBIRTHTemp/CamperFrontProfile_20261015_v7";
    const float Cut = 2.42f, Rise = .40f;
    // Least-squares fit to 28 unique points of the original front perimeter
    // strip at abs(X)=.8767, Y1.8..2.37. Adjacent strip independently agrees.
    const float Slope = .6292387f;
    static float Weight(float z) { float t = Mathf.Clamp01(z + 1); return t * t * (3 - 2 * t); }
    static float WeightDerivative(float z) { float t = z + 1; return t <= 0 || t >= 1 ? 0 : 6 * t * (1 - t); }
    static Vector3 Correct(Vector3 p) { p.z += Slope * Mathf.Clamp(p.y - Cut, 0, Rise) * Weight(p.z); return p; }
    static Vector3 FollowSurface(Vector3 p, MeshCollider collider, MeshFilter shell)
    {
        // Texture-drawn housings follow the shell triangle's interpolated
        // displacement, not the nonlinear field sampled at a floating lens.
        // Barycentric transfer keeps separate lenses attached to that surface.
        if ((Correct(p)-p).sqrMagnitude < 1e-14f) return p;
        RaycastHit nearest = default(RaycastHit); float distance = .12f; bool found = false;
        foreach (var direction in new[] {Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
        {
            if (!collider.Raycast(new Ray(p-direction*.20f,direction),out var hit,.40f)) continue;
            float d=Vector3.Distance(p,hit.point); if(d>=distance)continue;
            distance=d; nearest=hit; found=true;
        }
        if (!found) throw new Exception("Marker cannot be bound to source housing at " + p);
        var mesh=shell.sharedMesh; var indices=mesh.triangles; var vertices=mesh.vertices; int start=nearest.triangleIndex*3;
        var a=shell.transform.TransformPoint(vertices[indices[start]]);var b=shell.transform.TransformPoint(vertices[indices[start+1]]);var c=shell.transform.TransformPoint(vertices[indices[start+2]]);
        var weights=nearest.barycentricCoordinate;
        return p+(Correct(a)-a)*weights.x+(Correct(b)-b)*weights.y+(Correct(c)-c)*weights.z;
    }
    static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
    static string Structure(GameObject root)
    {
        return string.Join("\n", root.GetComponentsInChildren<Transform>(true).Select(t =>
            t.name + "|" + (t.GetComponent<Light>() ? "lamp-position" : t.localPosition.ToString("R")) + "|" + t.localRotation.ToString("R") + "|" + t.localScale.ToString("R") + "|" + t.gameObject.activeSelf)) +
            "\ncolliders=" + root.GetComponentsInChildren<Collider>(true).Length;
    }
    public static void Run()
    {
        if (File.Exists(Output + "/verified.txt")) throw new Exception("Preserve completed front profile candidate.");
        Directory.CreateDirectory(Folder); Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string originalHash = Hash(Input);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Input);
        if (!prefab) throw new Exception("Latest source prefab missing.");
        var before = Object.Instantiate(prefab); before.name = "rebirthCamper80s";
        var after = Object.Instantiate(prefab); after.name = "rebirthCamper80s";
        string structure = Structure(after);
        var originalShell=before.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="camper_body");
        var projection=originalShell.gameObject.AddComponent<MeshCollider>();projection.sharedMesh=originalShell.sharedMesh;Physics.SyncTransforms();
        var log = new List<string>(); int index = 0, changed = 0;
        foreach (var filter in after.GetComponentsInChildren<MeshFilter>(true))
        {
            bool shell = filter.name == "camper_body" || filter.name == "camper_interior";
            bool rail = filter.GetComponentsInParent<Transform>(true).Any(t => t.name == "Fitted rooftop guardrails");
            bool marker = filter.name == "BodyMarkerLenses" || filter.name == "lights" || filter.name == "parts";
            if (!shell && !rail && !marker) continue;
            Mesh mesh;
            if(filter.name=="camper_body"){int panels;mesh=RebirthCamperSidingContinuation.Build(filter.sharedMesh,filter.transform,out panels);log.Add("Physical siding: original flat faces="+panels+" new triangles="+mesh.triangles.Length/3);}
            else mesh = Object.Instantiate(filter.sharedMesh);
            var vertices = mesh.vertices; var normals = mesh.normals; int count = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = filter.transform.TransformPoint(vertices[i]);
                Vector3 q = filter.name=="BodyMarkerLenses" || filter.name=="lights" ? FollowSurface(p,projection,originalShell) : Correct(p);
                if ((q - p).sqrMagnitude < 1e-14f) continue;
                vertices[i] = filter.transform.InverseTransformPoint(q); count++;
                if (normals.Length == vertices.Length)
                {
                    var n = filter.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(normals[i]).normalized;
                    float a = p.y > Cut && p.y < Cut + Rise ? Slope * Weight(p.z) : 0;
                    float b = 1 + Slope * Mathf.Clamp(p.y - Cut, 0, Rise) * WeightDerivative(p.z);
                    n = new Vector3(n.x, n.y - a * n.z / b, n.z / b).normalized;
                    normals[i] = filter.transform.localToWorldMatrix.transpose.MultiplyVector(n).normalized;
                }
            }
            if (count == 0) { Object.DestroyImmediate(mesh); continue; }
            mesh.name = filter.sharedMesh.name + "_FrontProfile";
            mesh.vertices = vertices; if (normals.Length == vertices.Length) mesh.normals = normals;
            if(filter.name=="BodyMarkerLenses" || filter.name=="lights")mesh.RecalculateNormals();
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, Folder + "/Mesh_" + index++ + ".asset"); filter.sharedMesh = mesh;
            log.Add(filter.name + ": corrected vertices=" + count); changed += count;
        }
        foreach (var light in after.GetComponentsInChildren<Light>(true))
        {
            var position = light.transform.position; var corrected = FollowSurface(position,projection,originalShell);
            if (position != corrected) { light.transform.position = corrected; log.Add(light.name + ": lamp follows housing"); }
        }
        Object.DestroyImmediate(projection);
        if (changed == 0 || Structure(after) != structure) throw new Exception("Geometry correction or preservation check failed.");
        // Deterministic control section: slope through the added front band is
        // identical to the measured panel slope; lower/rear geometry is fixed.
        Vector3 seam = new Vector3(.88f, Cut, .69f), top = seam + Vector3.up * Rise;
        float measured = (Correct(top).z - Correct(seam).z) / Rise;
        if (Mathf.Abs(measured - Slope) > 1e-5f || Correct(seam) != seam || Correct(new Vector3(.88f, 3, -2)) != new Vector3(.88f, 3, -2))
            throw new Exception("Profile continuation invariants failed.");
        var bodyRenderer=after.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="camper_body");
        var alignedMaterial=new Material(bodyRenderer.sharedMaterial);alignedMaterial.shader=Shader.Find("REBIRTH/CamperAlignedStripe");
        if(!alignedMaterial.shader)throw new Exception("Aligned stripe shader missing");
        AssetDatabase.CreateAsset(alignedMaterial,Folder+"/CamperAlignedStripe.mat");bodyRenderer.sharedMaterial=alignedMaterial;
        log.Add("Native upper stripe: source-atlas red removed on upper painted wall, common 2.23..2.46m height band across side/front/rear. Disco artwork unchanged.");
        PrefabUtility.SaveAsPrefabAsset(after, Folder + "/rebirthCamper80s.prefab"); AssetDatabase.SaveAssets();
        if (Hash(Input) != originalHash) throw new Exception("Original prefab changed.");
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.65f,.65f,.65f);
        var sun = new GameObject("Profile key").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.1f; sun.transform.rotation = Quaternion.Euler(40,-35,0);
        var fill = new GameObject("Profile fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .45f; fill.transform.rotation = Quaternion.Euler(25,145,0);
        var camera = new GameObject("Profile review").AddComponent<Camera>(); camera.orthographic = true; camera.nearClipPlane = .01f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f,.14f,.16f);
        foreach (bool fixedModel in new[] { false, true })
        {
            before.SetActive(!fixedModel); after.SetActive(fixedModel); var root = fixedModel ? after : before;
            string label = fixedModel ? "after" : "before";
            Render(camera, new Vector3(-7,2.4f,-.6f), new Vector3(0,2.4f,-.6f), 1.8f, label + "-side");
            Render(camera, new Vector3(-5,3.4f,5), new Vector3(0,2,-.6f), 2.9f, label + "-front-quarter");
            Render(camera, new Vector3(5,3.4f,5), new Vector3(0,2,-.6f), 2.9f, label + "-opposite-quarter");
            Render(camera, new Vector3(-4,2.65f,2.5f), new Vector3(0,2.5f,.3f), .85f, label + "-corner-close");
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "camper_body" || r.name == "body"))
            { var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); block.SetFloat("_PatternEnabled",1); renderer.SetPropertyBlock(block); }
            Render(camera, new Vector3(-5,3.4f,5), new Vector3(0,2,-.6f), 2.9f, label + "-disco-quarter");
        }
        log.Add("Source=" + Input + " originalSHA256=" + originalHash);
        log.Add("Cut2.42m; added height.40m; original front slope=" + Slope + "; front roof advance=" + Slope * Rise + "m.");
        log.Add("PASS lower seam/rear fixed; structure/transform states/collider count unchanged; changed meshes cloned. Materials retained; inserted siding re-triangulated with measured geometric grooves and adjusted atlas repeat. No installed asset/game changes. Unity candidate only.");
        File.WriteAllLines(Output + "/verified.txt", log);
    }
    public static void Build()
    {
        if (!File.Exists(Output+"/verified.txt") || File.Exists(Output+"/bundle-verified.txt")) throw new Exception("Source review missing or bundle already verified.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Directory.CreateDirectory(Output+"/Bundle");
        string asset=Folder+"/rebirthCamper80s.prefab";
        var manifest=BuildPipeline.BuildAssetBundles(Output+"/Bundle",new[]{new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{asset}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        if(!manifest)throw new Exception("Candidate bundle build failed.");
        VerifyExisting();
    }
    public static void VerifyExisting()
    {
        string asset=Folder+"/rebirthCamper80s.prefab";
        var bundle=AssetBundle.LoadFromFile(Output+"/Bundle/rebirth_camper80s.unity3d");if(!bundle)throw new Exception("Cannot reopen candidate bundle.");
        try
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(asset);var root=Object.Instantiate(bundle.LoadAsset<GameObject>("rebirthCamper80s"));root.name=source.name;
            if(Structure(root)!=Structure(source))throw new Exception("Bundled structure differs from source.");
            int targets=root.GetComponentsInChildren<Collider>(true).Count(c=>c.name.StartsWith("REBIRTHInteraction_"));
            if(targets!=21)throw new Exception("Interaction targets changed: "+targets);
            var sourceFilters=source.GetComponentsInChildren<MeshFilter>(true);var actualFilters=root.GetComponentsInChildren<MeshFilter>(true);
            if(sourceFilters.Length!=actualFilters.Length)throw new Exception("Mesh count changed.");
            for(int i=0;i<sourceFilters.Length;i++)
            {
                var sm=sourceFilters[i].sharedMesh;var am=actualFilters[i].sharedMesh;
                if(sm.vertexCount!=am.vertexCount||sm.subMeshCount!=am.subMeshCount||(sm.bounds.center-am.bounds.center).sqrMagnitude>1e-10f||(sm.bounds.size-am.bounds.size).sqrMagnitude>1e-10f)throw new Exception("Mesh header mismatch: "+sourceFilters[i].name);
                for(int sub=0;sub<sm.subMeshCount;sub++)if(sm.GetIndexCount(sub)!=am.GetIndexCount(sub))throw new Exception("Index count mismatch: "+sourceFilters[i].name);
                if(sm.isReadable&&am.isReadable){var expected=sm.vertices;var actual=am.vertices;for(int j=0;j<expected.Length;j++)if((expected[j]-actual[j]).sqrMagnitude>1e-12f)throw new Exception("Serialized vertex mismatch: "+sourceFilters[i].name);}
                else Debug.Log("HEADER-ONLY geometry verification (unreadable mesh): "+sourceFilters[i].name);
            }
            var rail=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Fitted rooftop guardrails");
            if(!rail.gameObject.activeInHierarchy)throw new Exception("Permanent guardrails hidden.");
            if(root.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="shadow_plane").enabled)throw new Exception("Shadow plane unexpectedly enabled.");
            RebirthCamperCampIntegration.SetMode(root,true);Physics.SyncTransforms();
            string entry=RebirthCamperDefectAudit.Walk(root,1.8f,.1f);if(!entry.Contains("entry=True"))throw new Exception(entry);
            RebirthCamperCampIntegration.SetMode(root,false);
            File.WriteAllText(Output+"/bundle-verified.txt","PASS reopened source structure, all mesh headers/index counts, vertex identity on readable meshes (unreadable meshes header-only);21interaction targets; permanent rails; shadow plane disabled; static Unity standing entry.\n"+entry+"\nSHA256="+Hash(Output+"/Bundle/rebirth_camper80s.unity3d")+"\nNot installed; no actual in-game validation.");
        }
        finally{bundle.Unload(true);}
    }
    static Vector3[] ReadVertices(Mesh mesh)
    {
        using(var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(mesh))
        using(var vertices=new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount,Unity.Collections.Allocator.Temp))
        {data[0].GetVertices(vertices);return vertices.ToArray();}
    }
    static void Render(Camera camera, Vector3 eye, Vector3 target, float size, string name)
    {
        camera.transform.position = eye; camera.transform.LookAt(target); camera.orthographicSize = size;
        var rt = new RenderTexture(1280,960,24); var image = new Texture2D(1280,960,TextureFormat.RGB24,false);
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0,0,1280,960),0,0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png",image.EncodeToPNG()); }
        finally { camera.targetTexture = null; RenderTexture.active = null; Object.DestroyImmediate(image); Object.DestroyImmediate(rt); }
    }
}




