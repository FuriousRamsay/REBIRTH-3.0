using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

// Small detached summary, not a completion/reward claim. Native transport authenticates
// direction; request/world/session bind it to the original map synchronization scope.
internal sealed class RebirthPurgeObjectiveFrame
{
    internal const int MaximumBytes=65536,MaximumBiomes=64,MaximumEligible=200000;
    internal sealed class Biome
    {
        internal readonly string Name;
        internal readonly int Eligible,Discovered,Cleared;
        internal readonly IReadOnlyDictionary<int,int> EligibleByTier,ClearedByTier;
        internal Biome(string name,int eligible,int discovered,int cleared,IDictionary<int,int> tiers,IDictionary<int,int> clears)
        {
            Name=RebirthPoiIdentity.Canonical(name,128);
            if(Name!=name || eligible<0 || eligible>MaximumEligible || discovered<0 || discovered>eligible || cleared<0 || cleared>discovered || tiers==null || clears==null || tiers.Count>256 || clears.Count>256)throw new ArgumentException("Invalid objective counts.");
            long total=0,completed=0;
            foreach(var pair in tiers) { if(pair.Key<0 || pair.Key>255 || pair.Value<1 || pair.Value>eligible)throw new ArgumentException("Invalid tier total."); total+=pair.Value; }
            foreach(var pair in clears) { int available; if(pair.Value<1 || !tiers.TryGetValue(pair.Key,out available) || pair.Value>available)throw new ArgumentException("Invalid tier clearance."); completed+=pair.Value; }
            if(total!=eligible || completed!=cleared)throw new ArgumentException("Inconsistent tier totals.");
            Eligible=eligible;Discovered=discovered;Cleared=cleared;
            EligibleByTier=new ReadOnlyDictionary<int,int>(new Dictionary<int,int>(tiers));
            ClearedByTier=new ReadOnlyDictionary<int,int>(new Dictionary<int,int>(clears));
        }
    }
    internal readonly Guid Request,World,Session;
    internal readonly long Sequence,Revision,Generation;
    internal readonly bool Known;
    internal readonly int TargetPercentage;
    internal readonly IReadOnlyDictionary<string,Biome> Biomes;
    internal RebirthPurgeObjectiveFrame(Guid request,Guid world,Guid session,long sequence,long revision,long generation,bool known,IEnumerable<Biome> biomes,int targetPercentage=75)
    {
        if(request==Guid.Empty || world==Guid.Empty || session==Guid.Empty || sequence<1 || revision<0 || generation<0 || biomes==null || targetPercentage<1 || targetPercentage>100)throw new ArgumentException("Invalid objective scope.");
        var copy=new Dictionary<string,Biome>(StringComparer.Ordinal);long eligible=0,encodedLength=79;
        foreach(var biome in biomes) { if(biome==null || copy.Count>=MaximumBiomes || copy.ContainsKey(biome.Name))throw new ArgumentException("Invalid objective biome.");copy.Add(biome.Name,biome);eligible+=biome.Eligible;encodedLength+=16+Encoding.UTF8.GetByteCount(biome.Name)+9L*biome.EligibleByTier.Count; }
        if(eligible>MaximumEligible || encodedLength>MaximumBytes || !known && copy.Count!=0)throw new ArgumentException("Invalid objective publication.");
        Request=request;World=world;Session=session;Sequence=sequence;Revision=revision;Generation=generation;Known=known;TargetPercentage=targetPercentage;Biomes=new ReadOnlyDictionary<string,Biome>(copy);
    }
    internal static byte[] Encode(RebirthPurgeObjectiveFrame frame)
    {
        if(frame==null)throw new ArgumentNullException(nameof(frame));
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true),true))
        {
            writer.Write(0x504f4232);writer.Write(frame.Request.ToByteArray());writer.Write(frame.World.ToByteArray());writer.Write(frame.Session.ToByteArray());
            writer.Write(frame.Sequence);writer.Write(frame.Revision);writer.Write(frame.Generation);writer.Write(frame.Known);writer.Write((byte)frame.TargetPercentage);writer.Write((byte)frame.Biomes.Count);
            foreach(var biome in frame.Biomes.Values)
            {
                var name=Encoding.UTF8.GetBytes(biome.Name);writer.Write((ushort)name.Length);writer.Write(name);
                writer.Write(biome.Eligible);writer.Write(biome.Discovered);writer.Write(biome.Cleared);writer.Write((ushort)biome.EligibleByTier.Count);
                foreach(var tier in biome.EligibleByTier) { int cleared;biome.ClearedByTier.TryGetValue(tier.Key,out cleared);writer.Write((byte)tier.Key);writer.Write(tier.Value);writer.Write(cleared); }
            }
            writer.Flush();if(stream.Length>MaximumBytes)throw new ArgumentException("Objective frame exceeds bound.");return stream.ToArray();
        }
    }
    internal static bool TryDecode(byte[] bytes,out RebirthPurgeObjectiveFrame frame)
    {
        frame=null;if(bytes==null || bytes.Length<79 || bytes.Length>MaximumBytes)return false;
        try
        {
            using(var stream=new MemoryStream((byte[])bytes.Clone(),false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true),true))
            {
                if(reader.ReadInt32()!=0x504f4232)return false;
                var request=new Guid(reader.ReadBytes(16));var world=new Guid(reader.ReadBytes(16));var session=new Guid(reader.ReadBytes(16));
                long sequence=reader.ReadInt64(),revision=reader.ReadInt64(),generation=reader.ReadInt64();byte known=reader.ReadByte();if(known>1)return false;
                int target=reader.ReadByte();if(target<1 || target>100)return false;int count=reader.ReadByte();if(count>MaximumBiomes)return false;var biomes=new List<Biome>();
                for(int n=0;n<count;n++)
                {
                    int length=reader.ReadUInt16();if(length<1 || length>512 || length>stream.Length-stream.Position)return false;
                    string name=new UTF8Encoding(false,true).GetString(reader.ReadBytes(length));int eligible=reader.ReadInt32(),discovered=reader.ReadInt32(),cleared=reader.ReadInt32();
                    int tiers=reader.ReadUInt16();if(tiers>256)return false;var totals=new Dictionary<int,int>();var completed=new Dictionary<int,int>();
                    for(int t=0;t<tiers;t++) { int tier=reader.ReadByte(),total=reader.ReadInt32(),done=reader.ReadInt32();totals.Add(tier,total);if(done<0)return false;if(done>0)completed.Add(tier,done); }
                    biomes.Add(new Biome(name,eligible,discovered,cleared,totals,completed));
                }
                if(stream.Position!=stream.Length)return false;
                frame=new RebirthPurgeObjectiveFrame(request,world,session,sequence,revision,generation,known==1,biomes,target);return true;
            }
        }
        catch(ArgumentException){return false;}catch(IOException){return false;}catch(OverflowException){return false;}
    }
}