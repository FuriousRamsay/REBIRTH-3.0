using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Motion hierarchy authoring only; physics/occupant/light validation is separate.
public static class RebirthStandardVehicleMotion
{
    [Serializable] public class Binding { public string sourcePath, targetPath; public Vector3 pivot; }
    [Serializable] public class Plan { public string sourcePrefab, outputPrefab; public Binding[] bindings; }
    public static void AssembleBatch()
    {
        string planPath="";
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i<args.Length-1;i++) if(args[i]=="-vehicleMotionPlan") planPath=args[i+1];
        var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(planPath));
        if(!Application.isBatchMode || !plan.outputPrefab.StartsWith("Assets/REBIRTHStandardVehiclesGenerated/") ||
           File.Exists(plan.outputPrefab)) throw new InvalidOperationException("New generated output required.");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(plan.sourcePrefab);
        if(source==null) throw new FileNotFoundException(plan.sourcePrefab);
        var root=new GameObject(Path.GetFileNameWithoutExtension(plan.outputPrefab));
        try
        {
            var model=UnityEngine.Object.Instantiate(source,root.transform);
            model.name="Visuals";
            // Resolve all bindings before modifying geometry hierarchy.
            var visuals=new Transform[plan.bindings.Length];
            for(int i=0;i<visuals.Length;i++)
            {
                visuals[i]=model.transform.Find(plan.bindings[i].sourcePath);
                if(visuals[i]==null || visuals[i].GetComponent<MeshFilter>()==null)
                    throw new InvalidDataException("Missing prepared mesh: "+plan.bindings[i].sourcePath);
            }
            for(int i=0;i<visuals.Length;i++)
            {
                var binding=plan.bindings[i];
                var pivot=Ensure(root.transform,binding.targetPath);
                pivot.position=root.transform.TransformPoint(binding.pivot);
                // Preserve rest shape while moving its transform into the native pivot.
                visuals[i].SetParent(pivot,true);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(plan.outputPrefab));
            AssetDatabase.Refresh();
            PrefabUtility.SaveAsPrefabAsset(root,plan.outputPrefab);
            AssetDatabase.SaveAssets();
        }
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }
    static Transform Ensure(Transform root,string path)
    {
        var current=root;
        foreach(string segment in path.Split('/'))
        {
            if(string.IsNullOrWhiteSpace(segment)||segment=="..") throw new InvalidDataException(path);
            var child=current.Find(segment);
            if(child==null) {child=new GameObject(segment).transform;child.SetParent(current,false);}
            current=child;
        }
        return current;
    }
}
