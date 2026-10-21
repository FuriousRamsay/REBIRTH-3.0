using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;

internal static partial class RebirthProductiveEarnedRemoteClient
{
    private sealed class ClosingViewerTools:IDisposable
    {
        internal EntityPlayerLocal Actor;internal World World;internal string Root,Path;internal byte[] Plan;
        internal RebirthProductiveEarnedRemoteClosedRecord.Description Record;internal IDisposable Memory;internal double Next;
        internal bool Current()=>ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(Actor?.world,World)&&
            ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&Actor.IsSpawned()&&!Actor.IsDead()&&
            System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir())==Root&&RebirthProductiveEarnedArtifact.WorldIdentity(World)==Record.World&&
            RebirthCamperCharacterAccess.CurrentCreation(Actor)==Record.Creation&&RebirthStablePlayerIdentity.TryFromLocalPlatform(Actor,out var owner)&&owner.StorageKey==Record.Owner;
        public void Dispose(){Memory?.Dispose();Memory=null;Plan=null;}
    }
    private static readonly Dictionary<Guid,ClosingViewerTools> viewerToolsClosures=new Dictionary<Guid,ClosingViewerTools>();
    private static World viewerToolsClosureWorld;private static EntityPlayerLocal viewerToolsClosureActor;private static string viewerToolsClosureScope;private static bool viewerToolsClosureScanned,viewerToolsClosureUnknown;
    private static IEnumerator<string> viewerToolsScan;private static string viewerToolsScanPending;private static bool viewerToolsRetry;
    private static void ResetViewerToolsScan(){viewerToolsScan?.Dispose();viewerToolsScan=null;viewerToolsScanPending=null;viewerToolsClosureScanned=false;}
    private static bool ViewerToolMetadataCurrent(string path,RebirthProductiveEarnedRemoteClosedRecord.Description record)
    {
        foreach(string suffix in new[]{"",".scope",".nativeViewerSource",".nativeTools"})if(File.Exists(path+suffix+".tmp"))return false;
        return HashOriginalFile(path)==record.OriginalDigest&&HashOriginalFile(path+".scope")==record.HeaderDigest&&
            HashOriginalFile(path+".nativeViewerSource")==record.HoldDigest&&HashOriginalFile(path+".nativeTools")==record.TerminalDigest&&
            SavedNativeCloseStage(path,record,"nativeToolsOwnerUpload")&&SavedNativeCloseStage(path,record,"nativeToolsSyncAttempt");
    }
    private static bool ReadViewerToolClosed(string path,string world,string owner,string creation,out RebirthProductiveEarnedRemoteClosedRecord.Description record)
    {
        record=null;string closed=path+".viewerToolsClosed";if(!File.Exists(closed))return false;
        if(!Safe(closed)||File.Exists(closed+".tmp")||!RebirthProductiveEarnedRemoteClosedRecord.TryRead(Read(closed,4096),out var value)||
            value.World!=world||value.Owner!=owner||value.Creation!=creation||value.Kind!=1||System.IO.Path.GetFileName(path)!=value.Operation.ToString("N")+".rew"||
            !ViewerToolMetadataCurrent(path,value)||!SavedNativeCloseStage(path,value,"nativeViewerMetadataClosed")||!SavedNativeCloseStage(path,value,"nativeViewerReleased"))return false;
        record=value;return true;
    }
    private static bool QueueViewerToolsClosure(string path,EntityPlayerLocal actor)
    {
        IDisposable memory=null,workspace=null;
        try
        {
            if(actor?.world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,actor.world)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(actor,out var owner)||!File.Exists(path+".nativeTools"))return false;
            string world=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world),creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
            if(ReadViewerToolClosed(path,world,owner.StorageKey,creation,out _))return true;
            if(viewerToolsClosures.Values.Any(value=>value.Path==path))return true;
            if(viewerToolsClosures.Count>=4){viewerToolsRetry=true;return false;}
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null){viewerToolsRetry=true;return false;}
                long retainedLength=new[]{path,path+".scope",path+".nativeViewerSource",path+".nativeTools"}.Sum(file=>new FileInfo(file).Length);
                if(retainedLength<1||retainedLength>3L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes+2048)return false;
                byte[] image=Read(path,RebirthProductiveEarnedRowProof.MaximumArtifactBytes),scope=Read(path+".scope",2048);
                var frame=RebirthProductiveEarnedRowProof.Decode(image);string digest=Hash(image);Guid nonce;int deaths;Vector3i position;
                if(!frame.HasNativeBodies||!decoder.Admit(frame,image.Length)||!RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out _,out _,out _,out _)||
                    frame.World!=world||System.IO.Path.GetFileName(path)!=frame.Operation.ToString("N")+".rew")return false;
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*retainedLength+65536),out workspace))
                {viewerToolsRetry=true;return false;}
                byte[] source=Read(path+".nativeViewerSource",RebirthProductiveEarnedRowProof.MaximumArtifactBytes),
                    plan=Read(path+".nativeTools",RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                using(var stream=new MemoryStream(scope,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
                {
                    if(reader.ReadByte()!=1||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=owner.StorageKey||
                        RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512)!=creation)return false;
                    deaths=reader.ReadInt32();if(deaths<0||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,44)!=world)return false;
                    position=new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32());
                    if(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=digest||stream.Position!=stream.Length)return false;
                }
                using(var reader=new BinaryReader(new MemoryStream(plan,false)))
                {if(reader.ReadUInt32()!=0x31545252u||new Guid(reader.ReadBytes(16))!=frame.Operation||new Guid(reader.ReadBytes(16))!=frame.Birth)return false;nonce=new Guid(reader.ReadBytes(16));}
                if(nonce==Guid.Empty||!RebirthProductiveEarnedViewerToolSource.TryRead(null,image,source,nonce,out var paid,false)||
                    !RebirthProductiveEarnedRemoteToolPlan.TryRead(plan,frame.Operation,frame.Birth,nonce,digest,paid.Native,paid.Offset,out _))return false;
                var record=new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=frame.Operation,Birth=frame.Birth,Nonce=nonce,World=world,Owner=owner.StorageKey,
                    Creation=creation,Deaths=deaths,Position=position,Kind=1,OriginalDigest=digest,HeaderDigest=Hash(scope),HoldDigest=Hash(source),TerminalDigest=Hash(plan)};
                if(!RebirthProductiveEarnedRemoteClosedRecord.Valid(record)||!ViewerToolMetadataCurrent(path,record))return false;
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*plan.Length+8192+4L*path.Length),out memory))
                {viewerToolsRetry=true;return false;}
                viewerToolsClosures.Add(record.Operation,new ClosingViewerTools{Actor=actor,World=actor.world,Root=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir()),Path=path,Plan=plan,Record=record,Memory=memory});memory=null;return true;
            }
        }
        catch{return false;}
        finally{memory?.Dispose();workspace?.Dispose();}
    }
    private static void PumpViewerToolsClosures(double now)
    {
        if(!ReferenceEquals(viewerToolsClosureWorld,GameManager.Instance?.World))
        {foreach(var old in viewerToolsClosures.Values)old.Dispose();viewerToolsClosures.Clear();ResetViewerToolsScan();viewerToolsClosureWorld=GameManager.Instance?.World;viewerToolsClosureActor=null;viewerToolsClosureScope=null;viewerToolsClosureUnknown=false;}
        var actor=GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
        if(actor?.world?.IsRemote()!=true||!RebirthStablePlayerIdentity.TryFromLocalPlatform(actor,out var owner))return;
        string root=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir()),world=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world),creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
        string scope=root+"|"+world+"|"+owner.StorageKey+"|"+creation;
        if(!ReferenceEquals(viewerToolsClosureWorld,actor.world)||!ReferenceEquals(viewerToolsClosureActor,actor)||viewerToolsClosureScope!=scope)
        {foreach(var old in viewerToolsClosures.Values)old.Dispose();viewerToolsClosures.Clear();ResetViewerToolsScan();viewerToolsClosureWorld=actor.world;viewerToolsClosureActor=actor;viewerToolsClosureScope=scope;viewerToolsClosureUnknown=false;}
        if(!viewerToolsClosureScanned)
        {
            try
            {
                string folder=System.IO.Path.Combine(root,"RebirthProductiveViewerToolsOwner",world.Replace('/','_').Replace('+','-'),owner.StorageKey);
                if(!Safe(folder))throw new InvalidDataException();
                if(!Directory.Exists(folder))viewerToolsClosureScanned=true;
                else
                {
                    if(viewerToolsScan==null)viewerToolsScan=Directory.EnumerateFiles(folder,"*.rew").GetEnumerator();
                    long bytes=0;int visited=0;
                    while(visited<4)
                    {
                        string path=viewerToolsScanPending;viewerToolsScanPending=null;
                        if(path==null){if(!viewerToolsScan.MoveNext()){viewerToolsScan.Dispose();viewerToolsScan=null;viewerToolsClosureScanned=true;break;}path=viewerToolsScan.Current;}
                        if(!Safe(path)||File.Exists(path+".tmp")){viewerToolsClosureUnknown=true;visited++;continue;}
                        visited++;
                        // No gesture creates no payment original. Incomplete
                        // existing gestures retain their owning native route.
                        if(!File.Exists(path+".nativeTools")||!File.Exists(path+".nativeToolsOwnerUpload")||!File.Exists(path+".nativeToolsSyncAttempt"))continue;
                        long length;
                        try{length=new[]{path,path+".scope",path+".nativeViewerSource",path+".nativeTools"}.Sum(file=>new FileInfo(file).Length);}
                        catch{viewerToolsClosureUnknown=true;continue;}
                        if(length<1||length>3L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes+2048){viewerToolsClosureUnknown=true;continue;}
                        if(bytes>0&&bytes+length>24L*1024*1024){viewerToolsScanPending=path;break;}bytes+=length;
                        viewerToolsRetry=false;
                        if(!QueueViewerToolsClosure(path,actor))
                        {if(viewerToolsRetry){viewerToolsScanPending=path;break;}viewerToolsClosureUnknown=true;}
                    }
                }
            }
            catch{viewerToolsClosureUnknown=true;viewerToolsScan?.Dispose();viewerToolsScan=null;viewerToolsScanPending=null;viewerToolsClosureScanned=true;}
        }
        foreach(var pair in viewerToolsClosures.ToArray())
        {
            var value=pair.Value;if(!value.Current()){value.Dispose();viewerToolsClosures.Remove(pair.Key);continue;}
            if(now<value.Next)continue;value.Next=now+1;
            try
            {
                var record=value.Record;if(!ViewerToolMetadataCurrent(value.Path,record))continue;
                if(RebirthProductiveEarnedWire.TryReadReply(record.Operation,out var reply)&&reply.State==RebirthProductiveEarnedWire.Status.NativeToolSaved&&
                    reply.Operation==record.Operation&&reply.Birth==record.Birth&&reply.Nonce==record.Nonce&&reply.Digest==record.OriginalDigest&&
                    reply.World==record.World&&reply.Owner==record.Owner&&reply.Creation==record.Creation&&reply.Deaths==actor.Died&&reply.Position.Equals(record.Position))
                {
                    // A saved historical status closes metadata only; actual
                    // native returned-read stages are never synthesized here.
                    if(!WriteExact(value.Path+".nativeViewerMetadataClosed",NativeCloseStage(record,"nativeViewerMetadataClosed"),2048)||
                        !WriteExact(value.Path+".viewerToolsClosed",RebirthProductiveEarnedRemoteClosedRecord.Encode(record),4096)||
                        !WriteExact(value.Path+".nativeViewerReleased",NativeCloseStage(record,"nativeViewerReleased"),2048))continue;
                    foreach(var original in originals.Values.Where(item=>item.ViewerToolsOnly&&item.Path==value.Path&&item.Nonce==record.Nonce&&item.Digest==record.OriginalDigest).ToArray())
                    {originals.Remove(original.Station);original.Tools?.Dispose();original.Output?.Dispose();original.Memory?.Dispose();}
                    value.Dispose();viewerToolsClosures.Remove(pair.Key);continue;
                }
                RebirthProductiveEarnedWire.SendViewerToolStatus(actor,record.Position,record.Operation,record.Nonce,value.Plan);
            }
            catch{/* Exact original remains query-only; no native sync fallback. */}
        }
    }
}
