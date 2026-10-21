using System;
using UnityEngine.Scripting;

// Input only: the server owns movement limits, clearance, cadence, ammunition and damage.
[Preserve]
public sealed class NetPackageRebirthCamperTurretManual:NetPackage
{
    private Guid token;private int actor,entity,corner;private float yaw,pitch;private bool fire;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthCamperTurretManual Setup(int a,int e,int c,Guid t,float y,float p,bool f)
    {token=t;actor=a;entity=e;corner=c;yaw=y;pitch=p;fire=f;return this;}
    public override void read(PooledBinaryReader r){actor=r.ReadInt32();entity=r.ReadInt32();corner=r.ReadInt32();token=new Guid(r.ReadBytes(16));yaw=r.ReadSingle();pitch=r.ReadSingle();fire=r.ReadBoolean();}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(actor);w.Write(entity);w.Write(corner);w.Write(token.ToByteArray());w.Write(yaw);w.Write(pitch);w.Write(fire);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||world.IsRemote()||!ValidEntityIdForSender(actor))return;
        var player=world.GetEntity(actor) as EntityPlayer;var vehicle=world.GetEntity(entity) as EntityVehicle;
        if(RebirthCamperTurretAccess.Authenticated(world,Sender,player)&&RebirthCamperRadial.IsCamper(vehicle)
            &&!RebirthCamperTurretRuntime.For(vehicle).ManualInput(player,corner,token,yaw,pitch,fire))
            Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperTurretControlResult>().Setup(entity,corner,token,false));
    }
    public int GetLength()=>37;
}

