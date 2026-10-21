using System;
using System.Xml.Linq;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthHumanEquipmentRequest:NetPackage
{
    private int player,npc,action,target,source;private string token,argument,stable;private uint epoch;private ulong generation;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthHumanEquipmentRequest Setup(int p,int n,int a,int t,int s,string id,string value,string npcStable)
    {stable=npcStable;player=p;npc=n;action=a;target=t;source=s;token=id;argument=value??"";epoch=RebirthNpcNetworkEpoch.ClientEpoch;generation=RebirthNpcRecoveryProjectionScope.Current;return this;}
    public override void read(PooledBinaryReader r){player=r.ReadInt32();npc=r.ReadInt32();action=r.ReadByte();target=r.ReadInt32();source=r.ReadInt32();epoch=r.ReadUInt32();generation=r.ReadUInt64();token=RebirthSurvivorNetworkCodec.ReadBoundedString(r,64);argument=RebirthSurvivorNetworkCodec.ReadBoundedString(r,262144);stable=RebirthSurvivorNetworkCodec.ReadBoundedString(r,64);}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(player);w.Write(npc);w.Write((byte)action);w.Write(target);w.Write(source);w.Write(epoch);w.Write(generation);RebirthSurvivorNetworkCodec.WriteString(w,token,64);RebirthSurvivorNetworkCodec.WriteString(w,argument,262144);RebirthSurvivorNetworkCodec.WriteString(w,stable,64);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||!ValidEntityIdForSender(player)||
            epoch!=RebirthNpcNetworkEpoch.GetServerEpoch()||generation!=RebirthNpcRecoveryGeneration.RequireCurrent()||action<0||action>2)return;
        if((world.GetEntity(npc) as EntityRebirthHumanoidNPC)?.RebirthRuntimeState?.StableId.ToString()!=stable)return;
        var result=RebirthHumanEquipment.Handle(world.GetEntity(player) as EntityPlayer,world.GetEntity(npc) as EntityRebirthHumanoidNPC,action,target,source,argument);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthHumanEquipmentResponse>().Setup(player,token,result.ToString(SaveOptions.DisableFormatting)),_attachedToEntityId:player);
    }
    public int GetLength()=>0;
}
[Preserve]
public sealed class NetPackageRebirthHumanEquipmentResponse:NetPackage
{
    private int player;private string token,payload;private uint epoch;private ulong generation;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthHumanEquipmentResponse Setup(int p,string id,string data){player=p;token=id;payload=data;epoch=RebirthNpcNetworkEpoch.GetServerEpoch();generation=RebirthNpcRecoveryGeneration.RequireCurrent();return this;}
    public override void read(PooledBinaryReader r){player=r.ReadInt32();epoch=r.ReadUInt32();generation=r.ReadUInt64();token=RebirthSurvivorNetworkCodec.ReadBoundedString(r,64);payload=RebirthSurvivorNetworkCodec.ReadBoundedString(r,4194304);}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(player);w.Write(epoch);w.Write(generation);RebirthSurvivorNetworkCodec.WriteString(w,token,64);RebirthSurvivorNetworkCodec.WriteString(w,payload,4194304);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||epoch!=RebirthNpcNetworkEpoch.ClientEpoch||generation!=RebirthNpcRecoveryProjectionScope.Current)return;
        XUiC_RebirthHumanEquipment.Receive(player,token,payload);
    }
    public int GetLength()=>0;
}


