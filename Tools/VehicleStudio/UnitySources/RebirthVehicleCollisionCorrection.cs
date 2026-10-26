using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RebirthVehicleCollisionCorrection
{
    public static void Run()
    {
        foreach(string kind in new[]{"Bike","Buggy"})
        {
            string source="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Final_v"+(kind=="Bike"?"1":"2")+"/rebirth"+kind+".prefab";
            string output="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Collision_v1/rebirth"+kind+".prefab";
            if(File.Exists(output)) throw new InvalidOperationException("Preserve existing candidate: "+output);
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));
            try
            {
                root.name="rebirth"+kind;
                var interaction=root.GetComponent<BoxCollider>();
                if(interaction==null) throw new InvalidDataException("Expected the audited root box");
                // Native VMotorcycleP/GameObject and VTruck4x4 donor selection boxes
                // are disabled; active colliders belong to the Physics rigidbody.
                // Preserve the box for inspection, but do not let it push its own body.
                interaction.enabled=false;
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                AssetDatabase.Refresh();
                PrefabUtility.SaveAsPrefabAsset(root,output);
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        AssetDatabase.SaveAssets();
    }
}
