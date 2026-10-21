using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class RebirthGameBridge
{
    internal static bool OwnsCctvMouseLease(BridgeRequest request)
    {lock(batchGate)return batchToken!=null&&request.RunToken==batchToken&&!batchUncertain;}
}

internal static class RebirthGameBridgeCctvMouse
{
    internal static void Handle(BridgeRequest request)
    {
        float x=request.QueryFloat("x",0),y=request.QueryFloat("y",0);
        var world=GameManager.Instance?.World;var player=world?.GetPrimaryPlayer();
        var vehicle=world?.GetEntity(request.QueryInt("entity",-1)) as EntityVehicle;int corner=request.QueryInt("corner",-1);
        if(request.Method!="POST"||!RebirthGameBridge.IsRunning||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"
            ||!RebirthGameBridge.OwnsCctvMouseLease(request)||player==null||vehicle==null||corner<0||corner>3
            ||!RebirthCamperRadial.IsCamper(vehicle)||RebirthCamperCctvMouseInput.BridgeRawMouse!=null
            ||float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||Mathf.Abs(x)>2||Mathf.Abs(y)>2)
        {request.Fail("Exclusive CodexTest CCTV input scope and finite raw axes within +/-2 required",409);return;}
        var view=RebirthCamperTurretAccess.ObservePresentation(vehicle);
        if(view?["cameraActive"]?.Value<bool>()!=true||view["cameraCorner"].Value<int>()!=corner)
        {request.Fail("Exact current CCTV view required",409);return;}
        GameManager.Instance.StartCoroutine(Sample(request,player,vehicle,corner,view["cameraInstance"].Value<int>(),new Vector2(x,y)));
    }
    private static IEnumerator Sample(BridgeRequest request,EntityPlayerLocal player,EntityVehicle vehicle,int corner,int camera,Vector2 value)
    {
        int consumed=0;float until=Time.realtimeSinceStartup+1f;
        Func<bool> current=()=>!request.IsAbandoned&&Time.realtimeSinceStartup<until&&RebirthGameBridge.IsRunning
            &&RebirthGameBridge.OwnsCctvMouseLease(request)&&vehicle&&ReferenceEquals(player.world,vehicle.world)
            &&ReferenceEquals(player.world.GetEntity(vehicle.entityId),vehicle)
            &&RebirthCamperTurretRuntime.For(vehicle).ControlOwner(corner)==player.entityId
            &&RebirthCamperTurretAccess.ObservePresentation(vehicle)?["cameraInstance"]?.Value<int>()==camera;
        using(var scope=new RebirthGameBridgeInput.OwnedInputScope(player,current))
        {
            Func<Vector2?> provider=()=>
            {
                if(!scope.Admitted)return null;
                if(Mathf.Abs(Input.GetAxis("Mouse X"))+Mathf.Abs(Input.GetAxis("Mouse Y"))>.0001f)
                {scope.Refuse("physical mouse input took control");return null;}
                return consumed++==0?value:Vector2.zero;
            };
            RebirthCamperCctvMouseInput.BridgeRawMouse=provider;
            try
            {
                for(int i=0;i<4&&scope.Admitted;i++)yield return null;
                if(!scope.Admitted||consumed==0){request.Fail(scope.Failure??"CCTV did not consume raw input",409);yield break;}
                request.Complete(new JObject{["input"]="raw_mouse_axis_sample",["samples"]=1,["cameraInstance"]=camera,["x"]=value.x,["y"]=value.y});
            }
            finally{if(ReferenceEquals(RebirthCamperCctvMouseInput.BridgeRawMouse,provider))RebirthCamperCctvMouseInput.BridgeRawMouse=null;}
        }
    }
}

