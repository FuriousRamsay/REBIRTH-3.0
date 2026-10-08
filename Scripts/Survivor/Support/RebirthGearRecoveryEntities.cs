using System;
using System.IO;
using UnityEngine.Scripting;

// New types only: existing saved recovery containers keep their native layout.
[Preserve]
public class EntityRebirthGearRecoveryBackpack:EntityLootContainer
{
    public RebirthGearRecoveryIdentity RecoveryIdentity{get;private set;}
    public bool TryBindRecoveryIdentity(RebirthGearRecoveryIdentity identity){if(identity==null||RecoveryIdentity!=null)return false;RecoveryIdentity=identity;return true;}
    public override void Write(PooledBinaryWriter writer,StreamModeWrite mode)
    {
        if(RecoveryIdentity==null||OverrideLootList==null||OverrideName==null||OverrideLootList.Length>4096||OverrideName.Length>4096)throw new InvalidOperationException("Invalid recovery container metadata.");
        RebirthGearRecoveryStringWire.Validate(OverrideLootList);RebirthGearRecoveryStringWire.Validate(OverrideName);
        // EntityCreationData already carries the full bagData blob. Avoid duplicating
        // that bag inside its ushort-length entityData field.
        RebirthGearRecoveryEntityWire.WriteBase(writer,this);
        ((BinaryWriter)writer).Write((byte)1);((BinaryWriter)writer).Write(spawnById);((BinaryWriter)writer).Write(spawnByAllowShare);
        ((BinaryWriter)writer).Write(OverrideLootList);((BinaryWriter)writer).Write(OverrideName);
        RebirthGearRecoveryEntityWire.Write(writer,RecoveryIdentity);
    }
    public override void Read(byte version,PooledBinaryReader reader,StreamModeRead mode)
    {
        if(version!=38)throw new InvalidDataException("Unsupported compact recovery entity version.");
        RebirthGearRecoveryEntityWire.ReadBase(reader,this);
        var metadata=RebirthRecoveryEntityMetadata.ReadBody(reader,true);
        spawnById=metadata.SpawnById;spawnByAllowShare=metadata.AllowShare;
        OverrideLootList=metadata.LootList;OverrideName=metadata.Name;RecoveryIdentity=metadata.Identity;
        // Native EntityCreationData.ApplyToEntity has restored bagData before this call.
    }
}
[Preserve]
public class EntityRebirthGearRecoveryItem:EntityItem
{
    private bool hasRestoredOwnership;
    private int restoredBelongsPlayerId,restoredClientEntityId,restoredOwnerId;
    public RebirthGearRecoveryIdentity RecoveryIdentity{get;private set;}
    public bool TryBindRecoveryIdentity(RebirthGearRecoveryIdentity identity){if(identity==null||RecoveryIdentity!=null)return false;RecoveryIdentity=identity;return true;}
    public override void Write(PooledBinaryWriter writer,StreamModeWrite mode)
    {
        if(RecoveryIdentity==null||itemStack==null||itemStack.IsEmpty())throw new InvalidOperationException("Missing recovery item custody.");
        base.Write(writer,mode);
        ((BinaryWriter)writer).Write((byte)1);
        ((BinaryWriter)writer).Write(belongsPlayerId);((BinaryWriter)writer).Write(clientEntityId);((BinaryWriter)writer).Write(OwnerId);
        RebirthGearRecoveryEntityWire.Write(writer,RecoveryIdentity);
        // Exact stack is supplied by the scoped creation-data capture postfix.
    }
    public override void Read(byte version,PooledBinaryReader reader,StreamModeRead mode)
    {
        if(version!=38)throw new InvalidDataException("Unsupported recovery item version.");
        base.Read(version,reader,mode);
        var metadata=RebirthRecoveryEntityMetadata.ReadBody(reader,false);
        var items=bag?.ItemGrid?.items;
        if(items==null||items.Length!=1||items[0]==null||items[0].IsEmpty())throw new InvalidDataException("Missing recovery item bag payload.");
        SetItemStack(items[0].Clone());RecoveryIdentity=metadata.Identity;
        restoredBelongsPlayerId=metadata.BelongsPlayerId;restoredClientEntityId=metadata.ClientEntityId;restoredOwnerId=metadata.OwnerId;hasRestoredOwnership=true;
    }
    public override void PostInit()
    {
        // Factory overwrites belongsPlayerId after Read. Restore before world-data creation.
        if(hasRestoredOwnership){belongsPlayerId=restoredBelongsPlayerId;clientEntityId=restoredClientEntityId;OwnerId=restoredOwnerId;}
        base.PostInit();
    }
}
internal static class RebirthGearRecoveryEntityWire
{
    // Exact installed Entity.Write/Read fields for entity record version38.
    internal static void WriteBase(PooledBinaryWriter writer,Entity entity)
    {
        ((BinaryWriter)writer).Write((byte)entity.spawnerSource);
        if(entity.spawnerSource==EnumSpawnerSource.Biome){((BinaryWriter)writer).Write(entity.spawnerSourceBiomeIdHash);((BinaryWriter)writer).Write(entity.spawnerSourceChunkKey);}
        ((BinaryWriter)writer).Write(entity.WorldTimeBorn);
    }
    internal static void ReadBase(PooledBinaryReader reader,Entity entity)
    {
        entity.spawnerSource=(EnumSpawnerSource)reader.ReadByte();
        if(entity.spawnerSource==EnumSpawnerSource.Biome){entity.spawnerSourceBiomeIdHash=reader.ReadInt32();entity.spawnerSourceChunkKey=reader.ReadInt64();}
        entity.WorldTimeBorn=reader.ReadUInt64();
    }
    internal static void Write(PooledBinaryWriter writer,RebirthGearRecoveryIdentity identity){if(identity==null)throw new InvalidOperationException("Missing recovery publication identity.");var bytes=identity.Encode();((BinaryWriter)writer).Write((ushort)bytes.Length);((BinaryWriter)writer).Write(bytes);}
    internal static RebirthGearRecoveryIdentity Read(PooledBinaryReader reader){int length=reader.ReadUInt16();if(length<1||length>RebirthGearRecoveryIdentity.MaximumBytes)throw new InvalidDataException("Invalid recovery identity length.");var bytes=reader.ReadBytes(length);if(bytes.Length!=length||!RebirthGearRecoveryIdentity.TryDecode(bytes,out var identity))throw new InvalidDataException("Invalid recovery identity.");return identity;}
}