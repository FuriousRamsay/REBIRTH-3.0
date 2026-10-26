using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Four-wheel donor baseline previously measured from the target game's 4x4.
// Motorcycle physics must use its own measured donor, never this baseline.
public static class RebirthStandardVehiclePhysics
{
    public static void BuildBuggyBatch()
    { Build(false); }
    public static void BuildBikeBatch()
    { Build(true); }
    static void Build(bool bike)
    {
        string sourcePath=bike?"Assets/REBIRTHStandardVehiclesGenerated/BikeMotion_v1/rebirthBike.prefab":"Assets/REBIRTHStandardVehiclesGenerated/BuggyMotion_v2/rebirthBuggy.prefab";
        string output=bike?"Assets/REBIRTHStandardVehiclesGenerated/BikePhysics_v1/rebirthBike.prefab":"Assets/REBIRTHStandardVehiclesGenerated/BuggyPhysics_v1/rebirthBuggy.prefab";
        if(!Application.isBatchMode || File.Exists(output)) throw new InvalidOperationException("New batch candidate required");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if(source==null) throw new FileNotFoundException(sourcePath);
        var root=UnityEngine.Object.Instantiate(source);
        try
        {
            root.name=bike?"rebirthBike":"rebirthBuggy";
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var array=tags.FindProperty("tags");bool found=false;
            for(int i=0;i<array.arraySize;i++) if(array.GetArrayElementAtIndex(i).stringValue=="Physics") found=true;
            if(!found) {array.InsertArrayElementAtIndex(array.arraySize);array.GetArrayElementAtIndex(array.arraySize-1).stringValue="Physics";tags.ApplyModifiedProperties();}
            var physics=new GameObject("Physics");physics.transform.SetParent(root.transform,false);
            physics.tag="Physics";physics.layer=21;
            var body=physics.AddComponent<Rigidbody>();body.mass=bike?250:2400;body.useGravity=bike;
            body.drag=bike?.02f:.05f;body.angularDrag=.05f;
            if(bike)body.ResetCenterOfMass();else body.centerOfMass=new Vector3(0,.1f,0);
            body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=bike?CollisionDetectionMode.Discrete:CollisionDetectionMode.ContinuousDynamic;
            for(int i=0;i<(bike?2:4);i++)
            {
                var visual=root.transform.Find(bike&&i==0?"M/Forks/Wheel0":"Wheel"+i);
                if(visual==null) throw new InvalidDataException("Wheel missing");
                var mesh=visual.GetComponentInChildren<MeshRenderer>();
                var wheel=new GameObject("Wheel"+i);wheel.transform.SetParent(physics.transform,false);
                wheel.transform.position=visual.position;wheel.layer=21;
                if(bike){var p=wheel.transform.localPosition;p.x=0;wheel.transform.localPosition=p;}
                var wc=wheel.AddComponent<WheelCollider>();wc.radius=Mathf.Max(mesh.bounds.extents.y,mesh.bounds.extents.z);
                wc.mass=bike?32:45;wc.suspensionDistance=bike?.25f:.43f;
                float target=bike?.8f:.5f;
                // Collider center compensates travel to align the nominal rest hub.
                wc.center=bike?Vector3.zero:new Vector3(0,wc.suspensionDistance*target,0);
                wc.suspensionSpring=new JointSpring {spring=bike?10000:15000,damper=2000,targetPosition=target};
                wc.forceAppPointDistance=bike?0:.2f;
                wc.forwardFriction=new WheelFrictionCurve {extremumSlip=.4f,extremumValue=1,asymptoteSlip=.8f,asymptoteValue=bike&&i==1?.7f:.5f,stiffness=bike?1.3f:1.8f};
                wc.sidewaysFriction=new WheelFrictionCurve {extremumSlip=.2f,extremumValue=1,asymptoteSlip=.75f,asymptoteValue=.75f,stiffness=bike&&i==1?1.3f:1.8f};
            }
            // Conservative chassis hull excludes wheels and the front plow.
            var hull=new GameObject("ChassisHull");hull.transform.SetParent(physics.transform,false);hull.layer=21;
            var collider=hull.AddComponent<BoxCollider>();
            collider.center=bike?new Vector3(0,.9f,-.1f):new Vector3(0,1.1f,.4f);
            collider.size=bike?new Vector3(.45f,.7f,1.2f):new Vector3(1.65f,1.1f,3.7f);
            Directory.CreateDirectory(Path.GetDirectoryName(output));AssetDatabase.Refresh();
            PrefabUtility.SaveAsPrefabAsset(root,output);AssetDatabase.SaveAssets();
        }
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }
}
