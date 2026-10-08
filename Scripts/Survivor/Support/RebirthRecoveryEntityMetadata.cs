using System;
using System.IO;

// Detached compact metadata only; never allocates or initializes a native entity.
public sealed class RebirthRecoveryEntityMetadata
{
    public RebirthGearRecoveryIdentity Identity{get;private set;}
    public ulong WorldTimeBorn{get;private set;}
    public int SpawnById{get;private set;}public bool AllowShare{get;private set;}
    public string LootList{get;private set;}public string Name{get;private set;}
    public int BelongsPlayerId{get;private set;}public int ClientEntityId{get;private set;}public int OwnerId{get;private set;}
    private RebirthRecoveryEntityMetadata(){}
    public static bool TryRead(byte[] payload,RebirthRecoveryEntityRecord record,byte expectedStaticSource,bool backpack,out RebirthRecoveryEntityMetadata metadata)
    {
        metadata=null;if(payload==null||record==null||record.MetadataOffset<0||record.MetadataLength<=0||record.MetadataOffset>payload.Length-record.MetadataLength)return false;
        try{using(var stream=new MemoryStream(payload,record.MetadataOffset,record.MetadataLength,false))using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadByte()!=expectedStaticSource)return false;ulong time=reader.ReadUInt64();
            var candidate=ReadBody(reader,backpack);candidate.WorldTimeBorn=time;metadata=candidate;return true;
        }}catch{return false;}
    }
    internal static RebirthRecoveryEntityMetadata ReadBody(BinaryReader reader,bool backpack)
    {
        if(reader.ReadByte()!=1)throw new InvalidDataException("Unsupported recovery metadata schema.");
        var value=new RebirthRecoveryEntityMetadata();
        if(backpack){value.SpawnById=reader.ReadInt32();byte share=reader.ReadByte();if(share>1)throw new InvalidDataException("Invalid recovery sharing flag.");value.AllowShare=share!=0;value.LootList=RebirthGearRecoveryStringWire.Read(reader);value.Name=RebirthGearRecoveryStringWire.Read(reader);}
        else{value.BelongsPlayerId=reader.ReadInt32();value.ClientEntityId=reader.ReadInt32();value.OwnerId=reader.ReadInt32();}
        int length=reader.ReadUInt16();if(length<=0||length>RebirthGearRecoveryIdentity.MaximumBytes)throw new InvalidDataException("Invalid recovery identity length.");
        var bytes=reader.ReadBytes(length);if(bytes.Length!=length||!RebirthGearRecoveryIdentity.TryDecode(bytes,out var identity))throw new InvalidDataException("Invalid recovery identity.");
        if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new InvalidDataException("Trailing recovery metadata.");
        value.Identity=identity;return value;
    }
}