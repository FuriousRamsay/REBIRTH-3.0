using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Explicit read-only bridge check of native physics state, outside timed phases.
internal static class RebirthGameBridgeCamperCollisionAudit
{
    internal static JObject Run(EntityVehicle vehicle)
    {
        var compound=new List<Collider>();var detached=new List<Collider>();
        foreach(var collider in vehicle.RootTransform.GetComponentsInChildren<Collider>(false))
        {
            if(!collider||!collider.enabled||collider.isTrigger)continue;
            var actor=collider.GetComponentInParent<Entity>();
            if(actor!=null&&!ReferenceEquals(actor,vehicle))continue;
            if(collider.attachedRigidbody==vehicle.vehicleRB)compound.Add(collider);
            else detached.Add(collider);
        }
        int checkedPairs=0,missing=0;var examples=new JArray();
        foreach(var a in detached)foreach(var b in compound)
        {
            if(Physics.GetIgnoreLayerCollision(a.gameObject.layer,b.gameObject.layer))continue;
            checkedPairs++;
            if(Physics.GetIgnoreCollision(a,b))continue;
            missing++;
            if(examples.Count<8)examples.Add(new JObject{["detached"]=a.name,["compound"]=b.name});
        }
        return new JObject{["passed"]=missing==0&&compound.Count>0,["compound"]=compound.Count,
            ["campClaimReady"]=RebirthCamperEnergyImmobility.CampClaimReady(vehicle),
            ["constraints"]=vehicle.vehicleRB.constraints.ToString(),
            ["speed"]=vehicle.vehicleRB.velocity.magnitude,
            ["angularSpeed"]=vehicle.vehicleRB.angularVelocity.magnitude,
            ["detached"]=detached.Count,["checkedPairs"]=checkedPairs,["missingIgnores"]=missing,["examples"]=examples};
    }
}
