using System.Collections.Generic;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Server-authoritative initial/refresh replication for a human NPC's selected
/// model pipeline, persistent SDCS appearance seed and equipped armour.
/// </summary>
[Preserve]
public sealed class NetPackageRebirthNpcAppearanceEquipment : NetPackage
{
    private int entityId;
    private RebirthNpcStableId stableId;
    private uint worldEpoch;
    private uint revision;
    private RebirthHumanNpcAppearanceDescriptor appearance;
    private Equipment equipment;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthNpcAppearanceEquipment Setup(EntityRebirthHumanoidNPC npc)
    {
        entityId = npc.entityId;
        stableId = npc.RebirthRuntimeState.StableId;
        worldEpoch = RebirthNpcNetworkEpoch.GetServerEpoch();
        revision = npc.RebirthRuntimeState.Revision;
        appearance = npc.RebirthAppearance;
        equipment = npc.equipment;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        equipment = null;
        entityId = reader.ReadInt32();
        stableId = new RebirthNpcStableId(reader.ReadUInt64(), reader.ReadUInt64());
        worldEpoch = reader.ReadUInt32();
        revision = reader.ReadUInt32();
        appearance = RebirthNpcAppearanceBinaryCodec.Read(reader);

        if (!reader.ReadBoolean())
            return;

        ItemValue[] items = GameUtils.ReadItemValueArray(reader);
        equipment = new Equipment();
        if (items == null || items.Length != equipment.GetSlotCount())
            throw new InvalidDataException("NPC equipment slot count does not match the installed version.");
        int count = items.Length;
        for (int i = 0; i < count; i++)
            equipment.ItemGrid[i] = items[i] == null || items[i].IsEmpty() ? ItemStack.Empty.Clone() : new ItemStack(items[i], 1);
        for (int i = 0; i < count; i++)
            equipment.SetCosmeticSlot(i, reader.ReadInt32());

        int unlockedCount = RebirthNpcNetworkFraming.ReadCount(reader, RebirthNpcNetworkFraming.MaxUnlockedCosmetics, "unlocked cosmetics");
        for (int i = 0; i < unlockedCount; i++)
            equipment.m_unlockedCosmetics.Add(reader.ReadInt32());
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
        RebirthNpcAppearanceBinaryCodec.Write(binary, appearance);
        binary.Write(equipment != null);

        if (equipment == null)
            return;

        var equippedItems = new ItemValue[equipment.GetSlotCount()];
        for (int i = 0; i < equippedItems.Length; i++) equippedItems[i] = equipment.GetSlotItem(i);
        GameUtils.WriteItemValueArray(writer, equippedItems);
        int[] cosmetics = equipment.GetCosmeticIDs();
        for (int i = 0; i < cosmetics.Length; i++)
            binary.Write(cosmetics[i]);

        List<int> unlocked = equipment.m_unlockedCosmetics;
        int unlockedCount = System.Math.Min(unlocked.Count, RebirthNpcNetworkFraming.MaxUnlockedCosmetics);
        binary.Write(unlockedCount);
        for (int i = 0; i < unlockedCount; i++)
            binary.Write(unlocked[i]);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer || !RebirthNpcNetworkEpoch.AcceptClientEpoch(worldEpoch)) return;
        EntityRebirthHumanoidNPC npc = world.GetEntity(entityId) as EntityRebirthHumanoidNPC;
        if (npc == null || npc.RebirthRuntimeState == null || !npc.RebirthRuntimeState.StableId.Equals(stableId) || !appearance.IsValid)
            return;

        bool appearanceAccepted = npc.ApplyReplicatedHumanAppearance(appearance, revision);
        if (!appearanceAccepted) return;
        if (equipment != null && npc.equipment != null)
        {
            int slotCount = equipment.GetSlotCount();
            int count = Utils.FastMin(slotCount, npc.equipment.GetSlotCount());
            for (int i = 0; i < count; i++)
                npc.equipment.ItemGrid[i] = equipment.ItemGrid[i];
            int[] cosmetics = equipment.GetCosmeticIDs();
            for (int i = 0; i < count && i < cosmetics.Length; i++)
                npc.equipment.SetCosmeticSlot(i, cosmetics[i]);
        }
        npc.RefreshRebirthModelEquipment();
    }

    public int GetLength() => 0;
}
