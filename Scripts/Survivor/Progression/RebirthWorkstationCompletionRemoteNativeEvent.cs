using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal enum RemoteNativePhase:byte {ReceiptAccepted=1,PhysicalAccepted=2,QueueAccepted=3,CachedSpaceTransfer=4,CachedBoundary=5,CachedUnusedClose=6}
    internal sealed class RemoteNativeEvent
    {
        internal readonly NativeHostOriginal Source;
        internal readonly XElement Contribution;
        internal readonly byte[] Body;internal readonly int Offset;
        internal readonly ClientInfo Sender;internal readonly EntityPlayer Viewer;
        internal readonly RebirthProductiveEarnedUiDeferredTransport.HostContribution AcceptedContribution;
        internal readonly NativeViewerEffectAdmission SameOwnerEffect;
        internal readonly RebirthProductiveEarnedUiDeferredTransport.CachedBoundaryAdmission CachedBoundary;
        internal readonly RebirthWorkstationCompletionOriginal ReportedOriginal;
        internal readonly RemoteNativePhase Phase;
        internal readonly Guid Dispatch;
        private RemoteNativeEvent(NativeHostOriginal source,XElement contribution,byte[] body,int offset,
            RebirthProductiveEarnedUiDeferredTransport.HostContribution accepted,NativeViewerEffectAdmission effect,RemoteNativePhase phase,Guid dispatch,
            RebirthProductiveEarnedUiDeferredTransport.CachedBoundaryAdmission boundary,RebirthWorkstationCompletionOriginal original)
        {Source=source;Contribution=contribution;Body=body;Offset=offset;AcceptedContribution=accepted;SameOwnerEffect=effect;CachedBoundary=boundary;ReportedOriginal=original;Phase=phase;
            Dispatch=dispatch;Sender=source.Sender;Viewer=source.Viewer;}
        internal bool IsActualAcceptedRead()=>ReferenceEquals(acceptingRemoteNative,this)&&Source.CurrentViewer()&&
            ReferenceEquals(executingRemoteNative?.Event,this)&&executingRemoteNative.ReadCompleted&&executingRemoteNative.ProcessCompleted&&executingRemoteNative.Reads==1;
        internal static RemoteNativeEvent Admit(NativeHostOriginal source,XElement contribution,byte[] body,int offset,
            RebirthProductiveEarnedUiDeferredTransport.HostContribution accepted,NativeViewerEffectAdmission effect,RemoteNativePhase phase,Guid dispatch,
            RebirthProductiveEarnedUiDeferredTransport.CachedBoundaryAdmission boundary=null,RebirthWorkstationCompletionOriginal original=null)
        {
            // This factory is callable only inside the authenticated envelope
            // producer, after its complete read-only admission. A DTO parsed
            // elsewhere cannot turn a current equal body into a native event.
            if(!ReferenceEquals(admittingRemoteNative,source)||source?.CurrentViewer()!=true||
                (boundary!=null?accepted!=null||effect!=null||phase!=RemoteNativePhase.CachedBoundary||!boundary.Current():accepted!=null?effect!=null:effect==null||!effect.CurrentSavedSource())||dispatch==Guid.Empty)
                throw new InvalidDataException();
            if(phase==RemoteNativePhase.PhysicalAccepted||phase==RemoteNativePhase.QueueAccepted)
            {
                if(original==null||original.Operation!=source.Operation.ToString("N")||original.WorldGuid!=source.WorldGuid||
                    original.OwnerKey!=source.EarlierOwner.OriginalOwnerKey||original.Creation!=source.EarlierOwner.Creation||
                    original.ViewerKey!=source.ViewerKey||original.ViewerCreation!=source.ViewerCreation||
                    !XNode.DeepEquals(original.Write().Element("delivery")?.Elements().SingleOrDefault(),contribution.Element("physicalDelivery")?.Elements().SingleOrDefault()))throw new InvalidDataException();
            }
            return new RemoteNativeEvent(source,contribution,body,offset,accepted,effect,phase,dispatch,boundary,original);
        }
    }
    private sealed class RemoteNativeRead
    {internal RemoteNativeEvent Event;internal NetPackageTileEntity Packet;internal int Reads;internal bool ReadCompleted,ProcessCompleted;internal PooledBinaryReader Reader;}
    [ThreadStatic]private static NativeHostOriginal admittingRemoteNative;
    [ThreadStatic]private static RemoteNativeRead executingRemoteNative;
    [ThreadStatic]private static RemoteNativeEvent acceptingRemoteNative;
    internal static void EnterRemoteNativeRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {
        EnterCachedPhysicalRead(station,reader,mode,run);
        EnterCachedUnusedRead(station,reader,mode,run);
        var read=executingRemoteNative;if(read==null)return;
        if(!run||mode!=StreamModeRead.FromClient||!ReferenceEquals(reader.BaseStream,read.Packet.ms)||!ReferenceEquals(station,read.Event.Source.Station)||
            !read.Event.Source.CurrentViewer()||++read.Reads!=1)throw new InvalidDataException();
        read.Reader=reader;
    }
    internal static void ObserveRemoteNativeRead(PooledBinaryReader reader,bool run)
    {
        ObserveCachedPhysicalRead(reader,run);
        ObserveCachedUnusedRead(reader,run);
        var read=executingRemoteNative;if(read==null||!ReferenceEquals(reader,read.Reader))return;
        if(!run||read.Reads!=1||!ReferenceEquals(reader.BaseStream,read.Packet.ms)||reader.BaseStream.Position!=reader.BaseStream.Length||!read.Event.Source.CurrentViewer())return;
        using(var callbacks=read.Event.Source.Memory.TryEnterCallbacks())
        {
            if(callbacks==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(read.Event.Source.Station,read.Event.Body,read.Event.Offset))return;
        }
        read.ReadCompleted=true;
    }
    private static void PublishRemoteNativeProcess(RemoteNativeRead read)
    {
        if(!ReferenceEquals(read,executingRemoteNative)||!read.ReadCompleted||read.Reads!=1||!read.Event.Source.CurrentViewer())return;
        using(var callbacks=read.Event.Source.Memory.TryEnterCallbacks())
            if(callbacks==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(read.Event.Source.Station,read.Event.Body,read.Event.Offset))return;
        read.ProcessCompleted=true;acceptingRemoteNative=read.Event;
        try{read.Event.Source.ObserveAcceptedAdmissionFence(read.Event);if(read.Event.CachedBoundary!=null)
            {RebirthProductiveEarnedUiDeferredTransport.ObserveCachedBoundaryReceipt(read.Event);ObserveRemoteCachedBoundaryAccepted(read.Event);}
            else RebirthProductiveEarnedUiDeferredTransport.ObserveRemoteNativeEventAccepted(read.Event);
            read.Event.Source.ObserveAcceptedCachedSpace(read.Event);ObserveRemoteCachedCustodySpace(read.Event);ObserveRemoteCachedPhysicalAccepted(read.Event);}
        finally{acceptingRemoteNative=null;}
    }
}
