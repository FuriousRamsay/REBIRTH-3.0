using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // Issued by the actual FromPlayer -> same-object native Save/Write -> full
    // final-file readback. A parsed file/phase never constructs this token.
    internal sealed class SavedOwnerCheckpoint:IDisposable
    {
        internal readonly Original Original;internal readonly int Phase,Completed;
        internal long NativeSnapshotSequence;
        internal Guid Generation;
        internal byte[] Native {get;private set;}internal string Path {get;private set;}
        private IDisposable allocation;private bool published;private OwnerCapture capture;
        private NetPackagePlayerData packet;private bool packetPinned;
        internal byte[] FrozenOwnerPacket {get;private set;}internal string FrozenWireDigest {get;private set;}internal int FrozenWireLength {get;private set;}
        private SavedOwnerCheckpoint(Original original,int phase,int completed,IDisposable allocation)
        {Original=original;Phase=phase;Completed=completed;this.allocation=allocation;}
        internal static SavedOwnerCheckpoint Begin(Original original,int phase,int completed,IDisposable allocation)
        {
            if(savingOwner!=null||original?.Current()!=true)return null;
            return new SavedOwnerCheckpoint(original,phase,completed,allocation);
        }
        internal bool CaptureNative(PlayerDataFile data,string path)
        {
            if(!ReferenceEquals(savingOwner,this)||!MatchesCheckpoint(this,data))return false;
            Native=NativeOwnerImage(data);Path=path;return true;
        }
        internal void BindProjection(PlayerDataFile data)
        {
            if(!ReferenceEquals(savingOwner,this)||!MatchesCheckpoint(this,data))return;
            ForgetProjection();capture=new OwnerCapture{Data=data,Token=this};
            ownerCaptures.Remove(data);ownerCaptures.Add(data,capture);
        }
        internal void ForgetProjection()
        {
            if(capture?.Data!=null)ownerCaptures.Remove(capture.Data);
            if(capture!=null){capture.Data=null;capture.Token=null;capture.Stream=null;}capture=null;
        }
        internal void CaptureAndSaveNative()
        {
            if(!ReferenceEquals(savingOwner,this)||Original.Current()!=true)throw new InvalidOperationException();
            string destination=System.IO.Path.Combine(GameIO.GetPlayerDataDir(),Original.CanonicalOwner+"."+PlayerDataFile.EXT);
            foreach(string suffix in new[]{"",".tmp",".bak",".meta"})SafeOwnerPath(destination+suffix);
            if(!Original.World.IsRemote()){GameManager.Instance.SaveLocalPlayerData();return;}
            // Freeze the real owning actor once. The same native object is
            // saved locally and retained for the authenticated upload stage;
            // this local receipt alone never means the host accepted it.
            packet=NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(Original.Actor);
            if(capture==null||!ReferenceEquals(capture.Data,packet.playerDataFile)||!MatchesCheckpoint(this,packet.playerDataFile))
                throw new InvalidDataException();
            packet.RegisterSendQueue();packetPinned=true;
            packet.playerDataFile.Save(GameIO.GetPlayerDataDir(),Original.CanonicalOwner);
            TryFreezeOwnerPacket();
        }
        internal bool TryFreezeOwnerPacket()
        {
            if(FrozenOwnerPacket!=null)return LocalObservedSaved();
            if(!Original.World.IsRemote()||packet==null||!Original.Current()||!LocalObservedSaved()||
                !RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,checked(12L*(Native.Length+Evidence.Length+Original.Image.Length)+65536)))return false;
            if(!NativeOwnerImage(packet.playerDataFile).SequenceEqual(Native))return false;
            using(var stream=new RecyclerPdsPacketStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {writer.SetBaseStream(stream);packet.write(writer);writer.Flush();FrozenOwnerPacket=stream.ToArray();}
            byte[] wire=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.WireImage(packet.playerDataFile);
            FrozenWireLength=wire.Length;using(var hash=System.Security.Cryptography.SHA256.Create())FrozenWireDigest=Convert.ToBase64String(hash.ComputeHash(wire));
            if(!NativeOwnerImage(packet.playerDataFile).SequenceEqual(Native)||!MatchesCheckpoint(this,packet.playerDataFile))
            {FrozenOwnerPacket=null;FrozenWireDigest=null;FrozenWireLength=0;return false;}return true;
        }
        internal void PublishActualSave(PlayerDataFile data,string path)
        {
            if(!ReferenceEquals(savingOwner,this)||!ReferenceEquals(writingOwner?.Token,this)||
                !ReferenceEquals(writingOwner.Data,data)||!writingOwner.NativeWrite||Original.Current()!=true||
                !MatchesCheckpoint(this,data)||!MatchesOwnerFile(path,Native))return;
            EvidencePath=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),Original.Operation.ToString("N")+(Generation==Guid.Empty?"":".reentry."+Generation.ToString("N"))+".owner."+Phase+"."+Completed+".xml");
            var before=System.Xml.Linq.XElement.Parse(System.Text.Encoding.UTF8.GetString(Original.Image));
            var record=new System.Xml.Linq.XElement("workstationCompletionIntent",new System.Xml.Linq.XAttribute("operation",Original.Operation.ToString("N")),
                new System.Xml.Linq.XAttribute("world",Original.WorldKey),new System.Xml.Linq.XElement("before",before),
                new System.Xml.Linq.XElement("recyclerNativeOwnerSaved",new System.Xml.Linq.XAttribute("phase",Phase),new System.Xml.Linq.XAttribute("completed",Completed),
                    new System.Xml.Linq.XAttribute("path",path),new System.Xml.Linq.XElement("native",Convert.ToBase64String(Native))));
            if(!RebirthWorkstationCompletionPublication.Write(EvidencePath,record))return;
            Evidence=RebirthWorkstationCompletionPublication.CopyPublishedBytes(EvidencePath);
            published=Evidence!=null&&System.Xml.Linq.XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(EvidencePath));
        }
        internal bool LocalObservedSaved()=>published&&Native!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(EvidencePath,Evidence);
        internal bool ObservedSaved()=>LocalObservedSaved()&&(!Original.World.IsRemote()||IsRemoteCheckpointAccepted(this));
        internal NetPackagePlayerData FrozenPacket=>LocalObservedSaved()?packet:null;
        internal System.Xml.Linq.XElement CopyLocalSavedDescriptor()=>LocalObservedSaved()?new System.Xml.Linq.XElement("actualOwnerLocalSaved",
            new System.Xml.Linq.XAttribute("operation",Original.Operation.ToString("N")),new System.Xml.Linq.XAttribute("phase",Phase),new System.Xml.Linq.XAttribute("completed",Completed),
            new System.Xml.Linq.XAttribute("path",EvidencePath),new System.Xml.Linq.XAttribute("length",Evidence.Length),new System.Xml.Linq.XAttribute("digest",RebirthStationPublicationRecord.Digest(Evidence))):null;
        internal System.Xml.Linq.XElement CopySavedEvidence()=>ObservedSaved()?System.Xml.Linq.XElement.Parse(System.Text.Encoding.UTF8.GetString(Evidence)):null;
        private string EvidencePath;private byte[] Evidence;
        public void Dispose()
        {
            ForgetProjection();published=false;Native=Evidence=FrozenOwnerPacket=null;FrozenWireDigest=null;FrozenWireLength=0;Path=EvidencePath=null;
            var retained=packet;packet=null;
            if(retained!=null)
            {
                if(packetPinned){packetPinned=false;retained.SendQueueHandled();}
                else if(retained.inSendQueuesCount==0)NetPackageManager.FreePackage(retained);
            }
            allocation?.Dispose();allocation=null;
        }
    }
    private sealed class OwnerCapture
    {internal SavedOwnerCheckpoint Token;internal PlayerDataFile Data;internal string Path;internal bool NativeWrite;internal Stream Stream;}
    private sealed class OwnerWriting
    {internal OwnerCapture Capture,Previous;}
    [ThreadStatic]private static SavedOwnerCheckpoint savingOwner;
    [ThreadStatic]private static OwnerCapture writingOwner;
    private static readonly ConditionalWeakTable<PlayerDataFile,OwnerCapture> ownerCaptures=new ConditionalWeakTable<PlayerDataFile,OwnerCapture>();
    private static bool ownerFileInstalled;
    internal static void InstallOwnerFile(Harmony harmony)
    {if(ownerFileInstalled)return;RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RecyclerNativeOwnerWrite));ownerFileInstalled=true;}
    private static bool Phase(PlayerDataFile data,string key,out float value)
    {
        value=0;if(data?.buffData==null||data.buffData.Length>1024*1024)return false;
        return PhaseBytes(data.buffData.ToArray(),key,out value);
    }
    private static bool PhaseBytes(byte[] bytes,string key,out float value)
    {
        value=0;if(bytes==null||bytes.Length>1024*1024)return false;
        using(var memory=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(memory);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
            int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
            count=reader.ReadUInt16();bool found=false;
            for(int i=0;i<count;i++){string name=reader.ReadString();float number=reader.ReadSingle();if(name!=key)continue;
                if(found||float.IsNaN(number)||float.IsInfinity(number))return false;found=true;value=number;}
            return memory.Position==memory.Length;
        }
    }
    private static bool MatchesCheckpoint(SavedOwnerCheckpoint token,PlayerDataFile data)=>data!=null&&data.id==token.Original.Actor.entityId&&data.deaths==token.Original.Deaths&&
        Phase(data,token.Original.Receipt,out float phase)&&phase==token.Phase&&Phase(data,token.Original.Checkpoint,out float completed)&&completed==token.Completed&&
        token.Original.MatchesOwnerCheckpoint(RebirthGearPlayerDataInventory.ReadSlots(data,true),RebirthGearPlayerDataInventory.ReadSlots(data,false),token.Completed);
    private static bool ValidOwnerCheckpointStage(int phase,int completed,int slots)
    {
        return slots>=0&&completed>=0&&completed<=slots&&
            (phase==1&&completed==0||phase==2&&completed>0||phase==3&&completed==slots);
    }
    internal static SavedOwnerCheckpoint SaveLocalCheckpoint(Original original,int phase,int completed,Guid generation=default)
    {
        if(!ownerFileInstalled||savingOwner!=null||original?.Current()!=true||!ValidOwnerCheckpointStage(phase,completed,original.Slots.Length)||
            original.Actor.Buffs.GetCustomVar(original.Receipt)!=phase||original.Actor.Buffs.GetCustomVar(original.Checkpoint)!=completed||
            !original.MatchesOwnerCheckpoint(original.Actor.bag.ItemGrid.items,original.Actor.inventory.ItemGrid.items,completed)||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var allocation))return null;
        var token=SavedOwnerCheckpoint.Begin(original,phase,completed,allocation);
        if(token==null){allocation.Dispose();return null;}token.Generation=generation;savingOwner=token;
        try{token.CaptureAndSaveNative();if(token.LocalObservedSaved())return token;token.Dispose();return null;}
        catch{token.Dispose();return null;}
        finally{token.ForgetProjection();savingOwner=null;}
    }
    internal static void ObserveRecyclerOwnerFromPlayer(PlayerDataFile data,EntityPlayer player,bool run)
    {
        try{ObserveAutomaticRecyclerOwner(data,player,run);}catch{/* No automatic snapshot authority. */}
        var token=savingOwner;if(!run||token==null||!ReferenceEquals(player,token.Original.Actor)||token.Original.Current()!=true)return;
        try
        {
            if(!MatchesCheckpoint(token,data))return;
            token.BindProjection(data);
        }
        catch{/* No native projection witness. */}
    }
    internal static void EnterRecyclerOwnerSave(PlayerDataFile data,string directory,string playerId,bool run,out object state)
    {
        state=null;
        try{state=EnterRemoteRecyclerOwnerSave(data,directory,playerId,run);if(state!=null)return;}catch{/* Retain unknown remote checkpoint. */}
        if(!run||!ownerCaptures.TryGetValue(data,out var capture)||!ReferenceEquals(capture.Token,savingOwner)||capture.Token.Original.Current()!=true)return;
        try
        {
            if(playerId!=capture.Token.Original.CanonicalOwner||System.IO.Path.GetFullPath(directory)!=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir())||!MatchesCheckpoint(capture.Token,data))return;
            var path=System.IO.Path.GetFullPath(System.IO.Path.Combine(directory,playerId+"."+PlayerDataFile.EXT));
            foreach(string suffix in new[]{"",".tmp",".bak",".meta"})SafeOwnerPath(path+suffix);
            if(!capture.Token.CaptureNative(data,path))return;capture.Path=path;capture.NativeWrite=false;
            var writing=new OwnerWriting{Capture=capture,Previous=writingOwner};writingOwner=capture;state=writing;
        }
        catch{/* Unknown original stays retained by its controller. */}
    }
    internal static void ObserveRecyclerOwnerSave(bool run,object state)
    {
        if(state is RemoteOwnerWriting remote){ObserveRemoteRecyclerOwnerSave(run,remote);return;}
        if(state is not OwnerWriting writing)return;var capture=writing.Capture;var token=capture?.Token;
        try
        {
            if(!run||token==null||!capture.NativeWrite||!ReferenceEquals(savingOwner,token)||token.Original.Current()!=true||
                !MatchesCheckpoint(token,capture.Data)||!MatchesOwnerFile(capture.Path,token.Native))return;
            token.PublishActualSave(capture.Data,capture.Path);
        }
        catch{/* A caught native Save exception or changed full file is unknown. */}
    }
    internal static void EndRecyclerOwnerSave(object state)
    {
        if(state is RemoteOwnerWriting remote){EndRemoteRecyclerOwnerSave(remote);return;}
        if(state is not OwnerWriting writing)return;
        if(ReferenceEquals(writingOwner,writing.Capture))writingOwner=writing.Previous;
        if(writing.Capture?.Data!=null)ownerCaptures.Remove(writing.Capture.Data);
        if(writing.Capture!=null){writing.Capture.Data=null;writing.Capture.Token=null;writing.Capture.Stream=null;}writing.Capture=writing.Previous=null;
    }
    private sealed class OwnerNativeStream:MemoryStream
    {public override void Write(byte[] bytes,int at,int count){if(count<0||Position>16*1024*1024-count)throw new InvalidDataException();base.Write(bytes,at,count);}
     public override void WriteByte(byte value){if(Position>=16*1024*1024)throw new InvalidDataException();base.WriteByte(value);}}
    private sealed class RecyclerPdsPacketStream:MemoryStream
    {public override void Write(byte[] bytes,int at,int count){if(count<0||Position>24*1024*1024-count)throw new InvalidDataException();base.Write(bytes,at,count);}
     public override void WriteByte(byte value){if(Position>=24*1024*1024)throw new InvalidDataException();base.WriteByte(value);}}
    private static byte[] NativeOwnerImage(PlayerDataFile data)
    {
        using(var memory=new OwnerNativeStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(memory);writer.Write('t');writer.Write('t');writer.Write('p');writer.Write((byte)0);writer.Write((byte)PlayerDataFile.cFileVersion);data.Write(writer,StreamModeWrite.Persistency);writer.Flush();return memory.ToArray();}
    }
    private static void SafeOwnerPath(string path)
    {
        var root=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir());var full=System.IO.Path.GetFullPath(path);
        if(!full.StartsWith(root+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException();
        for(string current=full;!string.IsNullOrEmpty(current);current=System.IO.Path.GetDirectoryName(current))
        {try{if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();}
         catch(FileNotFoundException){}catch(DirectoryNotFoundException){}}
    }
    private static bool MatchesOwnerFile(string path,byte[] bytes)
    {
        if(bytes==null)return false;SafeOwnerPath(path);SafeOwnerPath(path+".tmp");if(File.Exists(path+".tmp"))return false;
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {if(file.Length!=bytes.Length)return false;var buffer=new byte[8192];int at=0,read;
            while((read=file.Read(buffer,0,buffer.Length))>0){for(int i=0;i<read;i++)if(bytes[at+i]!=buffer[i])return false;at+=read;}
            if(at!=bytes.Length)return false;}SafeOwnerPath(path);return true;
    }
    [HarmonyPatch(typeof(PlayerDataFile),nameof(PlayerDataFile.Write),new[]{typeof(PooledBinaryWriter),typeof(StreamModeWrite)})]
    private static class RecyclerNativeOwnerWrite
    {
        private static void Prefix(PlayerDataFile __instance,PooledBinaryWriter _bw,StreamModeWrite _eStreamMode,bool __runOriginal,out object __state)
        {
            __state=null;var capture=writingOwner;
            var remote=writingRecyclerOwner;
            if(__runOriginal&&remote!=null&&ReferenceEquals(remote.Data,__instance)&&_eStreamMode==StreamModeWrite.Persistency&&
                _bw?.BaseStream is FileStream remoteFile&&System.IO.Path.GetFullPath(remoteFile.Name)==remote.Path+".tmp"&&remoteFile.Position==5)
            {remote.Stream=remoteFile;__state=remote;return;}
            if(!__runOriginal||capture==null||!ReferenceEquals(capture.Data,__instance)||_eStreamMode!=StreamModeWrite.Persistency||
                _bw?.BaseStream is not FileStream file||System.IO.Path.GetFullPath(file.Name)!=capture.Path+".tmp"||file.Position!=5)return;
            capture.Stream=file;__state=capture;
        }
        private static void Postfix(bool __runOriginal,object __state)
        {
            if(__state is RemoteOwnerWriting remote)
            {if(__runOriginal&&remote.Stream!=null&&remote.Checkpoint?.Native!=null&&remote.Stream.Position==remote.Checkpoint.Native.Length)remote.Wrote=true;return;}
            if(__state is OwnerCapture local&&__runOriginal&&local.Stream!=null&&local.Token?.Native!=null&&local.Stream.Position==local.Token.Native.Length)local.NativeWrite=true;
        }
    }
}
