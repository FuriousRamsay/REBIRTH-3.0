using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Server-authoritative transition snapshot for the common REBIRTH NPC
/// presence, ownership, order and travel domains.
/// </summary>
[Preserve]
public sealed class NetPackageRebirthNpcRuntimeState : NetPackage
{
    private int entityId;
    private RebirthNpcStableId stableId;
    private uint worldEpoch;
    private uint revision;
    private RebirthNpcPresenceState presence;
    private RebirthNpcOwnershipKind ownershipKind;
    private string ownerId;
    private RebirthNpcOrderState order;
    private RebirthNpcTravelState travel;
    private Vector3 guardPosition;
    private bool hasGuardPosition;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthNpcRuntimeState Setup(EntityRebirthNPC npc)
    {
        RebirthNpcRuntimeState state = npc.RebirthRuntimeState;
        entityId = npc.entityId;
        stableId = state.StableId;
        worldEpoch = RebirthNpcNetworkEpoch.GetServerEpoch();
        revision = state.Revision;
        presence = state.Presence;
        ownershipKind = state.OwnershipKind;
        ownerId = state.OwnerId ?? string.Empty;
        order = state.Order;
        travel = state.Travel;
        guardPosition = state.GuardPosition;
        hasGuardPosition = state.HasGuardPosition;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        entityId = reader.ReadInt32();
        stableId = new RebirthNpcStableId(reader.ReadUInt64(), reader.ReadUInt64());
        worldEpoch = reader.ReadUInt32();
        revision = reader.ReadUInt32();
        presence = (RebirthNpcPresenceState)reader.ReadByte();
        ownershipKind = (RebirthNpcOwnershipKind)reader.ReadByte();
        ownerId = RebirthNpcNetworkFraming.ReadString(reader, RebirthNpcNetworkFraming.MaxId);
        order = (RebirthNpcOrderState)reader.ReadByte();
        travel = (RebirthNpcTravelState)reader.ReadByte();
        hasGuardPosition = reader.ReadBoolean();
        if (hasGuardPosition)
            guardPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        else
            guardPosition = Vector3.zero;
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(entityId);
        binary.Write(stableId.High);
        binary.Write(stableId.Low);
        binary.Write(worldEpoch);
        binary.Write(revision);
        binary.Write((byte)presence);
        binary.Write((byte)ownershipKind);
        RebirthNpcNetworkFraming.WriteString(binary, ownerId, RebirthNpcNetworkFraming.MaxId);
        binary.Write((byte)order);
        binary.Write((byte)travel);
        binary.Write(hasGuardPosition);
        if (hasGuardPosition)
        {
            binary.Write(guardPosition.x);
            binary.Write(guardPosition.y);
            binary.Write(guardPosition.z);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        // Clients apply server state. A client-originated package is ignored by
        // the authoritative server so this type cannot become a mutation path.
        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return;

        RebirthNpcPendingRuntimeStates.Receive(world, new RebirthNpcPendingRuntimeStates.Snapshot
        {
            EntityId = entityId, StableId = stableId, Epoch = worldEpoch, Revision = revision,
            Presence = presence, Ownership = ownershipKind, Owner = ownerId,
            Order = order, Travel = travel, GuardPosition = guardPosition, HasGuardPosition = hasGuardPosition
        });
    }

    public int GetLength() => 0;
}
