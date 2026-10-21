using System;
using System.Linq;

// Positive native Before observation plus conservative full-chunk candidate
// capacity. This is not an owner permission, source attempt or native setter.
internal static class RebirthRemoteResourcePlacedStationBefore
{
    internal sealed class Evidence
    {
        internal readonly Guid Job,Source;internal readonly string Binding,NativePayloadDigest;
        internal readonly long TileOffset;internal readonly ulong ObservedClock;
        internal readonly int ChunkX,ChunkZ,CandidateRawBytes,CandidateCompressedBytes;
        internal readonly int MandatorySourceGrowth;internal readonly long PayloadUpperBound;
        private readonly byte[] snapshot,tile,payload;
        private readonly RebirthRemoteResourceNativePublication.SavedMarker[] markers;
        internal Evidence(RebirthRemoteResourcePlacedStationPlan.Plan plan,RebirthRemoteResourceNativePublication.SavedTile native,
            byte[] bytes,byte[] nativeTile,ulong clock,int raw,int compressed,long bound)
        {Job=plan.Job;Source=plan.Source;Binding=plan.Binding;NativePayloadDigest=native.Publication.PayloadDigest;
            TileOffset=native.TileOffset;ObservedClock=clock;ChunkX=native.Publication.ChunkX;ChunkZ=native.Publication.ChunkZ;
            CandidateRawBytes=raw;CandidateCompressedBytes=compressed;MandatorySourceGrowth=plan.SourceMarkerGrowth;PayloadUpperBound=bound;
            snapshot=(byte[])bytes.Clone();tile=(byte[])nativeTile.Clone();payload=native.CopyPayload();markers=native.CopyMarkers();}
        internal byte[] CopySnapshot()=>(byte[])snapshot.Clone();internal byte[] CopyTile()=>(byte[])tile.Clone();
        internal byte[] CopyPayload()=>(byte[])payload.Clone();
        internal RebirthRemoteResourceNativePublication.SavedMarker[] CopyMarkers()=>(RebirthRemoteResourceNativePublication.SavedMarker[])markers.Clone();
        internal void WriteArtifact(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("savedBefore");writer.WriteAttributeString("job",Job.ToString("N"));writer.WriteAttributeString("source",Source.ToString("N"));writer.WriteAttributeString("binding",Binding);
            writer.WriteAttributeString("nativeDigest",NativePayloadDigest);writer.WriteAttributeString("chunkX",System.Xml.XmlConvert.ToString(ChunkX));writer.WriteAttributeString("chunkZ",System.Xml.XmlConvert.ToString(ChunkZ));
            writer.WriteAttributeString("tileOffset",System.Xml.XmlConvert.ToString(TileOffset));writer.WriteAttributeString("tileLength",System.Xml.XmlConvert.ToString(tile.Length));writer.WriteAttributeString("clock",System.Xml.XmlConvert.ToString(ObservedClock));
            writer.WriteAttributeString("raw",System.Xml.XmlConvert.ToString(CandidateRawBytes));writer.WriteAttributeString("compressed",System.Xml.XmlConvert.ToString(CandidateCompressedBytes));
            writer.WriteAttributeString("growth",System.Xml.XmlConvert.ToString(MandatorySourceGrowth));writer.WriteAttributeString("bound",System.Xml.XmlConvert.ToString(PayloadUpperBound));
            writer.WriteStartElement("snapshot");writer.WriteBase64(snapshot,0,snapshot.Length);writer.WriteEndElement();
            writer.WriteStartElement("nativePayload");writer.WriteBase64(payload,0,payload.Length);writer.WriteEndElement();writer.WriteStartElement("markers");
            foreach(var marker in markers)marker.WriteArtifact(writer);writer.WriteEndElement();writer.WriteEndElement();
        }
    }
    internal static bool TryObserve(RebirthRemoteResourceNativePublication.Watch watch,RebirthRemoteResourcePlacedStationPlan.Plan plan,out Evidence evidence)
    {
        evidence=null;
        try
        {
            if(watch==null||plan==null||!watch.TryCopyPublishedTile(out var native)||native.Publication==null||
                string.IsNullOrEmpty(native.Publication.Path)||new System.Text.UTF8Encoding(false,true).GetByteCount(native.Publication.Path)>4096)return false;
            var snapshot=native.CopySnapshot();var tile=native.CopyTile();
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(tile,out var parsed)||parsed.ClockOffset!=plan.ClockOffset||
                !RebirthRemoteResourcePlacedStationNativeReader.TryRead(plan.CopyAfter(),out var planned)||planned.ClockOffset!=plan.ClockOffset||planned.Burning!=plan.Burning||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(plan.QueueImage,out var queue)||RebirthCookingRemoteAdmissionBackend.Queue(queue)!=RebirthCookingRemoteAdmissionBackend.Queue(planned.Queue)||
                !RebirthCamperInventorySnapshot.Capture(plan.CopyInput()).SequenceEqual(RebirthCamperInventorySnapshot.Capture(planned.Input))||
                !RebirthCamperInventorySnapshot.Capture(plan.CopyOutput()).SequenceEqual(RebirthCamperInventorySnapshot.Capture(planned.Output))||
                !RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(tile,plan.ClockOffset,out var comparison,out ulong clock)||clock!=parsed.Clock||
                !comparison.SequenceEqual(plan.CopyBefore())||
                !RebirthRemoteResourcePlacedStationCandidate.TryMeasureChunk(snapshot,native.TileOffset,tile,plan.CopyAfter(),plan.SourceMarkerGrowth,out int raw,out int compressed,out long bound)||
                !watch.Current())return false;
            evidence=new Evidence(plan,native,snapshot,tile,clock,raw,compressed,bound);return true;
        }
        catch{return false;}
    }
}
