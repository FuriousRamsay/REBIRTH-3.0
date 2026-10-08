using System;using System.IO;namespace HydrationDoubles { internal static class ActualHydration {    internal static void RestoreToolbelt(EntityRebirthHumanoidNPC npc,PooledBinaryReader reader)
    {
        if(reader.ReadByte()!=1)throw new InvalidDataException("NPC toolbelt version changed.");
        bool wasReading=npc.inventory.isReading;npc.inventory.isReading=true;
        try
        {
            npc.inventory.ItemGrid.ReadInto(reader,StreamModeRead.Persistency);
            if(npc.inventory.ItemGrid.Length!=npc.inventory.SlotCount)
                throw new InvalidDataException("NPC toolbelt immutable slot count mismatch.");
            int selected=reader.ReadByte();
            if(selected>=npc.inventory.SlotCount)throw new InvalidDataException("NPC selected slot invalid.");
            npc.inventory.SetSelectedSlot(selected);
        }
        finally{npc.inventory.isReading=wasReading;}
    }
    // The installed Equipment.ReadInto invokes wear minEvents when an owner is present.
    // Temporarily detach that owner; rebuild flags/armor data without event publication.
    internal static void RestoreEquipment(EntityRebirthHumanoidNPC npc,PooledBinaryReader reader)
    {
        var owner=npc.equipment.m_entity;
        try{npc.equipment.m_entity=null;npc.equipment.ReadInto(reader,StreamModeRead.Persistency);}
        finally{npc.equipment.m_entity=owner;}
        for(int index=0;index<npc.equipment.ItemGrid.Length;index++)npc.equipment.BuildSlot(index);
        npc.equipment.ResetArmorGroups();
    }
}}