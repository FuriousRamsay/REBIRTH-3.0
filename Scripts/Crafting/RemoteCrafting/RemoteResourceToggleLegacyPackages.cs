using System.IO;
using Platform;
using UnityEngine.Scripting;

// Legacy packet identities retain their exact wire readers, but cannot mutate state or UI.
[Preserve]
public sealed class NetPackageRemoteResourceToggleRequest : NetPackage
{
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private Vector3i position;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        playerId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
        position = new Vector3i(binary.ReadInt32(), binary.ReadInt32(), binary.ReadInt32());
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(playerId); userId.ToStream(binary);
        binary.Write(position.x); binary.Write(position.y); binary.Write(position.z);
    }
    public override void ProcessPackage(World world, GameManager callbacks) { }
    public int GetLength() => 48;
}
[Preserve]
public sealed class NetPackageRemoteResourceStateRequest : NetPackage
{
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private Vector3i position;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        playerId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
        position = new Vector3i(binary.ReadInt32(), binary.ReadInt32(), binary.ReadInt32());
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(playerId); userId.ToStream(binary);
        binary.Write(position.x); binary.Write(position.y); binary.Write(position.z);
    }
    public override void ProcessPackage(World world, GameManager callbacks) { }
    public int GetLength() => 48;
}
[Preserve]
public sealed class NetPackageRemoteResourceToggleResult : NetPackage
{
    private Vector3i position;
    private bool manageable, excluded;
    private string reason;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public override void read(PooledBinaryReader reader)
    {
        position = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        manageable = reader.ReadBoolean(); excluded = reader.ReadBoolean(); reason = reader.ReadString();
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(position.x); binary.Write(position.y); binary.Write(position.z);
        binary.Write(manageable); binary.Write(excluded); binary.Write(reason ?? string.Empty);
    }
    public override void ProcessPackage(World world, GameManager callbacks) { }
    public int GetLength() => 0;
}
[Preserve]
public sealed class NetPackageRemoteResourceStateResult : NetPackage
{
    private Vector3i position;
    private bool manageable, excluded;
    private string reason;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public override void read(PooledBinaryReader reader)
    {
        position = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        manageable = reader.ReadBoolean(); excluded = reader.ReadBoolean(); reason = reader.ReadString();
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(position.x); binary.Write(position.y); binary.Write(position.z);
        binary.Write(manageable); binary.Write(excluded); binary.Write(reason ?? string.Empty);
    }
    public override void ProcessPackage(World world, GameManager callbacks) { }
    public int GetLength() => 0;
}
