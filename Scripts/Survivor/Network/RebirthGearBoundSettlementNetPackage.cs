using System;
using System.IO;
using UnityEngine.Scripting;
[Preserve]
public sealed class NetPackageRebirthGearBoundSettled : NetPackage
{
    private int playerId;private byte[] body;private RebirthGearSettlement terminal;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthGearBoundSettled Setup(int id,RebirthGearSettlement value)
    {
        if(id<=0||!RebirthGearTerminalWireCodec.TryEncode(value,out var bytes)||
            !RebirthGearTerminalWireCodec.TryDecode(bytes,out var copy))throw new ArgumentException("Invalid bound gear settlement.");
        playerId=id;body=bytes;terminal=copy;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        body=null;terminal=null;playerId=reader.ReadInt32();int length=reader.ReadUInt16();
        if(playerId<=0||length<1||length>RebirthGearTerminalWireCodec.MaximumBytes)throw new InvalidDataException("Invalid bound gear settlement length.");
        var bytes=reader.ReadBytes(length);
        if(bytes.Length!=length||!RebirthGearTerminalWireCodec.TryDecode(bytes,out var value))throw new InvalidDataException("Invalid bound gear settlement.");
        body=bytes;terminal=value;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(playerId<=0||body==null||terminal==null)throw new InvalidOperationException("Missing bound gear settlement.");
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write((ushort)body.Length);((BinaryWriter)writer).Write(body);
    }
    public int GetLength()=>8+(body?.Length??0);
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!RebirthGearTerminalClient.Receive(world,playerId,terminal))return;
        RebirthGearOfferClient.TryFinishCurrentTerminal(world,playerId);
    }
}