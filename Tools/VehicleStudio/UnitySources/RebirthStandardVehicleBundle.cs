using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class RebirthStandardVehicleBundle
{
    [Serializable] public class Receipt {public string status,kind,bundle,sha256,prefab;public uint crc;public int wheels,lights;}
    public static void BuildBothBatch()
    {
        if(!Application.isBatchMode || QualitySettings.activeColorSpace!=ColorSpace.Linear)
            throw new InvalidOperationException("Linear batch editor required");
        string version="v1", selected=null;
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i<args.Length-1;i++)
        {if(args[i]=="-vehicleCandidateVersion")version=args[i+1];if(args[i]=="-vehicleKind")selected=args[i+1];}
        if(!System.Text.RegularExpressions.Regex.IsMatch(version,@"^v[0-9]+$") || (selected!=null && selected!="Buggy" && selected!="Bike"))
            throw new InvalidDataException("Invalid candidate selection");
        foreach(string kind in selected==null?new[]{"Buggy","Bike"}:new[]{selected})
        {
            string prefabPath="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Final_"+version+"/rebirth"+kind+".prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if(prefab==null)throw new FileNotFoundException(prefabPath);
            string output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/"+kind+"Candidate_"+version;
            if(File.Exists(Path.Combine(output,"receipt.json")))throw new InvalidOperationException("Preserve previous bundle; use a new candidate version");
            string archive=Path.Combine(output,"UncompressedSource");Directory.CreateDirectory(archive);
            string name="rebirth_"+kind.ToLowerInvariant()+".unity3d";
            var builds=new[]{new AssetBundleBuild {assetBundleName=name,assetNames=new[]{prefabPath}}};
            if(BuildPipeline.BuildAssetBundles(archive,builds,BuildAssetBundleOptions.UncompressedAssetBundle,BuildTarget.StandaloneWindows64)==null)
                throw new InvalidDataException("Bundle build failed");
            uint crc;if(!BuildPipeline.GetCRCForAssetBundle(Path.Combine(archive,name),out crc))throw new InvalidDataException("CRC missing");
            string bundlePath=Path.Combine(output,name);
            var operation=AssetBundle.RecompressAssetBundleAsync(Path.Combine(archive,name),bundlePath,BuildCompression.LZ4Runtime,crc,UnityEngine.ThreadPriority.Normal);
            var deadline=DateTime.UtcNow.AddMinutes(12);
            while(!operation.isDone){if(DateTime.UtcNow>deadline)throw new TimeoutException("Recompression");System.Threading.Thread.Sleep(20);}
            if(operation.result!=AssetBundleLoadResult.Success)throw new InvalidDataException(operation.humanReadableResult);
            var loaded=AssetBundle.LoadFromFile(bundlePath,crc);
            if(loaded==null)throw new InvalidDataException("Bundle failed native CRC reload");
            try
            {
                var actual=loaded.LoadAsset<GameObject>("rebirth"+kind);
                int count=kind=="Bike"?2:4;
                if(actual==null || actual.GetComponentsInChildren<WheelCollider>(true).Length!=count)
                    throw new InvalidDataException("Bundled wheel contract failed");
                if(actual.GetComponentsInChildren<MonoBehaviour>(true).Any(x=>x!=null))throw new InvalidDataException("Runtime script leaked");
                string hash;using(var sha=SHA256.Create())using(var stream=File.OpenRead(bundlePath))hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                var receipt=new Receipt {status="BUNDLE_RELOADED_NOT_GAME_TESTED",kind=kind,bundle=bundlePath,sha256=hash,prefab=prefabPath,crc=crc,wheels=count,lights=actual.GetComponentsInChildren<Light>(true).Length};
                File.WriteAllText(Path.Combine(output,"receipt.json"),JsonUtility.ToJson(receipt,true));
            }
            finally{loaded.Unload(true);}
        }
    }
}
