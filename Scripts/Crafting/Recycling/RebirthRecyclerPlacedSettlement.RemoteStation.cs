using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed class RemoteStationRead
    {
        internal readonly RemoteHostOriginal Source;internal readonly NetPackageTileEntity Packet;internal readonly World World;
        internal int Reads;internal bool ReadReturned,ProcessReturned;internal PooledBinaryReader Reader;
        private RemoteStationRead(RemoteHostOriginal source,NetPackageTileEntity packet,World world){Source=source;Packet=packet;World=world;}
        internal static RemoteStationRead Enter(RemoteHostOriginal source,NetPackageTileEntity packet,World world)=>readingRecyclerStation==null?new RemoteStationRead(source,packet,world):null;
    }
    [ThreadStatic]private static RemoteStationRead readingRecyclerStation;
    internal sealed class AcceptedStationUpload
    {
        internal readonly RemoteHostOriginal Source;
        private AcceptedStationUpload(RemoteHostOriginal source){Source=source;}
        internal static AcceptedStationUpload FromActualProcess(RemoteStationRead read,NetPackageTileEntity packet,World world)
        {
            if(!ReferenceEquals(readingRecyclerStation,read)||!ReferenceEquals(read.Packet,packet)||!ReferenceEquals(read.World,world)||
                !read.ProcessReturned||!read.ReadReturned||read.Reads!=1||!read.Source.OwnerCurrent()||
                !read.Source.NativeLineageCurrent()||!RebirthProductiveEarnedNativeBody.MatchesCurrent(read.Source.Station,read.Source.AfterNative,read.Source.AfterOffset))return null;
            return new AcceptedStationUpload(read.Source);
        }
        internal bool Current()=>ReferenceEquals(Source.AcceptedUpload,this)&&Source.AcceptedStation&&
            Source.Publication?.CanObserveAcceptedUpload(Source)==true;
    }
    internal sealed partial class RemoteHostOriginal
    {internal AcceptedStationUpload AcceptedUpload;}
    private static void FinishAcceptedRecyclerStation(RemoteHostOriginal source)
    {
        var proof=source.AcceptedUpload;if(proof==null)return;
        try{if(proof.Current()&&source.Publication.ObserveRemoteAccepted(proof))source.AcceptedUpload=null;}
        catch{/* Retry only this actual read's publication; never native Process. */}
    }
    internal static bool EnterRecyclerNativeUpload(NetPackageTileEntity packet,World world,bool run,out object state)
    {
        state=null;
        if(!run||packet?.Sender==null||world?.IsRemote()!=false)return true;
        var source=remoteRecyclerOriginals.Values.SingleOrDefault(value=>ReferenceEquals(value.World,world)&&value.Station.ToWorldPos()==packet.teWorldPos);
        if(source==null)return true;
        try
        {
            if(readingRecyclerStation!=null||source.ClosedAuthority!=null||source.UnusedRequested||source.AcceptedStation||!ReferenceEquals(source.Peer,packet.Sender)||!source.Current()||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var received)||!ReferenceEquals(received,source.Connection)||
                !RebirthRecyclerRuntime.HasPlacedNativeAccess(source.Actor,source.Station)||source.Publication?.SavedBefore()!=true||
                source.OwnerBeforeCheckpoint?.CurrentSaved()!=true||source.OwnerBeforeCheckpoint.AcceptedHead==null||
                !source.OwnerBeforeCheckpoint.AcceptedHead.TryGetTarget(out var head)||!ReferenceEquals(head,packet.Sender.latestPlayerData)||
                packet.teBlockId!=source.Station.blockValue.type||packet.ms==null||packet.ms.Length!=source.AfterNative.Length)return false;
            lock(packet.ms)
            {
                long position=packet.ms.Position;
                try
                {
                    packet.ms.Position=0;
                    for(int i=0;i<source.AfterNative.Length;i++)
                    {int next=packet.ms.ReadByte();if(next<0||!(i>=source.AfterOffset&&i<source.AfterOffset+8)&&next!=source.AfterNative[i])return false;}
                }
                finally{packet.ms.Position=position;}
            }
            var attempt=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldKey),
                new XElement("before",XElement.Parse(Encoding.UTF8.GetString(source.Image))),new XElement("recyclerRemoteStationAttempted",
                    new XAttribute("nonce",source.Nonce.ToString("N")),source.OwnerBeforeCheckpoint.CopySavedDescriptor()));
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".remote-station-attempted.xml");
            if(!RebirthWorkstationCompletionPublication.Write(path,attempt)||!XNode.DeepEquals(attempt,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return false;
            var entered=RemoteStationRead.Enter(source,packet,world);if(entered==null)return false;
            readingRecyclerStation=entered;state=entered;return true;
        }
        catch{return false;}
    }
    internal static void EnterRecyclerRemoteNativeRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {
        var state=readingRecyclerStation;
        if(!run||state==null||mode!=StreamModeRead.FromClient||!ReferenceEquals(station,state.Source.Station)||
            !ReferenceEquals(reader.BaseStream,state.Packet.ms)||state.Reads!=0)return;
        state.Reads++;state.Reader=reader;
    }
    internal static void ObserveRecyclerRemoteNativeRead(PooledBinaryReader reader,bool run)
    {
        var state=readingRecyclerStation;
        if(run&&state!=null&&state.Reads==1&&ReferenceEquals(reader,state.Reader)&&ReferenceEquals(reader.BaseStream,state.Packet.ms)&&
            reader.BaseStream.Position==state.Packet.ms.Length)state.ReadReturned=true;
    }
    internal static void ObserveRecyclerNativeUpload(NetPackageTileEntity packet,World world,bool run,object opaque)
    {
        if(!run||opaque is not RemoteStationRead state||!ReferenceEquals(readingRecyclerStation,state)||!ReferenceEquals(state.Packet,packet)||
            !ReferenceEquals(state.World,world)||!state.ReadReturned||state.Reads!=1||!state.Source.OwnerCurrent()||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(state.Source.Station,state.Source.AfterNative,state.Source.AfterOffset))return;
        state.ProcessReturned=true;
        var proof=AcceptedStationUpload.FromActualProcess(state,packet,world);
        if(proof==null){state.Source.AdmissionUnknown=true;return;}
        state.Source.AcceptedStation=true;state.Source.AcceptedUpload=proof;FinishAcceptedRecyclerStation(state.Source);
    }
    internal static void EndRecyclerNativeUpload(object opaque)
    {if(opaque is RemoteStationRead state&&ReferenceEquals(readingRecyclerStation,state)){state.Reader=null;readingRecyclerStation=null;}}
}
