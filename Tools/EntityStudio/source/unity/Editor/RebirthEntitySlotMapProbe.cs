using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntitySlotMapProbe
    {
        public static void Run()
        {
            string key="EntityStudioSlotMapProbe_"+Guid.NewGuid().ToString("N");
            string folder="Assets/EntityStudioGenerated/"+key;
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();var material=new Material(Shader.Find("Standard"));var secondMaterial=new Material(Shader.Find("Standard"));
            try
            {
                if(AssetDatabase.IsValidFolder(folder)||Directory.Exists(folder))throw new IOException("Probe folder already exists.");
                Directory.CreateDirectory(folder);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                profile.entityClass=key;
                var maps=new EntityMaterialTextureOverride{sourceMaterial=material,mapName="Arm1",albedo=Input("albedo",new Color32(200,30,20,255)),normalMap=Input("normal",new Color32(128,128,255,255)),roughness=Input("rough",new Color32(128,128,128,255)),metallic=Input("metal",new Color32(64,64,64,255))};
                profile.materialTextureOverrides.Add(maps);
                var second=new EntityMaterialTextureOverride{sourceMaterial=secondMaterial,mapName="Arm2",albedo=Input("albedo2",new Color32(20,80,180,255)),normalMap=maps.normalMap,roughness=Input("rough2",new Color32(32,32,32,255)),metallic=Input("metal2",new Color32(240,240,240,255))};
                profile.materialTextureOverrides.Add(second);
                RebirthEntityTextureTools.ConfigureProfileMaps(profile);
                var normalImporter=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(maps.normalMap));
                var roughImporter=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(maps.roughness));
                var albedoImporter=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(maps.albedo));
                if(normalImporter.textureType!=TextureImporterType.NormalMap||roughImporter.sRGBTexture||!albedoImporter.sRGBTexture||!normalImporter.mipmapEnabled||!roughImporter.streamingMipmaps)throw new InvalidDataException("Slot texture import policy failed.");
                if(RebirthEntityMaterialMaps.FindOverride(profile,material)!=maps)throw new InvalidDataException("Slot material lookup failed.");
                if(RebirthEntityMaterialMaps.FindOverride(profile,secondMaterial)!=second)throw new InvalidDataException("Second slot material lookup failed.");
                var result=RebirthEntityMaterialMaps.GenerateSlotMaps(profile,maps);
                AssertPixels(result.normalRmoe,new Color32(128,64,255,0));AssertPixels(result.feralRmoe,new Color32(128,64,255,0));AssertPixels(result.specialRmoe,new Color32(128,64,255,255));
                AssertPixels(result.feralAlbedo,new Color32(187,26,17,255));AssertPixels(result.previewSmoothness,new Color32(64,0,0,127));AssertPixels(result.specialEmission,new Color32(255,255,255,255));
                var secondResult=RebirthEntityMaterialMaps.GenerateSlotMaps(profile,second);
                AssertPixels(secondResult.normalRmoe,new Color32(32,240,255,0));AssertPixels(secondResult.feralRmoe,new Color32(32,240,255,0));AssertPixels(secondResult.specialRmoe,new Color32(32,240,255,0));
                AssertPixels(secondResult.feralAlbedo,new Color32(18,70,153,255));AssertPixels(secondResult.previewSmoothness,new Color32(240,0,0,223));AssertPixels(secondResult.specialEmission,new Color32(0,0,0,255));
                profile.materialTextureOverrides.Add(maps);bool duplicateRejected=false;
                try{RebirthEntityMaterialMaps.FindOverride(profile,material);}catch(InvalidDataException){duplicateRejected=true;}
                if(!duplicateRejected)throw new InvalidDataException("Duplicate slot override was accepted.");
                VerifyConfigSlots(folder);
                Debug.Log("SLOT MAP PROBE PASSED: twelve owned4x4maps exactRGBA for two independent sources; import roles/mips verified; duplicate override rejected; five tier XML slot pairs/legacy fallback/partial binding rejection verified; no saved Farmer assets changed.");
            }
            finally
            {
                if(AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
                UnityEngine.Object.DestroyImmediate(profile);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(secondMaterial);
            }
            Texture2D Input(string name,Color32 color)
            {
                var image=new Texture2D(4,4,TextureFormat.RGBA32,false,true);
                string path=folder+"/"+name+".png";
                try{image.SetPixels32(Enumerable.Repeat(color,16).ToArray());image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(image);}
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }
        static void VerifyConfigSlots(string folder)
        {
            string[] tiers={"Normal","Feral","Radiated","Charged","Infernal"};
            string path=folder+"/vanilla-fixture.xml";
            var vanilla=new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("entity_classes",tiers.Select(tier=>new System.Xml.Linq.XElement("entity_class",new System.Xml.Linq.XAttribute("name","zombieUtilityWorker"+(tier=="Normal"?"":tier)),new System.Xml.Linq.XElement("property",new System.Xml.Linq.XAttribute("name","Tags"),new System.Xml.Linq.XAttribute("value","zombie,allbiome"))))));
            vanilla.Save(path);
            var receipt=new RebirthEntityBundleReceipt{assetName="RebirthFarmer",bundleReference="fixture?RebirthFarmer",physicsBodyName="RebirthFarmer",tierMaterialReferences=tiers.Select(t=>"fixture?Body_"+t+"_Slot0 67").ToArray(),extraTextureReferences=new[]{"Normal","Feral","Special"}.Select(m=>"fixture?RebirthFarmer_RMOE_"+m).ToArray()};
            var legacy=System.Xml.Linq.XDocument.Parse(RebirthFarmerEntityConfig.BuildPatch(receipt,path));
            if(legacy.Descendants("property").Any(p=>((string)p.Attribute("name")).StartsWith("RebirthEntityStudioMaterialSlot",StringComparison.Ordinal)))throw new InvalidDataException("Legacy configuration gained slot overrides.");
            receipt.tierMaterialReferences=receipt.tierMaterialReferences.Concat(tiers.SelectMany(t=>new[]{1,2}.Select(s=>"fixture?Body_"+t+"_Slot"+s+" 67"))).ToArray();
            receipt.extraTextureReferences=receipt.extraTextureReferences.Concat(new[]{1,2}.SelectMany(s=>new[]{"Normal","Feral","Special"}.Select(m=>"fixture?RebirthFarmer_Arm"+s+"_RMOE_"+m))).ToArray();
            var xml=System.Xml.Linq.XDocument.Parse(RebirthFarmerEntityConfig.BuildPatch(receipt,path));
            foreach(string tier in tiers)
            {
                var entity=xml.Descendants("entity_class").Single(e=>(string)e.Attribute("name")=="RebirthFarmer"+(tier=="Normal"?"":tier));
                foreach(int slot in new[]{1,2})
                {
                    string material=(string)entity.Elements("property").Single(p=>(string)p.Attribute("name")=="RebirthEntityStudioMaterialSlot"+slot).Attribute("value");
                    string map=(string)entity.Elements("property").Single(p=>(string)p.Attribute("name")=="RebirthEntityStudioRmoeMapSlot"+slot).Attribute("value");
                    string mask=tier=="Normal"?"Normal":tier=="Feral"?"Feral":"Special";
                    if(material!="fixture?Body_"+tier+"_Slot"+slot+" 67"||map!="fixture?RebirthFarmer_Arm"+slot+"_RMOE_"+mask)throw new InvalidDataException("Tier slot pair did not match its own atlas.");
                }
            }
            receipt.extraTextureReferences=receipt.extraTextureReferences.Where(p=>!p.EndsWith("?RebirthFarmer_Arm2_RMOE_Special",StringComparison.Ordinal)).ToArray();
            bool rejected=false;try{RebirthFarmerEntityConfig.BuildPatch(receipt,path);}catch(InvalidOperationException){rejected=true;}
            if(!rejected)throw new InvalidDataException("Incomplete slot binding was accepted.");
        }
        static void AssertPixels(Texture2D texture,Color32 expected)
        {
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try
            {
                if(texture==null||!image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(texture))))throw new InvalidDataException("Probe output missing.");
                foreach(var pixel in image.GetPixels32())if(!pixel.Equals(expected))throw new InvalidDataException("Unexpected packed pixel on "+texture.name+": "+pixel+" expected "+expected);
            }
            finally{UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
