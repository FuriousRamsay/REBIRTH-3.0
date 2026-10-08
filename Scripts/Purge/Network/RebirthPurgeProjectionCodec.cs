using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// Detached projection bytes only. Neither parsing nor a digest authenticates a sender,
// proves native authored causality, commits world state, or authorizes a map marker.
internal static class RebirthPurgeProjectionCodec
{
    public const int MaximumMessageBytes=1048576;
    private const int Magic=0x50525031, HeaderBytes=148;
    private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
    internal sealed class Message
    {
        public readonly Guid WorldId,SnapshotId;
        public readonly long Revision;
        public readonly int Sequence,ShardCount;
        public readonly string ManifestHash,ShardId,ShardHash;
        public readonly RebirthPoiClearanceLedger Ledger;
        internal Message(Guid world,Guid snapshot,long revision,int sequence,int count,string manifest,string shard,string hash,RebirthPoiClearanceLedger ledger)
        {WorldId=world;SnapshotId=snapshot;Revision=revision;Sequence=sequence;ShardCount=count;ManifestHash=manifest;ShardId=shard;ShardHash=hash;Ledger=ledger;}
    }
    // Caller supplies the original durable Published snapshot, never a proposed successor.
    // Returning bytes is not evidence that this precondition or native effects were true.
    public static bool TryWrite(RebirthPoiWorldSnapshot snapshot,Guid snapshotId,int sequence,out byte[] bytes)
    {
        bytes=null;
        if(snapshot==null || snapshotId==Guid.Empty || snapshot.Binding==null || !snapshot.Binding.IsCurrent ||
            snapshot.WorldId!=snapshot.Binding.WorldId || sequence<0 || sequence>=snapshot.References.Count) return false;
        try
        {
            long revision;System.Collections.Generic.Dictionary<string,RebirthPoiShardReference> manifestReferences;
            if(!RebirthPoiManifestCodec.TryRead(snapshot.Manifest,snapshot.Binding,out revision,out manifestReferences) ||
                revision!=snapshot.Revision || manifestReferences.Count!=snapshot.References.Count) return false;
            var keys=snapshot.References.Keys.OrderBy(k=>k,StringComparer.Ordinal).ToArray();
            // Validate the whole reference envelope before selecting an original shard.
            foreach(var key in keys)
            {
                RebirthPoiShardReference actual;
                var r=snapshot.References[key];
                if(r==null || r.Id!=key || !manifestReferences.TryGetValue(key,out actual) || actual.Id!=r.Id || actual.File!=r.File ||
                    actual.Hash!=r.Hash || actual.Revision!=r.Revision || actual.Count!=r.Count) return false;
            }
            string id=keys[sequence];var reference=snapshot.References[id];var ledger=snapshot.Shards[id];
            RebirthPoiClearanceLedger same;
            if(ledger.WorldId!=snapshot.WorldId || ledger.Records.Count<1 ||
                !ledger.TryDiscover(snapshot.Binding.Scope,snapshot.WorldId,ledger.Revision,ledger.Records.Values.First().Identity,out same) ||
                !ReferenceEquals(same,ledger)) return false;
            string text=RebirthPoiClearanceCodec.Write(ledger);
            int length=Utf8.GetByteCount(text);
            if(length>MaximumMessageBytes-HeaderBytes || Digest(text)!=reference.Hash || ledger.Records.Count!=reference.Count ||
                ledger.Records.Count<1 || ledger.Records.Count>RebirthPoiWorldSnapshot.RecordsPerShard) return false;
            using(var stream=new MemoryStream(HeaderBytes+length)) using(var writer=new BinaryWriter(stream,Utf8,true))
            {
                writer.Write(Magic);writer.Write(snapshot.WorldId.ToByteArray());writer.Write(snapshotId.ToByteArray());
                writer.Write(snapshot.Revision);writer.Write(sequence);writer.Write(keys.Length);
                writer.Write(HexBytes(Digest(snapshot.Manifest)));writer.Write(HexBytes(id));writer.Write(HexBytes(reference.Hash));
                writer.Write(reference.Revision);writer.Write(reference.Count);writer.Write(length);writer.Write(Utf8.GetBytes(text));writer.Flush();
                if(!snapshot.Binding.IsCurrent) return false;bytes=stream.ToArray();return true;
            }
        }
        catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}catch(OverflowException){return false;}
    }
    // All expected fields come from an independently authenticated original envelope.
    // No positional record reconstruction or relaxed Cleared promotion is performed.
    public static bool TryRead(byte[] bytes,Guid expectedWorld,object originalScope,Guid expectedSnapshot,long expectedRevision,
        string expectedManifestHash,int expectedSequence,int expectedShardCount,RebirthPoiShardReference expectedShard,out Message message)
    {
        message=null;
        if(bytes==null || bytes.Length<HeaderBytes || bytes.Length>MaximumMessageBytes || expectedWorld==Guid.Empty ||
            originalScope==null || expectedSnapshot==Guid.Empty || expectedRevision<1 || expectedSequence<0 ||
            expectedShardCount<1 || expectedShardCount>RebirthPoiWorldSnapshot.MaximumShards || expectedSequence>=expectedShardCount ||
            expectedShard==null || !RebirthPoiManifestCodec.LowerHex(expectedManifestHash,64) ||
            !RebirthPoiManifestCodec.LowerHex(expectedShard.Id,32) || !RebirthPoiManifestCodec.LowerHex(expectedShard.Hash,64) ||
            expectedShard.Revision<1 || expectedShard.Revision>expectedRevision || expectedShard.Count<1 ||
            expectedShard.Count>RebirthPoiWorldSnapshot.RecordsPerShard) return false;
        try
        {
            // Clone once after full byte bound check: caller mutation cannot alter parsed input.
            var input=(byte[])bytes.Clone();
            using(var stream=new MemoryStream(input,false)) using(var reader=new BinaryReader(stream,Utf8,true))
            {
                if(reader.ReadInt32()!=Magic || new Guid(reader.ReadBytes(16))!=expectedWorld || new Guid(reader.ReadBytes(16))!=expectedSnapshot ||
                    reader.ReadInt64()!=expectedRevision || reader.ReadInt32()!=expectedSequence || reader.ReadInt32()!=expectedShardCount ||
                    ToHex(reader.ReadBytes(32))!=expectedManifestHash || ToHex(reader.ReadBytes(16))!=expectedShard.Id ||
                    ToHex(reader.ReadBytes(32))!=expectedShard.Hash || reader.ReadInt64()!=expectedShard.Revision || reader.ReadInt32()!=expectedShard.Count) return false;
                int length=reader.ReadInt32();
                if(length<1 || length>MaximumMessageBytes-HeaderBytes || length!=stream.Length-stream.Position) return false;
                string text=Utf8.GetString(reader.ReadBytes(length));RebirthPoiClearanceLedger ledger;
                if(Digest(text)!=expectedShard.Hash || !RebirthPoiClearanceCodec.TryRead(text,expectedWorld,originalScope,out ledger) ||
                    ledger.Revision!=expectedShard.Revision || ledger.Records.Count!=expectedShard.Count || stream.Position!=stream.Length) return false;
                message=new Message(expectedWorld,expectedSnapshot,expectedRevision,expectedSequence,expectedShardCount,
                    expectedManifestHash,expectedShard.Id,expectedShard.Hash,ledger);return true;
            }
        }
        catch(ArgumentException){return false;}catch(IOException){return false;}catch(OverflowException){return false;}
    }
    private static string Digest(string text)
    {using(var hash=SHA256.Create())return ToHex(hash.ComputeHash(Utf8.GetBytes(text)));}
    private static string ToHex(byte[] data){return BitConverter.ToString(data).Replace("-","").ToLowerInvariant();}
    private static byte[] HexBytes(string text)
    {var bytes=new byte[text.Length/2];for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(text.Substring(i*2,2),16);return bytes;}
}

