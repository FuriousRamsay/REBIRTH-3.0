using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

// Native ItemValue metadata only. One manifest and indexed chunks are published
// with the same native vehicle save witness; no additional/external file writer.
internal static class RebirthCamperEnergyDeviceLeaseStorage
{
    internal const string Key="RebirthCamperEnergyDeviceLease";
    private const string ChunkPrefix="RebirthCamperEnergyDeviceLeasePart_";
    private const int ChunkBytes=49149; // Base64 65532 ASCII bytes, below canonical per-string bound.
    internal static int MaximumBytes=>RebirthCamperRemoteTransport.MaximumFrame;
    private static string Hash(byte[] value){using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(value));}
    internal static byte[] Read(ItemValue item)
    {
        string value;if(item==null||!item.TryGetMetadata(Key,out value))return Array.Empty<byte>();
        if(value==null||value.Length>((long)MaximumBytes+2)/3*4)throw new InvalidOperationException("Bounded native lease metadata required.");
        if(!value.StartsWith("parts1:",StringComparison.Ordinal))
        {var legacy=Convert.FromBase64String(value);if(legacy.Length>MaximumBytes)throw new InvalidOperationException("Legacy native lease exceeds transport frame.");return legacy;}
        var parts=value.Split(':');
        if(parts.Length!=4||!int.TryParse(parts[1],out int count)||!int.TryParse(parts[2],out int length)||length<1||length>MaximumBytes||count!=(length+ChunkBytes-1)/ChunkBytes)
            throw new InvalidOperationException("Malformed native device lease manifest.");
        var result=new byte[length];int offset=0;
        for(int index=0;index<count;index++)
        {
            if(!item.TryGetMetadata(ChunkPrefix+index,out string encoded)||encoded.Length>65532)throw new InvalidOperationException("Missing/bounded native device lease chunk.");
            var chunk=Convert.FromBase64String(encoded);int expected=Math.Min(ChunkBytes,length-offset);
            if(chunk.Length!=expected)throw new InvalidOperationException("Native lease chunk shape changed.");
            Array.Copy(chunk,0,result,offset,chunk.Length);offset+=chunk.Length;
        }
        if(Hash(result)!=parts[3])throw new InvalidOperationException("Native lease chunk digest changed.");return result;
    }
    internal static void Write(ItemValue live,byte[] bytes)
    {
        if(live==null||bytes==null||bytes.Length==0||bytes.Length>MaximumBytes)throw new InvalidOperationException("Complete transport-bounded native device lease required.");
        // All allocation, native typed-value construction and byte-key-count
        // checks complete detached before replacing this metadata dictionary.
        var candidate=live.Clone();candidate.Metadata=candidate.Metadata==null?new Dictionary<string,TypedMetadataValue>():candidate.Metadata.ToDictionary(p=>p.Key,p=>p.Value?.Clone());
        if(candidate.Metadata.Values.Any(value=>value==null||value.GetValue()==null))throw new InvalidOperationException("Existing native metadata cannot be safely serialized; retain it.");
        foreach(string name in candidate.Metadata.Keys.Where(IsOwnedPart).ToArray())candidate.Metadata.Remove(name);
        int count=(bytes.Length+ChunkBytes-1)/ChunkBytes;
        if(candidate.Metadata.Count+(candidate.Metadata.ContainsKey(Key)?0:1)+count>byte.MaxValue)
            throw new InvalidOperationException("Native byte metadata-key budget cannot safely hold the restoration manifest.");
        for(int index=0;index<count;index++)
        {
            int offset=index*ChunkBytes,length=Math.Min(ChunkBytes,bytes.Length-offset);var part=new byte[length];Array.Copy(bytes,offset,part,0,length);
            candidate.SetMetadata(ChunkPrefix+index,Convert.ToBase64String(part));
        }
        candidate.SetMetadata(Key,"parts1:"+count+":"+bytes.Length+":"+Hash(bytes));
        if(!Read(candidate).SequenceEqual(bytes))throw new InvalidOperationException("Native lease preflight differs.");
        live.Metadata=candidate.Metadata;
    }
    private static bool IsOwnedPart(string name)=>name.StartsWith(ChunkPrefix,StringComparison.Ordinal)&&int.TryParse(name.Substring(ChunkPrefix.Length),out int index)&&index>=0;
}
