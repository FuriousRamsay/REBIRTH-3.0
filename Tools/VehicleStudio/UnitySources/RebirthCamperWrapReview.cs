using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Dedicated batch editor only. Review methods are transient; SaveSoftFinishCandidateBatch saves a new candidate.
public static class RebirthCamperWrapReview
{
    private static string cosmeticPreview;
    private static string reviewArtwork;
    public static void RunArtworkComparisonBatch()
    {
        try
        {
            foreach (int version in new[] { 1, 2 })
            {
                reviewArtwork = "Assets/REBIRTHCamperWrapGenerated/ArtworkCandidates/DiscoReference_v" + version + ".png";
                cosmeticPreview = "DiscoArtwork" + version;
                RunBatch();
            }
        }
        finally { reviewArtwork = null; cosmeticPreview = null; }
    }
    private static bool softerFinish;
    public static void SaveSoftFinishCandidateBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Dedicated batch editor required.");
        string sourcePath = Arg("-rebirthWrapPrefab", "");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (!source) throw new Exception("Missing source prefab.");
        string folder = "Assets/REBIRTHCamperWrapGenerated/FinishCandidates/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var root = UnityEngine.Object.Instantiate(source);
        try
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != "body" && renderer.name != "camper_body") continue;
                var material = new Material(renderer.sharedMaterial);
                if (!material.HasProperty("_DiscoSmoothness")) throw new Exception("Finish shader missing.");
                bool approvedFinish=Arg("-rebirthApprovedDiscoFinish", "0")=="1";
                material.SetColor("_DiscoCoat", approvedFinish?new Color(.80f,.64f,.88f,1):new Color(.76f,.53f,.87f,1));
                material.SetFloat("_DiscoMetallic", approvedFinish?.32f:.28f);
                material.SetFloat("_DiscoSmoothness", approvedFinish?.82f:.70f);
                material.SetFloat("_PatternEnabled", 0);
                string artworkPath = Arg("-rebirthWrapArtwork", "");
                if (!string.IsNullOrEmpty(artworkPath))
                {
                    var artwork = AssetDatabase.LoadAssetAtPath<Texture2D>(artworkPath);
                    if (!artwork) throw new Exception("Candidate artwork missing: " + artworkPath);
                    material.SetTexture("_Artwork", artwork);
                }
                string lowerOffset = Arg("-rebirthWrapLowerOffsetY", "");
                if (!string.IsNullOrEmpty(lowerOffset)) material.SetFloat("_LowerArtworkOffsetY", float.Parse(lowerOffset, System.Globalization.CultureInfo.InvariantCulture));
                string bandArtworkPath = Arg("-rebirthWrapBandArtwork", "");
                if (!string.IsNullOrEmpty(bandArtworkPath))
                {
                    var bandArtwork = AssetDatabase.LoadAssetAtPath<Texture2D>(bandArtworkPath);
                    if (!bandArtwork || !material.HasProperty("_BandArtwork")) throw new Exception("Candidate band artwork missing or unsupported: " + bandArtworkPath);
                    material.SetTexture("_BandArtwork", bandArtwork);
                    material.SetFloat("_UseBandArtwork", 1);
                    if (Arg("-rebirthWrapBandFullTexture", "0") == "1") material.SetVector("_BandVRange", new Vector4(0,1,0,0));
                    string bandRange = Arg("-rebirthWrapBandVRange", "");
                    if (!string.IsNullOrEmpty(bandRange))
                    {
                        string[] parts = bandRange.Split(',');
                        if (parts.Length != 2) throw new Exception("Band range requires min,max.");
                        float lo = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                        float hi = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                        if (!(lo >= 0 && hi <= 1 && lo < hi)) throw new Exception("Invalid band range.");
                        material.SetVector("_BandVRange", new Vector4(lo,hi,0,0));
                    }
                }
                AssetDatabase.CreateAsset(material, folder + "/" + renderer.name + ".mat");
                renderer.sharedMaterial = material;
            }
            string prefab = folder + "/rebirthCamper80s.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefab); AssetDatabase.SaveAssets();
            string output = Arg("-rebirthWrapReport", "E:/_Haven/shared/REBIRTHTemp/DiscoFinishCandidate");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "candidate.txt"), prefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    // Identical geometry, camera and light for both palettes. Preview only.
    public static void RunFinishComparisonBatch()
    {
        try
        {
            foreach (bool softer in new[] { false, true })
            {
                softerFinish = softer;
                cosmeticPreview = softer ? "DiscoSoft" : "Disco";
                RunBatch();
            }
        }
        finally { cosmeticPreview = null; softerFinish = false; }
    }
    public static void RunCosmeticStatesBatch()
    {
        try { foreach(string state in new[]{"None","Dye","Disco"}) { cosmeticPreview=state; RunBatch(); } }
        finally { cosmeticPreview=null; }
    }
    private static int lightingCase = -1;
    private static bool lighterGlossFinish;
    public static void RunLighterGlossBatch()
    {
        try { lighterGlossFinish=true; cosmeticPreview="DiscoLighterGloss"; RunBatch(); }
        finally { lighterGlossFinish=false; cosmeticPreview=null; }
    }
    private static float reviewSmoothness = -1;
    private static bool mirrorSideLight;
    public static void RunMirrorReflectionBatch()
    {
        try { mirrorSideLight = true; cosmeticPreview = "DiscoMirrorReflection"; RunBatch(); }
        finally { mirrorSideLight = false; cosmeticPreview = null; }
    }
    public static void RunSideReflectionBatch()
    {
        try
        {
            for (lightingCase = 3; lightingCase < 6; lightingCase++)
            {
                cosmeticPreview = "DiscoSideReflection" + lightingCase;
                RunBatch();
            }
        }
        finally { lightingCase = -1; cosmeticPreview = null; }
    }
    public static void RunGlossComparisonBatch()
    {
        try
        {
            lightingCase = 2;
            foreach (float value in new[] { .55f, .70f, .85f })
            {
                reviewSmoothness = value;
                cosmeticPreview = "DiscoGloss" + Mathf.RoundToInt(value * 100);
                RunBatch();
            }
        }
        finally { reviewSmoothness = -1; lightingCase = -1; cosmeticPreview = null; }
    }
    public static void RunLightingComparisonBatch()
    {
        try
        {
            for (lightingCase = 0; lightingCase < 3; lightingCase++)
            {
                cosmeticPreview = "DiscoLighting" + lightingCase;
                RunBatch();
            }
        }
        finally { lightingCase = -1; cosmeticPreview = null; }
    }
    [Serializable] public class MeshRecord
    {
        public string name, path, meshAsset, materialAsset;
        public Vector3[] positions, normals;
        public Vector2[] uv;
        public int[] triangles;
    }
    [Serializable] public class Export { public string coordinateSpace = "prefab root local"; public MeshRecord[] meshes; }
    [Serializable] public class Report
    {
        public string status = "FAIL", unityVersion, prefab, error, candidatePrefab;
        public string reviewedBundle, reviewedBundleSha256;
        public string scope = "Actual Unity camera renders; no game validation or asset installation.";
        public string[] images;
        public string bodyMaterial, camperMaterial, bodyTexture, camperTexture;
        public Vector3 boundsCenter, boundsSize;
        public bool nativeNoDyePreview;
        public Vector3 lightEuler;
        public float smoothnessOverride = -1;
        public float keyLightIntensity;
    }
    public static void RunBatch()
    {
        string output = Arg("-rebirthWrapReport", "E:/_Haven/shared/REBIRTHTemp/CamperWrapReview");
        if(cosmeticPreview!=null) output=Path.Combine(output,cosmeticPreview);
        var report = new Report { unityVersion = Application.unityVersion,
            prefab = Arg("-rebirthWrapPrefab", "Assets/REBIRTHCamperPlayableGenerated/d585e05647d34ed09701817fe951956a/rebirthCamper80s.prefab"),
            bodyMaterial = Arg("-rebirthWrapBodyMaterial", ""), camperMaterial = Arg("-rebirthWrapCamperMaterial", ""),
            bodyTexture = Arg("-rebirthWrapBodyTexture", ""), camperTexture = Arg("-rebirthWrapCamperTexture", "") };
        Directory.CreateDirectory(output);
        var transient = new List<UnityEngine.Object>();
        AssetBundle reviewBundle = null;
        try
        {
            if (!Application.isBatchMode || Application.isPlaying) throw new Exception("Dedicated batch editor required.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string bundlePath = Arg("-rebirthWrapReviewBundle", "");
            GameObject source;
            if (string.IsNullOrEmpty(bundlePath)) source = AssetDatabase.LoadAssetAtPath<GameObject>(report.prefab);
            else
            {
                reviewBundle = AssetBundle.LoadFromFile(bundlePath);
                if (!reviewBundle) throw new Exception("Cannot reopen review bundle: " + bundlePath);
                source = reviewBundle.LoadAsset<GameObject>("rebirthCamper80s");
                report.prefab = reviewBundle.GetAllAssetNames().Single(x => x.EndsWith("/rebirthcamper80s.prefab", StringComparison.OrdinalIgnoreCase));
                report.reviewedBundle = Path.GetFullPath(bundlePath);
                using (var stream = File.OpenRead(bundlePath))
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    report.reviewedBundleSha256 = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                report.scope += " Render source is reopened bundle: " + bundlePath;
            }
            if (!source) throw new Exception("Missing prefab: " + report.prefab);
            var model = UnityEngine.Object.Instantiate(source); transient.Add(model);
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var camera in model.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            var records = new List<MeshRecord>();
            foreach (string name in new[] { "body", "camper_body" })
            {
                var matches = model.GetComponentsInChildren<MeshFilter>(true).Where(x => x.name == name).ToArray();
                if (matches.Length != 1) throw new Exception("Expected exactly one mesh named " + name + "; found " + matches.Length);
                var filter = matches[0]; var mesh = filter.sharedMesh; var renderer = filter.GetComponent<MeshRenderer>();
                if (!mesh || !renderer || renderer.sharedMaterials.Length != 1) throw new Exception("Unexpected exterior mesh/material layout: " + name);
                var matrix = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var normalMatrix = matrix.inverse.transpose;
                records.Add(new MeshRecord { name = name, path = PathOf(filter.transform, model.transform), meshAsset = AssetDatabase.GetAssetPath(mesh),
                    materialAsset = AssetDatabase.GetAssetPath(renderer.sharedMaterial), uv = mesh.uv, triangles = mesh.triangles,
                    positions = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray(),
                    normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray() });
                string matPath = name == "body" ? report.bodyMaterial : report.camperMaterial;
                string texturePath = name == "body" ? report.bodyTexture : report.camperTexture;
                var original = string.IsNullOrEmpty(matPath) ? renderer.sharedMaterial : AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!original) throw new Exception("Missing preview material: " + matPath);
                var material = new Material(original); transient.Add(material); renderer.sharedMaterial = material;
                string artworkPath = reviewArtwork ?? Arg("-rebirthWrapArtwork", "");
                if (!string.IsNullOrEmpty(artworkPath))
                {
                    var artwork = AssetDatabase.LoadAssetAtPath<Texture2D>(artworkPath);
                    if (!artwork || !material.HasProperty("_Artwork")) throw new Exception("Review artwork missing or unsupported.");
                    material.SetTexture("_Artwork", artwork);
                    report.scope += " Artwork override: " + artworkPath;
                }
                string lowerOffset = Arg("-rebirthWrapLowerOffsetY", "");
                if (!string.IsNullOrEmpty(lowerOffset)) material.SetFloat("_LowerArtworkOffsetY", float.Parse(lowerOffset, System.Globalization.CultureInfo.InvariantCulture));
                string bandArtworkPath = Arg("-rebirthWrapBandArtwork", "");
                if (!string.IsNullOrEmpty(bandArtworkPath))
                {
                    var bandArtwork = AssetDatabase.LoadAssetAtPath<Texture2D>(bandArtworkPath);
                    if (!bandArtwork || !material.HasProperty("_BandArtwork")) throw new Exception("Independent band artwork missing or unsupported.");
                    material.SetTexture("_BandArtwork", bandArtwork);
                    material.SetFloat("_UseBandArtwork", 1);
                    if (Arg("-rebirthWrapBandFullTexture", "0") == "1") material.SetVector("_BandVRange", new Vector4(0,1,0,0));
                    string bandRange = Arg("-rebirthWrapBandVRange", "");
                    if (!string.IsNullOrEmpty(bandRange))
                    {
                        string[] parts = bandRange.Split(',');
                        if (parts.Length != 2) throw new Exception("Band range requires min,max.");
                        float lo = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                        float hi = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                        if (!(lo >= 0 && hi <= 1 && lo < hi)) throw new Exception("Invalid band range.");
                        material.SetVector("_BandVRange", new Vector4(lo,hi,0,0));
                    }
                    report.scope += " Independent band artwork: " + bandArtworkPath;
                }
                if (name == "body" && Arg("-rebirthWrapNoDye", "0") == "1")
                {
                    material.SetColor("_Color", new Color(255,255,255,1));
                    report.nativeNoDyePreview = true;
                }
                if (!string.IsNullOrEmpty(texturePath))
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (!texture) throw new Exception("Missing preview texture: " + texturePath);
                    if (!material.HasProperty("_MainTex")) throw new Exception("Preview shader lacks _MainTex.");
                    material.SetTexture("_MainTex", texture); material.SetColor("_Color", Color.white);
                }
            }
            if (Arg("-rebirthReferenceWrap", "0") == "1") report.candidatePrefab=RebirthCamperReferenceWrap.Apply(model);
            if(cosmeticPreview!=null)
            {
                foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(renderer.name!="body" && renderer.name!="camper_body") continue;
                    var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                    properties.SetFloat("_PatternEnabled",cosmeticPreview.StartsWith("Disco")?1:0);
                    if (softerFinish)
                    {
                        properties.SetColor("_DiscoCoat", new Color(.76f,.53f,.87f,1));
                        properties.SetFloat("_DiscoMetallic", .28f);
                        properties.SetFloat("_DiscoSmoothness", .70f);
                    }
                    if (reviewSmoothness >= 0)
                    {
                        properties.SetFloat("_DiscoSmoothness", reviewSmoothness);
                        report.smoothnessOverride = reviewSmoothness;
                    }
                    if (lighterGlossFinish)
                    {
                        properties.SetColor("_DiscoCoat", new Color(.80f,.64f,.88f,1));
                        properties.SetFloat("_DiscoMetallic", .32f);
                        properties.SetFloat("_DiscoSmoothness", .82f);
                    }
                    // Exact installed VehiclePart.SetColors path uses Material.color.
                    // The review cloned this material above, so the asset is untouched.
                    if(renderer.name=="body") renderer.sharedMaterial.color=cosmeticPreview=="None"?new Color(255,255,255,1):new Color(32,140,32,1);
                    renderer.SetPropertyBlock(properties);
                    Debug.Log("WRAP_TINT " + cosmeticPreview + " " + renderer.name + " material=" + renderer.sharedMaterial.GetVector("_Color") + " block=" + properties.GetVector("_Color") + " neutral=" + AssetDatabase.GetAssetPath(renderer.sharedMaterial.GetTexture("_NeutralTex")));
                }
            }
            File.WriteAllText(Path.Combine(output, "exterior_uv.json"), JsonUtility.ToJson(new Export { meshes = records.ToArray() }));
            var visible = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (visible.Length == 0) throw new Exception("No visible renderers.");
            var bounds = visible[0].bounds; foreach (var renderer in visible.Skip(1)) bounds.Encapsulate(renderer.bounds);
            report.boundsCenter = bounds.center; report.boundsSize = bounds.size;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.62f, .64f, .70f);
            RenderSettings.fog = false;
            var sun = new GameObject("Review key light"); transient.Add(sun);
            var key = sun.AddComponent<Light>(); key.type = LightType.Directional;
            report.keyLightIntensity = float.Parse(Arg("-rebirthWrapLightIntensity", "1.1"), System.Globalization.CultureInfo.InvariantCulture);
            if (report.keyLightIntensity < 0 || float.IsNaN(report.keyLightIntensity) || float.IsInfinity(report.keyLightIntensity))
                throw new Exception("Invalid review light intensity.");
            key.intensity = report.keyLightIntensity;
            report.lightEuler = lightingCase < 0 ? new Vector3(35,140,0) :
                new[] { new Vector3(35,140,0), new Vector3(25,250,0), new Vector3(55,290,0),
                    new Vector3(8,114,0), new Vector3(20,114,0), new Vector3(35,114,0) }[lightingCase];
            sun.transform.rotation = Quaternion.Euler(report.lightEuler);
            if (mirrorSideLight)
            {
                // Reflect the direction to the three-quarter camera around the
                // outward left-panel normal, then negate for light travel.
                var view = new Vector3(-1, .16f, .45f).normalized;
                var normal = Vector3.left;
                var towardLight = 2 * Vector3.Dot(normal, view) * normal - view;
                sun.transform.rotation = Quaternion.LookRotation(-towardLight, Vector3.up);
                report.lightEuler = sun.transform.eulerAngles;
            }
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); transient.Add(ground);
            ground.transform.position = new Vector3(bounds.center.x, bounds.min.y - .03f, bounds.center.z);
            ground.transform.localScale = Vector3.one * 5;
            var groundMat = new Material(Shader.Find("Standard")); transient.Add(groundMat);
            groundMat.color = new Color(.19f, .21f, .22f); groundMat.SetFloat("_Glossiness", .1f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            if (Arg("-rebirthWrapSkyReflection", "0") == "1")
            {
                var sky = new Material(Shader.Find("Skybox/Procedural")); transient.Add(sky);
                sky.SetFloat("_SunSize", .04f);
                sky.SetFloat("_AtmosphereThickness", 1f);
                sky.SetColor("_SkyTint", new Color(.5f,.5f,.5f));
                sky.SetColor("_GroundColor", new Color(.25f,.27f,.24f));
                sky.SetFloat("_Exposure", 1.2f);
                RenderSettings.skybox = sky; RenderSettings.sun = key;
                var capture = new GameObject("Sky reflection capture"); transient.Add(capture);
                var probe = capture.AddComponent<ReflectionProbe>();
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
                probe.resolution = 128; probe.cullingMask = 0;
                probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
                string probeFolder = "Assets/REBIRTHCamperWrapGenerated/ReviewReflections";
                Directory.CreateDirectory(probeFolder);
                string probePath = probeFolder + "/" + Guid.NewGuid().ToString("N") + ".exr";
                if (!Lightmapping.BakeReflectionProbe(probe, probePath)) throw new Exception("Reflection bake failed.");
                AssetDatabase.ImportAsset(probePath, ImportAssetOptions.ForceSynchronousImport);
                var environment = AssetDatabase.LoadAssetAtPath<Cubemap>(probePath);
                if (!environment) throw new Exception("Baked reflection cubemap missing.");
                probe.enabled = false;
                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = environment;
                RenderSettings.reflectionIntensity = 1f;
            }
            report.images = new[] { "side.png", "reference_three_quarter.png", "rear.png" };
            Render(bounds, new Vector3(-1, .05f, 0), true, Path.Combine(output, report.images[0]));
            Render(bounds, new Vector3(-1, .16f, .45f), false, Path.Combine(output, report.images[1]));
            Render(bounds, new Vector3(.32f, .18f, -1), false, Path.Combine(output, report.images[2]));
            if (Arg("-rebirthRoofView", "false") == "true")
                Render(bounds, new Vector3(-.65f, 1.2f, .6f), false, Path.Combine(output, "roof_angle.png"));
            report.status = "PASS_UNITY_RENDERED_NOT_GAME_TESTED";
        }
        catch (Exception e) { report.error = e.ToString(); }
        finally
        {
            foreach (var item in transient.AsEnumerable().Reverse()) if (item) UnityEngine.Object.DestroyImmediate(item);
            if (reviewBundle) reviewBundle.Unload(true);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
        }
        if (report.error != null) throw new Exception("Wrap review failed; inspect " + output + "/result.json");
    }
    static void Render(Bounds bounds, Vector3 direction, bool orthographic, string output)
    {
        var go = new GameObject("Review camera"); var camera = go.AddComponent<Camera>();
        var target = new RenderTexture(1920, 1200, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(1920, 1200, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.backgroundColor = new Color(.11f, .13f, .16f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = .05f; camera.farClipPlane = 200; camera.fieldOfView = 32;
            camera.orthographic = orthographic; camera.orthographicSize = Mathf.Max(bounds.size.y * .66f, Mathf.Max(bounds.size.x, bounds.size.z) / 1.6f * .61f);
            float radius = bounds.extents.magnitude;
            go.transform.position = bounds.center + direction.normalized * radius * 3.0f;
            go.transform.LookAt(bounds.center); camera.targetTexture = target;
            camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1920, 1200), 0, 0); pixels.Apply();
            File.WriteAllBytes(output, pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null; target.Release();
            UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(go);
        }
    }
    static string PathOf(Transform t, Transform root)
    {
        var names = new List<string>(); while (t != root && t) { names.Add(t.name); t = t.parent; }
        names.Reverse(); return string.Join("/", names.ToArray());
    }
    static string Arg(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }
}
