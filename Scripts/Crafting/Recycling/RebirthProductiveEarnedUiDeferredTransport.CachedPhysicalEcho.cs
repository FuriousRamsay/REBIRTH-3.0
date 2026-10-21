using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static string CachedNativeDigest(byte[] body,int offset)
    {
        if(body==null||offset<0||offset>body.Length-8)throw new InvalidDataException();
        using(var sha=SHA256.Create())
        {
            sha.TransformBlock(body,0,offset,null,0);var elapsed=new byte[8];sha.TransformBlock(elapsed,0,8,null,0);
            sha.TransformFinalBlock(body,offset+8,body.Length-offset-8);
            return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
        }
    }
    private sealed class CachedEchoSetup
    {
        internal RebirthWorkstationCompletionPipeline.CachedPhysicalEchoSource Source;
        internal Guid Group=Guid.NewGuid();internal ulong Clock,First,Last;internal bool Valid;
    }
    private static readonly ConditionalWeakTable<NetPackageTileEntity,CachedEchoSetup> cachedEchoSetups=new ConditionalWeakTable<NetPackageTileEntity,CachedEchoSetup>();
    private static void BeginCachedPhysicalEchoSetup(NetPackageTileEntity packet,TileEntity tile,StreamModeWrite mode)
    {
        cachedEchoSetups.Remove(packet);if(mode!=StreamModeWrite.ToClient)return;
        var source=RebirthWorkstationCompletionPipeline.CachedPhysicalEchoSource.FromActualProcess(tile);
        if(source!=null)cachedEchoSetups.Add(packet,new CachedEchoSetup{Source=source,Clock=source.Station.lastTickTime,First=GameTimer.Instance.ticks});
    }
    private static void ObserveCachedPhysicalEchoSetup(NetPackageTileEntity packet,bool run)
    {
        if(!cachedEchoSetups.TryGetValue(packet,out var setup))return;
        try
        {
            setup.Last=GameTimer.Instance.ticks;var source=setup.Source;
            if(!run||!source.Current()||source.Station.lastTickTime!=setup.Clock||setup.First<setup.Clock||setup.Last<setup.First||
                !packet.teWorldPos.Equals(source.Station.ToWorldPos())||packet.teBlockId!=source.Station.blockValue.type||packet.ms?.Length!=source.Body.Length)return;
            using(var phase=source.Root.Memory.TryEnterCallbacks())
            {
                if(phase==null)return;byte[] body=ReadCachedEchoBody(packet);
                if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(body,source.Offset,source.Body,source.Offset))return;
                ulong elapsed=BitConverter.ToUInt64(body,source.Offset);setup.Valid=elapsed>=setup.First-setup.Clock&&elapsed<=setup.Last-setup.Clock;
            }
        }
        catch{setup.Valid=false;}
    }
    private static byte[] ReadCachedEchoBody(NetPackageTileEntity packet)
    {
        if(packet.ms==null||packet.ms.Length<8||packet.ms.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes)throw new InvalidDataException();
        byte[] bytes=new byte[checked((int)packet.ms.Length)];
        lock(packet.ms){long prior=packet.ms.Position;try{packet.ms.Position=0;int copied=0;while(copied<bytes.Length){int count=packet.ms.Read(bytes,copied,bytes.Length-copied);if(count<1)throw new EndOfStreamException();copied+=count;}}finally{packet.ms.Position=prior;}}
        return bytes;
    }
    private static bool CachedPhysicalEchoCurrent(object state)=>state is CachedEchoSetup setup&&setup.Valid&&setup.Source.Current();
    private static bool TryWrapCachedPhysicalEcho(ClientInfo peer,INetConnection connection,NetPackageTileEntity native,out NetPackage[] wrapped)
    {
        wrapped=null;if(!cachedEchoSetups.TryGetValue(native,out var setup))return false;
        // The creator already owns its actual local sync. Replaying the echo
        // over that same card would replace its retained invocation/session.
        if(ReferenceEquals(peer,setup.Source.Creator))return true;
        if(!setup.Source.CanEchoRecipient||setup.Source.Previous?.Recipient is not RecipientScope recipient||!ReferenceEquals(peer,recipient.Peer))return false;
        HydrationDispatch pending=null;var parts=new System.Collections.Generic.List<NetPackage>();
        try
        {
            if(!CachedPhysicalEchoCurrent(setup)||!ReferenceEquals(connection,recipient.Hydration.Connection)||native.Channel!=0||
                !ReferenceEquals(peer.netConnection?[0],connection)||peer.disconnecting||!peer.loginDone||connection.IsDisconnected()||
                !ReferenceEquals(recipient.World.GetEntity(recipient.Actor.entityId),recipient.Actor)||recipient.Actor.Died!=recipient.Deaths||
                RebirthCamperCharacterAccess.CurrentCreation(recipient.Actor)!=recipient.Creation||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)||owner.StorageKey!=recipient.Owner||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(32L*1024*1024,out var budget))return true;
            var source=setup.Source;
            pending=new HydrationDispatch(budget){Group=setup.Group,World=source.World,Station=source.Station,Peer=peer,Connection=connection,Actor=recipient.Actor,
                Owner=recipient.Owner,Creation=recipient.Creation,Deaths=recipient.Deaths,WorldKey=RebirthProductiveEarnedArtifact.WorldIdentity(source.World),
                Sources=Array.Empty<HostContribution>(),CachedEcho=setup,NativeBefore=source.Body};
            pending.Header=new XElement("recipientCachedPhysicalEcho",new XAttribute("version",1),new XAttribute("group",pending.Group.ToString("N")),
                new XAttribute("recipient",source.Previous.RecipientGroup.ToString("N")),new XAttribute("operation",source.Operation.ToString("N")),
                new XAttribute("original",RebirthWorkstationCompletionPipeline.CachedOriginalHash(source.Root)),
                new XAttribute("native",CachedNativeDigest(source.Body,source.Offset)),new XAttribute("offset",source.Offset),new XAttribute("handle",native.handle));
            byte[] metadata=OwnerFrameBytes(pending.Header),nativeBytes,envelope;
            using(var stream=new HydrationStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {writer.SetBaseStream(stream);native.write(writer);writer.Flush();nativeBytes=stream.ToArray();}
            using(var stream=new HydrationStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {writer.Write((uint)0x31484455);writer.Write(metadata.Length);writer.Write(metadata);writer.Write(0);writer.Write(nativeBytes.Length);writer.Write(nativeBytes);writer.Flush();envelope=stream.ToArray();}
            if(!pending.Current())return true;
            string digest=HydrationHash(envelope);pending.Count=(envelope.Length+NetPackageRebirthUiDeferredHydrationV1.Chunk-1)/NetPackageRebirthUiDeferredHydrationV1.Chunk;pending.EnvelopeLength=envelope.Length;
            for(int index=0;index<pending.Count;index++)
            {
                int start=index*NetPackageRebirthUiDeferredHydrationV1.Chunk,length=Math.Min(NetPackageRebirthUiDeferredHydrationV1.Chunk,envelope.Length-start);
                byte[] bytes=new byte[length];Buffer.BlockCopy(envelope,start,bytes,0,length);var lease=pending.Retain();if(lease==null)return true;
                parts.Add(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredHydrationV1>().Setup(pending.Group,envelope.Length,pending.Count,index,digest,bytes,pending,lease));
            }
            wrapped=parts.ToArray();parts.Clear();pending=null;return true;
        }
        catch{return true;}
        finally{foreach(var part in parts)NetPackageManager.FreePackage(part);pending?.Close();}
    }
    internal sealed class ClientCachedPhysicalObservation:IDisposable
    {
        internal ClientHydration Source;internal NetPackageTileEntity Packet;internal XElement Header;
        internal byte[] Body,Image;internal int Offset,Reads;internal long Version;internal bool ReadCompleted,Returned;
        internal string Path,Digest;internal IDisposable Allocation;
        internal bool Current()=>Returned&&ReadCompleted&&Reads==1&&Source?.CurrentSource()==true&&Source.ReadVersion==Version&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Image);
        public void Dispose(){Source=null;Packet=null;Header=null;Body=Image=null;Allocation?.Dispose();Allocation=null;}
    }
    [ThreadStatic]private static ClientCachedPhysicalObservation readingCachedPhysicalEcho;
    internal static void ObserveActualCachedRecipientSync(RebirthWorkstationCompletionPipeline.Frame frame)
    {
        if(!RebirthWorkstationCompletionPipeline.IsObservingCachedRecipientSync(frame))return;
        var terminal=frame.CachedPhysical.OwnerRenewal;var original=terminal.Source;
        if(!original.CurrentWindow()||!terminal.CurrentSavedSource()||!ReferenceEquals(original.Actor,frame.Observation.Viewer)||
            !ReferenceEquals(original.Station,frame.Observation.Station)||!ReferenceEquals(original.Window,frame.Observation.Window)||
            !original.Members.Any(value=>value.Operation==frame.Observation.Operation))return;
        ClientCachedPhysicalObservation scope=null;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(original.Station,out var body,out int offset))return;
                var memory=original.TryEnterPublicationWorkspace(checked(body.LongLength+65536));if(memory==null)return;
                scope=new ClientCachedPhysicalObservation{Source=original,Body=body,Offset=offset,Allocation=memory,Version=original.ReadVersion,
                    Reads=1,ReadCompleted=true,Returned=true,Digest=CachedNativeDigest(body,offset)};
            }
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Group.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(original.World)),new XElement("before",
                    new XElement("actualCachedRecipientSync",new XAttribute("version",1),new XAttribute("operation",frame.Observation.Operation.ToString("N")),
                        new XAttribute("delivery",frame.ConfirmedOriginal.Digest),new XAttribute("native",scope.Digest),new XAttribute("readVersion",scope.Version))));
            scope.Path=original.Path+".cached-physical-sync."+frame.Observation.Operation.ToString("N");
            if(!RebirthWorkstationCompletionPublication.Write(scope.Path,record))return;
            scope.Image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(scope.Path);if(scope.Image==null||!scope.Current())return;
            var previous=original.CachedPhysicalObservation;original.CachedPhysicalObservation=scope;scope=null;previous?.Dispose();
        }
        finally{scope?.Dispose();}
    }
    private static void BindCachedPhysicalEcho(ClientHydration transport,byte[] native,GameManager callbacks)
    {
        var header=transport.Header;
        if(header.Attributes().Count()!=8||(int?)header.Attribute("version")!=1||ReadGuid(header,"group")!=transport.Group||header.HasElements||transport.NativeEvents.Length!=0||
            !clientHydrations.TryGetValue(ReadGuid(header,"recipient"),out var original)||!original.CurrentSource()||!original.Consumed||original.Unknown||
            !ReferenceEquals(original.World,transport.World)||!ReferenceEquals(original.Connection,transport.Connection)||
            !original.Members.Any(member=>member.Operation==ReadGuid(header,"operation")))throw new InvalidDataException();
        var scope=new ClientCachedPhysicalObservation{Source=original,Header=new XElement(header),Offset=(int)header.Attribute("offset"),Digest=(string)header.Attribute("native")};
        var packet=NetPackageManager.GetPackage<NetPackageTileEntity>();scope.Packet=packet;
        try
        {
            scope.Allocation=original.TryEnterPublicationWorkspace(checked(native.Length+65536L));if(scope.Allocation==null)throw new InvalidDataException();
            using(var stream=new MemoryStream(native,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);if(reader.ReadUInt16()!=packet.PackageId)throw new InvalidDataException();packet.read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException();}
            if(!packet.teWorldPos.Equals(original.Station.ToWorldPos())||packet.teBlockId!=original.Station.blockValue.type||packet.handle!=(int)header.Attribute("handle"))throw new InvalidDataException();
            scope.Body=ReadCachedEchoBody(packet);if(CachedNativeDigest(scope.Body,scope.Offset)!=scope.Digest)throw new InvalidDataException();
            if(queueOwnerHydrationsProcessed.Count>=4096||queueOwnerHydrationHistoryMemory==null&&
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(4096L*128+65536,out queueOwnerHydrationHistoryMemory))throw new InvalidDataException();
            queueOwnerHydrationsProcessed.Add(transport.Group,transport.World);
            readingCachedPhysicalEcho=scope;packet.ProcessPackage(transport.World,callbacks);
            if(!scope.ReadCompleted||scope.Reads!=1)throw new InvalidDataException();
            scope.Returned=true;var previous=original.CachedPhysicalObservation;original.CachedPhysicalObservation=scope;scope=null;previous?.Dispose();
        }
        catch{original.Unknown=true;throw;}
        finally{readingCachedPhysicalEcho=null;if(scope!=null){scope.Packet=null;scope.Dispose();}else original.CachedPhysicalObservation.Packet=null;NetPackageManager.FreePackage(packet);}
    }
    private static void EnterCachedPhysicalEchoRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run,long version)
    {
        var source=readingCachedPhysicalEcho;
        if(source!=null&&run&&mode==StreamModeRead.FromServer&&ReferenceEquals(station,source.Source.Station)&&ReferenceEquals(reader.BaseStream,source.Packet?.ms))
        {source.Reads++;source.Version=version;}
    }
    private static void ObserveCachedPhysicalEchoRead(PooledBinaryReader reader,bool run)
    {
        var scope=readingCachedPhysicalEcho;
        if(!run||scope==null||scope.Reads!=1||!scope.Source.CurrentIdentity()||!ReferenceEquals(reader.BaseStream,scope.Packet?.ms)||reader.BaseStream.Position!=scope.Packet.ms.Length)return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(scope.Source.Station,scope.Body,scope.Offset))return;
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",scope.Source.Group.ToString("N")),
            new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(scope.Source.World)),new XElement("before",new XElement(scope.Header)),
            new XElement("actualCachedPhysicalEcho",new XAttribute("readVersion",scope.Version),new XElement(scope.Header)));
        scope.Path=scope.Source.Path+".cached-physical-echo."+(string)scope.Header.Attribute("group");
        if(!RebirthWorkstationCompletionPublication.Write(scope.Path,record))return;
        scope.Image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(scope.Path);if(scope.Image==null)return;
        scope.Source.ReadVersion=scope.Version;scope.ReadCompleted=true;
    }
}
