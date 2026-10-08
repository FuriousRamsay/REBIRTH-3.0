using System;
using System.IO;
using System.Text;
using UnityEngine.Scripting;

internal static class RebirthSelfMedicalPracticeWire
{
    internal static string Read(BinaryReader reader,int limit){int count=reader.ReadByte();if(count>limit)throw new InvalidDataException("Self practice string bound");byte[] data=reader.ReadBytes(count);if(data.Length!=count)throw new EndOfStreamException();foreach(byte b in data)if(b<32 || b>126)throw new InvalidDataException("Self practice ASCII");return Encoding.ASCII.GetString(data);}
    internal static void Write(BinaryWriter writer,string text,int limit){text=text??"";if(text.Length>limit)throw new InvalidDataException("Self practice string bound");foreach(char c in text)if(c<32 || c>126)throw new InvalidDataException("Self practice ASCII");writer.Write((byte)text.Length);writer.Write(Encoding.ASCII.GetBytes(text));}
    internal static Guid ReadGuid(BinaryReader reader){var bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();return new Guid(bytes);}
}
[Preserve]
public sealed class NetPackageRebirthSelfMedicalPracticeRequest:NetPackage
{
    int entityId;string creation="",definition="",item="";Guid epoch;long sequence;float fraction;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthSelfMedicalPracticeRequest Setup(int player,string character,string definitions,Guid session,long number,string treatment,float realized){entityId=player;creation=character;definition=definitions;epoch=session;sequence=number;item=treatment;fraction=realized;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader binary=reader;if(binary.ReadByte()!=1)throw new InvalidDataException("Self practice protocol");entityId=binary.ReadInt32();creation=RebirthSelfMedicalPracticeWire.Read(binary,64);definition=RebirthSelfMedicalPracticeWire.Read(binary,64);epoch=RebirthSelfMedicalPracticeWire.ReadGuid(binary);sequence=binary.ReadInt64();item=RebirthSelfMedicalPracticeWire.Read(binary,96);fraction=binary.ReadSingle();}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter binary=writer;binary.Write((byte)1);binary.Write(entityId);RebirthSelfMedicalPracticeWire.Write(binary,creation,64);RebirthSelfMedicalPracticeWire.Write(binary,definition,64);binary.Write(epoch.ToByteArray());binary.Write(sequence);RebirthSelfMedicalPracticeWire.Write(binary,item,96);binary.Write(fraction);}
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthSelfMedicalPracticeAwards.ReceiveRequest(world,Sender,entityId,creation,definition,epoch,sequence,item,fraction,Sender!=null && ValidEntityIdForSender(entityId));
}
[Preserve]
public sealed class NetPackageRebirthSelfMedicalPracticeResponse:NetPackage
{
    int entityId;string creation="",definition="";Guid epoch;long sequence;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthSelfMedicalPracticeResponse Setup(int player,string character,string definitions,Guid session,long number){entityId=player;creation=character;definition=definitions;epoch=session;sequence=number;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader binary=reader;if(binary.ReadByte()!=1)throw new InvalidDataException("Self practice protocol");entityId=binary.ReadInt32();creation=RebirthSelfMedicalPracticeWire.Read(binary,64);definition=RebirthSelfMedicalPracticeWire.Read(binary,64);epoch=RebirthSelfMedicalPracticeWire.ReadGuid(binary);sequence=binary.ReadInt64();}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter binary=writer;binary.Write((byte)1);binary.Write(entityId);RebirthSelfMedicalPracticeWire.Write(binary,creation,64);RebirthSelfMedicalPracticeWire.Write(binary,definition,64);binary.Write(epoch.ToByteArray());binary.Write(sequence);}
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthSelfMedicalPracticeAwards.ReceiveResponse(world,Sender,entityId,creation,definition,epoch,sequence);
}

