using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WrapGpuReview
{
    static string Arg(string key)
    {
        var args=Environment.GetCommandLineArgs();return args[Array.IndexOf(args,key)+1];
    }
    public static void Run()
    {
        try
        {
            string output=Arg("-reviewOutput");Directory.CreateDirectory(output);
            PlayerSettings.colorSpace=ColorSpace.Linear;
            foreach(string profile in new[]{"original","candidate"})
            {
                var bundle=AssetBundle.LoadFromFile(Arg("-"+profile));
                if(!bundle)throw new Exception("Cannot open "+profile);
                string dir=Path.Combine(output,profile);Directory.CreateDirectory(dir);
                using(var log=new StreamWriter(Path.Combine(dir,"textures.tsv")))
                foreach(var texture in bundle.LoadAllAssets<Texture2D>())
                {
                    if(profile=="candidate"&&texture.format!=TextureFormat.BC7)throw new Exception("Wrong format "+texture.name);
                    var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                    Graphics.Blit(texture,rt);
                    var prior=RenderTexture.active;RenderTexture.active=rt;
                    var readback=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false,false);
                    readback.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);readback.Apply();
                    File.WriteAllBytes(Path.Combine(dir,texture.name+".png"),readback.EncodeToPNG());
                    log.WriteLine(texture.name+"\t"+texture.width+"\t"+texture.height+"\t"+texture.format+"\t"+texture.mipmapCount);
                    RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(readback);
                }
                bundle.Unload(true);
            }
            File.WriteAllText(Path.Combine(output,"GPU_LOAD_PASS.txt"),Application.unityVersion+" "+SystemInfo.graphicsDeviceName);
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
