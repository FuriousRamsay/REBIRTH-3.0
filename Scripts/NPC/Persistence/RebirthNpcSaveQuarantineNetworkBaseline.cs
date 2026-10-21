using System;
using System.Collections.Generic;
using System.Globalization;

public static partial class RebirthNpcNetworkBaselineService
{
    private sealed class PeerConnection
    {
        internal readonly World World;internal readonly ConnectionManager Manager;internal readonly ClientInfo Peer;internal readonly INetConnection Transport;internal readonly int Entity;internal readonly uint Epoch;internal ulong Sequence;
        internal PeerConnection(World world,ConnectionManager manager,ClientInfo peer,INetConnection transport,uint epoch){World=world;Manager=manager;Peer=peer;Transport=transport;Entity=peer.entityId;Epoch=epoch;}
    }
    private static Dictionary<int,PeerConnection> Connections=new Dictionary<int,PeerConnection>();
    private static RebirthNpcAncillaryOriginalScope baselineScope;
    private static bool UseBaselineScope(){lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref baselineScope,Pending.Count==0&&FairOrder.Count==0&&Enqueued.Count==0);}
    private static bool BaselineJobCurrent(BaselineJob job)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(job==null||!ReferenceEquals(manager,job.RecipientManager)||job.Original==null||!ReferenceEquals(job.Original,baselineScope)||!job.Original.Current()||manager?.IsServer!=true||job.Recipient==null||!job.Recipient.loginDone||!job.Recipient.bAttachedToEntity||job.Recipient.entityId!=job.RecipientEntityId||job.Recipient.disconnecting||job.Transport==null||job.Transport.IsDisconnected()||job.Recipient.netConnection==null||job.Recipient.netConnection.Length==0||!ReferenceEquals(job.Recipient.netConnection[0],job.Transport)||!ReferenceEquals(manager.Clients.ForEntityId(job.RecipientEntityId),job.Recipient)||!RebirthNpcNetworkEpoch.ValidateConnectionEpoch(job.RecipientEntityId,job.ConnectionEpoch)||job.WorldEpoch!=RebirthNpcNetworkEpoch.GetServerEpoch())return false;
        lock(Sync)return Pending.TryGetValue(job.RecipientEntityId,out var current)&&ReferenceEquals(current,job);
    }
    private static bool EnsureBaselineStart(BaselineJob job,ConnectionManager manager)
    {
        if(!BaselineJobCurrent(job)||!ReferenceEquals(manager,job.RecipientManager))return false;
        if(job.HeaderAcknowledged)return true;
        double now=UnityEngine.Time.realtimeSinceStartup;
        if(now<job.HeaderRetry)return false;
        job.HeaderRetry=now+0.75;
        manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcBaselineStartV2>().Setup(job.WorldEpoch,job.ConnectionEpoch,job.Original.Generation,job.Sequence,job.Limit,job.InitialSkipped),_attachedToEntityId:job.RecipientEntityId);
        if(!BaselineJobCurrent(job))return false;job.StartReturned=true;return job.HeaderAcknowledged;
    }
    internal static void ReceiveHeaderAck(NetPackage package,World world,RebirthNpcBaselineFraming.Frame f)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(world,GameManager.Instance?.World)||package?.Sender==null||!package.ValidEntityIdForSender(package.Sender.entityId))return;
        lock(Sync)
        {
            if(!Pending.TryGetValue(package.Sender.entityId,out var job)||!ReferenceEquals(job.Recipient,package.Sender)||!ReferenceEquals(job.RecipientManager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!ReferenceEquals(job.Original?.World,world)||!job.StartReturned||!BaselineJobCurrent(job)||!RebirthNpcBaselineFraming.SameHeader(new RebirthNpcBaselineFraming.Frame{Epoch=job.WorldEpoch,Connection=job.ConnectionEpoch,Generation=job.Original.Generation,Sequence=job.Sequence,First=job.Limit,Second=job.InitialSkipped},f))return;
            job.HeaderAcknowledged=true;
        }
    }
    private static void RequeueOriginalBaseline(BaselineJob job)
    {
        // Exact retained scheduling metadata only; never a native send/admission.
        lock(Sync){if(job!=null&&Pending.TryGetValue(job.RecipientEntityId,out var current)&&ReferenceEquals(current,job)&&FairOrder.Count<MaxPendingClients*2&&Enqueued.Add(job.RecipientEntityId))FairOrder.Enqueue(job.RecipientEntityId);}
    }
    private static bool PeerGone(PeerConnection connection)=>connection?.Manager!=null&&connection.Peer!=null&&connection.Transport!=null&&(!ReferenceEquals(connection.Manager.Clients.ForEntityId(connection.Entity),connection.Peer)||connection.Transport.IsDisconnected()||connection.Peer.netConnection==null||connection.Peer.netConnection.Length==0||!ReferenceEquals(connection.Peer.netConnection[0],connection.Transport));
    private static bool TryPeerConnection(World world,ConnectionManager manager,ClientInfo peer,out PeerConnection connection)
    {
        connection=null;if(peer?.netConnection==null||peer.netConnection.Length==0||peer.netConnection[0]==null||peer.netConnection[0].IsDisconnected())return false;lock(Sync)
        {
            if(Connections.TryGetValue(peer.entityId,out var prior))
            {
                if(ReferenceEquals(prior.World,world)&&ReferenceEquals(prior.Manager,manager)&&ReferenceEquals(prior.Peer,peer)&&prior.Entity==peer.entityId&&ReferenceEquals(prior.Transport,peer.netConnection[0])&&!prior.Transport.IsDisconnected()&&RebirthNpcNetworkEpoch.ValidateConnectionEpoch(peer.entityId,prior.Epoch)){connection=prior;return true;}
                if(!PeerGone(prior))return false;Connections.Remove(peer.entityId);
            }
            var gone=new List<int>();foreach(var row in Connections)if(PeerGone(row.Value))gone.Add(row.Key);foreach(int id in gone)Connections.Remove(id);
            if(Connections.Count>=MaxPendingClients)return false;
            uint epoch=RebirthNpcNetworkEpoch.IssueConnectionEpoch(peer.entityId);if(epoch==0)return false;
            connection=new PeerConnection(world,manager,peer,peer.netConnection[0],epoch);Connections.Add(peer.entityId,connection);return true;
        }
    }
    private static bool RetireDisconnectedJob(BaselineJob job)
    {
        // Projection metadata only: exact captured original manager/peer removal
        // readback is sufficient. Never actor/item/payment/ACK authority, and no
        // current-B save reader or numeric replacement peer is adopted.
        if(!ThreadManager.IsMainThread()||job?.Original?.World==null||job.RecipientManager==null||job.Recipient==null||job.Transport==null||(ReferenceEquals(job.RecipientManager.Clients.ForEntityId(job.RecipientEntityId),job.Recipient)&&!job.Transport.IsDisconnected()&&job.Recipient.netConnection!=null&&job.Recipient.netConnection.Length>0&&ReferenceEquals(job.Recipient.netConnection[0],job.Transport)))return false;
        lock(Sync){if(!Pending.TryGetValue(job.RecipientEntityId,out var current)||!ReferenceEquals(current,job))return false;if(FairOrder.Count>MaxPendingClients*2)return false;var queue=new Queue<int>();foreach(int id in FairOrder)if(id!=job.RecipientEntityId)queue.Enqueue(id);Pending.Remove(job.RecipientEntityId);Enqueued.Remove(job.RecipientEntityId);FairOrder=queue;return true;}
    }
    internal static void RetireDisconnectedBaselines()
    {
        if(!ThreadManager.IsMainThread()||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return;
        lock(Sync){if(Pending.Count>MaxPendingClients||FairOrder.Count>MaxPendingClients*2)return;var jobs=new List<BaselineJob>(Pending.Values);foreach(var job in jobs)RetireDisconnectedJob(job);}
    }
    internal static bool CanRetireEmptyBaseline
    {get{lock(Sync){if(!ThreadManager.IsMainThread()||Pending.Count!=0||FairOrder.Count!=0||Enqueued.Count!=0)return false;foreach(var row in Connections.Values)if(!PeerGone(row))return false;return true;}}}
    internal static bool HasNoPendingBaseline{get{lock(Sync)return ThreadManager.IsMainThread()&&Pending.Count==0&&FairOrder.Count==0&&Enqueued.Count==0;}}
    private static void ResetBaselineConnections(){if(!CanRetireEmptyBaseline)throw new InvalidOperationException("Original live baseline connections remain retained");Connections.Clear();}
    internal static RebirthNpcSaveQuarantineDomain PrepareBaselineQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,baselineScope);if(!HasNoPendingBaseline||Connections.Count>MaxPendingClients||(baselineScope==null&&Connections.Count!=0))throw new InvalidOperationException("Original native baseline jobs require qualified continuation");
            var pending=Pending;var order=FairOrder;var enqueued=Enqueued;var connections=Connections;var original=baselineScope;var observed=new Dictionary<int,PeerConnection>(Connections);var epochs=new Dictionary<int,uint>();var sequences=new Dictionary<int,ulong>();var doc=RebirthNpcAncillaryOriginalScope.Image("networkBaseline",original,Array.Empty<long>());doc.DocumentElement.SetAttribute("pendingEmptyOnly","1");var ids=new List<int>(Connections.Keys);ids.Sort();foreach(int id in ids){var row=Connections[id];if(row?.World==null||row.Manager==null||row.Peer==null||row.Transport==null||row.Entity!=id||row.Epoch==0||(original!=null&&!ReferenceEquals(row.World,original.World)))throw new InvalidOperationException("Original baseline connection handle unknown");epochs[id]=row.Epoch;sequences[id]=row.Sequence;var item=doc.CreateElement("connection");item.SetAttribute("entity",id.ToString(CultureInfo.InvariantCulture));item.SetAttribute("epoch",row.Epoch.ToString(CultureInfo.InvariantCulture));item.SetAttribute("sequence",row.Sequence.ToString(CultureInfo.InvariantCulture));doc.DocumentElement.AppendChild(item);}
            var nextPending=new Dictionary<int,BaselineJob>();var nextOrder=new Queue<int>();var nextEnqueued=new HashSet<int>();var nextConnections=new Dictionary<int,PeerConnection>();
            return new RebirthNpcSaveQuarantineDomain("network-baseline",doc,
                ()=>{lock(Sync){if(!HasNoPendingBaseline||!ReferenceEquals(Pending,pending)||!ReferenceEquals(FairOrder,order)||!ReferenceEquals(Enqueued,enqueued)||!ReferenceEquals(Connections,connections)||!ReferenceEquals(baselineScope,original)||Connections.Count!=observed.Count)return false;foreach(var row in observed)if(!Connections.TryGetValue(row.Key,out var current)||!ReferenceEquals(current,row.Value)||current.Epoch!=epochs[row.Key]||current.Sequence!=sequences[row.Key])return false;return true;}},
                ()=>{lock(Sync){Pending=nextPending;FairOrder=nextOrder;Enqueued=nextEnqueued;Connections=nextConnections;baselineScope=null;}},
                ()=>{lock(Sync){Pending=pending;FairOrder=order;Enqueued=enqueued;Connections=connections;baselineScope=original;}});
        }
    }
}
internal static class RebirthNpcNetworkRetirementPreflight
{
    internal static bool CanReset=>ThreadManager.IsMainThread()&&RebirthNpcEquipmentProjectionReceipts.CanRetireEmptyReceipts&&RebirthNpcNetworkEpoch.CanRetireEpochState&&RebirthNpcNetworkBaselineService.CanRetireEmptyBaseline&&RebirthNpcRemoteCommandService.CanRetireEmptyRemoteCommands&&RebirthNpcPendingRuntimeStates.CanRetireEmptyProjection&&RebirthNpcPendingEquipmentSnapshots.CanRetireEmptyProjection&&RebirthNpcPendingAppearanceSnapshots.CanRetireEmptyProjection&&RebirthNpcRecoveryProjectionNetwork.CanRetireEmptyMailbox&&RebirthNpcBaselineClientHeader.CanRetireEmpty&&RebirthNpcRecoveryProjectionScope.CanRetireEmptyClientGeneration;
}
