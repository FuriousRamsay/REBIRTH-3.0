using System;

// One bounded, authenticated offer assembly. Dispatcher owns scope and session lifetime.
// Discarding this transport object never cancels the durable custody journal.
public sealed class RebirthBackpackLibraryOfferAssembly
{
    public const int ChunkBytes=16384;
    private readonly string creation;
    private readonly Guid transaction;
    private readonly int length;
    private readonly byte[][] chunks;
    private int received;
    private bool finished;
    private RebirthBackpackLibraryReceipt completed;
    public RebirthBackpackLibraryOfferAssembly(Guid creationId,Guid transactionId,int totalBytes) : this(creationId.ToString("N"),transactionId,totalBytes) {}
    public RebirthBackpackLibraryOfferAssembly(string creationId,Guid transactionId,int totalBytes)
    {
        if(!RebirthSurvivorRequestScope.TryNormalize(creationId,out var normalized)||transactionId==Guid.Empty||totalBytes<=0||totalBytes>RebirthBackpackLibraryWireCodec.MaxBytes)
            throw new ArgumentOutOfRangeException(nameof(totalBytes));
        creation=normalized;transaction=transactionId;length=totalBytes;
        chunks=new byte[(length+ChunkBytes-1)/ChunkBytes][];
    }
    public bool TryAdd(int index,byte[] bytes)
    {
        if(index<0||index>=chunks.Length||bytes==null||bytes.Length!=Math.Min(ChunkBytes,length-index*ChunkBytes))return false;
        var existing=chunks[index];
        if(existing!=null)
        {
            for(int i=0;i<bytes.Length;i++)if(existing[i]!=bytes[i])return false;
            return true;
        }
        chunks[index]=(byte[])bytes.Clone();received++;return true;
    }
    public bool TryFinish(out RebirthBackpackLibraryReceipt receipt)
    {
        receipt=null;if(received!=chunks.Length)return false;
        if(finished){receipt=completed;return receipt!=null;}
        finished=true;
        var bytes=new byte[length];
        for(int i=0;i<chunks.Length;i++)Buffer.BlockCopy(chunks[i],0,bytes,i*ChunkBytes,chunks[i].Length);
        if(!RebirthBackpackLibraryWireCodec.TryDecode(bytes,out var candidate)||
            candidate.CreationId!=creation||candidate.TransactionId!=transaction.ToString("N"))return false;
        completed=candidate;receipt=candidate;return true;
    }
}