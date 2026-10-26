using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Creates a separate authoring candidate from package vehicles. Original package
// scripts, physics and imported meshes are never edited or exported accidentally.
public static class RebirthStandardVehiclePreparation
{
    [Serializable] public class Receipt
    {
        public string source, candidate, status;
        public int bakedRenderers, removedScripts, removedPhysics;
    }
    public static void PrepareBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        string source = "", output = "", receiptPath = "";
        for (int i=0; i<args.Length-1; i++)
        {
            if(args[i]=="-vehicleSource") source=args[i+1];
            if(args[i]=="-vehicleOutput") output=args[i+1];
            if(args[i]=="-vehicleReceipt") receiptPath=args[i+1];
        }
        if(!Application.isBatchMode || !source.StartsWith("Assets/") ||
           !output.StartsWith("Assets/REBIRTHStandardVehiclesGenerated/") ||
           !output.EndsWith(".prefab") || File.Exists(output))
            throw new InvalidOperationException("Require batch mode, source asset and a new generated prefab path.");
        var original=AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if(original==null) throw new FileNotFoundException(source);
        string folder=Path.GetDirectoryName(output).Replace('\\','/');
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var copy=UnityEngine.Object.Instantiate(original);
        var receipt=new Receipt {source=source,candidate=output,status="PREPARED_NOT_RIGGED"};
        try
        {
            // Package demo controllers are incompatible with native game ownership.
            foreach(var script in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if(script!=null) {UnityEngine.Object.DestroyImmediate(script); receipt.removedScripts++;}
            foreach(var animator in copy.GetComponentsInChildren<Animator>(true))
                UnityEngine.Object.DestroyImmediate(animator);
            foreach(var joint in copy.GetComponentsInChildren<Joint>(true))
                UnityEngine.Object.DestroyImmediate(joint);
            foreach(var collider in copy.GetComponentsInChildren<Collider>(true))
                {UnityEngine.Object.DestroyImmediate(collider);receipt.removedPhysics++;}
            foreach(var body in copy.GetComponentsInChildren<Rigidbody>(true))
                {UnityEngine.Object.DestroyImmediate(body);receipt.removedPhysics++;}
            int index=0;
            foreach(var skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=new Mesh {name=skin.name+"_NativeRest"};
                skin.BakeMesh(mesh);
                if(mesh.vertexCount==0) throw new InvalidDataException("Empty baked mesh: "+skin.name);
                string meshPath=folder+"/mesh_"+(index++)+".asset";
                AssetDatabase.CreateAsset(mesh,meshPath);
                var host=skin.gameObject;
                var materials=skin.sharedMaterials;
                var shadows=skin.shadowCastingMode;
                var receive=skin.receiveShadows;
                var enabled=skin.enabled;
                UnityEngine.Object.DestroyImmediate(skin);
                host.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=host.AddComponent<MeshRenderer>();
                renderer.sharedMaterials=materials;
                renderer.shadowCastingMode=shadows;
                renderer.receiveShadows=receive;
                renderer.enabled=enabled;
                receipt.bakedRenderers++;
            }
            copy.name=Path.GetFileNameWithoutExtension(output);
            PrefabUtility.SaveAsPrefabAsset(copy,output);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(receiptPath));
            File.WriteAllText(receiptPath,JsonUtility.ToJson(receipt,true));
        }
        finally {UnityEngine.Object.DestroyImmediate(copy);}
    }
}
