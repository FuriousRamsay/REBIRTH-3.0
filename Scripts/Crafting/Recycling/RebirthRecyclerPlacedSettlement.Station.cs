using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // Authority-side native publication, independent of owner-file publication.
    // Neither a raw client after image nor a timer retry can mint Saved().
    internal sealed partial class StationPublication:IDisposable
    {
        internal readonly IStationOriginal Original;
        private readonly RebirthRemoteResourceNativeBinding binding;
        private readonly Chunk chunk;private readonly string markerKey;private readonly ChunkCustomData marker;
        private readonly byte[] markerImage;private RebirthRemoteResourceNativePublication.Watch watch;
        private bool after,closed,attempted,unknown,chunkOwned,cleanupAttempted,cleanupRemoved;private string path,beforePath;private byte[] receipt,beforeReceipt;
        private RebirthRemoteResourceNativePublication.Watch cleanupWatch;
        private IDisposable beforeSpanMemory,afterSpanMemory;
        private LoadedStation cold;
        private bool unusedBefore;
        private bool afterPublished;
        internal bool AfterApplied=>after;
        internal bool RetainedSavedCustody(RemoteHostOriginal source)=>ReferenceEquals(Original,source)&&!closed&&!unknown&&!cleanupAttempted&&
            source.SavedOwnerOriginCurrent()&&source.NativeLineageCurrent()&&binding.Current()&&ReferenceEquals(source.Station.GetChunk(),chunk)&&
            ReferenceEquals(source.World.GetChunkSync(chunk.X,chunk.Z),chunk)&&chunk.ChunkCustomData.dict.TryGetValue(markerKey,out var actual)&&ReferenceEquals(actual,marker)&&
            actual.TriggerWriteDataDelegate==null&&actual.expiresInWorldTime==ulong.MaxValue&&!actual.isSavedToNetwork&&actual.data.SequenceEqual(markerImage)&&
            source.AcceptedStation==after&&RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,after?source.AfterNative:source.BeforeNative,after?source.AfterOffset:source.BeforeOffset)&&
            (!after||afterPublished)&&receipt!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,receipt);
        internal bool MarkerRemoved=>cleanupRemoved;
        internal XElement CopyRetainedSavedCustody(RemoteHostOriginal source)=>RetainedSavedCustody(source)?new XElement("actualRetainedStationSaved",
            new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("stage",after?"after":"before"),new XAttribute("path",path),
            new XAttribute("length",receipt.Length),new XAttribute("digest",RebirthStationPublicationRecord.Digest(receipt)),new XAttribute("birth",Birth)):null;
        internal string Birth=>binding.Incarnation;
        private StationPublication(IStationOriginal original,RebirthRemoteResourceNativeBinding binding,Chunk chunk,string key,ChunkCustomData marker)
        {Original=original;this.binding=binding;this.chunk=chunk;markerKey=key;this.marker=marker;markerImage=(byte[])marker.data.Clone();}
        private bool Current()=>!closed&&(after?Original.NativeLineageCurrent():Original.Current())&&binding.Current()&&ReferenceEquals(Original.Station.GetChunk(),chunk)&&
            ReferenceEquals(Original.World.GetChunkSync(chunk.X,chunk.Z),chunk)&&chunk.ChunkCustomData.dict.TryGetValue(markerKey,out var current)&&
            ReferenceEquals(current,marker)&&current.TriggerWriteDataDelegate==null&&current.expiresInWorldTime==ulong.MaxValue&&!current.isSavedToNetwork&&
            System.Linq.Enumerable.SequenceEqual(current.data,markerImage)&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Station,after?Original.AfterNative:Original.BeforeNative,after?Original.AfterOffset:Original.BeforeOffset);
        internal static StationPublication BeginBefore(IStationOriginal original)
        {
            if(original?.World?.IsRemote()!=false||original.Current()!=true||
                !RebirthLogisticsEndpointIdentity.TryGetIncarnation(original.World,original.Station,out string birth)||
                !RebirthRemoteResourceNativeBinding.TryResolve(original.World,RemoteResourceIdentity.Workstation(original.Station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,birth,out var binding)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,original.BeforeNative,original.BeforeOffset))return null;
            var chunk=original.Station.GetChunk();if(chunk==null||chunk.InProgressUnloading)return null;
            string key="RebirthRecyclerPlacedV1:"+original.Station.localChunkPos.x+":"+original.Station.localChunkPos.y+":"+original.Station.localChunkPos.z+":"+original.Operation.ToString("N");
            // Reentry has a separate actual native-load issuer. An existing
            // marker cannot be adopted by equality or replaced by this API.
            if(chunk.ChunkCustomData.dict.ContainsKey(key))return null;
            var image=new XElement("recyclerPlacedNativeOriginal",new XAttribute("operation",original.Operation.ToString("N")),
                new XAttribute("birth",birth),new XAttribute("owner",original.Owner),new XAttribute("creation",original.Creation),new XAttribute("deaths",original.Deaths),
                new XAttribute("original",RebirthWorkstationCompletionOriginal.Hash(Encoding.UTF8.GetString(original.Image))));
            var nativeMarker=new ChunkCustomData(key,ulong.MaxValue,false){data=Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting))};
            chunk.ChunkCustomData.Set(key,nativeMarker);chunk.isModified=true;
            var result=new StationPublication(original,binding,chunk,key,nativeMarker);
            result.watch=RebirthRemoteResourceNativePublication.Begin(binding,result.Current,key);
            // A busy snapshot leaves this same privately issued marker/source
            // retained. Do not abandon it and later equality-adopt the marker.
            return result;
        }
        internal bool SavedBefore()
        {
            if(after||!Publish())return false;beforePath=path;beforeReceipt=(byte[])receipt.Clone();return true;
        }
        internal static StationPublication FromActualCold(IStationOriginal original,LoadedStation loaded)
        {
            if(original?.Current()!=true||loaded?.Current()!=true||loaded.Operation!=original.Operation||!ReferenceEquals(loaded.Station,original.Station)||
                !RebirthLogisticsEndpointIdentity.TryGetIncarnation(original.World,original.Station,out string birth)||
                !RebirthRemoteResourceNativeBinding.TryResolve(original.World,RemoteResourceIdentity.Workstation(original.Station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,birth,out var binding)||
                !loaded.Chunk.ChunkCustomData.dict.TryGetValue(loaded.MarkerKey,out var marker)||!marker.data.SequenceEqual(loaded.MarkerImage))return null;
            var result=new StationPublication(original,binding,loaded.Chunk,loaded.MarkerKey,marker){after=loaded.Stage=="after",afterPublished=loaded.Stage=="after",cold=loaded,path=loaded.ReceiptPath};
            // The loaded source already owns the full saved receipt/read graph.
            // Copy only after its measured primary length is charged here.
            long length=RebirthWorkstationCompletionPublication.PublishedLength(result.path);
            if(length<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+65536),out var memory))return null;
            if(result.after)result.afterSpanMemory=memory;else result.beforeSpanMemory=memory;
            result.receipt=loaded.CopyReceipt();if(result.receipt==null||!result.Current()){result.Dispose();return null;}
            return result;
        }
        internal bool ApplyLocalAfter(OwnerPublication owner)
        {
            if(Original is not RebirthRecyclerPlacedSettlement.Original local||Original.World.IsRemote()||attempted||unknown||!ReferenceEquals(owner?.Original,Original)||!owner.BeforeCheckpointSaved||
                beforeReceipt==null||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(beforePath,beforeReceipt)||!Current())return false;
            lock(chunk)
            {
                if(chunk.InProgressSaving||chunk.IsLocked||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)return false;
                chunk.InProgressSaving=true;chunkOwned=true;
            }
            try
            {
                var attempt=new XElement("workstationCompletionIntent",new XAttribute("operation",Original.Operation.ToString("N")),new XAttribute("world",Original.WorldKey),
                    new XElement("before",XElement.Parse(Encoding.UTF8.GetString(Original.Image))),new XElement("recyclerStationAttempted",new XElement("savedNativeBefore",Convert.ToBase64String(beforeReceipt))));
                string attemptPath=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),Original.Operation.ToString("N")+".station-attempted.xml");
                if(!RebirthWorkstationCompletionPublication.Write(attemptPath,attempt)||!XNode.DeepEquals(attempt,RebirthWorkstationCompletionPublication.ReadPrimary(attemptPath)))
                {ReleaseChunk();return false;}
                if(!Current()||!owner.BeforeCheckpointSaved){ReleaseChunk();return false;}
                attempted=true;watch?.Dispose();watch=null;
                var station=Original.Station;station.SetDisableModifiedCheck(true);
                station.Input=local.Input;station.Output=local.Output;station.Tools=local.Tools;station.Queue=local.Queue;
                if(local.Cycle)station.cycleRecipeQueue();
                station.ResetTickTime();
                if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,Original.AfterNative,Original.AfterOffset)){unknown=true;return false;}
                after=true;cold=null;station.SetDisableModifiedCheck(false);ReleaseChunk();station.setModified();
                watch=RebirthRemoteResourceNativePublication.Begin(binding,Current,markerKey);return watch!=null;
            }
            catch{unknown=true;return false;}
        }
        internal bool SavedAfter()=>after&&(Publish()||SavedAfterCustodyCurrent());
        // Historical native publication can finish terminal metadata after
        // the original actor disconnects or dies. This never prepares a new
        // station/owner mutation and never treats an unsaved After as saved.
        private bool SavedAfterCustodyCurrent()=>afterPublished&&!closed&&!unknown&&!cleanupAttempted&&receipt!=null&&
            Original.NativeLineageCurrent()&&binding.Current()&&ReferenceEquals(Original.Station.GetChunk(),chunk)&&
            chunk.ChunkCustomData.dict.TryGetValue(markerKey,out var actual)&&ReferenceEquals(actual,marker)&&
            actual.TriggerWriteDataDelegate==null&&actual.expiresInWorldTime==ulong.MaxValue&&!actual.isSavedToNetwork&&actual.data.SequenceEqual(markerImage)&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Station,Original.AfterNative,Original.AfterOffset)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,receipt);
        internal bool CanObserveAcceptedUpload(RemoteHostOriginal source)=>ReferenceEquals(Original,source)&&!closed&&!unknown&&!cleanupAttempted&&
            source.NativeLineageCurrent()&&binding.Current()&&ReferenceEquals(source.Station.GetChunk(),chunk)&&
            chunk.ChunkCustomData.dict.TryGetValue(markerKey,out var actual)&&ReferenceEquals(actual,marker)&&actual.data.SequenceEqual(markerImage)&&
            actual.TriggerWriteDataDelegate==null&&actual.expiresInWorldTime==ulong.MaxValue&&!actual.isSavedToNetwork&&beforeReceipt!=null&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(beforePath,beforeReceipt)&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.AfterNative,source.AfterOffset);
        internal bool ObserveRemoteAccepted(AcceptedStationUpload accepted)
        {
            if(after)return attempted&&!closed&&!unknown&&ReferenceEquals(accepted?.Source,Original)&&accepted.Current();
            if(attempted||closed||!ReferenceEquals(accepted?.Source,Original)||!accepted.Current()||beforeReceipt==null||
                !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(beforePath,beforeReceipt))return false;
            attempted=true;after=true;cold=null;watch?.Dispose();watch=null;
            watch=RebirthRemoteResourceNativePublication.Begin(binding,Current,markerKey);return true;
        }
        internal XElement CopySavedAfter()=>SavedAfter()?XElement.Parse(Encoding.UTF8.GetString(receipt)):null;
        internal bool RetireUnusedBefore(RemoteHostOriginal source)
        {
            if(!ReferenceEquals(Original,source)||!source.UnusedRequested||after||attempted||unknown||closed)return false;
            if(cleanupRemoved)return unusedBefore&&CleanupSaved();
            if(cleanupAttempted||!RetainedSavedCustody(source))return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldKey),
                new XElement("before",XElement.Parse(Encoding.UTF8.GetString(source.Image))),new XElement("recyclerUnusedMarkerAttempted",CopyRetainedSavedCustody(source)));
            string target=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".unused-marker-attempted.xml");
            if(!RebirthWorkstationCompletionPublication.Write(target,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(target)))return false;
            lock(chunk)
            {
                if(chunk.InProgressSaving||chunk.IsLocked||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)return false;
                chunk.InProgressSaving=true;chunkOwned=true;
            }
            try
            {
                unusedBefore=true;cleanupAttempted=true;watch?.Dispose();watch=null;
                chunk.ChunkCustomData.Remove(markerKey);chunk.isModified=true;cleanupRemoved=!chunk.ChunkCustomData.dict.ContainsKey(markerKey);
            }
            finally
            {
                if(source.NativeLineageCurrent()&&RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.BeforeNative,source.BeforeOffset)&&
                    (!chunk.ChunkCustomData.dict.TryGetValue(markerKey,out var remaining)||ReferenceEquals(remaining,marker)&&remaining.data.SequenceEqual(markerImage)))ReleaseChunk();
            }
            return CleanupSaved();
        }
        private bool CleanupCurrent()=>!closed&&cleanupRemoved&&Original.NativeLineageCurrent()&&binding.Current()&&
            ReferenceEquals(Original.Station.GetChunk(),chunk)&&!chunk.ChunkCustomData.dict.ContainsKey(markerKey);
        internal bool CleanupSaved()
        {
            if(!CleanupCurrent())return false;
            if(cleanupWatch==null||!cleanupWatch.Current())
            {cleanupWatch?.Dispose();cleanupWatch=RebirthRemoteResourceNativePublication.Begin(binding,CleanupCurrent);}
            return cleanupWatch?.TryReadPublishedTileDescriptor(out _,out _,out _,out _)==true;
        }
        internal XElement CopyCleanupSaved()
        {
            if(!CleanupSaved()||!cleanupWatch.TryReadPublishedTileDescriptor(out var proof,out long offset,out int length,out string digest))return null;
            return new XElement("recyclerCleanupSaved",new XAttribute("birth",Birth),new XAttribute("path",proof.Path),new XAttribute("payload",proof.PayloadDigest),
                new XAttribute("tileOffset",offset),new XAttribute("tileLength",length),new XAttribute("tileDigest",digest),
                new XAttribute("marker",RebirthStationPublicationRecord.Digest(markerImage)));
        }
        internal bool RetireTerminalMarker(string terminalPath,byte[] terminal)
        {
            if(cleanupRemoved)return CleanupSaved();
            if(cleanupAttempted||!after||terminal==null||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(terminalPath,terminal)||
                !Original.NativeLineageCurrent()||!binding.Current()||!chunk.ChunkCustomData.dict.TryGetValue(markerKey,out var actual)||!ReferenceEquals(actual,marker)||
                !System.Linq.Enumerable.SequenceEqual(marker.data,markerImage)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Station,Original.AfterNative,Original.AfterOffset))return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Original.Operation.ToString("N")),new XAttribute("world",Original.WorldKey),
                new XElement("before",XElement.Parse(Encoding.UTF8.GetString(Original.Image))),new XElement("recyclerTerminalMarkerRetirement",
                    new XElement("terminal",Convert.ToBase64String(terminal)),new XElement("marker",Convert.ToBase64String(markerImage))));
            string cleanupPath=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),Original.Operation.ToString("N")+".cleanup.xml");
            if(!RebirthWorkstationCompletionPublication.Write(cleanupPath,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(cleanupPath)))return false;
            // The actual Remove is metadata-only and follows both saved legs.
            // Its private event, not a later absent-marker lookup, owns retry.
            lock(chunk)
            {
                if(chunk.InProgressSaving||chunk.IsLocked||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)return false;
                chunk.InProgressSaving=true;chunkOwned=true;
            }
            try
            {
                cleanupAttempted=true;watch?.Dispose();watch=null;
                chunk.ChunkCustomData.Remove(markerKey);chunk.isModified=true;cleanupRemoved=!chunk.ChunkCustomData.dict.ContainsKey(markerKey);
            }
            finally
            {
                if(Original.NativeLineageCurrent()&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Station,Original.AfterNative,Original.AfterOffset)&&
                    (!chunk.ChunkCustomData.dict.TryGetValue(markerKey,out var remaining)||ReferenceEquals(remaining,marker)&&System.Linq.Enumerable.SequenceEqual(remaining.data,markerImage)))ReleaseChunk();
            }
            return CleanupSaved();
        }
        private bool Publish()
        {
            if(!Current())return false;
            if(cold!=null)return cold.Current()&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,receipt);
            if(watch==null||!watch.Current())
            {watch?.Dispose();watch=RebirthRemoteResourceNativePublication.Begin(binding,Current,markerKey);}
            if(watch==null||!watch.TryReadPublishedTileDescriptor(out var native,out long offset,out int length,out string digest))return false;
            var nativeMarker=watch.Markers?.SingleOrDefault(value=>value.Key==markerKey);
            if(watch.TileSpan==null||watch.TileSpan.Length!=length||nativeMarker?.Span==null||
                !nativeMarker.Bytes.SequenceEqual(markerImage))return false;
            // Descriptor publication already verified these exact native spans.
            // Retain them, rather than later hydrating an equal current TE or
            // requiring an unrelated region-file rewrite to preserve offsets.
            long measured=checked(24L*(length+nativeMarker.Span.Length)+65536);
            var spanMemory=after?afterSpanMemory:beforeSpanMemory;
            if(spanMemory==null)
            {
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(measured,out spanMemory))return false;
                if(after)afterSpanMemory=spanMemory;else beforeSpanMemory=spanMemory;
            }
            else if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(spanMemory,measured))return false;
            var tile=(byte[])watch.TileSpan.Clone();var markerSpan=(byte[])nativeMarker.Span.Clone();
            if(RebirthStationPublicationRecord.Digest(tile)!=digest||!watch.TryReadPublishedTileDescriptor(out var confirmed,out long confirmedOffset,out int confirmedLength,out string confirmedDigest)||
                !ReferenceEquals(native,confirmed)||offset!=confirmedOffset||length!=confirmedLength||digest!=confirmedDigest||
                !nativeMarker.Span.SequenceEqual(markerSpan)||!nativeMarker.Bytes.SequenceEqual(markerImage))return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Original.Operation.ToString("N")),new XAttribute("world",Original.WorldKey),
                new XElement("before",XElement.Parse(Encoding.UTF8.GetString(Original.Image))),
                new XElement("recyclerPlacedStationSaved",new XAttribute("stage",after?"after":"before"),new XAttribute("path",native.Path),
                    new XAttribute("payload",native.PayloadDigest),new XAttribute("tileOffset",offset),new XAttribute("tileLength",length),new XAttribute("tileDigest",digest),
                    new XElement("marker",Convert.ToBase64String(markerImage)),new XElement("native",Convert.ToBase64String(after?Original.AfterNative:Original.BeforeNative)),
                    new XElement("nativePersistency",Convert.ToBase64String(tile)),new XElement("nativeMarkerSpan",Convert.ToBase64String(markerSpan))));
            path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),Original.Operation.ToString("N")+(after?".station-after.xml":".station-before.xml"));
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return false;
            receipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
            if(receipt==null||!Current())return false;
            if(after)afterPublished=true;return true;
        }
        private void ReleaseChunk(){if(!chunkOwned)return;lock(chunk)chunk.InProgressSaving=false;chunkOwned=false;}
        public void Dispose(){if(closed)return;closed=true;watch?.Dispose();cleanupWatch?.Dispose();watch=cleanupWatch=null;receipt=beforeReceipt=null;
            beforeSpanMemory?.Dispose();afterSpanMemory?.Dispose();beforeSpanMemory=afterSpanMemory=null;ReleaseChunk();}
    }
}
