using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthNativeZombieBundleInspect
    {
        public static void Run()
        {
            AssetBundle bundle=null;
            var dependencies=new List<AssetBundle>();
            try
            {
                string path="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Addressables/Standalone/zombies_assets_entities/zombies/worker.bundle";
                string standalone=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),"../.."));
                foreach(string dependency in Directory.GetFiles(standalone,"*unitybuiltinshaders.bundle"))LoadDependency(dependency);
                LoadDependency(Path.Combine(standalone,"shaders_assets_all.bundle"));
                LoadDependency(Path.Combine(standalone,"zombies_assets_entities/zombies/common.bundle"));
                bundle=AssetBundle.LoadFromFile(path);if(bundle==null)throw new InvalidDataException("Native Worker bundle could not be read by this Editor.");
                var rows=new List<string>{"Unity="+Application.unityVersion,"Bundle="+path};
                foreach(string asset in bundle.GetAllAssetNames())
                {
                    rows.Add("ASSET "+asset);
                    if(!asset.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))continue;
                    GameObject prefab=bundle.LoadAsset<GameObject>(asset);if(prefab==null){rows.Add("PREFAB_UNAVAILABLE");continue;}
                    foreach(var transform in prefab.GetComponentsInChildren<Transform>(true))
                    {
                        rows.Add("NODE "+AnimationUtility.CalculateTransformPath(transform,prefab.transform)+" tag="+transform.tag);
                        foreach(var collider in transform.GetComponents<Collider>())rows.Add("COLLIDER "+EditorJsonUtility.ToJson(collider));
                        foreach(var renderer in transform.GetComponents<SkinnedMeshRenderer>())foreach(var material in renderer.sharedMaterials)
                        {
                            rows.Add("MATERIAL "+(material==null?"missing":material.name+" shader="+(material.shader!=null?material.shader.name:"missing")));
                            if(material!=null)foreach(string property in new[]{"_Albedo","_Normal","_RMOE"})
                                if(material.HasProperty(property)){Texture texture=material.GetTexture(property);rows.Add("TEXTURE "+property+"="+(texture!=null?texture.name+" "+texture.width+"x"+texture.height:"missing"));}
                        }
                    }
                }
                string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs","EntityStudioValidation","NativeWorkerBundle.txt");File.WriteAllLines(output,rows);Debug.Log("NATIVE WORKER BUNDLE INSPECTION: "+output);
                void LoadDependency(string dependency){var loaded=AssetBundle.LoadFromFile(dependency);if(loaded==null)throw new InvalidDataException("Native shader dependency unavailable: "+dependency);dependencies.Add(loaded);}
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
            finally{if(bundle!=null)bundle.Unload(true);for(int i=dependencies.Count-1;i>=0;i--)dependencies[i].Unload(true);}
        }
    }
}
