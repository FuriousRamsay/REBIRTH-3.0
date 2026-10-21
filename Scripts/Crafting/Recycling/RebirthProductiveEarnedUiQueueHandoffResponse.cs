using System;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiRemote
{
    private sealed class QueueResponse
    {internal QueueRead Read;internal NetPackageTileEntity Packet;internal ulong Clock,First,Last;internal bool Valid;internal IDisposable Resources;}
    private static readonly ConditionalWeakTable<NetPackageTileEntity,QueueResponse> queueResponses=new ConditionalWeakTable<NetPackageTileEntity,QueueResponse>();
    internal static bool IsQueueResponseCandidate(NetPackageTileEntity packet)=>packet!=null&&queueResponses.TryGetValue(packet,out var value)&&ReferenceEquals(readingQueue,value.Read);
    internal static void BeginQueueResponseSetup(NetPackageTileEntity packet,TileEntity tile,StreamModeWrite mode)
    {
        ForgetQueueResponseSetup(packet);var read=readingQueue;
        if(mode==StreamModeWrite.ToClient&&read?.ReadCompleted==true&&read.Reads==1&&ReferenceEquals(read.Source.Station,tile))
        {var hold=read.Source.RetainResponseResources();if(hold!=null)queueResponses.Add(packet,new QueueResponse{Read=read,Packet=packet,Resources=hold,Clock=read.Source.Station.lastTickTime,First=GameTimer.Instance.ticks});}
    }
    internal static void ObserveQueueResponseSetup(NetPackageTileEntity packet,bool run)
    {
        if(!queueResponses.TryGetValue(packet,out var value))return;
        value.Last=GameTimer.Instance.ticks;var source=value.Read.Source;
        try
        {
            if(!run||!ReferenceEquals(readingQueue,value.Read)||!source.Current()||!value.Read.ReadCompleted||value.Read.Reads!=1||
                source.Nonce==Guid.Empty||packet.handle!=value.Read.Packet.handle||!packet.teWorldPos.Equals(source.Station.ToWorldPos())||
                packet.teBlockId!=source.World.GetBlock(packet.teWorldPos).type||packet.ms?.Length!=value.Read.PacketBody.Length||
                source.Station.lastTickTime!=value.Clock||value.First<value.Clock||value.Last<value.First)return;
            ulong elapsed=0;int offset=source.AfterOffset;
            lock(packet.ms)
            {
                long old=packet.ms.Position;
                try
                {
                    packet.ms.Position=0;
                    for(int index=0;index<value.Read.PacketBody.Length;index++)
                    {
                        int next=packet.ms.ReadByte();if(next<0)return;
                        if(index>=offset&&index<offset+8)elapsed|=(ulong)(byte)next<<(8*(index-offset));
                        else if(next!=value.Read.PacketBody[index])return;
                    }
                }
                finally{packet.ms.Position=old;}
            }
            value.Valid=elapsed>=value.First-value.Clock&&elapsed<=value.Last-value.Clock;
        }
        catch{value.Valid=false;}
    }
    // This exception to the old recipient hold is issued only by the actual
    // UQR read -> native response Setup. It is not an equal-body permission.
    // The existing client echo observer and separate accepted nonce ACK remain
    // mandatory; this method grants no effects or saved publication authority.
    internal static bool TrySelectQueueResponse(NetPackageTileEntity packet,ClientInfo peer,INetConnection connection,out bool allow,out object context)
    {
        allow=false;context=null;
        if(packet==null||!queueResponses.TryGetValue(packet,out var value)||!ReferenceEquals(value.Read.Source.Sender,peer))return false;
        var source=value.Read.Source;
        allow=value.Valid&&ReferenceEquals(readingQueue,value.Read)&&value.Read.ReadCompleted&&value.Read.Reads==1&&source.Current()&&
            packet.handle==value.Read.Packet.handle&&ReferenceEquals(peer.netConnection?[packet.Channel],connection)&&!connection.IsDisconnected();
        if(allow)context=value;
        return true;
    }
    internal static bool QueueResponseCurrent(object context)=>context is QueueResponse value&&value.Valid&&ReferenceEquals(readingQueue,value.Read)&&
        value.Read.ReadCompleted&&value.Read.Reads==1&&value.Read.Allocation is RebirthProductiveEarnedObservationBudget.Decoder decoder&&decoder.IsCurrent&&
        value.Read.Source.Current()&&value.Read.Source.Station.lastTickTime==value.Clock&&
        RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Read.Source.Station,value.Read.PacketBody,value.Read.Source.AfterOffset);
    internal static RebirthProductiveEarnedObservationBudget.Decoder QueueResponseWorkspace(object context)=>QueueResponseCurrent(context)?(RebirthProductiveEarnedObservationBudget.Decoder)((QueueResponse)context).Read.Allocation:null;
    internal static bool TryGetQueueResponseSource(object context,out World world,out TileEntityWorkstation station)
    {world=null;station=null;if(!QueueResponseCurrent(context))return false;var value=(QueueResponse)context;world=value.Read.Source.World;station=value.Read.Source.Station;return true;}
    internal static XElement QueueResponseHeader(object context)
    {
        if(!QueueResponseCurrent(context))return null;var source=((QueueResponse)context).Read.Source;
        return new XElement("uqrOriginalResponse",new XAttribute("version",1),new XAttribute("operation",source.Operation.ToString("N")),
            new XAttribute("nonce",source.Nonce.ToString("N")),new XAttribute("digest",source.Digest));
    }
    internal static bool CopyQueueResponseBody(object context,out byte[] body,out int offset)
    {
        body=null;offset=-1;if(!QueueResponseCurrent(context))return false;var value=(QueueResponse)context;
        body=(byte[])value.Read.PacketBody.Clone();offset=value.Read.Source.AfterOffset;return true;
    }
    internal static void ForgetQueueResponseSetup(NetPackage packet)
    {if(packet is NetPackageTileEntity native&&queueResponses.TryGetValue(native,out var value)){queueResponses.Remove(native);value.Resources?.Dispose();value.Resources=null;}}
}
