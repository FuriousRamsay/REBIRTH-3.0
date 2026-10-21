using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private sealed class HostClose:IDisposable
    {
        internal HostOwnerSession Session;internal Guid Operation;internal string Mode,Basis,HeaderHash,NativeHash,Path;
        internal byte[] Header,Accepted,Observed,Retired,Body;internal int Offset;internal ulong Clock;
        internal OwnerPds.SavedOwnerFile Owner;internal RebirthProductiveEarnedUiRemote.TypedStationPublication NativeBefore;
        internal NativeStationPublication NativeBeforeNative;
        internal bool SavedNativeCurrent()=>NativeBefore!=null?NativeBefore.Current():NativeBeforeNative?.TrySeal()==true;
        internal HostTerminalPublication Terminal;internal bool AcceptedNative,Finished;internal IDisposable Memory;
        internal string OwnerPath,OwnerDigest;
        internal Guid Group,Birth;internal World World;internal TileEntityWorkstation Station;internal EntityPlayer Actor;internal ClientInfo Peer;
        internal INetConnection Connection;internal string OwnerKey,Creation;internal int Deaths;
        internal bool Identity()
        {
            return Memory!=null&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&
                ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&
                ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(Actor.entityId),Peer)&&Peer.loginDone&&!Peer.disconnecting&&
                ReferenceEquals(Peer.netConnection?[0],Connection)&&!Connection.IsDisconnected()&&
                !Actor.IsDead()&&Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
                RebirthStablePlayerIdentity.TryFromClientInfo(Peer,out var identity)&&identity.StorageKey==OwnerKey&&
                RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var birth)&&birth==Birth.ToString("N")&&
                (Finished?Retired!=null&&RebirthProductiveEarnedArtifact.FileMatches(Path+".retired",Retired):Session?.Hydration?.Sources!=null&&Session.Hydration.Sources.All(source=>source.SourceCurrentWithoutViewer()&&source.TrySealStation()))&&
                RebirthProductiveEarnedArtifact.FileMatches(Path+".header",Header);
        }
        internal bool NativeCurrent()
        {
            if(!Identity())return false;
            using(var callback=Session.Scope.Source.Memory.TryEnterCallbacks())return callback!=null&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(Session.Scope.Station,Body,Offset)&&(!AcceptedNative||Session.Scope.Station.lastTickTime==Clock);
        }
        public void Dispose(){Body=Header=Accepted=Observed=Retired=null;Memory?.Dispose();Memory=null;}
    }
    private sealed class CloseRead
    {internal HostClose Source;internal NetPackageTileEntity Packet;internal byte[] Native;internal int Reads;internal bool Complete;internal IDisposable Callback;}
    private static readonly Dictionary<Guid,HostClose> hostCloses=new Dictionary<Guid,HostClose>();
    [ThreadStatic]private static CloseRead readingClose;
    // Parent HostProtocol routes Action5 here BEFORE its ordinary live-lock
    // gate. Only an actual accepted close may continue after native Unlock.
    internal static void ReceiveRemoteCloseRequest(NetPackageRebirthUiDeferredOwnerV1 packet,World world)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||packet?.Action!=5||packet.Sender==null||
                packet.Total<1||packet.Total>4096||packet.Index!=0||packet.Part?.Length!=packet.Total||HydrationHash(packet.Part)!=packet.Digest||!packet.ValidEntityIdForSender(packet.Player))return;
            XElement frame;
            using(var xml=XmlReader.Create(new StringReader(new UTF8Encoding(false,true).GetString(packet.Part)),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096,MaxCharactersFromEntities=0}))frame=XElement.Load(xml);
            if(frame.Name!="recipientActualNativeClose"||frame.Attributes().Count()!=8||frame.HasElements||(int?)frame.Attribute("version")!=1||ReadGuid(frame,"group")!=packet.Group||
                !Guid.TryParseExact((string)frame.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||(long)frame.Attribute("window")<1)return;
            string phase=(string)frame.Attribute("phase"),mode=(string)frame.Attribute("mode"),basis=(string)frame.Attribute("basis"),nativeHash=(string)frame.Attribute("native");
            if(phase!="prepare"&&phase!="observed"&&phase!="acknowledged"||mode!="unused"&&mode!="consumed"||basis?.Length!=64||nativeHash?.Length!=64)return;
            if(hostCloses.TryGetValue(packet.Group,out var retired)&&retired.Finished)
            {
                if(!retired.Identity()||!ReferenceEquals(retired.Peer,packet.Sender)||retired.Actor.entityId!=packet.Player||retired.Operation!=operation||retired.Observed==null||
                    !RebirthProductiveEarnedArtifact.FileMatches(retired.Path+".observed",retired.Observed)||!RebirthProductiveEarnedArtifact.FileMatches(retired.Path+".native",retired.Accepted))return;
                var observed=new XElement(frame);observed.SetAttributeValue("phase","observed");if(!OwnerFrameBytes(observed).SequenceEqual(retired.Observed))return;
                if(phase=="acknowledged"){if(retired.Session?.CachedClosed==retired)return;hostCloses.Remove(packet.Group);retired.Dispose();return;}
                if(phase=="observed")retired.Peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerReplyV1>().Setup(packet.Group,6,HydrationHash(retired.Observed)));
                return;
            }
            if(!hostOwners.TryGetValue(packet.Group,out var session)||!ReferenceEquals(session.Hydration.Peer,packet.Sender)||
                !ReferenceEquals(session.Scope.World,world)||session.Scope.Actor.entityId!=packet.Player)return;
            if(!hostCloses.TryGetValue(packet.Group,out var close))
            {
                if(phase!="prepare"||hostCloses.Count>=4||!session.Scope.Current()||!session.Ready||
                    mode=="unused"&&(session.AfterDigest!=null||session.Final!=null||session.RemovalArmed||basis!=session.BeforeDigest)||
                    mode=="consumed"&&(basis!=session.AfterDigest||session.Final?.Current()!=true))return;
                var body=mode=="unused"?session.Hydration.NativeBefore:session.Final.Body;
                int offset=mode=="unused"?session.Hydration.BeforeOffset:session.Final.Offset;
                if(body==null||CloseNativeHash(body,offset)!=nativeHash||!OwnerPds.TryReadBefore(session.Scope,out var ownerBefore)||!ownerBefore.Current())return;
                var owner=ownerBefore;
                if(mode=="consumed"&&(!OwnerPds.TryReadAfter(session.Scope,out owner)||!owner.Current()))return;
                RebirthProductiveEarnedUiRemote.TypedStationPublication nativeBefore=null;
                NativeStationPublication nativeBeforeNative=null;
                if(mode=="unused")
                {
                    foreach(var source in session.Hydration.Sources)
                    {
                        if(source.PublishedBody==null||!source.TrySealStation()||
                            !RebirthProductiveEarnedNativeBody.SameExceptElapsed(body,offset,source.PublishedBody,source.PublishedOffset))continue;
                        nativeBefore=source.StationPublication;nativeBeforeNative=source.NativePublication;break;
                    }
                    if(nativeBefore!=null?!nativeBefore.Current():nativeBeforeNative?.TrySeal()!=true)return;
                }
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*body.Length+512*1024),out var memory))return;
                close=new HostClose{Session=session,Operation=operation,Mode=mode,Basis=basis,NativeHash=nativeHash,Header=(byte[])packet.Part.Clone(),HeaderHash=packet.Digest,
                    Body=body,Offset=offset,Owner=owner,OwnerPath=owner.Path,OwnerDigest=owner.NativeDigest,NativeBefore=nativeBefore,NativeBeforeNative=nativeBeforeNative,Terminal=session.Final,Memory=memory,
                    Path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"uiDeferred",packet.Group.ToString("N")+".close"),
                    Group=packet.Group,Birth=session.Scope.CustodyBirth,World=world,Station=session.Scope.Station,Actor=session.Scope.Actor,Peer=session.Scope.Peer,Connection=session.Hydration.Connection,
                    OwnerKey=session.Scope.Owner,Creation=session.Scope.Creation,Deaths=session.Scope.Deaths};
                if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(close.Path+".header",close.Header)||!close.NativeCurrent()){close.Dispose();return;}
                hostCloses.Add(packet.Group,close);return;
            }
            if(!ReferenceEquals(close.Session,session)||close.Operation!=operation||close.Mode!=mode||close.Basis!=basis||close.NativeHash!=nativeHash||!close.Identity())return;
            var prepared=new XElement(frame);prepared.SetAttributeValue("phase","prepare");
            if(!OwnerFrameBytes(prepared).SequenceEqual(close.Header))return;
            if(phase!="observed")return;
            if(!close.AcceptedNative||!close.NativeCurrent()||!RebirthProductiveEarnedArtifact.FileMatches(close.Path+".native",close.Accepted))return;
            if(close.Observed!=null&&!close.Observed.SequenceEqual(packet.Part))return;
            if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(close.Path+".observed",packet.Part))return;
            close.Observed=(byte[])packet.Part.Clone();
            var receipt=Encoding.ASCII.GetBytes("recipient-native-close:"+packet.Digest+":"+HydrationHash(close.Accepted));
            if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(close.Path+".retired",receipt))return;
            close.Retired=receipt;
            // ACK is tied to the immutable actual-close report and the private
            // accepted native event, never to a bare close command.
            if(FinishRemoteRecipientClose(close))close.Peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerReplyV1>().Setup(packet.Group,6,packet.Digest));
        }
        catch{/* Preserve unknown original/Ready custody. No native resend. */}
    }
    internal static bool EnterRemoteCloseNativeUpload(NetPackageTileEntity packet,World world,bool run,out object state)
    {
        state=null;if(!run||world?.IsRemote()!=false)return true;
        var matches=hostCloses.Values.Where(close=>!close.Finished&&ReferenceEquals(close.World,world)&&close.Station.ToWorldPos().Equals(packet.teWorldPos)).ToArray();
        if(matches.Length==0)return true;if(matches.Length!=1||readingClose!=null)return false;var source=matches[0];
        if(source.AcceptedNative||!source.Identity()||!source.Session.Scope.Current()||!ReferenceEquals(packet.Sender,source.Session.Scope.Peer)||!packet.ValidEntityIdForSender(source.Session.Scope.Actor.entityId)||
            !source.Owner.Current()||source.Mode=="unused"&&!source.SavedNativeCurrent()||source.Mode=="consumed"&&source.Terminal?.Current()!=true||
            packet.ms==null||packet.ms.Length!=source.Body.Length||packet.teBlockId!=world.GetBlock(packet.teWorldPos).type)return false;
        var callback=source.Session.Scope.Source.Memory.TryEnterCallbacks();if(callback==null)return false;
        try
        {
            if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.Body,source.Offset))return false;var bytes=new byte[(int)packet.ms.Length];
            lock(packet.ms){long prior=packet.ms.Position;try{packet.ms.Position=0;int n=0;while(n<bytes.Length){int count=packet.ms.Read(bytes,n,bytes.Length-n);if(count<1)return false;n+=count;}}finally{packet.ms.Position=prior;}}
            if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(bytes,source.Offset,source.Body,source.Offset)||BitConverter.ToUInt64(bytes,source.Offset)>GameTimer.Instance.ticks)return false;
            readingClose=new CloseRead{Source=source,Packet=packet,Native=bytes,Callback=callback};callback=null;state=readingClose;return true;
        }
        finally{callback?.Dispose();}
    }
    internal static void EnterRemoteCloseNativeRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {var value=readingClose;if(run&&value!=null&&mode==StreamModeRead.FromClient&&ReferenceEquals(station,value.Source.Session.Scope.Station)&&ReferenceEquals(reader.BaseStream,value.Packet.ms))value.Reads++;}
    internal static void ObserveRemoteCloseNativeRead(PooledBinaryReader reader,bool run)
    {var value=readingClose;if(run&&value!=null&&value.Reads==1&&ReferenceEquals(reader.BaseStream,value.Packet.ms)&&reader.BaseStream.Position==value.Packet.ms.Length)value.Complete=true;}
    internal static void ObserveRemoteCloseNativeUpload(NetPackageTileEntity packet,World world,bool run,object state)
    {
        if(state is not CloseRead value||!ReferenceEquals(readingClose,value)||!run||value.Reads!=1||!value.Complete||!ReferenceEquals(value.Packet,packet)||!value.Source.Identity())return;
        var close=value.Source;var scope=close.Session.Scope;
        if(!scope.Current()||!close.Owner.Current()||close.Mode=="unused"&&!close.SavedNativeCurrent()||close.Mode=="consumed"&&close.Terminal?.Current()!=true||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(scope.Station,value.Native,close.Offset)||
            scope.Station.lastTickTime!=GameTimer.Instance.ticks-BitConverter.ToUInt64(value.Native,close.Offset))return;
        var receipt=new XElement("recipientActualNativeCloseAccepted",new XAttribute("group",close.Session.Hydration.Group.ToString("N")),new XAttribute("operation",close.Operation.ToString("N")),
            new XAttribute("mode",close.Mode),new XAttribute("basis",close.Basis),new XAttribute("header",close.HeaderHash),new XAttribute("native",close.NativeHash),
            new XAttribute("ownerFile",close.OwnerPath),new XAttribute("ownerDigest",close.OwnerDigest),new XAttribute("elapsed",BitConverter.ToUInt64(value.Native,close.Offset)),
            new XAttribute("savedNativeSource",close.Mode=="unused"?(close.NativeBefore?.Operation??close.NativeBeforeNative.Source.Operation).ToString("N"):close.Terminal.Path));
        byte[] image=OwnerFrameBytes(receipt);
        if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(close.Path+".native",image))return;
        // Unused receipt rows were positively unchanged in this accepted read;
        // renew their exact actual reference for the next genuine hydration.
        var refresh=new Dictionary<HostContribution,CraftCompleteData>();
        if(close.Mode=="unused")foreach(var source in close.Session.Hydration.Sources)
        {
            var rows=scope.Station.CraftCompleteList.Where(row=>Row(row).SequenceEqual(source.Aggregate)).ToArray();if(rows.Length!=1)return;
            refresh.Add(source,rows[0]);
        }
        foreach(var item in refresh)item.Key.AcceptedReceipt=item.Value;
        close.Accepted=image;close.Clock=scope.Station.lastTickTime;close.AcceptedNative=true;
        value.Callback?.Dispose();value.Callback=null;
        if(close.Mode=="consumed"&&close.Terminal!=null)
        {
            acceptingCachedOwnerClose=close.Terminal;
            try{RebirthWorkstationCompletionPipeline.ObserveCachedOwnerClose(close.Terminal,value.Native,close.Offset,close.Path+".native",image);}
            finally{acceptingCachedOwnerClose=null;}
        }
    }
    internal static void EndRemoteCloseNativeUpload(object state)
    {if(state is CloseRead value){value.Callback?.Dispose();value.Callback=null;value.Native=null;if(ReferenceEquals(readingClose,value))readingClose=null;}}
    private static bool FinishRemoteRecipientClose(HostClose close)
    {
        var session=close.Session;var group=session.Hydration.Group;
        if(!close.AcceptedNative||close.Observed==null||!close.NativeCurrent())return false;
        if(RetainClosedCachedPhysicalGroup(close)){close.Finished=true;return close.Identity();}
        hostOwners.Remove(group);
        if(removals.TryGetValue(session.Scope.Operation,out var removal)&&ReferenceEquals(removal.Scope,session.Scope)){removals.Remove(session.Scope.Operation);removal.Dispose();}
        session.Scope.Dispose();
        if(dispatches.TryGetValue(group,out var hydration)&&ReferenceEquals(hydration,session.Hydration)){dispatches.Remove(group);hydration.Close();}
        session.BeforeInput=session.AfterInput=null;
        session.Final=null;close.Finished=true;close.Session=null;close.Owner=null;close.NativeBefore=null;close.NativeBeforeNative=null;close.Terminal=null;close.Body=null;
        RebirthProductiveEarnedObservationBudget.TryResizeDetached(close.Memory,checked(8L*(close.Header.Length+close.Accepted.Length+close.Observed.Length+close.Retired.Length)+65536));
        // Retain only the bounded immutable close ACK metadata until world
        // teardown; duplicate reports cannot reconstruct a released scope.
        return close.Identity();
    }
    private static void SweepRemoteHostCloses(World world)
    {foreach(var pair in hostCloses.Where(value=>!ReferenceEquals(value.Value.World,world)||value.Value.Finished&&value.Value.Session?.CachedClosed!=value.Value&&(value.Value.Peer.disconnecting||!value.Value.Peer.loginDone)).ToArray()){hostCloses.Remove(pair.Key);pair.Value.Dispose();}if(readingClose!=null&&!ReferenceEquals(readingClose.Source.World,world))readingClose=null;}
}
