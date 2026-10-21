using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private sealed class NativeEnvelope:IDisposable
    {
        internal Guid Dispatch=Guid.NewGuid();internal PendingNativeUpload Pending;internal INetConnection Connection;
        internal int Count;internal readonly HashSet<int> Enqueued=new HashSet<int>();internal bool Closed;
        internal IDisposable Allocation;private int references=1;
        internal IDisposable Retain(){if(Closed)return null;references++;return new Lease(this);}
        private sealed class Lease:IDisposable{private NativeEnvelope source;internal Lease(NativeEnvelope value){source=value;}public void Dispose(){System.Threading.Interlocked.Exchange(ref source,null)?.Release();}}
        private void Release(){if(--references==0){Pending=null;Connection=null;Allocation?.Dispose();Allocation=null;}}
        public void Dispose(){if(Closed)return;Closed=true;Release();}
    }
    internal static bool IsRemoteNativeEnvelope(NetPackage package)=>package is NetPackageRebirthCompletionNativeV1;
    internal static bool TryWrapRemoteNativeUpload(NetPackageTileEntity packet,INetConnection connection,out NetPackage[] wrapped)
    {
        wrapped=null;var pending=resumingNativeUpload;
        if(pending==null||!ReferenceEquals(pending.Setup?.Packet,packet))return false;
        NativeEnvelope envelope=null;IDisposable evidenceLease=null;var packages=new List<NetPackage>();
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var transports=manager?.GetConnectionToServer();
            if(pending.Unknown||pending.Ended||!pending.NativeHold||packet.inSendQueuesCount!=1||!pending.Setup.Current()||
                connection==null||transports==null||packet.Channel<0||packet.Channel>=transports.Length||!ReferenceEquals(transports[packet.Channel],connection))return true;
            XElement contribution;
            if(pending.Setup.CachedClose!=null)
            {
                var close=pending.Setup.CachedClose;
                if(!close.Current()||close.Setups!=1)return true;
                contribution=new XElement("cachedUnusedCloseSource",new XAttribute("close",close.Close.ToString("N")),
                    new XAttribute("header",close.Digest),new XElement(close.Header));
            }
            else if(pending.Setup.Cached!=null)
            {
                var cached=pending.Setup.Cached;
                if(!cached.Current()||(pending.Setup.CachedSpace!=null?!cached.Prepared||pending.Setup.CachedSpace.Observed!=true:
                    !cached.Ready||pending.Setup.Phase!=RemoteNativePhase.PhysicalAccepted&&pending.Setup.Phase!=RemoteNativePhase.QueueAccepted))return true;
                contribution=new XElement("cachedPhysicalSource",new XAttribute("ticket",cached.Grant.Ticket.ToString("N")),
                    new XAttribute("original",cached.Grant.Original),new XAttribute("generation",cached.Generation??""));
            }
            else if(pending.Setup.Effect!=null)
            {
                long size=pending.Setup.Effect.MeasureEvidence();
                if(size<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*size+65536),out evidenceLease))return true;
                var actual=pending.Setup.Effect.CopyEvidence();if(actual==null)return true;
                contribution=new XElement("sameOwnerEffect",actual);
            }
            else if(pending.Setup.LoadedDeferred!=null)
            {
                if(!TryCopyLoadedDeferredContribution(pending.Setup.Frame,out contribution,out evidenceLease))return true;
            }
            else if(pending.Receipt?.CurrentAggregateMembership()!=true||
                !RebirthProductiveEarnedUiQueueOwner.TryCopyNativeContribution(pending.Receipt,out contribution,out evidenceLease))return true;
            long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(contribution);
            if(pending.Setup.Phase==RemoteNativePhase.CachedUnusedClose){}
            else if(pending.Setup.Phase==RemoteNativePhase.CachedBoundary)
            {
                if(!IsCurrentRemoteCachedBoundary(pending.Setup.Frame))return true;
                measured=checked(measured+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(pending.Setup.Frame.Observation.FullBoundaryEvidence)+
                    RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(ActiveBefore(pending.Setup.Frame.Intent)));
            }
            else if(pending.Setup.Phase==RemoteNativePhase.CachedSpaceTransfer)
            {
                var evidence=pending.Setup.CachedSpace?.Evidence??pending.Setup.Transfer?.Evidence;if(evidence==null)return true;
                measured=checked(measured+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(evidence));
            }
            else if(pending.Setup.Phase!=RemoteNativePhase.ReceiptAccepted)
            {
                if(pending.Setup.Frame?.ConfirmedOriginal==null)return true;
                measured=checked(measured+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(pending.Setup.Frame.Intent.Element("confirmedOriginal"))+
                    RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(ActiveBefore(pending.Setup.Frame.Intent))+
                    RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(pending.Setup.Frame.ConfirmedOriginal.Write()));
            }
            long maximum=checked(measured+pending.Body.Length+65536);
            if(maximum>NetPackageRebirthCompletionNativeV1.Maximum||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*maximum),out var allocation))return true;
            envelope=new NativeEnvelope{Pending=pending,Connection=connection,Allocation=allocation};
            var header=new XElement("nativeCompletionUpload",new XAttribute("version",1),new XAttribute("dispatch",envelope.Dispatch.ToString("N")),
                new XAttribute("operation",pending.Setup.Operation.ToString("N")),new XAttribute("phase",(byte)pending.Setup.Phase),
                new XAttribute("nonce",pending.Setup.Nonce.ToString("N")),new XAttribute("elapsedOffset",pending.Setup.ElapsedOffset),new XElement(contribution));
            if(pending.Setup.Phase==RemoteNativePhase.CachedUnusedClose){}
            else if(pending.Setup.Phase==RemoteNativePhase.CachedBoundary)
                header.Add(new XElement("fullBoundary",new XElement(pending.Setup.Frame.Observation.FullBoundaryEvidence)),
                    new XElement("activeBefore",new XElement(ActiveBefore(pending.Setup.Frame.Intent))));
            else if(pending.Setup.Phase==RemoteNativePhase.CachedSpaceTransfer)
            {
                var evidence=pending.Setup.CachedSpace?.Observed==true?pending.Setup.CachedSpace.Evidence:
                    pending.Setup.Transfer?.Observed==true?pending.Setup.Transfer.Evidence:null;
                if(evidence==null)return true;header.Add(new XElement(evidence));
            }
            else if(pending.Setup.Phase!=RemoteNativePhase.ReceiptAccepted)
            {
                if(pending.Setup.Frame?.ConfirmedOriginal==null)return true;
                header.Add(new XElement("physicalDelivery",pending.Setup.Frame.ConfirmedOriginal.Write().Element("delivery").Elements().Single()),
                    new XElement("activeBefore",new XElement(ActiveBefore(pending.Setup.Frame.Intent))),
                    new XElement("confirmedOriginal",pending.Setup.Frame.ConfirmedOriginal.Write()));
            }
            byte[] metadata=Encoding.UTF8.GetBytes(header.ToString(SaveOptions.DisableFormatting)),native;
            using(var stream=new NativeEnvelopeStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {writer.SetBaseStream(stream);packet.write(writer);writer.Flush();native=stream.ToArray();}
            // The exact queued native object still has the byte body captured
            // inside Add; serialization must not silently select later state.
            lock(packet.ms)
            {long prior=packet.ms.Position;try{packet.ms.Position=0;for(int i=0;i<pending.Body.Length;i++)if(packet.ms.ReadByte()!=pending.Body[i])return true;
                if(packet.ms.Position!=packet.ms.Length)return true;}finally{packet.ms.Position=prior;}}
            byte[] bytes;
            using(var stream=new NativeEnvelopeStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {writer.Write((uint)0x314e434d);writer.Write(metadata.Length);writer.Write(metadata);writer.Write(native.Length);writer.Write(native);writer.Flush();bytes=stream.ToArray();}
            envelope.Count=(bytes.Length+NetPackageRebirthCompletionNativeV1.Chunk-1)/NetPackageRebirthCompletionNativeV1.Chunk;
            string digest;using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            for(int i=0;i<envelope.Count;i++)
            {
                int start=i*NetPackageRebirthCompletionNativeV1.Chunk,length=Math.Min(NetPackageRebirthCompletionNativeV1.Chunk,bytes.Length-start);
                var part=new byte[length];Buffer.BlockCopy(bytes,start,part,0,length);var lease=envelope.Retain();if(lease==null)return true;
                packages.Add(NetPackageManager.GetPackage<NetPackageRebirthCompletionNativeV1>().Setup(envelope.Dispatch,bytes.Length,envelope.Count,i,digest,part,envelope,lease));
            }
            wrapped=packages.ToArray();packages.Clear();envelope.Dispose();envelope=null;return true;
        }
        catch{return true;}
        finally{foreach(var item in packages)NetPackageManager.FreePackage(item);envelope?.Dispose();evidenceLease?.Dispose();}
    }
    internal static void NoteRemoteNativeEnqueued(NetPackage package,INetConnection connection)
    {
        if(package is not NetPackageRebirthCompletionNativeV1 fragment||fragment.Original is not NativeEnvelope source||
            !ReferenceEquals(source.Connection,connection)||fragment.Dispatch!=source.Dispatch||!source.Enqueued.Add(fragment.Index)||source.Enqueued.Count!=source.Count)return;
        var pending=source.Pending;if(pending==null||!ReferenceEquals(resumingNativeUpload,pending)||!pending.NativeHold)return;
        if(pending.Setup.CachedClose is CachedUnusedClose close){close.Enqueued=true;close.Dispatch=source.Dispatch;}
        if(pending.Setup.CachedSpace!=null)pending.Setup.CachedSpace.Enqueued=true;
        if(pending.Setup.Phase==RemoteNativePhase.CachedBoundary)
        {pending.Setup.Frame.CachedBoundaryVersion=pending.Setup.Frame.SavedFullBoundaryVersion;pending.Setup.Frame.CachedBoundaryEnqueued=true;}
        if(pending.Setup.CachedSpace==null&&pending.Setup.Phase==RemoteNativePhase.CachedSpaceTransfer&&
            pending.Setup.Transfer is CachedOutputTransfer transfer&&transfer.Observed&&transfer.Evidence!=null&&
            ReferenceEquals(transfer.Frame,pending.Setup.Frame)&&(pending.Setup.Effect==null||NativeViewerCachedEffectSaved(transfer.Frame)))
        {
            // Issued only by the complete, actual native enqueue group. This
            // is a custody fence; the next Before still proves host save.
            transfer.Frame.CachedEffectTransferVersion=transfer.NativeVersion;
            transfer.Frame.CachedEffectTransferEnqueued=true;
        }
        pendingNativeUploads.Remove(pending);var native=pending.Setup.Packet;
        pending.Body=null;pending.Receipt=null;pending.Setup=null;pending.TransferLease?.Dispose();pending.TransferLease=null;pending.SourceLease?.Dispose();pending.SourceLease=null;
        pending.Allocation?.Dispose();pending.Allocation=null;pending.NativeHold=false;native.SendQueueHandled();source.Pending=null;
    }
    internal static void ReleaseUnenqueuedRemoteNative(NetPackage[] packets)
    {
        foreach(var source in packets.OfType<NetPackageRebirthCompletionNativeV1>().Select(value=>value.Original).OfType<NativeEnvelope>().Distinct())
            source.Dispose();
        // Original packet custody remains with its exact pending action. A
        // partial private group is never automatically reprocessed/replayed.
    }
    private sealed class NativeEnvelopeStream:MemoryStream
    {
        public override void Write(byte[] bytes,int offset,int count){if(count<0||Position>NetPackageRebirthCompletionNativeV1.Maximum-count)throw new InvalidDataException();base.Write(bytes,offset,count);}
        public override void WriteByte(byte value){if(Position>=NetPackageRebirthCompletionNativeV1.Maximum)throw new InvalidDataException();base.WriteByte(value);}
    }
    private sealed class NativeInput:IDisposable
    {internal World World;internal ClientInfo Sender;internal IDisposable Allocation;internal int Total;internal string Digest;internal double Updated;public void Dispose(){Allocation?.Dispose();Allocation=null;Sender=null;World=null;}}
    private static readonly Dictionary<Guid,NativeInput> nativeInputs=new Dictionary<Guid,NativeInput>();
    private static readonly Dictionary<Guid,World> nativeProcessed=new Dictionary<Guid,World>();
    private static IDisposable nativeProcessedAllocation;
    private static void SweepNativeEnvelopeWorld(World world)
    {
        foreach(var pair in nativeInputs.Where(value=>!ReferenceEquals(value.Value.World,world)||UnityEngine.Time.realtimeSinceStartup-value.Value.Updated>30).ToArray())
        {nativeInputs.Remove(pair.Key);pair.Value.Dispose();}
        foreach(var key in nativeProcessed.Where(value=>!ReferenceEquals(value.Value,world)).Select(value=>value.Key).ToArray())nativeProcessed.Remove(key);
        if(nativeProcessed.Count==0){nativeProcessedAllocation?.Dispose();nativeProcessedAllocation=null;}
    }
    internal static void ReceiveRemoteNativeUpload(NetPackageRebirthCompletionNativeV1 fragment,World world,GameManager callbacks)
    {
        NativeInput input=null;
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var sender=fragment.Sender;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||manager?.IsServer!=true||
                sender==null||!sender.loginDone||sender.disconnecting||!ReferenceEquals(manager.Clients?.ForEntityId(sender.entityId),sender)||
                !fragment.ValidEntityIdForSender(sender.entityId)||fragment.Part==null||nativeProcessed.ContainsKey(fragment.Dispatch))return;
            if(!nativeInputs.TryGetValue(fragment.Dispatch,out input))
            {
                SweepNativeEnvelopeWorld(world);
                if(nativeInputs.Count>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*fragment.Total+65536),out var allocation))return;
                input=new NativeInput{World=world,Sender=sender,Total=fragment.Total,Digest=fragment.Digest,Allocation=allocation,Updated=UnityEngine.Time.realtimeSinceStartup};nativeInputs.Add(fragment.Dispatch,input);
            }
            if(!ReferenceEquals(input.World,world)||!ReferenceEquals(input.Sender,sender)||input.Total!=fragment.Total||input.Digest!=fragment.Digest)return;
            input.Updated=UnityEngine.Time.realtimeSinceStartup;
            string scope="mp015-native|"+NativeScope(world)+"|"+NativeScope(sender);
            if(!RebirthRemoteResourceFragments.TryAdd(scope,fragment.Dispatch,fragment.Digest,fragment.Total,fragment.Count,fragment.Index,Convert.ToBase64String(fragment.Part),
                UnityEngine.Time.realtimeSinceStartup,out var bytes)||bytes==null)return;
            if(nativeProcessed.Count>=4096||nativeProcessedAllocation==null&&
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(4096L*128+65536,out nativeProcessedAllocation))return;
            XElement header;byte[] native;
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
            {
                if(reader.ReadUInt32()!=0x314e434d)throw new InvalidDataException();int length=reader.ReadInt32();if(length<1||length>bytes.Length-12)throw new InvalidDataException();
                var metadata=reader.ReadBytes(length);using(var xml=new MemoryStream(metadata,false))using(var parsed=XmlReader.Create(xml,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=NetPackageRebirthCompletionNativeV1.Maximum}))header=XElement.Load(parsed);
                length=reader.ReadInt32();if(length<1||length>2*1024*1024+65536||length!=stream.Length-stream.Position)throw new InvalidDataException();native=reader.ReadBytes(length);
            }
            if(header.Name!="nativeCompletionUpload"||(int?)header.Attribute("version")!=1||(string)header.Attribute("dispatch")!=fragment.Dispatch.ToString("N")||
                (int?)header.Attribute("phase")<1||(int?)header.Attribute("phase")>6||!Guid.TryParseExact((string)header.Attribute("operation"),"N",out var operation))return;
            var phase=(RemoteNativePhase)(byte)(int)header.Attribute("phase");
            if(header.Element("cachedUnusedCloseSource")!=null)
            {ReceiveCachedUnusedNative(header,native,fragment.Dispatch,world,callbacks,sender);return;}
            if(header.Element("cachedPhysicalSource")!=null)
            {ReceiveCachedPhysicalNative(header,native,fragment.Dispatch,world,callbacks,sender);return;}
            var source=nativeHostOriginals.TryGetValue(operation,out var found)?found:null;
            if(source==null||!ReferenceEquals(source.Sender,sender)||!source.BeforeSaved||!source.CurrentViewer()||!source.AdmissionCurrent()||(string)header.Attribute("nonce")!=source.Nonce.ToString("N"))return;
            var packet=NetPackageManager.GetPackage<NetPackageTileEntity>();
            try
            {
                using(var stream=new MemoryStream(native,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
                {reader.SetBaseStream(stream);if(reader.ReadUInt16()!=packet.PackageId)throw new InvalidDataException();packet.read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException();}
                packet.Sender=sender;
                if(!packet.teWorldPos.Equals(source.Station.ToWorldPos())||packet.teBlockId!=source.Station.blockValue.type||packet.ms==null||packet.ms.Length>2*1024*1024)return;
                byte[] body;lock(packet.ms){long prior=packet.ms.Position;try{packet.ms.Position=0;body=new byte[(int)packet.ms.Length];int offset=0;while(offset<body.Length){int read=packet.ms.Read(body,offset,body.Length-offset);if(read==0)throw new EndOfStreamException();offset+=read;}}finally{packet.ms.Position=prior;}}
                var contribution=header.Element("deferredNativeContribution")??header.Element("sameOwnerEffect");
                if(header.Elements("deferredNativeContribution").Count()+header.Elements("sameOwnerEffect").Count()!=1)return;
                int elapsedOffset=(int)header.Attribute("elapsedOffset");
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    if(decoder==null||!RebirthProductiveEarnedNativeBody.TryRead(body,elapsedOffset,out _,out _))return;
                if(contribution==null)return;
                contribution=new XElement(contribution);contribution.SetAttributeValue("phase",(byte)phase);
                if(phase==RemoteNativePhase.CachedBoundary)contribution.Add(new XElement(header.Element("fullBoundary")),new XElement(header.Element("activeBefore")));
                else if(phase==RemoteNativePhase.CachedSpaceTransfer)contribution.Add(new XElement(header.Element("actualCachedSpaceTransfer")));
                else if(phase!=RemoteNativePhase.ReceiptAccepted)
                {contribution.Add(new XElement(header.Element("physicalDelivery")),new XElement(header.Element("activeBefore")));}
                RebirthProductiveEarnedUiDeferredTransport.HostContribution accepted=null;NativeViewerEffectAdmission effect=null;
                RebirthProductiveEarnedUiDeferredTransport.CachedBoundaryAdmission boundary=null;
                if(phase==RemoteNativePhase.CachedBoundary)
                {if(!RebirthProductiveEarnedUiDeferredTransport.CachedBoundaryAdmission.TryCreate(source,contribution,body,elapsedOffset,out boundary))return;}
                else if(!RebirthProductiveEarnedUiDeferredTransport.TryAdmitRemoteNativeEvent(source,contribution,body,elapsedOffset,out accepted,out effect))return;
                admittingRemoteNative=source;RemoteNativeEvent actual;
                RebirthWorkstationCompletionOriginal original=null;
                if((phase==RemoteNativePhase.PhysicalAccepted||phase==RemoteNativePhase.QueueAccepted)&&
                    !RebirthWorkstationCompletionOriginal.TryRead(header.Element("confirmedOriginal")?.Elements().SingleOrDefault(),out original))
                {admittingRemoteNative=null;boundary?.Dispose();return;}
                try{actual=RemoteNativeEvent.Admit(source,contribution,body,elapsedOffset,accepted,effect,phase,fragment.Dispatch,boundary,original);}
                catch{boundary?.Dispose();throw;}
                finally{admittingRemoteNative=null;}
                nativeProcessed.Add(fragment.Dispatch,world);executingRemoteNative=new RemoteNativeRead{Event=actual,Packet=packet};
                try{packet.ProcessPackage(world,callbacks);PublishRemoteNativeProcess(executingRemoteNative);}
                finally{executingRemoteNative=null;boundary?.Dispose();}
            }
            finally{NetPackageManager.FreePackage(packet);}
        }
        catch{/* An attempted native dispatch never retries from receipt absence or current equality. */}
        finally
        {
            if(input!=null&&nativeProcessed.ContainsKey(fragment.Dispatch))
            {nativeInputs.Remove(fragment.Dispatch);input.Dispose();}
        }
    }
}
