using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RebirthStandardVehiclePaintAuthoring
{
    public static void BuildBothBatch()
    {
        var shader=Shader.Find("REBIRTH/StandardVehiclePaint");
        var artwork=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/REBIRTHCamperWrapGenerated/ArtworkCandidates/DiscoLower_v10.png");
        if(shader==null || artwork==null)throw new InvalidDataException("Paint shader/artwork missing");
        foreach(string kind in new[]{"Buggy","Bike"})
        {
            string source="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Lights_v2/rebirth"+kind+".prefab";
            string folder="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Paint_v2";
            string output=folder+"/rebirth"+kind+".prefab";
            if(File.Exists(output))throw new InvalidOperationException("Candidate exists");
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if(original==null)throw new FileNotFoundException(source);
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var root=UnityEngine.Object.Instantiate(original);int count=0;
            try
            {
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // Wheel assemblies reuse the bike body atlas: exclude them explicitly.
                    if(renderer.transform.IsChildOf(root.transform.Find("Wheel1")) ||
                       renderer.name.IndexOf("Wheel",StringComparison.OrdinalIgnoreCase)>=0 ||
                       renderer.name.IndexOf("_FL_",StringComparison.OrdinalIgnoreCase)>=0 ||
                       renderer.name.IndexOf("_FR_",StringComparison.OrdinalIgnoreCase)>=0 ||
                       renderer.name.IndexOf("_RL_",StringComparison.OrdinalIgnoreCase)>=0 ||
                       renderer.name.IndexOf("_RR_",StringComparison.OrdinalIgnoreCase)>=0)continue;
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++)
                    {
                        var originalMaterial=materials[i];
                        if(originalMaterial==null || originalMaterial.name.IndexOf("body",StringComparison.OrdinalIgnoreCase)<0)continue;
                        var material=new Material(originalMaterial);material.shader=shader;
                        material.SetColor("_Color",new Color(255,255,255,1));
                        material.SetColor("_DiscoCoat",new Color(.80f,.64f,.88f,1));
                        material.SetFloat("_BikeAtlas",kind=="Bike"?1:0);
                        material.SetTexture("_Artwork",artwork);material.SetFloat("_PatternEnabled",0);
                        AssetDatabase.CreateAsset(material,folder+"/Paint"+(count++)+".mat");materials[i]=material;
                    }
                    renderer.sharedMaterials=materials;
                }
                if(count==0)throw new InvalidDataException("No painted materials selected");
                root.name="rebirth"+kind;PrefabUtility.SaveAsPrefabAsset(root,output);AssetDatabase.SaveAssets();
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
