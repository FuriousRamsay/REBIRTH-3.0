using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthNativeEyeTextureInspect
    {
        public static void Run()
        {
            const string root="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Addressables/Standalone/zombies_assets_entities/zombies/";
            AssetBundle common=null,worker=null;
            try
            {
                common=AssetBundle.LoadFromFile(root+"common.bundle");worker=AssetBundle.LoadFromFile(root+"worker.bundle");
                if(worker==null)throw new IOException("Native worker reference bundle missing.");
                GameObject prefab=worker.LoadAsset<GameObject>(worker.GetAllAssetNames().Single(path=>path.EndsWith("/ZWorker.prefab",StringComparison.OrdinalIgnoreCase)));
                var eye=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(renderer=>renderer.name=="FeralEye");Material material=eye.sharedMaterial;
                var rows=new List<string>{"property,texture,width,height,nonblackPixels,meanR,meanG,meanB,maxR,maxG,maxB,emissionColor"};
                foreach(string property in new[]{"_MainTex","_EmissionMap"})
                {
                    Texture texture=material.GetTexture(property);if(texture==null)throw new InvalidDataException("Native eye texture unresolved: "+property);
                    RenderTexture target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);RenderTexture previous=RenderTexture.active;
                    var readback=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false,true);
                    try
                    {
                        Graphics.Blit(texture,target);RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0);readback.Apply();
                        long r=0,g=0,b=0;int count=0,mr=0,mg=0,mb=0;
                        foreach(Color32 pixel in readback.GetPixels32())
                        {if(Math.Max(pixel.r,Math.Max(pixel.g,pixel.b))<=2)continue;count++;r+=pixel.r;g+=pixel.g;b+=pixel.b;mr=Math.Max(mr,pixel.r);mg=Math.Max(mg,pixel.g);mb=Math.Max(mb,pixel.b);}
                        if(count==0)throw new InvalidDataException("Native eye map has no nonblack samples.");
                        Color emission=material.GetColor("_EmissionColor");
                        rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5:F6},{6:F6},{7:F6},{8},{9},{10},{11}",property,texture.name,texture.width,texture.height,count,r/(255.0*count),g/(255.0*count),b/(255.0*count),mr/255f,mg/255f,mb/255f,emission.ToString().Replace(',',';')));
                    }
                    finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(readback);}
                }
                string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/NativeCharacterReview/NativeFeralEyeTextureStats.csv");File.WriteAllLines(output,rows);Debug.Log("NATIVE EYE TEXTURE STATS: "+output+"; decoded linear GPU samples; native textures not exported.");
            }
            finally{if(worker!=null)worker.Unload(true);if(common!=null)common.Unload(true);}
        }
    }
}
