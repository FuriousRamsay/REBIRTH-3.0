using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Detached artifact integrity, not admission/payment authority. Callers must
// additionally rederive the retained source revision, native incarnation and
// original owner scope before publishing or continuing any operation.
internal static class RebirthRemoteResourcePlacedStationArtifactReader
{
    // Root + fixed original/placed action + plan + saved-Before nodes; each
    // admitted source marker/target identity adds marker/span/data only.
    private const int MaximumElements=22+3*(RebirthRemoteResourceDurableJournal.MaximumSources+1);
    internal sealed class Image
    {
        internal readonly RebirthRemoteResourceClientOriginal.Intent Original;
        internal readonly string Birth;internal readonly long SourceRevision,SourcePreparedBytes,SourceGraphBytes,SourceReadWorkspace,ReadWorkspace;
        private readonly XElement body;
        internal Image(XElement value,RebirthRemoteResourceClientOriginal.Intent original,long workspace)
        {body=value;Original=original;ReadWorkspace=workspace;Birth=(string)value.Attribute("birth");SourceRevision=(long)value.Attribute("sourceRevision");SourcePreparedBytes=(long)value.Attribute("sourcePreparedBytes");SourceGraphBytes=(long?)value.Attribute("sourceGraphBytes")??0;SourceReadWorkspace=(long?)value.Attribute("sourceReadWorkspace")??0;}
        internal XElement Copy()=>new XElement(body);
        internal bool HasCapturedAllocation=>SourceGraphBytes>0&&SourceReadWorkspace>0;
        internal bool ValidateSource(RebirthRemoteResourceDurableJournal.Record current)=>ValidateSourceBody(this,body,current);
    }
    private static bool Shape(XElement node,string name,string[] attributes,string[] children)
    {
        return node!=null&&node.Name==name&&node.Attributes().Count()==attributes.Length&&
            node.Attributes().All(a=>a.Name.NamespaceName.Length==0&&attributes.Contains(a.Name.LocalName))&&
            node.Elements().Count()==children.Length&&node.Elements().Select(e=>e.Name.ToString()).SequenceEqual(children)&&
            !node.Nodes().Any(n=>!(n is XElement)&&!(n is XText text&&string.IsNullOrWhiteSpace(text.Value)));
    }
    private static byte[] Bytes(XElement node,int maximum,bool empty=false)
    {
        if(node==null||node.HasAttributes||node.HasElements||node.Value.Length>4L*((maximum+2L)/3))throw new InvalidDataException();
        byte[] bytes=Convert.FromBase64String(node.Value);
        if(bytes.Length>maximum||(!empty&&bytes.Length==0)||Convert.ToBase64String(bytes)!=node.Value)throw new InvalidDataException();return bytes;
    }
    private static bool SourceMarkerGrowth(XElement artifact,XElement sourceImage,RebirthRemoteResourceDurableJournal.Record original,Vector3i target,int expectedGrowth)
    {
        var markers=artifact.Element("savedBefore").Element("markers").Elements("marker").ToDictionary(marker=>(string)marker.Attribute("key"),StringComparer.Ordinal);
        var expected=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal)
        {"RebirthLogisticsStationV1:"+(target.x&15)+":"+target.y+":"+(target.z&15)};
        int growth=0;
        foreach(var source in sourceImage.Element("sources").Elements("source"))
        {
            int kind=(int)source.Attribute("kind");if(kind!=(int)RemoteResourceSourceKind.StaticContainer&&kind!=(int)RemoteResourceSourceKind.WorkstationOutput)continue;
            string id=(string)source.Attribute("id"),prefix=kind==(int)RemoteResourceSourceKind.StaticContainer?"S:":"W:";
            if(id==null||!id.StartsWith(prefix,StringComparison.Ordinal))return false;var parts=id.Substring(2).Split(',');
            if(parts.Length!=3||!int.TryParse(parts[0],System.Globalization.NumberStyles.AllowLeadingSign,System.Globalization.CultureInfo.InvariantCulture,out int x)||
                !int.TryParse(parts[1],System.Globalization.NumberStyles.AllowLeadingSign,System.Globalization.CultureInfo.InvariantCulture,out int y)||
                !int.TryParse(parts[2],System.Globalization.NumberStyles.AllowLeadingSign,System.Globalization.CultureInfo.InvariantCulture,out int z))return false;
            var position=new Vector3i(x,y,z);
            if((kind==(int)RemoteResourceSourceKind.StaticContainer?RemoteResourceIdentity.Static(position):RemoteResourceIdentity.Workstation(position))!=id)return false;
            if((x>>4)!=(target.x>>4)||(z>>4)!=(target.z>>4))continue;
            string incarnation=(string)source.Attribute("incarnation"),key="RebirthOriginalSourceV2:"+(x&15)+":"+y+":"+(z&15)+":"+incarnation;
            if(!expected.Add(key)||!markers.TryGetValue(key,out var marker))return false;
            var data=Bytes(marker.Element("data"),22+RebirthRemoteResourceSourceEffectCodec.MaximumReceipts*114);
            if(!RebirthRemoteResourceSourceEffectCodec.TryRead(data,incarnation,out var effects)||effects.Length>=RebirthRemoteResourceSourceEffectCodec.MaximumReceipts||
                effects.Any(effect=>effect.Purpose==RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource&&effect.Operation==original.Operation))return false;
            var span=Bytes(marker.Element("span"),131072);int header;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true),true))
            {writer.Write(key);writer.Flush();header=checked((int)stream.Length);}
            if(BitConverter.ToUInt64(span,header)!=ulong.MaxValue||span[header+8]!=0)return false;
            growth=checked(growth+114);
        }
        return growth==expectedGrowth&&markers.Keys.All(expected.Contains)&&markers.Count==expected.Count;
    }
    internal static bool TryValidateSource(Image artifact,RebirthRemoteResourceDurableJournal.Record current)
        =>artifact!=null&&artifact.ValidateSource(current);
    private static bool ValidateSourceBody(Image artifact,XElement image,RebirthRemoteResourceDurableJournal.Record current)
    {
        try
        {
            if(artifact==null||current==null||!RebirthRemoteResourceDurableJournal.TryReadPreparedRevisionBounded(current,artifact.SourceRevision,artifact.SourcePreparedBytes,artifact.SourceReadWorkspace,out var prepared)||
                prepared.Phase!=1||prepared.HasOwnerAction||!prepared.SourcesUnattempted||
                !RebirthRemoteResourcePlacedStationArtifact.TryCountSource(prepared,out long preparedBytes)||preparedBytes!=artifact.SourcePreparedBytes||
                artifact.HasCapturedAllocation&&(!RebirthRemoteResourcePlacedStationAllocation.TryMeasureSourceGraph(prepared,out long sourceGraph)||sourceGraph!=artifact.SourceGraphBytes)||
                !RebirthRemoteResourcePlacedStationOriginal.TryReadAdmission(current,out var admitted)||admitted.Digest!=artifact.Original.Digest)return false;
            var plan=image.Element("plan");
            if((string)plan.Attribute("source")!=current.Operation.ToString("N")||(string)plan.Attribute("binding")!=current.Binding||
                !RebirthRemoteResourcePlacedStationOriginal.ValidateInput(admitted,out var position,out _,out _,out _,out var template,out int batches,out int tier,out int belt,out var payment,out bool turnsOn,out var input))return false;
            var original=admitted.Copy();
            var before=RebirthRemoteResourceClientOriginal.ReadActor(original.Element("actorBefore").Value);
            var required=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Element("requirements").Value));
            var sourceImage=prepared.Copy();
            if(!SourceMarkerGrowth(image,sourceImage,current,position,(int)plan.Attribute("sourceMarkerGrowth")))return false;
            var remote=sourceImage.Element("sources").Elements("source").SelectMany(source=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(source.Element("paid").Value))).ToArray();
            if(remote.Length<1||remote.Length>8192)return false;
            ItemStack[] expectedInput;RebirthCamperEnergyActorImage expectedActor;System.Collections.Generic.List<ItemStack> paid;
            if(payment==RebirthRemoteResourcePlacedStationScope.PaymentSource.StationInput)
            {
                if(!RebirthRemoteResourcePlacedStationInputPlan.TryCreate(input,required,remote,out var grid))return false;
                expectedInput=grid.CopyAfter();expectedActor=before;paid=grid.CopyPaid().ToList();
            }
            else
            {
                if(!RebirthRemoteResourceCraftPlan.IsExactShortfall(before,required,remote,belt)||
                    !RebirthRemoteResourceCraftPlan.TryDebit(before,required,remote,belt,out expectedActor,out paid))return false;
                expectedInput=input;
            }
            if(!expectedActor.Matches(RebirthRemoteResourceClientOriginal.ReadActor(plan.Element("actorAfter").Value))||
                !RebirthRemoteResourcePlacedStationNativeReader.TryRead(Bytes(plan.Element("afterTile"),8*1024*1024),out var after)||
                !RebirthCamperInventorySnapshot.Capture(expectedInput).SequenceEqual(RebirthCamperInventorySnapshot.Capture(after.Input))||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(plan.Element("queue").Value,out var queue))return false;
            int slot=(int)plan.Attribute("slot");var row=queue[slot];
            int entity=(int)plan.Attribute("entity");
            if(row==null||entity<0||row.StartingEntityId!=entity||!RebirthRemoteResourcePlacedStationPaidRow.TryCreate(admitted.Operation,current.Operation,current.Binding,template,paid,batches,tier,entity,slot==queue.Length-1,out var expectedRow,admitted.Copy().Element("action").Element("outputImage").Value))return false;
            // Rebuild the exact admitted row from real original paid fragments,
            // including rich cancellation images and canonical extra outputs.
            // Unrelated queue rows are not replaced or granted by this check.
            var expected=(RecipeQueueItem[])queue.Clone();expected[slot]=expectedRow;
            if(RebirthRemoteResourcePlacedStationQueue.Encode(expected)!=plan.Element("queue").Value)return false;
            var saved=image.Element("savedBefore");var snapshot=Bytes(saved.Element("snapshot"),RebirthStationSnapshotEvidence.MaximumSnapshotBytes);
            int tileOffset=checked((int)(long)saved.Attribute("tileOffset")),tileLength=(int)saved.Attribute("tileLength");var nativeBefore=new byte[tileLength];
            Buffer.BlockCopy(snapshot,tileOffset,nativeBefore,0,tileLength);
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(nativeBefore,out var old)||
                !RebirthCamperInventorySnapshot.Capture(old.Input).SequenceEqual(RebirthCamperInventorySnapshot.Capture(input))||
                !RebirthStationNativeQueueEntry.TryFindVacantSlot(old.Queue,admitted.Operation,out int originalSlot)||originalSlot!=slot)return false;
            var originalQueue=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Element("action").Element("queue").Value,RebirthRemoteResourcePlacedStationQueue.MaximumBytes);
            if(originalQueue.Element("native")?.Value!=RebirthCookingRemoteAdmissionBackend.Queue(old.Queue))return false;
            var ownSources=sourceImage.Element("sources").Elements("source").Where(source=>(int)source.Attribute("kind")== (int)RemoteResourceSourceKind.WorkstationOutput&&
                (string)source.Attribute("id")==RemoteResourceIdentity.Workstation(position)).ToArray();
            if(ownSources.Length>1)return false;
            if(ownSources.Length==1)
            {
                var source=ownSources[0];
                if((string)source.Attribute("incarnation")!=artifact.Birth||
                    !RebirthCamperInventorySnapshot.Capture(old.Output).SequenceEqual(Convert.FromBase64String(source.Element("before").Value)))return false;
                old.Output=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(source.Element("after").Value));
            }
            // Detached re-encoding keeps every unrelated field and queue row
            // exactly original. Only the admitted slot, selected input/output,
            // native TurnOn intent and comparison clock may differ.
            for(int i=0;i<old.Queue.Length;i++)if(i!=slot&&RebirthCookingRemoteAdmissionBackend.Queue(new[]{old.Queue[i]})!=RebirthCookingRemoteAdmissionBackend.Queue(new[]{queue[i]}))return false;
            old.Input=expectedInput;old.Queue=queue;old.Burning=old.Burning||turnsOn;old.Clock=0;
            return RebirthRemoteResourcePlacedStationNativeReader.Encode(old).SequenceEqual(Bytes(plan.Element("afterTile"),8*1024*1024));
        }
        catch{return false;}
    }
    internal static bool TryRead(byte[] bytes,out Image image,RebirthRemoteResourcePlacedStationAllocation.Admission allocation=null,
        RebirthRemoteResourcePlacedStationPreparation.ColdRead cold=null,RebirthRemoteResourcePlacedStationAllocation.Scratch borrowedScratch=null)
    {
        image=null;RebirthRemoteResourcePlacedStationAllocation.Scratch ownedScratch=null;
        try
        {
            object scratchOwner=(object)allocation??cold;
            if(scratchOwner!=null)
            {
                if(borrowedScratch!=null){if(borrowedScratch.Phase!=RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Artifact||!borrowedScratch.IsFor(scratchOwner))return false;}
                else if(allocation!=null)
                {if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Artifact,out ownedScratch))return false;}
                else if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(cold,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Artifact,out ownedScratch))return false;
            }
            if(bytes==null||bytes.Length<1||bytes.Length>RebirthRemoteResourcePlacedStationArtifact.MaximumBytes||allocation!=null&&cold!=null||
                cold!=null&&(!cold.Current()||cold.Header.Bytes!=bytes.LongLength))return false;
            // Reject a forged workspace declaration and XML node explosion
            // before allocating the retained XElement graph. The second pass
            // still validates every field; this pass grants no authority.
            using(var scanStream=new MemoryStream(bytes,false))
            using(var scan=XmlReader.Create(scanStream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,
                MaxCharactersInDocument=RebirthRemoteResourcePlacedStationArtifact.MaximumBytes}))
            {
                int elements=0;
                while(scan.Read())
                {
                    if(scan.Depth>32)return false;
                    if(scan.NodeType==XmlNodeType.Comment||scan.NodeType==XmlNodeType.ProcessingInstruction||scan.NodeType==XmlNodeType.CDATA||scan.NodeType==XmlNodeType.XmlDeclaration)return false;
                    if(scan.NodeType!=XmlNodeType.Element)continue;
                    if(++elements>MaximumElements||scan.AttributeCount>14)return false;
                    if(elements==1)
                    {
                        if(scan.Name!="placedFetchedOriginal"||!long.TryParse(scan.GetAttribute("sourcePreparedBytes"),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out long prepared)||
                            prepared<1||prepared>128L*1024*1024||checked(bytes.LongLength*3+prepared*8+64L*1024*1024)>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
                    }
                    if(scan.MoveToFirstAttribute())do{if(scan.Value.Length>4096)return false;}while(scan.MoveToNextAttribute());
                    scan.MoveToElement();
                }
            }
            XElement root;
            using(var stream=new MemoryStream(bytes,false))
            using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,
                MaxCharactersInDocument=RebirthRemoteResourcePlacedStationArtifact.MaximumBytes,IgnoreComments=false,IgnoreProcessingInstructions=false}))root=XElement.Load(reader);
            if(!RebirthRemoteResourcePlacedStationAllocation.TryAdmitArtifactBeforeHydration(root,bytes.LongLength,out long readWorkspace)||
                readWorkspace<RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes||
                allocation!=null&&(!(borrowedScratch??ownedScratch).IsFor(allocation)||!allocation.ReserveReadWorkspace(readWorkspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes))||
                cold!=null&&(!(borrowedScratch??ownedScratch).IsFor(cold)||!cold.ReserveReadWorkspace(readWorkspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes)))return false;
            string version=(string)root.Attribute("version");
            if(version!="1"&&version!="2"||!Shape(root,"placedFetchedOriginal",version=="2"?new[]{"version","birth","sourcePreparedBytes","sourceRevision","sourceGraphBytes","sourceReadWorkspace"}:new[]{"version","birth","sourcePreparedBytes","sourceRevision"},new[]{"original","plan","savedBefore"})||
                !Guid.TryParseExact((string)root.Attribute("birth"),"N",out var birth)||birth==Guid.Empty||birth.ToString("N")!=(string)root.Attribute("birth"))return false;
            if(version=="2")
            {
                long sourceGraph=(long)root.Attribute("sourceGraphBytes"),sourceWorkspace=(long)root.Attribute("sourceReadWorkspace");
                if(sourceGraph<1||sourceWorkspace<checked(8*sourceGraph+64L*1024*1024)||sourceWorkspace>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace||
                    allocation!=null&&(sourceGraph!=allocation.SourceGraphBytes||sourceWorkspace!=allocation.PlanningBytes)||
                    cold!=null&&(sourceGraph!=cold.Header.SourceGraph||sourceWorkspace!=cold.Header.SourceWorkspace||
                        (long)root.Attribute("sourceRevision")!=cold.Header.Revision||(long)root.Attribute("sourcePreparedBytes")!=cold.Header.SourceBytes||
                        (string)root.Attribute("birth")!=cold.Header.Birth))return false;
            }
            else if(cold!=null)return false;
            long sourceBytes=(long)root.Attribute("sourcePreparedBytes"),revision=(long)root.Attribute("sourceRevision");
            if(sourceBytes<1||sourceBytes>128L*1024*1024||revision<0||checked(bytes.LongLength*3+sourceBytes*8+64L*1024*1024)>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
            var wrapper=root.Element("original");
            if(!Shape(wrapper,"original",Array.Empty<string>(),new[]{"fetchClientOriginal"})||
                !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(wrapper.Elements().Single().ToString(SaveOptions.DisableFormatting),out var original)||
                !RebirthRemoteResourcePlacedStationOriginal.ValidateInput(original,out var position,out uint block,out _,out _,out _,out _,out _,out _,out _,out _,out _))return false;
            var plan=root.Element("plan");var saved=root.Element("savedBefore");
            if(!Shape(plan,"plan",new[]{"job","source","binding","slot","burning","clockOffset","sourceMarkerGrowth","entity"},new[]{"queue","afterTile","actorAfter"})||
                !Shape(saved,"savedBefore",new[]{"job","source","binding","nativeDigest","chunkX","chunkZ","tileOffset","tileLength","clock","raw","compressed","growth","bound"},new[]{"snapshot","nativePayload","markers"})||
                (string)plan.Attribute("job")!=original.Operation.ToString("N")||
                !Guid.TryParseExact((string)plan.Attribute("source"),"N",out var source)||source==Guid.Empty||source.ToString("N")!=(string)plan.Attribute("source"))return false;
            string binding=(string)plan.Attribute("binding");
            if(binding==null||binding.Length!=64||binding.Any(c=>!Uri.IsHexDigit(c))||new[]{"job","source","binding"}.Any(key=>(string)plan.Attribute(key)!=(string)saved.Attribute(key)))return false;
            int growth=(int)plan.Attribute("sourceMarkerGrowth"),clockOffset=(int)plan.Attribute("clockOffset"),slot=(int)plan.Attribute("slot");
            if(growth<0||growth!=(int)saved.Attribute("growth")||slot<0||slot>=255||(int)plan.Attribute("entity")<0||
                plan.Element("queue").HasAttributes||plan.Element("queue").HasElements||plan.Element("queue").Value.Length>RebirthRemoteResourcePlacedStationQueue.MaximumBytes||
                plan.Element("actorAfter").HasAttributes||plan.Element("actorAfter").HasElements||plan.Element("actorAfter").Value.Length>8*1024*1024)return false;
            var snapshot=Bytes(saved.Element("snapshot"),RebirthStationSnapshotEvidence.MaximumSnapshotBytes);
            var payload=Bytes(saved.Element("nativePayload"),RebirthStationRegionPayload.MaximumPayloadBytes);
            long offset=(long)saved.Attribute("tileOffset");int length=(int)saved.Attribute("tileLength");
            if(offset<8||length<1||offset>snapshot.LongLength-length||snapshot.Length<9||
                !snapshot.Take(8).SequenceEqual(new byte[]{116,116,99,0,47,0,0,0})||!payload.Take(8).SequenceEqual(snapshot.Take(8))||
                !RebirthStationChunkInflate.TryDecode(payload,snapshot.Length-8,out var inflated)||!inflated.SequenceEqual(snapshot.Skip(8)))return false;
            using(var sha=SHA256.Create())if(!string.Equals(BitConverter.ToString(sha.ComputeHash(payload)).Replace("-",""),(string)saved.Attribute("nativeDigest"),StringComparison.OrdinalIgnoreCase))return false;
            var before=new byte[length];Buffer.BlockCopy(snapshot,(int)offset,before,0,length);
            var after=Bytes(plan.Element("afterTile"),8*1024*1024);
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(before,out var old)||!RebirthRemoteResourcePlacedStationNativeReader.TryRead(after,out var next)||
                old.LocalPosition.x!=(position.x&15)||old.LocalPosition.y!=position.y||old.LocalPosition.z!=(position.z&15)||
                next.LocalPosition.x!=old.LocalPosition.x||next.LocalPosition.y!=old.LocalPosition.y||next.LocalPosition.z!=old.LocalPosition.z||
                (int)saved.Attribute("chunkX")!=(position.x>>4)||(int)saved.Attribute("chunkZ")!=(position.z>>4)||
                old.ClockOffset!=clockOffset||next.ClockOffset!=clockOffset||old.Clock!=(ulong)saved.Attribute("clock")||next.Clock!=0||next.Burning!=(bool)plan.Attribute("burning")||slot>=next.Queue.Length||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(plan.Element("queue").Value,out var queue)||
                RebirthCookingRemoteAdmissionBackend.Queue(queue)!=RebirthCookingRemoteAdmissionBackend.Queue(next.Queue)||
                !RebirthRemoteResourcePlacedStationCandidate.TryMeasureChunk(snapshot,offset,before,after,growth,out int raw,out int compressed,out long bound)||
                raw!=(int)saved.Attribute("raw")||compressed!=(int)saved.Attribute("compressed")||bound!=(long)saved.Attribute("bound"))return false;
            RebirthRemoteResourceClientOriginal.ReadActor(plan.Element("actorAfter").Value);
            var markers=saved.Element("markers");
            // All admitted native source ledgers may share this chunk;
            // the target's separate native incarnation adds one marker.
            if(markers.HasAttributes||markers.Elements().Count()>RebirthRemoteResourceDurableJournal.MaximumSources+1||markers.Elements().Any(m=>!Shape(m,"marker",new[]{"key","offset"},new[]{"span","data"})))return false;
            var keys=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            bool targetIdentity=false;string targetKey="RebirthLogisticsStationV1:"+old.LocalPosition.x+":"+old.LocalPosition.y+":"+old.LocalPosition.z;
            foreach(var marker in markers.Elements())
            {
                string key=(string)marker.Attribute("key");long start=(long)marker.Attribute("offset");
                var span=Bytes(marker.Element("span"),131072);var data=Bytes(marker.Element("data"),65535,true);
                if(string.IsNullOrEmpty(key)||key.Length>1024||!keys.Add(key)||start<8||start>snapshot.LongLength-span.Length||!span.SequenceEqual(snapshot.Skip((int)start).Take(span.Length)))return false;
                // Native ChunkCustomData.Write: canonical bounded key, ulong
                // expiry, bool network, ushort length, exact data. Never call
                // its unbounded ReadString on an untrusted declared length.
                using(var keyStream=new MemoryStream())
                using(var writer=new BinaryWriter(keyStream,new UTF8Encoding(false,true),true))
                {
                    writer.Write(key);writer.Flush();var keyBytes=keyStream.ToArray();int header=keyBytes.Length;
                    if(span.Length!=header+11+data.Length||!span.Take(header).SequenceEqual(keyBytes)||span[header+8]>1||
                        (span[header+9]|span[header+10]<<8)!=data.Length||!span.Skip(header+11).SequenceEqual(data))return false;
                    if(key==targetKey)
                    {
                        if(data.Length!=19||data[0]!=1||BitConverter.ToUInt64(span,header)!=ulong.MaxValue||span[header+8]!=0||
                            BitConverter.ToUInt16(data,17)!=new BlockValue(block).type)return false;
                        var id=new byte[16];Buffer.BlockCopy(data,1,id,0,16);if(new Guid(id)!=birth)return false;targetIdentity=true;
                    }
                }
            }
            if(!targetIdentity)return false;
            if(scratchOwner!=null&&!(borrowedScratch??ownedScratch).IsFor(scratchOwner))return false;
            image=new Image(root,original,readWorkspace);return true;
        }
        catch{image=null;return false;}
        finally{ownedScratch?.Dispose();}
    }
}
