using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

// Editor-only authoring. All compound colliders share the existing vehicle
// rigidbody; native VehiclePart.modRBT controls their parent as one unit.
public static class RebirthCamperPloughCollision
{
    const string Data="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017/PloughCollision/collision-shapes.json";
    const string Folder="Assets/REBIRTHPloughCollisionReview_20261017";
    [Serializable] public class Shape { public string name; public Vector3[] vertices; public int[] triangles; }
    [Serializable] public class Shapes { public Shape[] shapes; }
    public static void Apply(GameObject vehicle,string assetFolder,string dataPath=null)
    {
        var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags=settings.FindProperty("tags");bool found=false;
        for(int i=0;i<tags.arraySize;i++)if(tags.GetArrayElementAtIndex(i).stringValue=="E_VehicleStrong")found=true;
        if(!found){tags.InsertArrayElementAtIndex(tags.arraySize);tags.GetArrayElementAtIndex(tags.arraySize-1).stringValue="E_VehicleStrong";settings.ApplyModifiedProperties();}
        var physics=vehicle.transform.Find("Physics");
        if(!physics)throw new Exception("Original vehicle Physics root missing");
        var previous=physics.Find("REBIRTHModPlowHull");
        if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var root=new GameObject("REBIRTHModPlowHull");root.transform.SetParent(physics,false);
        root.layer=21;root.tag="E_VehicleStrong";
        var input=JsonUtility.FromJson<Shapes>(File.ReadAllText(dataPath??Data));
        if(input.shapes.Length!=27)throw new Exception("Expected 12 blade bands, 7 spikes and 8 teeth");
        Directory.CreateDirectory(assetFolder);AssetDatabase.Refresh();
        foreach(var shape in input.shapes)
        {
            var child=new GameObject(shape.name);child.transform.SetParent(root.transform,false);child.layer=21;child.tag="E_VehicleStrong";
            var mesh=new Mesh {name=shape.name};mesh.vertices=shape.vertices;mesh.triangles=shape.triangles;mesh.RecalculateBounds();mesh.RecalculateNormals();
            string path=assetFolder+"/"+shape.name+".asset";
            if(AssetDatabase.LoadAssetAtPath<Mesh>(path))path=AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(mesh,path);
            var collider=child.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;collider.isTrigger=false;
            if(collider.bounds.size==Vector3.zero)throw new Exception("Empty collision shape "+shape.name);
        }
        // Never attach a second Rigidbody or a trigger-only damage proxy.
        root.SetActive(false);
    }
    public static void AuthorReview()
    {
        string path=Folder+"/rebirthCamper80s.prefab";
        if(File.Exists(path))throw new Exception("Review already exists; preserve it");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperArmorPlow_20261017_v4/rebirthCamper80s.prefab");
        if(!source)throw new Exception("Native mod-equipped source missing");
        var root=UnityEngine.Object.Instantiate(source);
        Apply(root,Folder+"/Collision");
        var group=root.transform.Find("Physics/REBIRTHModPlowHull");group.gameObject.SetActive(true);
        Physics.SyncTransforms();
        foreach(var collider in group.GetComponentsInChildren<MeshCollider>())
        {
            if(collider.isTrigger||!collider.convex||collider.gameObject.layer!=21||!collider.CompareTag("E_VehicleStrong"))throw new Exception("Invalid impact collider");
        }
        group.gameObject.SetActive(false);PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
        UnityEngine.Object.DestroyImmediate(root);
        File.WriteAllText(Path.GetDirectoryName(Data)+"/unity-collider-review.txt","PASS: 27 convex non-trigger shapes, native vehicle impact layer/tag and mod-controlled Physics parent. Review prefab only; not installed or gameplay-tested.");
    }
}

