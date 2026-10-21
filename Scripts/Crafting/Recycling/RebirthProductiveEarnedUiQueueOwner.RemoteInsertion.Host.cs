using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Runtime.CompilerServices;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    private sealed class RemoteHeader:IDisposable
    {
        internal Guid Operation,Birth;internal ClientInfo Peer;internal EntityPlayer Actor;internal World World;internal TileEntityWorkstation Station;
        internal string Text,Owner,Creation,Binding,Digest,WorldKey,Reply,Path;internal int Deaths,Index,Count,Block;
        internal byte[] Image;internal Witness Source;internal IDisposable Allocation,SourceHold,ImageAllocation;
        internal bool Consumed;
        internal bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World?.IsRemote()==false&&
            Actor!=null&&!Actor.IsDead()&&Actor.Died==Deaths&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&
            RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&Peer.loginDone&&!Peer.disconnecting&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(Actor.entityId),Peer)&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Peer,out var owner)&&owner.StorageKey==Owner&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&World.GetBlock(Station.ToWorldPos()).type==Block&&
            RebirthProductiveEarnedArtifact.WorldIdentity(World)==WorldKey&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var birth)&&birth==Birth.ToString("N");
        public void Dispose(){Allocation?.Dispose();SourceHold?.Dispose();ImageAllocation?.Dispose();Allocation=SourceHold=ImageAllocation=null;Image=null;}
    }
    private static readonly Dictionary<Guid,RemoteHeader> remoteHeaders=new Dictionary<Guid,RemoteHeader>();
    private sealed class RemoteIssued {internal object Event;}
    private static readonly ConditionalWeakTable<NativeIntroduction,object> remoteEvents=new ConditionalWeakTable<NativeIntroduction,object>();
    private static readonly ConditionalWeakTable<NativeIntroduction,object>.CreateValueCallback createRemoteEvent=ignored=>new object();
    private static readonly ConditionalWeakTable<Witness,RemoteIssued> remoteIssued=new ConditionalWeakTable<Witness,RemoteIssued>();
    private static void RecordRemoteIssuedSource(Witness source)
    {
        if(reading is NativeIntroduction native&&native.ReadCompleted&&native.Reads==1&&ReferenceEquals(native.Station,source.Station))
            remoteIssued.Add(source,new RemoteIssued{Event=remoteEvents.GetValue(native,createRemoteEvent)});
    }
    internal static void ReceiveRemoteInsertionRequest(NetPackageRebirthUiQueueInsertionRequestV1 packet,World world)
    {
        try
        {
            if(!installed||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(world,GameManager.Instance?.World)||packet.Sender==null)return;
            using var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();if(decoder==null)return;
            ObserveWorld();var node=XElement.Parse(packet.Text,LoadOptions.None);
            if(node.Name!="remoteQueueInsertion"||node.Attributes().Count()!=14||node.Elements().Count()!=1||node.Element("row")==null||node.Element("row").HasElements||node.Element("row").HasAttributes||
                (int?)node.Attribute("version")!=1||!Guid.TryParseExact((string)node.Attribute("operation"),"N",out var operation)||operation==Guid.Empty)return;
            var peer=packet.Sender;int player=(int)node.Attribute("player");var actor=world.GetEntity(player) as EntityPlayer;
            string owner=(string)node.Attribute("owner"),creation=(string)node.Attribute("creation");int deaths=(int)node.Attribute("deaths");
            var position=new Vector3i((int)node.Attribute("x"),(int)node.Attribute("y"),(int)node.Attribute("z"));
            var station=world.GetTileEntity(position) as TileEntityWorkstation;
            if(actor==null||actor.IsDead()||station==null||station is RebirthCamperStationState||!peer.loginDone||peer.disconnecting||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(player),peer)||!packet.ValidEntityIdForSender(player)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||identity.StorageKey!=owner||
                actor.Died!=deaths||RebirthCamperCharacterAccess.CurrentCreation(actor)!=creation||!RebirthOriginalActionScope.Validate(owner,creation))return;
            if(remoteHeaders.TryGetValue(operation,out var existing))
            {
                if(ReferenceEquals(existing.Peer,peer)&&existing.Text==packet.Text&&existing.Current()&&existing.Consumed)
                {
                    if(packet.Ack&&existing.Source?.SourceCurrent()==true&&RebirthProductiveEarnedArtifact.FileMatches(existing.Path,existing.Image))
                    {remoteHeaders.Remove(operation);existing.Dispose();}
                    else if(!packet.Ack)ReplyRemoteInsertion(existing);
                }
                return;
            }
            if(packet.Query||packet.Ack||remoteHeaders.Count>=32||station.Queue==null)return;
            int index=(int)node.Attribute("index"),count=(int)node.Attribute("count"),block=(int)node.Attribute("block");
            string binding=node.Element("row").Value,digest=(string)node.Attribute("digest"),worldKey=(string)node.Attribute("world");
            if(index<0||index>=station.Queue.Length||count<1||count>short.MaxValue||binding.Length<1||binding.Length>32768||
                digest?.Length!=44||world.GetBlock(position).type!=block||RebirthProductiveEarnedArtifact.WorldIdentity(world)!=worldKey||
                !RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var birth)||!Guid.TryParseExact(birth,"N",out var incarnation)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*packet.Text.Length+65536),out var allocation))return;
            var header=new RemoteHeader{Operation=operation,Peer=peer,Actor=actor,World=world,Station=station,Text=packet.Text,Owner=owner,Creation=creation,
                Deaths=deaths,Index=index,Count=count,Block=block,Binding=binding,Digest=digest,WorldKey=worldKey,Birth=incarnation,Allocation=allocation};
            if(!header.Current()){header.Dispose();return;}remoteHeaders.Add(operation,header);
        }
        catch{/* Invalid metadata never changes native enqueue handling. */}
    }
    // Called after the existing sole accepted-native observer has issued the
    // full durable earlier owner source. The same NativeIntroduction is private
    // proof of actual packet/read execution, not a caller-provided acceptance.
    internal static void ObserveRemoteIntroduction(NetPackageTileEntity packet,World world,bool run,object state)
    {
        try
        {
            if(!run||state is not NativeIntroduction native||!ReferenceEquals(reading,native)||native.Reads!=1||!native.ReadCompleted||
                !ReferenceEquals(native.Packet,packet)||!ReferenceEquals(native.World,world)||native.PacketBody==null)return;
            using var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();if(decoder==null)return;
            string digest=ProofHash(native.PacketBody);
            foreach(var header in remoteHeaders.Values.Where(value=>!value.Consumed&&ReferenceEquals(value.Peer,packet.Sender)&&
                ReferenceEquals(value.Station,native.Station)&&value.Digest==digest).ToArray())
            {
                if(!header.Current()||header.Owner!=native.Owner||header.Creation!=native.Creation||header.Deaths!=native.Deaths)continue;
                var rows=native.Station.Queue;var row=rows?[header.Index];if(row?.Recipe==null||row.Multiplier!=header.Count||row.StartingEntityId!=native.Actor.entityId||Binding(row)!=header.Binding)continue;
                string nativeBinding=NativeBinding(row);
                if(native.Bindings.Any(before=>before==nativeBinding)||rows.Count(other=>other?.Recipe!=null&&NativeBinding(other)==nativeBinding)!=1)continue;
                var sources=originals.Where(value=>ReferenceEquals(value.Station,native.Station)&&value.SourcePublished()&&value.Matches(row)&&
                    value.OriginalOwnerKey==native.Owner&&value.Creation==native.Creation&&value.Deaths==native.Deaths).ToArray();if(sources.Length!=1)continue;
                var source=sources[0];
                if(!remoteIssued.TryGetValue(source,out var issued)||!remoteEvents.TryGetValue(native,out var nativeEvent)||!ReferenceEquals(issued.Event,nativeEvent))continue;
                long budget=checked(12L*(source.Image.Length+native.PacketBody.Length+header.Text.Length)+65536);
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(budget,out var allocation))continue;
                IDisposable hold=null;
                try
                {
                    var original=XElement.Parse(Encoding.UTF8.GetString(source.Image));
                    if(original.Element("beforeNative")?.Value!=Convert.ToBase64String(native.Before))continue;
                    string purpose=Export(source);if(purpose.Length<1||purpose.Length>4096)continue;
                    var reply=new XElement("remoteQueueInsertionAccepted",new XAttribute("version",1),new XAttribute("operation",header.Operation.ToString("N")),
                        new XAttribute("digest",header.Digest),new XAttribute("index",header.Index),new XAttribute("count",header.Count),new XElement("purpose",purpose)).ToString(SaveOptions.DisableFormatting);
                    hold=source.RetainPublishedSource();if(hold==null)continue;
                    var image=Encoding.UTF8.GetBytes(new XElement("remoteQueueInsertionAttestation",new XAttribute("version",1),
                        XElement.Parse(header.Text),new XElement("acceptedNativePacket",Convert.ToBase64String(native.PacketBody)),
                        new XElement("fullDurableOwnerOriginal",original),XElement.Parse(reply)).ToString(SaveOptions.DisableFormatting));
                    if(image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)continue;
                    string path=Path.Combine(source.Save,"RebirthProductiveEarnedUi","queueInsertions",header.Owner,header.Operation.ToString("N")+".accepted");
                    if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(path,image)||!header.Current()||!source.SourceCurrent())continue;
                    header.Image=image;header.Path=path;header.Reply=reply;header.Source=source;header.SourceHold=hold;hold=null;
                    header.ImageAllocation=allocation;allocation=null;header.Consumed=true;ReplyRemoteInsertion(header);
                }
                finally{hold?.Dispose();allocation?.Dispose();}
            }
        }
        catch{/* Retain unresolved actual insertion; no equality-only fallback. */}
    }
    private static void ReplyRemoteInsertion(RemoteHeader header)
    {
        if(!header.Consumed||!header.Current()||header.Source?.SourceCurrent()!=true||
            !RebirthProductiveEarnedArtifact.FileMatches(header.Path,header.Image))return;
        header.Peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthUiQueueInsertionReplyV1>().Setup(header.Reply));
    }
}
