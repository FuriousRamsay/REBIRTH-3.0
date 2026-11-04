using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WrapComparisonGpuCheck
{
    public static void Run()
    {
        var loaded=new List<AssetBundle>();
        try
        {
            var args=Environment.GetCommandLineArgs();string directory=args[Array.IndexOf(args,"-comparisonDirectory")+1];
            string original=args[Array.IndexOf(args,"-comparisonOriginal")+1];
            var baseline=AssetBundle.LoadFromFile(original);if(!baseline)throw new Exception("Original unavailable");loaded.Add(baseline);
            var lines=new List<string>{Application.unityVersion+" "+SystemInfo.graphicsDeviceName};
            foreach(string percent in new[]{"100","75","50","35"})
            {
                string id="NavezganeExplorerCompare"+percent;
                var bundle=AssetBundle.LoadFromFile(Path.Combine(directory,"rebirth_wrap_"+id.ToLowerInvariant()+".unity3d"));
                if(!bundle)throw new Exception("Concurrent bundle load failed "+id);loaded.Add(bundle);
                string marker="assets/rebirthcamperwrapcollection/contracts/"+id.ToLowerInvariant()+"-body-layout.json";
                if(!bundle.Contains(marker))throw new Exception("Missing independent contract path "+id);
                var textures=bundle.LoadAllAssets<Texture2D>();if(textures.Length!=13)throw new Exception("Incomplete texture set "+id);
                foreach(var texture in textures)
                {
                    if(texture.format!=TextureFormat.BC7||texture.width%4!=0||texture.height%4!=0)throw new Exception("Invalid runtime format "+texture.name);
                    var rt=RenderTexture.GetTemporary(128,128,0,RenderTextureFormat.ARGB32);
                    Graphics.Blit(texture,rt);var prior=RenderTexture.active;RenderTexture.active=rt;
                    var read=new Texture2D(128,128,TextureFormat.RGBA32,false);read.ReadPixels(new Rect(0,0,128,128),0,0);read.Apply();
                    lines.Add(id+"\t"+texture.name+"\t"+texture.width+"x"+texture.height+"\t"+texture.mipmapCount+"\t"+Hash128.Compute(read.GetRawTextureData()));
                    RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(read);
                }
            }
            lines.Add("PASS original and all four copies loaded concurrently; all 52 textures GPU rendered.");
            File.WriteAllLines(Path.Combine(Directory.GetParent(directory).Parent.FullName,"gpu-comparison-PASS.txt"),lines);
            foreach(var bundle in loaded)bundle.Unload(true);EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);foreach(var bundle in loaded)if(bundle)bundle.Unload(true);EditorApplication.Exit(1);}
    }
}
