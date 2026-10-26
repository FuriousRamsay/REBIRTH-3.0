using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RebirthStandardVehicleFinalize
{
    public static void CorrectBuggySteeringBatch()
    {
        const string output="Assets/REBIRTHStandardVehiclesGenerated/BuggyFinal_v2/rebirthBuggy.prefab";
        if(File.Exists(output))throw new InvalidOperationException("Preserve candidate; choose a new version");
        var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHStandardVehiclesGenerated/BuggyFinal_v1/rebirthBuggy.prefab");
        var root=UnityEngine.Object.Instantiate(original);
        try
        {
            root.name="rebirthBuggy";
            var steering=root.transform.Find("M/Steering");
            var children=new Transform[steering.childCount];
            for(int i=0;i<children.Length;i++)children[i]=steering.GetChild(i);
            foreach(var child in children)child.SetParent(root.transform,true);
            // Baked rim PCA normal and outer-ring circle fit, not the shaft's bounds center.
            steering.localPosition=new Vector3(-.43700595f,1.67902236f,.27055104f);
            steering.localRotation=Quaternion.LookRotation(new Vector3(.00000099f,-.35277082f,.93570976f));
            foreach(var child in children)child.SetParent(steering,true);
            Directory.CreateDirectory(Path.GetDirectoryName(output));AssetDatabase.Refresh();
            PrefabUtility.SaveAsPrefabAsset(root,output);AssetDatabase.SaveAssets();
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        RebirthStandardVehicleMotionReview.ReviewBatch();
    }
    public static void PrepareBothBatch()
    {
        foreach(string kind in new[]{"Buggy","Bike"})
        {
            bool bike=kind=="Bike";
            string path="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Paint_v2/rebirth"+kind+".prefab";
            string output="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Final_v1/rebirth"+kind+".prefab";
            if(File.Exists(output))throw new InvalidOperationException("New candidate required");
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(original==null)throw new FileNotFoundException(path);
            var root=UnityEngine.Object.Instantiate(original);
            try
            {
                root.name="rebirth"+kind;root.layer=14;
                var interaction=root.AddComponent<BoxCollider>();
                interaction.center=bike?new Vector3(0,.9f,.15f):new Vector3(0,1.3f,.3f);
                interaction.size=bike?new Vector3(.8f,1.5f,2.4f):new Vector3(2.4f,2,5.5f);
                var physics=root.transform.Find("Physics");
                if(physics==null || physics.tag!="Physics" || physics.GetComponent<Rigidbody>()==null)
                    throw new InvalidDataException("Physics contract missing");
                for(int i=0;i<(bike?2:4);i++)
                    if(physics.Find("Wheel"+i)?.GetComponent<WheelCollider>()==null)
                        throw new InvalidDataException("Native physics wheel missing");
                if(!bike)
                {
                    var steering=root.transform.Find("M/Steering");
                    var children=new Transform[steering.childCount];
                    for(int i=0;i<children.Length;i++)children[i]=steering.GetChild(i);
                    foreach(var child in children)child.SetParent(root.transform,true);
                    steering.localPosition=new Vector3(-.437f,1.66f,.55f);
                    steering.localRotation=Quaternion.LookRotation(new Vector3(0,-.223f,.386f));
                    foreach(var child in children)child.SetParent(steering,true);
                }
                foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if(script!=null)throw new InvalidDataException("Unexpected runtime component");
                Directory.CreateDirectory(Path.GetDirectoryName(output));AssetDatabase.Refresh();
                PrefabUtility.SaveAsPrefabAsset(root,output);AssetDatabase.SaveAssets();
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
