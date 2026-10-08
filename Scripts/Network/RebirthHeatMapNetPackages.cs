using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthHeatMapRequest : NetPackage
{
    private int playerId;
    private int requestId;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthHeatMapRequest Setup(int entityId, int sequence)
    {
        playerId = entityId;
        requestId = sequence;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader r = (BinaryReader)reader;
        playerId = r.ReadInt32();
        requestId = r.ReadInt32();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter w = (BinaryWriter)writer;
        w.Write(playerId);
        w.Write(requestId);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || !ValidEntityIdForSender(playerId)) return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        if (player == null) return;

        float activity, cooldown;
        int regionX, regionZ;
        bool ready;
        bool available = RebirthHeatMapReader.Read(world, player.position, out activity, out cooldown, out ready, out regionX, out regionZ);

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection != null && connection.IsServer)
            connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthHeatMapResponse>()
                .Setup(playerId, requestId, activity, cooldown, ready, available, regionX, regionZ), _attachedToEntityId: playerId);
    }

    public int GetLength() => 12;
}

[Preserve]
public sealed class NetPackageRebirthHeatMapResponse : NetPackage
{
    private int playerId;
    private int requestId;
    private float activity;
    private float cooldown;
    private bool ready;
    private bool available;
    private int regionX;
    private int regionZ;

    public NetPackageRebirthHeatMapResponse Setup(int ownerEntityId, int sequence, float value, float cooldownSeconds, bool isReady, bool isAvailable, int x, int z)
    {
        playerId = ownerEntityId;
        requestId = sequence;
        activity = value;
        cooldown = cooldownSeconds;
        ready = isReady;
        available = isAvailable;
        regionX = x;
        regionZ = z;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader r = (BinaryReader)reader;
        playerId = r.ReadInt32();
        requestId = r.ReadInt32();
        activity = r.ReadSingle();
        cooldown = r.ReadSingle();
        ready = r.ReadBoolean();
        available = r.ReadBoolean();
        regionX = r.ReadInt32();
        regionZ = r.ReadInt32();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter w = (BinaryWriter)writer;
        w.Write(playerId);
        w.Write(requestId);
        w.Write(activity);
        w.Write(cooldown);
        w.Write(ready);
        w.Write(available);
        w.Write(regionX);
        w.Write(regionZ);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || !world.IsRemote()) return;
        EntityPlayerLocal player = world.GetPrimaryPlayer();
        if (player == null || player.entityId != playerId) return;
        RebirthHeatMapHudState.Receive(playerId, requestId, activity, cooldown, ready, available, regionX, regionZ);
    }

    public int GetLength() => 33;
}
