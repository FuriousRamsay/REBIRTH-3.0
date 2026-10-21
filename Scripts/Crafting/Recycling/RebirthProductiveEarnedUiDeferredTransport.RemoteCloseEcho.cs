using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Runtime.CompilerServices;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // This is the native response produced INSIDE the one accepted close
    // Process. It is not a new Setup, a raw equal broadcast, or a replay.
    private sealed class CloseEchoSetup
    {internal CloseRead Read;internal ulong Clock,First,Last;internal bool Valid;}
    private static readonly ConditionalWeakTable<NetPackageTileEntity,CloseEchoSetup> closeEchoSetups=new ConditionalWeakTable<NetPackageTileEntity,CloseEchoSetup>();
    private static void BeginRemoteCloseEchoSetup(NetPackageTileEntity packet,TileEntity tile,StreamModeWrite mode)
    {
        closeEchoSetups.Remove(packet);var read=readingClose;
        if(mode==StreamModeWrite.ToClient&&read?.Complete==true&&read.Reads==1&&ReferenceEquals(tile,read.Source.Station))
            closeEchoSetups.Add(packet,new CloseEchoSetup{Read=read,Clock=read.Source.Station.lastTickTime,First=GameTimer.Instance.ticks});
    }
    private static void ObserveRemoteCloseEchoSetup(NetPackageTileEntity packet,bool run)
    {
        if(!closeEchoSetups.TryGetValue(packet,out var value))return;
        value.Last=GameTimer.Instance.ticks;var source=value.Read.Source;
        try
        {
            if(!run||!ReferenceEquals(readingClose,value.Read)||!value.Read.Complete||!source.Identity()||packet.handle!=value.Read.Packet.handle||
                !packet.teWorldPos.Equals(source.Station.ToWorldPos())||packet.teBlockId!=source.Station.blockValue.type||
                source.Station.lastTickTime!=value.Clock||value.First<value.Clock||value.Last<value.First||packet.ms?.Length!=value.Read.Native.Length)return;
            byte[] bytes=new byte[value.Read.Native.Length];lock(packet.ms){long old=packet.ms.Position;try{packet.ms.Position=0;int offset=0;while(offset<bytes.Length){int count=packet.ms.Read(bytes,offset,bytes.Length-offset);if(count<1)return;offset+=count;}}finally{packet.ms.Position=old;}}
            if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(bytes,source.Offset,value.Read.Native,source.Offset))return;
            ulong elapsed=BitConverter.ToUInt64(bytes,source.Offset);value.Valid=elapsed>=value.First-value.Clock&&elapsed<=value.Last-value.Clock;
        }
        catch{value.Valid=false;}
    }
    private static bool CloseEchoCurrent(object state)=>state is CloseEchoSetup value&&value.Valid&&ReferenceEquals(readingClose,value.Read)&&
        value.Read.Complete&&value.Read.Reads==1&&value.Read.Source.Identity();
    private static bool TryWrapRemoteCloseEcho(ClientInfo peer,INetConnection connection,NetPackageTileEntity native,out NetPackage[] wrapped)
    {
        wrapped=null;if(!closeEchoSetups.TryGetValue(native,out var selected)||!ReferenceEquals(selected.Read.Source.Peer,peer))return false;
        HydrationDispatch pending=null;var parts=new System.Collections.Generic.List<NetPackage>();
        try
        {
            var close=selected.Read.Source;
            if(!CloseEchoCurrent(selected)||!ReferenceEquals(close.Connection,connection)||native.Channel!=0||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(32L*1024*1024,out var budget))return true;
            pending=new HydrationDispatch(budget){Group=close.Operation,World=close.World,Station=close.Station,Peer=peer,Connection=connection,Actor=close.Actor,
                Owner=close.OwnerKey,Creation=close.Creation,Deaths=close.Deaths,WorldKey=RebirthProductiveEarnedArtifact.WorldIdentity(close.World),Sources=Array.Empty<HostContribution>(),CloseEcho=selected};
            pending.Header=new XElement("recipientActualCloseEcho",new XAttribute("version",1),new XAttribute("group",close.Operation.ToString("N")),
                new XAttribute("recipient",close.Group.ToString("N")),new XAttribute("header",close.HeaderHash),new XAttribute("handle",native.handle));
            byte[] metadata=OwnerFrameBytes(pending.Header),nativeBytes,envelope;
            using(var stream=new HydrationStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {writer.SetBaseStream(stream);native.write(writer);writer.Flush();nativeBytes=stream.ToArray();}
            using(var stream=new HydrationStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {writer.Write((uint)0x31484455);writer.Write(metadata.Length);writer.Write(metadata);writer.Write(0);writer.Write(nativeBytes.Length);writer.Write(nativeBytes);writer.Flush();envelope=stream.ToArray();}
            if(!pending.Current())return true;
            string digest=HydrationHash(envelope);pending.Count=(envelope.Length+NetPackageRebirthUiDeferredHydrationV1.Chunk-1)/NetPackageRebirthUiDeferredHydrationV1.Chunk;
            pending.EnvelopeLength=envelope.Length;pending.NativeBefore=selected.Read.Native;
            for(int index=0;index<pending.Count;index++)
            {
                int start=index*NetPackageRebirthUiDeferredHydrationV1.Chunk,length=Math.Min(NetPackageRebirthUiDeferredHydrationV1.Chunk,envelope.Length-start);
                byte[] bytes=new byte[length];Buffer.BlockCopy(envelope,start,bytes,0,length);var lease=pending.Retain();if(lease==null)return true;
                parts.Add(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredHydrationV1>().Setup(pending.Group,envelope.Length,pending.Count,index,digest,bytes,pending,lease));
            }
            wrapped=parts.ToArray();parts.Clear();pending=null;return true;
        }
        catch{return true;}
        finally{foreach(var packet in parts)NetPackageManager.FreePackage(packet);pending?.Close();}
    }
    [ThreadStatic]private static RemoteClose receivingCloseEcho;
    private static void BindRemoteCloseEcho(ClientHydration transport,byte[] native,GameManager callbacks)
    {
        var header=transport.Header;
        if(header.Attributes().Count()!=5||(int?)header.Attribute("version")!=1||ReadGuid(header,"group")!=transport.Group||header.HasElements||transport.NativeEvents.Length!=0||
            !remoteCloses.TryGetValue(ReadGuid(header,"recipient"),out var close)||!close.Identity()||close.Operation!=transport.Group||!close.Enqueued||close.Observed==null||
            !ReferenceEquals(close.Original.World,transport.World)||!ReferenceEquals(close.Original.Connection,transport.Connection)||
            (string)header.Attribute("header")!=HydrationHash(close.Header)||(int)header.Attribute("handle")!=close.Handle||close.EchoObserved)throw new InvalidDataException();
        var packet=NetPackageManager.GetPackage<NetPackageTileEntity>();
        try
        {
            using(var stream=new MemoryStream(native,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);if(reader.ReadUInt16()!=packet.PackageId)throw new InvalidDataException();packet.read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException();}
            if(packet.handle!=close.Handle||!packet.teWorldPos.Equals(close.Original.Station.ToWorldPos())||packet.teBlockId!=close.Original.Station.blockValue.type||
                packet.ms?.Length!=close.Native.Length)throw new InvalidDataException();
            byte[] body=new byte[close.Native.Length];lock(packet.ms){long old=packet.ms.Position;try{packet.ms.Position=0;int count=0;while(count<body.Length){int n=packet.ms.Read(body,count,body.Length-count);if(n<1)throw new EndOfStreamException();count+=n;}}finally{packet.ms.Position=old;}}
            if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(body,close.Offset,close.Native,close.Offset))throw new InvalidDataException();
            // Record no-replay history before the actual Process; an exception
            // is unknown and cannot cause another native invocation.
            if(queueOwnerHydrationsProcessed.Count>=4096||queueOwnerHydrationHistoryMemory==null&&
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(4096L*128+65536,out queueOwnerHydrationHistoryMemory))throw new InvalidDataException();
            queueOwnerHydrationsProcessed.Add(transport.Group,transport.World);close.EchoPacket=packet;
            receivingCloseEcho=close;packet.ProcessPackage(transport.World,callbacks);
            if(!close.EchoObserved)throw new InvalidDataException();
        }
        finally{receivingCloseEcho=null;close.EchoPacket=null;NetPackageManager.FreePackage(packet);}
    }
    private static void EnterRemoteCloseEchoRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run,long version)
    {
        var close=receivingCloseEcho;
        if(run&&mode==StreamModeRead.FromServer&&close!=null&&ReferenceEquals(close.Original.Station,station)&&ReferenceEquals(reader.BaseStream,close.EchoPacket?.ms))
        {close.EchoReads++;close.EchoVersion=version;}
    }
    private static void ObserveRemoteCloseEchoRead(PooledBinaryReader reader,bool run)
    {
        var close=receivingCloseEcho;
        if(!run||close==null||close.EchoReads!=1||!close.Original.CurrentIdentity()||!ReferenceEquals(reader.BaseStream,close.EchoPacket?.ms)||
            reader.BaseStream.Position!=close.EchoPacket.ms.Length)return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(close.Original.Station,close.Native,close.Offset)||!CloseOwnerUnchanged(close))return;
            byte[] receipt=Encoding.ASCII.GetBytes("actual-close-native-echo:"+close.Operation.ToString("N")+":"+HydrationHash(close.Header));
            if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(close.Path+".echo",receipt))return;
            close.Original.ReadVersion=close.EchoVersion;close.EchoObserved=true;
        }
    }
}
