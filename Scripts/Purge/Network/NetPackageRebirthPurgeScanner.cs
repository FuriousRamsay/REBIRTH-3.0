using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthPurgeScanner : NetPackage
{
    private RebirthPurgeScanner.View view;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthPurgeScanner Setup(RebirthPurgeScanner.View value){view=value;return this;}
    public override void read(PooledBinaryReader reader)
    {
        var value=new RebirthPurgeScanner.View();
        value.Player=reader.ReadInt32();
        value.World=new Guid(reader.ReadBytes(16));value.Session=new Guid(reader.ReadBytes(16));
        value.Sequence=reader.ReadInt64();value.Epoch=reader.ReadInt64();
        value.LifetimeKills=reader.ReadInt64();
        value.PoiKey=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,2048);
        value.Prefab=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,256);
        value.X=reader.ReadInt32();value.Y=reader.ReadInt32();value.Z=reader.ReadInt32();
        value.Tier=reader.ReadInt32();value.Rooms=reader.ReadInt32();value.Cleared=reader.ReadInt32();
        value.Enemies=reader.ReadInt32();value.Approximate=reader.ReadBoolean();
        int count=reader.ReadUInt16();
        if(value.LifetimeKills< -1||value.Sequence<1||value.Epoch<0||count>8192||value.Rooms<0||value.Rooms>4096||value.Cleared<0||value.Cleared>value.Rooms||
            value.Enemies<0||value.Enemies>1000000||value.Tier<0||value.Tier>255)
            throw new InvalidDataException("Invalid Purge scanner projection.");
        value.Targets=new int[count];for(int i=0;i<count;i++)value.Targets[i]=reader.ReadInt32();
        view=value;
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);writer.Write(view.Player);writer.Write(view.World.ToByteArray());writer.Write(view.Session.ToByteArray());
        writer.Write(view.Sequence);writer.Write(view.Epoch);writer.Write(view.LifetimeKills);RebirthSurvivorNetworkCodec.WriteString(writer,view.PoiKey,2048);
        RebirthSurvivorNetworkCodec.WriteString(writer,view.Prefab,256);
        writer.Write(view.X);writer.Write(view.Y);writer.Write(view.Z);writer.Write(view.Tier);
        writer.Write(view.Rooms);writer.Write(view.Cleared);writer.Write(view.Enemies);writer.Write(view.Approximate);
        if(view.Targets.Length>8192)throw new InvalidDataException("Purge scanner actor bound exceeded.");
        writer.Write((ushort)view.Targets.Length);foreach(int id in view.Targets)writer.Write(id);
    }
    public int GetLength()=>108+((view?.Prefab?.Length??0)+(view?.PoiKey?.Length??0))*3+(view?.Targets?.Length??0)*4;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||!world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||
            !RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge)return;
        RebirthPurgeScanner.Receive(world,view);
    }
}
