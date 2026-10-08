using System;
using System.IO;
using System.Text;

public sealed class RebirthGearRecoveryIdentity
{
    public const int MaximumBytes=4300;
    public string OwnerKey{get;private set;}public string CreationId{get;private set;}
    public Guid TransactionId{get;private set;}public Guid PublicationId{get;private set;}public int EntryIndex{get;private set;}
    private RebirthGearRecoveryIdentity(){}
    public static bool TryCreate(string owner,string creation,Guid transaction,Guid publication,int entry,out RebirthGearRecoveryIdentity result)
    {
        result=null;if(string.IsNullOrWhiteSpace(owner)||owner.Length>1024||transaction==Guid.Empty||publication==Guid.Empty||entry<0||entry>=189||
            !RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized))return false;
        result=new RebirthGearRecoveryIdentity{OwnerKey=owner,CreationId=normalized,TransactionId=transaction,PublicationId=publication,EntryIndex=entry};return true;
    }
    public byte[] Encode()
    {
        var utf8=new UTF8Encoding(false,true);var owner=utf8.GetBytes(OwnerKey);var creation=utf8.GetBytes(CreationId);
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {writer.Write((byte)1);writer.Write(TransactionId.ToByteArray());writer.Write(PublicationId.ToByteArray());writer.Write((ushort)EntryIndex);writer.Write((ushort)owner.Length);writer.Write(owner);writer.Write((byte)creation.Length);writer.Write(creation);return stream.ToArray();}
    }
    public static bool TryDecode(byte[] bytes,out RebirthGearRecoveryIdentity result)
    {
        result=null;if(bytes==null||bytes.Length>MaximumBytes)return false;
        try{using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
        {if(reader.ReadByte()!=1)return false;var tx=reader.ReadBytes(16);var publication=reader.ReadBytes(16);if(tx.Length!=16||publication.Length!=16)return false;int entry=reader.ReadUInt16();int ownerLength=reader.ReadUInt16();if(ownerLength<1||ownerLength>4096)return false;var owner=reader.ReadBytes(ownerLength);int creationLength=reader.ReadByte();if(creationLength!=32&&creationLength!=71)return false;var creation=reader.ReadBytes(creationLength);if(owner.Length!=ownerLength||creation.Length!=creationLength||stream.Position!=stream.Length)return false;var utf8=new UTF8Encoding(false,true);return TryCreate(utf8.GetString(owner),utf8.GetString(creation),new Guid(tx),new Guid(publication),entry,out result);}}
        catch{return false;}
    }
}