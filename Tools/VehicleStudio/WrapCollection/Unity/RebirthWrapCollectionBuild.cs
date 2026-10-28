using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RebirthWrapCollectionBuild
{
    private static string Arg(string key)
    {
        string[] args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,key);
        if(i<0||i+1>=args.Length)throw new Exception("Missing "+key);
        return args[i+1];
    }
    public static void Build()
    {
        string output=Arg("-wrapOutput"); Directory.CreateDirectory(output);
        PlayerSettings.colorSpace=ColorSpace.Linear;
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/REBIRTHCamperWrapCollection/Artwork"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;
            importer.sRGBTexture=true; importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled=true; importer.isReadable=false;
            importer.wrapMode=TextureWrapMode.Clamp; importer.filterMode=FilterMode.Trilinear;
            importer.maxTextureSize=2048;
            var settings=new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=2048,format=TextureImporterFormat.BC7,compressionQuality=100};
            importer.SetPlatformTextureSettings(settings); importer.SaveAndReimport();
        }
        string[] selected=Environment.GetCommandLineArgs().Contains("-wrapBuildIds")?Arg("-wrapBuildIds").Split(','):null;
        string[] assets=AssetDatabase.FindAssets("t:Shader",new[]{"Assets/REBIRTHCamperWrapCollection/Shaders"})
            .Select(AssetDatabase.GUIDToAssetPath)
            .Concat(AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/REBIRTHCamperWrapCollection/Artwork"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>selected==null||selected.Contains(Path.GetFileNameWithoutExtension(p)))).ToArray();
        if(selected!=null&&assets.Count(p=>p.EndsWith(".png"))!=selected.Length)throw new Exception("Requested build artwork missing.");
        foreach(string path in assets.Where(p=>p.EndsWith(".shader")))
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Shader failed: "+path);
        }
        AssetDatabase.SaveAssets();
        var map=new AssetBundleBuild{assetBundleName="rebirth_camper_wraps.unity3d",assetNames=assets};
        if(BuildPipeline.BuildAssetBundles(output,new[]{map},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64)==null)
            throw new Exception("Collection build failed.");
        var bundle=AssetBundle.LoadFromFile(Path.Combine(output,map.assetBundleName));
        if(bundle==null)throw new Exception("Cannot reopen collection bundle.");
        try
        {
            var names=bundle.GetAllAssetNames();
            foreach(string path in assets)if(!names.Contains(path.ToLowerInvariant()))throw new Exception("Missing asset "+path);
            File.WriteAllText(Path.Combine(output,"assets.txt"),string.Join("\n",names));
        }
        finally{bundle.Unload(true);}
        Debug.Log("WRAP_COLLECTION_BUNDLE_PASS assets="+assets.Length);
    }
    public static void BuildAndReview()
    {
        Build();
        string runtime=Path.Combine(Arg("-wrapRuntimeRoot"),"Resources");Directory.CreateDirectory(runtime);
        File.Copy(Path.Combine(Arg("-wrapOutput"),"rebirth_camper_wraps.unity3d"),Path.Combine(runtime,"rebirth_camper_wraps.unity3d"),true);
        RebirthWrapCollectionReview.Run();
        if(Environment.GetCommandLineArgs().Contains("-wrapIconOutput"))RebirthWrapCollectionIcons.Run();
    }
}
