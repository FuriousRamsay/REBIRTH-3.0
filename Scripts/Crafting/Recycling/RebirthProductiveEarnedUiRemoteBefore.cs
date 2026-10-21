using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// UBF1 preupload admission. A request never changes the host TE. Only normal
// native owner Process/read plus actual saved native publication issues proof.
internal static class RebirthProductiveEarnedUiRemoteBefore
{
    private sealed class Original
    {
        internal EntityPlayer Actor;internal ClientInfo Peer;internal World World;internal TileEntityWorkstation Station;
        internal Guid Operation,Birth,Nonce;internal string Owner,Creation,WorldKey,Save,Path,Hash,UiHash,Purpose;
        internal int Deaths,Index,Offset;internal ulong Clock,AcceptedElapsed;
        internal byte[] Image,Before,After,SavedReceipt;internal IDisposable Retention;
        internal RebirthProductiveEarnedUiQueueOwner.Witness Earlier;
        internal RebirthProductiveEarnedObservationBudget Memory;
        internal RebirthProductiveEarnedObservationBudget.Observation Allocation;
        internal RebirthRemoteResourceNativePublication.Watch Watch;
        internal Publication Saved;
        internal bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&
            Actor.Died==Deaths&&RebirthProductiveEarnedArtifact.RemoteAuthority(Actor,Peer,Station,Creation)&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Peer,out var identity)&&identity.StorageKey==Owner&&
            WorldKey==RebirthProductiveEarnedArtifact.WorldIdentity(World)&&Save==System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var birth)&&birth==Birth.ToString("N")&&
            Earlier.SourceCurrent()&&RebirthProductiveEarnedArtifact.FileMatches(Path,Image);
        internal bool NativeCurrent()
        {using(var callback=Memory?.TryEnterCallbacks())return callback!=null&&Current()&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,After,Offset);}
    }
    internal sealed class Publication
    {
        private readonly Original original;
        internal readonly RebirthStationSnapshotEvidence.Publication Native;
        internal readonly long TileOffset;internal readonly int TileLength;internal readonly string TileDigest;
        private Publication(Original source,RebirthStationSnapshotEvidence.Publication native,long offset,int length,string digest)
        {original=source;Native=native;TileOffset=offset;TileLength=length;TileDigest=digest;}
        internal ulong AcceptedElapsed=>original.AcceptedElapsed;
        internal bool Matches(EntityPlayer actor,ClientInfo peer,TileEntityWorkstation station,Guid operation,string uiHash,
            RebirthProductiveEarnedUiQueueOwner.Witness earlier,byte[] body,int offset,RebirthProductiveEarnedObservationBudget.Decoder decoder)
            =>decoder?.IsCurrent==true&&ReferenceEquals(original.Saved,this)&&original.Current()&&
                ReferenceEquals(original.Actor,actor)&&ReferenceEquals(original.Peer,peer)&&ReferenceEquals(original.Station,station)&&original.Operation==operation&&
                original.UiHash==uiHash&&ReferenceEquals(original.Earlier,earlier)&&original.Offset==offset&&
                RebirthProductiveEarnedNativeBody.SameExceptElapsed(original.After,offset,body,offset)&&
                RebirthProductiveEarnedArtifact.FileMatches(original.Path+".saved",original.SavedReceipt)&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(station,body,offset);
        internal static bool TryIssue(object candidate,out Publication publication)
        {
            publication=null;
            if(candidate is not Original source||!originals.TryGetValue(source.Operation,out var retained)||!ReferenceEquals(source,retained)||source.Watch==null||!source.NativeCurrent()||!source.Watch.TryReadPublishedTileDescriptor(out var native,out long offset,out int length,out string digest)||!source.NativeCurrent())return false;
            var receipt=new XElement("uiNativeBeforePublished",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("nonce",source.Nonce.ToString("N")),
                new XAttribute("request",source.Hash),new XAttribute("uio",source.UiHash),new XAttribute("body",Hash(source.After)),new XAttribute("elapsed",source.AcceptedElapsed),
                new XAttribute("path",native.Path),new XAttribute("payload",native.PayloadDigest),new XAttribute("offset",offset),new XAttribute("length",length),new XAttribute("tile",digest));
            byte[] bytes=Encoding.UTF8.GetBytes(receipt.ToString(SaveOptions.DisableFormatting));
            if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(source.Path+".saved",bytes))return false;
            var result=new Publication(source,native,offset,length,digest);source.SavedReceipt=bytes;source.Saved=result;
            source.Watch.Dispose();source.Watch=null;source.Allocation.Dispose();source.Allocation=null;source.Memory.Dispose();
            publication=result;return true;
        }
    }
    private sealed class Read
    {internal Original Source;internal NetPackageTileEntity Packet;internal byte[] Body;internal int Reads;internal bool Complete;internal IDisposable Allocation;}
    private static readonly Dictionary<Guid,Original> originals=new Dictionary<Guid,Original>();
    internal static bool BlocksStation(TileEntityWorkstation station)=>originals.Values.Any(value=>ReferenceEquals(value.Station,station)&&ReferenceEquals(value.World,GameManager.Instance?.World));
    [ThreadStatic]private static Read reading;
    internal static bool Register(EntityPlayer actor,ClientInfo peer,TileEntityWorkstation station,string creation,Guid operation,byte[] image,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        try
        {
            if(!RebirthProductiveEarnedUiOriginal.Enabled||!RebirthProductiveEarnedArtifact.RemoteAuthority(actor,peer,station,creation)||
                RebirthProductiveEarnedUiDeferredTransport.HostReadyHold(station)||RebirthProductiveEarnedUiOwnerFile.HoldsLocalOwner(station)||RebirthRemoteResourceSourceCustody.BlocksStation(station)||
                RebirthRemoteResourcePlacedStationClientCustody.Blocks(station)||RebirthRemoteResourcePlacedStationCustody.Blocks(station)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||image==null||image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
            string hash=Hash(image);
            if(originals.TryGetValue(operation,out var existing))
            {if(!existing.Current()||existing.Hash!=hash||!existing.Image.SequenceEqual(image)||!ReferenceEquals(existing.Actor,actor)||!ReferenceEquals(existing.Peer,peer))return false;reply=Reply(existing);return true;}
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||originals.Count>=4)return false;var node=XElement.Parse(Encoding.UTF8.GetString(image),LoadOptions.None);
                if(node.Name!="uiNativeBefore"||(int)node.Attribute("version")!=1||(string)node.Attribute("operation")!=operation.ToString("N")||
                    !Guid.TryParseExact((string)node.Attribute("birth"),"N",out var birth)||birth==Guid.Empty||
                    !RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,station,out var actualBirth)||actualBirth!=birth.ToString("N"))return false;
                int index=(int)node.Attribute("index"),offset=(int)node.Attribute("offset");string purpose=(string)node.Element("purpose"),uiHash=(string)node.Attribute("uio");
                byte[] after=Convert.FromBase64String((string)node.Element("after"));
                byte[] uiOriginal=Convert.FromBase64String((string)node.Element("uiOriginal"));
                if(uiHash?.Length!=64||index!=station.Queue.Length-1||!RebirthProductiveEarnedUiQueueOwner.MatchesPurpose(station,index,station.Queue[index],purpose,out var earlier)||
                    !RebirthProductiveEarnedUiOriginal.ValidateRemoteBeforeImage(uiOriginal,actor,station,operation,birth,identity.StorageKey,creation,index,after,offset)||
                    !RebirthProductiveEarnedNativeBody.TryRead(after,offset,out var target,out _)||target.Queue.Length!=station.Queue.Length||
                    !RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out var before,out _)||
                    RebirthProductiveEarnedCoordinator.TryNativeMarker(station,out _))return false;
                for(int i=0;i<station.Queue.Length;i++)
                {
                    var old=station.Queue[i];var row=target.Queue[i];
                    if(old==null||row==null||old.Multiplier!=row.Multiplier||old.RepairItem!=null||row.RepairItem!=null||old.AmountToRepair!=0||row.AmountToRepair!=0||
                        RebirthProductiveEarnedUiQueueOwner.NativeBinding(old)!=RebirthProductiveEarnedUiQueueOwner.NativeBinding(row))return false;
                    if(old.Recipe!=null&&old.Multiplier>0&&(!RebirthProductiveEarnedUiQueueOwner.TryObserveQueueOwner(station,i,old,out var owner)||
                        RebirthProductiveEarnedUiQueueOwner.Binding(old)!=owner.RecipeBinding))return false;
                }
                if(!RebirthProductiveEarnedUiRemote.AllowsBeforeSource(station,actor,earlier)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*image.Length+8L*(before.Length+after.Length)+65536),out var retention))return false;
                var value=new Original{Actor=actor,Peer=peer,World=actor.world,Station=station,Operation=operation,Birth=birth,Nonce=Guid.NewGuid(),Owner=identity.StorageKey,
                    Creation=creation,Deaths=actor.Died,WorldKey=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world),Save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()),
                    Path=System.IO.Path.Combine(GameIO.GetSaveGameDir(),"RebirthProductiveEarnedUi","remote",identity.StorageKey,operation.ToString("N"),"before.ubf"),
                    Image=(byte[])image.Clone(),Hash=hash,UiHash=uiHash,Purpose=purpose,Index=index,Offset=offset,Before=before,After=after,Clock=station.lastTickTime,Earlier=earlier,Retention=retention};
                if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(value.Path,value.Image)){retention.Dispose();return false;}
                // Allocation identity is separate from the later UIO1 original;
                // this fresh budget key grants no operation or native authority.
                value.Memory=RebirthProductiveEarnedObservationBudget.TryOpenUiFrame(actor.world,birth,Guid.NewGuid(),image);
                if(value.Memory==null){retention.Dispose();return false;}
                originals.Add(operation,value);reply=Reply(value);return true;
            }
        }
        catch{return false;}
    }
    private static RebirthProductiveEarnedWire.Reply Reply(Original value)=>new RebirthProductiveEarnedWire.Reply{Operation=value.Operation,Birth=value.Birth,Nonce=value.Nonce,
        World=value.WorldKey,Owner=value.Owner,Creation=value.Creation,Deaths=value.Deaths,Position=value.Station.ToWorldPos(),Digest=value.Hash,QueueOwner=value.Purpose,
        State=value.Saved!=null?RebirthProductiveEarnedWire.Status.UiBeforeSaved:RebirthProductiveEarnedWire.Status.UiBeforeHeader};
    internal static bool Query(EntityPlayer actor,ClientInfo peer,TileEntityWorkstation station,string creation,Guid operation,Guid nonce,string hash,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;if(!originals.TryGetValue(operation,out var value)||!value.Current()||value.Nonce!=nonce||value.Hash!=hash||
            !ReferenceEquals(value.Actor,actor)||!ReferenceEquals(value.Peer,peer)||!ReferenceEquals(value.Station,station)||value.Creation!=creation)return false;
        try{if(value.Saved==null)Publication.TryIssue(value,out _);reply=Reply(value);return true;}catch{return false;}
    }
    internal static bool TryGetSaved(EntityPlayer actor,ClientInfo peer,TileEntityWorkstation station,Guid operation,string uiHash,
        RebirthProductiveEarnedUiQueueOwner.Witness earlier,byte[] body,int offset,RebirthProductiveEarnedObservationBudget.Decoder decoder,out Publication proof)
    {proof=null;if(!originals.TryGetValue(operation,out var value)||value.Saved==null||!value.Saved.Matches(actor,peer,station,operation,uiHash,earlier,body,offset,decoder))return false;proof=value.Saved;return true;}
    internal static bool EnterNativeUpload(NetPackageTileEntity packet,World world,bool run,out object state)
    {
        state=null;if(!run||world?.IsRemote()!=false)return true;
        try
        {
            var found=originals.Values.Where(value=>value.Saved==null&&ReferenceEquals(value.World,world)&&value.Station.ToWorldPos().Equals(packet.teWorldPos)).ToArray();
            if(found.Length==0)return true;if(found.Length!=1||reading!=null)return false;var source=found[0];
            if(!source.Current()||!ReferenceEquals(source.Peer,packet.Sender)||packet.ms==null||packet.ms.Length<1||packet.ms.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||
                packet.teBlockId!=world.GetBlock(packet.teWorldPos).type)return false;
            var allocation=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();if(allocation==null)return false;
            try
            {
                var raw=new byte[(int)packet.ms.Length];lock(packet.ms){long prior=packet.ms.Position;try{packet.ms.Position=0;int read=0;while(read<raw.Length){int n=packet.ms.Read(raw,read,raw.Length-read);if(n<1)return false;read+=n;}}finally{packet.ms.Position=prior;}}
                if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(raw,source.Offset,source.After,source.Offset)||source.Station.lastTickTime!=source.Clock||
                    !RebirthProductiveEarnedNativeBody.TryCaptureCurrent(source.Station,out var current,out int offset)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(current,offset,source.Before,offset)||BitConverter.ToUInt64(raw,source.Offset)>GameTimer.Instance.ticks)return false;
                reading=new Read{Source=source,Packet=packet,Body=raw,Allocation=allocation};state=reading;allocation=null;return true;
            }
            finally{allocation?.Dispose();}
        }
        catch{return false;}
    }
    internal static void EnterNativeRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {var value=reading;if(run&&value!=null&&mode==StreamModeRead.FromClient&&ReferenceEquals(station,value.Source.Station)&&ReferenceEquals(reader.BaseStream,value.Packet.ms))value.Reads++;}
    internal static void ObserveNativeRead(PooledBinaryReader reader,bool run)
    {var value=reading;if(run&&value!=null&&value.Reads==1&&ReferenceEquals(reader.BaseStream,value.Packet.ms)&&reader.BaseStream.Position==value.Packet.ms.Length)value.Complete=true;}
    internal static void ObserveNativeUpload(NetPackageTileEntity packet,World world,bool run,object state)
    {
        try
        {
            if(state is not Read value||!ReferenceEquals(reading,value)||!run||value.Reads!=1||!value.Complete||!ReferenceEquals(value.Packet,packet)||!value.Source.Current())return;
            value.Allocation?.Dispose();value.Allocation=null;var source=value.Source;
            using(var callback=source.Memory.TryEnterCallbacks())
            {if(callback==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,value.Body,source.Offset)||
                source.Station.lastTickTime!=GameTimer.Instance.ticks-BitConverter.ToUInt64(value.Body,source.Offset))return;}
            source.AcceptedElapsed=BitConverter.ToUInt64(value.Body,source.Offset);
            source.Allocation=source.Memory.TryObserveNative(source.After,source.Offset);if(source.Allocation==null)return;
            if(!RebirthRemoteResourceNativeBinding.TryResolve(world,RemoteResourceIdentity.Workstation(source.Station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,source.Birth.ToString("N"),out var binding))
            {source.Allocation.Dispose();source.Allocation=null;return;}
            RebirthRemoteResourceNativePublication.Install();source.Watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,source.NativeCurrent,source.Memory);
            if(source.Watch==null){source.Allocation.Dispose();source.Allocation=null;}
        }
        catch{if(state is Read failed&&failed.Source.Watch==null){failed.Source.Allocation?.Dispose();failed.Source.Allocation=null;} /* Retain the original unknown; never replay native upload. */}
    }
    internal static void EndNativeUpload(object state)
    {if(state is Read value){value.Allocation?.Dispose();value.Body=null;if(ReferenceEquals(reading,value))reading=null;}}
    internal static void ReleaseNativeAllocation(object state)
    {if(state is Read value){value.Allocation?.Dispose();value.Allocation=null;}}
    internal static void Release(Guid operation)
    {if(!originals.TryGetValue(operation,out var value))return;originals.Remove(operation);value.Watch?.Dispose();value.Allocation?.Dispose();value.Memory?.Dispose();value.Retention?.Dispose();}
    internal static void ReleaseWorld(){foreach(var operation in originals.Keys.ToArray())Release(operation);reading=null;}
    private static string Hash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
}


