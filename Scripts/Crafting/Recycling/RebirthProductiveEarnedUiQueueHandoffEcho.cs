using System;
using System.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    private sealed class QueueEcho
    {internal QueueHandoff Source;internal NetPackageTileEntity Packet;internal int Reads;internal bool ReadCompleted,Selected;internal IDisposable Allocation;internal byte[] Body;}
    [ThreadStatic]private static QueueEcho readingQueueEcho;
    internal static bool IsPendingQueueHandoffNative(NetPackage packet)=>packet is NetPackageTileEntity native&&handoffs.Values.Any(value=>value.Submitted&&
        ReferenceEquals(value.Binding.World,GameManager.Instance?.World)&&value.Binding.Position.Equals(native.teWorldPos));
    internal static void EnterQueueEcho(NetPackageTileEntity packet,World world,bool run,out object state)
    {
        state=null;if(!run||world?.IsRemote()!=true||readingQueueEcho!=null)return;
        try
        {
            var found=handoffs.Values.Where(value=>value.Submitted&&ReferenceEquals(value.Binding.World,world)&&value.Binding.Position.Equals(packet.teWorldPos)).ToArray();
            if(found.Length!=1)return;var value=found[0];
            bool selected=RebirthProductiveEarnedUiDeferredTransport.IsActualQueueResponsePacket(packet,world);
            if(!value.Current()||!HandoffUiCurrent(value)||packet.teBlockId!=value.Binding.BlockType||packet.ms==null||packet.ms.Length<1||
                packet.ms.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||
                !selected&&(!RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection)||!ReferenceEquals(connection,value.Connection)))return;
            // The selected read must allow its own queue-owner importer to use
            // the shared decoder before NotifyListeners. Charge this bounded
            // byte copy separately; it grants no additional source authority.
            IDisposable allocation;
            if(selected){if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*packet.ms.Length+65536),out allocation))return;}
            else allocation=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();
            if(allocation==null)return;
            try
            {
                byte[] body=new byte[(int)packet.ms.Length];lock(packet.ms)
                {long prior=packet.ms.Position;try{packet.ms.Position=0;int read=0;while(read<body.Length){int count=packet.ms.Read(body,read,body.Length-read);if(count<1)return;read+=count;}}finally{packet.ms.Position=prior;}}
                if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Binding.Station,value.After,value.AfterOffset)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(body,value.AfterOffset,value.After,value.AfterOffset)||BitConverter.ToUInt64(body,value.AfterOffset)>GameTimer.Instance.ticks)return;
                readingQueueEcho=new QueueEcho{Source=value,Packet=packet,Allocation=allocation,Body=body,Selected=selected};state=readingQueueEcho;allocation=null;
            }
            finally{allocation?.Dispose();}
        }
        catch{}
    }
    internal static void EnterQueueEchoRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {var value=readingQueueEcho;if(run&&value!=null&&mode==StreamModeRead.FromServer&&ReferenceEquals(station,value.Source.Binding.Station)&&ReferenceEquals(reader.BaseStream,value.Packet.ms))value.Reads++;}
    internal static void ObserveQueueEchoRead(PooledBinaryReader reader,bool run)
    {var value=readingQueueEcho;if(run&&value!=null&&value.Reads==1&&ReferenceEquals(reader.BaseStream,value.Packet.ms)&&reader.BaseStream.Position==value.Packet.ms.Length)value.ReadCompleted=true;}
    internal static void ObserveQueueEcho(NetPackageTileEntity packet,World world,bool run,object state)
    {
        try
        {
            if(state is not QueueEcho value||!ReferenceEquals(readingQueueEcho,value)||!run||!value.ReadCompleted||value.Reads!=1||!ReferenceEquals(value.Packet,packet))return;
            if(value.Selected&&!RebirthProductiveEarnedUiDeferredTransport.IsSuccessfulQueueResponseRead(packet,world))return;
            value.Allocation?.Dispose();value.Allocation=null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!value.Source.Current()||!HandoffUiCurrent(value.Source)||
                    !RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Source.Binding.Station,value.Body,value.Source.AfterOffset)||
                    value.Source.Binding.Station.lastTickTime!=GameTimer.Instance.ticks-BitConverter.ToUInt64(value.Body,value.Source.AfterOffset))return;
                // This actual same-connection native reconstruction renews only
                // the local clock lineage. Host nonce ACK remains mandatory.
                value.Source.Clock=value.Source.Binding.Station.lastTickTime;value.Source.ResponseObserved=true;
            }
        }
        catch{}
    }
    internal static void EndQueueEcho(object state)
    {if(state is QueueEcho value){value.Allocation?.Dispose();value.Body=null;if(ReferenceEquals(readingQueueEcho,value))readingQueueEcho=null;}}
    internal static void ReleaseQueueEchoAllocation(object state)
    {if(state is QueueEcho value){value.Allocation?.Dispose();value.Allocation=null;}}
}
