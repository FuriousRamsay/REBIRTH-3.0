using System;
using System.Linq;

// One publication per authenticated request; existing attempts never respawn.
internal static class RebirthGearRecoveryDispatcher
{
    internal static bool TryAdvance(EntityPlayer player,ClientInfo sender,string creation,string transaction)
    {
        if(!ThreadManager.IsMainThread()||!RebirthGearRecoveryEntityCapture.IsInstalled||
            !RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var record))return false;
        var pending=record.Support.PendingGearTransfer;
        if(pending==null||pending.TransactionId!=transaction||record.Support.GearTransferPhase!=RebirthGearTransferPhase.GearCommitted||!pending.TryGetRecoveryManifest(out var manifest))return false;
        int pack=EntityClass.FromString("rebirthGearRecoveryBackpackV1"),item=EntityClass.FromString("rebirthGearRecoveryItemV1");
        if(EntityClass.GetEntityClass(pack)?.classname!=typeof(EntityRebirthGearRecoveryBackpack)||EntityClass.GetEntityClass(item)?.classname!=typeof(EntityRebirthGearRecoveryItem))return false;
        foreach(var publication in manifest.ToXml().Elements())
        {
            Guid id=Guid.ParseExact((string)publication.Attribute("id"),"N");
            if(pending.HasRecoveryPublicationReceipt(id))continue;
            if(!pending.TryGetRecoveryAttempt(id,out var attempt))
                return RebirthGearRecoveryPublication.TryPublishFirst(player,sender,creation,transaction,id,(string)publication.Attribute("kind")=="backpack"?pack:item);
            // Placement is saved before first publication. No cache loading/repair or broad disk scans.
            double x=Math.Floor((double)attempt.X/16),z=Math.Floor((double)attempt.Z/16);
            if(x<int.MinValue||x>int.MaxValue||z<int.MinValue||z>int.MaxValue)return false;
            // Native Chunk header Y is zero; entity.chunkPosAddedEntityTo.Y is a
            // 16-block vertical entity-list index, not the serialized chunk Y.
            var entity=player.world?.GetEntity(attempt.EntityId);
            var marker=(entity as EntityRebirthGearRecoveryBackpack)?.RecoveryIdentity??(entity as EntityRebirthGearRecoveryItem)?.RecoveryIdentity;
            int firstEntry=int.Parse((string)publication.Elements().First().Attribute("index"),System.Globalization.CultureInfo.InvariantCulture);
            if(entity!=null&&entity.addedToChunk&&marker!=null&&marker.OwnerKey==record.StablePlayerKey&&marker.CreationId==pending.CreationId&&marker.TransactionId.ToString("N")==transaction&&marker.PublicationId==id&&marker.EntryIndex==firstEntry)
            {
                RebirthGearRecoverySnapshotRequest.TryQueueOriginal(player,sender,creation,transaction,id);
                if(!ReferenceEquals(record.Support.PendingGearTransfer,pending))return false;
                var location=entity.chunkPosAddedEntityTo;
                if((location.x!=(int)x||location.z!=(int)z)&&RebirthRecoveryWorldDiskEvidence.TryRecordReceipt(player,sender,creation,transaction,id,location.x,0,location.z,pack,item))return true;
                if(!ReferenceEquals(record.Support.PendingGearTransfer,pending))return false;
            }
            return RebirthRecoveryWorldDiskEvidence.TryRecordReceipt(player,sender,creation,transaction,id,(int)x,0,(int)z,pack,item);
        }
        return pending.HasAllRecoveryPublicationReceipts;
    }
}