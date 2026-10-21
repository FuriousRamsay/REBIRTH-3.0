using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed class HydrationDispatch
    {
        internal Guid Group;internal World World;internal TileEntityWorkstation Station;
        internal ClientInfo Peer;internal INetConnection Connection;internal EntityPlayer Actor;
        internal string Owner,Creation,WorldKey;internal int Deaths;
        internal HostContribution[] Sources;internal RebirthProductiveEarnedUiQueueOwner.NativeHydrationPublication QueueOwners;
        internal RebirthWorkstationCompletionPipeline.NativeReceiptHydrationPublication NativeReceipts;internal byte[] NativeBefore;internal int BeforeOffset;
        internal RebirthWorkstationCompletionPipeline.CachedPrimaryHydrationPublication CachedPrimaries;
        internal RebirthRecyclerPlacedSettlement.RecyclerReentryPublication RecyclerReentry;
        internal XElement Header;internal object CloseEcho,CachedEcho,QueueResponse;internal RebirthProductiveEarnedRemoteHost.NativeEcho OrdinaryEcho;internal bool Enqueued,Closed;internal int Count,EnvelopeLength,MetadataCount,NativeCount;
        internal readonly HashSet<int> Queued=new HashSet<int>();
        private readonly object gate=new object();private IDisposable allocation;private int references=1;private long reservedBytes=32L*1024*1024;
        internal HydrationDispatch(IDisposable reservation){allocation=reservation;}
        internal bool Current()=>OrdinaryEcho!=null?!Closed&&OrdinaryEcho.Current():CachedEcho!=null?!Closed&&CachedPhysicalEchoCurrent(CachedEcho):CloseEcho!=null?!Closed&&CloseEchoCurrent(CloseEcho):!Closed&&ThreadManager.IsMainThread()&&Sources!=null&&Sources.All(value=>value.SourceCurrentWithoutViewer()&&value.TrySealStation())&&(QueueOwners==null||(Enqueued?QueueOwners.SourceCurrent():QueueOwners.Current()))&&
            (NativeReceipts==null||(Enqueued?NativeReceipts.SourceCurrent():NativeReceipts.Current()))&&
            (CachedPrimaries==null||(Enqueued?CachedPrimaries.SourceCurrent():CachedPrimaries.Current()))&&
            (RecyclerReentry==null||(Enqueued?RecyclerReentry.SourceCurrent():RecyclerReentry.Current()))&&
            (QueueResponse==null||RebirthProductiveEarnedUiRemote.QueueResponseCurrent(QueueResponse))&&
            ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&
            ReferenceEquals(Peer.netConnection?[0],Connection)&&!Connection.IsDisconnected()&&Peer.loginDone&&!Peer.disconnecting&&
            Actor.Died==Deaths&&RebirthProductiveEarnedArtifact.RemoteAuthority(Actor,Peer,Station,Creation)&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Peer,out var identity)&&identity.StorageKey==Owner;
        internal IDisposable Retain(){lock(gate){if(Closed||allocation==null)return null;references++;return new Lease(this);}}
        internal bool NarrowQueued()
        {
            lock(gate){if(Closed||allocation==null||Header==null||NativeBefore==null)return false;
                long bytes=checked(65536L+16L*Encoding.UTF8.GetByteCount(Header.ToString(SaveOptions.DisableFormatting))+2L*NativeBefore.Length+EnvelopeLength);
                if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,bytes))return false;reservedBytes=bytes;return true;}
        }
        internal bool ReserveMetadata(long bytes)
        {lock(gate){if(Closed||bytes<1||bytes>ExpandedHydrationMetadataMaximum)return false;
            long needed=checked(32L*1024*1024+16*bytes+4L*RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,needed))return false;reservedBytes=needed;return true;}}
        internal bool GrowMetadataTransport(long bytes)
        {lock(gate){if(allocation==null||bytes<1||bytes>2L*PackedHydrationMetadataMaximum+65536)return false;
            long needed=checked(reservedBytes+bytes);if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,needed))return false;reservedBytes=needed;return true;}}
        internal void DropMetadataTransport(long bytes)
        {lock(gate){if(allocation==null||bytes<=0||bytes>=reservedBytes)return;long needed=reservedBytes-bytes;
            if(RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,needed))reservedBytes=needed;}}
        internal void Close(){if(Closed)return;ForgetUnenqueuedHydrationMetadata(this);Closed=true;NativeBefore=null;Header=null;Sources=null;CloseEcho=CachedEcho=null;OrdinaryEcho?.Dispose();OrdinaryEcho=null;QueueOwners?.Dispose();QueueOwners=null;NativeReceipts?.Dispose();NativeReceipts=null;CachedPrimaries?.Dispose();CachedPrimaries=null;RecyclerReentry?.Dispose();RecyclerReentry=null;QueueResponse=null;Release();}
        private void Release(){lock(gate){if(--references!=0)return;allocation?.Dispose();allocation=null;}}
        private sealed class Lease:IDisposable
        {private HydrationDispatch source;internal Lease(HydrationDispatch value){source=value;}public void Dispose(){System.Threading.Interlocked.Exchange(ref source,null)?.Release();}}
    }
    private static readonly Dictionary<Guid,HydrationDispatch> dispatches=new Dictionary<Guid,HydrationDispatch>();
    private static string HydrationHash(byte[] image)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(image)).Replace("-","").ToLowerInvariant();}

    // false leaves unrelated native delivery alone; true/null holds a positively
    // known original which cannot currently acquire its exact purpose wrapper.
    internal static bool TryWrapNativeHydration(ClientInfo peer,INetConnection connection,NetPackageTileEntity native,out NetPackage[] wrapped)
    {
        bool actualQueueResponse=RebirthProductiveEarnedUiRemote.TrySelectQueueResponse(native,peer,connection,out bool queueAllowed,out var queueContext);
        if(actualQueueResponse&&!queueAllowed){wrapped=null;return true;}
        if(TryWrapRemoteCloseEcho(peer,connection,native,out wrapped))return true;
        if(TryWrapOrdinaryEarnedEcho(peer,connection,native,out wrapped))return true;
        if(TryWrapCachedPhysicalEcho(peer,connection,native,out wrapped))return true;
        wrapped=null;HydrationDispatch pending=null;var packages=new List<NetPackage>();bool selected=false;
        try
        {
            if(!ThreadManager.IsMainThread()||peer==null||connection==null||native==null)return actualQueueResponse;
            World world;TileEntityWorkstation station;
            if(!(actualQueueResponse?RebirthProductiveEarnedUiRemote.TryGetQueueResponseSource(queueContext,out world,out station):
                TryGetKnownNativeCandidate(native,out world,out station))||world?.IsRemote()!=false)return actualQueueResponse;
            // The phase5 packet is a proved nochange observation. Its native
            // Process echo must not overwrite a creator's later local transfer.
            // The ordinary next open supplies the saved original grant.
            if(RebirthWorkstationCompletionPipeline.SuppressCachedBoundaryEcho(native,world,station))return true;
            if(HasActualAuthorityNativeAdd(world,station))return true;
            var possible=actualQueueResponse?Array.Empty<HostContribution>():host.Values.Where(value=>!value.Closed&&!value.RoutingSettled&&value.SupersededBy==Guid.Empty&&ReferenceEquals(value.World,world)&&ReferenceEquals(value.Station,station)&&
                value.RecipientEntityId==peer.entityId).ToArray();
            bool queueCandidate=RebirthProductiveEarnedUiQueueOwner.HasNativeHydrationCandidate(world,station)&&world.GetEntity(peer.entityId) is EntityPlayer queuedViewer&&
                RebirthProductiveEarnedArtifact.RemoteAuthority(queuedViewer,peer,station,RebirthCamperCharacterAccess.CurrentCreation(queuedViewer));
            bool receiptCandidate=RebirthWorkstationCompletionPipeline.HasNativeReceiptHydrationCandidate(world,station,peer,connection);
            bool cachedCandidate=RebirthWorkstationCompletionPipeline.HasCachedPrimaryHydrationCandidate(world,station);
            bool recyclerCandidate=RebirthRecyclerPlacedSettlement.HasRecyclerReentryCandidate(world,station,peer,connection);
            if(possible.Length==0&&!queueCandidate&&!receiptCandidate&&!cachedCandidate&&!recyclerCandidate&&!actualQueueResponse)return false;selected=true;
            if(station.CraftCompleteList==null||station.CraftCompleteList.Count>64)return true;
            var candidates=possible.Where(value=>value.AcceptedReceipt!=null&&station.CraftCompleteList?.Any(row=>ReferenceEquals(row,value.AcceptedReceipt)&&Row(row).SequenceEqual(value.Aggregate))==true).ToArray();
            if(possible.Length>0&&candidates.Length!=possible.Length||candidates.Length==0&&!queueCandidate&&!receiptCandidate&&!cachedCandidate&&!recyclerCandidate&&!actualQueueResponse)return true;
            if(!actualQueueResponse&&(!TryGetSelectedNativeSource(native,out var qualifiedWorld,out var qualifiedStation)||
                !ReferenceEquals(world,qualifiedWorld)||!ReferenceEquals(station,qualifiedStation)))return true;
            foreach(var expired in dispatches.Values.Where(value=>!value.Current()&&!RetainsClosedCachedPhysicalGroup(value)).ToArray()){dispatches.Remove(expired.Group);expired.Close();}
            // One original hydration owns this recipient/station until terminal.
            // Later ordinary broadcasts cannot replay native Process over it.
            if(!actualQueueResponse&&dispatches.Values.Any(value=>ReferenceEquals(value.Peer,peer)&&ReferenceEquals(value.Station,station)))return true;
            if(candidates.Length>4||!ReferenceEquals(peer.netConnection?[native.Channel],connection)||native.Channel!=0||connection.IsDisconnected()||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||world.GetEntity(peer.entityId) is not EntityPlayer actor)return true;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
            if(candidates.Any(value=>value.RecipientOwner!=identity.StorageKey||value.RecipientCreation!=creation||value.Recipient.Deaths!=actor.Died||!value.TrySealStation())||
                !RebirthProductiveEarnedArtifact.RemoteAuthority(actor,peer,station,creation)||dispatches.Count>=4)return true;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(32L*1024*1024,out var reservation))return true;
            pending=new HydrationDispatch(reservation){Group=Guid.NewGuid(),World=world,Station=station,Peer=peer,Connection=connection,Actor=actor,
                Owner=identity.StorageKey,Creation=creation,Deaths=actor.Died,WorldKey=RebirthProductiveEarnedArtifact.WorldIdentity(world),Sources=candidates,QueueResponse=queueContext};
            if(queueCandidate&&!RebirthProductiveEarnedUiQueueOwner.TryCaptureNativeHydration(world,station,peer,connection,out pending.QueueOwners,actualQueueResponse?RebirthProductiveEarnedUiRemote.QueueResponseWorkspace(queueContext):null))return true;
            if(receiptCandidate&&!RebirthWorkstationCompletionPipeline.TryCaptureNativeReceiptHydration(world,station,peer,connection,out pending.NativeReceipts))return true;
            if(recyclerCandidate&&!RebirthRecyclerPlacedSettlement.TryCaptureRecyclerReentry(world,station,peer,connection,out pending.RecyclerReentry))return true;
            if(actualQueueResponse)
            {if(!pending.Current()||!RebirthProductiveEarnedUiRemote.CopyQueueResponseBody(queueContext,out pending.NativeBefore,out pending.BeforeOffset))return true;}
            else using(var callbacks=candidates.Length>0?candidates[0].Memory.TryEnterCallbacks():RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {if(callbacks==null||!pending.Current()||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out pending.NativeBefore,out pending.BeforeOffset))return true;}
            var header=new XElement("deferredNativeHydration",new XAttribute("version",1),new XAttribute("group",pending.Group.ToString("N")),
                new XAttribute("world",pending.WorldKey),new XAttribute("owner",pending.Owner),new XAttribute("creation",pending.Creation),
                new XAttribute("entity",actor.entityId),new XAttribute("deaths",actor.Died),new XAttribute("x",native.teWorldPos.x),
                new XAttribute("y",native.teWorldPos.y),new XAttribute("z",native.teWorldPos.z),new XAttribute("block",native.teBlockId),new XAttribute("handle",native.handle));
            if(pending.QueueOwners!=null)header.Add(pending.QueueOwners.Image);
            if(pending.NativeReceipts!=null)header.Add(pending.NativeReceipts.Image);
            if(pending.RecyclerReentry!=null)header.Add(pending.RecyclerReentry.Image);
            if(actualQueueResponse)header.Add(RebirthProductiveEarnedUiRemote.QueueResponseHeader(queueContext));
            if(cachedCandidate)
            {
                if(!RebirthWorkstationCompletionPipeline.TryCaptureCachedPrimaryHydration(world,station,out var cached,actor,pending.Owner,peer,connection,pending.Group))return true;
                pending.CachedPrimaries=cached;
                if(!pending.ReserveMetadata(checked(MeasurePublicationElement(header)+cached.MeasureImage()+128)))return true;
                header.Add(cached.Image);
            }
            var nativeEvents=new List<byte[]>();
            foreach(var source in candidates)
            {
                var aggregate=station.CraftCompleteList.Single(value=>Row(value).SequenceEqual(source.Aggregate));
                var entry=new XElement("aggregate",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("nonce",source.Nonce.ToString("N")),
                    new XAttribute("birth",source.Birth.ToString("N")),new XAttribute("source",source.SourceHash),new XAttribute("native",HydrationHash(source.Aggregate)),
                    new XAttribute("recipe",aggregate.RecipeName),new XAttribute("membership",HydrationHash(Encoding.UTF8.GetBytes(source.ImmutableMembership.ToString(SaveOptions.DisableFormatting)))));
                entry.Add(new XElement("recipient",RebirthProductiveEarnedUiQueueOwner.Export(source.Recipient)));
                foreach(var member in source.ImmutableMembership.Element("members").Elements("member"))
                {
                    if(nativeEvents.Count>=8)return true;
                    if(!TryResolveContributionPurpose(source,(string)member.Attribute("source"),out var creator))return true;
                    int nativeEvent=nativeEvents.Count/2;nativeEvents.Add(ReadBytes(member,"beforeComplete"));nativeEvents.Add(ReadBytes(member,"afterComplete"));
                    entry.Add(new XElement("member",new XAttribute("operation",(string)member.Attribute("operation")),new XAttribute("kind",(string)member.Attribute("kind")),
                        new XAttribute("contribution",(int)member.Attribute("contribution")),new XAttribute("nativeEvent",nativeEvent),new XElement("creator",RebirthProductiveEarnedUiQueueOwner.Export(creator))));
                }
                header.Add(entry);
            }
            if(!PackHydrationMetadata(pending,header,out byte[] metadata,out byte[] sideMetadata))return true;
            byte[] nativeBytes;
            using(var stream=new HydrationStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {writer.SetBaseStream(stream);native.write(writer);writer.Flush();nativeBytes=stream.ToArray();}
            if(!pending.Current()||!actualQueueResponse&&(!TryGetSelectedNativeSource(native,out var afterWorld,out var afterStation)||
                !ReferenceEquals(world,afterWorld)||!ReferenceEquals(station,afterStation)))return true;
            if(!actualQueueResponse)using(var callbacks=candidates.Length>0?candidates[0].Memory.TryEnterCallbacks():RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {if(callbacks==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,pending.NativeBefore,pending.BeforeOffset))return true;}
            byte[] envelope;
            using(var stream=new HydrationStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {writer.Write((uint)0x31484455);writer.Write(metadata.Length);writer.Write(metadata);writer.Write(nativeEvents.Count/2);
                foreach(var image in nativeEvents){writer.Write(image.Length);writer.Write(image);}
                writer.Write(nativeBytes.Length);writer.Write(nativeBytes);writer.Flush();envelope=stream.ToArray();}
            string digest=HydrationHash(envelope);int count=(envelope.Length+NetPackageRebirthUiDeferredHydrationV1.Chunk-1)/NetPackageRebirthUiDeferredHydrationV1.Chunk;
            int metadataCount=sideMetadata==null?0:(sideMetadata.Length+NetPackageRebirthUiDeferredHydrationV1.Chunk-1)/NetPackageRebirthUiDeferredHydrationV1.Chunk;
            if(checked(envelope.Length+(sideMetadata?.Length??0))>NetPackageRebirthUiDeferredHydrationV1.Maximum)return true;
            pending.Count=checked(count+metadataCount);pending.NativeCount=count;pending.MetadataCount=metadataCount;
            pending.EnvelopeLength=checked(envelope.Length+(sideMetadata?.Length??0));pending.Header=header;
            if(!RetainHydrationMetadataSend(pending,sideMetadata,digest))return true;
            if(sideMetadata!=null)
            {
                string metadataDigest=HydrationHash(sideMetadata);
                for(int index=0;index<metadataCount;index++)
                {
                    int start=index*NetPackageRebirthUiDeferredHydrationV1.Chunk,length=Math.Min(NetPackageRebirthUiDeferredHydrationV1.Chunk,sideMetadata.Length-start);
                    byte[] part=new byte[length];Buffer.BlockCopy(sideMetadata,start,part,0,length);var lease=pending.Retain();if(lease==null)return true;
                    packages.Add(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredHydrationV1>().Setup(pending.Group,sideMetadata.Length,metadataCount,index,metadataDigest,part,pending,lease).AsMetadata());
                }
            }
            for(int index=0;index<count;index++)
            {
                int start=index*NetPackageRebirthUiDeferredHydrationV1.Chunk,length=Math.Min(NetPackageRebirthUiDeferredHydrationV1.Chunk,envelope.Length-start);
                byte[] part=new byte[length];Buffer.BlockCopy(envelope,start,part,0,length);var lease=pending.Retain();if(lease==null)return true;
                packages.Add(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredHydrationV1>().Setup(pending.Group,envelope.Length,count,index,digest,part,pending,lease));
            }
            dispatches.Add(pending.Group,pending);wrapped=packages.ToArray();pending=null;packages.Clear();return true;
        }
        catch{return selected;}
        finally{foreach(var packet in packages)NetPackageManager.FreePackage(packet);pending?.Close();}
    }
    internal static void NoteHydrationEnqueued(NetPackage packet,INetConnection connection)
    {
        if(packet is not NetPackageRebirthUiDeferredHydrationV1 fragment||fragment.Original is not HydrationDispatch original||
            !original.Current()||!ReferenceEquals(connection,original.Connection)||fragment.Group!=original.Group||
            fragment.Count!=(fragment.Component==1?original.MetadataCount:original.NativeCount==0?original.Count:original.NativeCount))return;
        original.Queued.Add(fragment.Component==1?fragment.Index:original.MetadataCount+fragment.Index);if(original.Queued.Count==original.Count&&original.NarrowQueued())
        {original.Enqueued=true;original.CachedPrimaries?.MarkEnqueued();if(original.Sources.Length==0){dispatches.Remove(original.Group);original.Close();}}
    }
    internal static void ReleaseUnenqueuedHydration(NetPackage[] packets)
    {
        if(!ThreadManager.IsMainThread()||packets==null)return;
        foreach(var original in packets.OfType<NetPackageRebirthUiDeferredHydrationV1>().Select(value=>value.Original).OfType<HydrationDispatch>().Distinct())
        {if(original.Enqueued)continue;if(dispatches.TryGetValue(original.Group,out var retained)&&ReferenceEquals(retained,original))dispatches.Remove(original.Group);original.Close();}
    }
    private sealed class HydrationStream:MemoryStream
    {
        public override void Write(byte[] bytes,int offset,int count){if(count<0||Position>NetPackageRebirthUiDeferredHydrationV1.NativeEnvelopeMaximum-count)throw new InvalidDataException();base.Write(bytes,offset,count);}
        public override void WriteByte(byte value){if(Position>=NetPackageRebirthUiDeferredHydrationV1.NativeEnvelopeMaximum)throw new InvalidDataException();base.WriteByte(value);}
    }
}



