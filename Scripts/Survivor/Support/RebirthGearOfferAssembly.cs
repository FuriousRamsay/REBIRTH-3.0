using System;
using System.Security.Cryptography;

// One authenticated transport assembly; caller owns session/timeout limits.
// Refusing/expiring fragments never cancels persistent gear custody.
public sealed class RebirthGearOfferAssembly
{
    public const int ChunkBytes=16384;
    private readonly string creation, digest;
    private readonly Guid transaction;
    private readonly long revision;
    private readonly int length;
    private readonly byte[][] chunks;
    private int received;
    private bool failed;
    private RebirthGearTransferState completed;
    public RebirthGearOfferAssembly(string creationId,Guid transactionId,long expectedRevision,int totalBytes,string sha256)
    {
        if(!RebirthSurvivorRequestScope.TryNormalize(creationId,out creation)||transactionId==Guid.Empty||
            expectedRevision<0||expectedRevision==long.MaxValue||totalBytes<=0||totalBytes>RebirthGearOfferWireCodec.MaxBytes||
            sha256==null||sha256.Length!=64)throw new ArgumentException("Invalid gear offer envelope.");
        foreach(char c in sha256)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))throw new ArgumentException("Invalid gear offer digest.");
        transaction=transactionId;revision=expectedRevision;length=totalBytes;digest=sha256;
        chunks=new byte[(length+ChunkBytes-1)/ChunkBytes][];
    }
    public static string ComputeDigest(byte[] bytes)
    {
        if(bytes==null||bytes.Length==0||bytes.Length>RebirthGearOfferWireCodec.MaxBytes)throw new ArgumentException("Invalid gear offer bytes.");
        using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
    }
    public bool TryAdd(RebirthGearOfferFragment fragment)
    {
        if(fragment==null||fragment.CreationId!=creation||fragment.TransactionId!=transaction||
            fragment.ExpectedRevision!=revision||fragment.TotalBytes!=length||fragment.Digest!=digest)return Refuse();
        return TryAdd(fragment.Index,fragment.CopyPayload());
    }
    public bool TryAdd(int index,byte[] bytes)
    {
        if(failed)return false;
        if(index<0||index>=chunks.Length||bytes==null||bytes.Length!=Math.Min(ChunkBytes,length-index*ChunkBytes))return Refuse();
        var prior=chunks[index];
        if(prior!=null){for(int i=0;i<bytes.Length;i++)if(prior[i]!=bytes[i])return Refuse();return true;}
        chunks[index]=(byte[])bytes.Clone();received++;return true;
    }
    private bool Refuse(){failed=true;completed=null;Array.Clear(chunks,0,chunks.Length);return false;}
    public bool TryFinish(out RebirthGearTransferState offer)
    {
        offer=null;if(failed||received!=chunks.Length)return false;
        if(completed!=null){offer=completed;return true;}
        var bytes=new byte[length];
        for(int i=0;i<chunks.Length;i++)Buffer.BlockCopy(chunks[i],0,bytes,i*ChunkBytes,chunks[i].Length);
        if(ComputeDigest(bytes)!=digest||!RebirthGearOfferWireCodec.TryDecode(bytes,out var candidate)||
            candidate.CreationId!=creation||candidate.TransactionId!=transaction.ToString("N")||candidate.ExpectedRevision!=revision)return Refuse();
        completed=candidate;offer=candidate;return true;
    }
}