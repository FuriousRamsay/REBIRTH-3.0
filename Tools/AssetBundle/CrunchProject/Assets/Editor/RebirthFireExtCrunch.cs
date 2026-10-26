using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RebirthFireExtCrunch
{
    [Serializable] public class Entry {public int id,width,height,format,mips;public bool srgb;}
    [Serializable] public class Plan {public Entry[] textures;}
    [Serializable] public class Result {public Entry[] textures;public string editor;}
    public static void Run()
    {
        try
        {
            string root=Directory.GetParent(Application.dataPath).FullName;
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(root,"plan.json")));
            string output=Path.Combine(root,"CrunchOutput");Directory.CreateDirectory(output);
            foreach(var entry in plan.textures)
            {
                string path="Assets/Textures/T_"+entry.id+".png";
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;
                importer.sRGBTexture=entry.srgb;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency=false;
                importer.npotScale=TextureImporterNPOTScale.None;
                importer.mipmapEnabled=entry.mips>1;
                importer.mipmapFilter=TextureImporterMipFilter.BoxFilter;
                importer.isReadable=true;
                importer.streamingMipmaps=false;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.crunchedCompression=true;
                importer.compressionQuality=100;
                var platform=importer.GetPlatformTextureSettings("Standalone");
                platform.name="Standalone";platform.overridden=true;
                platform.maxTextureSize=Math.Max(32,Math.Max(entry.width,entry.height));
                platform.format=entry.format==28?TextureImporterFormat.DXT1Crunched:TextureImporterFormat.DXT5Crunched;
                platform.textureCompression=TextureImporterCompression.CompressedHQ;
                platform.crunchedCompression=true;platform.compressionQuality=100;
                importer.SetPlatformTextureSettings(platform);importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if(texture==null||texture.width!=entry.width||texture.height!=entry.height||texture.mipmapCount!=entry.mips||(int)texture.format!=entry.format)
                    throw new InvalidDataException("Unexpected imported Crunch image: "+entry.id+" / "+(texture==null?"null":texture.format+" "+texture.mipmapCount));
                File.WriteAllBytes(Path.Combine(output,entry.id+".bin"),texture.GetRawTextureData());
                Debug.Log("[FIREEXT CRUNCH] "+entry.id+" "+texture.width+"x"+texture.height+" "+texture.format);
            }
            File.WriteAllText(Path.Combine(output,"receipt.json"),JsonUtility.ToJson(new Result{textures=plan.textures,editor=Application.unityVersion},true));
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
