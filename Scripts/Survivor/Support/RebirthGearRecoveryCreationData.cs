using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// Detached native creation data only. No factory, world spawn, save or source debit.
internal static class RebirthGearRecoveryCreationData
{
    internal static bool TryBuild(RebirthGearTransferState checkpoint,Guid publicationId,string ownerKey,int ownerEntityId,int recoveryClassId,out EntityCreationData creation)
    {
        creation=null;
        if(!RebirthGearRecoveryEntityCapture.IsInstalled||checkpoint==null||ownerEntityId<=0||!checkpoint.TryGetPlan(out var plan)||
            !checkpoint.TryGetRecoveryManifest(out var manifest)||!checkpoint.TryGetRecoveryAttempt(publicationId,out var attempt))return false;
        if(attempt.OriginalOwnerEntityId!=ownerEntityId)return false;
        var publication=manifest.ToXml().Elements().SingleOrDefault(e=>(string)e.Attribute("id")==publicationId.ToString("N"));
        if(publication==null)return false;
        bool backpack=(string)publication.Attribute("kind")=="backpack";
        var entityClass=EntityClass.GetEntityClass(recoveryClassId);
        if(entityClass==null||entityClass.classname!=(backpack?typeof(EntityRebirthGearRecoveryBackpack):typeof(EntityRebirthGearRecoveryItem)))return false;
        try
        {
            var indices=publication.Elements().Select(e=>int.Parse((string)e.Attribute("index"),CultureInfo.InvariantCulture)).ToArray();
            if(!RebirthGearRecoveryIdentity.TryCreate(ownerKey,checkpoint.CreationId,Guid.Parse(checkpoint.TransactionId),publicationId,indices[0],out var identity))return false;
            int slots=1;Vector2i size=new Vector2i(1,1);
            if(backpack)
            {
                var loot=LootContainer.GetLootContainer("cntDropBag");if(loot==null||loot.size.x<=0||loot.size.y<=0)return false;
                size=loot.size;slots=Math.Max(checked(size.x*size.y),indices.Length);
                if(slots>4096)return false;
            }
            var stacks=ItemStack.CreateArray(slots);
            for(int i=0;i<indices.Length;i++)
            {
                var stack=plan.Recovery[indices[i]].Item;
                if(!RebirthNativeItemCodec.TryDecode(stack.ItemData,out var value)||stack.Count<=0)return false;
                stacks[i]=new ItemStack(value.Clone(),stack.Count);
            }
            var bag=new Bag(size,XUiC_ItemStack.StackLocationTypes.LootContainer,null);bag.SetSlots(stacks);
            var blob=StreamUtils.ToBlob(writer=>bag.Write(writer,StreamModeWrite.Persistency));
            if(blob==null||blob.Length==0||blob.Length>4*1024*1024)return false;
            var entityData=StreamUtils.ToBlob(writer=>
            {
                // Installed entity record38 base fields for a non-Biome source.
                ((BinaryWriter)writer).Write((byte)EnumSpawnerSource.StaticSpawner);((BinaryWriter)writer).Write(attempt.WorldTime);
                if(backpack){((BinaryWriter)writer).Write((byte)1);((BinaryWriter)writer).Write(ownerEntityId);((BinaryWriter)writer).Write(false);((BinaryWriter)writer).Write("");((BinaryWriter)writer).Write("");}
                else
                {
                    ((BinaryWriter)writer).Write((byte)1);((BinaryWriter)writer).Write(ownerEntityId);
                    ((BinaryWriter)writer).Write(0);((BinaryWriter)writer).Write(ownerEntityId);
                }
                RebirthGearRecoveryEntityWire.Write(writer,identity);
            });
            if(entityData==null||entityData.Length==0||entityData.Length>ushort.MaxValue)return false;
            creation=new EntityCreationData{entityClass=recoveryClassId,id=attempt.EntityId,pos=new Vector3(attempt.X,attempt.Y,attempt.Z),rot=new Vector3(0,attempt.Yaw,0),
                lifetime=backpack?float.MaxValue:attempt.LifetimeSeconds,belongsPlayerId=ownerEntityId,spawnById=backpack?ownerEntityId:-1,spawnerSource=EnumSpawnerSource.StaticSpawner,
                readFileVersion=38,readStreamMode=StreamModeRead.Persistency,bagData=blob,entityData=entityData};
            return true;
        }
        catch{creation=null;return false;}
    }
}