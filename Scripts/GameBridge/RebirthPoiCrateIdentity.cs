using System;
using System.IO;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>Native, server-owned placement identity. It never changes crate inventory or sign text.</summary>
[Preserve]
public sealed class TEFeatureRebirthPoiCrateIdentity : TEFeatureAbs
{
    private const byte Schema = 1;
    public Guid PlacementId { get; private set; }
    private int poiId = -1;
    private Vector3i poiOrigin, poiSize;

    private static bool IsServer => SingletonMonoBehaviour<ConnectionManager>.Instance != null
        && SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;

    private void ClearIdentity()
    {
        PlacementId = Guid.Empty;
        poiId = -1;
        poiOrigin = poiSize = default(Vector3i);
    }

    public override void OnAdded(Vector3i position, BlockValue blockValue)
    {
        base.OnAdded(position, blockValue);
        ClearIdentity();
        // A new physical placement never inherits a source/prefab's custody identity.
        if (IsServer && Parent?.Owner != null && GameManager.Instance != null && !GameManager.Instance.IsEditMode())
        {
            PlacementId = Guid.NewGuid();
            SetModified();
        }
    }

    public override void CopyFromInternal(TileEntityComposite other)
    {
        // Clone/copy is not evidence of the same physical placement. Native disk/network
        // deserialization uses Read, which is the only restoration path for an existing ID.
        ClearIdentity();
    }

    public override void UpgradeDowngradeFrom(TileEntityComposite other)
    {
        base.UpgradeDowngradeFrom(other);
        ClearIdentity();
    }

    public override void OnBlockReset(Vector3i position, BlockValue blockValue)
    {
        base.OnBlockReset(position, blockValue);
        ClearIdentity();
        if (IsServer) SetModified();
    }

    public override void Reset(FastTags<TagGroup.Global> tags)
    {
        base.Reset(tags);
        ClearIdentity();
        if (IsServer) SetModified();
    }

    public bool IsUnbound => PlacementId != Guid.Empty && poiId < 0;

    public bool MatchesPoi(PrefabInstance poi)
    {
        return PlacementId != Guid.Empty && poi != null && poiId == poi.id
            && poiOrigin.Equals(poi.boundingBoxPosition) && poiSize.Equals(poi.boundingBoxSize);
    }

    public bool HasLocalOwner(EntityPlayerLocal player)
    {
        return player != null && Parent?.Owner != null && PlatformManager.InternalLocalUserIdentifier != null
            && Parent.Owner.Equals(PlatformManager.InternalLocalUserIdentifier);
    }

    public bool TryBindServer(EntityPlayer player, PrefabInstance poi, Guid expected)
    {
        if (!IsServer || player == null || player.world == null || player.world.IsRemote() || player.IsDead()
            || expected == Guid.Empty || PlacementId != expected || Parent?.Owner == null || poi == null
            || Vector3.Distance(player.position, ToWorldCenterPos()) > 8f
            || RebirthGameBridgeWorld.DistanceTo(poi, ToWorldCenterPos()) > 12f
            || blockValue.Block?.GetBlockName() != RebirthGameBridgePoiStorage.CrateName) return false;
        // Native placement records PersistentPlayerData.PrimaryId, which can differ from
        // an internal/cross-platform account key. Match the native placement identity.
        var persistent = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        var placementOwner = persistent?.PrimaryId;
        if (placementOwner == null && player is EntityPlayerLocal)
            placementOwner = PlatformManager.InternalLocalUserIdentifier;
        if (placementOwner == null || !Parent.Owner.Equals(placementOwner)) return false;
        if (poiId >= 0 && !MatchesPoi(poi)) return false;
        if (!RebirthPoiCrateLedgerService.Reserve(player, poi, ToWorldPos(), expected)) return false;
        if (poiId >= 0) return MatchesPoi(poi); // Replays may confirm, never reassign a placed crate.
        if (!ValidScope(poi.id, poi.boundingBoxSize)) return false;
        poiId = poi.id;
        poiOrigin = poi.boundingBoxPosition;
        poiSize = poi.boundingBoxSize;
        SetModified(); // Native authoritative tile sync plus chunk dirty notification.
        return true;
    }

    private static bool ValidScope(int id, Vector3i size)
    {
        return id >= 0 && size.x > 0 && size.y > 0 && size.z > 0
            && size.x <= 2048 && size.y <= 2048 && size.z <= 2048;
    }

    public override void Read(PooledBinaryReader reader, StreamModeRead mode)
    {
        base.Read(reader, mode);
        // Parse into locals first. Client tile writes cannot change server-owned identity.
        bool clientWrite = mode == StreamModeRead.FromClient;
        if (!clientWrite) ClearIdentity();
        if (reader.ReadByte() != Schema) throw new InvalidDataException("Unknown POI crate identity version.");
        byte[] bytes = reader.ReadBytes(16);
        if (bytes.Length != 16) throw new EndOfStreamException();
        Guid placement = new Guid(bytes);
        byte bound = reader.ReadByte();
        if (bound > 1) throw new InvalidDataException("Invalid POI crate binding flag.");
        int id = -1;
        Vector3i origin = default(Vector3i), size = default(Vector3i);
        if (bound == 1)
        {
            id = reader.ReadInt32();
            origin = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            size = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            if (placement == Guid.Empty || !ValidScope(id, size)) throw new InvalidDataException("Invalid POI crate scope.");
        }
        if (clientWrite) return;
        PlacementId = placement;
        poiId = id;
        poiOrigin = origin;
        poiSize = size;
    }

    public override void Write(PooledBinaryWriter writer, StreamModeWrite mode)
    {
        base.Write(writer, mode);
        writer.Write(Schema);
        writer.Write(PlacementId.ToByteArray());
        bool bound = PlacementId != Guid.Empty && poiId >= 0;
        writer.Write((byte)(bound ? 1 : 0));
        if (!bound) return;
        writer.Write(poiId);
        writer.Write(poiOrigin.x); writer.Write(poiOrigin.y); writer.Write(poiOrigin.z);
        writer.Write(poiSize.x); writer.Write(poiSize.y); writer.Write(poiSize.z);
    }
}

internal static class RebirthPoiCrateIdentity
{
    public static TEFeatureRebirthPoiCrateIdentity Resolve(EntityPlayer player, Vector3i position)
    {
        var tile = player?.world?.GetTileEntity(position);
        TEFeatureRebirthPoiCrateIdentity marker;
        return tile != null && tile.TryGetSelfOrFeature(out marker) ? marker : null;
    }

    public static bool RequestBinding(EntityPlayerLocal player, PrefabInstance poi, Vector3i position, Guid expected)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (player == null || poi == null || expected == Guid.Empty || connection == null) return false;
        if (!player.world.IsRemote()) return Resolve(player, position)?.TryBindServer(player, poi, expected) == true;
        connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPoiCrateBinding>()
            .Setup(player.entityId, position, expected, poi.id));
        return true;
    }
}

[Preserve]
public sealed class NetPackageRebirthPoiCrateBinding : NetPackage
{
    private int playerId, poiId;
    private Vector3i position;
    private Guid placement;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthPoiCrateBinding Setup(int owner, Vector3i crate, Guid expected, int poi)
    {
        playerId = owner; position = crate; placement = expected; poiId = poi;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        placement = Guid.Empty;
        if (reader.ReadByte() != 1) throw new InvalidDataException("Unknown POI crate binding request.");
        playerId = reader.ReadInt32();
        position = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        byte[] bytes = reader.ReadBytes(16);
        if (bytes.Length != 16) throw new EndOfStreamException();
        placement = new Guid(bytes);
        poiId = reader.ReadInt32();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        writer.Write((byte)1); writer.Write(playerId);
        writer.Write(position.x); writer.Write(position.y); writer.Write(position.z);
        writer.Write(placement.ToByteArray()); writer.Write(poiId);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || Sender == null || placement == Guid.Empty
            || !ValidEntityIdForSender(playerId)) return;
        var player = world.GetEntity(playerId) as EntityPlayer;
        if (player == null || !ReferenceEquals(player.world, world)) return;
        var prefabs = callbacks?.GetDynamicPrefabDecorator()?.allPrefabs;
        if (prefabs == null) return;
        foreach (PrefabInstance poi in prefabs)
            if (poi != null && poi.id == poiId)
            {
                RebirthPoiCrateIdentity.Resolve(player, position)?.TryBindServer(player, poi, placement);
                return;
            }
    }
}