using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Match the user's circled brown plate/grille reference, not a different steel.
// Only previously added detail-material assignments change. No mesh/UV edits.
public static class RebirthCamperBrownMetalMatch
{
    const string Folder="Assets/REBIRTHCamperBrownMetalMatch_20261021";
    const string Source="Assets/REBIRTHCamperApprovedArmor_20261018_v4/plates.mat";
    const string Prefab="Assets/REBIRTHPickupRearCornerCandidate_20261021_v1/rebirthCamper80s.prefab";
    public static void Run()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var original=AssetDatabase.LoadAssetAtPath<Material>(Source);
        if(!original)throw new Exception("Missing exact approved brown plates material");
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/BrownReferenceSteel.mat");
        bool create=!material;if(create)material=new Material(original){name="Camper brown reference metal detail"};
        var audit=new List<string>{"Reference="+Source,"Normalized rectangle=(5,195,160,160)/2048; no resampling or color changes; source albedo160px spans0.15625m=1024px/m"};
        foreach(string property in new[]{"_MainTex","_BumpMap","_MetallicGlossMap","_OcclusionMap"})
        {
            var source=original.GetTexture(property) as Texture2D;if(!source)throw new Exception("Missing source map "+property);
            string path=AssetDatabase.GetAssetPath(source);
            var decoded=new Texture2D(2,2,TextureFormat.RGBA32,false,property!="_MainTex");
            if(!decoded.LoadImage(File.ReadAllBytes(path)))throw new Exception("Cannot decode reference map "+path);
            // Keep maps aligned even if an auxiliary source has a different resolution.
            int x=Mathf.RoundToInt(decoded.width*5f/2048f),y=Mathf.RoundToInt(decoded.height*195f/2048f);
            int w=Mathf.RoundToInt(decoded.width*160f/2048f),h=Mathf.RoundToInt(decoded.height*160f/2048f);
            var crop=new Texture2D(w,h,TextureFormat.RGBA32,false,property!="_MainTex");
            crop.SetPixels(decoded.GetPixels(x,y,w,h));crop.Apply();
            string output=Folder+"/BrownReference"+property+".png";File.WriteAllBytes(output,crop.EncodeToPNG());
            Object.DestroyImmediate(decoded);Object.DestroyImmediate(crop);
            AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(output);
            importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=16;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;
            importer.npotScale=TextureImporterNPOTScale.None;importer.sRGBTexture=property=="_MainTex";
            if(property=="_BumpMap")importer.textureType=TextureImporterType.NormalMap;
            importer.SaveAndReimport();
            material.SetTexture(property,AssetDatabase.LoadAssetAtPath<Texture2D>(output));
            material.SetTextureScale(property,Vector2.one*(2048f/160f));material.SetTextureOffset(property,Vector2.zero);
            audit.Add(property+" source="+path+" exactPixelRect="+x+","+y+","+w+","+h);
        }
        if(create)AssetDatabase.CreateAsset(material,Folder+"/BrownReferenceSteel.mat");
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));root.name="rebirthCamper80s";int count=0;
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var materials=renderer.sharedMaterials;bool changed=false;
            for(int i=0;i<materials.Length;i++)
                if(materials[i]&&AssetDatabase.GetAssetPath(materials[i])=="Assets/REBIRTHCamperSteelSurfaceDetail_20261021/FullMapSteel.mat")
                {materials[i]=material;changed=true;}
            if(changed){renderer.sharedMaterials=materials;count++;audit.Add("changed="+renderer.name);}
        }
        if(count!=122)throw new Exception("Expected122 previously approved detail renderers, found"+count+"; no prefab saved");
        PrefabUtility.SaveAsPrefabAsset(root,Prefab);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
        File.WriteAllLines(Folder+"/brown-reference-audit.txt",audit);
        Debug.Log("REBIRTH brown reference match: "+count+" renderers, original plate properties and pixel colors preserved");
    }
}
