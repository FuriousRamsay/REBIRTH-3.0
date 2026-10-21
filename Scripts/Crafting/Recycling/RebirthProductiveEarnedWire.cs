using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine.Scripting;

// Transport/status ONLY. A complete request, Prepared reply or cached ACK is
// never permission to wear tools, change a timer or invoke native queue work.
internal static class RebirthProductiveEarnedWire
{
    internal enum Kind:byte{Describe=0,Prepare=1,Query=2,Hold=3,ReadHold=4,UiPrepare=5,UiQuery=6,UiTerminal=7,UiQueueHandoff=8,UiQueueQuery=9,UiBeforeFlush=10,UiBeforeQuery=11,NativeOutputPlan=12,NativePendingPlan=13,NativeFinalAck=14,NativeToolPlan=15,NativeViewerTools=16,ViewerToolStatus=17}
    private static bool Active=>RebirthProductiveEarnedCoordinator.Enabled||RebirthProductiveEarnedUiOriginal.Enabled||
        RebirthProductiveEarnedUiCoordinator.HasRecipientQueueHandoff||RebirthProductiveEarnedUiRemote.HasRetainedQueueTransition||RebirthProductiveEarnedUiDeferredTransport.HasHostRecipientQueueAuthority;
    internal enum Status:byte{Unknown=0,Described=1,Header=2,Prepared=3,Published=4,Held=5,UiTerminalHeader=6,UiTerminalSaved=7,UiTerminalStationSaved=8,UiQueueHeader=9,UiQueueAccepted=10,UiBeforeHeader=11,UiBeforeSaved=12,NativeOutputHeader=13,NativeOutputSaved=14,NativePendingHeader=15,NativePendingSaved=16,NativeClosed=17,NativeToolHeader=18,NativeToolSaved=19}
    internal sealed class Reply
    {
        internal Guid Operation,Birth,Nonce;internal string World,Owner,Creation,Digest,QueueOwner;
        internal int Deaths;internal Vector3i Position;internal Status State;
    }
    private static readonly RebirthRemoteResourceRepairSendQueue outgoing=new RebirthRemoteResourceRepairSendQueue();
    private static readonly Dictionary<Guid,Reply> replies=new Dictionary<Guid,Reply>();
    private static bool installed;
    internal static void Install()
    {
        if(installed)return;
        RebirthProductiveEarnedRemoteHost.Install();
        RebirthProductiveEarnedCancellation.Install();
        ModEvents.GameUpdate.RegisterHandler(Update);installed=true;
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {if(Active){RebirthProductiveEarnedRemoteHost.ObserveNativeWorldBoundary();RebirthProductiveEarnedPendingToolGestures.Pump();RebirthProductiveEarnedCoordinator.PumpNativeTerminals();RebirthProductiveEarnedRemoteClient.Update();RebirthProductiveEarnedUiCoordinator.Update();outgoing.Pump(4);}}
    internal static bool Send(EntityPlayerLocal actor,TileEntityWorkstation station,Kind kind,Guid operation,Guid nonce,byte[] artifact,string digest,bool nativeRefresh=false)
    {
        if(!Active||actor==null||station==null||operation==Guid.Empty||actor.IsDead()||
            actor.world?.IsRemote()!=true||!ReferenceEquals(actor.world,GameManager.Instance?.World)||
            !ReferenceEquals(actor.world.GetPrimaryPlayer(),actor)||!ReferenceEquals(actor.world.GetTileEntity(station.ToWorldPos()),station)||
            LockManager.Instance?.IsLockedByLocalPlayer(station,0)!=true)return false;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var connection=manager?.connectionToServer?[0];
        if(manager?.IsClient!=true||manager.IsServer||connection==null)return false;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(actor),world=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world);
        if(world==null)return false;
        int deaths=actor.Died;var position=station.ToWorldPos();
        bool Current()=>Active&&ReferenceEquals(actor.world,GameManager.Instance?.World)&&!actor.IsDead()&&
            actor.Died==deaths&&RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&
            RebirthProductiveEarnedArtifact.WorldIdentity(actor.world)==world&&ReferenceEquals(actor.world.GetTileEntity(position),station)&&
            LockManager.Instance?.IsLockedByLocalPlayer(station,0)==true&&
            (kind!=Kind.UiQueueHandoff&&kind!=Kind.UiQueueQuery||RebirthProductiveEarnedUiCoordinator.CanSendQueueHandoff(actor,station,operation,digest))&&
            (kind!=Kind.UiBeforeFlush&&kind!=Kind.UiBeforeQuery||RebirthProductiveEarnedUiCoordinator.CanSendRemoteBefore(actor,station,operation,digest));
        if(!Current())return false;
        if(kind!=Kind.Prepare&&kind!=Kind.UiPrepare&&kind!=Kind.UiTerminal&&kind!=Kind.UiQueueHandoff&&kind!=Kind.UiBeforeFlush&&kind!=Kind.NativeOutputPlan&&kind!=Kind.NativePendingPlan&&kind!=Kind.NativeToolPlan)
        {
            if(!RebirthLogisticsOriginalTransport.CanSendNative(connection))return false;
            manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthProductiveEarnedRequest>().Setup(actor.entityId,creation,deaths,position,
                kind,operation,nonce,digest??"",0,0,0,kind==Kind.Hold?RebirthProductiveEarnedRemoteHost.EarlyManifest(RebirthProductiveEarnedRowProof.Decode(artifact),artifact):nativeRefresh&&kind==Kind.Query?"nativeOriginalEcho":""));return true;
        }
        if(artifact==null||artifact.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
        if(kind!=Kind.UiTerminal&&kind!=Kind.UiQueueHandoff&&kind!=Kind.UiBeforeFlush&&kind!=Kind.NativeOutputPlan&&kind!=Kind.NativePendingPlan&&kind!=Kind.NativeToolPlan)
        {var frame=kind==Kind.UiPrepare?RebirthProductiveEarnedUiOriginal.ReadUiArtifact(artifact).Earned:RebirthProductiveEarnedRowProof.Decode(artifact);if(frame.Operation!=operation||frame.World!=world)return false;}
        string sendKey="earned|"+world+"|"+actor.entityId+"|"+operation.ToString("N");
        if(outgoing.Count>=4&&!outgoing.IsQueued(sendKey))return false;
        var body=new RebirthRemoteResourceFragments.Body(artifact);
        if(digest!=null&&digest!=body.Hash)return false;
        Install();
        return outgoing.Enqueue(sendKey,body,Current,
            ()=>RebirthLogisticsOriginalTransport.CanSendNative(connection),index=>manager.SendToServer(
                NetPackageManager.GetPackage<NetPackageRebirthProductiveEarnedRequest>().Setup(actor.entityId,creation,deaths,position,
                    kind,operation,nonce,body.Hash,body.Length,body.Count,index,body.Part(index))));
    }
    internal static bool TryReadReply(Guid operation,out Reply reply)=>replies.TryGetValue(operation,out reply);
    internal static bool SendViewerToolStatus(EntityPlayerLocal actor,Vector3i position,Guid operation,Guid nonce,byte[] plan)
    {
        if(!Active||actor==null||actor.IsDead()||!actor.IsSpawned()||actor.world?.IsRemote()!=true||operation==Guid.Empty||nonce==Guid.Empty||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetPrimaryPlayer(),actor)||plan==null||plan.Length<1||plan.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var connection=manager?.connectionToServer?[0];
        if(manager?.IsClient!=true||manager.IsServer||!RebirthLogisticsOriginalTransport.CanSendNative(connection))return false;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(actor),world=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world);int deaths=actor.Died;
        bool Current()=>Active&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&!actor.IsDead()&&actor.Died==deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&RebirthProductiveEarnedArtifact.WorldIdentity(actor.world)==world&&ReferenceEquals(manager.connectionToServer?[0],connection);
        string key="earned-viewer-tools-status|"+world+"|"+actor.entityId+"|"+operation.ToString("N");
        if(outgoing.Count>=4&&!outgoing.IsQueued(key))return false;
        var body=new RebirthRemoteResourceFragments.Body(plan);Install();
        return outgoing.Enqueue(key,body,Current,()=>RebirthLogisticsOriginalTransport.CanSendNative(connection),index=>manager.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthProductiveEarnedRequest>().Setup(actor.entityId,creation,deaths,position,Kind.ViewerToolStatus,
                operation,nonce,body.Hash,body.Length,body.Count,index,body.Part(index))));
    }
    internal static bool SendNativeFinalStatus(EntityPlayerLocal actor,Vector3i position,Guid operation,Guid nonce,string digest)
    {
        if(!Active||actor==null||actor.IsDead()||!actor.IsSpawned()||actor.world?.IsRemote()!=true||operation==Guid.Empty||nonce==Guid.Empty||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetPrimaryPlayer(),actor))return false;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var connection=manager?.connectionToServer?[0];
        if(manager?.IsClient!=true||manager.IsServer||!RebirthLogisticsOriginalTransport.CanSendNative(connection))return false;
        manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthProductiveEarnedRequest>().Setup(actor.entityId,RebirthCamperCharacterAccess.CurrentCreation(actor),actor.Died,
            position,Kind.NativeFinalAck,operation,nonce,digest,0,0,0,string.Empty));return true;
    }
    internal static void ReleaseNativeClosedStatus(Guid operation,Guid nonce,string digest)
    {if(replies.TryGetValue(operation,out var value)&&value.State==Status.NativeClosed&&value.Nonce==nonce&&value.Digest==digest)replies.Remove(operation);}
    internal static void ReleaseAcceptedQueueStatus(Guid operation,Guid nonce,string digest)
    {if(replies.TryGetValue(operation,out var value)&&value.State==Status.UiQueueAccepted&&value.Nonce==nonce&&value.Digest==digest)replies.Remove(operation);}
    internal static void ReleaseSavedUiStatus(Guid operation,Guid nonce)
    {if(replies.TryGetValue(operation,out var value)&&value.State==Status.UiTerminalSaved&&value.Nonce==nonce)replies.Remove(operation);}
    internal static void Receive(World world,Reply reply)
    {
        // Retain bounded readonly status. The future owner coordinator must
        // correlate its OWN durable original/native images before any effect.
        var actor=world?.GetPrimaryPlayer();
        if(!Active||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||
            actor==null||actor.IsDead()||actor.Died!=reply.Deaths||RebirthCamperCharacterAccess.CurrentCreation(actor)!=reply.Creation||
            RebirthProductiveEarnedArtifact.WorldIdentity(world)!=reply.World||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.StorageKey!=reply.Owner)return;
        if(replies.Count>=32&&!replies.ContainsKey(reply.Operation))return;
        // Unknown never revokes immutable provenance needed to retry the same
        // header. Effect stages still require their exact nonce/digest/native proof.
        if(reply.State==Status.Unknown&&replies.ContainsKey(reply.Operation))return;
        replies[reply.Operation]=reply;
    }
    internal static void Handle(NetPackageRebirthProductiveEarnedRequest packet,World world)
    {
        if(!Active||world?.IsRemote()!=false||!ReferenceEquals(world,GameManager.Instance?.World))return;
        RebirthProductiveEarnedUiCoordinator.ObservePurposeWorld();
        var actor=world.GetEntity(packet.Player) as EntityPlayer;
        if(packet.Action==Kind.ViewerToolStatus)
        {
            if(actor==null||actor.Died!=packet.Deaths||!RebirthProductiveEarnedRemoteHost.ViewerToolMetadataCaller(actor,packet.Sender,packet.Creation)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(packet.Sender,out var payer))return;
            string scope="earned-viewer-tools-status|"+RebirthProductiveEarnedArtifact.WorldIdentity(world)+"|"+payer.StorageKey+"|"+packet.Creation+"|"+packet.Deaths+"|"+packet.Position;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,packet.Operation,packet.Digest,packet.Total,packet.Count,packet.Index,packet.Text,
                DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var complete)||complete==null||
                !RebirthProductiveEarnedRemoteHost.TryHistoricalViewerToolsReply(actor,packet.Sender,packet.Position,packet.Creation,packet.Operation,packet.Nonce,complete,out var saved))return;
            if(!RebirthLogisticsOriginalTransport.CanSendNative(packet.Sender.netConnection[0]))return;
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthProductiveEarnedReply>().Setup(saved),_attachedToEntityId:actor.entityId);return;
        }
        if(actor!=null&&actor.Died==packet.Deaths&&(packet.Action==Kind.Query||packet.Action==Kind.NativeFinalAck)&&
            RebirthProductiveEarnedRemoteHost.TryClosedStatus(actor,packet.Sender,packet.Creation,packet.Position,packet.Operation,packet.Nonce,packet.Digest,out var closedReply))
        {
            var closedConnection=packet.Sender?.netConnection?[0];if(!RebirthLogisticsOriginalTransport.CanSendNative(closedConnection))return;
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthProductiveEarnedReply>().Setup(closedReply),_attachedToEntityId:actor.entityId);return;
        }
        var station=world.GetTileEntity(packet.Position) as TileEntityWorkstation;
        if(actor==null||actor.Died!=packet.Deaths||!RebirthStablePlayerIdentity.TryFromClientInfo(packet.Sender,out var identity)||
            !RebirthProductiveEarnedArtifact.RemoteAuthority(actor,packet.Sender,station,packet.Creation))return;
        Install();
        string actualWorld=RebirthProductiveEarnedArtifact.WorldIdentity(world);if(actualWorld==null)return;
        var reply=new Reply{Operation=packet.Operation,Position=packet.Position,World=actualWorld,
            Owner=identity.StorageKey,Creation=packet.Creation,Deaths=actor.Died,Nonce=packet.Nonce,Digest=packet.Digest,State=Status.Unknown};
        if(packet.Action==Kind.NativeViewerTools)
        {RebirthProductiveEarnedRemoteHost.BeginViewerTools(actor,packet.Sender,station,packet.Creation);return;}
        if(packet.Action==Kind.Describe)
        {
            if(RebirthProductiveEarnedRemoteHost.TryDescribe(actor,packet.Sender,station,packet.Creation,out var birth))
            {reply.Birth=birth;reply.State=Status.Described;
             int index=(station.Queue?.Length??0)-1;if(index>=0&&RebirthProductiveEarnedUiQueueOwner.TryObserveQueueOwner(station,index,station.Queue[index],out var queueOwner))reply.QueueOwner=RebirthProductiveEarnedUiQueueOwner.Export(queueOwner);}
        }
        else if(packet.Action==Kind.ReadHold)
        {
            if(RebirthProductiveEarnedRemoteHost.TryReadEarlyStatus(actor,packet.Sender,station,packet.Creation,packet.Operation,packet.Digest,out var retainedNonce,out var retainedBirth))
            {reply.Nonce=retainedNonce;reply.Birth=retainedBirth;reply.State=Status.Held;}
        }
        else if(packet.Action==Kind.Hold)
        {
            if(RebirthProductiveEarnedRemoteHost.TryHoldEarly(actor,packet.Sender,station,packet.Creation,packet.Operation,packet.Digest,packet.Text,out var heldNonce,out var heldBirth))
            {reply.Nonce=heldNonce;reply.Birth=heldBirth;reply.State=Status.Held;}
        }
        else if(packet.Action==Kind.NativeFinalAck)
        {
            if(RebirthProductiveEarnedRemoteHost.AcknowledgeNativeFinal(actor,packet.Sender,station,packet.Creation,packet.Operation,packet.Nonce,packet.Digest,out var closed))reply=closed;
        }
        else if(packet.Action==Kind.NativeToolPlan)
        {
            string scope="earned-native-tools|"+reply.World+"|"+reply.Owner+"|"+reply.Creation+"|"+reply.Deaths+"|"+packet.Position;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,packet.Operation,packet.Digest,packet.Total,packet.Count,packet.Index,packet.Text,
                DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var complete)||complete==null)return;
            if(!RebirthProductiveEarnedRemoteHost.RegisterNativeTools(actor,packet.Sender,station,packet.Creation,packet.Operation,packet.Nonce,complete,out var nativeReply))return;reply=nativeReply;
        }
        else if(packet.Action==Kind.NativePendingPlan)
        {
            string scope="earned-native-pending|"+reply.World+"|"+reply.Owner+"|"+reply.Creation+"|"+reply.Deaths+"|"+packet.Position;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,packet.Operation,packet.Digest,packet.Total,packet.Count,packet.Index,packet.Text,
                DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var complete)||complete==null)return;
            if(!RebirthProductiveEarnedRemoteHost.RegisterNativePending(actor,packet.Sender,station,packet.Creation,packet.Operation,packet.Nonce,complete,out var nativeReply))return;reply=nativeReply;
        }
        else if(packet.Action==Kind.NativeOutputPlan)
        {
            string scope="earned-native-output|"+reply.World+"|"+reply.Owner+"|"+reply.Creation+"|"+reply.Deaths+"|"+packet.Position;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,packet.Operation,packet.Digest,packet.Total,packet.Count,packet.Index,packet.Text,
                DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var complete)||complete==null)return;
            if(!RebirthProductiveEarnedRemoteHost.RegisterNativeOutput(actor,packet.Sender,station,packet.Creation,packet.Operation,packet.Nonce,complete,out var nativeReply))return;reply=nativeReply;
        }
        else if(packet.Action==Kind.UiBeforeFlush)
        {
            string scope="earned-ui-before|"+reply.World+"|"+reply.Owner+"|"+reply.Creation+"|"+reply.Deaths+"|"+packet.Position;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,packet.Operation,packet.Digest,packet.Total,packet.Count,packet.Index,packet.Text,
                DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var complete)||complete==null)return;
            if(!RebirthProductiveEarnedUiRemoteBefore.Register(actor,packet.Sender,station,packet.Creation,packet.Operation,complete,out var beforeReply))return;reply=beforeReply;
        }
        else if(packet.Action==Kind.UiBeforeQuery)
        {if(!RebirthProductiveEarnedUiRemoteBefore.Query(actor,packet.Sender,station,packet.Creation,packet.Operation,packet.Nonce,packet.Digest,out var beforeReply))return;reply=beforeReply;}
        else if(packet.Action==Kind.UiQueueHandoff)
        {
            string scope="earned-ui-queue|"+reply.World+"|"+reply.Owner+"|"+reply.Creation+"|"+reply.Deaths+"|"+packet.Position;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,packet.Operation,packet.Digest,packet.Total,packet.Count,packet.Index,packet.Text,
                DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var complete)||complete==null)return;
            if(!RebirthProductiveEarnedUiRemote.RegisterQueueHandoff(actor,packet.Sender,station,packet.Creation,packet.Operation,complete,out var queueReply))return;reply=queueReply;
        }
        else if(packet.Action==Kind.UiQueueQuery)
        {if(!RebirthProductiveEarnedUiRemote.QueryQueueHandoff(actor,packet.Sender,station,packet.Creation,packet.Operation,packet.Nonce,packet.Digest,out var queueReply))return;reply=queueReply;}
        else if(packet.Action==Kind.UiTerminal)
        {
            string scope="earned-ui-terminal|"+reply.World+"|"+reply.Owner+"|"+reply.Creation+"|"+reply.Deaths+"|"+packet.Position;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,packet.Operation,packet.Digest,packet.Total,packet.Count,packet.Index,packet.Text,
                DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var complete)||complete==null)return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {if(decoder==null||!RebirthProductiveEarnedUiRemote.RegisterTerminal(actor,packet.Sender,station,packet.Creation,packet.Operation,packet.Nonce,complete,out var uiReply))return;reply=uiReply;}
        }
        else if(packet.Action==Kind.UiPrepare)
        {
            string scope="earned-ui|"+reply.World+"|"+reply.Owner+"|"+reply.Creation+"|"+reply.Deaths+"|"+packet.Position;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,packet.Operation,packet.Digest,packet.Total,packet.Count,packet.Index,packet.Text,
                DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var complete)||complete==null)return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {if(decoder==null||!RebirthProductiveEarnedUiRemote.Register(actor,packet.Sender,station,packet.Creation,complete,decoder,out var uiReply))return;reply=uiReply;}
        }
        else if(packet.Action==Kind.UiQuery)
        {if(!RebirthProductiveEarnedUiRemote.Query(actor,packet.Sender,station,packet.Creation,packet.Operation,packet.Nonce,packet.Digest,out var uiReply))return;reply=uiReply;}
        else if(packet.Action==Kind.Prepare)
        {
            string scope="earned|"+reply.World+"|"+reply.Owner+"|"+reply.Creation+"|"+reply.Deaths+"|"+packet.Position;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,packet.Operation,packet.Digest,packet.Total,packet.Count,packet.Index,
                packet.Text,DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var complete)||complete==null)return;
            if(complete.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return; // Retain exact original; pressure grants no terminal status.
                var frame=RebirthProductiveEarnedRowProof.Decode(complete);
                if(frame.Operation!=packet.Operation||!decoder.Admit(frame,complete.Length))return;
                if(RebirthProductiveEarnedRemoteHost.Prepare(actor,packet.Sender,station,packet.Creation,complete,packet.Nonce,decoder,out var nonce,out var digest))
                {reply.Birth=frame.Birth;reply.Nonce=nonce;reply.Digest=digest;reply.State=Status.Header;}
            }
        }
        else if(packet.Action==Kind.Query&&RebirthProductiveEarnedRemoteHost.TryRebindQuery(actor,packet.Sender,station,packet.Creation,packet.Operation,packet.Nonce,packet.Digest)&&RebirthProductiveEarnedRemoteHost.TryOriginalBirth(actor,packet.Sender,packet.Operation,packet.Nonce,packet.Digest,out var originalBirth))
        {
            reply.Birth=originalBirth;
            if(RebirthProductiveEarnedRemoteHost.QueryNativeOutput(actor,packet.Sender,packet.Operation,packet.Nonce,packet.Digest,out var nativeOutputReply))reply=nativeOutputReply;
            else if(RebirthProductiveEarnedRemoteHost.TryPublished(actor,packet.Sender,packet.Operation,packet.Nonce,packet.Digest,out bool published)&&published)
                reply.State=Status.Published;
            else if(RebirthProductiveEarnedRemoteHost.TryBeforePublished(actor,packet.Sender,packet.Operation,packet.Nonce,packet.Digest,out bool before))
                reply.State=before?Status.Prepared:Status.Header;
            if(packet.Text=="nativeOriginalEcho")RebirthProductiveEarnedRemoteHost.SendOriginalNativeEcho(actor,packet.Sender,packet.Operation,packet.Nonce,packet.Digest);
        }
        // A small typed reply never stands in for the native saved proof.
        var connection=packet.Sender?.netConnection?[0];
        if(!RebirthLogisticsOriginalTransport.CanSendNative(connection))return; // Original retries same query; no new operation.
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthProductiveEarnedReply>().Setup(reply),_attachedToEntityId:actor.entityId);
    }
}
[Preserve]
public sealed class NetPackageRebirthProductiveEarnedRequest:NetPackage
{
    internal int Player,Deaths,Total,Count,Index;internal Vector3i Position;
    internal string Creation,Digest,Text;internal Guid Operation,Nonce;internal RebirthProductiveEarnedWire.Kind Action;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthProductiveEarnedRequest Setup(int player,string creation,int deaths,Vector3i position,RebirthProductiveEarnedWire.Kind action,
        Guid operation,Guid nonce,string digest,int total,int count,int index,string text)
    {Player=player;Creation=creation;Deaths=deaths;Position=position;Action=action;Operation=operation;Nonce=nonce;Digest=digest;Total=total;Count=count;Index=index;Text=text;return this;}
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadByte()!=1)throw new InvalidDataException();Player=reader.ReadInt32();Creation=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512);Deaths=reader.ReadInt32();
        Position=new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32());Action=(RebirthProductiveEarnedWire.Kind)reader.ReadByte();
        if((byte)Action>17||!Guid.TryParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N",out Operation)||Operation==Guid.Empty||
            !Guid.TryParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N",out Nonce))throw new InvalidDataException();
        Digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);Total=reader.ReadInt32();Count=reader.ReadInt32();Index=reader.ReadInt32();
        Text=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,12000);
        if(Action==RebirthProductiveEarnedWire.Kind.Prepare||Action==RebirthProductiveEarnedWire.Kind.UiPrepare||Action==RebirthProductiveEarnedWire.Kind.UiTerminal||Action==RebirthProductiveEarnedWire.Kind.UiQueueHandoff||Action==RebirthProductiveEarnedWire.Kind.UiBeforeFlush||Action==RebirthProductiveEarnedWire.Kind.NativeOutputPlan||Action==RebirthProductiveEarnedWire.Kind.NativePendingPlan||Action==RebirthProductiveEarnedWire.Kind.NativeToolPlan||Action==RebirthProductiveEarnedWire.Kind.ViewerToolStatus)
        {if(Total<1||Total>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||Count!=(Total+8999)/9000||Index<0||Index>=Count||Digest.Length!=64||!Digest.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f'))throw new InvalidDataException();}
        else if(Action==RebirthProductiveEarnedWire.Kind.Hold)
        {if(Total!=0||Count!=0||Index!=0||Text.Length!=120||Digest.Length!=64||!Digest.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f'))throw new InvalidDataException();}
        else if(Total!=0||Count!=0||Index!=0||Text.Length!=0&&(Action!=RebirthProductiveEarnedWire.Kind.Query||Text!="nativeOriginalEcho"))throw new InvalidDataException();
    }
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);writer.Write(Player);writer.Write(Creation);writer.Write(Deaths);writer.Write(Position.x);writer.Write(Position.y);writer.Write(Position.z);writer.Write((byte)Action);
     writer.Write(Operation.ToString("N"));writer.Write(Nonce.ToString("N"));writer.Write(Digest);writer.Write(Total);writer.Write(Count);writer.Write(Index);writer.Write(Text);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world?.IsRemote()==false&&Sender!=null&&ValidEntityIdForSender(Player))try{RebirthProductiveEarnedWire.Handle(this,world);}catch{/* Typed original remains unknown. */}}
    public int GetLength()=>0;
}
[Preserve]
public sealed class NetPackageRebirthProductiveEarnedReply:NetPackage
{
    private RebirthProductiveEarnedWire.Reply value;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthProductiveEarnedReply Setup(RebirthProductiveEarnedWire.Reply reply){value=reply;return this;}
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadByte()!=2)throw new InvalidDataException();value=new RebirthProductiveEarnedWire.Reply();
        if(!Guid.TryParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N",out value.Operation)||value.Operation==Guid.Empty||
            !Guid.TryParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N",out value.Birth)||
            !Guid.TryParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N",out value.Nonce))throw new InvalidDataException();
        value.State=(RebirthProductiveEarnedWire.Status)reader.ReadByte();if((byte)value.State>19)throw new InvalidDataException();
        value.World=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,44);value.Owner=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
        value.Creation=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512);value.Deaths=reader.ReadInt32();value.Digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
        value.Position=new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32());
        value.QueueOwner=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,4096);
        if(value.Deaths<0||value.State!=RebirthProductiveEarnedWire.Status.Unknown&&value.Birth==Guid.Empty||
            (byte)value.State>=2&&(value.Nonce==Guid.Empty||value.Digest.Length!=64||
                !value.Digest.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')))throw new InvalidDataException();
    }
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)2);writer.Write(value.Operation.ToString("N"));writer.Write(value.Birth.ToString("N"));writer.Write(value.Nonce.ToString("N"));writer.Write((byte)value.State);
     writer.Write(value.World);writer.Write(value.Owner);writer.Write(value.Creation);writer.Write(value.Deaths);writer.Write(value.Digest??"");writer.Write(value.Position.x);writer.Write(value.Position.y);writer.Write(value.Position.z);writer.Write(value.QueueOwner??"");}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==true)RebirthProductiveEarnedWire.Receive(world,value);}
    public int GetLength()=>0;
}
