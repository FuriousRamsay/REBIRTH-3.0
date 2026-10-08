using System;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// First publication only. Existing attempts are reconciliation work, never respawn requests.
internal static class RebirthGearRecoveryPublication
{
    internal static bool TryPublishFirst(EntityPlayer player,ClientInfo sender,string creationId,string transactionId,Guid publicationId,int recoveryClassId)
    {
        if(!ThreadManager.IsMainThread())return false;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var game=GameManager.Instance;var world=player?.world;
        if(manager==null||!manager.IsServer||game==null||world==null||world.IsRemote()||!ReferenceEquals(game.World,world)||
            !RebirthRemoteGearInventorySource.TryResolve(player,sender,creationId,out var record)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||identity.StorageKey!=record.StablePlayerKey)return false;
        string key=identity.StorageKey;int ownerEntityId=player.entityId;var pending=record.Support.PendingGearTransfer;
        if(pending==null||pending.TransactionId!=transactionId||record.Support.GearTransferPhase!=RebirthGearTransferPhase.GearCommitted||
            !pending.TryGetRecoveryManifest(out var manifest)||pending.TryGetRecoveryAttempt(publicationId,out _))return false;
        var publication=manifest.ToXml().Elements().SingleOrDefault(e=>(string)e.Attribute("id")==publicationId.ToString("N"));if(publication==null)return false;
        bool backpack=(string)publication.Attribute("kind")=="backpack";
        var definition=EntityClass.GetEntityClass(recoveryClassId);
        if(definition?.classname!=(backpack?typeof(EntityRebirthGearRecoveryBackpack):typeof(EntityRebirthGearRecoveryItem)))return false;
        Func<RebirthGearTransferState,bool> current=expected=>player.entityId==ownerEntityId&&ReferenceEquals(world.GetEntity(ownerEntityId),player)&&ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.IsServer&&ReferenceEquals(game,GameManager.Instance)&&ReferenceEquals(game.World,world)&&ReferenceEquals(player.world,world)&&
            RebirthRemoteGearInventorySource.TryResolve(player,sender,creationId,out var live)&&ReferenceEquals(live,record)&&RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&
            RebirthWorldCharacterService.TryGetIdentity(player,out var liveIdentity)&&liveIdentity!=null&&liveIdentity.StorageKey==key&&record.StablePlayerKey==key&&ReferenceEquals(record.Support.PendingGearTransfer,expected)&&
            RebirthGearTransferSavedWitness.Matches(record.Support,creationId,expected,RebirthGearTransferPhase.GearCommitted);
        if(!current(pending)||!RebirthWorldCharacterRepository.HasSavedGearTransfer(identity,pending,RebirthGearTransferPhase.GearCommitted)||!current(pending))return false;
        int entityId=EntityFactory.nextEntityID;
        if(entityId<=0||entityId==int.MaxValue||world.GetEntity(entityId)!=null)return false;
        EntityFactory.nextEntityID=entityId+1; // Reserve once on the native main thread; gaps are harmless.
        var position=player.GetPosition()+Vector3.up;
        if(!RebirthGearRecoveryAttempt.TryCreate(publicationId,entityId,position.x,position.y,position.z,0,backpack?3600:1800,world.GetWorldTime(),ownerEntityId,out var attempt)||
            !pending.TryAppendRecoveryAttempt(attempt,out var staged)||!RebirthGearRecoveryCreationData.TryBuild(staged,publicationId,key,ownerEntityId,recoveryClassId,out var data)||!current(pending))return false;
        if(!RebirthRemoteGearAppliedConfirmation.TryRecordRecoveryAttempt(player,sender,creationId,transactionId,attempt,out var checkpoint)||
            !XNode.DeepEquals(checkpoint.ToXml(),staged.ToXml())||!current(checkpoint))return false;
        Entity entity=null;bool spawnStarted=false;
        try
        {
            var indices=publication.Elements().Select(e=>int.Parse((string)e.Attribute("index"),System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            entity=EntityFactory.CreateEntity(data);
            var marker=(entity as EntityRebirthGearRecoveryBackpack)?.RecoveryIdentity??(entity as EntityRebirthGearRecoveryItem)?.RecoveryIdentity;
            if(entity==null||entity.entityId!=entityId||marker==null||marker.PublicationId!=publicationId||marker.TransactionId.ToString("N")!=transactionId||marker.OwnerKey!=key||marker.CreationId!=pending.CreationId||marker.EntryIndex!=indices[0]||!current(checkpoint))return false;
            pending.TryGetPlan(out var plan);
            var items=backpack?entity.bag?.ItemGrid?.items:new[]{(entity as EntityItem)?.itemStack};
            if(items==null||items.Length<indices.Length)return false;
            for(int i=0;i<indices.Length;i++){var expected=plan.Recovery[indices[i]].Item;var item=items[i];if(item==null||item.count!=expected.Count||RebirthNativeItemCodec.Encode(item.itemValue)!=expected.ItemData)return false;}
            for(int i=indices.Length;i<items.Length;i++)if(items[i]!=null&&!items[i].IsEmpty())return false;
            if(!current(checkpoint)||world.GetEntity(entityId)!=null)return false;
            spawnStarted=true;world.SpawnEntityInWorld(entity);
            if(!current(checkpoint)||!ReferenceEquals(world.GetEntity(entityId),entity))return false;
            RebirthGearRecoverySnapshotRequest.TryQueueOriginal(player,sender,creationId,transactionId,publicationId);
            return current(checkpoint)&&ReferenceEquals(world.GetEntity(entityId),entity);
        }
        catch{return false;} // Attempt remains saved on all uncertain native effects.
        finally{if(entity!=null&&!spawnStarted&&entity.RootTransform!=null)UnityEngine.Object.Destroy(entity.RootTransform.gameObject);}
        // Success is a live first spawn only, never durable recovery completion.
    }
}