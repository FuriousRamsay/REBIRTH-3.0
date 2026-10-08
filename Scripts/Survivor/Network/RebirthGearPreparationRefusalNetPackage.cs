using System;
using System.IO;
using UnityEngine.Scripting;

// Distinct reply phases; this packet caches evidence only, never moves or releases items.
[Preserve]
public sealed class NetPackageRebirthGearPreparationRefusal : NetPackage
{
    private int playerId;private bool acknowledged;private byte[] body;private RebirthGearPreparationRefusal refusal;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthGearPreparationRefusal Setup(int id,RebirthGearPreparationRefusal value,bool retiredAcknowledgment)
    {
        if(id<=0||!RebirthGearPreparationRefusalWireCodec.TryEncode(value,out var bytes)||
            !RebirthGearPreparationRefusalWireCodec.TryDecode(bytes,out var copy))throw new ArgumentException("Invalid original gear refusal.");
        playerId=id;acknowledged=retiredAcknowledgment;body=bytes;refusal=copy;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        body=null;refusal=null;playerId=reader.ReadInt32();byte phase=reader.ReadByte();int length=reader.ReadUInt16();
        if(playerId<=0||phase>1||length<1||length>RebirthGearPreparationRefusalWireCodec.MaximumBytes)
            throw new InvalidDataException("Invalid gear refusal reply length or phase.");
        var bytes=reader.ReadBytes(length);
        if(bytes.Length!=length||!RebirthGearPreparationRefusalWireCodec.TryDecode(bytes,out var copy))throw new InvalidDataException("Invalid gear refusal reply.");
        acknowledged=phase==1;body=bytes;refusal=copy;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(playerId<=0||body==null||refusal==null)throw new InvalidOperationException("Missing gear refusal reply.");
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write((byte)(acknowledged?1:0));
        ((BinaryWriter)writer).Write((ushort)body.Length);((BinaryWriter)writer).Write(body);
    }
    public int GetLength()=>9+(body?.Length??0);
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!ReferenceEquals(callbacks,GameManager.Instance))return;
        if(acknowledged)RebirthGearPreparationRefusalClient.ReceiveAcknowledged(world,playerId,refusal);
        else RebirthGearPreparationRefusalClient.Receive(world,playerId,refusal);
    }
}