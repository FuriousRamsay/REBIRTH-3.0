using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

// Installs only the approved additions into the functional vehicle rig. Never
// round-trip native meshes through the modelling interchange file.
public static class RebirthApprovedPickupInstall
{
    const string Source = "Assets/REBIRTHBareTruckBodywork_20261020_v3/rebirthCamper80s.prefab";
    const string Folder = "Assets/REBIRTHApprovedPickupProduction_20261020";
    const string Output = "E:/_Haven/shared/REBIRTHStaged/VehicleStudio/ApprovedPickupInstall_20261020";
    const string Data = "E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TruckBodyworkRearDetails_20261020/textured-truck-meshes.json";
    const string SurveyPath = "E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TruckBodyworkTextured_20261020/source-materials.json";
    const string VisualPath = "GameObject/M/VisualBody";
    const string BodyPath = VisualPath + "/80sCamperTruck/body";
    static readonly List<string> report = new List<string>();
    static Material body;
    [Serializable] public class Scenario { public bool[] installedStates; }
    static Transform Group(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
    static T Save<T>(T item, string name) where T : Object
    {
        string path = Folder + "/" + name;
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing) { EditorUtility.CopySerialized(item, existing); Object.DestroyImmediate(item); return existing; }
        AssetDatabase.CreateAsset(item, path); return item;
    }
    static string PathOf(Transform t, Transform root) { var p = new List<string>(); while (t && t != root) { p.Add(t.name); t = t.parent; } p.Reverse(); return string.Join("/", p); }
    static string[] PhysicsSignature(GameObject root) => root.GetComponentsInChildren<Collider>(true).Select(c =>
        PathOf(c.transform, root.transform) + "|" + c.GetType().Name + "|" + EditorJsonUtility.ToJson(c)).OrderBy(x => x).ToArray();
    static GameObject Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name).gameObject;
    static void State(GameObject root, bool camper)
    {
        RebirthCamperBodyVisibility.Apply(camper, Find(root, "REBIRTHCamperBodyModule"),
            Find(root, RebirthCamperBodyVisibility.PickupBodywork), Find(root, RebirthCamperBodyVisibility.PickupPaint));
    }
    public static void Build()
    {
        Directory.CreateDirectory(Output); Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tagManager.FindProperty("tags");
        if (!Enumerable.Range(0, tags.arraySize).Any(i => tags.GetArrayElementAtIndex(i).stringValue == "LOD"))
        {
            tags.InsertArrayElementAtIndex(tags.arraySize); tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = "LOD";
            tagManager.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        var root = Object.Instantiate(original); root.name = "rebirthCamper80s";
        var beforePhysics = PhysicsSignature(root);
        foreach (string name in new[] { "REBIRTHPickupOnlyBodywork", "REBIRTHTruckCommonChassis" })
            Object.DestroyImmediate(Find(root, name));
        var visual = root.transform.Find(VisualPath);
        var camper = Find(root, "REBIRTHCamperBodyModule").transform;
        var bodyTransform = root.transform.Find(BodyPath);
        var pickup = Group(visual, RebirthCamperBodyVisibility.PickupBodywork);
        var paint = Group(bodyTransform, RebirthCamperBodyVisibility.PickupPaint);
        var data = JsonUtility.FromJson<RebirthApprovedTruckTextures.MeshData>(File.ReadAllText(Data));
        var survey = JsonUtility.FromJson<RebirthApprovedTruckTextures.Survey>(File.ReadAllText(SurveyPath));
        var materials = new Dictionary<string, Material>();
        foreach (string key in data.parts.Select(p => p.material).Distinct())
        {
            string asset = survey.materials.First(e => e.part.Split('/').Last() == key).asset;
            var material = AssetDatabase.LoadAssetAtPath<Material>(asset);
            if (!material) throw new Exception("Missing original material: " + asset);
            if (key == "body")
            {
                material = new Material(material) { name = "Original truck finish" };
                material.shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/RebirthPickupOriginalFinish.shader");
                if (!material.shader) throw new Exception("Missing approved shader");
                material.color = new Color(255, 255, 255, 1); material.SetFloat("_PatternEnabled", 0);
                material = Save(material, "OriginalTruckFinish.mat"); body = material;
            }
            materials[key] = material;
        }
        // Existing native LODs use the same body shader and textures at every distance.
        foreach (var renderer in bodyTransform.GetComponentsInChildren<Renderer>(true))
        { renderer.sharedMaterial = body; if (renderer.transform != bodyTransform) renderer.tag = "LOD"; }
        RepairSharedLods(root, camper);
        SplitPlates(root, camper);
        int serial = 0;
        // Owner correction: the load floor stays dark and does not receive dye.
        // Keep its original surface maps, with an independent fixed finish.
        // Use a fixed black surface, not the vehicle paint shader: the latter
        // applies its atlas mask and can leave parts of this floor pale.
        var floorMaterial = new Material(Shader.Find("Standard")) { name = "Dark undyed bed floor", color = new Color(.08f, .08f, .08f, 1) };
        floorMaterial.mainTexture = body.GetTexture("_NeutralTex");
        floorMaterial.SetTexture("_BumpMap", body.GetTexture("_BumpMap"));
        floorMaterial.SetFloat("_BumpScale", body.GetFloat("_BumpScale"));
        floorMaterial.EnableKeyword("_NORMALMAP");
        floorMaterial.SetTexture("_OcclusionMap", body.GetTexture("_OcclusionMap"));
        floorMaterial.SetFloat("_OcclusionStrength", body.GetFloat("_OcclusionStrength"));
        floorMaterial.SetFloat("_Metallic", 0); floorMaterial.SetFloat("_Glossiness", .25f);
        floorMaterial = Save(floorMaterial, "DarkUndyedBedFloor.mat");
        foreach (var part in data.parts.Where(p => p.group != "NativeTruckReference"))
        {
            if (part.group != "PickupOnlyInfill") throw new Exception("Unexpected non-pickup addition: " + part.name);
            var mesh = new Mesh { name = part.name, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = part.vertices; mesh.normals = part.normals; mesh.uv = part.uv;
            // The export (x, z, -y) is a rotation with positive determinant.
            // Its winding already agrees with the original Unity meshes. Reversing
            // it exposes the inside of the bumper and stamped bed panels.
            mesh.triangles = part.triangles; mesh.RecalculateBounds(); mesh.RecalculateTangents();
            mesh = Save(mesh, "addition_" + serial++ + ".asset");
            var go = new GameObject(part.name); go.layer = bodyTransform.gameObject.layer;
            bool floor = part.name == "Continuous pressed load floor - native tub cutouts";
            bool dyed = part.material == "body" && !floor;
            // Export coordinates are world-space. Parent after establishing that pose.
            go.transform.SetParent(dyed ? paint : pickup, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = floor ? floorMaterial : materials[part.material];
            if (dyed) go.tag = "LOD";
        }
        CloseBedCorners(paint);
        if (!beforePhysics.SequenceEqual(PhysicsSignature(root))) throw new Exception("Native collider configuration changed");
        if (root.GetComponentsInChildren<WheelCollider>(true).Length != 4) throw new Exception("Native wheel colliders missing");
        foreach (var entry in survey.materials)
        {
            var a = original.transform.Find(entry.part); var b = root.transform.Find(entry.part);
            if (!a || !b) throw new Exception("Native part missing: " + entry.part);
            if (a.localPosition != b.localPosition || a.localRotation != b.localRotation || a.localScale != b.localScale)
                throw new Exception("Native transform changed: " + entry.part);
            if (a.name != "licence_plate" && a.GetComponent<MeshFilter>().sharedMesh != b.GetComponent<MeshFilter>().sharedMesh)
                throw new Exception("Native mesh replaced: " + entry.part);
            if (a.name != "body" && a.GetComponent<Renderer>().sharedMaterial != b.GetComponent<Renderer>().sharedMaterial)
                throw new Exception("Native material replaced: " + entry.part);
        }
        report.Add("PASS native geometry/UV references, transforms, non-paint materials and collider configuration retained.");
        report.Add("Approved pickup additions=" + serial + "; source=" + Data);
        report.Add("Two painted front bed corner closures cover the measured 11 mm lateral / 28 mm longitudinal panel gaps.");
        State(root, false);
        string prefabPath = Folder + "/rebirthCamper80s.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath); AssetDatabase.SaveAssets(); Object.DestroyImmediate(root);
        AssetDatabase.ExportPackage(AssetDatabase.GetDependencies(prefabPath, true).Where(p => p.StartsWith(Folder + "/")).ToArray(),
            Output + "/approved-pickup-production.unitypackage", ExportPackageOptions.Default);
        string bundleDir = Output + "/Bundle"; Directory.CreateDirectory(bundleDir);
        var build = new AssetBundleBuild { assetBundleName = "rebirth_camper80s.unity3d", assetNames = new[] { prefabPath }, addressableNames = new[] { "rebirthCamper80s" } };
        if (!BuildPipeline.BuildAssetBundles(bundleDir, new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64))
            throw new Exception("Asset bundle build failed");
        string bundlePath = bundleDir + "/rebirth_camper80s.unity3d";
        if (!BuildPipeline.GetCRCForAssetBundle(bundlePath, out uint crc)) throw new Exception("Bundle CRC unavailable");
        var bundle = AssetBundle.LoadFromFile(bundlePath, crc); if (!bundle) throw new Exception("Bundle reopen failed");
        try
        {
            root = Object.Instantiate(bundle.LoadAsset<GameObject>("rebirthCamper80s"));
            Validate(root);
            Review(root);
            report.Add("PASS bundle CRC reopen=" + crc + "; native address rebirthCamper80s; game NOT launched/tested.");
        }
        finally { if (root) Object.DestroyImmediate(root); bundle.Unload(true); }
        File.WriteAllLines(Output + "/verification.txt", report);
        Debug.Log("APPROVED_PICKUP_INSTALL_VERIFIED");
    }
    static void CloseBedCorners(Transform paint)
    {
        // Front sheet ends at |x|=.770,z=-.437; side sheets begin at
        // |x|=.7795,z=-.465. Fit a rounded folded strip over each seam.
        foreach (int side in new[] { -1, 1 })
        {
            var points = new List<Vector2> { new Vector2(.750f, -.438f) };
            var directions = new List<Vector2> { new Vector2(0, -1) };
            for (int i = 0; i <= 12; i++)
            {
                float angle = i * Mathf.PI / 24;
                points.Add(new Vector2(.765f + .014f * Mathf.Sin(angle), -.452f + .014f * Mathf.Cos(angle)));
                directions.Add(new Vector2(-Mathf.Sin(angle), -Mathf.Cos(angle)));
            }
            points.Add(new Vector2(.779f, -.482f)); directions.Add(new Vector2(-1, 0));
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int i = 0; i < points.Count; i++)
                for (int j = 0; j < 2; j++)
                {
                    v.Add(new Vector3(side * points[i].x, j == 0 ? .679f : 1.125f, points[i].y));
                    n.Add(new Vector3(side * directions[i].x, 0, directions[i].y)); uv.Add(new Vector2(.10f + .04f * i / (points.Count - 1), .41f + j * .08f));
                }
            for (int i = 0; i < points.Count - 1; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), n[a]) < 0)
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                else triangles.AddRange(new[] { a, b, c, b, d, c });
            }
            var mesh = new Mesh { name = "Painted bed front corner " + side };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            mesh = Save(mesh, "painted_corner_" + side + ".asset");
            var go = new GameObject(mesh.name); go.transform.SetParent(paint, true); go.layer = paint.parent.gameObject.layer; go.tag = "LOD";
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = body;
        }
    }
    static void RepairSharedLods(GameObject root, Transform camper)
    {
        foreach (string category in new[] { "body", "windows", "lights", "parts" })
        {
            var part = root.transform.Find(VisualPath + "/80sCamperTruck/" + category);
            var copies = camper.Cast<Transform>().Where(t => t.name.StartsWith("Camper shared " + category + " ")).ToList();
            var pairs = new Dictionary<Renderer, Renderer>();
            foreach (var f in part.GetComponentsInChildren<MeshFilter>(true))
            {
                // Different lamp groups reuse LOD child names. Match their native
                // vertex streams, not merely the first transform with that name.
                var copy = copies.FirstOrDefault(t => t.name == "Camper shared " + category + " " + f.name
                    && t.GetComponent<MeshFilter>().sharedMesh.vertices.SequenceEqual(f.sharedMesh.vertices));
                if (!copy) throw new Exception("Unmatched camper split: " + PathOf(f.transform, root.transform));
                pairs.Add(f.GetComponent<Renderer>(), copy.GetComponent<Renderer>()); copies.Remove(copy);
            }
            foreach (var lod in part.GetComponentsInChildren<LODGroup>(true))
            {
                var levels = lod.GetLODs();
                for (int i = 0; i < levels.Length; i++)
                {
                    var extra = new List<Renderer>();
                    foreach (var r in levels[i].renderers)
                    {
                        if (!r) throw new Exception("Native LOD renderer missing");
                        if (pairs.TryGetValue(r, out var copy)) { copy.gameObject.SetActive(true); extra.Add(copy); }
                    }
                    levels[i].renderers = levels[i].renderers.Concat(extra).ToArray();
                }
                lod.SetLODs(levels);
            }
        }
        report.Add("Shared camper triangle splits rejoined to original distance LOD levels.");
    }
    static void SplitPlates(GameObject root, Transform camper)
    {
        var plateRoot = root.transform.Find(VisualPath + "/80sCamperTruck/licence_plate"); int serial = 0;
        var pairs = new Dictionary<Renderer, Renderer>();
        foreach (var f in plateRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = f.sharedMesh; var front = Object.Instantiate(mesh); var rear = Object.Instantiate(mesh);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var keep = new List<int>(); var hide = new List<int>(); var triangles = mesh.GetTriangles(s); var v = mesh.vertices;
                for (int i = 0; i < triangles.Length; i += 3)
                    (f.transform.TransformPoint((v[triangles[i]] + v[triangles[i + 1]] + v[triangles[i + 2]]) / 3).z > 0 ? keep : hide)
                        .AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
                front.SetTriangles(keep, s); rear.SetTriangles(hide, s);
            }
            f.sharedMesh = Save(front, "front_plate_" + serial + ".asset");
            var copy = new GameObject("Camper original plate " + serial); copy.layer = f.gameObject.layer;
            copy.transform.SetParent(camper, false); copy.transform.SetPositionAndRotation(f.transform.position, f.transform.rotation); copy.transform.localScale = f.transform.lossyScale;
            copy.AddComponent<MeshFilter>().sharedMesh = Save(rear, "camper_plate_" + serial++ + ".asset");
            var renderer = copy.AddComponent<MeshRenderer>(); renderer.sharedMaterials = f.GetComponent<Renderer>().sharedMaterials;
            pairs.Add(f.GetComponent<Renderer>(), renderer);
        }
        foreach (var lod in plateRoot.GetComponentsInChildren<LODGroup>(true))
        {
            var levels = lod.GetLODs();
            for (int i = 0; i < levels.Length; i++) levels[i].renderers = levels[i].renderers.Concat(levels[i].renderers.Where(pairs.ContainsKey).Select(r => pairs[r])).ToArray();
            lod.SetLODs(levels);
        }
        report.Add("Original front plate common; original camper rear plate module-only; approved pickup plate retains original UV/artwork.");
    }
    static void Validate(GameObject root)
    {
        var paintRoot = Find(root, RebirthCamperBodyVisibility.PickupPaint);
        var trimRoot = Find(root, RebirthCamperBodyVisibility.PickupBodywork);
        var module = Find(root, "REBIRTHCamperBodyModule");
        var nativeBody = root.transform.Find(BodyPath).GetComponent<Renderer>();
        var floor = Find(root, "Continuous pressed load floor - native tub cutouts").GetComponent<Renderer>();
        var floorColor = floor.sharedMaterial.color;
        bool darkFloor = floor.sharedMaterial.shader.name == "Standard" && Mathf.Abs(floorColor.r - .08f) < .001f && Mathf.Abs(floorColor.g - .08f) < .001f
            && Mathf.Abs(floorColor.b - .08f) < .001f && Mathf.Abs(floorColor.a - 1) < .001f;
        if (floor.transform.IsChildOf(paintRoot.transform) || floor.sharedMaterial == nativeBody.sharedMaterial || !darkFloor)
            throw new Exception("Floor must remain dark and independent of native dye: parent=" + floor.transform.parent.name + ", material=" + floor.sharedMaterial.name + ", color=" + floor.sharedMaterial.color.ToString("R") + ", same=" + (floor.sharedMaterial == nativeBody.sharedMaterial));
        var paints = paintRoot.GetComponentsInChildren<Renderer>(true);
        if (paints.Length == 0 || paints.Any(r => !r.CompareTag("LOD") || r.sharedMaterials.Length != 1 || r.sharedMaterial != nativeBody.sharedMaterial))
            throw new Exception("Native dye binding mismatch");
        var rearPlate = Find(root, "Original rear license plate").GetComponent<Renderer>();
        if (rearPlate.sharedMaterial == nativeBody.sharedMaterial || rearPlate.sharedMaterial.mainTexture.name != "plateTX")
            throw new Exception("Original plate texture lost");
        if (root.GetComponentsInChildren<WheelCollider>(true).Length != 4) throw new Exception("Bundle missing wheels");
        var bumper = Find(root, "Rear bumper - straight span rounded returns").GetComponent<Renderer>();
        if (bumper.sharedMaterial != root.transform.Find(VisualPath + "/80sCamperTruck/parts").GetComponent<Renderer>().sharedMaterial)
            throw new Exception("Rear/front bumper material differs");
        var bumperMesh = bumper.GetComponent<MeshFilter>().sharedMesh;
        var verts = bumperMesh.vertices; var normals = bumperMesh.normals; var indices = bumperMesh.triangles;
        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            if (Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]), normals[a] + normals[b] + normals[c]) < -.000001f)
                throw new Exception("Bumper triangle points against its approved surface normals");
        }
        report.Add("PASS rear bumper original front material; all bumper triangle directions agree with approved normals.");
        report.Add("PASS load floor uses separate fixed dark finish outside native paint hierarchy; vertical corner closures share body dye.");
        var scenario = JsonUtility.FromJson<Scenario>(File.ReadAllText(Output + "/pickup-body-toggle.json"));
        if (scenario.installedStates == null || scenario.installedStates.Length < 3) throw new Exception("Missing roundtrip scenario");
        foreach (bool installed in scenario.installedStates)
        {
            State(root, installed);
            if (module.activeSelf != installed || trimRoot.activeSelf == installed || paintRoot.activeSelf == installed)
                throw new Exception("Modifier visibility state mismatch");
            if (paints.Any(r => r.gameObject.activeInHierarchy == installed)) throw new Exception("Paint section visibility mismatch");
            if (rearPlate.gameObject.activeInHierarchy == installed) throw new Exception("Rear plate state mismatch");
            report.Add("PASS shared runtime Apply(hasCamperBody=" + installed + "): pickup=" + trimRoot.activeSelf + ", paint=" + paintRoot.activeSelf + ", camper=" + module.activeSelf);
        }
        if (root.GetComponentsInChildren<LODGroup>(true).Any(g => g.GetLODs().Any(l => l.renderers.Any(r => !r)))) throw new Exception("Broken LOD reference");
        report.Add("PASS native dye shared by " + paints.Length + " added renderers; plate excluded; four wheel colliders; LOD groups=" + root.GetComponentsInChildren<LODGroup>(true).Length);
    }
    static void Review(GameObject root)
    {
        foreach (var lod in root.GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(0);
        RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.55f, .59f, .65f); RenderSettings.ambientEquatorColor = new Color(.35f, .36f, .39f); RenderSettings.ambientGroundColor = new Color(.20f, .19f, .18f);
        RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        var skycam = new GameObject("Reflection capture").AddComponent<Camera>(); skycam.transform.position = new Vector3(0, 100, 0); skycam.cullingMask = 0; skycam.clearFlags = CameraClearFlags.Skybox;
        var cube = new Cubemap(128, TextureFormat.RGBA32, true); skycam.RenderToCubemap(cube); RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflection = cube; Object.DestroyImmediate(skycam.gameObject);
        foreach (var spec in new[] { new Vector4(42, -35, 0, 1.15f), new Vector4(25, 140, 0, .65f), new Vector4(60, 40, 0, .45f) })
        { var l = new GameObject("Studio light").AddComponent<Light>(); l.type = LightType.Directional; l.intensity = spec.w; l.transform.rotation = Quaternion.Euler(spec.x, spec.y, spec.z); }
        State(root, false);
        Render("pickup", new Vector3(-5, 3, -6), new Vector3(0, .85f, -.35f), 2.17f);
        Render("rear-detail", new Vector3(2, 1, -6), new Vector3(0, .75f, -2.72f), .77f);
        var material = root.transform.Find(BodyPath).GetComponent<Renderer>().sharedMaterial;
        var before = material.color; material.color = new Color(190, 45, 35, 1);
        Render("pickup-dyed", new Vector3(-5, 3, -6), new Vector3(0, .85f, -.35f), 2.17f); material.color = before;
        material.color = new Color(190, 45, 35, 1);
        Render("painted-corner-detail", new Vector3(-3, 2, -4), new Vector3(.70f, .92f, -.47f), .30f); material.color = before;
        State(root, true);
        Render("camper", new Vector3(-5, 3, -6), new Vector3(0, 1.4f, -.5f), 2.7f);
    }
    static void Render(string label, Vector3 direction, Vector3 target, float size)
    {
        var cam = new GameObject("Review camera").AddComponent<Camera>(); cam.transform.position = target + direction; cam.transform.LookAt(target);
        cam.orthographic = true; cam.orthographicSize = size; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.12f, .14f, .15f); cam.nearClipPlane = .05f; cam.farClipPlane = 50;
        var rt = new RenderTexture(1800, 1320, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; rt.Create(); cam.targetTexture = rt; cam.Render();
        var old = RenderTexture.active; RenderTexture.active = rt; var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/" + label + ".png", image.EncodeToPNG()); RenderTexture.active = old;
        cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image); Object.DestroyImmediate(cam.gameObject);
    }
}
