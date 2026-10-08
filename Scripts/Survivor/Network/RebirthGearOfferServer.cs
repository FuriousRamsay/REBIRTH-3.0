using System;
using System.Xml.Linq;

// Saved Prepared delivery only. A successful return means fragments were queued,
// never owner inventory application, saved ACK or terminal custody settlement.
public static class RebirthGearOfferServer
{
    // Intermediate advancement only: pending recovery and owner hold remain intact.
    public static bool TryAdvanceApplied(EntityPlayer player,ClientInfo sender,string creation,Guid transaction)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(transaction==Guid.Empty||manager==null||!manager.IsServer||
            !RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var record))return false;
        var pending=record.Support.PendingGearTransfer;
        string transfer=transaction.ToString("N");
        if(pending==null||pending.TransactionId!=transfer||!RebirthSurvivorRequestScope.Matches(creation,pending.CreationId))return false;
        try
        {
            if(record.Support.GearTransferPhase!=RebirthGearTransferPhase.GearCommitted&&
                !RebirthRemoteGearAppliedConfirmation.TryConfirm(player,sender,creation,transfer))return false;
            if(!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!manager.IsServer||
                !RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var current)||
                !ReferenceEquals(current,record)||!ReferenceEquals(record.Support.PendingGearTransfer,pending))return false;
            return RebirthRemoteGearAppliedConfirmation.TryCommit(player,sender,creation,transfer);
        }
        catch{return false;}
    }
    // One original recovery publication per request; only saved terminal completion
    // may return true. A first spawn or uncertain disk observation retains custody.
    public static bool TryAdvanceRecovery(EntityPlayer player,ClientInfo sender,string creation,Guid transaction)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var game=GameManager.Instance;var world=game?.World;
        if(transaction==Guid.Empty||manager==null||!manager.IsServer||world==null||world.IsRemote()||
            !RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var record))return false;
        string transfer=transaction.ToString("N");
        Func<bool> current=()=>ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.IsServer&&
            ReferenceEquals(game,GameManager.Instance)&&ReferenceEquals(game.World,world)&&!world.IsRemote()&&
            RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var live)&&ReferenceEquals(live,record)&&
            RebirthWorldCharacterRepository.IsCurrentCachedRecord(record);
        try
        {
            if(!current())return false;
            // Empty recovery and already-saved complete receipts need no new effect.
            if(RebirthRemoteGearAppliedConfirmation.TryFinishRecovery(player,sender,creation,transfer))return current();
            if(!current())return false;
            var pending=record.Support.PendingGearTransfer;
            if(pending==null||pending.TransactionId!=transfer||record.Support.GearTransferPhase!=RebirthGearTransferPhase.GearCommitted||
                !RebirthSurvivorRequestScope.Matches(creation,pending.CreationId)||pending.HasAllRecoveryPublicationReceipts)return false;
            if(!RebirthGearRecoveryDispatcher.TryAdvance(player,sender,creation,transfer)||!current())return false;
            return RebirthRemoteGearAppliedConfirmation.TryFinishRecovery(player,sender,creation,transfer)&&current();
        }
        catch{return false;}
    }
    public static bool TrySendPrepared(EntityPlayer player,ClientInfo sender,string creation)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(manager==null||!manager.IsServer||!RebirthRemoteGearPreparation.TryReplay(player,sender,creation,out var offer))return false;
        return TrySendPreparedCore(player,sender,creation,manager,offer,RebirthGearTransferPhase.Prepared);
    }
    public static bool TrySendPreparedBound(EntityPlayer player,ClientInfo sender,string originalMarker)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(manager==null||!manager.IsServer||
            !RebirthRemoteGearPreparation.TryReplayBound(player,sender,originalMarker,out var offer))return false;
        return TrySendPreparedCore(player,sender,offer.CreationId,manager,offer,RebirthGearTransferPhase.Prepared);
    }
    public static bool TrySendRetainedBound(EntityPlayer player,ClientInfo sender,string originalMarker)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(manager==null||!manager.IsServer||
            !RebirthRemoteGearPreparation.TryReplayBoundRetained(player,sender,originalMarker,out var offer,out var phase))return false;
        return TrySendPreparedCore(player,sender,offer.CreationId,manager,offer,phase);
    }
    // Terminal originals use existing bounded fragment transport, but never the
    // pending Prepared sender or any journal advancement/native item effect.
    public static bool TrySendTerminalOriginal(EntityPlayer player,ClientInfo sender,string marker,RebirthGearSettlement expected)
    {
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(manager==null||!manager.IsServer||expected==null||
                !RebirthRemoteGearAppliedConfirmation.TryGetBoundTerminalOriginal(player,sender,marker,out var original,out var terminal)||
                !XNode.DeepEquals(terminal.Write(),expected.Write())||!RebirthGearOfferWireCodec.TryEncode(original,out var bytes))return false;
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearOfferFragment));
            string digest=RebirthGearOfferAssembly.ComputeDigest(bytes);
            int count=(bytes.Length+RebirthGearOfferAssembly.ChunkBytes-1)/RebirthGearOfferAssembly.ChunkBytes;
            for(int index=0;index<count;index++)
            {
                if(!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!manager.IsServer||
                    !RebirthRemoteGearAppliedConfirmation.TryGetBoundTerminalOriginal(player,sender,marker,out var current,out var outcome)||
                    !XNode.DeepEquals(current.ToXml(),original.ToXml())||!XNode.DeepEquals(outcome.Write(),terminal.Write()))return false;
                int offset=index*RebirthGearOfferAssembly.ChunkBytes;
                var chunk=new byte[Math.Min(RebirthGearOfferAssembly.ChunkBytes,bytes.Length-offset)];Buffer.BlockCopy(bytes,offset,chunk,0,chunk.Length);
                if(!RebirthGearOfferFragment.TryCreate(original.CreationId,Guid.ParseExact(original.TransactionId,"N"),original.ExpectedRevision,
                    bytes.Length,digest,index,chunk,out var fragment))return false;
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthGearOfferFragment>().Setup(player.entityId,fragment),_attachedToEntityId:player.entityId);
            }
            return true;
        }
        catch{return false;}
    }
    private static bool TrySendPreparedCore(EntityPlayer player,ClientInfo sender,string creation,
        ConnectionManager manager,RebirthGearTransferState offer,RebirthGearTransferPhase expectedPhase)
    {
        if(!RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var record))return false;
        var retained=record.Support.PendingGearTransfer;
        if(retained==null||!XNode.DeepEquals(retained.ToXml(),offer.ToXml())||
            !RebirthGearOfferWireCodec.TryEncode(offer,out var bytes))return false;
        try
        {
            // Resolve mapping before queuing any fragments.
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearOfferFragment));
            string digest=RebirthGearOfferAssembly.ComputeDigest(bytes);
            int count=(bytes.Length+RebirthGearOfferAssembly.ChunkBytes-1)/RebirthGearOfferAssembly.ChunkBytes;
            for(int index=0;index<count;index++)
            {
                if(!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!manager.IsServer||
                    !RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var current)||!ReferenceEquals(current,record)||
                    !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||
                    !ReferenceEquals(record.Support.PendingGearTransfer,retained)||record.Support.GearTransferPhase!=expectedPhase)return false;
                int offset=index*RebirthGearOfferAssembly.ChunkBytes;
                var chunk=new byte[Math.Min(RebirthGearOfferAssembly.ChunkBytes,bytes.Length-offset)];Buffer.BlockCopy(bytes,offset,chunk,0,chunk.Length);
                if(!RebirthGearOfferFragment.TryCreate(offer.CreationId,Guid.ParseExact(offer.TransactionId,"N"),offer.ExpectedRevision,
                    bytes.Length,digest,index,chunk,out var fragment))return false;
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthGearOfferFragment>().Setup(player.entityId,fragment),_attachedToEntityId:player.entityId);
            }
            return true;
        }
        catch{return false;} // Retained saved intent is untouched on uncertain delivery.
    }
}