using System;
using System.Globalization;
using System.Linq;

// Captured-record evidence only. Caller must bind validated custom class IDs and
// original owner entity ID to the saved world; this does not authorize settlement.
public static class RebirthRecoveryPublicationEvidence
{
    public static bool TryMatch(byte[] payload,int offset,RebirthGearTransferState pending,Guid publicationId,string ownerKey,int originalOwnerEntityId,int backpackClassId,int itemClassId,byte staticSource,out int endOffset)
    {
        endOffset=0;
        if(pending==null||string.IsNullOrWhiteSpace(ownerKey)||originalOwnerEntityId<=0||backpackClassId==itemClassId||
            !pending.TryGetRecoveryManifest(out var manifest)||!pending.TryGetRecoveryAttempt(publicationId,out var attempt))return false;
        if(attempt.OriginalOwnerEntityId!=originalOwnerEntityId)return false;
        var publication=manifest.ToXml().Elements().SingleOrDefault(e=>(string)e.Attribute("id")==publicationId.ToString("N"));
        if(publication==null)return false;
        bool backpack=(string)publication.Attribute("kind")=="backpack";
        if(!RebirthRecoveryEntityRecord.TryRead(payload,offset,backpack?backpackClassId:itemClassId,out var record)||record.EntityId!=attempt.EntityId||
            (backpack?record.Lifetime!=float.MaxValue:record.Lifetime>attempt.LifetimeSeconds)||
            !RebirthRecoveryEntityMetadata.TryRead(payload,record,staticSource,backpack,out var metadata))return false;
        var identity=metadata.Identity;
        int first=int.Parse((string)publication.Elements().First().Attribute("index"),CultureInfo.InvariantCulture);
        if(metadata.WorldTimeBorn!=attempt.WorldTime||identity.OwnerKey!=ownerKey||identity.CreationId!=pending.CreationId||
            identity.TransactionId.ToString("N")!=pending.TransactionId||identity.PublicationId!=publicationId||identity.EntryIndex!=first)return false;
        if(backpack){if(metadata.SpawnById!=originalOwnerEntityId||metadata.AllowShare||metadata.LootList!=""||metadata.Name!="")return false;}
        else if(metadata.BelongsPlayerId!=originalOwnerEntityId||metadata.OwnerId!=originalOwnerEntityId||metadata.ClientEntityId!=0)return false;
        if(!RebirthRecoveryBagPayload.Matches(payload,record,pending,publicationId))return false;
        endOffset=record.EndOffset;return true;
    }
}