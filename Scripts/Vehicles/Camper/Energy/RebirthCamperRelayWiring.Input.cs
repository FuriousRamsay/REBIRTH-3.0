using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

internal static partial class RebirthCamperRelayWiring
{
    private static EntityVehicle selectedVehicle;private static EntityPlayerLocal selectedActor;
    private static Guid selectedToken;private static int selectedRelay;private static bool waiting,ready;
    private static float expires;
    private static readonly RebirthCamperWireFeedback wireFeedback=new RebirthCamperWireFeedback();
    internal static object ObserveWireFeedback()=>new{selected=selectedToken!=Guid.Empty,waiting,ready,
        vehicle=selectedVehicle?selectedVehicle.entityId:-1,relay=selectedRelay,previewActive=wireFeedback.Active,
        source=wireFeedback.SourceName,endpoint=new[]{wireFeedback.Endpoint.x,wireFeedback.Endpoint.y,wireFeedback.Endpoint.z}};
    internal static void Install()
    {
        new Harmony("rebirth.camper.relay.wiring").CreateClassProcessor(typeof(RebirthCamperWireInput)).Patch();
        new Harmony("rebirth.camper.relay.wiring.disconnect").CreateClassProcessor(typeof(RebirthCamperWireDisconnectInput)).Patch();
        ModEvents.GameUpdate.RegisterHandler(UpdateWireSelection);
        ModEvents.WorldShuttingDown.RegisterHandler(ShutdownWireSelection);
    }
    private static void ShutdownWireSelection(ref ModEvents.SWorldShuttingDownData data)
    {wireFeedback.Clear();selections.Clear();selectedActor=null;selectedVehicle=null;selectedToken=Guid.Empty;waiting=ready=false;}
    private static void UpdateWireSelection(ref ModEvents.SGameUpdateData data)
    {
        if(selectedToken!=Guid.Empty&&(!selectedVehicle||selectedActor==null||selectedActor.IsDead()||Tool(selectedActor)==null
            ||Time.realtimeSinceStartup>expires||!ReferenceEquals(GameManager.Instance?.World,selectedActor.world)))Cancel();
        if(ready&&!wireFeedback.Update(selectedActor,Tool(selectedActor)))Cancel();
    }
    private static void Request(byte action,int corner,Vector3i position)
    {
        waiting=true;expires=Time.realtimeSinceStartup+60;
        if(selectedActor.world.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperRelayEditV1>().Setup(selectedActor.entityId,selectedVehicle.entityId,selectedToken,action,selectedRelay,corner,position),false);
        else Receive(selectedActor.world,null,selectedActor.entityId,selectedVehicle.entityId,selectedToken,action,selectedRelay,corner,position);
    }
    private static void Cancel()
    {
        wireFeedback.Clear();
        if(selectedActor!=null&&selectedVehicle&&selectedToken!=Guid.Empty&&ReferenceEquals(selectedActor.world,GameManager.Instance?.World))Request(2,-1,default);
        selectedActor=null;selectedVehicle=null;selectedToken=Guid.Empty;waiting=ready=false;
    }
    internal static void ReceiveResult(Guid token,byte action,bool accepted,string status)
    {
        if(token!=selectedToken)return;
        var actor=selectedActor;waiting=false;
        if(action==0&&accepted)
        {
            var energy=RebirthCamperEnergyRuntime.Observe(selectedVehicle);
            ready=wireFeedback.Begin(actor,Tool(actor),Relay(selectedVehicle,selectedRelay),energy!=null&&energy.Mode==RebirthCamperEnergyMode.Camp&&energy.AvailableWatts>0);
            if(!ready)Cancel();
        }
        else
        {
            if(accepted&&actor!=null&&(action==1||action==3))
                Audio.Manager.PlayInsidePlayerHead(status=="xuiCamperWireDisconnected"?"wiretool_grab":"wiretool_place");
            wireFeedback.Clear();selectedActor=null;selectedVehicle=null;selectedToken=Guid.Empty;ready=false;
        }
        if(!accepted&&actor!=null)GameManager.ShowTooltip(actor,Localization.Get(status));
    }
    internal static bool WireClick(ItemActionData data,bool released)
    {
        if(!(data?.invData?.holdingEntity is EntityPlayerLocal actor))return false;
        var target=RebirthCamperWireTarget.Focus(actor);
        if(target==null&&selectedToken==Guid.Empty)return false;
        if(!released)return true;
        if(waiting)return true;
        if(target&&target.Kind==0)
        {
            if(selectedToken!=Guid.Empty){Cancel();return true;}
            selectedActor=actor;selectedVehicle=target.Vehicle;selectedRelay=target.Slot;selectedToken=Guid.NewGuid();ready=false;Request(0,-1,default);return true;
        }
        if(!ready){GameManager.ShowTooltip(actor,Localization.Get("xuiCamperWireChooseRelay"));return true;}
        if(target)
        {if(target.Vehicle==selectedVehicle&&target.Kind==1)Request(1,target.Slot,default);return true;}
        var ray=actor.GetLookRay();
        if(Voxel.Raycast(actor.world,ray,Constants.cDigAndBuildDistance,-538750989,8,0))Request(1,-1,Voxel.voxelRayHitInfo.hit.blockPos);
        return true;
    }
    internal static bool WireDisconnectClick(ItemActionData data,bool released)
    {
        if(!(data?.invData?.holdingEntity is EntityPlayerLocal actor))return false;
        var target=RebirthCamperWireTarget.Focus(actor);
        if(target==null||target.Kind!=0)return false;
        if(!released)return true;
        if(waiting)return true;
        if(selectedToken!=Guid.Empty)Cancel();
        selectedActor=actor;selectedVehicle=target.Vehicle;selectedRelay=target.Slot;
        selectedToken=Guid.NewGuid();ready=false;Request(3,-1,default);
        return true;
    }
}
[HarmonyPatch(typeof(ItemActionConnectPower),nameof(ItemActionConnectPower.ExecuteAction))]
internal static class RebirthCamperWireInput
{
    private static bool Prefix(ItemActionConnectPower __instance,ItemActionData __0,bool __1)
    {
        if(__0?.invData?.holdingEntity is EntityPlayerLocal player&&RebirthCamperWireTarget.Focus(player)?.Kind==0
            &&__0 is ItemActionConnectPower.ConnectPowerData native&&native.HasStartPoint)__instance.StopHolding(__0);
        return !RebirthCamperRelayWiring.WireClick(__0,__1);
    }
}
[HarmonyPatch]
internal static class RebirthCamperWireDisconnectInput
{
    private static System.Reflection.MethodBase TargetMethod()
    {
        var type=AccessTools.TypeByName("ItemActionDisconnectPower");
        return type==null?null:AccessTools.Method(type,"ExecuteAction");
    }
    private static bool Prefix(ItemActionData __0,bool __1)
        =>!RebirthCamperRelayWiring.WireDisconnectClick(__0,__1);
}
[Preserve]
public sealed class NetPackageRebirthCamperRelayEditV1:NetPackage
{
    private int actor,vehicle,relay,corner;private Guid token;private byte action;private Vector3i position;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthCamperRelayEditV1 Setup(int a,int v,Guid t,byte mode,int r,int c,Vector3i p){actor=a;vehicle=v;token=t;action=mode;relay=r;corner=c;position=p;return this;}
    public override void read(PooledBinaryReader r){actor=r.ReadInt32();vehicle=r.ReadInt32();token=new Guid(r.ReadBytes(16));action=r.ReadByte();relay=r.ReadInt32();corner=r.ReadInt32();position=new Vector3i(r.ReadInt32(),r.ReadInt32(),r.ReadInt32());}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(actor);w.Write(vehicle);w.Write(token.ToByteArray());w.Write(action);w.Write(relay);w.Write(corner);w.Write(position.x);w.Write(position.y);w.Write(position.z);}
    public override void ProcessPackage(World world,GameManager manager){if(world!=null&&!world.IsRemote()&&ValidEntityIdForSender(actor))RebirthCamperRelayWiring.Receive(world,Sender,actor,vehicle,token,action,relay,corner,position);}
}
[Preserve]
public sealed class NetPackageRebirthCamperRelayResultV1:NetPackage
{
    private Guid token;private byte action;private bool accepted;private string status;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthCamperRelayResultV1 Setup(Guid t,byte a,bool ok,string message){token=t;action=a;accepted=ok;status=message;return this;}
    public override void read(PooledBinaryReader r){token=new Guid(r.ReadBytes(16));action=r.ReadByte();accepted=r.ReadBoolean();status=r.ReadString();}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(token.ToByteArray());w.Write(action);w.Write(accepted);w.Write(status??string.Empty);}
    public override void ProcessPackage(World world,GameManager manager){if(world?.IsRemote()==true)RebirthCamperRelayWiring.ReceiveResult(token,action,accepted,status);}
}
