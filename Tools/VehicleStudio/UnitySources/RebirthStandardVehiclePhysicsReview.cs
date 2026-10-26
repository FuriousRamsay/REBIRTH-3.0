using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RebirthStandardVehiclePhysicsReview
{
    [Serializable] public class WheelResult
    { public string path; public float radius, hubError; public Vector3 hub; }
    [Serializable] public class Result
    { public string prefab,status; public float mass; public WheelResult[] wheels; public string limitation; }
    public static void ValidateBothBatch()
    {
        foreach(string kind in new[]{"Buggy","Bike"})
        {
            bool bike=kind=="Bike";
            string path="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Physics_v1/rebirth"+kind+".prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null) throw new FileNotFoundException(path);
            var physics=prefab.transform.Find("Physics");
            if(physics==null || physics.tag!="Physics" || physics.gameObject.layer!=21)
                throw new InvalidDataException("Physics root/tag/layer invalid: "+kind);
            var body=physics.GetComponent<Rigidbody>();
            if(body==null || Mathf.Abs(body.mass-(bike?250:2400))>.01f)
                throw new InvalidDataException("Donor mass mismatch: "+kind+" actual="+(body==null?"missing":body.mass.ToString("R")));
            var wheels=physics.GetComponentsInChildren<WheelCollider>();
            if(wheels.Length!=(bike?2:4)) throw new InvalidDataException("Wheel count mismatch");
            var result=new Result {prefab=path,status="STRUCTURE_PASS",mass=body.mass,
                wheels=new WheelResult[wheels.Length],limitation="Static hierarchy and nominal hub alignment only. Ground settling, driving, rider fit and game runtime remain unverified."};
            for(int i=0;i<wheels.Length;i++)
            {
                var wc=physics.Find("Wheel"+i).GetComponent<WheelCollider>();
                var visual=prefab.transform.Find(bike&&i==0?"M/Forks/Wheel0":"Wheel"+i);
                Vector3 hub=wc.transform.TransformPoint(wc.center-new Vector3(0,wc.suspensionDistance*wc.suspensionSpring.targetPosition,0));
                float error=Vector3.Distance(hub,visual.position);
                if(error>.001f || wc.radius<=0 || float.IsNaN(wc.radius)) throw new InvalidDataException("Wheel alignment invalid");
                result.wheels[i]=new WheelResult {path="Physics/Wheel"+i,radius=wc.radius,hubError=error,hub=hub};
            }
            foreach(var script in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                if(script!=null) throw new InvalidDataException("Unexpected runtime script: "+script.GetType());
            string output="E:/_Haven/shared/REBIRTHTemp/BuggyBikeAudit_20261012/"+kind+"-physics-validation.json";
            File.WriteAllText(output,JsonUtility.ToJson(result,true));
        }
    }
}
