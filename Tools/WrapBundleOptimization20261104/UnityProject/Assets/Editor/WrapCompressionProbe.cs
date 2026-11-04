using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WrapCompressionProbe
{
    public static void Run()
    {
        try
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string path = "Assets/Textures/NativeAligned.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.isReadable = true;
            var settings = importer.GetPlatformTextureSettings("Standalone");
            settings.overridden = true;
            settings.maxTextureSize = 4096;
            settings.format = TextureImporterFormat.BC7;
            settings.compressionQuality = 100;
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            string report = "Editor=" + Application.unityVersion + " GPU=" + SystemInfo.graphicsDeviceName
                + " SupportsBC7=" + SystemInfo.SupportsTextureFormat(TextureFormat.BC7)
                + " Imported=" + texture.width + "x" + texture.height + " " + texture.format;
            // Also test the native non-multiple-of-four dimensions directly, independently of importer fallback.
            var direct = new Texture2D(texture.width, texture.height, TextureFormat.BC7, false);
            int bytes = ((texture.width + 3) / 4) * ((texture.height + 3) / 4) * 16;
            direct.LoadRawTextureData(new byte[bytes]);
            direct.Apply(false, false);
            report += " Direct=" + direct.width + "x" + direct.height + " " + direct.format;
            File.WriteAllText(Path.Combine(root, "probe.txt"), report);
            Debug.Log(report);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
