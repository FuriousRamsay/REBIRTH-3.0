using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildTraderVoices
{
    [Serializable] private sealed class AssetList { public string[] paths; }
    public static void Build()
    {
        var paths=JsonUtility.FromJson<AssetList>(File.ReadAllText("Assets/voice_assets.json")).paths;
        foreach(var path in paths)
        {
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono=true;

            importer.loadInBackground=true;
            var settings=importer.defaultSampleSettings;
            settings.preloadAudioData=false;
            settings.loadType=AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;
            settings.quality=.75f;
            importer.defaultSampleSettings=settings;
            importer.SaveAndReimport();
        }
        Directory.CreateDirectory("Build");
        var build=new AssetBundleBuild {assetBundleName="rebirth_trader_voices.unity3d",assetNames=paths,
            addressableNames=paths.Select(Path.GetFileNameWithoutExtension).ToArray()};
        var result=BuildPipeline.BuildAssetBundles("Build",new[]{build},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        if(result==null)throw new Exception("Bundle build failed");
        var bundle=AssetBundle.LoadFromFile("Build/"+build.assetBundleName);
        if(bundle==null||bundle.GetAllAssetNames().Length!=3600)throw new Exception("Bundle asset count mismatch");
        foreach(var path in paths)
        {
            var clip=bundle.LoadAsset<AudioClip>(Path.GetFileNameWithoutExtension(path));
            if(clip==null||clip.length<=0)throw new Exception("Invalid bundled clip: "+path);
            Resources.UnloadAsset(clip);
        }
        File.WriteAllText("Build/verification.json","{\"clips\":3600,\"loadedAndVerified\":3600,\"unity\":\""+Application.unityVersion+"\"}");
        bundle.Unload(true);
    }
}

