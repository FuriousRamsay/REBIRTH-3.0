using System;
using System.IO;
using System.Globalization;
using System.Xml;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthDogCapacitySyncV2:NetPackage
{
    private uint epoch,connection;private ulong generation;private int player,dogs,total,cap;private long revision;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthDogCapacitySyncV2 Setup(int playerId,int dogCount,int totalCount,int globalCap,long rev,uint recipientConnection)
    {epoch=RebirthNpcNetworkEpoch.GetServerEpoch();connection=recipientConnection;generation=RebirthNpcRecoveryGeneration.RequireCurrent();player=playerId;dogs=dogCount;total=totalCount;cap=globalCap;revision=rev;return this;}
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write((ushort)1);w.Write(epoch);w.Write(connection);w.Write(generation);w.Write(player);w.Write(dogs);w.Write(total);w.Write(cap);w.Write(revision);}
    public override void read(PooledBinaryReader r)
    {if(r.ReadUInt16()!=1)throw new InvalidDataException("Unsupported dog capacity projection version");epoch=r.ReadUInt32();connection=r.ReadUInt32();generation=r.ReadUInt64();player=r.ReadInt32();dogs=r.ReadInt32();total=r.ReadInt32();cap=r.ReadInt32();revision=r.ReadInt64();}
    public override void ProcessPackage(World world,GameManager callbacks)
    {RebirthDogCapacitySyncService.ReceiveQualified(world,player,dogs,total,cap,revision,epoch,connection,generation);}
    public int GetLength()=>0;
}

public static partial class RebirthDogCapacitySyncService
{
    private static uint capacityEpoch,capacityConnection;private static ulong capacityGeneration;
    internal static bool CanRetireEmptyCapacity
    {get{lock(Sync)return ThreadManager.IsMainThread()&&clientPlayerEntityId==-1&&clientRevision==0;}}
    private static bool CapacityCurrent(EntityPlayer player)
    {return ThreadManager.IsMainThread()&&ReferenceEquals(player.world,GameManager.Instance?.World)&&ReferenceEquals(player.world,clientWorld)&&ReferenceEquals(clientWorld?.GetEntity(player.entityId),player)&&capacityEpoch==RebirthNpcNetworkEpoch.ClientEpoch&&capacityConnection==RebirthNpcNetworkEpoch.ClientConnectionEpoch&&capacityGeneration==RebirthNpcRecoveryProjectionScope.Current;}
    internal static void ReceiveQualified(World world,int player,int dogs,int total,int cap,long revision,uint epoch,uint connection,ulong generation)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||connection==0||connection!=RebirthNpcNetworkEpoch.ClientConnectionEpoch||!RebirthNpcNetworkEpoch.AcceptClientEpoch(epoch)||!RebirthNpcRecoveryProjectionScope.Observe(generation)||dogs<0||total<dogs||cap<1||revision<=0)return;
        var actor=world.GetPrimaryPlayer();if(actor==null||actor.entityId!=player)return;
        lock(Sync)
        {
            if(clientWorld!=null&&(!ReferenceEquals(clientWorld,world)||capacityEpoch!=epoch||capacityConnection!=connection))return;
            if(!CanReplaceCapacity(clientPlayerEntityId==player,capacityGeneration,clientRevision,generation,revision))return;
            clientWorld=world;capacityEpoch=epoch;capacityConnection=connection;capacityGeneration=generation;clientPlayerEntityId=player;clientDogCount=dogs;clientTotalCount=total;clientGlobalCap=cap;clientRevision=revision;
        }
    }
    internal static bool CanReplaceCapacity(bool samePlayer,ulong priorGeneration,long priorRevision,ulong generation,long revision)
    {return generation>=priorGeneration&&(!samePlayer||generation>priorGeneration||revision>=priorRevision);}
    internal static RebirthNpcSaveQuarantineDomain PrepareCapacityQuarantine(World world,uint epoch,uint connection)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||world==null||(clientWorld!=null&&(!ReferenceEquals(clientWorld,world)||capacityEpoch!=epoch||capacityConnection!=connection))||(clientWorld==null&&!CanRetireEmptyCapacity))throw new InvalidOperationException("Original capacity projection scope unavailable");
            var originalWorld=clientWorld;uint oldEpoch=capacityEpoch,oldConnection=capacityConnection;ulong oldGeneration=capacityGeneration;int player=clientPlayerEntityId,dogs=clientDogCount,total=clientTotalCount,cap=clientGlobalCap;long revision=clientRevision;var doc=CapacityImage();
            return new RebirthNpcSaveQuarantineDomain("dog-capacity-projection",doc,
                ()=>{lock(Sync)return ReferenceEquals(clientWorld,originalWorld)&&CapacityImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){clientWorld=null;capacityEpoch=capacityConnection=0;capacityGeneration=0;clientPlayerEntityId=-1;clientDogCount=clientTotalCount=0;clientGlobalCap=1;clientRevision=0;}},
                ()=>{lock(Sync){clientWorld=originalWorld;capacityEpoch=oldEpoch;capacityConnection=oldConnection;capacityGeneration=oldGeneration;clientPlayerEntityId=player;clientDogCount=dogs;clientTotalCount=total;clientGlobalCap=cap;clientRevision=revision;}});
        }
    }
    private static XmlDocument CapacityImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("dogCapacity");doc.AppendChild(root);root.SetAttribute("player",clientPlayerEntityId.ToString(CultureInfo.InvariantCulture));root.SetAttribute("dogs",clientDogCount.ToString(CultureInfo.InvariantCulture));root.SetAttribute("total",clientTotalCount.ToString(CultureInfo.InvariantCulture));root.SetAttribute("cap",clientGlobalCap.ToString(CultureInfo.InvariantCulture));root.SetAttribute("revision",clientRevision.ToString(CultureInfo.InvariantCulture));root.SetAttribute("generation",capacityGeneration.ToString(CultureInfo.InvariantCulture));root.SetAttribute("epoch",capacityEpoch.ToString(CultureInfo.InvariantCulture));root.SetAttribute("connection",capacityConnection.ToString(CultureInfo.InvariantCulture));return doc;
    }
}
