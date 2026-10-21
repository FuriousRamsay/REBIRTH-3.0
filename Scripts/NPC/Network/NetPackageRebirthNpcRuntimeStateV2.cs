using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Explicit V2 recovery generation framing; the original V1 layout is unchanged.
/// Server-authoritative transition snapshot for the common REBIRTH NPC
/// presence, ownership, order and travel domains.
/// </summary>
[Preserve]
public sealed class NetPackageRebirthNpcRuntimeStateV2 : NetPackage
{
    private int entityId;
    private RebirthNpcStableId stableId;
    private uint worldEpoch;
    private uint revision;
    private ulong recoveryGeneration;
    private RebirthNpcPresenceState presence;
    private RebirthNpcOwnershipKind ownershipKind;
    private string ownerId;
    private RebirthNpcOrderState order;
    private RebirthNpcTravelState travel;
    private Vector3 guardPosition;
    private bool hasGuardPosition;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthNpcRuntimeStateV2 Setup(EntityRebirthNPC npc)
    {
        RebirthNpcRuntimeState state = npc.RebirthRuntimeState;
        entityId = npc.entityId;
        stableId = state.StableId;
        worldEpoch = RebirthNpcNetworkEpoch.GetServerEpoch();
        revision = state.Revision;
        recoveryGeneration = RebirthNpcRecoveryGeneration.RequireCurrent();
        presence = state.Presence;
        ownershipKind = state.OwnershipKind;
        ownerId = state.OwnerId ?? string.Empty;
        order = state.Order;
        travel = state.Travel;
        guardPosition = state.GuardPosition;
        hasGuardPosition = state.HasGuardPosition;
        Validate();return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadUInt16()!=2)throw new InvalidDataException("Unsupported runtime projection codec.");
        entityId = reader.ReadInt32();
        stableId = new RebirthNpcStableId(reader.ReadUInt64(), reader.ReadUInt64());
        worldEpoch = reader.ReadUInt32();
        recoveryGeneration = reader.ReadUInt64();
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
        Validate();
    }

    private void Validate()
    {
        if(entityId<=0||stableId.IsEmpty||worldEpoch==0||ownerId==null||ownerId.Length>RebirthNpcNetworkFraming.MaxId||
            !System.Enum.IsDefined(typeof(RebirthNpcPresenceState),presence)||!System.Enum.IsDefined(typeof(RebirthNpcOwnershipKind),ownershipKind)||
            !System.Enum.IsDefined(typeof(RebirthNpcOrderState),order)||!System.Enum.IsDefined(typeof(RebirthNpcTravelState),travel)||
            float.IsNaN(guardPosition.x)||float.IsInfinity(guardPosition.x)||float.IsNaN(guardPosition.y)||float.IsInfinity(guardPosition.y)||float.IsNaN(guardPosition.z)||float.IsInfinity(guardPosition.z))
            throw new InvalidDataException("Invalid V2 runtime projection.");
    }

    public override void write(PooledBinaryWriter writer)
    {
        Validate();
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write((ushort)2);
        binary.Write(entityId);
        binary.Write(stableId.High);
        binary.Write(stableId.Low);
        binary.Write(worldEpoch);
        binary.Write(recoveryGeneration);
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
            EntityId = entityId, StableId = stableId, Epoch = worldEpoch, Revision = revision, Generation = recoveryGeneration,
            Presence = presence, Ownership = ownershipKind, Owner = ownerId,
            Order = order, Travel = travel, GuardPosition = guardPosition, HasGuardPosition = hasGuardPosition
        });
    }

    public int GetLength() => 0;
}
