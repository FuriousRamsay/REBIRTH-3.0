using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedRemoteClient
{
    private sealed class EchoRead
    {
        internal Original Original;internal NetPackageTileEntity Packet;internal byte[] Expected;
        internal int Offset,Reads;internal long Version;internal bool Returned;
    }
    [ThreadStatic]private static EchoRead receivingEcho;
    internal static bool AcceptNativeEcho(XElement header,Guid group,World world,INetConnection connection,byte[] native,GameManager callbacks)
    {
        NetPackageTileEntity packet=null;EchoRead read=null;
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            bool toolsNative=(string)header?.Attribute("phase")=="tools"||(string)header?.Attribute("phase")=="viewerToolsSaved";
            bool viewerBefore=(string)header?.Attribute("phase")=="viewerToolsBefore";
            bool viewerNative=viewerBefore||(string)header?.Attribute("phase")=="viewerTools"||(string)header?.Attribute("phase")=="viewerToolsSaved";
            if(receivingEcho!=null||header?.Name!="ordinaryActualEarnedEcho"||header.HasElements||header.Attributes().Count()!=(viewerNative?toolsNative||viewerBefore?17:16:toolsNative?9:8)||
                (int?)header.Attribute("version")!=1||(string)header.Attribute("group")!=group.ToString("N")||
                !Guid.TryParseExact((string)header.Attribute("operation"),"N",out var operation)||world?.IsRemote()!=true||
                !ReferenceEquals(world,GameManager.Instance?.World)||manager?.IsClient!=true||manager.IsServer||
                !ReferenceEquals(manager.connectionToServer?[0],connection)||!RebirthLogisticsOriginalTransport.CanSendNative(connection))return false;
            if(viewerNative&&BindViewerToolDescription(header,world)==null)return false;
            var matches=originals.Values.Where(value=>value.Frame.Operation==operation).ToArray();if(matches.Length!=1)return false;
            var original=matches[0];bool after=(string)header.Attribute("phase")=="after",terminal=(string)header.Attribute("phase")=="terminal",pendingNative=(string)header.Attribute("phase")=="pending";
            if(!viewerNative&&!after&&!terminal&&!pendingNative&&!toolsNative&&(string)header.Attribute("phase")!="before"||!original.Identity()||original.Nonce==Guid.Empty||
                (string)header.Attribute("nonce")!=original.Nonce.ToString("N")||(string)header.Attribute("birth")!=original.Frame.Birth.ToString("N")||
                (string)header.Attribute("digest")!=original.Digest||after&&(!original.Attempted||!original.Applied||!original.AfterSent)||terminal&&original.Output?.Returned!=true||pendingNative&&original.Pending?.Returned!=true)return false;
            if(toolsNative&&(original.Tools?.Record==null||(string)header.Attribute("purposeDigest")!=Hash(original.Tools.Record)||
                !MatchesSavedStageDescription(original,"nativeToolsOwnerUpload",original.Nonce)||!MatchesSavedStageDescription(original,"nativeToolsSyncAttempt",original.Nonce)))return false;
            if(viewerBefore&&(original.Tools?.Record==null||(string)header.Attribute("purposeDigest")!=Hash(original.Tools.Record)||
                original.Tools.SyncEntered||File.Exists(original.Path+".nativeToolsSyncAttempt")||File.Exists(original.Path+".nativeToolsSyncAttempt.tmp")))return false;
            bool toolsAfter=terminal&&original.Tools?.SyncReturned==true;
            byte[] expected=viewerNative&&!toolsNative?original.Output.Plan.Native:pendingNative?original.Pending.Plan.After:toolsNative||toolsAfter?original.Tools.Plan.After:terminal?original.Output.Plan.Native:after?original.Frame.AfterNative:original.Frame.BeforeNative;
            int offset=viewerNative&&!toolsNative?original.Output.Plan.Offset:pendingNative?original.Pending.Plan.AfterOffset:toolsNative||toolsAfter?original.Tools.Plan.AfterOffset:terminal?original.Output.Plan.Offset:after?original.Frame.AfterElapsedOffset:original.Frame.BeforeElapsedOffset;
            using(var allocation=original.Memory?.TryEnterCallbacks())
            {
                if(allocation==null)return false;
                packet=NetPackageManager.GetPackage<NetPackageTileEntity>();
                using(var stream=new MemoryStream(native,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
                {reader.SetBaseStream(stream);if(reader.ReadUInt16()!=packet.PackageId)return false;packet.read(reader);if(stream.Position!=stream.Length)return false;}
                if((int)header.Attribute("handle")!=packet.handle||!packet.teWorldPos.Equals(original.Station.ToWorldPos())||
                    packet.teBlockId!=original.Station.blockValue.type||packet.ms?.Length!=expected.Length)return false;
                var body=new byte[expected.Length];lock(packet.ms){long old=packet.ms.Position;try{packet.ms.Position=0;int count=0;while(count<body.Length){int n=packet.ms.Read(body,count,body.Length-count);if(n<1)return false;count+=n;}}finally{packet.ms.Position=old;}}
                if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(body,offset,expected,offset))return false;
            }
            read=new EchoRead{Original=original,Packet=packet,Expected=expected,Offset=offset};receivingEcho=read;
            packet.ProcessPackage(world,callbacks);
            if(!read.Returned||read.Reads!=1||!original.Identity()||original.Frame.RowIndex<0||original.Frame.RowIndex>=original.Station.Queue.Length)return false;
            if(viewerNative||terminal||pendingNative||toolsNative)
            {
                original.ReadFence=read.Version;original.HasReadFence=true;
                if(viewerNative&&original.Current())GameManager.Instance.SaveLocalPlayerData();
                if(toolsNative&&original.Current()&&Stage(original,"nativeToolsSavedEcho",original.Nonce))
                {original.Tools.SavedEcho=true;original.Tools.OwnerSaved=true;if(Stage(original,"nativeToolsSaved",original.Nonce))original.Tools.Saved=true;}
                return original.Current();
            }
            var row=original.Station.Queue[original.Frame.RowIndex];
            if(row?.Recipe==null||row.Multiplier!=original.Frame.Multiplier||RebirthProductiveEarnedUiQueueOwner.Binding(row)!=original.Frame.FullRecipeBinding||
                RebirthRecipeOutputs.DefinitionIdentity(row.Recipe)!=original.Frame.ExtrasDefinition)return false;
            original.Row=row;original.Recipe=row.Recipe;original.ReadFence=read.Version;original.HasReadFence=true;
            return original.Current();
        }
        catch{return false;}
        finally{receivingEcho=null;if(packet!=null)NetPackageManager.FreePackage(packet);}
    }
    internal static void EnterOriginalEchoRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run,long version)
    {
        var read=receivingEcho;
        if(run&&mode==StreamModeRead.FromServer&&read!=null&&read.Original.Identity()&&ReferenceEquals(read.Original.Station,station)&&ReferenceEquals(reader.BaseStream,read.Packet.ms))
        {read.Reads++;read.Version=version;}
    }
    internal static void ObserveOriginalEchoRead(PooledBinaryReader reader,bool run)
    {
        var read=receivingEcho;if(!run||read==null||read.Reads!=1||!read.Original.Identity()||!ReferenceEquals(reader.BaseStream,read.Packet.ms)||reader.BaseStream.Position!=read.Packet.ms.Length)return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            read.Returned=decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(read.Original.Station,read.Expected,read.Offset);
    }
}
