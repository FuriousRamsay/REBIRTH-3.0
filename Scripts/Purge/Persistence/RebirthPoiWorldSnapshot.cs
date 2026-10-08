using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

internal sealed class RebirthPoiShardReference
{
    public readonly string Id,File,Hash;
    public readonly long Revision;
    public readonly int Count;
    public RebirthPoiShardReference(string id,string file,string hash,long revision,int count)
    { Id=id; File=file; Hash=hash; Revision=revision; Count=count; }
}
internal sealed class RebirthPoiWorldSnapshot
{
    public const int RecordsPerShard=512,MaximumShards=4096;
    public readonly long Revision;
    public readonly Guid WorldId;
    internal readonly RebirthPoiWorldBinding Binding;
    internal readonly IReadOnlyDictionary<string,RebirthPoiClearanceLedger> Shards;
    internal readonly IReadOnlyDictionary<string,RebirthPoiShardReference> References;
    internal readonly IReadOnlyDictionary<string,string> RecordShards;
    internal readonly string Manifest;
    public int Count { get { return RecordShards.Count; } }
    internal RebirthPoiWorldSnapshot(RebirthPoiWorldBinding binding,long revision,
        Dictionary<string,RebirthPoiClearanceLedger> shards,Dictionary<string,RebirthPoiShardReference> references,string manifest)
    {
        if(binding==null || revision<0 || shards==null || references==null || shards.Count>MaximumShards || shards.Count!=references.Count) throw new ArgumentException("Invalid world snapshot.");
        var index=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var pair in shards)
        {
            RebirthPoiShardReference reference;
            if(!references.TryGetValue(pair.Key,out reference) || pair.Value.WorldId!=binding.WorldId || pair.Value.Revision>revision ||
                pair.Value.Records.Count>RecordsPerShard || pair.Value.Records.Count!=reference.Count || reference.Revision!=pair.Value.Revision) throw new ArgumentException("Invalid shard scope.");
            foreach(var key in pair.Value.Records.Keys) { if(index.ContainsKey(key)) throw new ArgumentException("Cross-shard duplicate POI."); index.Add(key,pair.Key); }
        }
        Binding=binding; WorldId=binding.WorldId; Revision=revision; Manifest=manifest;
        Shards=new ReadOnlyDictionary<string,RebirthPoiClearanceLedger>(new Dictionary<string,RebirthPoiClearanceLedger>(shards,StringComparer.Ordinal));
        References=new ReadOnlyDictionary<string,RebirthPoiShardReference>(new Dictionary<string,RebirthPoiShardReference>(references,StringComparer.Ordinal));
        RecordShards=new ReadOnlyDictionary<string,string>(index);
    }
    public bool TryGet(RebirthPoiIdentity identity,out RebirthPoiClearanceRecord record)
    {
        record=null; string shard;
        return identity!=null && RecordShards.TryGetValue(identity.Key,out shard) && Shards[shard].Records.TryGetValue(identity.Key,out record) && record.Identity.Biome==identity.Biome;
    }
}