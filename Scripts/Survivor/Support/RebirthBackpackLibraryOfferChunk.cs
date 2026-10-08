using System;
using System.IO;

// Packet body format; native NetPackage and authenticated dispatch integration are separate.
public sealed class RebirthBackpackLibraryOfferChunk
{
    public string CreationId {get;private set;}
    public Guid TransactionId {get;private set;}
    public int TotalBytes {get;private set;}
    public int Index {get;private set;}
    private readonly byte[] payload;
    public int BodyLength=>(Guid.TryParse(CreationId,out _)?43:99)+payload.Length;
    private RebirthBackpackLibraryOfferChunk(string creation,Guid transaction,int total,int index,byte[] bytes)
    {CreationId=creation;TransactionId=transaction;TotalBytes=total;Index=index;payload=(byte[])bytes.Clone();}
    public static bool TryCreate(Guid creation,Guid transaction,int total,int index,byte[] bytes,out RebirthBackpackLibraryOfferChunk chunk)
        =>TryCreate(creation.ToString("N"),transaction,total,index,bytes,out chunk);
    public static bool TryCreate(string creation,Guid transaction,int total,int index,byte[] bytes,out RebirthBackpackLibraryOfferChunk chunk)
    {
        chunk=null;
        if(!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)||transaction==Guid.Empty||total<=0||total>RebirthBackpackLibraryWireCodec.MaxBytes||
            index<0||index>=(total+RebirthBackpackLibraryOfferAssembly.ChunkBytes-1)/RebirthBackpackLibraryOfferAssembly.ChunkBytes||
            bytes==null||bytes.Length!=Math.Min(RebirthBackpackLibraryOfferAssembly.ChunkBytes,total-index*RebirthBackpackLibraryOfferAssembly.ChunkBytes))return false;
        chunk=new RebirthBackpackLibraryOfferChunk(normalized,transaction,total,index,bytes);return true;
    }
    public void Write(BinaryWriter writer)
    {
        bool guid=Guid.TryParse(CreationId,out var creation);writer.Write((byte)(guid?1:2));
        if(guid)writer.Write(creation.ToByteArray());else {writer.Write((byte)71);writer.Write(System.Text.Encoding.ASCII.GetBytes(CreationId));}
        writer.Write(TransactionId.ToByteArray());
        writer.Write(TotalBytes);writer.Write(Index);writer.Write((ushort)payload.Length);writer.Write(payload);
    }
    public static bool TryRead(BinaryReader reader,out RebirthBackpackLibraryOfferChunk chunk)
    {
        chunk=null;if(reader==null)return false;
        try
        {
            byte version=reader.ReadByte();string creation;
            if(version==1)creation=new Guid(reader.ReadBytes(16)).ToString("N");
            else if(version==2){if(reader.ReadByte()!=71)return false;var id=reader.ReadBytes(71);if(id.Length!=71)return false;creation=System.Text.Encoding.ASCII.GetString(id);if(!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)||normalized!=creation||Guid.TryParse(creation,out _))return false;}
            else return false;
            var transaction=new Guid(reader.ReadBytes(16));
            int total=reader.ReadInt32(),index=reader.ReadInt32(),count=reader.ReadUInt16();
            if(count<=0||count>RebirthBackpackLibraryOfferAssembly.ChunkBytes)return false;
            var bytes=reader.ReadBytes(count);
            return bytes.Length==count&&TryCreate(creation,transaction,total,index,bytes,out chunk);
        }
        catch{return false;}
    }
    public bool Deliver(RebirthBackpackLibraryOfferInbox inbox,object session)
        =>inbox!=null&&inbox.TryBegin(session,CreationId,TransactionId,TotalBytes)&&
        inbox.TryAdd(session,CreationId,TransactionId,Index,payload);
}