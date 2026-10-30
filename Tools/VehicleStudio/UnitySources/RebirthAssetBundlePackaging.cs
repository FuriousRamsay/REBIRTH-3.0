using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Stage this editor source alongside every REBIRTH bundle builder. Missing policy
// is a compile error, never permission to silently package uncompressed textures.
public static class RebirthAssetBundlePackaging
{
    public static AssetBundleManifest BuildAssetBundles(string output, AssetBundleBuild[] builds,
        BuildAssetBundleOptions options, BuildTarget target)
    {
        if (builds == null) throw new ArgumentNullException(nameof(builds));
        if (BuildPipeline.GetBuildTargetGroup(target) != BuildTargetGroup.Standalone)
            throw new NotSupportedException("Crunch packaging currently qualifies desktop DXT1/DXT5 targets only.");
        Directory.CreateDirectory(output);
        string receipt = Path.Combine(output, "crunch-textures-verified.tsv");
        if (File.Exists(receipt)) File.Delete(receipt);
        var rows = new List<string> { "asset\ttexture\tformat\twidth\theight\tquality\tstatus" };
        var exceptions = new Dictionary<string,string>(StringComparer.Ordinal);
        var dependencies = AssetDatabase.GetDependencies(builds.SelectMany(b => b.assetNames).Distinct().ToArray(), true);
        foreach (string path in dependencies.OrderBy(p => p, StringComparer.Ordinal))
        {
            var textures = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture>().ToArray();
            if (textures.Length == 0) continue;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            string exception = importer == null ? "generated or embedded texture has no TextureImporter" :
                textures.Any(t => !(t is Texture2D)) ? "non-2D texture" :
                extension == ".hdr" || extension == ".exr" || importer.textureType == TextureImporterType.Lightmap ? "HDR/lightmap data preserved" : null;
            if (exception != null)
            {
                foreach (var texture in textures)
                {
                    exceptions[Signature(texture)] = exception;
                    rows.Add(Row(path, texture, "EXCEPTION: " + exception));
                }
                continue;
            }
            // Unity's automatic desktop format resolver applies Crunch and chooses
            // the alpha-capable DXT variant; a fixed BC format bypasses that choice.
            var wanted = TextureImporterFormat.Automatic;
            var settings = importer.GetPlatformTextureSettings("Standalone");
            if (!settings.overridden) settings.maxTextureSize = importer.maxTextureSize;
            bool change = !importer.crunchedCompression || importer.compressionQuality != 100 || importer.textureCompression != TextureImporterCompression.Compressed ||
                !settings.overridden || !settings.crunchedCompression || settings.format != wanted || settings.compressionQuality != 100 || settings.textureCompression != TextureImporterCompression.Compressed;
            if (change)
            {
                importer.crunchedCompression = true;
                importer.compressionQuality = 100;
                importer.textureCompression = TextureImporterCompression.Compressed;
                settings.name = "Standalone";
                settings.overridden = true;
                settings.format = wanted;
                settings.crunchedCompression = true;
                settings.compressionQuality = 100;
                settings.textureCompression = TextureImporterCompression.Compressed;
                importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
            }
            foreach (var texture in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>())
            {
                string status = "CRUNCH";
                if (!IsCrunch(texture) && (!Mathf.IsPowerOfTwo(texture.width) || !Mathf.IsPowerOfTwo(texture.height))
                    && (texture.format == TextureFormat.RGBA32 || texture.format == TextureFormat.RGB24))
                {
                    exceptions[Signature(texture)] = "native non-power-of-two dimensions preserved";
                    status = "EXCEPTION: native non-power-of-two dimensions preserved";
                }
                else RequireCrunch(texture, path);
                rows.Add(Row(path, texture, status));
            }
        }
        // This is preflight evidence; the verified receipt is published only after
        // reopening the actual bundles and checking their packaged textures.
        var manifest = BuildPipeline.BuildAssetBundles(output, builds, options, target);
        if (manifest == null) throw new InvalidOperationException("Asset bundle build failed.");
        foreach (var build in builds)
        {
            string name = build.assetBundleName + (string.IsNullOrEmpty(build.assetBundleVariant) ? "" : "." + build.assetBundleVariant);
            var bundle = AssetBundle.LoadFromFile(Path.Combine(output, name));
            if (bundle == null) throw new InvalidOperationException("Cannot reopen bundle for Crunch verification: " + name);
            try
            {
                foreach (var texture in bundle.LoadAllAssets<Texture>())
                {
                    if (exceptions.TryGetValue(Signature(texture), out string reason))
                        rows.Add(Row("BUNDLE:" + name, texture, "EXCEPTION: " + reason));
                    else
                    {
                        if (!(texture is Texture2D image)) throw new InvalidOperationException("Unexpected non-2D packaged texture: " + name);
                        RequireCrunch(image, name);
                        rows.Add(Row("BUNDLE:" + name, image, "CRUNCH"));
                    }
                }
            }
            finally { bundle.Unload(true); }
        }
        File.WriteAllLines(receipt, rows);
        return manifest;
    }

    private static string Signature(Texture texture) => texture.name + "|" + texture.GetType().Name + "|" +
        (texture is Texture2D image ? image.format.ToString() : "non-2D") + "|" + texture.width + "|" + texture.height;
    private static string Row(string path, Texture texture, string status) => path + "\t" + texture.name + "\t" +
        (texture is Texture2D image ? image.format.ToString() : texture.GetType().Name) + "\t" + texture.width + "\t" + texture.height +
        "\t" + (status == "CRUNCH" ? "100" : "n/a") + "\t" + status;
    private static bool IsCrunch(Texture2D texture) => texture.format == TextureFormat.DXT1Crunched || texture.format == TextureFormat.DXT5Crunched;

    private static void RequireCrunch(Texture2D texture, string path)
    {
        if (!IsCrunch(texture))
            throw new InvalidOperationException("Crunch required; actual format " + texture.format + " at " + path + "/" + texture.name +
                ". Preserve approved dimensions/artwork; do not silently resize or accept fallback.");
    }
}
