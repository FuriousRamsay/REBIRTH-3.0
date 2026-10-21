using System;
using System.Linq;
using System.Threading;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static void ObserveRemoteRecipientCloseSetup(NetPackageTileEntity packet,TileEntity tile,StreamModeWrite mode,bool run)
    {
        var close=closingRecipient;
        if(close==null||!close.SyncEntered||mode!=StreamModeWrite.ToServer||!ReferenceEquals(tile,close.Original.Station))return;
        close.Setups++;
        try
        {
            if(!run||close.Setups!=1||!close.Identity()||packet.handle!=close.Original.Station.lockHandleWaitingFor||packet.ms==null||packet.ms.Length!=close.Native.Length)return;
            var bytes=new byte[close.Native.Length];
            lock(packet.ms){long prior=packet.ms.Position;try{packet.ms.Position=0;int n=0;while(n<bytes.Length){int count=packet.ms.Read(bytes,n,bytes.Length-n);if(count<1)return;n+=count;}}finally{packet.ms.Position=prior;}}
            if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(bytes,close.Offset,close.Native,close.Offset))return;
            close.Packet=packet;close.PacketBody=bytes;close.Handle=packet.handle;
        }
        catch{/* No native enqueue witness. */}
    }
    private sealed class CloseSend
    {internal RemoteClose Source;internal NetConnectionAbs Connection;internal NetPackage Packet;internal NetPackage[] Before;internal int References;internal object Gate;internal bool Locked;}
    internal static void EnterRemoteRecipientCloseSend(NetConnectionAbs connection,NetPackage packet,bool run,out object state)
    {
        state=null;var close=closingRecipient;
        if(!run||close==null||!ReferenceEquals(close.Packet,packet)||!ReferenceEquals(close.Original.Connection,connection)||!close.Identity())return;
        var read=new CloseSend{Source=close,Connection=connection,Packet=packet,Gate=QueueGate(connection)};
        try
        {
            if(read.Gate==null)return;Monitor.Enter(read.Gate);read.Locked=true;read.Before=QueueImage(connection);
            if(read.Before.Length>8192||read.Before.Any(value=>ReferenceEquals(value,packet)))return;
            read.References=packet.inSendQueuesCount;state=read;read=null;
        }
        finally{if(read?.Locked==true)Monitor.Exit(read.Gate);}
    }
    internal static void ObserveRemoteRecipientCloseSend(bool run,object state)
    {
        if(!run||state is not CloseSend read||!read.Locked||!ReferenceEquals(closingRecipient,read.Source)||!read.Source.Identity())return;
        var after=QueueImage(read.Connection);
        if(after.Length!=read.Before.Length+1||!after.Take(read.Before.Length).SequenceEqual(read.Before)||!ReferenceEquals(after[^1],read.Packet)||
            read.Packet.inSendQueuesCount!=read.References+1)return;
        read.Source.Sends++;read.Source.Enqueued=read.Source.Sends==1;read.Source.Packet=null;
    }
    internal static void EndRemoteRecipientCloseSend(object state)
    {if(state is CloseSend read&&read.Locked){read.Locked=false;Monitor.Exit(read.Gate);}}
}
