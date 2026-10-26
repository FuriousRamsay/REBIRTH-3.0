using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

public static partial class RebirthCamperArmorReference
{
    const string MaterialSources="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Tools/VehicleStudio/AddonSources/ArmorPlow/ReferenceTextures_20261017";
    public static void AuthorTexturedReference() { Author(); ApplyReferenceMaterials(); }
    static void CloseView(Camera camera,string label,Vector3 eye,Vector3 target,float size)
    {
        camera.transform.position=eye;camera.transform.LookAt(target);camera.orthographicSize=size;
        var rt=new RenderTexture(1600,1100,24);var image=new Texture2D(1600,1100,TextureFormat.RGB24,false);
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();
        File.WriteAllBytes(Output+"/"+label+".png",image.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
    }
    public static void ApplyReferenceMaterials()
    {
        // Keep the already-reviewed clay scene/meshes. Material edits only.
        ApplySurface("OxidizedBareSteel","Clay steel");
        ApplySurface("ChippedOchreSteel","Clay plate");
        var hardware=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Clay hardware.mat");
        var steel=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Clay steel.mat");
        hardware.CopyPropertiesFromMaterial(steel);hardware.color=new Color(.85f,.80f,.72f);hardware.SetFloat("_BumpScale",.18f);EditorUtility.SetDirty(hardware);
        AssetDatabase.SaveAssets();Build();
    }
    static void ApplySurface(string file,string materialName)
    {
        string path=Folder+"/"+file+"_Albedo.png";
        File.Copy(MaterialSources+"/"+file+".png",path,true);AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture=true;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.ToSmaller;
        importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Compressed;importer.crunchedCompression=true;importer.compressionQuality=90;importer.SaveAndReimport();
        var raw=new Texture2D(2,2,TextureFormat.RGB24,false);raw.LoadImage(File.ReadAllBytes(path));
        int n=raw.width>=2048?2048:raw.width>=1024?1024:Mathf.ClosestPowerOfTwo(raw.width/2);
        var normals=new Color32[n*n];
        Func<float,float,float> height=(u,v)=>{
            var c=raw.GetPixelBilinear(Mathf.Repeat(u,1),Mathf.Repeat(v,1));
            float corrosion=Mathf.Clamp01((c.r-c.g)*4);
            return c.grayscale*.35f-corrosion*.10f;
        };
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float u=(x+.5f)/n,v=(y+.5f)/n,d=1f/n;
            var normal=new Vector3((height(u-d,v)-height(u+d,v))*3,(height(u,v-d)-height(u,v+d))*3,1).normalized;
            normals[y*n+x]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f);
        }
        SaveTexture(file+"_Normal",normals,n,true);
        var finish=new Color32[256*256];var ao=new Color32[finish.Length];
        for(int y=0;y<256;y++)for(int x=0;x<256;x++)
        {
            var c=raw.GetPixelBilinear((x+.5f)/256,(y+.5f)/256);
            float rust=Mathf.Clamp01((c.r-c.g)*4),painted=file.StartsWith("Chipped")?Mathf.Clamp01((c.g-c.b)*4):0;
            float metal=Mathf.Lerp(.48f,.035f,Mathf.Max(rust,painted));float smoothness=Mathf.Lerp(.24f,.09f,Mathf.Max(rust,painted));
            finish[y*256+x]=new Color(metal,metal,metal,smoothness);float ambient=Mathf.Lerp(.98f,.78f,rust);ao[y*256+x]=new Color(ambient,ambient,ambient);
        }
        SaveTexture(file+"_MetallicSmoothness",finish,256,false,true);SaveTexture(file+"_AO",ao,256,false,true);Object.DestroyImmediate(raw);
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+materialName+".mat");
        material.color=Color.white;material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+file+"_Normal.png"));material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.45f);
        material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+file+"_MetallicSmoothness.png"));material.EnableKeyword("_METALLICGLOSSMAP");
        material.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+file+"_AO.png"));material.SetFloat("_GlossMapScale",1);EditorUtility.SetDirty(material);
    }
}
