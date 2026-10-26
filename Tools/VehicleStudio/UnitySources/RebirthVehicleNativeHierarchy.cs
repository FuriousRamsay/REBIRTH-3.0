using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Matches EntityFactory.CompleteEntity: entity component belongs on GameObject;
// visual and Physics transforms must be siblings under the neutral prefab root.
public static class RebirthVehicleNativeHierarchy
{
    public static void Run()
    {
        foreach(string kind in new[]{"Bike","Buggy"})
        {
            string name="rebirth"+kind;
            string source="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Collision_v1/"+name+".prefab";
            string output="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"NativeHierarchy_v1/"+name+".prefab";
            if(File.Exists(output)) throw new InvalidOperationException("Preserve existing candidate "+output);
            var root=new GameObject(name);
            try
            {
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source),root.transform,false);
                model.name="GameObject";
                var physics=model.transform.Find("Physics");
                if(physics==null || physics.GetComponent<Rigidbody>()==null) throw new InvalidDataException("Physics missing");
                physics.SetParent(root.transform,false);
                if(model.transform.IsChildOf(physics) || physics.IsChildOf(model.transform)) throw new InvalidDataException("Physics/visual separation failed");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                AssetDatabase.Refresh();
                PrefabUtility.SaveAsPrefabAsset(root,output);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        AssetDatabase.SaveAssets();
    }
}
