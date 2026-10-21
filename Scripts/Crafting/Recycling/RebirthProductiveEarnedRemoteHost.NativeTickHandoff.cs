using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    [ThreadStatic] private static TileEntityWorkstation nativeHandoffTick;
    internal static bool IsNativeHandoffTick(TileEntityWorkstation station)=>ThreadManager.IsMainThread()&&ReferenceEquals(nativeHandoffTick,station)&&station!=null;
    internal static bool CanRunNativeHandoffSettlement(TileEntityWorkstation station)
        =>!RebirthProductiveEarnedUiQueueOwner.HoldsInitialPublication(station)&&!RebirthRecyclerPlacedSettlement.HoldsStation(station)&&
            !RebirthCookingMillingPublication.Held(station)&&!RebirthRemoteResourceSourceCustody.BlocksStation(station)&&
            !RebirthRemoteResourcePlacedStationClientCustody.Blocks(station)&&!RebirthRemoteResourcePlacedStationCustody.Blocks(station)&&
            !RebirthProductiveEarnedUiCoordinator.Blocks(station)&&RebirthProductiveEarnedUiDeferredTransport.CanTickAuthorityNative(station)&&
            RebirthProductiveEarnedUiDeferredTransport.CanTickKnownNativeReceipt(station)&&RebirthWorkstationCompletionPipeline.CanTickRemoteNativeCompletion(station)&&
            RebirthProductiveEarnedUiCoordinator.CanTickQueueHandoff(station)&&RebirthProductiveEarnedUiRemote.CanTickHostQueueHandoff(station);
    // Called only by the existing actual UpdateTick Prefix, before its held
    // check. This is distinct from a HandleRecipeQueue invocation and cannot
    // grant its output/timer effects.
    private static void ObserveNativeTickHandoff(TileEntityWorkstation station,bool runOriginal)
    {
        if(!runOriginal||!RebirthProductiveEarnedCoordinator.Enabled||!ThreadManager.IsMainThread()||
            station==null||GameManager.Instance?.World?.IsRemote()!=false||nativeHandoffTick!=null)return;
        nativeHandoffTick=station;
        try
        {
            var world=GameManager.Instance.World;
            ObserveWorld();ObserveEarly();
            if(TryFinishNativeClearedHandoffs(station))return;
            if(!CanRunNativeHandoffSettlement(station))return;
            foreach(var retained in pending.Values.Where(value=>!value.ViewerToolsOnly&&ReferenceEquals(value.Station,station)).ToArray())
                if(TryAdoptRemoteSavedPending(retained,null)||TryAdoptRemoteSavedRoot(retained,null)||TryAdoptRemoteSavedEarnedOriginal(retained,null))return;
            if(!RebirthProductiveEarnedSavedNativeSource.TryObserved(station,out var source)||!source.PrimaryCurrent()||
                !source.CurrentIdentity(world,station,source.Frame.Birth,source.Frame.Operation)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset))return;
            if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var incarnation))
            {
                using(var admission=RebirthLogisticsEndpointIdentity.Begin(world,station,()=>
                    ReferenceEquals(nativeHandoffTick,station)&&source.PrimaryCurrent()&&
                    source.CurrentIdentity(world,station,source.Frame.Birth,source.Frame.Operation)&&
                    RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset)))
                    if(admission?.Current()!=true)return;
                if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out incarnation))return;
            }
            if(Guid.TryParseExact(incarnation,"N",out var custody)&&custody!=Guid.Empty)source.CustodyBirth=custody;
            TryReleaseLoadedViewerTools(station,source);
            foreach(var hold in coldEarly.Where(value=>value.Position.Equals(station.ToWorldPos())).ToArray())
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null||!RebirthProductiveEarnedArtifact.TryReadRemoteObservedOriginal(station,hold.Birth,hold.Operation,out var artifact,out var image))continue;
                    string header=hold.Path.Substring(0,hold.Path.Length-5)+".header";
                    if(!Safe(header)||File.Exists(header+".tmp"))continue;
                    var frame=RebirthProductiveEarnedRowProof.Decode(image);byte[] headerBytes=Read(header);
                    if(!decoder.Admit(frame,image.Length)||!TryReadHeader(headerBytes,hold.Owner,hold.Creation,hold.Deaths,station,frame,Digest(image),out var nonce)||nonce!=hold.Nonce)continue;
                    var restored=new Context{World=world,Station=station,Artifact=artifact,Image=image,Operation=hold.Operation,Nonce=nonce,
                        Owner=hold.Owner,Creation=hold.Creation,Deaths=hold.Deaths,Directory=Path.GetFullPath(GameIO.GetSaveGameDir()),HeaderPath=header,Header=headerBytes,WorldKey=hold.World};
                    if(TryAdoptRemoteSavedPending(restored,source)||TryAdoptRemoteSavedRoot(restored,source)||TryAdoptRemoteSavedEarnedOriginal(restored,source))return;
                }
            }
            if(!EarlyBlocks(station)&&!pending.Values.Any(value=>ReferenceEquals(value.Station,station)))
                RebirthProductiveEarnedCoordinator.RestoreSavedNativeAtTick(station);
        }
        catch{/* An unqualified native source keeps its existing financial hold. */}
        finally
        {
            try{RebirthProductiveEarnedCoordinator.PumpAdoptedNativeTick(station);}catch{}
            nativeHandoffTick=null;
        }
    }

    private static bool TryAdoptRemoteSavedRoot(Context source,RebirthProductiveEarnedSavedNativeSource.Evidence loaded)
    {
        IDisposable workspace=null;
        RebirthProductiveEarnedObservationBudget.Decoder ownedDecoder=null;
        try
        {
            // A live exact owning card retains its original remote route.
            if(OriginalStillOwnsNativeCard(source)||!ReferenceEquals(nativeHandoffTick,source.Station)||source.Artifact?.Current()!=true||
                source.WorldKey!=RebirthProductiveEarnedArtifact.WorldIdentity(source.World))return false;
            string root=source.HeaderPath,original=source.Artifact.OriginalPath;
            var length=new FileInfo(root+".nativeOutput").Length;
            if(length<100||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*length+65536),out workspace))return false;
            if(loaded==null&&(ownedDecoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())==null)return false;
            foreach(string suffix in new[]{".nativePending",".nativePending.tmp",".nativeTools.tmp"})if(File.Exists(root+suffix))return false;
            foreach(string suffix in new[]{".partial-successor.invoking",".partial-successor.invoking.tmp",".earned-child.invoking",".earned-child.invoking.tmp",".tool-gesture",".tool-gesture.tmp"})
                if(File.Exists(original+suffix))return false;
            byte[] record=ReadNativeOutputMetadata(root+".nativeOutput",RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            byte[] saved=ReadNativeOutputMetadata(root+".nativeOutputSaved",8192);
            var frame=RebirthProductiveEarnedRowProof.Decode(source.Image);
            if(ownedDecoder!=null&&!ownedDecoder.Admit(frame,source.Image.Length))return false;
            if(!RebirthProductiveEarnedNativeOutputPlan.TryRead(record,frame,source.Nonce,Digest(source.Image),out var plan))return false;
            source.NativeOutput=plan;source.NativeOutputRecord=record;
            if(!ValidateNativeOutputSaved(source,saved))return false;
            bool tools=File.Exists(root+".nativeTools");
            if(loaded==null)
            {
                if(!source.NativeOutputSaved||!source.NativeOutputAccepted||tools&&(source.Tools?.Saved!=true||!source.Tools.Accepted))return false;
                if(tools)plan=EffectiveNativeOutput(source);
            }
            else
            {
                if(!loaded.Terminal||!loaded.PrimaryCurrent()||loaded.Frame.Operation!=source.Operation||loaded.Frame.Birth!=frame.Birth||
                    !loaded.Original.SequenceEqual(source.Image)||tools&&!loaded.ToolsRebased)return false;
                if(tools)
                {
                    plan.Native=loaded.Native;plan.Offset=loaded.NativeOffset;
                    if(!File.Exists(root+".nativeToolsSaved"))return false;
                }
            }
            bool marked=RebirthProductiveEarnedCoordinator.TryNativeMarker(source.Station,out var marker);
            if((marked?!source.Artifact.MatchesMarker(marker):plan.PendingIndex>=0||!source.NativeMarkerReleased)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,plan.Native,plan.Offset))return false;
            RebirthProductiveEarnedSavedNativeSource.NativeChildDependency[] toolDependencies=null;
            if(tools&&!RebirthProductiveEarnedSavedNativeSource.TryRemoteHandoffToolDependencies(original,source.Image,plan.Native,plan.Offset,out toolDependencies))return false;
            string holdPath=root.Substring(0,root.Length-7)+".hold";var hold=ReadEarly(holdPath);
            if(hold.Operation!=source.Operation||hold.Birth!=frame.Birth||hold.Nonce!=source.Nonce||hold.Owner!=source.Owner||hold.Creation!=source.Creation||
                hold.Deaths!=source.Deaths||hold.World!=source.WorldKey||!hold.Position.Equals(source.Station.ToWorldPos()))return false;
            byte[] adopted;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {writer.Write(0x31414E52u);writer.Write(Digest(source.Image));writer.Write(Digest(source.Header));writer.Write(Digest(record));writer.Write(Digest(saved));
             writer.Write(tools?Digest(ReadNativeOutputMetadata(root+".nativeToolsSaved",8192)):"");writer.Flush();adopted=stream.ToArray();}
            string adoption=root+".nativeStationAdopted";
            if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(adoption,adopted))return false;
            // Freeze immutable source dependencies before retiring the remote
            // observer graph. No actor/card predicate is carried into native work.
            var artifact=source.Artifact;byte[] originalImage=source.Image,header=source.Header;
            Func<bool> qualification=()=>artifact.Current()&&RebirthProductiveEarnedArtifact.FileMatches(original,originalImage)&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(adoption,adopted)&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(root,header)&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(root+".nativeOutput",record,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(root+".nativeOutputSaved",saved)&&
                RebirthProductiveEarnedSavedNativeSource.RemoteHandoffDependenciesCurrent(toolDependencies);
            string originalDigest=Digest(originalImage),headerDigest=Digest(header),terminalDigest=Digest(saved);
            Func<bool> releaseReady=()=>NativeAdoptionReleased(hold,1,originalDigest,headerDigest,terminalDigest);
            if(plan.PendingIndex>=0)
            {
                if(!RebirthProductiveEarnedCoordinator.AdoptRemoteSavedNativeTerminal(source.Station,artifact,frame,plan,qualification,
                    checked((long)originalImage.Length+header.Length+record.Length+saved.Length+adopted.Length+2048),tools,releaseReady))return false;
            }
            else if(!source.NativeMarkerReleased)
            {
                if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(root+".nativeStationClearInvoking",adopted)||
                    !RebirthProductiveEarnedCoordinator.ClearRemoteNativeFinal(source.Station,artifact))return false;
                source.NativeMarkerReleased=true;
            }
            var closed=new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=source.Operation,Birth=frame.Birth,Nonce=source.Nonce,World=source.WorldKey,
                Owner=source.Owner,Creation=source.Creation,Deaths=source.Deaths,Position=source.Station.ToWorldPos(),Kind=1,OriginalDigest=Digest(source.Image),
                HeaderDigest=Digest(source.Header),HoldDigest=Digest(hold.Bytes),TerminalDigest=Digest(saved)};
            byte[] closedBytes=RebirthProductiveEarnedRemoteClosedRecord.Encode(closed);
            if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(root+".closed",closedBytes)||
                !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(root+".nativeReleased",System.Text.Encoding.ASCII.GetBytes(Digest(closedBytes))))return false;
            DisposeAdoptedRemoteRoot(source);pending.Remove(source.Operation);early.Remove(source.Operation);coldEarly.RemoveAll(value=>value.Operation==source.Operation);
            RefreshEarlyAfterNativeClosure();return true;
        }
        catch{return false;}
        finally{ownedDecoder?.Dispose();workspace?.Dispose();}
    }
    private static void DisposeAdoptedRemoteRoot(Context source)
    {
        // Native coordinator retains its own charged source. Do not retire the
        // producer's saved-load descriptor or its marker during this handoff.
        source.Tools?.Dispose();source.Tools=null;source.Observation?.Dispose();source.Observation=null;
        source.BeforeWatch?.Dispose();source.BeforeWatch=null;source.BeforeAllocation?.Dispose();source.BeforeAllocation=null;
        source.NativeOutputWatch?.Dispose();source.NativeOutputWatch=null;source.NativePendingWatch?.Dispose();source.NativePendingWatch=null;
        source.NativeOutputRetention?.Dispose();source.NativeOutputRetention=null;source.NativePendingRetention?.Dispose();source.NativePendingRetention=null;
        source.NativeOutput=null;source.NativeOutputRecord=null;source.NativePending=null;source.NativePendingRecord=null;
        source.Memory?.Dispose();source.Memory=null;
    }
    private static bool OriginalStillOwnsNativeCard(Context source)
    {
        if(source.Current())return true;
        try
        {
            if(LockManager.Instance==null)return true;
            if(!LockManager.Instance.singleLocks.TryGetByValue(new LockManager.LockEntry(source.Station,0),out var id))return false;
            var actor=source.World.GetEntity(id) as EntityPlayer;
            var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(id);
            return actor?.Died==source.Deaths&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.StorageKey==source.Owner&&
                RebirthProductiveEarnedArtifact.RemoteAuthority(actor,peer,source.Station,source.Creation);
        }
        catch{return true;}
    }
    private static bool HasNativeStationAdoption(string root,string originalDigest,string headerDigest)
    {
        try
        {
            byte[] bytes=ReadNativeOutputMetadata(root+".nativeStationAdopted",1024);
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadUInt32()!=0x31414E52u||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=originalDigest||
                    RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=headerDigest||
                    RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(ReadNativeOutputMetadata(root+".nativeOutput",RebirthProductiveEarnedRowProof.MaximumArtifactBytes))||
                    RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(ReadNativeOutputMetadata(root+".nativeOutputSaved",8192)))return false;
                string tools=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                if(stream.Position<stream.Length&&
                    (RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(ReadNativeOutputMetadata(root+".nativePending",RebirthProductiveEarnedRowProof.MaximumArtifactBytes))||
                     RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(ReadNativeOutputMetadata(root+".nativePendingSaved",8192))))return false;
                return stream.Position==stream.Length&&(tools.Length==0||tools==Digest(ReadNativeOutputMetadata(root+".nativeToolsSaved",8192)));
            }
        }
        catch{return false;}
    }
    private static bool NativeAdoptionReleased(Early hold,byte kind,string originalDigest,string headerDigest,string terminalDigest)
    {
        try{return TryClosedEarly(hold,out var closed)&&closed.Kind==kind&&closed.OriginalDigest==originalDigest&&
            closed.HeaderDigest==headerDigest&&closed.TerminalDigest==terminalDigest;}
        catch{return false;}
    }
}
