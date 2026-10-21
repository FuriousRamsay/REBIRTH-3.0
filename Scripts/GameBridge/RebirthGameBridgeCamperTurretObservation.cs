using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal static class RebirthGameBridgeCamperTurretObservation
{
    internal static void Handle(BridgeRequest request)
    {
        var world=GameManager.Instance?.World;int id=request.QueryInt("entity",-1);
        var vehicle=world?.GetEntity(id) as EntityVehicle;
        float seconds=request.QueryFloat("seconds",0);
        if(vehicle==null||!RebirthCamperRadial.IsCamper(vehicle)||!ReferenceEquals(vehicle.world,world))
        {request.Fail("An exact live camper entity is required",409);return;}
        if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||seconds>10)
        {request.Fail("Observation duration must be finite and between zero and ten seconds");return;}
        if(seconds==0){request.Complete(RebirthCamperTurretRuntime.Observation(vehicle,true));return;}
        GameManager.Instance.StartCoroutine(Sample(request,vehicle,seconds));
    }
    private static IEnumerator Sample(BridgeRequest request,EntityVehicle vehicle,float seconds)
    {
        var samples=new JArray();float until=Time.realtimeSinceStartup+seconds;
        while(true)
        {
            if(request.IsAbandoned)yield break;
            var world=GameManager.Instance?.World;
            if(vehicle==null||world==null||!ReferenceEquals(vehicle.world,world)||!ReferenceEquals(world.GetEntity(vehicle.entityId),vehicle))
            {request.Fail("Observed camper/world changed during capture",409);yield break;}
            JObject snapshot;
            try{snapshot=RebirthCamperTurretRuntime.Observation(vehicle,samples.Count==0);}
            catch(Exception error){request.Fail("Turret observation failed: "+error.Message,500);yield break;}
            samples.Add(snapshot);
            if(Time.realtimeSinceStartup>=until)break;
            yield return new WaitForSecondsRealtime(.1f);
        }
        request.Complete(new JObject{["schema"]=1,["samples"]=samples,["durationRequested"]=seconds,["inputInjected"]=false});
    }
}
