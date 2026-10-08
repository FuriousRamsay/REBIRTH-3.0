using System.Collections.Generic;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

/// <summary>Server-to-client read-only semantic equipment projection.</summary>
[Preserve]
public sealed class NetPackageRebirthNpcSemanticEquipment : NetPackage
{
    private int entityId;
    private string stableId;
    private uint worldEpoch;
    private uint revision;
    private Dictionary<RebirthNpcEquipmentSlot, string> slots;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthNpcSemanticEquipment Setup(EntityRebirthHumanoidNPC npc, RebirthNpcEquipmentSnapshot snapshot)
    {
        entityId = npc.entityId;
        stableId = snapshot.NpcId.ToString();
        worldEpoch = RebirthNpcNetworkEpoch.GetServerEpoch();
        revision = snapshot.Revision;
        slots = new Dictionary<RebirthNpcEquipmentSlot, string>(snapshot.Slots);
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        entityId = reader.ReadInt32();
        stableId = RebirthNpcNetworkFraming.ReadString(reader, RebirthNpcNetworkFraming.MaxId);
        worldEpoch = reader.ReadUInt32();
        revision = reader.ReadUInt32();
        int count = RebirthNpcNetworkFraming.ReadByteCount(reader, RebirthNpcNetworkFraming.MaxEquipmentSlots, "semantic equipment slots");
        slots = new Dictionary<RebirthNpcEquipmentSlot, string>(count);
        for (int i = 0; i < count; i++)
            slots[(RebirthNpcEquipmentSlot)reader.ReadByte()] = RebirthNpcNetworkFraming.ReadString(reader, RebirthNpcNetworkFraming.MaxId);
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(entityId);
        RebirthNpcNetworkFraming.WriteString(binary, stableId, RebirthNpcNetworkFraming.MaxId);
        binary.Write(worldEpoch);
        binary.Write(revision);
        int slotCount = slots != null ? System.Math.Min(slots.Count, RebirthNpcNetworkFraming.MaxEquipmentSlots) : 0;
        binary.Write((byte)slotCount);
        if (slots == null) return;
        var ordered = new List<KeyValuePair<RebirthNpcEquipmentSlot, string>>(slots);
        ordered.Sort((left, right) => left.Key.CompareTo(right.Key));
        for (int i = 0; i < slotCount; i++)
        {
            KeyValuePair<RebirthNpcEquipmentSlot, string> pair = ordered[i];
            binary.Write((byte)pair.Key);
            RebirthNpcNetworkFraming.WriteString(binary, pair.Value, RebirthNpcNetworkFraming.MaxId);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer || !RebirthNpcNetworkEpoch.AcceptClientEpoch(worldEpoch)) return;
        RebirthNpcStableId id;
        if (!RebirthNpcStableId.TryParse(stableId, out id)) return;
        EntityRebirthHumanoidNPC npc = world.GetEntity(entityId) as EntityRebirthHumanoidNPC;
        if (npc == null || npc.RebirthRuntimeState == null || !npc.RebirthRuntimeState.StableId.Equals(id)) return;
        RebirthNpcNativeEquipmentBridge.ApplyReplicatedSnapshot(npc, id, revision, slots);
    }

    public int GetLength() => 0;
}
