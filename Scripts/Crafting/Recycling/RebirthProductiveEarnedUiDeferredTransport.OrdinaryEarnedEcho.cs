using System;
using System.IO;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private static bool TryWrapOrdinaryEarnedEcho(ClientInfo peer,INetConnection connection,NetPackageTileEntity native,out NetPackage[] wrapped)
    {
        wrapped=null;if(!RebirthProductiveEarnedRemoteHost.TrySelectOriginalEcho(native,peer,connection,out var selected))return false;
        HydrationDispatch pending=null;var parts=new System.Collections.Generic.List<NetPackage>();
        try
        {
            var source=selected.Original;
            if(!selected.Current()||!ReferenceEquals(source.Sender,peer)||!ReferenceEquals(peer.netConnection?[0],connection)||native.Channel!=0||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(32L*1024*1024,out var budget))return true;
            pending=new HydrationDispatch(budget){Group=selected.Group,World=source.World,Station=source.Station,Peer=peer,Connection=connection,Actor=source.Actor,
                Owner=source.Owner,Creation=source.Creation,Deaths=source.Deaths,WorldKey=source.WorldKey,Sources=Array.Empty<HostContribution>(),OrdinaryEcho=selected};
            var frame=RebirthProductiveEarnedRowProof.Decode(source.Image);
            pending.Header=new XElement("ordinaryActualEarnedEcho",new XAttribute("version",1),new XAttribute("group",selected.Group.ToString("N")),
                new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("nonce",source.Nonce.ToString("N")),new XAttribute("digest",HydrationHash(source.Image)),
                new XAttribute("phase",selected.Viewer?selected.Tools?"viewerToolsSaved":selected.ViewerPlan?"viewerToolsBefore":"viewerTools":selected.Pending?"pending":selected.Tools?"tools":selected.Terminal?"terminal":selected.After?"after":"before"),new XAttribute("birth",frame.Birth.ToString("N")),new XAttribute("handle",native.handle));
            if(selected.Viewer)pending.Header.Add(new XAttribute("original",Convert.ToBase64String(source.Image)),new XAttribute("source",Convert.ToBase64String(source.ViewerToolsRecord)),
                new XAttribute("payer",source.Owner),new XAttribute("creation",source.Creation),new XAttribute("deaths",source.Deaths),
                new XAttribute("x",source.Station.ToWorldPos().x),new XAttribute("y",source.Station.ToWorldPos().y),new XAttribute("z",source.Station.ToWorldPos().z));
            if(selected.Tools||selected.ViewerPlan)pending.Header.Add(new XAttribute("purposeDigest",selected.PurposeDigest));
            if(!PackHydrationMetadata(pending,pending.Header,out var metadata,out var sideMetadata))return true;
            byte[] nativeBytes,envelope;
            using(var stream=new HydrationStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {writer.SetBaseStream(stream);native.write(writer);writer.Flush();nativeBytes=stream.ToArray();}
            using(var stream=new HydrationStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {writer.Write(0x31484455u);writer.Write(metadata.Length);writer.Write(metadata);writer.Write(0);writer.Write(nativeBytes.Length);writer.Write(nativeBytes);writer.Flush();envelope=stream.ToArray();}
            if(!pending.Current())return true;
            string digest=HydrationHash(envelope);int nativeCount=(envelope.Length+NetPackageRebirthUiDeferredHydrationV1.Chunk-1)/NetPackageRebirthUiDeferredHydrationV1.Chunk;
            int metadataCount=sideMetadata==null?0:(sideMetadata.Length+NetPackageRebirthUiDeferredHydrationV1.Chunk-1)/NetPackageRebirthUiDeferredHydrationV1.Chunk;
            if(checked(envelope.Length+(sideMetadata?.Length??0))>NetPackageRebirthUiDeferredHydrationV1.Maximum)return true;
            pending.Count=nativeCount+metadataCount;pending.NativeCount=nativeCount;pending.MetadataCount=metadataCount;
            pending.EnvelopeLength=envelope.Length+(sideMetadata?.Length??0);pending.NativeBefore=selected.Native;
            if(!RetainHydrationMetadataSend(pending,sideMetadata,digest))return true;
            if(sideMetadata!=null)
            {
                string sideDigest=HydrationHash(sideMetadata);
                for(int index=0;index<metadataCount;index++)
                {
                    int start=index*NetPackageRebirthUiDeferredHydrationV1.Chunk,length=Math.Min(NetPackageRebirthUiDeferredHydrationV1.Chunk,sideMetadata.Length-start);
                    byte[] bytes=new byte[length];Buffer.BlockCopy(sideMetadata,start,bytes,0,length);var lease=pending.Retain();if(lease==null)return true;
                    parts.Add(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredHydrationV1>().Setup(pending.Group,sideMetadata.Length,metadataCount,index,sideDigest,bytes,pending,lease).AsMetadata());
                }
            }
            for(int index=0;index<nativeCount;index++)
            {
                int start=index*NetPackageRebirthUiDeferredHydrationV1.Chunk,length=Math.Min(NetPackageRebirthUiDeferredHydrationV1.Chunk,envelope.Length-start);
                byte[] bytes=new byte[length];Buffer.BlockCopy(envelope,start,bytes,0,length);var lease=pending.Retain();if(lease==null)return true;
                parts.Add(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredHydrationV1>().Setup(pending.Group,envelope.Length,nativeCount,index,digest,bytes,pending,lease));
            }
            dispatches.Add(pending.Group,pending);wrapped=parts.ToArray();parts.Clear();pending=null;return true;
        }
        catch{return true;}
        finally{foreach(var packet in parts)NetPackageManager.FreePackage(packet);if(pending!=null)pending.Close();else if(wrapped==null)selected.Dispose();}
    }
    private static void BindOrdinaryEarnedEcho(ClientHydration transport,byte[] native,GameManager callbacks)
    {
        if(transport.NativeEvents.Length!=0||queueOwnerHydrationsProcessed.Count>=4096||queueOwnerHydrationsProcessed.ContainsKey(transport.Group)||
            queueOwnerHydrationHistoryMemory==null&&!RebirthProductiveEarnedObservationBudget.TryRetainDetached(4096L*128+65536,out queueOwnerHydrationHistoryMemory))
            throw new InvalidDataException("Actual earned echo history bound");
        queueOwnerHydrationsProcessed.Add(transport.Group,transport.World);
        if(!RebirthProductiveEarnedRemoteClient.AcceptNativeEcho(transport.Header,transport.Group,transport.World,transport.Connection,native,callbacks))
            throw new InvalidDataException("Unqualified actual earned echo");
    }
}
