using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed class RecyclerClient:IDisposable
    {
        internal Original Source;internal OwnerPublication Owner;internal INetConnection Connection;internal XUiC_WorkstationWindowGroup Window;internal long Session;
        internal Guid Request,Nonce;internal int Slot,Units,Stage,HostPhase,HostCompleted;internal bool HostSaved,Unknown,Applied,NativeSent,ReceiptSaved,Acknowledged;
        internal bool PrincipalWritingStarted,PrincipalEntered;
        internal SavedOwnerCheckpoint SentOwner;internal bool OwnerPacketSent;internal string TerminalPath;internal byte[] Terminal;internal float NextQuery;internal XElement LastReply;
        internal NetPackagePlayerData AutomaticPacket;internal RecyclerAutomaticOwner AutomaticObservation;
        internal IDisposable AutomaticMemory,TerminalMemory;
        internal readonly List<WeakReference<PlayerDataFile>> AutomaticCaptures=new List<WeakReference<PlayerDataFile>>();
        internal long AutomaticSequence,AutomaticCaptured,AutomaticSent,AutomaticAcknowledged;
        internal bool AcknowledgementSent;
        internal bool UnusedCloseObserved;
        internal RecyclerReadReentry Reentry;
        internal RecyclerJoining ColdJoined;internal int ColdPhase,ColdCompleted;internal Guid Generation;
        internal TileEntityWorkstation UnqualifiedStation;
        internal bool Current()=>Source?.Current()==true&&Source.World.IsRemote()&&Connection!=null&&!Connection.IsDisconnected()&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],Connection);
        internal bool MetadataCurrent()=>Source?.OwnerLineageCurrent()==true&&Source.NativeLineageCurrent()&&Source.World.IsRemote()&&Connection!=null&&!Connection.IsDisconnected()&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],Connection);
        internal bool SavedTerminalMetadataCurrent()=>ReceiptSaved&&TerminalMemory!=null&&Terminal!=null&&Source!=null&&AutomaticPacket==null&&AutomaticSent<=AutomaticAcknowledged&&
            ReferenceEquals(GameManager.Instance?.World,Source.World)&&Source.World.IsRemote()&&Source.WorldKey==RebirthWorkstationCompletionOutbox.CurrentGuid(Source.World)&&
            Source.Directory==Path.GetFullPath(GameIO.GetSaveGameDir())&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&
            identity.StorageKey==Source.Owner&&identity.CanonicalId==Source.CanonicalOwner&&Connection!=null&&!Connection.IsDisconnected()&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],Connection)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(TerminalPath,Terminal);
        internal bool Live()=>Source?.ClosedOwner!=null?Current()&&Source.ClosedOwner.IsClient(this):Reentry==null&&Current()&&Window?.windowGroup?.isShowing==true&&ReferenceEquals(Window.WorkstationData?.TileEntity,Source.Station)&&
            ReferenceEquals(Window.xui?.playerUI?.entityPlayer,Source.Actor)&&LockManager.Instance?.IsLockedByLocalPlayer(Source.Station,0)==true&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(Window,out long session)&&session==Session;
        public void Dispose()
        {
            // Drop game-owned graphs before releasing their source reservation.
            Window=null;UnqualifiedStation=null;Connection=null;LastReply=null;SentOwner=null;Terminal=null;
            ReleaseAutomaticRecyclerOwner(this);
            Reentry?.Dispose();Reentry=null;
            // The actual native join may own several independent originals.
            // Its root/sweep owns disposal until each operation is qualified.
            ColdJoined=null;
            foreach(var captured in AutomaticCaptures)if(captured.TryGetTarget(out var data))recyclerAutomaticOwners.Remove(data);
            AutomaticCaptures.Clear();
            AutomaticMemory?.Dispose();AutomaticMemory=null;
            TerminalMemory?.Dispose();TerminalMemory=null;
            Owner?.Dispose();Owner=null;Source?.Dispose();Source=null;
        }
    }
    internal sealed class RemotePaidStation
    {
        private readonly RecyclerClient client;internal Original Source=>client.Source;
        private RemotePaidStation(RecyclerClient client){this.client=client;}
        internal bool Current()=>client.Current()&&!client.Unknown&&(Source.ClosedOwner!=null||client.Applied)&&client.Stage>=3&&Source.MatchesStationStage(true);
        internal static RemotePaidStation FromActualReply(RecyclerClient client)=>ReferenceEquals(receivingRecyclerReply,client)&&client.Stage>=3?new RemotePaidStation(client):null;
    }
    private static readonly List<RecyclerClient> recyclerClients=new List<RecyclerClient>();
    [ThreadStatic]private static RecyclerClient startingRecyclerOwner,projectingRecyclerClient,receivingRecyclerReply;
    [ThreadStatic]private static NetPackageTileEntity sendingRecyclerStation;
    private static bool IsRemoteCheckpointAccepted(SavedOwnerCheckpoint token)=>recyclerClients.Any(value=>ReferenceEquals(value.Source,token.Original)&&value.Current()&&
        value.HostSaved&&value.HostPhase==token.Phase&&value.HostCompleted==token.Completed&&ReferenceEquals(value.SentOwner,token));
    private static bool CanAdvanceRemoteRecyclerOwner(Original source)=>!source.World.IsRemote()||recyclerClients.Any(value=>ReferenceEquals(value.Source,source)&&value.Live()&&!value.Unknown&&value.Stage>=3);
    private static bool RecyclerClientOwnerHeld(EntityPlayerLocal actor)=>recyclerClients.Any(value=>!value.Acknowledged&&!ReferenceEquals(value,startingRecyclerOwner)&&
        ReferenceEquals(value.Source?.Actor,actor)&&value.Source.OwnerLineageCurrent());
    private static bool RecyclerClientStationHeld(TileEntityWorkstation station)=>recyclerClients.Any(value=>!value.Acknowledged&&ReferenceEquals(value.Source?.World,GameManager.Instance?.World)&&
        (ReferenceEquals(value.Source.Station,station)||ReferenceEquals(value.UnqualifiedStation,station))&&ReferenceEquals(value.Source.World.GetTileEntity(station.ToWorldPos()),station))||
        joinedRecyclerOwners.Any(value=>ReferenceEquals(value.World,GameManager.Instance?.World)&&value.Current()&&value.UnqualifiedStations.Values.Any(candidate=>ReferenceEquals(candidate,station))&&
            ReferenceEquals(value.World.GetTileEntity(station.ToWorldPos()),station));
    internal static bool BeginRemoteAction(EntityPlayerLocal actor,TileEntityWorkstation station,Guid operation,Guid request,string purpose,int slot,int units,
        ItemStack[] input,RecipeQueueItem[] queue,IList<ItemStack> returns,ItemStack[] output,ItemStack[] tools,Func<bool> current,out string reason,bool cycle=false,float seconds=0)
    {
        reason="xuiRebirthCamperStationSaving";
        if(!authorityInstalled||!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=true||!actor.IsSpawned()||actor.IsDead()||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetPrimaryPlayer(),actor)||
            !ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||current?.Invoke()!=true||operation==Guid.Empty||request==Guid.Empty)return false;
        if(!TrySelectAuthorityAttempt(actor,station,operation,purpose,out operation))return false;
        var pending=recyclerClients.SingleOrDefault(value=>value.Source?.Operation==operation);
        if(pending!=null)return ReferenceEquals(pending.Source.Actor,actor)&&ReferenceEquals(pending.Source.Station,station)&&pending.Source.Purpose==purpose&&pending.Current();
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0];var window=XUiC_RebirthStationWorkspace.ActiveInstance;
        if(connection==null||connection.IsDisconnected()||recyclerClients.Count>=4||HoldsStation(station)||RebirthCookingOutputDelivery.IsHeld(actor)||window?.windowGroup?.isShowing!=true||
            !ReferenceEquals(window.WorkstationData?.TileEntity,station)||!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out long session))return false;
        var source=Original.Capture(actor,station,operation,purpose,input,queue,returns,output,tools,current,cycle,seconds);if(source==null)return false;
        bool retained=false;IDisposable automaticMemory=null;
        try
        {
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out automaticMemory))return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",source.WorldKey),
                new XElement("before",XElement.Parse(Encoding.UTF8.GetString(source.Image))),new XElement("recyclerRemoteClientOriginal",new XAttribute("request",request.ToString("N")),new XAttribute("slot",slot),new XAttribute("units",units),new XAttribute("session",session)));
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".remote-client.xml");
            if(ArtifactNodeExists(path)||ArtifactNodeExists(path+".tmp")||ArtifactNodeExists(path+".bak")||!RebirthWorkstationCompletionPublication.Write(path,record)||
                !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return false;
            var client=new RecyclerClient{Source=source,Connection=connection,Window=window,Session=session,Request=request,Slot=slot,Units=units,AutomaticMemory=automaticMemory};
            recyclerClients.Add(client);retained=true;SendPrepare(client);reason=null;return true;
        }
        finally{if(!retained){automaticMemory?.Dispose();source.Dispose();}}
    }
    private static void SendPlacedText(string text)
    {
        byte[] bytes=Encoding.UTF8.GetBytes(text);if(bytes.Length<1||bytes.Length>12*1024*1024)throw new InvalidDataException();SendPlacedBytes(bytes);
    }
    private static void SendPlacedBytes(byte[] bytes)
    {
        if(bytes.Length<1||bytes.Length>25*1024*1024)throw new InvalidDataException();
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var world=GameManager.Instance?.World;
        var connection=manager?.connectionToServer?[0];
        bool Current()=>ThreadManager.IsMainThread()&&world?.IsRemote()==true&&ReferenceEquals(GameManager.Instance?.World,world)&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)&&ReferenceEquals(manager.connectionToServer?[0],connection)&&connection!=null&&!connection.IsDisconnected();
        if(!Current())throw new InvalidOperationException("Recycler connection is not current.");
        int count=(bytes.Length+8999)/9000;Guid group=Guid.NewGuid();
        var packets=new NetPackageRebirthRecyclerPlacedRequestV1[count];
        try
        {
            // Allocation denial cannot enqueue a partial purpose group.
            for(int index=0;index<count;index++)
            {
                if(!Current())throw new InvalidOperationException("Recycler connection changed before packet allocation.");
                var packet=NetPackageManager.GetPackage<NetPackageRebirthRecyclerPlacedRequestV1>();packets[index]=packet;
                packet.SetupSlice(group,bytes,index);
            }
            for(int index=0;index<count;index++)
            {
                if(!Current())throw new InvalidOperationException("Recycler connection changed before packet send.");
                var packet=packets[index];
                manager.SendToServer(packet);
                packets[index]=null;
            }
        }
        finally
        {
            foreach(var packet in packets)
                if(packet!=null&&packet.inSendQueuesCount==0)NetPackageManager.FreePackage(packet);
        }
    }
    private static void SendPrepare(RecyclerClient client)
    {
        if(!client.Live())return;
        SendPlacedText(new XElement("recyclerRemotePrepare",new XAttribute("version",1),new XAttribute("player",client.Source.Actor.entityId),new XAttribute("request",client.Request.ToString("N")),
            new XAttribute("slot",client.Slot),new XAttribute("units",client.Units),new XElement("original",XElement.Parse(Encoding.UTF8.GetString(client.Source.Image)))).ToString(SaveOptions.DisableFormatting));
    }
    private static XElement ClientCommand(RecyclerClient client,string command)=>new XElement("recyclerRemoteCommand",new XAttribute("operation",client.Source.Operation.ToString("N")),
        new XAttribute("nonce",client.Nonce.ToString("N")),new XAttribute("original",RebirthStationPublicationRecord.Digest(client.Source.Image)),new XAttribute("command",command));
    internal static void ReceivePlacedReply(NetPackageRebirthRecyclerPlacedReplyV1 packet,World world)
    {
        if(!authorityInstalled||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||!ThreadManager.IsMainThread()||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var received)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],received))return;
        try
        {
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(packet.Text,8192);
            if(ObserveViewedTerminal(body,received))return;
            if(ObserveRemoteRecyclerUnusedReply(body,received))return;
            if(body.Name!="recyclerRemoteStatus"||(int?)body.Attribute("version")!=1||!Guid.TryParseExact((string)body.Attribute("operation"),"N",out var operation)||
                !Guid.TryParseExact((string)body.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty)return;
            if(ObserveJoinedRecyclerTerminalReply(body,received))return;
            var client=recyclerClients.SingleOrDefault(value=>value.Source.Operation==operation&&ReferenceEquals(value.Connection,received));
            if(client==null||!client.MetadataCurrent()&&(!client.SavedTerminalMetadataCurrent()||((int?)body.Attribute("stage")??-1)<5)||
                (string)body.Attribute("original")!=RebirthStationPublicationRecord.Digest(client.Source.Image)||
                (string)body.Attribute("world")!=client.Source.WorldKey||(string)body.Attribute("owner")!=client.Source.Owner||
                (string)body.Attribute("creation")!=client.Source.Creation||(int?)body.Attribute("deaths")!=client.Source.Deaths||client.Nonce!=Guid.Empty&&client.Nonce!=nonce)return;
            int stage=(int)body.Attribute("stage"),phase=(int)body.Attribute("phase"),count=(int)body.Attribute("completed");bool saved=(bool)body.Attribute("ownerSaved");
            if(stage<client.Stage||stage<0||stage>6||phase<0||phase>3||count<0||count>client.Source.Slots.Length)return;
            client.Nonce=nonce;client.Stage=stage;client.HostPhase=phase;client.HostCompleted=count;client.HostSaved=saved;
            long automatic=(long?)body.Attribute("automaticSequence")??0;
            if(automatic>client.AutomaticAcknowledged&&automatic<=client.AutomaticSent)client.AutomaticAcknowledged=automatic;
            if(client.AutomaticPacket!=null&&client.AutomaticObservation?.Sequence<=client.AutomaticAcknowledged)ReleaseAutomaticRecyclerOwner(client);
            client.LastReply=new XElement(body);
            receivingRecyclerReply=client;
            try
            {
                client.Source.ClosedOwner?.ObserveActualStage(client);
                if(stage>=3&&client.Owner!=null&&!client.Owner.StationPaid)client.Owner.ArmRemotePaid(RemotePaidStation.FromActualReply(client));
                if(stage==6&&client.ReceiptSaved)
                {
                    var final=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",client.Source.WorldKey),
                        new XElement("before",XElement.Parse(Encoding.UTF8.GetString(client.Source.Image))),new XElement("recyclerRemoteRetired",new XElement(body)));
                    string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".remote-retired.xml");
                    if(RebirthWorkstationCompletionPublication.Write(path,final)&&XNode.DeepEquals(final,RebirthWorkstationCompletionPublication.ReadPrimary(path)))
                    {client.Acknowledged=true;recyclerClients.Remove(client);var actor=client.Source.Actor;var station=client.Source.Station;client.Dispose();RebirthRecyclerRuntime.Refresh(actor,station);}
                }
            }
            finally{receivingRecyclerReply=null;}
        }
        catch{/* No parsed reply alone restores an original or repeats a native call. */}
    }
}
