using System;
using System.Collections.Generic;
using System.Linq;

// Installed callsites are present, but activation awaits the complete remote
// native-owner prepare/upload/publication/ACK protocol and parent review.
internal static partial class RebirthProductiveEarnedCoordinator
{
    internal static bool Enabled {get;private set;}
    // Activation is a separate reviewed bootstrap decision. Merely installing
    // actual native observers below never grants ordinary financial permission.
    internal static void EnableReviewedSource()
    {
        RebirthProductiveEarnedSavedNativeSource.Install();
        RebirthProductiveEarnedUiCoordinator.Install();
        RebirthProductiveEarnedWire.Install();Enabled=true;
    }
    internal sealed class Attempt
    {
        internal TileEntityWorkstation Station;
        internal RecipeQueueItem Row;
        internal Recipe Recipe;
        internal int Multiplier;
        internal float Target;
        internal ItemStack[] Tools;
        internal RebirthProductiveEarnedRowProof.Frame Frame;
        internal RebirthProductiveEarnedArtifact.Prepared Artifact;
        internal RebirthProductiveEarnedPublication.Observation Observation;
        internal Func<bool> RemoteSavedEarned;
        internal bool NativeTickCustody;
        internal Func<bool> NativeAdoptionReleaseReady;
        internal bool NativeSuffixRequired;
        internal byte[] MobileOriginal;
        internal byte[] MobileReturnedImage;
        internal bool MobileAwaitingReturn,MobileReturnUnknown,MobileReturnedPublication;
        internal bool MobileOutputReturned;
        internal bool MobileInitialSaved;
        internal bool MobileOutputEntered,MobileNoEffectReturn;
        internal MobileTerminal MobileTerminal;
        internal MobileSuccessor MobileSuccessor;
        internal object NativeOutput;internal IDisposable Retention;
        internal bool OutputTimerObserved;
        internal bool PendingTimerObserved,PendingTimerAmbiguous;
        internal int PendingTimerIndex=-1;
        internal float PendingTimerBefore,PendingTimerTarget;
        internal NativeTerminal Terminal;
        internal NativePartialSuccessor PartialSuccessor;
        internal NativeEarnedChild EarnedChild;
        internal string PendingRecipeBinding,PendingExtras;
        internal IDisposable PendingDefinitionRetention;
        internal RebirthProductiveEarnedSavedNativeSource.CompletedNativeLoad LoadedCleanup;
        internal bool ClearedNativeMarker,NativeMetadataClosed;
        internal RebirthProductiveEarnedArtifact.CompletionRetention CompletionRetention;
    }
    private static readonly Dictionary<TileEntityWorkstation,Attempt> retained=new Dictionary<TileEntityWorkstation,Attempt>();
    private static World retainedWorld;
    private static void ObserveWorld()
    {
        var world=GameManager.Instance?.World;
        if(ReferenceEquals(world,retainedWorld))return;
        foreach(var old in retained.Values)
        {
            old.EarnedChild?.Dispose();old.PartialSuccessor?.Dispose();old.Terminal?.Dispose();old.Observation?.Dispose();
            RebirthProductiveEarnedSavedNativeSource.Release(old.Artifact?.LoadedSource);
            old.Tools=null;old.Frame=null;old.PendingDefinitionRetention?.Dispose();old.Retention?.Dispose();
            old.LoadedCleanup?.Dispose();old.LoadedCleanup=null;
            old.MobileTerminal?.Dispose();old.MobileTerminal=null;
            old.MobileSuccessor?.Dispose();old.MobileSuccessor=null;
        }
        retained.Clear();retainedWorld=world;
        // Native marker/artifacts remain original-save owned; no terminal inferred.
    }
    internal static bool BlocksNewWork(TileEntityWorkstation station)=>RebirthProductiveEarnedUiCoordinator.Blocks(station)||Enabled&&(!RebirthProductiveEarnedRemoteClient.IsExecuting(station)&&RebirthProductiveEarnedRemoteClient.HasPending(station)||TryNativeMarker(station,out _)||
        retained.TryGetValue(station,out var active)&&active.Artifact!=null);
    internal static bool HoldTimer(TileEntityWorkstation station,RecipeQueueItem row)
    {
        if(!Enabled)return false;
        ObserveWorld();
        if(RebirthProductiveEarnedRemoteClient.HasPending(station))return true;
        if(retained.TryGetValue(station,out var active))return ReferenceEquals(active.Row,row)&&
            row.Multiplier==active.Multiplier&&ReferenceEquals(row.Recipe,active.Recipe);
        return TryNativeMarker(station,out _); // Unknown original marker cannot mint new work.
    }
    internal static bool PrepareFinal(TileEntityWorkstation station,RecipeQueueItem row,ref float target,float worked,ItemStack[] afterTools,out Attempt attempt)
    {
        attempt=null;if(!Enabled)return true;
        try
        {
            if(GameManager.Instance?.World?.IsRemote()==true)return PrepareFinalCore(station,row,ref target,worked,afterTools,out attempt);
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {if(decoder==null)return false;return PrepareFinalCore(station,row,ref target,worked,afterTools,out attempt);}
        }
        catch{attempt=null;return false;} // Existing original/candidates stay held; no native setter ran.
    }
    private static bool PrepareFinalCore(TileEntityWorkstation station,RecipeQueueItem row,ref float target,float worked,ItemStack[] afterTools,out Attempt attempt)
    {
        attempt=null;ObserveWorld();
        if(retained.TryGetValue(station,out var retry))
        {
            if(retry.Artifact!=null||!ReferenceEquals(retry.Row,row)||!ReferenceEquals(retry.Recipe,row.Recipe)||
                retry.Multiplier!=row.Multiplier||retry.Frame.WorkedSeconds!=worked||
                !RebirthProductiveEarnedRowProof.CaptureRow(row).SequenceEqual(retry.Frame.BeforeRow)||
                !RebirthProductiveEarnedArtifact.SameTools(station.Tools,retry.Frame.BeforeTools)||
                !RebirthProductiveEarnedArtifact.SameTools(afterTools,retry.Frame.AfterTools))return false;
            if(!RebirthProductiveEarnedArtifact.TryPrepare(station,retry.Frame,out var original))return false;
            retry.Artifact=original;
            if(retry.MobileOriginal!=null&&!RebirthProductiveEarnedMobileOrigin.Publish(original,retry.MobileOriginal))return false;
            target=retry.Target;attempt=retry;return true;
        }
        // No persistence/wear is introduced for nondurable required tools.
        bool degradable=RebirthProductiveToolWork.Required(row.Recipe).Any(name=>station.Tools.Any(stack=>
            stack!=null&&!stack.IsEmpty()&&stack.itemValue.ItemClass?.GetItemName()==name&&stack.itemValue.MaxUseTimes>0));
        if(!degradable)return true;
        if(!RebirthRecyclerNativeLoop.IsCurrentInvocation(station)||retained.Count>=4||retained.ContainsKey(station)||
            TryNativeMarker(station,out _))return false;
        var world=GameManager.Instance?.World;
        if(world?.IsRemote()==true)return RebirthProductiveEarnedRemoteClient.Prepare(station,row,ref target,worked,afterTools,out attempt);
        if(world?.IsRemote()!=false)return false;
        // Mobile originals freeze the entire stock image. Refuse final paid
        // work while native output is full, before any marker or wear is owed;
        // the normal owning output-take route can then make room normally.
        if(station is RebirthCamperStationState&&!MobileOutputCanFit(station,row))return false;
        // Native uses a strict negative while predicate. Represent an exactly
        // earned zero with the smallest negative value BEFORE freezing proof;
        // no stalled elapsed can be credited to the next application.
        if(target==0)target=-float.Epsilon;
        Guid birth;
        if(station is RebirthCamperStationState mobile)birth=mobile.InstallationId;
        else
        {
            if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var id))
            {
                // Identity admission uses the existing native save scheduler.
                // No work occurs until there is an actual current incarnation.
                using(var admission=RebirthLogisticsEndpointIdentity.Begin(world,station,()=>
                    RebirthRecyclerNativeLoop.IsCurrentInvocation(station)&&ReferenceEquals(world,GameManager.Instance?.World)))
                    if(admission==null||!admission.Current())return false;
                if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out id))return false;
            }
            if(!Guid.TryParseExact(id,"N",out birth))return false;
        }
        if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(station.Tools,262144,349528,out var toolsBefore)||
            !RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(afterTools,262144,349528,out var toolsAfter))return false;
        var after=new RecipeQueueItem{Recipe=row.Recipe,Multiplier=row.Multiplier,CraftingTimeLeft=target,
            IsCrafting=row.IsCrafting,Quality=row.Quality,StartingEntityId=row.StartingEntityId,OneItemCraftTime=row.OneItemCraftTime,
            RepairItem=row.RepairItem,AmountToRepair=row.AmountToRepair};
        var frame=new RebirthProductiveEarnedRowProof.Frame{Birth=birth,Operation=Guid.NewGuid(),World=RebirthProductiveEarnedArtifact.WorldIdentity(world),
            RowIndex=station.Queue.Length-1,Multiplier=row.Multiplier,WorkedSeconds=worked,
            BeforeRow=RebirthProductiveEarnedRowProof.CaptureRow(row),AfterRow=RebirthProductiveEarnedRowProof.CaptureRow(after),
            BeforeTools=Convert.FromBase64String(toolsBefore),AfterTools=Convert.FromBase64String(toolsAfter)};
        frame.FullRecipeBinding=RebirthProductiveEarnedUiQueueOwner.Binding(row);frame.ExtrasDefinition=RebirthRecipeOutputs.DefinitionIdentity(row.Recipe);
        byte[] mobileOriginal=null;
        if(station is RebirthCamperStationState mobileSource)
        {
            if(!RebirthProductiveEarnedMobileOrigin.TryCapture(mobileSource,frame,afterTools,target,out mobileOriginal))return false;
            frame.MobileOriginDigest=RebirthProductiveEarnedMobileOrigin.Digest(mobileOriginal);
        }
        if(!(station is RebirthCamperStationState)&&
            !RebirthProductiveEarnedNativeBody.TryCapturePlan(station,afterTools,frame.RowIndex,target,
                out frame.BeforeNative,out frame.AfterNative,out frame.BeforeElapsedOffset,out frame.AfterElapsedOffset))return false;
        var value=new Attempt{Station=station,Row=row,Recipe=row.Recipe,Multiplier=row.Multiplier,Target=target,Tools=afterTools,Frame=frame,MobileOriginal=mobileOriginal};
        // Charge the detached frame/tool graph before retaining it across ticks.
        byte[] measuredImage=RebirthProductiveEarnedRowProof.Encode(frame);
        if(!RebirthProductiveEarnedObservationBudget.TryMeasureTools(frame.AfterTools,out long graph,out _)||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*measuredImage.Length+graph+(mobileOriginal?.LongLength??0)+
                (mobileOriginal==null?0:4L*RebirthStationSnapshotEvidence.MaximumSnapshotBytes+
                    2L*(RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace/3))),out value.Retention))return false;
        // Retain the exact operation even if publication fails; no fresh-ID retry.
        retained.Add(station,value);
        if(!RebirthProductiveEarnedArtifact.TryPrepare(station,frame,out var artifact))return false;
        value.Artifact=artifact;
        if(mobileOriginal!=null&&!RebirthProductiveEarnedMobileOrigin.Publish(artifact,mobileOriginal))return false;
        attempt=value;return true;
    }
    internal static void Applied(Attempt attempt)
    {
        if(!Enabled||attempt==null)return;
        if(GameManager.Instance?.World?.IsRemote()==true){RebirthProductiveEarnedRemoteClient.Applied(attempt);return;}
        if(!RebirthRecyclerNativeLoop.IsCurrentInvocation(attempt.Station)||attempt.Artifact?.MatchesAfter(attempt.Station)!=true)
            return; // Retain unknown; no completion permission.
        if(attempt.Station is RebirthCamperStationState mobile)
        {
            mobile.RestoreProductiveEarnedMarker(true,attempt.Artifact.Marker(attempt.Frame));mobile.setModified();
            attempt.MobileAwaitingReturn=true;return;
        }
        if(!(attempt.Station is RebirthCamperStationState))
            RebirthProductiveEarnedSavedNativeSource.RegisterPreparedOriginal(attempt.Artifact.OriginalPath,GameIO.GetSaveGameDir());
        ObserveInitialNativeApplied(attempt);
        attempt.Observation=RebirthProductiveEarnedPublication.BeginAfterNativeWork(attempt.Station,attempt.Artifact);
    }
    internal static bool AllowsCompletion(TileEntityWorkstation station,RecipeQueueItem row)
    {
        if(!Enabled)return true;ObserveWorld();
        if(retained.TryGetValue(station,out var terminalPending)&&terminalPending.OutputTimerObserved)
        {TryFinishNativeTerminal(terminalPending);return false;}
        if(RebirthProductiveEarnedRemoteClient.HasPending(station))return RebirthProductiveEarnedRemoteClient.AllowsNativeOutput(station,row);
        if(!retained.TryGetValue(station,out var active))
        {
            if(!TryNativeMarker(station,out _))return true;
            if(!TryBindCold(station,row,out active))return false;
        }
        if(!ReferenceEquals(active.Row,row)||row.Multiplier!=active.Multiplier||!ReferenceEquals(row.Recipe,active.Recipe)||
            !MatchesAfterCharged(active))return false;
        if(active.MobileAwaitingReturn||active.MobileReturnUnknown)return false;
        if(active.NativeAdoptionReleaseReady?.Invoke()==false)return false;
        if(active.RemoteSavedEarned!=null)
        {
            if(!active.RemoteSavedEarned())return false;
            active.NativeOutput=RebirthRecyclerNativeLoop.InvocationToken(station);return active.NativeOutput!=null;
        }
        if(active.Observation==null||!active.Observation.Current())
        {active.Observation?.Dispose();active.Observation=RebirthProductiveEarnedPublication.BeginAfterNativeWork(station,active.Artifact);}
        if(active.Observation?.Saved()!=true)return false;
        if(!(station is RebirthCamperStationState))
        {
            if(!EffectiveNativeAfter(active,out var nativeAfter,out int nativeOffset)||
                !RebirthProductiveEarnedSavedNativeSource.Publish(active.Artifact,active.Observation.Memory,active.Observation.Placed,
                    nativeAfter,nativeOffset,false,active.Observation.Current))return false;
        }
        active.NativeOutput=RebirthRecyclerNativeLoop.InvocationToken(station);return active.NativeOutput!=null;
    }
    private static bool TryBindCold(TileEntityWorkstation station,RecipeQueueItem row,out Attempt active)
    {
        active=null;if(row?.Recipe==null||retained.Count>=4)return false;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!TryNativeMarker(station,out _)||
                !RebirthProductiveEarnedArtifact.TryLoadObserved(station,out var artifact))return false;
            var frame=artifact.ReadFrame();
            if(frame.Multiplier!=row.Multiplier)return false;
            var value=new Attempt{Station=station,Row=row,Recipe=row.Recipe,Multiplier=row.Multiplier,Frame=frame,Artifact=artifact};
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*artifact.ImageLength+
                (station is RebirthCamperStationState?4L*RebirthStationSnapshotEvidence.MaximumSnapshotBytes+
                    2L*(RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace/3):0)),out value.Retention))return false;
            retained.Add(station,value);active=value;return true;
        }
    }
    private static bool MatchesAfterCharged(Attempt active)
    {
        if(active?.Observation!=null)return active.Observation.Current();
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            return decoder!=null&&active?.Artifact?.MatchesAfter(active.Station)==true;
    }
    internal static void ObserveEntered(TileEntityWorkstation station)
    {
        if(GameManager.Instance?.World?.IsRemote()==true){RebirthProductiveEarnedRemoteClient.ApplyNativePendingOnEnter(station);return;}
        if(!Enabled||!RebirthRecyclerNativeLoop.IsCurrentInvocation(station)||!TryNativeMarker(station,out _))return;
        if(TryRetireObservedNativeTerminal(station))return;
        TryRestorePendingNativeTerminal(station);
        // Resume the already-returned terminal under this real new invocation.
        // The old NativeOutput token is never rebound or used as another output
        // permit; saved successor application retains its separate gates.
        if(retained.TryGetValue(station,out var pending)&&pending.OutputTimerObserved&&pending.Terminal!=null)
            TryFinishNativeTerminal(pending);
        var row=station.Queue?.LastOrDefault();if(row?.Recipe==null)return;
        AllowsCompletion(station,row); // Starts/observes only; native effect remains its own gate.
    }
    internal static bool OwnsEarnedRow(TileEntityWorkstation station,RecipeQueueItem row)
    {
        if(GameManager.Instance?.World?.IsRemote()==true)return RebirthProductiveEarnedRemoteClient.OwnsEarnedRow(station,row);
        if(!Enabled||row==null)return false;
        if(!retained.TryGetValue(station,out var active)&&!TryBindCold(station,row,out active))return false;
        if(active.Artifact==null||!ReferenceEquals(active.Row,row)||!ReferenceEquals(active.Recipe,row.Recipe)||active.Multiplier!=row.Multiplier||
            !MatchesAfterCharged(active)||!TryNativeMarker(station,out var marker))return false;
        return active.Artifact.MatchesMarker(marker);
    }
    internal static bool HasSavedEarned(TileEntityWorkstation station,RecipeQueueItem row)
        =>Enabled&&(GameManager.Instance?.World?.IsRemote()==true?RebirthProductiveEarnedRemoteClient.HasSavedEarned(station,row):
            retained.TryGetValue(station,out var active)&&ReferenceEquals(active.Row,row)&&
            active.Multiplier==row.Multiplier&&ReferenceEquals(active.Recipe,row.Recipe)&&active.NativeAdoptionReleaseReady?.Invoke()!=false&&
            (active.RemoteSavedEarned?.Invoke()==true||active.Observation?.Saved()==true));
    internal static void BeforeNativeTimerWrite(TileEntityWorkstation station,RecipeQueueItem row)
    {
        RebirthProductiveEarnedRemoteClient.ObserveNativeOutputTimer(station,row);
        if(!Enabled||!retained.TryGetValue(station,out var active)||active.NativeOutput==null||
            !ReferenceEquals(active.NativeOutput,RebirthRecyclerNativeLoop.InvocationToken(station))||
            !ReferenceEquals(active.Row,row)||!ReferenceEquals(active.Recipe,row.Recipe)||row.Multiplier!=active.Multiplier-1)return;
        // Native insertion AND AddCraftComplete returned, then this exact row
        // decremented in the admitted invocation. Keep its marker/original until
        // the entire resulting native station (including receipt list) is saved.
        active.OutputTimerObserved=true;
    }
    internal static bool TryCancellationOriginal(TileEntityWorkstation station,Recipe recipe,int multiplier,
        out RebirthProductiveEarnedArtifact.Prepared artifact,out RebirthProductiveEarnedRowProof.Frame frame)
    {
        artifact=null;frame=null;if(!Enabled||recipe==null||multiplier<1||!TryNativeMarker(station,out var marker))return false;
        if(!RebirthProductiveEarnedRowProof.TryReadMarker(marker,out var birth,out var operation)||
            !RebirthProductiveEarnedArtifact.TryLoadAfter(station,birth,operation,marker,out artifact))return false;
        frame=artifact.ReadFrame();
        var row=station.Queue?.LastOrDefault();
        return row?.Recipe!=null&&row.Multiplier==multiplier&&ReferenceEquals(row.Recipe,recipe)&&frame.Multiplier==multiplier;
    }
    internal static bool RetireNativeCancellation(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact)
    {
        if(!Enabled||artifact==null||!RebirthProductiveEarnedCancellation.IsSyncedNativeCancellation(station,artifact))return false;
        var frame=artifact.ReadFrame();
        if(retained.TryGetValue(station,out var active)&&active.Frame.Operation!=frame.Operation)return false;
        if(!ClearExactMarker(station,artifact.Marker(frame)))return false;
        if(active!=null){active.Observation?.Dispose();retained.Remove(station);active.Tools=null;active.Frame=null;active.PendingDefinitionRetention?.Dispose();active.Retention?.Dispose();active.Retention=null;}
        artifact.RetainCompleted();return true;
    }
    internal static bool TryNativeMarker(TileEntityWorkstation station,out byte[] marker)
    {
        marker=null;
        if(station is RebirthCamperStationState mobile)
        {var bytes=mobile.CopyProductiveEarnedMarker();if(bytes.Length==0)return false;marker=bytes;return true;}
        var chunk=station?.GetChunk();if(chunk==null)return false;
        var p=station.localChunkPos;string key="RebirthProductiveEarnedV1:"+p.x+":"+p.y+":"+p.z;
        if(!chunk.ChunkCustomData.dict.TryGetValue(key,out var value))return false;
        // A malformed existing marker remains a blocking unknown.
        marker=value?.data==null?Array.Empty<byte>():(byte[])value.data.Clone();return true;
    }
    private static bool ClearExactMarker(TileEntityWorkstation station,byte[] expected)
    {
        if(!TryNativeMarker(station,out var actual)||!actual.SequenceEqual(expected))return false;
        if(station is RebirthCamperStationState mobile)
        {mobile.RestoreProductiveEarnedMarker(true,Array.Empty<byte>());mobile.setModified();return mobile.CopyProductiveEarnedMarker().Length==0;}
        var p=station.localChunkPos;var chunk=station.GetChunk();string key="RebirthProductiveEarnedV1:"+p.x+":"+p.y+":"+p.z;
        if(!chunk.ChunkCustomData.dict.TryGetValue(key,out var marker)||marker.TriggerWriteDataDelegate!=null||
            marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork)return false;
        chunk.ChunkCustomData.Remove(key);chunk.isModified=true;station.setModified();return !chunk.ChunkCustomData.dict.ContainsKey(key);
    }
    internal static bool RetireUiTerminalMarker(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact,
        RebirthProductiveEarnedUiRemote.TerminalPublication proof)
    {
        if(artifact==null||proof==null||artifact.Operation!=proof.Operation||artifact.Birth!=proof.Birth||!proof.Matches(station))return false;
        var frame=artifact.ReadFrame();return ClearExactMarker(station,artifact.Marker(frame));
    }
}
