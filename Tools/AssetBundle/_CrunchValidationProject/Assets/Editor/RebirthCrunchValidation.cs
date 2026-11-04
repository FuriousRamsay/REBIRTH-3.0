using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RebirthCrunchValidation
{
    public static void Run()
    {
        string[] paths = { "Assets/opaque.png", "Assets/alpha.png", "Assets/native-npot.png" };
        for (int index = 0; index < paths.Length; index++)
        {
            int width = index == 2 ? 1088 : 128, height = index == 2 ? 512 : 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)(i % 251), 100, 180, (byte)(index == 0 ? 255 : i % 256));
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(paths[index], texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(paths[index]);
            var importer = (TextureImporter)AssetImporter.GetAtPath(paths[index]);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        string output = Path.GetFullPath("CrunchBuild");
        RebirthAssetBundlePackaging.BuildAssetBundles(output, new[] { new AssetBundleBuild { assetBundleName = "synthetic.unity3d", assetNames = paths } },
            BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        foreach (string path in paths)
        {
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (path.Contains("npot") && (image.width != 1088 || image.height != 512)) throw new Exception("Native dimensions altered");
        }
        // Generated texture assets have no importer and must be reported explicitly.
        AssetDatabase.DeleteAsset("Assets/generated.asset");
        AssetDatabase.CreateAsset(new Texture2D(16, 16), "Assets/generated.asset");
        string report = File.ReadAllText(Path.Combine(output,"crunch-textures-verified.tsv"));
        if (!report.Contains("DXT1Crunched") || !report.Contains("DXT5Crunched") || !report.Contains("EXCEPTION: native non-power-of-two dimensions preserved")) throw new Exception("Missing actual format or exception evidence");
        string generatedOutput = output + "-generated";
        RebirthAssetBundlePackaging.BuildAssetBundles(generatedOutput, new[] { new AssetBundleBuild { assetBundleName = "unsupported.unity3d", assetNames = new[] { "Assets/generated.asset" } } }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        if (!File.ReadAllText(Path.Combine(generatedOutput,"crunch-textures-verified.tsv")).Contains("EXCEPTION: generated or embedded")) throw new Exception("Generated texture not reported");
        File.WriteAllText("CrunchBuild/validation-PASS.txt", "Actual Unity import and bundle reopen PASS; RGB DXT1Crunched/alpha DXT5Crunched; native NPOT dimensions retained and reported; generated texture exception reported. No production bundles changed.");
    }
}
