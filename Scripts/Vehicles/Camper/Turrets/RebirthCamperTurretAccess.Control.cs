using System;
using UnityEngine;
using UnityEngine.Scripting;

internal static partial class RebirthCamperTurretAccess
{
    internal static bool CanUse(EntityVehicle vehicle,EntityPlayer player)
    {
        if(!ThreadManager.IsMainThread()||!RebirthCamperRadial.IsCamper(vehicle)||!RebirthCamperBodyAttachment.HasBody(vehicle)||vehicle.world==null||vehicle.IsDead()
            ||player==null||player.IsDead()||!ReferenceEquals(vehicle.world,player.world)
            ||!ReferenceEquals(GameManager.Instance?.World,vehicle.world)
            ||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)
            ||!ReferenceEquals(vehicle.world.GetEntity(player.entityId),player))return false;
        if(ReferenceEquals(player.AttachedToEntity,vehicle)&&vehicle.FindAttachSlot(player)>=0)return true;
        if((player.position-vehicle.position).sqrMagnitude>64)return false;
        if(!vehicle.IsLocked()||!vehicle.hasLock())return true;
        var data=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        return data!=null&&((data.PrimaryId!=null&&(data.PrimaryId.Equals(vehicle.GetOwner())||vehicle.isAllowedUser(data.PrimaryId)))
            ||(data.NativeId!=null&&(data.NativeId.Equals(vehicle.GetOwner())||vehicle.isAllowedUser(data.NativeId))));
    }
    internal static bool Authenticated(World world,ClientInfo sender,EntityPlayer player)
    {
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        return ThreadManager.IsMainThread()&&world!=null&&!world.IsRemote()&&connection?.IsServer==true
            &&sender!=null&&player!=null&&sender.entityId==player.entityId&&sender.loginDone&&!sender.disconnecting
            &&sender.InternalId!=null&&sender.netConnection!=null&&sender.netConnection.Length>0
            &&sender.netConnection[0]!=null&&!sender.netConnection[0].IsDisconnected()
            &&ReferenceEquals(player.world,world)&&ReferenceEquals(world.GetEntity(player.entityId),player)
            &&ReferenceEquals(connection.Clients?.ForEntityId(player.entityId),sender);
    }
    private static void Control(Session s,int corner,Guid token,bool acquire)
    {
        if(s.Player?.world==null||s.Vehicle?.world!=s.Player.world)return;
        if(acquire&&(!ReferenceEquals(current,s)||!s.Current()))return;
        if(s.Player.world.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthCamperTurretControl>().Setup(s.Player.entityId,s.Vehicle.entityId,corner,token,acquire),false);
        else
        {
            var runtime=RebirthCamperTurretRuntime.For(s.Vehicle);
            if(acquire)s.View.ControlResult(token,corner,runtime.AcquireControl(s.Player,corner,token));
            else runtime.ReleaseControl(s.Player,token);
        }
    }
    internal static void ControlResult(int entity,int corner,Guid token,bool accepted)
    {
        var s=current;
        if(s!=null&&s.Vehicle.entityId==entity)s.View.ControlResult(token,corner,accepted);
        // If the window closed in transit, the abandoned lease expires in three seconds.
    }
}

[Preserve]
public sealed class NetPackageRebirthCamperTurretControl:NetPackage
{
    int actor,entity,corner;Guid token;bool acquire;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthCamperTurretControl Setup(int a,int e,int c,Guid t,bool get)
    {actor=a;entity=e;corner=c;token=t;acquire=get;return this;}
    public override void read(PooledBinaryReader r){actor=r.ReadInt32();entity=r.ReadInt32();corner=r.ReadInt32();token=new Guid(r.ReadBytes(16));acquire=r.ReadBoolean();}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(actor);w.Write(entity);w.Write(corner);w.Write(token.ToByteArray());w.Write(acquire);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||world.IsRemote()||!ValidEntityIdForSender(actor))return;
        var player=world.GetEntity(actor) as EntityPlayer;var vehicle=world.GetEntity(entity) as EntityVehicle;
        if(!RebirthCamperTurretAccess.Authenticated(world,Sender,player)||!RebirthCamperRadial.IsCamper(vehicle))return;
        var runtime=RebirthCamperTurretRuntime.For(vehicle);
        if(!acquire){runtime.ReleaseControl(player,token);return;}
        bool accepted=runtime.AcquireControl(player,corner,token);
        Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperTurretControlResult>().Setup(entity,corner,token,accepted));
    }
    public int GetLength()=>29;
}
[Preserve]
public sealed class NetPackageRebirthCamperTurretControlResult:NetPackage
{
    int entity,corner;Guid token;bool accepted;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthCamperTurretControlResult Setup(int e,int c,Guid t,bool ok){entity=e;corner=c;token=t;accepted=ok;return this;}
    public override void read(PooledBinaryReader r){entity=r.ReadInt32();corner=r.ReadInt32();token=new Guid(r.ReadBytes(16));accepted=r.ReadBoolean();}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(entity);w.Write(corner);w.Write(token.ToByteArray());w.Write(accepted);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==true)RebirthCamperTurretAccess.ControlResult(entity,corner,token,accepted);}
    public int GetLength()=>25;
}
