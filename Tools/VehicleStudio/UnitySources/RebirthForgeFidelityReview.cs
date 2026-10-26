using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Isolated imported-material review. Does not build/install a game bundle.
public static class RebirthForgeFidelityReview
{
    static string Root = "Assets/REBIRTHForgeFidelity/Export_v17";
    static string Output = "E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/Unity_v17";
    static int TextureMax = 2048;
    static Vector3 ExpectedBounds = new Vector3(.8904f, .567f, .6130f);
    static Texture2D Load(string name, bool normal = false, bool color = false)
    {
        bool hdr = File.Exists(Root + "/" + name + ".exr");
        string path = Root + "/" + name + (hdr ? ".exr" : ".png");
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new FileNotFoundException(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = color && !hdr;
        importer.isReadable = !normal;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = TextureMax;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static void Capture(Camera camera, string name)
    {
        var rt = new RenderTexture(1400, 1100, 24);
        var image = new Texture2D(1400, 1100, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1400, 1100), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
        }
    }
    public static void RunBatch()
    {
        if (Directory.Exists(Output)) throw new InvalidOperationException("Preserve prior review evidence");
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        var modelPath = Root + "/forge-review.fbx";
        var modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (!modelImporter) throw new FileNotFoundException(modelPath);
        modelImporter.importNormals = ModelImporterNormals.Import;
        modelImporter.importTangents = ModelImporterTangents.CalculateMikk;
        modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
        modelImporter.SaveAndReimport();
        var albedo = Load("albedo", color: true);
        var normal = Load("normal", normal: true);
        var roughness = Load("roughness"); var metallic = Load("metallic");
        var emission = Load("emission", color: true);
        var packed = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
        var metalPixels = metallic.GetPixels(); var roughPixels = roughness.GetPixels();
        if (metalPixels.Length != roughPixels.Length) throw new InvalidDataException("Map sizes differ");
        for (int i = 0; i < metalPixels.Length; i++)
            metalPixels[i] = new Color(metalPixels[i].r, 0, 0, 1 - roughPixels[i].r);
        packed.SetPixels(metalPixels); packed.Apply();
        File.WriteAllBytes(Root + "/metallic-smoothness.png", packed.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(packed); AssetDatabase.Refresh();
        var packedMap = Load("metallic-smoothness");
        var material = new Material(Shader.Find("Standard"));
        material.name = "Forge isolated baked material review"; material.color = Color.white;
        material.mainTexture = albedo; material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap", packedMap); material.EnableKeyword("_METALLICGLOSSMAP");
        material.SetFloat("_GlossMapScale", 1); material.SetFloat("_BumpScale", 1);
        material.SetTexture("_EmissionMap", emission); material.SetColor("_EmissionColor", Color.white); material.EnableKeyword("_EMISSION");
        AssetDatabase.CreateAsset(material, Root + "/forge-review.mat");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
        int triangles = 0;
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = filter.sharedMesh;
            if (mesh.uv.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount)
                throw new InvalidDataException("Missing imported UV0 or normals: " + filter.name);
            triangles += mesh.triangles.Length / 3;
        }
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            var slots = new Material[Math.Max(1, renderer.sharedMaterials.Length)];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
        }
        // Static review geometry shares one baked material; avoid retaining hundreds of renderers.
        var sourceFilters = model.GetComponentsInChildren<MeshFilter>();
        var combine = new CombineInstance[sourceFilters.Length];
        for (int i = 0; i < sourceFilters.Length; i++)
        {
            combine[i].mesh = sourceFilters[i].sharedMesh;
            combine[i].transform = model.transform.worldToLocalMatrix * sourceFilters[i].transform.localToWorldMatrix;
            sourceFilters[i].GetComponent<Renderer>().enabled = false;
        }
        var combined = new Mesh { name = "Forge isolated combined review mesh", indexFormat = IndexFormat.UInt32 };
        combined.CombineMeshes(combine, true, true); combined.RecalculateBounds();
        AssetDatabase.CreateAsset(combined, Root + "/forge-review-mesh.asset");
        var combinedObject = new GameObject("Forge combined material review"); combinedObject.transform.SetParent(model.transform, false);
        combinedObject.AddComponent<MeshFilter>().sharedMesh = combined;
        combinedObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        var importedBounds = combinedObject.GetComponent<MeshRenderer>().bounds;
        if (Vector3.Distance(importedBounds.size, ExpectedBounds) > .03f)
            throw new InvalidDataException("Imported scale/axis mismatch: " + importedBounds.size);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.15f, .15f, .15f);
        var reflection = new Cubemap(16, TextureFormat.RGBA32, false);
        foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
        {
            var pixels = new Color[256]; for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(.35f, .35f, .35f);
            reflection.SetPixels(pixels, face);
        }
        reflection.Apply(); AssetDatabase.CreateAsset(reflection, Root + "/diagnostic-reflection.asset");
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = reflection; RenderSettings.reflectionIntensity = .6f;
        var key = new GameObject("Diagnostic key").AddComponent<Light>(); key.type = LightType.Directional;
        key.intensity = 1.1f; key.transform.rotation = Quaternion.Euler(45, 135, 0);
        var fill = new GameObject("Diagnostic fill").AddComponent<Light>(); fill.type = LightType.Directional;
        fill.intensity = .25f; fill.transform.rotation = Quaternion.Euler(20, -35, 0);
        var camera = new GameObject("Imported mesh review camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .08f, .08f); camera.fieldOfView = 26.5f;
        camera.transform.position = new Vector3(-1.05f, 1.05f, 1.8f); camera.transform.LookAt(new Vector3(-.09f, .29f, .015f));
        Capture(camera, "imported-full");
        camera.transform.position = new Vector3(-.95f, .62f, .82f); camera.transform.LookAt(new Vector3(-.41f, .30f, .01f));
        Capture(camera, "imported-bellows");
        File.WriteAllText(Output + "/material-review.txt", "UNVERIFIED_FIDELITY_REVIEW\nTriangles=" + triangles +
            "\nBounds=" + importedBounds.size + "\nCombinedStaticRenderers=1\nShader=Standard\nColorSpace=" + QualitySettings.activeColorSpace +
            "\nNormal imported as NormalMap; no manual green flip.\nMetallic red, smoothness=1-roughness in alpha.\nDiagnostic Unity lighting differs from Blender; no calibrated appearance claim.\nNo bundle build or game installation.");
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Root + "/ForgeReview.unity");
    }
    public static void RunBatchV21()
    {
        Root = "Assets/REBIRTHForgeFidelity/Export_v21";
        Output = "E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/Unity_v21";
        TextureMax = 2048;
        ExpectedBounds = new Vector3(.8904f, .584f, .6130f);
        RunBatch();
    }
}
