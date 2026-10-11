using System.IO;
using UnityEngine.Scripting;

internal static class RebirthPurgeSupplyNotification
{
    internal static void Send(EntityPlayer player)
    {
        if(player is EntityPlayerLocal local){Show(local);return;}
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection!=null&&connection.IsServer)
            connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeSupplyNotice>(),_attachedToEntityId:player.entityId);
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
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public override void read(PooledBinaryReader reader){}
    public override void write(PooledBinaryWriter writer){base.write(writer);}
    public int GetLength()=>0;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world!=null&&world.IsRemote()&&ReferenceEquals(GameManager.Instance.World,world))RebirthPurgeSupplyNotification.Show(world.GetPrimaryPlayer());
    }
}
