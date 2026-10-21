using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// Exact native persistence observations only. The original source lease owns
// mutation permission separately; no observer can write an inventory or receipt.
internal static class RebirthRemoteResourceNativePublication
{
    private static bool installed;
    private static readonly List<Watch> pending=new List<Watch>();
    private static readonly object publicationGate=new object();
    internal static int ActiveSnapshotWatchCount{get{lock(publicationGate)return pending.Count(watch=>!watch.Closed&&watch.ChunkProof!=null);}}
    private static readonly object observationBudgetGate=new object();
    private static long observationBytes;
    private const long MaximumObservationBytes=2L*RebirthStationSnapshotEvidence.MaximumRequests*RebirthStationSnapshotEvidence.MaximumSnapshotBytes;
    private sealed class ObservationAllocation:IDisposable
    {
        internal long Bytes;internal IDisposable Shared;private int closed;
        public void Dispose(){if(System.Threading.Interlocked.Exchange(ref closed,1)!=0)return;try{Shared?.Dispose();}finally{lock(observationBudgetGate)observationBytes-=Bytes;}}
    }
    private static bool ReserveObservation(RebirthStationSnapshotEvidence.Request request,long bytes,out IDisposable allocation)
    {
        allocation=null;if(bytes<1||bytes>MaximumObservationBytes)return false;IDisposable shared=null;
        if(request?.Budget!=null&&!request.Budget.TryReserveAllocation(bytes,out shared))return false;
        lock(observationBudgetGate)
        {
            if(observationBytes>MaximumObservationBytes-bytes){shared?.Dispose();return false;}
            observationBytes+=bytes;allocation=new ObservationAllocation{Bytes=bytes,Shared=shared};return true;
        }
    }
    private static void Track(Watch watch){lock(publicationGate)pending.Add(watch);}
    internal sealed class Watch:IDisposable
    {
        internal RebirthRemoteResourceNativeBinding Source;internal RemoteResourceTransactions.ActionPayment ElectricalOriginal;internal Func<bool> Original;
        internal ItemStack[] Items;internal bool[] Locks;internal DateTime Expires;internal bool Closed;
        internal Chunk Chunk;internal TileEntity Tile;internal Marker[] Markers;
        internal RebirthStationSnapshotEvidence.Request ChunkProof;internal byte[] TileSpan;internal long TileOffset=-1;
        private byte[] nativePayload;
        private IDisposable payloadAllocation;
        internal void RememberPayload(byte[] bytes,IDisposable allocation)
        {var copy=(byte[])bytes.Clone();var old=payloadAllocation;nativePayload=copy;payloadAllocation=allocation;old?.Dispose();}
        internal RebirthLogisticsVehiclePublication.Expected Vehicle;
        internal RebirthLogisticsDronePublication.Expected Drone;
        internal bool Current()
        {
            try
            {
            if(Closed||!ThreadManager.IsMainThread()||Expires<=DateTime.UtcNow||Original?.Invoke()!=true)return false;
            if(ElectricalOriginal!=null){if(!RemoteResourceTransactions.OwnsElectricalTargetObservation(ElectricalOriginal)||!ElectricalOriginal.TargetCurrent())return false;}
            else{if(Source?.Current()!=true||!Same(Source.CopySlots(),Items))return false;var locks=Source.CopyLocks();if(locks==null||!locks.SequenceEqual(Locks))return false;}
            var actualWorld=ElectricalOriginal?.World??Source?.World;
            if(Tile!=null&&(Chunk==null||Chunk.InProgressUnloading||!ReferenceEquals(Tile.GetChunk(),Chunk)||
                !ReferenceEquals(actualWorld.GetChunkSync(Chunk.X,Chunk.Z),Chunk)))return false;
            if(Markers!=null)foreach(var marker in Markers)
                if(!Chunk.ChunkCustomData.dict.TryGetValue(marker.Key,out var live)||!ReferenceEquals(live,marker.Value)||
                    live.TriggerWriteDataDelegate!=null||live.expiresInWorldTime!=ulong.MaxValue||live.isSavedToNetwork||live.data==null||!live.data.SequenceEqual(marker.Bytes))return false;
            return true;
            }
            catch{return false;}
        }
        internal bool Saved()
        {
            if(!Current())return false;
            if(Vehicle!=null)return RebirthLogisticsVehiclePublication.Verify(Vehicle,Current);
            if(Drone!=null)return RebirthLogisticsDronePublication.Verify(Drone,Current);
            if(ChunkProof==null||TileSpan==null||Markers==null||Markers.Length>RebirthRemoteResourceDurableJournal.MaximumSources+1)return false;
            var offsets=new long[Markers.Length+1];var spans=new byte[Markers.Length+1][];offsets[0]=TileOffset;spans[0]=TileSpan;
            for(int index=0;index<Markers.Length;index++){offsets[index+1]=Markers[index].Offset;spans[index+1]=Markers[index].Span;}
            return RebirthStationSnapshotEvidence.TryValidatePublishedSnapshotSpans(ChunkProof,offsets,spans,out _)&&Current();
        }
        // Detached observation only. The bytes are the positively published
        // native snapshot, not a newly serialized approximation of current state.
        internal bool TryCopyPublishedTile(out SavedTile evidence)
        {
            evidence=null;
            try
            {
                if(Tile==null||!Saved()||
                    !RebirthStationSnapshotEvidence.TryGetPublishedSnapshot(ChunkProof,out var payloadPublication)||
                    !RebirthStationSnapshotEvidence.TryCopy(ChunkProof,out var bytes)||
                    !Matches(bytes,TileOffset,TileSpan))return false;
                var markers=new SavedMarker[Markers.Length];
                for(int index=0;index<markers.Length;index++)
                {
                    var marker=Markers[index];if(!Matches(bytes,marker.Offset,marker.Span))return false;
                    markers[index]=new SavedMarker(marker.Key,marker.Offset,marker.Span,marker.Bytes);
                }
                if(!Current())return false;
                byte[] payload;lock(publicationGate)payload=nativePayload==null?null:(byte[])nativePayload.Clone();
                if(payload==null||RebirthStationPublicationRecord.Digest(payload)!=payloadPublication.PayloadDigest||
                    !RebirthStationSnapshotEvidence.TryGetPublishedSnapshot(ChunkProof,out var confirmed)||!ReferenceEquals(payloadPublication,confirmed))return false;
                RebirthStationSnapshotEvidence.TryGetPublished(ChunkProof,out var publication);
                evidence=new SavedTile(publication,payloadPublication,bytes,TileOffset,TileSpan,markers,payload);return Current();
            }
            catch{return false;}
        }
        // Exact already-published persistency tile only. Callers that do not
        // need unrelated chunk/payload bytes must reserve before this clone.
        // The returned allocation owns the detached span until the caller drops it.
        internal bool TryCopyPublishedTileSpan(out RebirthStationSnapshotEvidence.Publication publication,
            out long tileOffset,out byte[] span,out IDisposable allocation)
        {
            publication=null;tileOffset=-1;span=null;allocation=null;
            try
            {
                if(!TryReadPublishedTileDescriptor(out var observed,out long offset,out int length,out string digest)||
                    ChunkProof?.Budget==null||!ChunkProof.Budget.TryReserveAllocation(length,out allocation))return false;
                var original=TileSpan;
                if(original==null||original.Length!=length)return false;
                span=(byte[])original.Clone();
                if(RebirthStationPublicationRecord.Digest(span)!=digest||!Current()||
                    !ReferenceEquals(original,TileSpan)||
                    !TryReadPublishedTileDescriptor(out var confirmed,out long confirmedOffset,out int confirmedLength,out string confirmedDigest)||
                    !ReferenceEquals(observed,confirmed)||offset!=confirmedOffset||length!=confirmedLength||digest!=confirmedDigest)return false;
                publication=observed;tileOffset=offset;return true;
            }
            catch{return false;}
            finally{if(publication==null){span=null;allocation?.Dispose();allocation=null;}}
        }
        // Same positive native publication and exact-span checks as the copy
        // reader, without materializing another snapshot or payload image.
        internal bool TryReadPublishedTileDescriptor(out RebirthStationSnapshotEvidence.Publication publication,out long tileOffset,out int tileLength,out string tileDigest)
        {
            publication=null;tileOffset=-1;tileLength=0;tileDigest=null;
            try
            {
                if(Tile==null||TileSpan==null||Markers==null||!Current()||
                    Markers.Length>RebirthRemoteResourceDurableJournal.MaximumSources+1)return false;
                var offsets=new long[Markers.Length+1];var spans=new byte[Markers.Length+1][];
                offsets[0]=TileOffset;spans[0]=TileSpan;
                for(int index=0;index<Markers.Length;index++)
                {offsets[index+1]=Markers[index].Offset;spans[index+1]=Markers[index].Span;}
                if(!RebirthStationSnapshotEvidence.TryValidatePublishedSpans(ChunkProof,offsets,spans,out var observed))return false;
                lock(publicationGate)
                {
                    if(Closed||nativePayload==null||RebirthStationPublicationRecord.Digest(nativePayload)!=observed.PayloadDigest)return false;
                }
                if(!Current()||!RebirthStationSnapshotEvidence.TryValidatePublishedSpans(ChunkProof,offsets,spans,out var confirmed)||
                    !ReferenceEquals(observed,confirmed))return false;
                publication=observed;tileOffset=TileOffset;tileLength=TileSpan.Length;
                tileDigest=RebirthStationPublicationRecord.Digest(TileSpan);return true;
            }
            catch{return false;}
        }
        public void Dispose()
        {
            RebirthStationSnapshotEvidence.Request request;
            RebirthLogisticsVehiclePublication.Expected vehicle;
            RebirthLogisticsDronePublication.Expected drone;
            IDisposable allocation;
            lock(publicationGate)
            {
                if(Closed)return;Closed=true;pending.Remove(this);
                request=ChunkProof;vehicle=Vehicle;drone=Drone;allocation=payloadAllocation;
                // A native worker may retain this Watch in Captured.Targets.
                // It checks Closed under this same gate before touching fields.
                // Retire owned clones and callbacks now rather than retaining
                // the original actor/plan graph until that worker returns.
                ChunkProof=null;Vehicle=null;Drone=null;payloadAllocation=null;
                Source=null;ElectricalOriginal=null;Original=null;Items=null;Locks=null;Chunk=null;Tile=null;
                Markers=null;TileSpan=null;TileOffset=-1;nativePayload=null;
            }
            // Never acquire snapshot/endpoint disposal locks while holding the
            // worker publication gate. Its captured Request/Payload lifetime
            // remains independently owned by the native callback.
            try{request?.Dispose();}
            finally{try{vehicle?.Dispose();}finally{try{drone?.Dispose();}finally{allocation?.Dispose();}}}
        }
    }
    internal sealed class SavedMarker
    {
        internal readonly string Key;internal readonly long Offset;
        private readonly byte[] span,data;
        internal SavedMarker(string key,long offset,byte[] nativeSpan,byte[] payload)
        {Key=key;Offset=offset;span=(byte[])nativeSpan.Clone();data=(byte[])payload.Clone();}
        internal byte[] CopySpan()=>(byte[])span.Clone();
        internal byte[] CopyData()=>(byte[])data.Clone();
        internal void WriteArtifact(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("marker");writer.WriteAttributeString("key",Key);writer.WriteAttributeString("offset",System.Xml.XmlConvert.ToString(Offset));
            writer.WriteStartElement("span");writer.WriteBase64(span,0,span.Length);writer.WriteEndElement();writer.WriteStartElement("data");writer.WriteBase64(data,0,data.Length);writer.WriteEndElement();writer.WriteEndElement();
        }
    }
    internal sealed class SavedTile
    {
        internal readonly RebirthStationSnapshotEvidence.Publication Publication;
        internal readonly RebirthStationSnapshotEvidence.PublishedSnapshot PayloadPublication;
        internal readonly long TileOffset;
        private readonly byte[] snapshot,tile,payload;private readonly SavedMarker[] markers;
        internal SavedTile(RebirthStationSnapshotEvidence.Publication publication,RebirthStationSnapshotEvidence.PublishedSnapshot payloadPublication,byte[] bytes,long offset,byte[] nativeTile,SavedMarker[] nativeMarkers,byte[] nativePayload)
        {Publication=publication;PayloadPublication=payloadPublication;TileOffset=offset;snapshot=(byte[])bytes.Clone();tile=(byte[])nativeTile.Clone();markers=(SavedMarker[])nativeMarkers.Clone();payload=(byte[])nativePayload.Clone();}
        internal void WriteActionArtifact(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("nativeActionSource");writer.WriteAttributeString("version","1");writer.WriteAttributeString("offset",System.Xml.XmlConvert.ToString(TileOffset));
            writer.WriteStartElement("snapshot");writer.WriteBase64(snapshot,0,snapshot.Length);writer.WriteEndElement();writer.WriteStartElement("tile");writer.WriteBase64(tile,0,tile.Length);writer.WriteEndElement();
            writer.WriteStartElement("payload");writer.WriteBase64(payload,0,payload.Length);writer.WriteEndElement();writer.WriteStartElement("markers");foreach(var marker in markers)marker.WriteArtifact(writer);writer.WriteEndElement();writer.WriteEndElement();
        }
        internal byte[] CopySnapshot()=>(byte[])snapshot.Clone();
        internal byte[] CopyTile()=>(byte[])tile.Clone();
        internal byte[] CopyPayload()=>(byte[])payload.Clone();
        internal SavedMarker[] CopyMarkers()=>(SavedMarker[])markers.Clone();
    }
    internal sealed class Marker
    {
        internal string Key;internal ChunkCustomData Value;internal byte[] Bytes,Span;internal long Offset=-1;
    }
    private static bool Same(ItemStack[] a,ItemStack[] b)=>a!=null&&b!=null&&a.Length==b.Length&&a.Select((item,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(item,b[index])).All(value=>value);
    private static bool Matches(byte[] bytes,long offset,byte[] span)
    {if(bytes==null||span==null||offset<8||offset>bytes.Length-span.Length)return false;for(int index=0;index<span.Length;index++)if(bytes[(int)offset+index]!=span[index])return false;return true;}
    internal static Watch Begin(RebirthRemoteResourceNativeBinding source,Func<bool> originalCurrent,params string[] exactChunkMarkerKeys)
        =>BeginCore(source,originalCurrent,3,exactChunkMarkerKeys);
    // Purpose-owned aggregate reservation for native snapshot/proof buffers.
    // Caller separately charges its retained slot/marker graphs; this does not
    // enlarge the existing native8MiB layout or introduce effect authority.
    internal static Watch BeginWithBudget(RebirthRemoteResourceNativeBinding source,Func<bool> originalCurrent,
        IRebirthStationSnapshotByteBudget budget,params string[] exactChunkMarkerKeys)
    {
        if(budget==null||budget.MaximumSnapshotBytes!=RebirthStationSnapshotEvidence.MaximumSnapshotBytes||source?.Endpoint is not TileEntity)return null;
        return BeginCore(source,originalCurrent,3,exactChunkMarkerKeys,budget);
    }
    internal static Watch BeginPlacedTarget(RebirthRemoteResourceNativeBinding target,RebirthRemoteResourceDurableJournal.Record original,
        IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> sourceLeases,Func<bool> targetCurrent)
    {
        try
        {
            if(target?.Station==null||original==null||original.Phase!=1||original.HasOwnerAction||!original.SourcesUnattempted||sourceLeases==null||
                sourceLeases.Count>RebirthRemoteResourceDurableJournal.MaximumSources||targetCurrent?.Invoke()!=true||
                !RebirthRemoteResourcePlacedStationOriginal.TryReadAdmission(original,out var admitted)||
                !RebirthRemoteResourcePlacedStationOriginal.ValidateInput(admitted,out var position,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _)||
                target.Position.x!=position.x||target.Position.y!=position.y||target.Position.z!=position.z||
                !RemoteResourceWorldIdentity.TryGet(target.World,out string actualWorld)||actualWorld!=original.World)return null;
            var sources=original.Copy().Element("sources").Elements("source").ToArray();
            if(sources.Length<1||sources.Length>RebirthRemoteResourceDurableJournal.MaximumSources)return null;
            var keys=new HashSet<string>(StringComparer.Ordinal);var chunk=target.Station.GetChunk();
            foreach(var node in sources)
            {
                if(node.Element("npc")!=null)continue;string id=(string)node.Attribute("id");
                if(!sourceLeases.TryGetValue(id,out var lease)||lease?.Original==null||lease.Original.Domain!=RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource||
                    lease.Original.Owner!=original.Owner||lease.Original.Operation!=original.Operation||lease.Original.Binding!=original.Binding||!lease.Current())return null;
                if(lease.Source.Endpoint is not TileEntity tile)continue;
                if(!lease.TryPreviewTileDebit(out var observed,out string key,out var before,out _)||!ReferenceEquals(observed,tile))return null;
                if(!ReferenceEquals(tile.GetChunk(),chunk))continue;
                if(!chunk.ChunkCustomData.dict.TryGetValue(key,out var marker)||marker.data==null||!marker.data.SequenceEqual(before))return null;
                keys.Add(key);
            }
            // This named expansion is derived only from the actual admitted
            // original's source keys. It never scans arbitrary world metadata.
            return BeginCore(target,targetCurrent,RebirthRemoteResourceDurableJournal.MaximumSources,keys.ToArray());
        }
        catch{return null;}
    }
    // Readonly After observer: exact same admitted source-marker set plus the
    // existing target identity. This supplies no queue/payment permission.
    internal static Watch BeginPlacedPaidTarget(RebirthRemoteResourceNativeBinding target,RebirthRemoteResourceDurableJournal.Record root,Func<bool> current)
    {
        try
        {
            if(target?.Station==null||root==null||root.Phase!=2||!root.HasOwnerAction||(int)root.Copy().Attribute("outcome")!=1||
                root.Copy().Element("ownerAction")?.Attribute("domain")?.Value!="native-placed-station"||current?.Invoke()!=true)return null;
            var keys=new HashSet<string>(StringComparer.Ordinal);var chunk=target.Station.GetChunk();
            foreach(var source in root.Copy().Element("sources").Elements("source"))
            {
                if(source.Element("npc")!=null)continue;int kind=(int)source.Attribute("kind");
                if(kind!=(int)RemoteResourceSourceKind.StaticContainer&&kind!=(int)RemoteResourceSourceKind.WorkstationOutput)continue;
                string id=(string)source.Attribute("id");var parts=id.Substring(2).Split(',');
                if(parts.Length!=3||!int.TryParse(parts[0],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int x)||
                    !int.TryParse(parts[1],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int y)||
                    !int.TryParse(parts[2],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int z))return null;
                if((x>>4)!=chunk.X||(z>>4)!=chunk.Z)continue;
                string key="RebirthOriginalSourceV2:"+(x&15)+":"+y+":"+(z&15)+":"+(string)source.Attribute("incarnation");
                if(!keys.Add(key))return null;
            }
            if(keys.Count>RebirthRemoteResourceDurableJournal.MaximumSources)return null;
            return BeginCore(target,current,RebirthRemoteResourceDurableJournal.MaximumSources,keys.ToArray());
        }
        catch{return null;}
    }
    private static Watch BeginCore(RebirthRemoteResourceNativeBinding source,Func<bool> originalCurrent,int maximumKeys,string[] exactChunkMarkerKeys,
        IRebirthStationSnapshotByteBudget budget=null)
    {
        if(!installed||source?.Current()!=true||originalCurrent?.Invoke()!=true||!ThreadManager.IsMainThread())return null;
        foreach(var stale in pending.ToArray())if(!stale.Current())stale.Dispose();if(pending.Count>=16)return null;
        var watch=new Watch{Source=source,Original=originalCurrent,Items=source.CopySlots(),Locks=source.CopyLocks(),Expires=DateTime.UtcNow.AddSeconds(30)};
        if(watch.Items==null||watch.Locks==null||watch.Items.Length!=watch.Locks.Length)return null;
        try
        {
            if(source.Vehicle!=null)
            {watch.Vehicle=RebirthLogisticsVehiclePublication.Begin(source.Vehicle,watch.Current);if(watch.Vehicle==null)return null;Track(watch);return watch;}
            if(source.Drone!=null)
            {watch.Drone=RebirthLogisticsDronePublication.Begin(source.Drone,watch.Current);if(watch.Drone==null)return null;Track(watch);return watch;}
            watch.Tile=source.Endpoint as TileEntity;watch.Chunk=watch.Tile?.GetChunk();
            if(watch.Chunk==null||exactChunkMarkerKeys==null||exactChunkMarkerKeys.Length>maximumKeys||exactChunkMarkerKeys.Distinct(StringComparer.Ordinal).Count()!=exactChunkMarkerKeys.Length)return null;
            var markerKeys=exactChunkMarkerKeys.ToList();
            if(source.Station!=null)
            {
                if(!RebirthLogisticsEndpointIdentity.TryGetNativeMarker(source.World,source.Station,out string identityKey,out _))return null;
                if(!markerKeys.Contains(identityKey,StringComparer.Ordinal))markerKeys.Add(identityKey);
            }
            // Bound all retained marker bytes before the first marker clone.
            long markerBytes=0;
            foreach(string key in markerKeys)
            {
                if(string.IsNullOrEmpty(key)||key.Length>256||!watch.Chunk.ChunkCustomData.dict.TryGetValue(key,out var value)||value.data==null||value.data.Length>32768)return null;
                markerBytes=checked(markerBytes+value.data.LongLength);
                if(markerBytes>RebirthStationSnapshotEvidence.MaximumSnapshotBytes)return null;
            }
            var markers=new List<Marker>();
            foreach(string key in markerKeys)
            {
                var value=watch.Chunk.ChunkCustomData.dict[key];
                markers.Add(new Marker{Key=key,Value=value,Bytes=(byte[])value.data.Clone()});
            }
            watch.Markers=markers.ToArray();if(!watch.Current())return null;
            var provider=source.World.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
            if(regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrEmpty(regions.saveDirectory))return null;
            var access=regions.regionFileAccess;string directory=Path.GetFullPath(regions.saveDirectory);var original=watch.Original;
            watch.Original=()=>original()&&ReferenceEquals(source.World.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&
                ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==directory;
            watch.ChunkProof=RebirthStationSnapshotEvidence.Watch(source.World,watch.Chunk,watch.Chunk.X,watch.Chunk.Z,watch.Current,watch.Tile,budget:budget);
            if(watch.ChunkProof==null)return null;Track(watch);
            lock(watch.Chunk)
            {
                if(!watch.Current()||watch.Chunk.IsLocked||watch.Chunk.InProgressSaving||watch.Chunk.NeedsDecoration||watch.Chunk.InProgressDecorating||watch.Chunk.NeedsLightCalculation||watch.Chunk.InProgressLighting)
                {watch.Dispose();return null;}
                watch.Chunk.InProgressSaving=true;
            }
            try{if(!watch.Current()){watch.Dispose();return null;}regions.SaveChunkSnapshot(watch.Chunk,true);}
            finally{lock(watch.Chunk)watch.Chunk.InProgressSaving=false;}
            return watch;
        }
        catch{watch.Dispose();return null;}
    }
    internal static Watch BeginElectricalTarget(RemoteResourceTransactions.ActionPayment original)
    {
        if(!RemoteResourceTransactions.OwnsElectricalTargetObservation(original)||!original.TargetCurrent())return null;
        var tile=original.ServiceTarget;var chunk=tile.GetChunk();
        if(chunk==null||chunk.InProgressUnloading||!ReferenceEquals(original.World.GetChunkSync(chunk.X,chunk.Z),chunk))return null;
        var watch=new Watch{ElectricalOriginal=original,Tile=tile,Chunk=chunk,Items=Array.Empty<ItemStack>(),Locks=Array.Empty<bool>(),Expires=DateTime.UtcNow.AddSeconds(30)};
        try
        {
            string key=RebirthElectricalSourceIdentity.NativeKey(original.ServiceCandidate.Position);
            if(!chunk.ChunkCustomData.dict.TryGetValue(key,out var marker)||marker.data==null||marker.data.Length<22||marker.data.Length>32768)return null;
            watch.Markers=new[]{new Marker{Key=key,Value=marker,Bytes=(byte[])marker.data.Clone()}};
            var provider=original.World.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
            if(regions==null||regions.regionFileAccess is not RegionFileAccessSectorBased||string.IsNullOrEmpty(regions.saveDirectory))return null;
            var access=regions.regionFileAccess;string directory=Path.GetFullPath(regions.saveDirectory);
            watch.Original=()=>RemoteResourceTransactions.OwnsElectricalTargetObservation(original)&&original.TargetCurrent()&&ReferenceEquals(original.World.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==directory;
            if(!watch.Current())return null;watch.ChunkProof=RebirthStationSnapshotEvidence.Watch(original.World,chunk,chunk.X,chunk.Z,watch.Current,tile);
            if(watch.ChunkProof==null)return null;Track(watch);
            lock(chunk){if(!watch.Current()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting){watch.Dispose();return null;}chunk.InProgressSaving=true;}
            try{if(!watch.Current()){watch.Dispose();return null;}regions.SaveChunkSnapshot(chunk,true);}finally{lock(chunk)chunk.InProgressSaving=false;}
            return watch;
        }
        catch{watch.Dispose();return null;}
    }
    private static byte[] Span(Stream stream,long start)
    {
        long end=stream.Position,length=end-start;if(start<8||length<1||length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes||!stream.CanRead||!stream.CanSeek)return null;
        var bytes=new byte[(int)length];try{stream.Position=start;int count=0;while(count<bytes.Length){int read=stream.Read(bytes,count,bytes.Length-count);if(read<=0)return null;count+=read;}return bytes;}
        finally{stream.Position=end;}
    }
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.remote.resources.native.publication.v1");
        try
        {
            // Exact already shared observers are idempotent, independent of the
            // camper feature switch. They grant no source mutation permission.
            foreach(var type in new[]{typeof(RebirthStationSnapshotUpdatePatch),typeof(RebirthStationSnapshotResetPatch),typeof(RebirthStationChunkSerializedPatch),typeof(RebirthStationSnapshotWritePatch),typeof(RebirthStationRegionPublishedPatch),typeof(RebirthStationInputSerializedPatch),typeof(RebirthStationQueueSerializedPatch),typeof(TileWrite),typeof(MarkerWrite),typeof(SavedPayload)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
            RebirthLogisticsVehiclePublication.Install();RebirthLogisticsDronePublication.Install();
            ModEvents.GameUpdate.RegisterHandler(Update);installed=true;
        }
        catch{harmony.UnpatchSelf();throw;}
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {foreach(var watch in pending.ToArray())try{if(!watch.Current())watch.Dispose();}catch{watch.Dispose();}}
    [HarmonyPatch(typeof(RebirthStationPublicationEvidence),nameof(RebirthStationPublicationEvidence.After))]
    private static class SavedPayload
    {
        private sealed class Captured
        {internal RebirthStationSnapshotEvidence.Request Request;internal byte[] Payload;internal Watch[] Targets;}
        private static void Prefix(RebirthStationPublicationEvidence.WriteScope write,Exception error,out Captured __state)
        {
            __state=null;
            try
            {
                if(error!=null||write?.Scope?.Request==null||write.Payload==null||write.Payload.Length<8||write.Payload.Length>RebirthStationRegionPayload.MaximumPayloadBytes)return;
                lock(publicationGate)
                {
                    var targets=pending.Where(watch=>!watch.Closed&&ReferenceEquals(watch.ChunkProof,write.Scope.Request)).ToArray();
                    if(targets.Length==0)return;
                    // Already detached by the shared native observer; retaining
                    // this reference cannot modify the writer's pooled bytes.
                    __state=new Captured{Request=write.Scope.Request,Payload=write.Payload,Targets=targets};
                }
            }
            catch{__state=null;}
        }
        private static void Postfix(Captured __state)
        {
            IDisposable workspace=null,rawAllocation=null;
            try
            {
                if(__state==null||!RebirthStationSnapshotEvidence.TryGetPublishedSnapshot(__state.Request,out var proof)||
                    RebirthStationPublicationRecord.Digest(__state.Payload)!=proof.PayloadDigest||
                    !ReserveObservation(__state.Request,4L*RebirthStationSnapshotEvidence.MaximumSnapshotBytes+RebirthStationRegionPayload.MaximumPayloadBytes+128*1024,out workspace)||
                    !RebirthStationSnapshotEvidence.TryCopy(__state.Request,out var raw,out rawAllocation)||raw.Length<9||
                    !RebirthStationChunkInflate.TryDecode(__state.Payload,raw.Length-8,out var decoded)||decoded.Length!=raw.Length-8)return;
                for(int i=0;i<8;i++)if(__state.Payload[i]!=raw[i])return;
                for(int i=0;i<decoded.Length;i++)if(decoded[i]!=raw[i+8])return;
                // Native publication may finish on its worker. No live world,
                // actor or inventory is queried here; main-thread getters
                // still require the exact current full original watch.
                lock(publicationGate)foreach(var watch in __state.Targets)
                    if(!watch.Closed&&ReferenceEquals(watch.ChunkProof,__state.Request)&&ReserveObservation(__state.Request,__state.Payload.Length,out var retained))
                    {try{watch.RememberPayload(__state.Payload,retained);retained=null;}finally{retained?.Dispose();}}
            }
            catch{/* Observation failure grants no proof and changes no native save. */}
            finally{rawAllocation?.Dispose();workspace?.Dispose();}
        }
    }
    [HarmonyPatch]
    private static class TileWrite
    {
        private static IEnumerable<MethodBase> TargetMethods()
                {
            var seen=new HashSet<MethodBase>();
            foreach(var type in new[]{typeof(TileEntityComposite),typeof(TileEntityWorkstation)}.Concat(typeof(TileEntityPowered).Assembly.GetTypes().Where(type=>typeof(TileEntityPowered).IsAssignableFrom(type))))
            {
                var method=AccessTools.DeclaredMethod(type,"write",new[]{typeof(PooledBinaryWriter),typeof(StreamModeWrite)});
                if(method!=null&&seen.Add(method))yield return method;
            }
        }
        private static void Prefix(TileEntity __instance,PooledBinaryWriter _bw,StreamModeWrite _eStreamMode,out long __state)
        {__state=_eStreamMode==StreamModeWrite.Persistency?RebirthStationSnapshotEvidence.StartStationSpan(__instance,_bw.BaseStream):-1;}
        private static void Postfix(TileEntity __instance,PooledBinaryWriter _bw,long __state,MethodBase __originalMethod,bool __runOriginal)
        {
            if(__state<8)return;
            try{foreach(var watch in pending)if(ReferenceEquals(watch.Tile,__instance)&&watch.Current())
            {if(watch.ElectricalOriginal!=null&&(!__runOriginal||__originalMethod!=AccessTools.Method(__instance.GetType(),"write",new[]{typeof(PooledBinaryWriter),typeof(StreamModeWrite)})))continue;watch.TileSpan=Span(_bw.BaseStream,__state);watch.TileOffset=__state;}}catch{ /* An observation cannot alter native save. */ }
        }
    }
    [HarmonyPatch(typeof(ChunkCustomData),nameof(ChunkCustomData.Write))]
    private static class MarkerWrite
    {
        private static void Prefix(ChunkCustomData __instance,BinaryWriter _bw,out long __state)
        {
            __state=-1;
            try
            {
            foreach(var watch in pending)if(watch.Markers!=null&&watch.Markers.Any(marker=>ReferenceEquals(marker.Value,__instance))&&watch.Current())
            {__state=RebirthStationSnapshotEvidence.StartStationSpan(watch.Tile,_bw.BaseStream);break;}
            }
            catch{__state=-1;}
        }
        private static void Postfix(ChunkCustomData __instance,BinaryWriter _bw,long __state)
        {
            if(__state<8)return;
            try{foreach(var watch in pending)if(watch.Current()&&watch.Markers!=null)foreach(var marker in watch.Markers)if(ReferenceEquals(marker.Value,__instance))
            {marker.Span=Span(_bw.BaseStream,__state);marker.Offset=__state;}}catch{ /* Evidence only. */ }
        }
    }
}
