using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

// Transport framing only: a complete body confers no source/owner permission.
// Callers authenticate their native connection and derive actualScope before
// accepting fragments, then validate the complete domain original again.
internal static class RebirthRemoteResourceFragments
{
    internal const int ChunkBytes=9000;
    internal const int ChunkText=12000;
    internal const int MaximumBody=32*1024*1024;
    private const int MaximumBuffered=64*1024*1024,MaximumAssemblies=8;
    private sealed class Assembly
    {
        internal string Hash;internal int Total,Count,Received,Bytes;internal double Created,Updated;internal byte[][] Parts;
    }
    internal sealed class Body
    {
        internal readonly string Hash;internal readonly int Length;private readonly byte[] bytes;
        internal int Count=>(Length+ChunkBytes-1)/ChunkBytes;
        internal Body(byte[] value)
        {
            if(value==null||value.Length<1||value.Length>MaximumBody)throw new InvalidDataException("Original transport body exceeds bounds.");
            bytes=(byte[])value.Clone();Length=bytes.Length;Hash=Digest(bytes);
        }
        internal string Part(int index)
        {
            if(index<0||index>=Count)throw new InvalidDataException();int offset=index*ChunkBytes;
            return Convert.ToBase64String(bytes,offset,Math.Min(ChunkBytes,Length-offset));
        }
    }
    private static readonly Dictionary<string,Assembly> pending=new Dictionary<string,Assembly>(StringComparer.Ordinal);
    private static int buffered;
    private static string Digest(byte[] bytes)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    private static bool Hex(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static void Remove(string key)
    {if(pending.TryGetValue(key,out var value)){buffered-=value.Bytes;pending.Remove(key);}}
    internal static void ClearTransient()
    {pending.Clear();buffered=0;}
    internal static bool TryAdd(string actualScope,Guid original,string hash,int total,int count,int index,string text,double now,out byte[] complete)
    {
        complete=null;
        try
        {
            if(string.IsNullOrEmpty(actualScope)||actualScope.Length>2048||actualScope.Any(char.IsControl)||original==Guid.Empty||!Hex(hash)||
                total<1||total>MaximumBody||count!=(total+ChunkBytes-1)/ChunkBytes||index<0||index>=count||text==null||text.Length>ChunkText||
                double.IsNaN(now)||double.IsInfinity(now))return false;
            byte[] part=Convert.FromBase64String(text);int expected=Math.Min(ChunkBytes,total-index*ChunkBytes);
            if(part.Length!=expected||Convert.ToBase64String(part)!=text)return false;
            foreach(var expired in pending.Where(pair=>now<pair.Value.Created||now-pair.Value.Updated>30d).Select(pair=>pair.Key).ToArray())Remove(expired);
            string key=actualScope+"|"+original.ToString("N");
            if(!pending.TryGetValue(key,out var assembly))
            {
                if(pending.Count>=MaximumAssemblies||buffered>MaximumBuffered-part.Length)return false;
                assembly=new Assembly{Hash=hash,Total=total,Count=count,Parts=new byte[count][],Created=now,Updated=now};pending.Add(key,assembly);
            }
            // Conflicting headers cannot replace an already retained body.
            if(assembly.Hash!=hash||assembly.Total!=total||assembly.Count!=count)return false;
            if(assembly.Parts[index]!=null)return assembly.Parts[index].SequenceEqual(part);
            if(buffered>MaximumBuffered-part.Length)return false;
            assembly.Parts[index]=part;assembly.Bytes+=part.Length;assembly.Received++;assembly.Updated=now;buffered+=part.Length;
            if(assembly.Received!=count)return true;
            byte[] result=new byte[total];for(int n=0;n<count;n++)Buffer.BlockCopy(assembly.Parts[n],0,result,n*ChunkBytes,assembly.Parts[n].Length);
            if(Digest(result)!=hash){Remove(key);return false;}
            Remove(key);complete=result;return true;
        }
        catch{complete=null;return false;}
    }
}
