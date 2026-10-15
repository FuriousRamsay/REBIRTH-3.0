using System;
using System.IO;
using UnityEngine.Scripting;

internal static class RebirthPurgeSupplyNotification
{
    internal static void Send(EntityPlayer player)
    {
        if(player==null)return;
        if(player is EntityPlayerLocal local){Show(local);return;}
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection!=null&&connection.IsServer)
            connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeSupplyNotice>().Setup(RebirthPoiMapSync.LocalWorld,RebirthPoiMapSync.LocalSession),_attachedToEntityId:player.entityId);
    }
    internal static void Show(EntityPlayerLocal player)
    {
        if(player==null||!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge)return;
        GameManager.ShowTooltip(player,Localization.Get("xuiRebirthPurgeSupplyIncoming"));
        Audio.Manager.PlayInsidePlayerHead("purge_airdrop");
        RebirthPurgeInformationTopics.Request(player,"PurgeSupplyUpdate");
    }
}
[Preserve]
public sealed class NetPackageRebirthPurgeSupplyNotice:NetPackage
{
    private Guid worldId,session;
    internal NetPackageRebirthPurgeSupplyNotice Setup(Guid world,Guid currentSession){worldId=world;session=currentSession;return this;}
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public override void read(PooledBinaryReader reader){worldId=new Guid(reader.ReadBytes(16));session=new Guid(reader.ReadBytes(16));}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write(worldId.ToByteArray());writer.Write(session.ToByteArray());}
    public int GetLength()=>32;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        RebirthPurgeNoticeDelivery.Receive(world,worldId,session,()=>RebirthPurgeSupplyNotification.Show(world.GetPrimaryPlayer()));
    }
}
