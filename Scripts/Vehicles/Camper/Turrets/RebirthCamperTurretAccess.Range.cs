using System;
using UnityEngine.Scripting;

internal static partial class RebirthCamperTurretAccess
{
    internal static void RangeWarning(EntityVehicle vehicle,EntityPlayer player,int corner,Guid token,float range,float distance)
    {
        if(player is EntityPlayerLocal){PresentRangeWarning(vehicle.entityId,corner,token,range,distance);return;}
        var peer=SingletonMonoBehaviour<ConnectionManager>.Instance.Clients?.ForEntityId(player.entityId);
        if(Authenticated(vehicle.world,peer,player))peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperTurretRangeWarning>().Setup(vehicle.entityId,corner,token,range,distance));
    }
    internal static void PresentRangeWarning(int entity,int corner,Guid token,float range,float distance)
    {
        var session=current;
        if(session?.Vehicle?.entityId==entity&&session.Current())session.View.RangeDenied(token,corner,range,distance);
    }
}

[Preserve]
public sealed class NetPackageRebirthCamperTurretRangeWarning:NetPackage
{
    private int entity,corner;private Guid token;private float range,distance;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthCamperTurretRangeWarning Setup(int e,int c,Guid t,float r,float d)
    {entity=e;corner=c;token=t;range=r;distance=d;return this;}
    public override void read(PooledBinaryReader r){entity=r.ReadInt32();corner=r.ReadInt32();token=new Guid(r.ReadBytes(16));range=r.ReadSingle();distance=r.ReadSingle();}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(entity);w.Write(corner);w.Write(token.ToByteArray());w.Write(range);w.Write(distance);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world?.IsRemote()==true)RebirthCamperTurretAccess.PresentRangeWarning(entity,corner,token,range,distance);}
    public int GetLength()=>32;
}
