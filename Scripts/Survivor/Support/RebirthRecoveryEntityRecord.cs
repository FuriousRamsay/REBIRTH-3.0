using System;
using System.IO;

// Exact installed v38 custom recovery record framing, Persistency mode only.
// No ItemValue/Bag/entity decode, allocation from declared blob size, or callbacks.
public sealed class RebirthRecoveryEntityRecord
{
    public int EntityId{get;private set;}public int ClassId{get;private set;}
    public float Lifetime{get;private set;}public float X{get;private set;}public float Y{get;private set;}public float Z{get;private set;}
    public int BagOffset{get;private set;}public int BagLength{get;private set;}
    public int MetadataOffset{get;private set;}public int MetadataLength{get;private set;}public int EndOffset{get;private set;}
    private RebirthRecoveryEntityRecord(){}
    public static bool TryRead(byte[] payload,int offset,int expectedCustomClass,out RebirthRecoveryEntityRecord record)
    {
        record=null;if(payload==null||payload.Length>RebirthRecoveryChunkPayload.MaximumExpandedBytes||offset<0||offset>=payload.Length)return false;
        try{using(var stream=new MemoryStream(payload,false))using(var reader=new BinaryReader(stream))
        {
            stream.Position=offset;if(reader.ReadByte()!=38)return false;
            int classId=reader.ReadInt32(),entity=reader.ReadInt32();if(classId!=expectedCustomClass||entity<=0)return false;
            float lifetime=Float(reader),x=Float(reader),y=Float(reader),z=Float(reader);if(lifetime<0)return false;
            Float(reader);Float(reader);Float(reader);Flag(reader);
            // BodyDamage version4 is exactly version, damageType and flags (3 int32).
            if(reader.ReadInt32()!=4)return false;Skip(stream,8);
            if(Flag(reader))return false; // Custom recovery entities never carry EntityStats.
            Skip(stream,2);if(!Flag(reader))return false;
            int bagLength=reader.ReadInt32(),bagOffset=checked((int)stream.Position);
            if(bagLength<=0||bagLength>4*1024*1024)return false;Skip(stream,bagLength);
            Skip(stream,15); // Home XYZ, home range, spawner source.
            int metadataLength=reader.ReadUInt16(),metadataOffset=checked((int)stream.Position);
            if(metadataLength<=0)return false;Skip(stream,metadataLength);
            if(Flag(reader))return false; // Recovery is neither trader nor drone/player/native itemClass.
            Float(reader);Skip(stream,24); // Stress, requestedBy int64, requestKey16.
            record=new RebirthRecoveryEntityRecord{ClassId=classId,EntityId=entity,Lifetime=lifetime,X=x,Y=y,Z=z,BagOffset=bagOffset,BagLength=bagLength,MetadataOffset=metadataOffset,MetadataLength=metadataLength,EndOffset=checked((int)stream.Position)};return true;
        }}catch{return false;}
    }
    private static float Float(BinaryReader reader){float value=reader.ReadSingle();if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException();return value;}
    private static bool Flag(BinaryReader reader){byte value=reader.ReadByte();if(value>1)throw new InvalidDataException();return value!=0;}
    private static void Skip(Stream stream,int count){if(count<0||stream.Position+count>stream.Length)throw new EndOfStreamException();stream.Position+=count;}
}