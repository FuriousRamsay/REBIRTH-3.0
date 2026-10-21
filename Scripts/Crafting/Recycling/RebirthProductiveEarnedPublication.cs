using System;
using System.Linq;

// Row-owned native evidence. The inactive coordinator calls this adapter; it does not produce output or
// supply a payment/owner mutation permit. Saved() is meaningful only alongside
// the original typed artifact and exact live After row/tool images.
internal static class RebirthProductiveEarnedPublication
{
    internal sealed class Observation : IDisposable
    {
        internal TileEntityWorkstation Station;
        internal RebirthProductiveEarnedArtifact.Prepared Artifact;
        internal byte[] Marker;
        internal Chunk Chunk;
        internal string MarkerKey;
        internal ChunkCustomData NativeMarker;
        internal RebirthRemoteResourceNativePublication.Watch Placed;
        internal RebirthCamperStationSaveEvidence.Request Mobile;
        internal RebirthCamperStationSaveEvidence.Publication MobilePublished;
        internal bool Closed;
        internal RebirthProductiveEarnedObservationBudget Memory;
        internal RebirthProductiveEarnedObservationBudget.Observation Allocation;
        internal Func<bool> Additional;
        internal byte[] NativeAfter;internal int NativeAfterOffset;
        internal bool Current()
        {
            using(var allocation=Memory?.TryEnterCallbacks())
            {if(allocation==null)return false;return CurrentCore();}
        }
        private bool CurrentCore()
        {
            try
            {
                if(Closed||!ThreadManager.IsMainThread()||Artifact?.MatchesAfter(Station)!=true||Additional!=null&&!Additional())return false;
                if(Station is RebirthCamperStationState mobile)
                    return mobile.ProductiveEarnedV5&&mobile.CopyProductiveEarnedMarker().SequenceEqual(Marker);
                return NativeAfter!=null&&RebirthProductiveEarnedCoordinator.PublicationNativeAfter(Station,Artifact,Artifact.ReadFrame(),out var expected,out int expectedOffset)&&
                    expectedOffset==NativeAfterOffset&&expected.SequenceEqual(NativeAfter)&&
                    RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,NativeAfter,NativeAfterOffset)&&
                    Chunk!=null&&!Chunk.InProgressUnloading&&ReferenceEquals(Station.GetChunk(),Chunk)&&
                    Chunk.ChunkCustomData.dict.TryGetValue(MarkerKey,out var value)&&ReferenceEquals(value,NativeMarker)&&
                    value.TriggerWriteDataDelegate==null&&!value.isSavedToNetwork&&value.expiresInWorldTime==ulong.MaxValue&&
                    value.data!=null&&value.data.SequenceEqual(Marker);
            }
            catch{return false;}
        }
        internal bool Saved()
        {try{return SavedCore();}catch{return false;}}
        private bool SavedCore()
        {
            if(!Current())return false;
            if(Placed!=null)return Placed.TryReadPublishedTileDescriptor(out _,out _,out _,out _)&&Current();
            if(MobilePublished==null)
            {if(Mobile==null||!RebirthCamperStationSaveEvidence.TryObserve(Mobile,out var saved))return false;MobilePublished=saved;}
            using(var callbacks=Memory?.TryEnterCallbacks())
            {
                if(callbacks==null)return false;
                var frame=Artifact.ReadFrame();
                return MobilePublished.ReadStationSnapshot(restored=>
                    restored.ProductiveEarnedV5&&restored.CopyProductiveEarnedMarker().SequenceEqual(Marker)&&
                    restored.Queue!=null&&frame.RowIndex==restored.Queue.Length-1&&
                    RebirthProductiveEarnedRowProof.CaptureRow(restored.Queue[frame.RowIndex]).SequenceEqual(frame.AfterRow)&&
                    RebirthProductiveEarnedArtifact.SameTools(restored.Tools,frame.AfterTools))&&Current();
            }
        }
        public void Dispose()
        {
            if(Closed)return;
            // Mobile dirty cleanup can fail and must be retryable with the same
            // request/charged graphs, rather than marking a leaked scope closed.
            Placed?.Dispose();Mobile?.Dispose();MobilePublished?.Dispose();MobilePublished=null;
            Closed=true;Allocation?.Dispose();Memory?.Dispose();
            // Disposal releases transient observation only. Never erase the
            // native marker/artifact or infer a no-effect/output terminal.
        }
    }
    internal static Observation BeginAfterNativeWork(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact)
    {
        if(!ThreadManager.IsMainThread()||
            !(RebirthRecyclerNativeLoop.IsCurrentInvocation(station)||RebirthProductiveEarnedRemoteHost.IsAcceptedUpload(station,artifact)||
                RebirthProductiveEarnedCoordinator.IsAdoptedNativeChildPublication(station,artifact)||
                RebirthProductiveEarnedCoordinator.IsMobileReturnedPublication(station,artifact)||
                RebirthProductiveEarnedCoordinator.IsInitialNativeSuffixPublication(station,artifact)))return null;
        RebirthProductiveEarnedRowProof.Frame frame;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {if(decoder==null||artifact?.MatchesAfter(station)!=true)return null;frame=artifact.ReadFrame();}
        byte[] marker=artifact.Marker(frame);
        var result=new Observation{Station=station,Artifact=artifact,Marker=marker,
            Additional=RebirthProductiveEarnedRemoteHost.AcceptedCurrent(station,artifact)};
        try
        {
            if(station is RebirthCamperStationState mobile)
            {
#if REBIRTH_CAMPER_DIAGNOSTICS
                return null;
#else
                result.Memory=RebirthProductiveEarnedObservationBudget.TryOpen(artifact,frame);
                if(result.Memory==null){result.Dispose();return null;}
                result.Allocation=result.Memory.TryObserveMobile(mobile);
                if(result.Allocation==null){result.Dispose();return null;}
                var existing=mobile.CopyProductiveEarnedMarker();
                if(existing.Length!=0&&!existing.SequenceEqual(marker)){result.Dispose();return null;}
                mobile.RestoreProductiveEarnedMarker(true,marker);mobile.setModified();
                var runtime=RebirthCamperModuleRuntime.For(mobile.BoundVehicle);
                var address=new RebirthCamperStationAddress(runtime.Identity,mobile.InstallationId);
                result.Mobile=RebirthCamperStationSaveEvidence.BeginProcessingRecovery(mobile,address,result.Current,result.Memory);
#endif
            }
            else
            {
                var world=GameManager.Instance?.World;
                if(!RebirthRemoteResourceNativeBinding.TryResolve(world,RemoteResourceIdentity.Workstation(station.ToWorldPos()),
                    RemoteResourceSourceKind.WorkstationOutput,artifact.CustodyBirth.ToString("N"),out var binding))return null;
                result.Chunk=station.GetChunk();
                var local=station.localChunkPos;
                result.MarkerKey="RebirthProductiveEarnedV1:"+local.x+":"+local.y+":"+local.z;
                if(result.Chunk.ChunkCustomData.dict.TryGetValue(result.MarkerKey,out var existing))
                {
                    if(existing.TriggerWriteDataDelegate!=null||existing.isSavedToNetwork||existing.expiresInWorldTime!=ulong.MaxValue||
                        existing.data==null||!existing.data.SequenceEqual(marker))return null;
                    result.NativeMarker=existing;
                }
                else
                {
                    result.NativeMarker=new ChunkCustomData(result.MarkerKey,ulong.MaxValue,false){data=(byte[])marker.Clone()};
                    result.Chunk.ChunkCustomData.Set(result.MarkerKey,result.NativeMarker);result.Chunk.isModified=true;station.setModified();
                }
                result.Memory=RebirthProductiveEarnedObservationBudget.TryOpen(artifact,frame);
                if(result.Memory==null){result.Dispose();return null;}
                if(!RebirthProductiveEarnedCoordinator.PublicationNativeAfter(station,artifact,frame,out result.NativeAfter,out result.NativeAfterOffset))
                {result.Dispose();return null;}
                result.Allocation=result.Memory.TryObserveNative(result.NativeAfter,result.NativeAfterOffset);
                if(result.Allocation==null){result.Dispose();return null;}
                RebirthRemoteResourceNativePublication.Install();
                result.Placed=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,result.Current,result.Memory,result.MarkerKey);
                if(result.Placed==null){result.Dispose();return null;}
            }
            if(!result.Current()){result.Dispose();return null;}return result;
        }
        catch{result.Dispose();return null;}
    }
}
