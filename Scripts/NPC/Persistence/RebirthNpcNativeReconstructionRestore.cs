using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// Restores only an original held offworld embodiment. No native creation/publication or identity grants.
internal static class RebirthNpcNativeReconstructionRestore
{
    internal static bool TryRestore(EntityRebirthHumanoidNPC npc,RebirthNpcPersistentRecord person,out string reason)
    {
        reason="Original held NPC reconstruction required.";
        var world=GameManager.Instance?.World;var state=npc?.RebirthRuntimeState;var evidence=person?.NativeReconstruction;
        if(npc==null||world==null||world.IsRemote()||!ReferenceEquals(npc.world,world)||!npc.IsPreparedRestorationPending||
            world.GetEntity(npc.entityId)!=null||state==null||evidence==null||
            !RebirthNpcPreparedPhysicalHold.IsPhysicallyHeld(npc,person?.Presence?.EmbodimentGeneration??0)||!evidence.HasNativeHeader||!evidence.HasNativeGeometry||!evidence.HasNativeHand||!evidence.HasNativeBodyPolicy||!evidence.BodyPolicy.HasDynamics||!evidence.Matches(person)||
            state.StableId!=person.Identity.StableNpcId||state.ProfileId!=person.Profile.ProfileId||
            EntityClass.list[npc.entityClass].UseAIPackages||npc.Stats?.GetType()!=typeof(EntityStats)||
            !RebirthNpcPreparedPersonAdmission.TryValidate(person,out var profile,out reason)||profile!=state.ProfileId)return false;
        if(person.Work!=null||person.Mission!=null||person.Contract!=null){reason="NPC work/mission/contract domain requires an explicit reconstruction handler.";return false;}
        if(!RebirthNpcNativeHeaderProjection.TryPreflight(npc,evidence.Header,out var belongsId,out var spawnId))
        {reason="Original native header references/enums not currently resolvable.";return false;}
        if(!RebirthNpcNativeGeometryProjection.TryPreflight(npc,evidence.Geometry))
        {reason="Original saved native geometry binding is not ready.";return false;}
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        RebirthNpcInventoryTransactionService.TryGetExistingNativeCustody(state.StableId,out var custody,out var inventoryRevision);
        if(!evidence.TryValidateCustody(custody,inventoryRevision)||
            !evidence.TryValidateControllers(person.ControllerFragments,RebirthNpcReconstructionControllerRegistry.Supports)||
            !RebirthNpcReconstructionControllerRegistry.TryPreflight(person.ControllerFragments)||!Preflight(evidence))
        {reason="NPC native/custody/controller preflight failed.";return false;}
        if(!RebirthNpcAggregatePersistenceStore.TryGet(state.StableId,out var original)||
            original.AggregateRevision!=person.AggregateRevision||original.AggregateChecksum!=person.AggregateChecksum)
        {reason="Original saved person changed before restoration.";return false;}
        try
        {
            npc.hasAI=false;
            XElement image=evidence.Write();
            Read(image,"actor",r=>npc.Read((byte)EntityCreationData.FileVersion,r,StreamModeRead.Persistency));
            Read(image,"bodyDamage",r=>npc.bodyDamage=BodyDamage.Read(r,EntityCreationData.FileVersion));
            Read(image,"toolbelt",r=>RestoreToolbelt(npc,r));
            Read(image,"equipment",r=>RestoreEquipment(npc,r));
            Read(image,"bag",r=>npc.bag.ReadInto(r,StreamModeRead.Persistency));
            Read(image,"stats",r=>npc.Stats.Read(r));
            npc.Buffs.CVars.Clear();
            Read(image,"buffs",r=>npc.Buffs.Read(r));
            RebirthNpcNativeHeaderProjection.Hydrate(npc,evidence.Header,belongsId,spawnId);
            if(!RebirthNpcNativeGeometryProjection.TryRestore(npc,evidence.Geometry))
                throw new InvalidDataException("Original absolute geometry restoration/readback failed.");
            if(!RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person))
                throw new InvalidDataException("Original hand/model materialization is not qualified or ready.");
            if(!RebirthNpcPreparedPhysicalHold.TryRestoreDesiredBodyPolicy(npc,person,evidence.BodyPolicy))
                throw new InvalidDataException("Original desired native body policy could not be bound.");
            if(!RebirthNpcReconstructionControllerRegistry.TryRestore(npc,person.ControllerFragments))
                throw new InvalidDataException("Required NPC controller restoration failed.");
            if(person.Ownership!=null)
            {
                if(!Enum.TryParse(person.Ownership.OwnershipState,false,out RebirthNpcOwnershipKind kind)||
                    !RebirthNpcTransactions.SetOwner(state,npc.RebirthProfile,kind,person.Ownership.OwnerPlatformIdOrPersistentPlayerId).Succeeded)
                    throw new InvalidDataException("NPC ownership restoration failed.");
            }
            if(person.Order!=null)
            {
                if(!Enum.TryParse(person.Order.OrderState,false,out RebirthNpcOrderState order)||
                    !RebirthNpcTransactions.SetOrder(state,npc.RebirthProfile,order,person.Transform.AnchorPosition??Vector3.zero,person.Transform.AnchorPosition.HasValue).Succeeded)
                    throw new InvalidDataException("NPC order restoration failed.");
            }
            if(!RebirthNpcTransactions.SetPresence(state,RebirthNpcPresenceState.Suspended).Succeeded)
                throw new InvalidDataException("NPC staged presence failed.");
            RebirthNpcInventoryTransactionService.TryGetExistingNativeCustody(state.StableId,out var current,out var revision);
            if(!ReferenceEquals(world,GameManager.Instance?.World)||world.GetEntity(npc.entityId)!=null||
                !ReferenceEquals(current,custody)||revision!=inventoryRevision||npc.IsDead()||npc.Health!=person.Vitals.CurrentHealth||
                !RebirthNpcNativeReconstructionCapture.TryCapture(npc,person,custody,inventoryRevision,RebirthNpcReconstructionControllerRegistry.Supports,out var captured,out _)||
                !XNode.DeepEquals(captured.Write(),evidence.Write())||
                !RebirthNpcAggregatePersistenceStore.TryGet(state.StableId,out original)||original.AggregateRevision!=person.AggregateRevision||
                original.AggregateChecksum!=person.AggregateChecksum)
                throw new InvalidDataException("NPC restoration did not reproduce original native state.");
            npc.SetPreparedRestorationVerified(person.AggregateRevision,person.AggregateChecksum,person.Presence.EmbodimentGeneration,inventoryRevision);
            reason="Original NPC native state reconstructed and verified offworld.";return true;
        }
        catch(Exception ex) when(ex is IOException||ex is InvalidDataException||ex is ArgumentException||ex is InvalidOperationException||ex is System.Collections.Generic.KeyNotFoundException)
        {npc.hasAI=false;reason="NPC remains held after restoration failure:"+ex.GetType().Name;return false;}
    }
    // Inventory.ReadInto would bind the hand and start a GameManager coroutine. Hydrate only
    // the validated grid and selected index here; hand materialization belongs to admission.
    private static void RestoreToolbelt(EntityRebirthHumanoidNPC npc,PooledBinaryReader reader)
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
    private static void RestoreEquipment(EntityRebirthHumanoidNPC npc,PooledBinaryReader reader)
    {
        var owner=npc.equipment.m_entity;
        try{npc.equipment.m_entity=null;npc.equipment.ReadInto(reader,StreamModeRead.Persistency);}
        finally{npc.equipment.m_entity=owner;}
        for(int index=0;index<npc.equipment.ItemGrid.Length;index++)npc.equipment.BuildSlot(index);
        npc.equipment.ResetArmorGroups();
    }
    private static bool Preflight(RebirthNpcNativeReconstruction evidence)
    {
        try{
        using(var stream=File.OpenRead(typeof(Entity).Assembly.Location))using(var hash=System.Security.Cryptography.SHA256.Create())
            if(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=evidence.NativeAssemblyHash)return false;
        return evidence.Hand!=null&&RebirthNpcNativeReconstructionPreflight.TryValidateItemPayload(evidence.Hand.BarePayload,Block.ItemsStartHere,
            id=>{var item=ItemClass.GetForId(id);return item==null?(bool?)null:item is ItemClassModifier;})&&
            RebirthNpcNativeReconstructionPreflight.TryValidate(evidence,Block.ItemsStartHere,(byte)EnumSpawnerSource.Biome,
            source=>Enum.IsDefined(typeof(EnumSpawnerSource),(EnumSpawnerSource)source)&&(EnumSpawnerSource)source!=EnumSpawnerSource.Delete,
            id=>{var item=ItemClass.GetForId(id);return item==null?(bool?)null:item is ItemClassModifier;},
            id=>Equipment.CosmeticMappingIDString.TryGetValue(id,out var name)&&ItemClass.GetItemClass(name)!=null);
        }catch(Exception ex) when(ex is IOException||ex is InvalidDataException||ex is ArgumentException||ex is System.Security.SecurityException){return false;}
    }
    private static void Read(XElement image,string name,Action<PooledBinaryReader> read)
    {
        byte[] bytes=Convert.FromBase64String((string)image.Element(name).Attribute("payload"));
        using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(_bReset:false))
        {reader.SetBaseStream(stream);read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException("NPC native restore did not consume exact payload.");}
    }
}
