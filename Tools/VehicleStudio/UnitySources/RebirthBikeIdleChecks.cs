using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

// Reproduce rigidbody/wheel behaviour separately from the game's native forces.
// This is a regression diagnostic, not an in-game handling qualification.
public static class RebirthBikeIdleChecks
{
    [Serializable] class Data { public Entry[] components; }
    [Serializable] class Entry { public string type,path; public Pose transform; public Values component; }
    [Serializable] class Pose { public Vector3 m_LocalPosition,m_LocalScale; public Quaternion m_LocalRotation; }
    [Serializable] class Values {
        public Vector3 m_Center,m_Size; public float m_Radius,m_Mass,m_Drag,m_AngularDrag,m_SuspensionDistance,m_ForceAppPointDistance,m_WheelDampingRate;
        public JointSpring m_SuspensionSpring; public Friction m_ForwardFriction,m_SidewaysFriction;
    }
    [Serializable] class Friction { public float m_ExtremumSlip,m_ExtremumValue,m_AsymptoteSlip,m_AsymptoteValue,m_Stiffness;
        public WheelFrictionCurve Curve()=>new WheelFrictionCurve{extremumSlip=m_ExtremumSlip,extremumValue=m_ExtremumValue,asymptoteSlip=m_AsymptoteSlip,asymptoteValue=m_AsymptoteValue,stiffness=m_Stiffness}; }
    static GameObject Native()
    {
        var root=new GameObject("ExactNativeMotorcyclePhysics");var rb=root.AddComponent<Rigidbody>();
        var data=JsonUtility.FromJson<Data>(File.ReadAllText("E:/_Haven/shared/REBIRTHStaged/VehicleStudio/native-vehicle-physics-20261016.json"));
        foreach(var entry in data.components.Where(e=>e.path.StartsWith("VMotorcycleP/Physics")))
        {
            var c=entry.component;
            if(entry.type=="Rigidbody"){rb.mass=c.m_Mass;rb.drag=c.m_Drag;rb.angularDrag=c.m_AngularDrag;continue;}
            var go=new GameObject(entry.path.Split('/').Last());go.transform.SetParent(root.transform,false);
            go.transform.localPosition=entry.transform.m_LocalPosition;go.transform.localRotation=entry.transform.m_LocalRotation;go.transform.localScale=entry.transform.m_LocalScale;
            if(entry.type=="BoxCollider"){var box=go.AddComponent<BoxCollider>();box.center=c.m_Center;box.size=c.m_Size;}
            if(entry.type=="WheelCollider"){
                var w=go.AddComponent<WheelCollider>();w.center=c.m_Center;w.radius=c.m_Radius;w.mass=c.m_Mass;
                w.suspensionDistance=c.m_SuspensionDistance;w.suspensionSpring=c.m_SuspensionSpring;w.forceAppPointDistance=c.m_ForceAppPointDistance;w.wheelDampingRate=c.m_WheelDampingRate;
                w.forwardFriction=c.m_ForwardFriction.Curve();w.sidewaysFriction=c.m_SidewaysFriction.Curve();
            }
        }
        return root;
    }
    public static void Run()
    {
        var report=new List<string>{"Offline 30-second gravity-only idle comparison. Native runtime stabilisation, sleep, terrain and material overrides are not reproduced."};
        var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        try
        {
            foreach(float slope in new[]{0f,5f})foreach(string variant in new[]{"native","old","candidate"})
            {
                var root=variant=="native"?Native():Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>((variant=="old"?"Assets/REBIRTHSeatTargets_20261017":"Assets/REBIRTHSeatPelvis_20261017")+"/rebirthBike.prefab"));
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    ground.transform.position=new Vector3(0,-.5f,0);ground.transform.localScale=new Vector3(200,1,200);ground.transform.rotation=Quaternion.Euler(0,0,slope);
                    var rb=root.GetComponentInChildren<Rigidbody>();
                    // Native Init makes this override only for automatic COM.
                    if(rb.automaticCenterOfMass)rb.centerOfMass=new Vector3(0,.1f,0);
                    rb.useGravity=true;rb.isKinematic=false;
                    foreach(var collider in root.GetComponents<Collider>())if(collider.attachedRigidbody!=rb)collider.enabled=false;
                    rb.transform.SetPositionAndRotation(Vector3.up*1.1f,Quaternion.Euler(0,0,2));Physics.SyncTransforms();
                    for(int i=0;i<1500;i++)Physics.Simulate(.02f);
                    report.Add(variant+" slope="+slope+" up="+rb.transform.up.y.ToString("F4")+" yaw="+rb.rotation.eulerAngles.y.ToString("F3")+" position="+rb.position.ToString("F3")+" speed="+rb.velocity.magnitude.ToString("F4"));
                }
                finally{Object.DestroyImmediate(root);Object.DestroyImmediate(ground);}
            }
        }
        finally{Physics.simulationMode=previous;}
        File.WriteAllLines(RebirthVehicleSeatUpgrade.Output+"/idle-comparison.txt",report);Debug.Log(string.Join("\n",report));
    }
}
