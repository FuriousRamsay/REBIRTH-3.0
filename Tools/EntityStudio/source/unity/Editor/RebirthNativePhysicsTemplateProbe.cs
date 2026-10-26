using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthNativePhysicsTemplateProbe
    {
        public static void Run()
        {
            const string directory="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Addressables/Standalone/zombies_assets_entities/zombies/";
            var common=AssetBundle.LoadFromFile(directory+"common.bundle");var worker=AssetBundle.LoadFromFile(directory+"worker.bundle");
            try
            {
                if(worker==null)throw new InvalidOperationException("Native Worker bundle unavailable.");
                var prefab=worker.LoadAsset<GameObject>(worker.GetAllAssetNames().Single(path=>path.EndsWith("/ZWorker.prefab",StringComparison.OrdinalIgnoreCase)));
                var rows=new List<string>{"Native prefab hierarchy; Unity tag index mapping in Editor is not authoritative for game tags."};
                foreach(var node in prefab.GetComponentsInChildren<Transform>(true))
                {
                    string detail="";foreach(var collider in node.GetComponents<Collider>())
                    {
                        detail+=" collider="+collider.GetType().Name;
                        if(collider is CapsuleCollider capsule)detail+=" center="+capsule.center.ToString("F6")+" radius="+capsule.radius+" height="+capsule.height+" direction="+capsule.direction;
                        if(collider is BoxCollider box)detail+=" center="+box.center.ToString("F6")+" size="+box.size.ToString("F6");
                        if(collider is CharacterController character)detail+=" center="+character.center.ToString("F6")+" radius="+character.radius+" height="+character.height;
                    }
                    rows.Add(AnimationUtility.CalculateTransformPath(node,prefab.transform)+" scale="+node.localScale.ToString("F6")+" components="+string.Join(",",node.GetComponents<Component>().Where(value=>value!=null).Select(value=>value.GetType().Name))+detail);
                }
                string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/NativeWorkerPhysicsTemplate.txt");File.WriteAllLines(output,rows);Debug.Log("NATIVE PHYSICS TEMPLATE PROBE: "+output);
            }
            finally{if(worker!=null)worker.Unload(true);if(common!=null)common.Unload(true);}
        }
    }
}
