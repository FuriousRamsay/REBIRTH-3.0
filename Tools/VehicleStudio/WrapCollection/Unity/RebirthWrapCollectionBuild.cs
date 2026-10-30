using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RebirthWrapCollectionBuild
{
    [Serializable] private sealed class QualityAsset { public string target; public string sha256; }
    [Serializable] private sealed class QualityReceipt { public string id; public string status; public QualityAsset[] assets; }
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
            bool concept=RebirthCamperWrapAppearance.IsLayeredTexture(Path.GetFileNameWithoutExtension(path));
            int maximum=concept?4096:2048;
            var format=TextureImporterFormat.Automatic;
            var npot=concept?TextureImporterNPOTScale.None:TextureImporterNPOTScale.ToNearest;
            var current=importer.GetPlatformTextureSettings("Standalone");
            if(importer.textureType==TextureImporterType.Default&&importer.sRGBTexture&&importer.mipmapEnabled&&!importer.isReadable
                &&importer.wrapMode==TextureWrapMode.Clamp&&importer.filterMode==FilterMode.Trilinear&&importer.anisoLevel==16
                &&current.overridden&&current.format==format&&current.maxTextureSize==maximum&&importer.npotScale==npot
                &&importer.crunchedCompression&&current.crunchedCompression&&current.compressionQuality==100
                &&importer.textureCompression==TextureImporterCompression.Compressed&&current.textureCompression==TextureImporterCompression.Compressed)continue;
            importer.npotScale=npot;
            importer.anisoLevel=16;
            importer.textureType=TextureImporterType.Default;
            importer.sRGBTexture=true; importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled=true; importer.isReadable=false;
            importer.wrapMode=TextureWrapMode.Clamp; importer.filterMode=FilterMode.Trilinear;
            importer.maxTextureSize=maximum;
            importer.crunchedCompression=true; importer.compressionQuality=100;
            importer.textureCompression=TextureImporterCompression.Compressed;
            var settings=new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=maximum,format=format,compressionQuality=100,crunchedCompression=true,textureCompression=TextureImporterCompression.Compressed};
            importer.SetPlatformTextureSettings(settings); importer.SaveAndReimport();
        }
        string[] selected=Environment.GetCommandLineArgs().Contains("-wrapBuildIds")?Arg("-wrapBuildIds").Split(','):null;
        // Legacy catalogue art is on visual HOLD. New authoring must supply a
        // measured, source-hash-verified quality receipt before any bundle build.
        if(selected==null)throw new Exception("Bulk authoring blocked: select IDs with measured panel contracts.");
        foreach(string id in selected){
            string quality="Assets/REBIRTHCamperWrapCollection/Quality/"+id+".json";
            if(!File.Exists(quality))throw new Exception("Missing panel quality receipt for "+id);
            var receipt=JsonUtility.FromJson<QualityReceipt>(File.ReadAllText(quality));
            if(receipt.id!=id||receipt.status!="PASS"||receipt.assets==null||receipt.assets.Length!=(RebirthCamperWrapAppearance.UsesNativeBodyLayout(id)?12:9))throw new Exception("Invalid panel receipt for "+id);
            foreach(var asset in receipt.assets){
                string path="Assets/REBIRTHCamperWrapCollection/Artwork/"+asset.target;
                if(Path.GetFileName(asset.target)!=asset.target||!File.Exists(path))throw new Exception("Invalid panel target: "+asset.target);
                using(var sha=System.Security.Cryptography.SHA256.Create()){
                    string hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
                    if(hash!=asset.sha256)throw new Exception("Panel changed after quality verification: "+asset.target);
                }
            }
        }
        string[] assets=AssetDatabase.FindAssets("t:Shader",new[]{"Assets/REBIRTHCamperWrapCollection/Shaders"})
            .Select(AssetDatabase.GUIDToAssetPath)
            .Concat(AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/REBIRTHCamperWrapCollection/Artwork"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>selected==null||selected.Contains(Path.GetFileNameWithoutExtension(p)))).ToArray();
        if(selected!=null&&assets.Count(p=>p.EndsWith(".png"))!=selected.Length)throw new Exception("Requested build artwork missing.");
        foreach(string design in selected.Where(RebirthCamperWrapAppearance.IsLayered))
        {
            string[] dependencies={"UndeadChillHoodMaster","UndeadChillSceneLayers","UndeadChillBotanicalLayers","UndeadChillGardenCorner","UndeadChillMusicCorner","UndeadChillLeftBandSharp","UndeadChillRightBandSharp","UndeadChillRearSharp","UndeadChillFrontSharp"};
            if(RebirthCamperWrapAppearance.UsesNativeBodyLayout(design))dependencies=dependencies.Concat(new[]{"UndeadChillRearDoor","UndeadChillRearLeft","UndeadChillRearRight"}).ToArray();
            foreach(string dependency in dependencies)
            {
                string path="Assets/REBIRTHCamperWrapCollection/Artwork/"+dependency.Replace("UndeadChill",design)+".png";
                if(!AssetDatabase.LoadAssetAtPath<Texture2D>(path))throw new Exception("Missing layered artwork: "+path);
                if(!assets.Contains(path))assets=assets.Concat(new[]{path}).ToArray();
            }
        }
        foreach(string path in assets.Where(p=>p.EndsWith(".shader")))
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Shader failed: "+path);
        }
        AssetDatabase.SaveAssets();
        string bundleName=Environment.GetCommandLineArgs().Contains("-wrapApprovedUndead")?RebirthCamperWrapAppearance.DedicatedBundleName(Arg("-wrapBuildIds")):"rebirth_camper_wraps.unity3d";
        var map=new AssetBundleBuild{assetBundleName=bundleName,assetNames=assets};
        if(RebirthAssetBundlePackaging.BuildAssetBundles(output,new[]{map},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64)==null)
            throw new Exception("Collection build failed.");
        foreach(string path in assets.Where(p=>p.EndsWith(".shader"))){
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
            if(ShaderUtil.ShaderHasError(shader))throw new Exception("Built shader variants failed: "+path+"\n"+string.Join("\n",ShaderUtil.GetShaderMessages(shader).Select(m=>m.message)));
        }
        var bundle=AssetBundle.LoadFromFile(Path.Combine(output,map.assetBundleName));
        if(bundle==null)throw new Exception("Cannot reopen collection bundle.");
        try
        {
            var names=bundle.GetAllAssetNames();
            var conceptTexture=bundle.LoadAsset<Texture2D>("assets/rebirthcamperwrapcollection/artwork/undeadchill.png");
            if(conceptTexture)File.WriteAllText(Path.Combine(output,"concept-import.txt"),"width="+conceptTexture.width+" height="+conceptTexture.height+" format="+conceptTexture.format+" mipmaps="+conceptTexture.mipmapCount);
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
        string name=Environment.GetCommandLineArgs().Contains("-wrapApprovedUndead")?RebirthCamperWrapAppearance.DedicatedBundleName(Arg("-wrapBuildIds")):"rebirth_camper_wraps.unity3d";
        File.Copy(Path.Combine(Arg("-wrapOutput"),name),Path.Combine(runtime,name),true);
        RebirthWrapCollectionReview.Run();
        if(Environment.GetCommandLineArgs().Contains("-wrapIconOutput"))RebirthWrapCollectionIcons.Run();
    }
}
