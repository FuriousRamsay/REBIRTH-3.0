using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Offline diagnostic only: never saves or changes a source prefab.
public static class RebirthVehiclePhysicsAudit
{
    public static void Run()
    {
        var report = new StringBuilder("Offline flat-ground physics; not game qualification.\n");
        bool previous = Physics.autoSimulation;
        Physics.autoSimulation = false;
        try
        {
            foreach (string path in new[] {
                "Assets/REBIRTHStandardVehiclesGenerated/BikeFinal_v1/rebirthBike.prefab",
                "Assets/REBIRTHStandardVehiclesGenerated/BuggyFinal_v2/rebirthBuggy.prefab" })
            foreach(bool interactionEnabled in new[]{true,false})
            {
                var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0,-.5f,0);
                ground.transform.localScale = new Vector3(200,1,200);
                try
                {
                    root.transform.position = Vector3.up * 2;
                    var interaction = root.GetComponent<BoxCollider>();
                    if(interaction!=null) interaction.enabled=interactionEnabled;
                    Physics.SyncTransforms();
                    var body = root.GetComponentInChildren<Rigidbody>();
                    report.AppendLine(path);
                    report.AppendLine("Root interaction collider enabled="+interactionEnabled);
                    report.AppendLine("Body mass="+body.mass+" scale="+body.transform.lossyScale+" center="+body.worldCenterOfMass);
                    foreach(var c in root.GetComponentsInChildren<Collider>())
                        report.AppendLine(c.GetType().Name+" "+c.name+" bounds="+c.bounds+" scale="+c.transform.lossyScale+" trigger="+c.isTrigger);
                    body.isKinematic=false;
                    float peak=0;
                    for(int i=0;i<500;i++)
                    {
                        if(!body.useGravity) body.AddForce(Vector3.down*9.81f*body.mass);
                        Physics.Simulate(.02f);
                        peak=Mathf.Max(peak,body.velocity.magnitude);
                        if(i%50==0) report.AppendLine("t="+(i*.02f)+" pos="+body.position+" vel="+body.velocity+" rot="+body.rotation.eulerAngles);
                    }
                    report.AppendLine("Peak speed="+peak+" final="+body.position);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(ground); }
            }
        }
        finally { Physics.autoSimulation=previous; }
        string output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/PhysicsAudit_20261016.txt";
        File.WriteAllText(output,report.ToString());
        Debug.Log(report.ToString());
    }
}
