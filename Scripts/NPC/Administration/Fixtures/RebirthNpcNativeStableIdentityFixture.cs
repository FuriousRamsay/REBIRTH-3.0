#if DEBUG
using System;
using System.IO;

// Prepared only. Actual native extension codec; no synthetic identity authority.
internal static class RebirthNpcNativeStableIdentityFixture
{
    internal static void Verify()
    {
        var id=new RebirthNpcStableId(7,19);byte[] bytes;
        using(var stream=new MemoryStream())
        {
            using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
            {RebirthNpcNativeStableIdentityCodec.Write(writer,id);writer.Write(0x52424431);writer.Write((byte)1);writer.Write(id.High);writer.Write(id.Low);writer.Write("owner");}
            bytes=stream.ToArray();Require(bytes.Length<ushort.MaxValue);
        }
        using(var reader=new BinaryReader(new MemoryStream(bytes)))
        {Require(RebirthNpcNativeStableIdentityCodec.Read(reader)==id);Require(reader.BaseStream.Position==21&&reader.ReadInt32()==0x52424431);}
        // Legacy dog bytes must remain untouched for the existing RBD1 reader.
        using(var reader=new BinaryReader(new MemoryStream(bytes,21,bytes.Length-21)))
        {long start=reader.BaseStream.Position;Require(RebirthNpcNativeStableIdentityCodec.Read(reader).IsEmpty&&reader.BaseStream.Position==start);Require(reader.ReadInt32()==0x52424431);}
        for(int length=4;length<21;length++)
        {var shortImage=new byte[length];Array.Copy(bytes,shortImage,length);Refuses(()=>Read(shortImage));}
        var future=(byte[])bytes.Clone();future[4]=2;Refuses(()=>Read(future));
        var empty=(byte[])bytes.Clone();Array.Clear(empty,5,16);Refuses(()=>Read(empty));
        using(var reader=new BinaryReader(new MemoryStream(new byte[0])))Require(RebirthNpcNativeStableIdentityCodec.Read(reader).IsEmpty);
        RebirthNpcNativeStableIdentityCodec.RequireNativeEntityDataLength(ushort.MaxValue-RebirthNpcNativeStableIdentityCodec.Size+RebirthNpcNativeStableIdentityCodec.Size);
        Refuses(()=>RebirthNpcNativeStableIdentityCodec.RequireNativeEntityDataLength((long)ushort.MaxValue+1));
        Refuses(()=>RebirthNpcNativeStableIdentityCodec.RequireNativeEntityDataLength(-1));
    }
    private static void Read(byte[] bytes){using(var reader=new BinaryReader(new MemoryStream(bytes)))RebirthNpcNativeStableIdentityCodec.Read(reader);}
    private static void Refuses(Action action){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Require(rejected);}
    private static void Require(bool condition){if(!condition)throw new InvalidOperationException("Native stable identity codec fixture failed");}
}

#endif
