using System;
using UnityEngine.Scripting;

// Retain native package registration and wire layout for the retired Workbench route.
[Preserve]
public sealed class NetPackageRebirthWorkbenchCraftRequest : NetPackage
{
    internal Guid Operation;
    internal bool Acknowledgement;
    internal Vector3i Position;
    internal string Creation, Recipe, Definition;
    internal uint Block;
    internal int Deaths, Count, Tier;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public override void read(PooledBinaryReader reader)
    {
        RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);
        Operation = new Guid(reader.ReadBytes(16)); Acknowledgement = reader.ReadBoolean();
        Position = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        if (Acknowledgement) return;
        Creation = RebirthCookingGridCustody.ReadWireString(reader, 512); Deaths = reader.ReadInt32();
        Block = reader.ReadUInt32(); Recipe = RebirthCookingGridCustody.ReadWireString(reader, 128);
        Definition = RebirthCookingGridCustody.ReadWireString(reader, 64); Count = reader.ReadInt32(); Tier = reader.ReadInt32();
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); writer.Write(Operation.ToByteArray()); writer.Write(Acknowledgement);
        writer.Write(Position.x); writer.Write(Position.y); writer.Write(Position.z);
        if (Acknowledgement) return;
        RebirthCookingGridCustody.WriteWireString(writer, Creation, 512); writer.Write(Deaths);
        writer.Write(Block); RebirthCookingGridCustody.WriteWireString(writer, Recipe, 128);
        RebirthCookingGridCustody.WriteWireString(writer, Definition, 64); writer.Write(Count); writer.Write(Tier);
    }
    public override void ProcessPackage(World world, GameManager callbacks) { }
    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthWorkbenchCraftResult : NetPackage
{
    internal Guid Operation;
    internal byte Phase;
    internal int Slot;
    internal string Row;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public override void read(PooledBinaryReader reader)
    {
        RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);
        Operation = new Guid(reader.ReadBytes(16)); Phase = reader.ReadByte(); Slot = reader.ReadInt32();
        Row = RebirthCookingGridCustody.ReadWireString(reader, 48 * 1024);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); writer.Write(Operation.ToByteArray()); writer.Write(Phase); writer.Write(Slot);
        RebirthCookingGridCustody.WriteWireString(writer, Row, 48 * 1024);
    }
    public override void ProcessPackage(World world, GameManager callbacks) { }
    public int GetLength() => 0;
}
