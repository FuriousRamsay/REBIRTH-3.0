using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    internal sealed class NativeEcho:IDisposable
    {
        internal Context Original;internal bool After,Terminal,Pending,Tools,Viewer,ViewerPlan,Valid;internal Guid Group=Guid.NewGuid();
        internal string PurposeDigest;
        internal ulong Clock,First,Last;internal int Offset;internal byte[] Native;
        internal IDisposable Retention;internal bool Transferred;
        internal bool Current()=>Retention!=null&&Valid&&Original?.Current()==true&&(Viewer?ViewerToolSourceCurrent(Original)&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Station,Native,Offset):Pending?Original.NativePendingSaved&&NativePendingCurrent(Original):Tools?Original.Tools?.Saved==true&&NativeToolsCurrent(Original):Terminal?Original.NativeOutputSaved&&NativeOutputCurrent(Original):After?Original.AfterPublished&&Original.AfterCurrent():Original.BeforePublished&&Original.BeforeCurrent());
        public void Dispose(){var held=Retention;Retention=null;Native=null;Original=null;held?.Dispose();}
    }
    [ThreadStatic]private static NativeEcho selectingEcho;
    private static readonly ConditionalWeakTable<NetPackageTileEntity,NativeEcho> nativeEchoes=new ConditionalWeakTable<NetPackageTileEntity,NativeEcho>();
    internal static void SendOriginalNativeEcho(EntityPlayer actor,ClientInfo peer,Guid operation,Guid nonce,string digest)
    {
        if(selectingEcho!=null||!pending.TryGetValue(operation,out var source)||source.Nonce!=nonce||!ReferenceEquals(source.Actor,actor)||
            !ReferenceEquals(source.Sender,peer)||Digest(source.Image)!=digest||!source.Current())return;
        bool viewer=source.ViewerToolsOnly&&ViewerToolSourceCurrent(source);
        bool pendingNative=!viewer&&source.NativePendingAccepted&&source.NativePendingSaved&&NativePendingCurrent(source);
        bool toolsNative=!pendingNative&&source.Tools?.Saved==true&&NativeToolsCurrent(source);
        bool terminal=!viewer&&!pendingNative&&!toolsNative&&source.NativeOutputAccepted&&source.NativeOutputSaved&&NativeOutputCurrent(source);
        bool after=!viewer&&!pendingNative&&!toolsNative&&!terminal&&TryRetainAfterPublication(source);if(!viewer&&!pendingNative&&!toolsNative&&!terminal&&!after&&!TryRetainBeforePublication(source))return;
        if(!viewer&&!pendingNative&&!toolsNative&&!terminal&&(after?!source.AfterCurrent():!source.BeforeCurrent()))return;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*source.Image.Length+65536),out var retained))return;
        NativeEcho token=null;
        try{
        var frame=RebirthProductiveEarnedRowProof.Decode(source.Image);var paid=EffectiveNativeOutput(source);
        token=new NativeEcho{Original=source,After=after,Terminal=terminal,Pending=pendingNative,Tools=toolsNative||viewer&&source.Tools?.Saved==true,Viewer=viewer,ViewerPlan=viewer&&source.Tools?.Record!=null,
            PurposeDigest=toolsNative||viewer&&source.Tools?.Record!=null?Digest(source.Tools.Record):null,
            Native=viewer?paid.Native:pendingNative?source.NativePending.After:toolsNative?source.Tools.Plan.After:terminal?paid.Native:after?frame.AfterNative:frame.BeforeNative,
            Offset=viewer?paid.Offset:pendingNative?source.NativePending.AfterOffset:toolsNative?source.Tools.Plan.AfterOffset:terminal?paid.Offset:after?frame.AfterElapsedOffset:frame.BeforeElapsedOffset,Retention=retained};retained=null;
        var connection=peer.netConnection?[0];if(!RebirthLogisticsOriginalTransport.CanSendNative(connection))return;
        selectingEcho=token;
        try{SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(source.Station,StreamModeWrite.ToClient),_attachedToEntityId:actor.entityId);}
        finally{selectingEcho=null;}
        }finally{retained?.Dispose();if(token?.Transferred!=true)token?.Dispose();}
    }
    internal static void BeginOriginalEchoSetup(NetPackageTileEntity packet,TileEntity tile,StreamModeWrite mode)
    {
        if(nativeEchoes.TryGetValue(packet,out var prior)){nativeEchoes.Remove(packet);if(!prior.Transferred)prior.Dispose();}var token=selectingEcho;
        if(token==null||mode!=StreamModeWrite.ToClient||!ReferenceEquals(token.Original.Station,tile))return;
        token.Clock=token.Original.Station.lastTickTime;token.First=GameTimer.Instance.ticks;nativeEchoes.Add(packet,token);
    }
    internal static void ObserveOriginalEchoSetup(NetPackageTileEntity packet,bool run)
    {
        if(!nativeEchoes.TryGetValue(packet,out var token))return;
        token.Last=GameTimer.Instance.ticks;
        try
        {
            if(!run||!ReferenceEquals(selectingEcho,token)||!token.Original.Current()||token.Original.Station.lastTickTime!=token.Clock||
                token.First<token.Clock||token.Last<token.First||packet.ms?.Length!=token.Native.Length||
                !packet.teWorldPos.Equals(token.Original.Station.ToWorldPos())||packet.teBlockId!=token.Original.Station.blockValue.type)return;
            var raw=new byte[token.Native.Length];lock(packet.ms){long old=packet.ms.Position;try{packet.ms.Position=0;int count=0;while(count<raw.Length){int read=packet.ms.Read(raw,count,raw.Length-count);if(read<1)return;count+=read;}}finally{packet.ms.Position=old;}}
            if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(raw,token.Offset,token.Native,token.Offset))return;
            ulong elapsed=BitConverter.ToUInt64(raw,token.Offset);token.Valid=elapsed>=token.First-token.Clock&&elapsed<=token.Last-token.Clock;
        }
        catch{token.Valid=false;}
    }
    internal static bool TrySelectOriginalEcho(NetPackageTileEntity packet,ClientInfo peer,INetConnection connection,out NativeEcho token)
    {
        if(!nativeEchoes.TryGetValue(packet,out token))return false;
        nativeEchoes.Remove(packet);token.Transferred=true;
        return true; // A known but invalid original is held by its wrapper.
    }
}
