using System;
using System.IO;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private sealed class RecyclerOwnerEnvelope
    {internal NetPackagePlayerData Native;internal NetPackageRebirthRecyclerPlacedRequestV1 Envelope;internal INetConnection Connection;internal bool ReadReturned,Automatic;}
    [ThreadStatic]private static RecyclerOwnerEnvelope executingRecyclerOwnerEnvelope;
    private static bool TryRecyclerReceivedConnection(NetPackage packet,out INetConnection connection)
    {
        var scope=executingRecyclerOwnerEnvelope;
        if(scope!=null&&scope.ReadReturned&&ReferenceEquals(scope.Native,packet)&&ReferenceEquals(scope.Envelope,processingRecyclerRequest)&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(scope.Envelope,out var actual)&&ReferenceEquals(actual,scope.Connection))
        {connection=actual;return true;}
        return RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out connection);
    }
    private static void SendPlacedOwnerEnvelope(RecyclerClient client,SavedOwnerCheckpoint checkpoint)
    {
        if(!client.Live()||!ReferenceEquals(client.Owner?.PendingCheckpoint,checkpoint)||checkpoint.FrozenOwnerPacket==null||!checkpoint.LocalObservedSaved())throw new InvalidDataException();
        var header=ClientCommand(client,"ownerNative");header.Add(new XAttribute("phase",checkpoint.Phase),new XAttribute("completed",checkpoint.Completed),new XAttribute("wireDigest",checkpoint.FrozenWireDigest));
        byte[] text=Encoding.UTF8.GetBytes(header.ToString(SaveOptions.DisableFormatting));if(text.Length>8192)throw new InvalidDataException();
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*(checkpoint.FrozenOwnerPacket.Length+text.Length)+65536),out var memory))throw new InvalidDataException();
        using(memory)using(var stream=new RecyclerPdsPacketStream())using(var writer=new BinaryWriter(stream))
        {writer.Write((byte)1);writer.Write(text.Length);writer.Write(text);writer.Write(checkpoint.FrozenOwnerPacket.Length);writer.Write(checkpoint.FrozenOwnerPacket);writer.Flush();SendPlacedBytes(stream.ToArray());}
    }
    private static void ReceivePlacedOwnerEnvelope(NetPackageRebirthRecyclerPlacedRequestV1 envelope,World world,INetConnection connection,byte[] bytes)
    {
        if(executingRecyclerOwnerEnvelope!=null||!ReferenceEquals(processingRecyclerRequest,envelope))return;
        NetPackagePlayerData native=null;
        try
        {
            using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);if(reader.ReadByte()!=1)throw new InvalidDataException();int textLength=reader.ReadInt32();if(textLength<1||textLength>8192)throw new InvalidDataException();
                byte[] text=reader.ReadBytes(textLength);if(text.Length!=textLength)throw new EndOfStreamException();
                var header=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(text),8192);
                int length=reader.ReadInt32();if(length<1||length>24*1024*1024||stream.Position+length!=stream.Length)throw new InvalidDataException();
                string command=(string)header.Attribute("command");bool automatic=command=="ownerAutosave";
                if(header.Name!="recyclerRemoteCommand"||!automatic&&command!="ownerNative"||!Guid.TryParseExact((string)header.Attribute("operation"),"N",out var operation)||
                    !remoteRecyclerOriginals.TryGetValue(operation,out var source)||!ReferenceEquals(source.Peer,envelope.Sender)||!ReferenceEquals(source.Connection,connection)||
                    (string)header.Attribute("nonce")!=source.Nonce.ToString("N")||(string)header.Attribute("original")!=RebirthStationPublicationRecord.Digest(source.Image))return;
                var token=source.OwnerCheckpoint;
                if(!automatic&&(token==null||token.Entered||(int?)header.Attribute("phase")!=token.Phase||
                    (int?)header.Attribute("completed")!=token.Completed||(string)header.Attribute("wireDigest")!=token.WireDigest))return;
                native=NetPackageManager.GetPackage<NetPackagePlayerData>();
                if(reader.ReadUInt16()!=native.PackageId)throw new InvalidDataException();native.read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException();
                native.Sender=envelope.Sender;
                if(automatic)
                {
                    if(!native.ValidEntityIdForSender(native.playerDataFile.id)||token?.Matches(native.playerDataFile)!=true)return;
                    byte[] wire=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.WireImage(native.playerDataFile);string digest;
                    using(var hash=System.Security.Cryptography.SHA256.Create())digest=Convert.ToBase64String(hash.ComputeHash(wire));
                    token=RemoteOwnerCheckpoint.BeginAutomatic(source,header,wire.Length,digest);if(token==null)return;
                }
                var scope=new RecyclerOwnerEnvelope{Native=native,Envelope=envelope,Connection=connection,ReadReturned=true,Automatic=automatic};executingRecyclerOwnerEnvelope=scope;
                try{native.ProcessPackage(world,GameManager.Instance);}finally{executingRecyclerOwnerEnvelope=null;}
                ReplyPlaced(source);
            }
        }
        finally{if(native!=null&&native.inSendQueuesCount==0)NetPackageManager.FreePackage(native);}
    }
}
