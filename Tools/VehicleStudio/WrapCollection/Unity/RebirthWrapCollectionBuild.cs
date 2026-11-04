using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RebirthWrapCollectionBuild
{
    [Serializable] private sealed class QualityAsset { public string target; public string sha256; }
    [Serializable] private sealed class QualityReceipt { public string id; public string status; public string surfaceContractVersion;public string nativeCoverageSHA256;public string mappingSHA256;public string geometrySHA256; public QualityAsset[] assets; }
    [Serializable] private sealed class CoverageView { public string name; public int uncoveredPixels; }
    [Serializable] private sealed class NativeCoverage {public string status,version,geometrySHA256;public QualityAsset[] mappingFiles,maskFiles;public string[] requiredViews;public CoverageView[] views;}
    private static string HashFile(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    private static void VerifyNativeCoverage(QualityReceipt receipt){
        string path="Assets/REBIRTHCamperWrapCollection/Quality/NativeSurfaceCoverage.json";
        if(!File.Exists(path)||HashFile(path)!=receipt.nativeCoverageSHA256)throw new Exception("Missing or stale native coverage proof");
        var proof=JsonUtility.FromJson<NativeCoverage>(File.ReadAllText(path));
        if(proof.status!="PASS"||proof.version!=RebirthCamperWrapAppearance.NativeCoverageContract||proof.views==null||proof.views.Length<98||proof.requiredViews==null||proof.requiredViews.Length!=proof.views.Length||proof.requiredViews.Except(proof.views.Select(v=>v.name)).Any()||proof.views.Any(v=>v.uncoveredPixels!=0)||proof.views.Select(v=>v.name).Distinct().Count()!=proof.views.Length)throw new Exception("Native coverage proof failed");
        if(!Environment.GetCommandLineArgs().Contains("-wrapVehicleBundle")||HashFile(Arg("-wrapVehicleBundle"))!=proof.geometrySHA256||receipt.geometrySHA256!=proof.geometrySHA256)throw new Exception("Native geometry changed after coverage verification");
        if(proof.maskFiles==null||proof.maskFiles.Length!=12||proof.maskFiles.Select(f=>f.target).Distinct().Count()!=12)throw new Exception("Incomplete mask proof");
        foreach(var file in proof.maskFiles)if(Path.GetFileName(file.target)!=file.target||HashFile(Path.Combine(Arg("-wrapSurfaceTemplates"),file.target))!=file.sha256)throw new Exception("Mask changed after native verification");
        if(proof.mappingFiles==null||proof.mappingFiles.Length!=4||proof.mappingFiles.Select(f=>f.target).Distinct().Count()!=4)throw new Exception("Incomplete shader-family proof");
        foreach(var file in proof.mappingFiles){
            if(Path.GetFileName(file.target)!=file.target||HashFile("Assets/REBIRTHCamperWrapCollection/Shaders/"+file.target)!=file.sha256)throw new Exception("Shader family changed after coverage verification");
        }
    }
    private static void VerifyNativeAlpha(string id){
        string templates=Arg("-wrapSurfaceTemplates");
        string[] panels={"hood","left-continuous","right-continuous","camper-front-continuous","camper-rear-fixed-continuous","rear-door","camper-roof","cab-roof","truck-front-face","cab-back","pickup-tailgate","pickup-bed-interior-unfolded"};
        string[] suffixes={"HoodMaster","SceneLayers","BotanicalLayers","GardenCorner","MusicCorner","LeftBandSharp","RightBandSharp","RearSharp","FrontSharp","RearDoor","RearLeft","RearRight"};
        for(int k=0;k<panels.Length;k++){
            var mask=new Texture2D(2,2,TextureFormat.RGBA32,false);var art=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try{
                if(!ImageConversion.LoadImage(mask,File.ReadAllBytes(Path.Combine(templates,panels[k]+"-paintable.png")))||!ImageConversion.LoadImage(art,File.ReadAllBytes("Assets/REBIRTHCamperWrapCollection/Artwork/"+id+suffixes[k]+".png")))throw new Exception("Unreadable source cutout");
                if(mask.width!=art.width||mask.height!=art.height)throw new Exception("Wrong native cutout dimensions: "+panels[k]);
                var ma=mask.GetPixels32();var aa=art.GetPixels32();for(int i=0;i<ma.Length;i++)if(ma[i].a!=aa[i].a||(aa[i].a==0&&(aa[i].r!=0||aa[i].g!=0||aa[i].b!=0)))throw new Exception("Wrong native alpha or transparent RGB: "+panels[k]);
            }finally{UnityEngine.Object.DestroyImmediate(mask);UnityEngine.Object.DestroyImmediate(art);}
        }
    }
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
            if(RebirthCamperWrapAppearance.UsesNativeBodyLayout(id)){
                VerifyNativeCoverage(receipt);
                VerifyNativeAlpha(id);
                string mapping="Assets/REBIRTHCamperWrapCollection/Shaders/RebirthCamperReferenceWrapCollection.shader";
                if(receipt.surfaceContractVersion!=RebirthCamperWrapAppearance.NativeCoverageContract)throw new Exception("Obsolete or missing native surface contract: "+id);
                using(var sha=System.Security.Cryptography.SHA256.Create()){
                    string hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(mapping))).Replace("-","").ToLowerInvariant();
                    if(hash!=receipt.mappingSHA256)throw new Exception("Mapper changed after contract verification: "+id);
                }
                if(receipt.geometrySHA256!="4184efcb37ae3a55c6691c2749a213953e465de7d3268c22c5f1f48fc8df5290")throw new Exception("Unverified native geometry: "+id);
                Directory.CreateDirectory("Assets/REBIRTHCamperWrapCollection/Contracts");
                File.WriteAllText("Assets/REBIRTHCamperWrapCollection/Contracts/"+id.ToLowerInvariant()+"-body-layout.json","{\"layoutVersion\":3,\"version\":\""+receipt.surfaceContractVersion+"\"}");
            }
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
        AssetDatabase.Refresh();
        foreach(string id in selected.Where(RebirthCamperWrapAppearance.UsesNativeBodyLayout)){
            string path="Assets/REBIRTHCamperWrapCollection/Contracts/"+id.ToLowerInvariant()+"-body-layout.json";
            if(!AssetDatabase.LoadAssetAtPath<TextAsset>(path))throw new Exception("Missing layout marker: "+id);
            assets=assets.Concat(new[]{path}).ToArray();
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
