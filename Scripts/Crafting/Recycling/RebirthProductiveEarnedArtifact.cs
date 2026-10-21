using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

// Immutable row proof artifacts live beside the native save. Publication is
// preparation ONLY: no native timer/tool/marker/output is changed by this file.
// Called only by the currently inactive earned coordinator/native callback path.
internal static partial class RebirthProductiveEarnedArtifact
{
    internal sealed partial class Prepared
    {
        private readonly World world;
        private readonly TileEntityWorkstation station;
        private readonly string save, path, worldIdentity;
        private readonly byte[] image;
        private readonly Guid reboundNativeCustody;
        internal RebirthProductiveEarnedSavedNativeSource.Evidence LoadedSource;
        internal Func<bool> observedRemoteIdentity;
        internal readonly Guid Birth, Operation;
        internal Guid CustodyBirth=>LoadedSource?.CustodyBirth??(reboundNativeCustody==Guid.Empty?Birth:reboundNativeCustody);
        internal Prepared(World world,TileEntityWorkstation station,string save,string path,byte[] bytes,Guid birth,Guid operation,Guid reboundCustody=default)
        {this.world=world;this.station=station;this.save=save;this.path=path;image=(byte[])bytes.Clone();Birth=birth;Operation=operation;worldIdentity=WorldIdentity(world);reboundNativeCustody=reboundCustody;}
        internal byte[] CopyImage()=>(byte[])image.Clone();
        // Immutable identity only, including after marker retirement. This
        // digest never establishes Current or grants a native effect.
        internal byte[] OriginalDigest(){using(var sha=SHA256.Create())return sha.ComputeHash(image);}
        internal int ImageLength=>image.Length;
        internal string OriginalPath=>path;
        internal RebirthProductiveEarnedRowProof.Frame ReadFrame()
        {if(!Current())throw new InvalidDataException("Original earned artifact is not current.");return RebirthProductiveEarnedRowProof.Decode(image);}
        internal byte[] Marker(RebirthProductiveEarnedRowProof.Frame frame)
        {if(!Current())throw new InvalidDataException("Original earned artifact is not current.");return RebirthProductiveEarnedRowProof.Marker(frame,image);}
        internal bool MatchesMarker(byte[] marker)=>Current()&&RebirthProductiveEarnedRowProof.MatchesMarker(marker,image);
        internal bool IsTarget(TileEntityWorkstation target)=>ReferenceEquals(target,station);
        internal bool NativeRetirementIdentityCurrent()
        {
            try{return ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&!world.IsRemote()&&
                Path.GetFullPath(GameIO.GetSaveGameDir())==save&&WorldIdentity(world)==worldIdentity&&
                NativeCurrent(world,station,CustodyBirth)&&FileMatches(path,image);}
            catch{return false;}
        }
        internal bool TryPublishNativeTerminal(byte[] body,int elapsedOffset,RebirthStationSnapshotEvidence.Publication saved,
            long tileOffset,int tileLength,string tileDigest)
        {
            try
            {
                if(!Current()||saved==null||body==null||body.Length<1||body.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||
                    elapsedOffset<13||elapsedOffset>body.Length-8||tileOffset<0||tileLength<1||string.IsNullOrEmpty(tileDigest)||
                    !RebirthProductiveEarnedNativeBody.TryRead(body,elapsedOffset,out _,out _))return false;
                byte[] terminal;
                using(var buffer=new MemoryStream())
                using(var writer=new BinaryWriter(buffer))
                {
                    writer.Write(0x31544552); // RET1, historical native station terminal.
                    writer.Write(Birth.ToByteArray());writer.Write(Operation.ToByteArray());
                    writer.Write(image.Length);writer.Write(image);writer.Write(elapsedOffset);writer.Write(body.Length);writer.Write(body);
                    writer.Write(saved.Path);writer.Write(saved.PayloadDigest);writer.Write(saved.ChunkX);writer.Write(saved.ChunkZ);
                    writer.Write(tileOffset);writer.Write(tileLength);writer.Write(tileDigest);writer.Flush();
                    if(buffer.Length>2L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
                    terminal=buffer.ToArray();
                }
                string destination=path+".native-terminal",candidate=destination+".tmp";
                if(!SafePath(destination)||!SafePath(candidate)||File.Exists(candidate))return false;
                if(File.Exists(destination))return TerminalMatches(destination,terminal)&&Current();
                using(var file=new FileStream(candidate,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {file.Write(terminal,0,terminal.Length);file.Flush(true);}
                if(!TerminalMatches(candidate,terminal)||!Current())return false;
                File.Move(candidate,destination);
                return TerminalMatches(destination,terminal)&&Current();
            }
            catch{return false;}
        }
        private static bool TerminalMatches(string path,byte[] expected)
        {
            if(expected==null||expected.Length<1||expected.Length>2L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes||!SafePath(path))return false;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length!=expected.Length)return false;
                var block=new byte[4096];int offset=0;
                while(offset<expected.Length)
                {
                    int count=stream.Read(block,0,Math.Min(block.Length,expected.Length-offset));if(count<1)return false;
                    for(int i=0;i<count;i++)if(block[i]!=expected[offset+i])return false;offset+=count;
                }
                return stream.ReadByte()==-1&&stream.Length==expected.Length&&SafePath(path);
            }
        }
        internal bool MatchesAfter(TileEntityWorkstation target)
        {
            if(!IsTarget(target)||!Current())return false;
            var frame=RebirthProductiveEarnedRowProof.Decode(image);
            return target.Queue!=null&&frame.RowIndex==target.Queue.Length-1&&
                RebirthProductiveEarnedRowProof.CaptureRow(target.Queue[frame.RowIndex]).SequenceEqual(frame.AfterRow)&&
                (!frame.HasRecipeDefinition||RebirthProductiveEarnedUiQueueOwner.Binding(target.Queue[frame.RowIndex])==frame.FullRecipeBinding&&
                    RebirthRecipeOutputs.DefinitionIdentity(target.Queue[frame.RowIndex].Recipe)==frame.ExtrasDefinition)&&
                SameTools(target.Tools,frame.AfterTools)&&
                (!(target is RebirthCamperStationState mobile)||frame.HasMobileOrigin&&RebirthProductiveEarnedMobileOrigin.MatchesAfter(mobile,this,frame));
        }
        internal void RetainCompleted()
        {
            try
            {
                if(!Current())return;
                string directory=Path.GetDirectoryName(path);
                if(Path.GetFileName(directory)=="retained")return;
                string archiveDirectory=Path.Combine(directory,"retained"),archive=Path.Combine(archiveDirectory,Path.GetFileName(path));
                if(!SafePath(archiveDirectory)||!SafePath(archive))return;
                Directory.CreateDirectory(archiveDirectory);
                if(File.Exists(archive))return; // Preserve both on any interrupted/conflicting archive.
                File.Move(path,archive);
            }
            catch{/* Retain original file on unavailable archive; no effect proof inferred. */}
        }
        internal bool Current()
        {
            try
            {
                return ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&!world.IsRemote()&&
                    Path.GetFullPath(GameIO.GetSaveGameDir())==save&&WorldIdentity(world)==worldIdentity&&
                    (observedRemoteIdentity!=null?observedRemoteIdentity():LoadedSource==null?NativeCurrent(world,station,CustodyBirth):LoadedSource.CurrentIdentity(world,station,Birth,Operation))&&
                    SafePath(path)&&File.Exists(path)&&new FileInfo(path).Length==image.Length&&FileMatches(path,image)&&MobileDependenciesCurrent();
            }
            catch{return false;}
        }
    }
    internal static bool TryPrepare(TileEntityWorkstation station,RebirthProductiveEarnedRowProof.Frame frame,out Prepared prepared)
        =>TryPrepareCore(station,frame,null,null,null,out prepared);
    internal static bool TryPrepareRemote(TileEntityWorkstation station,RebirthProductiveEarnedRowProof.Frame frame,
        EntityPlayer actor,ClientInfo sender,string creation,out Prepared prepared)
        =>TryPrepareCore(station,frame,actor,sender,creation,out prepared);
    private static bool TryPrepareCore(TileEntityWorkstation station,RebirthProductiveEarnedRowProof.Frame frame,
        EntityPlayer actor,ClientInfo sender,string creation,out Prepared prepared)
    {
        prepared=null;
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||station==null||
                frame?.World!=WorldIdentity(world)||
                !(actor==null&&sender==null?RebirthRecyclerNativeLoop.IsCurrentInvocation(station)||RebirthProductiveEarnedCoordinator.IsAdoptedNativeChildPreparation(station,frame)||RebirthProductiveEarnedCoordinator.IsMobileChildPreparation(station,frame):
                    RemoteAuthority(actor,sender,station,creation))||!NativeCurrent(world,station,frame?.Birth??Guid.Empty)||
                RebirthRemoteResourceSourceCustody.BlocksStation(station)||
                RebirthRemoteResourcePlacedStationClientCustody.Blocks(station)||RebirthRemoteResourcePlacedStationCustody.Blocks(station)||
                !RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out var before,out var after,out var beforeTools,out var afterTools)||
                station.Queue==null||frame.RowIndex!=station.Queue.Length-1)return false;
            if(actor!=null&&(!frame.HasNativeBodies||
                !RebirthProductiveEarnedNativeBody.TryRead(frame.BeforeNative,frame.BeforeElapsedOffset,out var nativeBefore,out _)||
                !nativeBefore.LocalPosition.Equals(station.localChunkPos)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,frame.BeforeNative,frame.BeforeElapsedOffset)))return false;
            var row=station.Queue[frame.RowIndex];
            if(row?.Recipe==null||!RebirthProductiveEarnedRowProof.CaptureRow(row).SequenceEqual(frame.BeforeRow)||
                RebirthRecyclerRuntime.IsMarked(row.Recipe)||RebirthCookingHeat.Managed(row.Recipe)||
                frame.HasRecipeDefinition&&(RebirthProductiveEarnedUiQueueOwner.Binding(row)!=frame.FullRecipeBinding||
                    RebirthRecipeOutputs.DefinitionIdentity(row.Recipe)!=frame.ExtrasDefinition)||
                !SameTools(station.Tools,frame.BeforeTools))return false;
            // Only an entire application actually covered by productive work
            // can prepare earned permission. Zero/uninitialized timers cannot.
            float needed=before.CraftingTimeLeft>0?before.CraftingTimeLeft:before.CraftingTimeLeft<0?before.OneItemCraftTime:0;
            if(needed<=0||float.IsNaN(needed)||float.IsInfinity(needed)||frame.WorkedSeconds!=needed||
                RebirthProductiveToolWork.Limit(station.Tools,row.Recipe,station.block,needed)!=needed||
                !SameTools(RebirthProductiveToolWork.Wear(station.Tools,row.Recipe,station.block,needed),frame.AfterTools))return false;
            byte[] bytes=RebirthProductiveEarnedRowProof.Encode(frame);
            string save=Path.GetFullPath(GameIO.GetSaveGameDir());
            if(!Directory.Exists(save)||!SafePath(save))return false;
            string directory=Path.Combine(save,"RebirthProductiveEarned",frame.Birth.ToString("N"));
            if(!SafePath(directory))return false;
            Directory.CreateDirectory(directory);
            if(!SafePath(directory))return false;
            string path=Path.Combine(directory,frame.Operation.ToString("N")+".rew");
            string temp=path+".tmp";
            // An unknown retained candidate is never deleted/promoted/replaced.
            if(!SafePath(path)||!SafePath(temp)||File.Exists(temp))return false;
            if(File.Exists(path))
            {
                if(new FileInfo(path).Length!=bytes.Length||!FileMatches(path,bytes))return false;
            }
            else
            {
                int entries=0;long pendingBytes=0;
                foreach(string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if(++entries>=128||!SafePath(entry))return false;
                    if(Directory.Exists(entry)){if(Path.GetFileName(entry)!="retained")return false;continue;}
                    var existing=new FileInfo(entry);
                    if(existing.Length<1||existing.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
                    pendingBytes=checked(pendingBytes+existing.Length);
                    if(pendingBytes>64L*1024*1024-bytes.Length)return false;
                }
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(new FileInfo(temp).Length!=bytes.Length||!FileMatches(temp,bytes))return false;
                File.Move(temp,path);
                if(new FileInfo(path).Length!=bytes.Length||!FileMatches(path,bytes))return false;
            }
            var result=new Prepared(world,station,save,path,bytes,frame.Birth,frame.Operation);
            if(!result.Current()||(!(station is RebirthCamperStationState)&&frame.HasNativeBodies&&frame.HasRecipeDefinition&&
                !RebirthProductiveEarnedSavedNativeSource.PublishOriginalLocation(result,station,frame)))return false;
            prepared=result;return true;
        }
        catch{return false;}
    }
    internal static bool RemoteAuthority(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation)
    {
        var world=GameManager.Instance?.World;var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||connection?.IsServer!=true||actor==null||sender==null||actor.IsDead()||
            !ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||
            !ReferenceEquals(connection.Clients.ForEntityId(actor.entityId),sender)||RebirthCamperCharacterAccess.CurrentCreation(actor)!=creation||
            station==null||station is RebirthCamperStationState||!ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station)||
            (actor.position-station.ToWorldPos().ToVector3()).sqrMagnitude>64f||station.IsSharedLock(0)||LockManager.Instance==null||
            !LockManager.Instance.singleLocks.TryGetByValue(new LockManager.LockEntry(station,0),out var owner)||owner!=actor.entityId||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity))return false;
        var native=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId;
        return native!=null&&RebirthOriginalActionScope.Matches(actor,identity.StorageKey,creation)&&
            RebirthWorkstationSecurityService.CanAccessWorkstation(world,station.ToWorldPos(),actor,native,false,
                RebirthSecureAccessPurpose.WorkstationAccess,out _);
    }
    // Reads a previously durable original, not permission to replay owner work.
    internal static bool TryReadPreparedIdentity(TileEntityWorkstation station,Guid birth,Guid operation,out Prepared prepared,out byte[] image)
    {
        prepared=null;image=null;
        try
        {
            var world=GameManager.Instance?.World;
            if(world?.IsRemote()!=false||!NativeCurrent(world,station,birth)||operation==Guid.Empty)return false;
            string save=Path.GetFullPath(GameIO.GetSaveGameDir());
            string path=Path.Combine(save,"RebirthProductiveEarned",birth.ToString("N"),operation.ToString("N")+".rew");
            if(!SafePath(path)||!File.Exists(path)||File.Exists(path+".tmp"))return false;
            image=ReadBounded(path);var frame=RebirthProductiveEarnedRowProof.Decode(image);
            if(frame.Birth!=birth||frame.Operation!=operation||frame.World!=WorldIdentity(world)||
                !RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out _,out _,out _,out _))return false;
            var result=new Prepared(world,station,save,path,image,birth,operation);
            if(!result.Current())return false;prepared=result;return true;
        }
        catch{prepared=null;image=null;return false;}
    }
    internal static bool TryReadPrepared(TileEntityWorkstation station,RebirthProductiveEarnedRowProof.Frame expected,out Prepared prepared)
    {
        prepared=null;
        try
        {
            var world=GameManager.Instance?.World;
            if(world?.IsRemote()!=false||!NativeCurrent(world,station,expected.Birth)||expected.World!=WorldIdentity(world))return false;
            string save=Path.GetFullPath(GameIO.GetSaveGameDir());
            string path=Path.Combine(save,"RebirthProductiveEarned",expected.Birth.ToString("N"),expected.Operation.ToString("N")+".rew");
            if(!SafePath(path)||File.Exists(path+".tmp")||!File.Exists(path))return false;
            byte[] bytes=ReadBounded(path);
            if(!RebirthProductiveEarnedRowProof.MatchesEncoded(expected,bytes))return false;
            var result=new Prepared(world,station,save,path,bytes,expected.Birth,expected.Operation);
            if(!result.Current())return false;prepared=result;return true;
        }
        catch{return false;}
    }
    internal static bool TryLoadAfter(TileEntityWorkstation station,Guid birth,Guid operation,byte[] marker,out Prepared prepared)
    {
        prepared=null;
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!NativeCurrent(world,station,birth)||operation==Guid.Empty)return false;
            string save=Path.GetFullPath(GameIO.GetSaveGameDir());
            string path=Path.Combine(save,"RebirthProductiveEarned",birth.ToString("N"),operation.ToString("N")+".rew");
            string archived=Path.Combine(Path.GetDirectoryName(path),"retained",Path.GetFileName(path));
            if(!SafePath(path)||!SafePath(archived)||File.Exists(path+".tmp")||File.Exists(archived+".tmp"))return false;
            string source=File.Exists(path)?path:File.Exists(archived)?archived:null;
            if(source==null)return false;
            long length=new FileInfo(source).Length;
            if(length<100||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
            byte[] bytes=ReadBounded(source);
            if(File.Exists(path)&&File.Exists(archived)&&(new FileInfo(archived).Length!=bytes.Length||!ReadBounded(archived).SequenceEqual(bytes)))return false;
            var frame=RebirthProductiveEarnedRowProof.Decode(bytes);
            if(frame.Birth!=birth||frame.Operation!=operation||frame.World!=WorldIdentity(world)||
                !RebirthProductiveEarnedRowProof.MatchesMarker(marker,bytes)||
                !RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out _,out _,out _,out _))return false;
            var result=new Prepared(world,station,save,source,bytes,birth,operation);
            if(!result.MatchesAfter(station))return false;prepared=result;return true;
        }
        catch{return false;}
    }
    internal static bool TryLoadObserved(TileEntityWorkstation station,out Prepared prepared)
    {
        prepared=null;
        try
        {
            var world=GameManager.Instance?.World;
            if(station is RebirthCamperStationState mobile)return TryLoadObservedMobile(mobile,out prepared);
            if(!RebirthProductiveEarnedSavedNativeSource.TryObserved(station,out var source)||source.Terminal||
                !source.CurrentIdentity(world,station,source.Frame.Birth,source.Frame.Operation)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset))return false;
            const string suffix=".earned-load";
            if(source.Load?.PredictedNative!=true&&!source.Path.EndsWith(suffix,StringComparison.Ordinal))return false;
            string path=source.Load?.PredictedNative==true?source.Path:source.Path.Substring(0,source.Path.Length-suffix.Length);
            if(!FileMatches(path,source.Original))return false;
            if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var custody))
            {
                using(var admission=RebirthLogisticsEndpointIdentity.Begin(world,station,()=>
                    RebirthRecyclerNativeLoop.IsCurrentInvocation(station)&&source.CurrentIdentity(world,station,source.Frame.Birth,source.Frame.Operation)))
                    if(admission?.Current()!=true)return false;
                if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out custody))return false;
            }
            if(!Guid.TryParseExact(custody,"N",out source.CustodyBirth)||source.CustodyBirth==Guid.Empty)return false;
            var result=new Prepared(world,station,Path.GetFullPath(GameIO.GetSaveGameDir()),path,source.Original,
                source.Frame.Birth,source.Frame.Operation){LoadedSource=source};
            if(!result.MatchesAfter(station))return false;prepared=result;return true;
        }
        catch{return false;}
    }
    internal static bool SameTools(ItemStack[] tools,byte[] image)
    {
        return image!=null&&RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(tools,
            RebirthProductiveEarnedRowProof.MaximumFrameBytes,349528,out var encoded)&&
            Convert.FromBase64String(encoded).SequenceEqual(image);
    }
    private static bool NativeCurrent(World world,TileEntityWorkstation station,Guid birth)
    {
        if(birth==Guid.Empty||station==null)return false;
        if(station is RebirthCamperStationState mobile)
            return mobile.InstallationId==birth&&mobile.BoundVehicle!=null&&
                ReferenceEquals(mobile.BoundVehicle.world,world)&&ReferenceEquals(world.GetEntity(mobile.BoundVehicle.entityId),mobile.BoundVehicle)&&
                ReferenceEquals(RebirthCamperModuleRuntime.For(mobile.BoundVehicle).Stations.Find(birth)?.State,mobile);
        return RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var id)&&id==birth.ToString("N");
    }
    internal static string WorldIdentity(World world)
    {
        string text=world?.IsRemote()==true?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):world?.Guid;
        if(world==null||!Guid.TryParse(text,out var id)||id==Guid.Empty)return null;
        using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id.ToString("N"))));
    }
    // Exact bounded file equality, not a publication receipt. Avoid repeated
    // whole-artifact clones in every current/native callback predicate.
    internal static bool FileMatches(string path,byte[] expected)
    {
        try
        {
            if(expected==null||expected.Length<100||expected.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !SafePath(path)||File.Exists(path+".tmp"))return false;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length!=expected.Length)return false;
                var buffer=new byte[4096];int offset=0;
                while(offset<expected.Length)
                {
                    int count=stream.Read(buffer,0,Math.Min(buffer.Length,expected.Length-offset));if(count<1)return false;
                    for(int index=0;index<count;index++)if(buffer[index]!=expected[offset+index])return false;
                    offset+=count;
                }
                return stream.ReadByte()==-1&&stream.Length==expected.Length;
            }
        }
        catch{return false;}
    }
    private static byte[] ReadBounded(string path)
    {
        if(!SafePath(path))throw new InvalidDataException("Unsafe earned artifact path.");
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            long length=stream.Length;
            if(length<100||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)throw new InvalidDataException("Invalid earned artifact size.");
            var result=new byte[(int)length];int offset=0;
            while(offset<result.Length){int read=stream.Read(result,offset,result.Length-offset);if(read<1)throw new EndOfStreamException();offset+=read;}
            if(stream.ReadByte()!=-1||stream.Length!=length)throw new InvalidDataException("Earned artifact changed during read.");
            return result;
        }
    }
    internal static bool SafePath(string path)
    {
        try
        {
            string current=Path.GetFullPath(path);
            while(!string.IsNullOrEmpty(current))
            {
                try{if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return false;}
                catch(FileNotFoundException){}
                catch(DirectoryNotFoundException){}
                string parent=Path.GetDirectoryName(current);if(parent==current)break;current=parent;
            }
            return true;
        }
        catch{return false;}
    }
}
