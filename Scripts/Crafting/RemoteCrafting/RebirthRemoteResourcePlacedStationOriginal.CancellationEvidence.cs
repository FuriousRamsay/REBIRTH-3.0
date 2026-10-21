using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private sealed class CancellationSnapshotBudget:IRebirthStationSnapshotByteBudget,IRebirthRemoteResourceRetainedNativeBuffers
    {
        private readonly object gate=new object();private long used;private bool closed;private CancellationLoadRetirement loads;private CancellationHostCustody custody;
        private const long Capacity=6L*(RebirthStationSnapshotEvidence.MaximumSnapshotBytes+RebirthStationRegionPayload.MaximumPayloadBytes);
        internal static bool TryOpen(CancellationServer original,out CancellationSnapshotBudget budget)
        {budget=null;if(!original.Current()||!original.Memory.Reserve(checked(original.Memory.ReservedBytes+Capacity)))return false;var candidate=new CancellationSnapshotBudget();if(!original.Memory.RetainNativeBuffers(candidate))return false;budget=candidate;return true;}
        public int MaximumSnapshotBytes=>RebirthStationSnapshotEvidence.MaximumSnapshotBytes;
        internal bool Drained{get{lock(gate)return used==0&&(loads==null||loads.IsDrained)&&(custody==null||custody.IsDrained);}}
        internal bool AttachLoadRetirement(CancellationLoadRetirement value)
        {lock(gate){if(closed||loads!=null&&!loads.RetiredAndDrained||value==null)return false;loads=value;return true;}}
        internal bool AttachHostCustody(CancellationHostCustody value){lock(gate){if(closed||value==null||custody!=null&&!ReferenceEquals(custody,value))return false;custody=value;return true;}}
        public bool IsDrained=>Drained;
        internal void Close(){lock(gate)closed=true;}
        private sealed class Allocation:IDisposable
        {private CancellationSnapshotBudget owner;private readonly long bytes;internal Allocation(CancellationSnapshotBudget value,long size){owner=value;bytes=size;}public void Dispose(){var actual=System.Threading.Interlocked.Exchange(ref owner,null);if(actual!=null)lock(actual.gate)actual.used-=bytes;}}
        public bool TryReserveAllocation(long bytes,out IDisposable allocation)
        {
            allocation=null;if(bytes<1||bytes>Capacity)return false;
            lock(gate){if(closed||used>Capacity-bytes)return false;used+=bytes;allocation=new Allocation(this,bytes);return true;}
        }
    }
    internal sealed class CancellationNativeEvidence
    {internal byte[] Snapshot,Tile,Payload;internal long Offset;internal string Canonical;}
    private static bool ReserveCancellationEvidence(CancellationServer original,long bytes)
    {
        if(bytes<1||bytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
        if(bytes<=original.EvidenceWorkspace)return true;
        if(!original.Memory.Reserve(checked(original.Memory.ReservedBytes+bytes-original.EvidenceWorkspace)))return false;original.EvidenceWorkspace=bytes;return true;
    }
    private static bool ObserveCancellationSnapshot(CancellationServer original,bool after)
    {
        if(!original.Current()||!original.FullTile(after?original.AfterTile:original.BeforeTile)||after&&!CancellationMarkerCurrent(original))return false;
        if(ReadCancellationSnapshot(original,after,out _))return true;
        if(original.SnapshotBudget==null&&!CancellationSnapshotBudget.TryOpen(original,out original.SnapshotBudget))return false;
        if(original.Watch==null||!original.Watch.Current())
        {
            original.Watch?.Dispose();original.Watch=null;
            if(!RebirthRemoteResourceNativeBinding.TryResolve(original.Actor.world,RemoteResourceIdentity.Workstation(original.Station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,original.Birth,out var target))return false;
            bool Current()=>original.Current()&&original.FullTile(after?original.AfterTile:original.BeforeTile)&&(!after||CancellationMarkerCurrent(original));
            original.Watch=RebirthRemoteResourceNativePublication.BeginWithBudget(target,Current,original.SnapshotBudget,after?new[]{CancellationMarkerKey(original)}:new string[0]);return false;
        }
        if(!original.Watch.TryCopyPublishedTile(out var saved)||saved.Publication==null||saved.PayloadPublication==null||string.IsNullOrEmpty(saved.Publication.Path)||Encoding.UTF8.GetByteCount(saved.Publication.Path)>4096||
            !SavedCancellationOwner(original,after?2f:10f,after,out var owner))return false;
        var snapshot=saved.CopySnapshot();var payload=saved.CopyPayload();var tile=saved.CopyTile();var buffs=owner.buffData?.ToArray();
        if(buffs==null||buffs.Length>1024*1024||!ReserveCancellationEvidence(original,checked(12L*(snapshot.Length+payload.Length+tile.Length+buffs.Length)+65536)))return false;
        var body=new XElement("placedCancellationSaved",new XAttribute("version",1),new XAttribute("cancel",original.Operation.ToString("N")),new XAttribute("source",original.Source.ToString("N")),
            new XAttribute("binding",original.Binding),new XAttribute("birth",original.Birth),new XAttribute("after",after),new XAttribute("request",RebirthRemoteResourceDurableJournal.Digest(original.Header)),
            new XAttribute("chunkX",saved.Publication.ChunkX),new XAttribute("chunkZ",saved.Publication.ChunkZ),new XAttribute("generation",saved.PayloadPublication.Generation),
            new XAttribute("path",saved.Publication.Path),new XAttribute("payloadDigest",saved.Publication.PayloadDigest),new XAttribute("tileOffset",saved.TileOffset),new XAttribute("tileLength",tile.Length),
            new XAttribute("ownerId",owner.id),new XAttribute("deaths",owner.deaths),new XElement("snapshot",Convert.ToBase64String(snapshot)),new XElement("payload",Convert.ToBase64String(payload)),
            new XElement("ownerActor",RebirthRemoteResourceClientOriginal.ActorImage(after?original.AfterActor:original.BeforeActor)),new XElement("ownerBuffs",Convert.ToBase64String(buffs)),new XElement("markers"));
        foreach(var marker in saved.CopyMarkers())body.Element("markers").Add(new XElement("marker",new XAttribute("key",marker.Key),new XAttribute("offset",marker.Offset),new XAttribute("length",marker.CopySpan().Length),new XElement("data",Convert.ToBase64String(marker.CopyData()))));
        string text=body.ToString(SaveOptions.DisableFormatting);byte[] bytes=Encoding.UTF8.GetBytes(text);
        if(bytes.Length>MaximumResultBytes||!SaveCancellation(original.Operation,original.Source,original.Original.Creation,after?"After":"Before",bytes,true)||
            !ReadCancellationSnapshot(original,after,out _))return false;
        if(after)original.AfterEvidence=text;original.Watch.Dispose();original.Watch=null;return true;
    }
    private static bool ReadCancellationSnapshot(CancellationServer original,bool after,out CancellationNativeEvidence evidence)
    {
        evidence=null;var record=CancellationJournal(original.Operation,after?"After":"Before").Read(original.Operation);if(record==null)return false;
        if(record.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Source||record.OwnerCreation!=original.Original.Creation||record.Revision!=0||!record.Before.SequenceEqual(record.After)||
            !ReserveCancellationEvidence(original,checked(12L*record.Before.Length+65536)))return false;
        string text=new UTF8Encoding(false,true).GetString(record.Before);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumResultBytes);
        if(!ValidateCancellationSnapshot(body,original.Operation,original.Source,original.Binding,original.Birth,original.Header,after,
            after?original.AfterTile:original.BeforeTile,after?original.AfterActor:original.BeforeActor,original.Actor.entityId,original.Original.Deaths,
            original.Station.ToWorldPos(),after?original.Marker:null,out evidence))return false;
        evidence.Canonical=text;if(after)original.AfterEvidence=text;return original.Current();
    }
    private static bool ValidateCancellationSnapshot(XElement body,Guid operation,Guid source,string binding,string birth,string header,bool after,byte[] expectedTile,
        RebirthCamperEnergyActorImage expectedActor,int ownerId,int deaths,Vector3i position,byte[] cancellationMarker,out CancellationNativeEvidence evidence)
    {
        evidence=null;
        if(body.Name!="placedCancellationSaved"||body.Attributes().Count()!=16||body.Elements().Count()!=5||(int?)body.Attribute("version")!=1||
            (string)body.Attribute("cancel")!=operation.ToString("N")||(string)body.Attribute("source")!=source.ToString("N")||(string)body.Attribute("binding")!=binding||
            (string)body.Attribute("birth")!=birth||(bool?)body.Attribute("after")!=after||(string)body.Attribute("request")!=RebirthRemoteResourceDurableJournal.Digest(header)||
            (int?)body.Attribute("ownerId")!=ownerId||(int?)body.Attribute("deaths")!=deaths||(int?)body.Attribute("chunkX")!=(position.x>>4)||(int?)body.Attribute("chunkZ")!=(position.z>>4)||
            (long?)body.Attribute("generation") is not long generation||generation<1||string.IsNullOrEmpty((string)body.Attribute("path"))||Encoding.UTF8.GetByteCount((string)body.Attribute("path"))>4096||
            body.Element("ownerActor")?.Value!=RebirthRemoteResourceClientOriginal.ActorImage(expectedActor))return false;
        var snapshot=Convert.FromBase64String(body.Element("snapshot").Value);var payload=Convert.FromBase64String(body.Element("payload").Value);var buffs=Convert.FromBase64String(body.Element("ownerBuffs").Value);
        if(snapshot.Length<9||snapshot.Length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes||payload.Length<8||payload.Length>RebirthStationRegionPayload.MaximumPayloadBytes||buffs.Length>1024*1024||
            RebirthStationPublicationRecord.Digest(payload)!=(string)body.Attribute("payloadDigest")||!RebirthStationChunkInflate.TryDecode(payload,snapshot.Length-8,out var inflated)||!inflated.SequenceEqual(snapshot.Skip(8)))return false;
        var owner=new PlayerDataFile{buffData=new MemoryStream(buffs,false)};
        if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(owner,CancellationReceipt(operation),out float receipt,out bool hasReceipt)||!hasReceipt||receipt!=(after?2f:10f)||
            (long?)body.Attribute("tileOffset") is not long offset||(int?)body.Attribute("tileLength") is not int length||offset<8||length<1||offset>snapshot.LongLength-length)return false;
        var tile=new byte[length];Buffer.BlockCopy(snapshot,(int)offset,tile,0,length);
        // Hydrate only the already admitted expected financial frame. Actual
        // saved bytes must match it before any native item graph can be read.
        if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(expectedTile,out var parsed)||parsed.LocalPosition!=new Vector3i(position.x&15,position.y,position.z&15)||
            !RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(tile,parsed.ClockOffset,out var normalized,out _)||!normalized.SequenceEqual(expectedTile))return false;
        var markers=body.Element("markers").Elements().ToArray();if(markers.Length!=(after?2:1))return false;
        string identity="RebirthLogisticsStationV1:"+(position.x&15)+":"+position.y+":"+(position.z&15);bool sawIdentity=false,sawCancellation=false;
        foreach(var marker in markers)
        {
            if(marker.Name!="marker"||marker.Attributes().Count()!=3||marker.Elements().Count()!=1)return false;
            string key=(string)marker.Attribute("key");var data=Convert.FromBase64String(marker.Element("data").Value);
            if(key==identity&&!sawIdentity)
            {
                // Original target identity's full native marker is retained in
                // the immutable original, rather than trusting a bare birth.
                var original=RebirthCookingRemoteAdmission.ReadProtocolXml(header,MaximumResultBytes).Element("fetchClientOriginal");
                ushort block=checked((ushort)new BlockValue((uint)original.Element("action").Attribute("block")).type);
                byte[] expected;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {writer.Write((byte)1);writer.Write(Guid.ParseExact(birth,"N").ToByteArray());writer.Write(block);writer.Flush();expected=stream.ToArray();}
                if(!data.SequenceEqual(expected))return false;sawIdentity=true;
            }
            else if(after&&key=="RebirthPlacedCancellationV1:"+operation.ToString("N")&&!sawCancellation&&cancellationMarker!=null&&data.SequenceEqual(cancellationMarker))sawCancellation=true;
            else return false;
            if(!CompletionMarker(snapshot,key,(long)marker.Attribute("offset"),(int)marker.Attribute("length"),data))return false;
        }
        if(!sawIdentity||after&&!sawCancellation)return false;evidence=new CancellationNativeEvidence{Snapshot=snapshot,Payload=payload,Tile=tile,Offset=offset};return true;
    }
}
