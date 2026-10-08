using System;
using UnityEngine;

#nullable disable

/// <summary>Game-thread, text-only native SDCS catalogue qualification; never loads meshes.</summary>
public static class RebirthSdcsCatalogService
{
    private static string loadedFingerprint;
    public static void EnsureVisualCatalog() { QualifyTextCatalog(); }

    internal static string QualifyTextCatalog()
    {
        // Resources("sdcs") is the native SetupData input on clients and dedicated.
        // Dedicated installations without that resource refuse; no client authority fallback.
        TextAsset asset=Resources.Load("sdcs") as TextAsset;
        if(asset==null||string.IsNullOrWhiteSpace(asset.text)||asset.text.Length>4*1024*1024)
            throw new InvalidOperationException("Installed native SDCS text catalogue unavailable or oversized.");
        string fingerprint;
        using(var hash=System.Security.Cryptography.SHA256.Create())
            fingerprint="sha256:"+BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(asset.text))).Replace("-","").ToLowerInvariant();
        if(loadedFingerprint!=fingerprint || !HasDefinitions())
        {
            SDCSDataUtils.SetupData();
            if(!HasDefinitions()) throw new InvalidOperationException("Native SDCS text catalogue has no usable race/eye definitions.");
            loadedFingerprint=fingerprint;
        }
        return fingerprint;
    }
    private static bool HasDefinitions()
    {
        return SDCSDataUtils.VariantData!=null && SDCSDataUtils.VariantData.Count>0
            && SDCSDataUtils.GetRaceList(true).Count>0 && SDCSDataUtils.GetRaceList(false).Count>0
            && SDCSDataUtils.EyeColorList!=null && SDCSDataUtils.EyeColorList.Count>0;
    }
}