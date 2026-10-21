using System.Collections.Generic;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Server-authoritative initial/refresh replication for a human NPC's selected
/// model pipeline, persistent SDCS appearance seed and equipped armour.
/// </summary>
[Preserve]
public sealed class NetPackageRebirthNpcAppearanceEquipmentV2 : NetPackage
{
    private int entityId;
    private RebirthNpcStableId stableId;
    private uint worldEpoch;
    private uint revision, semanticRevision;
    private ulong recoveryGeneration;
    private RebirthHumanNpcAppearanceDescriptor appearance;
    private Equipment equipment;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthNpcAppearanceEquipmentV2 Setup(EntityRebirthHumanoidNPC npc)
    {
        entityId = npc.entityId;
        stableId = npc.RebirthRuntimeState.StableId;
        worldEpoch = RebirthNpcNetworkEpoch.GetServerEpoch();
        revision = npc.RebirthRuntimeState.Revision;
        recoveryGeneration = RebirthNpcRecoveryGeneration.RequireCurrent();
        semanticRevision = RebirthNpcEquipmentService.GetSnapshot(stableId).Revision;
        appearance = npc.RebirthAppearance;
        equipment = null;
        if(npc.equipment!=null)
        {
            equipment=new Equipment();
            if(equipment.GetSlotCount()!=npc.equipment.GetSlotCount())throw new InvalidDataException("Native appearance equipment shape changed.");
            for(int i=0;i<equipment.GetSlotCount();i++)equipment.ItemGrid[i]=npc.equipment.ItemGrid[i].Clone();
            int[] cosmetics=npc.equipment.GetCosmeticIDs();
            for(int i=0;i<equipment.GetSlotCount()&&i<cosmetics.Length;i++)equipment.SetCosmeticSlot(i,cosmetics[i]);
            if(npc.equipment.m_unlockedCosmetics.Count>RebirthNpcNetworkFraming.MaxUnlockedCosmetics)throw new InvalidDataException("Appearance cosmetics exceed lossless projection codec.");
            equipment.m_unlockedCosmetics.AddRange(npc.equipment.m_unlockedCosmetics);
        }
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        equipment = null;
        if(reader.ReadUInt16()!=2)throw new InvalidDataException("Unsupported appearance projection codec.");
        entityId = reader.ReadInt32();
        stableId = new RebirthNpcStableId(reader.ReadUInt64(), reader.ReadUInt64());
        worldEpoch = reader.ReadUInt32();
        recoveryGeneration = reader.ReadUInt64();
        semanticRevision = reader.ReadUInt32();
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
        binary.Write((ushort)2);
        binary.Write(entityId);
        binary.Write(stableId.High);
        binary.Write(stableId.Low);
        binary.Write(worldEpoch);
        binary.Write(recoveryGeneration);
        binary.Write(semanticRevision);
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
        int unlockedCount = unlocked.Count;
        if(unlockedCount>RebirthNpcNetworkFraming.MaxUnlockedCosmetics)throw new InvalidDataException("Appearance cosmetics exceed lossless projection codec.");
        binary.Write(unlockedCount);
        for (int i = 0; i < unlockedCount; i++)
            binary.Write(unlocked[i]);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthNpcPendingAppearanceSnapshots.Receive(world,new RebirthNpcPendingAppearanceSnapshots.Snapshot
        {Entity=entityId,Stable=stableId,Epoch=worldEpoch,Generation=recoveryGeneration,Revision=revision,SemanticRevision=semanticRevision,Appearance=appearance,Equipment=equipment});
    }
    public int GetLength() => 0;
}
