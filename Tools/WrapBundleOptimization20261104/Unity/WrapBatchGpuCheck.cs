using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class WrapBatchGpuCheck
{
    [Serializable] public class Plan { public string directory; public string[] bundles; }
    [Serializable] public class TextureRow {public string bundle,name,format,pixelHash;public int width,height,mips;}
    [Serializable] public class BundleRow {public string name,sha256;}
    [Serializable] public class Receipt {public string editor,gpu,status;public TextureRow[] textures;public BundleRow[] bundles;}
    static string Digest(string path)
    {
        using(var stream=File.OpenRead(path))using(var hash=System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }
    public static void Run()
    {
        try
        {
            var args=Environment.GetCommandLineArgs();string path=args[Array.IndexOf(args,"-gpuPlan")+1];
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(path));var rows=new List<TextureRow>();var bundles=new List<BundleRow>();
            foreach(string name in plan.bundles)
            {
                string file=Path.Combine(plan.directory,name);string digest=Digest(file);
                var bundle=AssetBundle.LoadFromFile(file);
                if(!bundle)throw new Exception("Bundle open failed "+name);
                var textures=bundle.LoadAllAssets<Texture2D>();
                if(textures.Length!=13)throw new Exception("Unexpected native texture count "+name);
                foreach(var texture in textures)
                {
                    if(texture.format!=TextureFormat.BC7||texture.width%4!=0||texture.height%4!=0||texture.mipmapCount<1)
                        throw new Exception("Invalid BC7 native texture "+name+" "+texture.name);
                    var rt=RenderTexture.GetTemporary(128,128,0,RenderTextureFormat.ARGB32);
                    Graphics.Blit(texture,rt);var prior=RenderTexture.active;RenderTexture.active=rt;
                    var read=new Texture2D(128,128,TextureFormat.RGBA32,false);read.ReadPixels(new Rect(0,0,128,128),0,0);read.Apply();
                    var pixels=read.GetPixels32();bool any=false;
                    foreach(var pixel in pixels)if(pixel.a>0&&(pixel.r>0||pixel.g>0||pixel.b>0)){any=true;break;}
                    if(!any)throw new Exception("Blank GPU readback "+name+" "+texture.name);
                    rows.Add(new TextureRow{bundle=name,name=texture.name,format=texture.format.ToString(),width=texture.width,
                        height=texture.height,mips=texture.mipmapCount,pixelHash=Hash128.Compute(read.GetRawTextureData()).ToString()});
                    RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(read);
                }
                bundle.Unload(true);if(digest!=Digest(file))throw new Exception("Bundle changed during GPU check "+name);
                bundles.Add(new BundleRow{name=name,sha256=digest});Debug.Log("BC7_GPU_PASS "+name);
            }
            File.WriteAllText(path+".receipt.json",JsonUtility.ToJson(new Receipt{editor=Application.unityVersion,
                gpu=SystemInfo.graphicsDeviceName,status="PASS",textures=rows.ToArray(),bundles=bundles.ToArray()},true));
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
