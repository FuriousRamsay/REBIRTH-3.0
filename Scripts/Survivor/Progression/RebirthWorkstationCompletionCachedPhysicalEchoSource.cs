using System;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal static string CachedOriginalHash(CachedCustody source)=>source?.Image==null?null:HistoricalHash(source.Image);
    // Observation only during the SAME actual native Process broadcast. The
    // later saved physical publication is a separate mandatory authority.
    internal sealed class CachedPhysicalEchoSource:IDisposable
    {
        private CachedPhysicalRead read;
        private CachedPhysicalEchoSource(CachedPhysicalRead original){read=original;}
        internal CachedCustody Root=>read?.Action?.Issue?.Cache;
        internal RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication Previous=>Root?.OwnerTerminal;
        internal World World=>Root?.World;
        internal TileEntityWorkstation Station=>Root?.Station;
        internal ClientInfo Creator=>read?.Action?.Sender;
        internal byte[] Body=>read?.Body;
        internal int Offset=>read?.Offset??-1;
        internal Guid Operation=>Root?.Operation??Guid.Empty;
        internal int Phase=>read?.Phase??-1;
        internal bool CanEchoRecipient=>Phase==3&&Previous?.HasSavedCachedIdentity(Root)==true;
        internal bool Current()=>read!=null&&ReferenceEquals(executingCachedPhysical,read)&&(read.Phase==2||read.Phase==3||read.Phase==4)&&read.Reads==1&&read.ReadCompleted&&
            read.Action.Issue.CurrentIdentity()&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(World,Station,out var fence)&&fence==read.ReadFence;
        internal static CachedPhysicalEchoSource FromActualProcess(TileEntity tile)
        {
            var actual=executingCachedPhysical;
            if(actual==null||(actual.Phase!=2&&actual.Phase!=3&&actual.Phase!=4)||!actual.ReadCompleted||!ReferenceEquals(actual.Action.Station,tile))return null;
            if(actual.Echo==null)actual.Echo=new CachedPhysicalEchoSource(actual);
            return actual.Echo.Current()?actual.Echo:null;
        }
        public void Dispose(){read=null;}
    }
}
