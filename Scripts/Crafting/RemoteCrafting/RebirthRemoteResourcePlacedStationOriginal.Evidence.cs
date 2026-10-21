using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private const uint CompletionMagic=0x31504650;
    private static void CompletionAncestors(string path)
    {for(var current=new DirectoryInfo(Path.GetFullPath(path));current!=null;current=current.Parent)if(current.Exists&&(current.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();}
    private static string CompletionPath(Guid source,string temporary=null)
    {
        if(source==Guid.Empty)throw new InvalidDataException();string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedSavedAfter");
        CompletionAncestors(directory);string name=source.ToString("N")+".rpe";int count=0;
        if(Directory.Exists(directory))foreach(string path in Directory.EnumerateFileSystemEntries(directory,source.ToString("N")+"*"))
            if(++count>32||Directory.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||
                Path.GetFileName(path)!=name&&!string.Equals(path,temporary,StringComparison.Ordinal))throw new InvalidDataException();
        return Path.Combine(directory,name);
    }
    private static void CompletionBytes(BinaryWriter writer,byte[] bytes){writer.Write(bytes.Length);writer.Write(bytes);}
    private static byte[] CompletionBytes(BinaryReader reader,int maximum)
    {int count=reader.ReadInt32();if(count<1||count>maximum||count>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();var bytes=reader.ReadBytes(count);if(bytes.Length!=count)throw new EndOfStreamException();return bytes;}
    private static string CompletionText(BinaryReader reader,int maximum)
    {
        uint size=0;int groups=0;byte part;
        do{part=reader.ReadByte();if(groups==4&&(part&248)!=0)throw new InvalidDataException();size|=(uint)(part&127)<<(groups*7);groups++;if(groups>5)throw new InvalidDataException();}while((part&128)!=0);
        if(groups>1&&(part&127)==0||size>maximum||size>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();
        var bytes=reader.ReadBytes((int)size);if(bytes.Length!=size)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(bytes);
    }
    private static void WriteCompletion(BinaryWriter writer,WorldApplication original,RebirthRemoteResourceNativePublication.SavedTile saved,
        byte[] snapshot,byte[] payload,string actor,byte[] buffs)
    {
        writer.Write(CompletionMagic);writer.Write(original.Reference.Encode());writer.Write(original.Actor.entityId);writer.Write(original.Artifact.Original.Deaths);
        writer.Write(actor);CompletionBytes(writer,buffs);writer.Write(saved.Publication.ChunkX);writer.Write(saved.Publication.ChunkZ);
        writer.Write(saved.PayloadPublication.Generation);writer.Write(saved.Publication.Path);writer.Write(saved.Publication.PayloadDigest);
        writer.Write(saved.TileOffset);writer.Write(saved.CopyTile().Length);CompletionBytes(writer,snapshot);CompletionBytes(writer,payload);
        var markers=saved.CopyMarkers();writer.Write(markers.Length);
        foreach(var marker in markers){writer.Write(marker.Key);writer.Write(marker.Offset);writer.Write(marker.CopySpan().Length);}
    }
    private static bool CompletionMarker(byte[] snapshot,string key,long offset,int length,byte[] expected)
    {
        if(key==null||key.Length>1024||offset<8||length<1||offset>snapshot.LongLength-length||expected==null||expected.Length>ushort.MaxValue)return false;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true),true))
        {
            writer.Write(key);writer.Write(ulong.MaxValue);writer.Write(false);writer.Write((ushort)expected.Length);writer.Write(expected);writer.Flush();
            if(stream.Length!=length)return false;byte[] span=stream.ToArray();for(int index=0;index<span.Length;index++)if(snapshot[(int)offset+index]!=span[index])return false;return true;
        }
    }
    private static bool CompletionMarkerImage(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,
        string key,out byte[] expected)
    {
        expected=null;var original=artifact.Copy();var marker=original.Element("savedBefore").Element("markers").Elements("marker").SingleOrDefault(value=>(string)value.Attribute("key")==key);
        if(marker==null)return false;byte[] before=Convert.FromBase64String(marker.Element("data").Value);
        string target="RebirthLogisticsStationV1:";
        if(key.StartsWith(target,StringComparison.Ordinal)){expected=before;return true;} // Full original reader already validates exact target birth/type.
        foreach(var source in root.Copy().Element("sources").Elements("source"))
        {
            int kind=(int)source.Attribute("kind");if(source.Element("npc")!=null||kind!=(int)RemoteResourceSourceKind.StaticContainer&&kind!=(int)RemoteResourceSourceKind.WorkstationOutput)continue;
            string id=(string)source.Attribute("id"),incarnation=(string)source.Attribute("incarnation");var parts=id.Substring(2).Split(',');
            if(parts.Length!=3||!int.TryParse(parts[0],out int x)||!int.TryParse(parts[1],out int y)||!int.TryParse(parts[2],out int z))return false;
            string sourceKey="RebirthOriginalSourceV2:"+(x&15)+":"+y+":"+(z&15)+":"+incarnation;
            if(sourceKey!=key)continue;
            string delta=RebirthRemoteResourceDurableJournal.Digest(id+"|"+incarnation+"|1|"+source.Element("after").Value+"|"+source.Element("locks").Value);
            return RebirthRemoteResourceSourceEffectCodec.TryPlan(before,incarnation,RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource,
                root.Operation,root.Owner,root.Binding,1,delta,out expected);
        }
        return false;
    }
    // The full original artifact has already passed its captured historical
    // budget and native reader. This reader validates the *actual* saved After
    // evidence under the separately admitted completion workspace.
    private static bool TryReadCompletion(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,
        RebirthRemoteResourcePlacedStationReference.Image reference,RebirthRemoteResourcePlacedStationAllocation.Admission allocation,out string proof,bool beforePositiveStage=false,
        RebirthRemoteResourcePlacedStationPreparation.ColdRead cold=null,Source4SavedNative source4=null)
    {
        proof=null;RebirthRemoteResourcePlacedStationAllocation.Scratch scratch=null;
        try
        {
            if(root==null||(allocation==null)==(cold==null))return false;
            if(allocation!=null)
            {if(allocation.Source!=root.Operation||allocation.Binding!=root.Binding||!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Completion,out scratch))return false;}
            else
            {
                if(!cold.Current()||cold.Header.Source!=root.Operation||artifact==null||
                    !RebirthRemoteResourcePlacedStationAllocation.TryCompletionWorkspace(artifact,out long workspace,out _)||
                    !cold.ReserveReadWorkspace(checked(artifact.ReadWorkspace+workspace))||
                    !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(cold,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Completion,out scratch))return false;
            }
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||
                !RemoteResourceWorldIdentity.TryGet(GameManager.Instance.World,out string world)||root==null||world!=root.World||
                artifact==null||reference==null||reference.Source!=root.Operation||reference.Binding!=root.Binding||
                (root.Phase!=2&&root.Phase!=4)||(int)root.Copy().Attribute("outcome")!=1||root.Copy().Element("ownerRefund")!=null||
                !System.Xml.Linq.XNode.DeepEquals(root.Copy().Element("ownerAction"),OwnerPermission(root,artifact,reference))||
                !artifact.ValidateSource(root)||!RebirthRemoteResourcePlacedStationAllocation.TryCompletionWorkspace(artifact,out _,out long maximum))return false;
            string observedDigest=null;Source4SavedNative captured=null;string path=CompletionPath(root.Operation);using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var reader=new BinaryReader(input,new UTF8Encoding(false,true),true))
            {
                if(input.Length<1||input.Length>maximum||reader.ReadUInt32()!=CompletionMagic||CompletionText(reader,4096)!=reference.Encode())return false;
                int actorId=reader.ReadInt32(),deaths=reader.ReadInt32();string actor=CompletionText(reader,8*1024*1024);var buffs=CompletionBytes(reader,1024*1024);
                if(actorId!=(int)artifact.Copy().Element("plan").Attribute("entity")||deaths!=artifact.Original.Deaths||actor!=artifact.Copy().Element("plan").Element("actorAfter").Value)return false;
                var owner=new PlayerDataFile{buffData=new MemoryStream(buffs,false)};
                if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(owner,RebirthRemoteResourceOwnerAction.Receipt(artifact.Original.Operation),out float phase,out bool present)||!present||phase!=2f)return false;
                int chunkX=reader.ReadInt32(),chunkZ=reader.ReadInt32();long generation=reader.ReadInt64();string nativePath=CompletionText(reader,4096),digest=CompletionText(reader,64);
                if(generation<1||string.IsNullOrEmpty(nativePath)||digest.Length!=64)return false;
                long offset=reader.ReadInt64();int length=reader.ReadInt32();var snapshot=CompletionBytes(reader,RebirthStationSnapshotEvidence.MaximumSnapshotBytes);
                var payload=CompletionBytes(reader,RebirthStationRegionPayload.MaximumPayloadBytes);
                if(!ValidateInput(artifact.Original,out var position,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _)||
                    chunkX!=(position.x>>4)||chunkZ!=(position.z>>4)||snapshot.Length<9||offset<8||length<1||offset>snapshot.LongLength-length||
                    !snapshot.Take(8).SequenceEqual(new byte[]{116,116,99,0,47,0,0,0})||!payload.Take(8).SequenceEqual(snapshot.Take(8))||
                    RebirthStationPublicationRecord.Digest(payload)!=digest||!RebirthStationChunkInflate.TryDecode(payload,snapshot.Length-8,out var inflated)||
                    !inflated.SequenceEqual(snapshot.Skip(8)))return false;
                var tile=new byte[length];Buffer.BlockCopy(snapshot,(int)offset,tile,0,length);
                var planned=Convert.FromBase64String(artifact.Copy().Element("plan").Element("afterTile").Value);
                if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(tile,out var parsed)||
                    !RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(tile,parsed.ClockOffset,out var canonical,out _)||!canonical.SequenceEqual(planned)||
                    parsed.LocalPosition!=new Vector3i(position.x&15,position.y,position.z&15))return false;
                int count=reader.ReadInt32();var wanted=artifact.Copy().Element("savedBefore").Element("markers").Elements("marker").Select(value=>(string)value.Attribute("key")).ToArray();
                if(count!=wanted.Length||count<1||count>RebirthRemoteResourceDurableJournal.MaximumSources+1)return false;
                var keys=new HashSet<string>(StringComparer.Ordinal);
                for(int index=0;index<count;index++)
                {
                    string key=CompletionText(reader,4096);long start=reader.ReadInt64();int span=reader.ReadInt32();
                    if(!keys.Add(key)||!wanted.Contains(key,StringComparer.Ordinal)||!CompletionMarkerImage(root,artifact,key,out var expected)||
                        !CompletionMarker(snapshot,key,start,span,expected))return false;
                }
                if(input.Position!=input.Length)return false;
                input.Position=0;using(var sha=SHA256.Create())observedDigest=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();
                if(source4!=null)captured=new Source4SavedNative{Snapshot=snapshot,Payload=payload,Tile=tile,Offset=offset,Position=position,CompletionDigest=observedDigest,CompletionLength=input.Length};
            }
            var record=ApplicationJournal(artifact.Original.Operation).Read(artifact.Original.Operation);byte[] command=ApplicationCommand(root,artifact,reference);
            if(record==null||record.State!=(beforePositiveStage?RebirthCamperTransferJournal.Phase.Uncertain:RebirthCamperTransferJournal.Phase.Applied)||record.Vehicle!=root.Operation||record.OwnerCreation!=root.Creation||record.Revision!=0||
                !record.Before.SequenceEqual(command)||!record.After.SequenceEqual(command))return false;
            proof=RebirthRemoteResourceDurableJournal.Digest("native-placed-station-publication|"+root.Operation.ToString("N")+"|"+root.Binding+"|"+reference.Encode()+"|"+observedDigest);
            if(source4!=null)source4.CopyFrom(captured);return true;
        }
        catch{proof=null;return false;}
        finally{scratch?.Dispose();}
    }
    private static bool WriteCompletionCandidate(WorldApplication original)
    {
        RebirthRemoteResourcePlacedStationAllocation.Scratch scratch=null;
        try
        {
            if(original?.Allocation==null||!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(original.Allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Completion,out scratch)||
                !ApplicationAfter(original)||original.Watch==null||!original.Watch.TryCopyPublishedTile(out var saved)||saved.Publication==null||
                !TrySavedPaidOwner(original.Actor,original.Artifact,out string actor,out var buffs)||actor!=original.OwnerActor||
                !RebirthRemoteResourcePlacedStationAllocation.TryCompletionWorkspace(original.Artifact,out _,out long maximum))return false;
            var snapshot=saved.CopySnapshot();var payload=saved.CopyPayload();var tile=saved.CopyTile();
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(tile,out var parsed)||
                !RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(tile,parsed.ClockOffset,out var actual,out _)||!actual.SequenceEqual(original.After)||!ApplicationAfter(original))return false;
            long bytes;using(var counter=new RebirthRemoteResourcePlacedStationStreams.Counter(maximum))using(var writer=new BinaryWriter(counter,new UTF8Encoding(false,true),true))
            {WriteCompletion(writer,original,saved,snapshot,payload,actor,buffs);writer.Flush();bytes=counter.Length;}
            string path=CompletionPath(original.Root.Operation);Directory.CreateDirectory(Path.GetDirectoryName(path));CompletionAncestors(Path.GetDirectoryName(path));
            // An existing or interrupted candidate is never replaced with a
            // new observation; its exact original must resolve independently.
            if(!File.Exists(path))
            {
                string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new BinaryWriter(output,new UTF8Encoding(false,true),true))
                {WriteCompletion(writer,original,saved,snapshot,payload,actor,buffs);writer.Flush();if(output.Length!=bytes)return false;output.Flush(true);}
                if(!ApplicationAfter(original)||CompletionPath(original.Root.Operation,temporary)!=path)return false;File.Move(temporary,path);
            }
            return ApplicationAfter(original);
        }
        catch{return false;}
        finally{scratch?.Dispose();}
    }
    private static bool PublishCompletion(WorldApplication original)
    {
        try
        {
            // Separate method/phase: candidate capture buffers no longer have
            // caller references when the independent full reader begins.
            if(!WriteCompletionCandidate(original))return false;
            // The actual native event precedes this monotone positive journal
            // stage. A crash before it remains Uncertain, never inferred used.
            var journal=ApplicationJournal(original.Artifact.Original.Operation);var record=journal.Read(original.Artifact.Original.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Uncertain||!record.Before.SequenceEqual(original.Command)||!record.After.SequenceEqual(original.Command)||!ApplicationAfter(original)||
                !TryReadCompletion(original.Root,original.Artifact,original.Reference,original.Allocation,out _,true))return false;
            record=journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied&&TryReadCompletion(original.Root,original.Artifact,original.Reference,original.Allocation,out _);
        }
        catch{return false;}
    }
    internal static bool TryObservePlacedPublication(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,out string proof)
    {
        proof=null;
        try
        {
            if(root==null)return false;
            RebirthRemoteResourceClientOriginal.Intent original;RebirthRemoteResourcePlacedStationAllocation.Admission allocation;
            if(root.Phase==4)
            {if(!RebirthRemoteResourcePlacedStationPreparation.TryReadUsedAllocation(actor,root,out original,out allocation))return false;}
            else if(!RebirthRemoteResourcePlacedStationPreparation.TryReadRetained(actor,root,out original,out _,out allocation,out _))return false;
            if(
                !ServerScope(actor,original)||!readySources.TryGetValue(root.Operation,out var ready)||ready.Original!=original.Digest||
                !RebirthRemoteResourcePlacedStationReference.TryRead(ready.Reference,out var reference)||
                !RebirthRemoteResourcePlacedStationReference.TryResolve(root,ready.Reference,allocation,out var artifact))return false;
            return TryReadCompletion(root,artifact,reference,allocation,out proof);
        }
        catch{proof=null;return false;}
    }
}
