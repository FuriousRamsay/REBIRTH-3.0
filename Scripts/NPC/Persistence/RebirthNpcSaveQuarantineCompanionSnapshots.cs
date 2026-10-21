using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthCompanionSnapshotResponseV2:NetPackage
{
    private uint epoch,connection;private ulong generation,requestEpoch,requestId;private RebirthCompanionSnapshot snapshot;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthCompanionSnapshotResponseV2 Setup(ulong requestSession,ulong request,RebirthCompanionSnapshot value,uint recipientConnection)
    {epoch=RebirthNpcNetworkEpoch.GetServerEpoch();connection=recipientConnection;generation=RebirthNpcRecoveryGeneration.RequireCurrent();requestEpoch=requestSession;requestId=request;snapshot=value;return this;}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);BinaryWriter w=(BinaryWriter)writer;w.Write((ushort)1);w.Write(epoch);w.Write(connection);w.Write(generation);w.Write(requestEpoch);w.Write(requestId);RebirthCompanionSnapshotCodec.Write(w,snapshot??new RebirthCompanionSnapshot());}
    public override void read(PooledBinaryReader reader)
    {if(reader.ReadUInt16()!=1)throw new InvalidDataException("Unsupported companion recovery snapshot version");epoch=reader.ReadUInt32();connection=reader.ReadUInt32();generation=reader.ReadUInt64();requestEpoch=reader.ReadUInt64();requestId=reader.ReadUInt64();snapshot=RebirthCompanionSnapshotCodec.Read((BinaryReader)reader);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world?.IsRemote()==true)RebirthCompanionSnapshotService.ReceiveQualified(requestEpoch,requestId,snapshot,world,epoch,connection,generation);}
    public int GetLength()=>0;
}

public static partial class RebirthCompanionSnapshotService
{
    private static ulong SnapshotGeneration(World world)=>world.IsRemote()?RebirthNpcRecoveryProjectionScope.Current:RebirthNpcRecoveryGeneration.RequireCurrent();
    private static bool CanRequestSnapshot()
    {
        var world=GameManager.Instance?.World;var actor=world?.GetPrimaryPlayer();
        if(!ThreadManager.IsMainThread()||world==null||actor==null||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        foreach(var p in Pending.Values)if(!SnapshotCurrent(p))return false;return true;
    }
    private static PendingRequest CapturePendingSnapshot(World world,EntityPlayerLocal player,Action<RebirthCompanionSnapshot> callback)
    {return new PendingRequest{World=world,Actor=player,Callback=callback,Deadline=Time.unscaledTime+RequestTimeoutSeconds,Epoch=world.IsRemote()?RebirthNpcNetworkEpoch.ClientEpoch:RebirthNpcNetworkEpoch.GetServerEpoch(),Connection=world.IsRemote()?RebirthNpcNetworkEpoch.ClientConnectionEpoch:0,Generation=SnapshotGeneration(world)};}
    private static bool SnapshotCurrent(PendingRequest p)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||p==null||world==null||!ReferenceEquals(p.World,world)||!ReferenceEquals(p.Actor?.world,world)||!ReferenceEquals(world.GetEntity(p.Actor.entityId),p.Actor)||!ReferenceEquals(world.GetPrimaryPlayer(),p.Actor))return false;
        return p.Generation==SnapshotGeneration(world)&&p.Epoch==(world.IsRemote()?RebirthNpcNetworkEpoch.ClientEpoch:RebirthNpcNetworkEpoch.GetServerEpoch())&&p.Connection==(world.IsRemote()?RebirthNpcNetworkEpoch.ClientConnectionEpoch:0);
    }
    internal static void ReceiveQualified(ulong session,ulong request,RebirthCompanionSnapshot snapshot,World world,uint epoch,uint connection,ulong generation)
    {
        if(!ThreadManager.IsMainThread()||world==null||!ReferenceEquals(world,GameManager.Instance?.World)||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return;
        if(world.IsRemote()&&(connection==0||connection!=RebirthNpcNetworkEpoch.ClientConnectionEpoch||!RebirthNpcNetworkEpoch.AcceptClientEpoch(epoch)||!RebirthNpcRecoveryProjectionScope.Observe(generation)))return;
        if(!world.IsRemote()&&(epoch!=RebirthNpcNetworkEpoch.GetServerEpoch()||connection!=0||generation!=RebirthNpcRecoveryGeneration.RequireCurrent()))return;
        if(session!=requestEpoch||request==0||!Pending.TryGetValue(request,out var pending)||!SnapshotCurrent(pending)||!MatchesSnapshotStamp(pending.Epoch,pending.Connection,pending.Generation,epoch,connection,generation))return;
        Pending.Remove(request);var value=snapshot??new RebirthCompanionSnapshot();RebirthCompanionService.AugmentLocalSnapshot(value);pending.Callback(value);
    }
    internal static bool MatchesSnapshotStamp(uint capturedEpoch,uint capturedConnection,ulong capturedGeneration,uint epoch,uint connection,ulong generation)
    {return capturedEpoch==epoch&&capturedConnection==connection&&capturedGeneration==generation;}
    internal static bool CanRetireEmptySnapshots=>ThreadManager.IsMainThread()&&Pending.Count==0;
    internal static RebirthNpcSaveQuarantineDomain PrepareSnapshotQuarantine(World world,uint epoch,uint connection)
    {
        if(!ThreadManager.IsMainThread()||world==null)throw new InvalidOperationException("Original companion callback scope unavailable");
        var records=Pending;var next=new Dictionary<ulong,PendingRequest>();ulong session=requestEpoch,allocator=nextRequestId;var doc=SnapshotPendingImage(world,epoch,connection);
        return new RebirthNpcSaveQuarantineDomain("companion-snapshot-callbacks",doc,
            ()=>ThreadManager.IsMainThread()&&ReferenceEquals(Pending,records)&&requestEpoch==session&&nextRequestId==allocator&&SnapshotPendingImage(world,epoch,connection).OuterXml==doc.OuterXml,
            ()=>{Pending=next;unchecked{requestEpoch++;}if(requestEpoch==0)requestEpoch=1;},
            ()=>{Pending=records;requestEpoch=session;nextRequestId=allocator;});
    }
    private static XmlDocument SnapshotPendingImage(World world,uint epoch,uint connection)
    {
        if(Pending.Count>MaximumPendingRequests)throw new InvalidOperationException("Companion callback bound exceeded");var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("companionSnapshots");doc.AppendChild(root);root.SetAttribute("session",requestEpoch.ToString(CultureInfo.InvariantCulture));root.SetAttribute("allocator",nextRequestId.ToString(CultureInfo.InvariantCulture));var keys=new List<ulong>(Pending.Keys);keys.Sort();
        foreach(ulong key in keys)
        {
            var p=Pending[key];if(key==0||p?.Callback==null||!ReferenceEquals(p.World,world)||p.Actor==null||!ReferenceEquals(p.Actor.world,world)||p.Epoch!=epoch||p.Connection!=connection||float.IsNaN(p.Deadline)||float.IsInfinity(p.Deadline))throw new InvalidOperationException("Original companion callback identity unknown");var row=doc.CreateElement("pending");root.AppendChild(row);row.SetAttribute("request",key.ToString(CultureInfo.InvariantCulture));row.SetAttribute("actor",p.Actor.entityId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("generation",p.Generation.ToString(CultureInfo.InvariantCulture));row.SetAttribute("epoch",p.Epoch.ToString(CultureInfo.InvariantCulture));row.SetAttribute("connection",p.Connection.ToString(CultureInfo.InvariantCulture));row.SetAttribute("deadline",p.Deadline.ToString("R",CultureInfo.InvariantCulture));
        }
        return doc;
    }
}
