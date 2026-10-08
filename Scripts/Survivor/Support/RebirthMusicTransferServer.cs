using System;

public static class RebirthMusicTransferServer
{
    public static bool Prepare(EntityPlayer player, ClientInfo sender, int operation, int index,
        int itemType, ushort seed, long revision, string creationId, out RebirthMusicTransferState offer)
    {
        offer=null;
        if(operation==1)return false; // Insertion requires an exact native source proof.
        return PrepareCore(player,sender,operation,index,itemType,seed,revision,creationId,false,false,null,out offer);
    }

    public static bool PrepareMusicSource(EntityPlayer player,ClientInfo sender,int operation,int index,
        int itemType,ushort seed,long revision,string creationId,string sourceProof,out RebirthMusicTransferState offer)
    {
        offer=null;
        if(operation==1&&!RebirthMusicSourceProof.IsValid(sourceProof))return false;
        return PrepareCore(player,sender,operation,index,itemType,seed,revision,creationId,false,false,sourceProof,out offer);
    }
    public static bool PrepareAudiobook(EntityPlayer player,ClientInfo sender,int operation,int index,
        int itemType,ushort seed,long revision,string creationId,out RebirthMusicTransferState offer)
    {
        return PrepareCore(player,sender,operation,index,itemType,seed,revision,creationId,true,false,null,out offer);
    }

    public static bool PrepareAudiobookSource(EntityPlayer player,ClientInfo sender,int operation,int index,
        int itemType,ushort seed,long revision,string creationId,string sourceProof,out RebirthMusicTransferState offer)
    {
        offer=null;
        if(operation==1&&!RebirthMusicSourceProof.IsValid(sourceProof))return false;
        return PrepareCore(player,sender,operation,index,itemType,seed,revision,creationId,true,false,sourceProof,out offer);
    }
    public static bool PrepareLocalMusicSource(EntityPlayerLocal player,int operation,int index,int itemType,
        ushort seed,long revision,string creationId,string sourceProof,out RebirthMusicTransferState offer)
    {
        offer=null;
        if(operation==1&&!RebirthMusicSourceProof.IsValid(sourceProof))return false;
        return PrepareCore(player,null,operation,index,itemType,seed,revision,creationId,false,true,sourceProof,out offer);
    }
    public static bool PrepareLocalAudiobook(EntityPlayerLocal player,int operation,int index,int itemType,ushort seed,long revision,string creationId,out RebirthMusicTransferState offer)
    {return PrepareCore(player,null,operation,index,itemType,seed,revision,creationId,true,true,null,out offer);}

    public static bool PrepareLocalAudiobookSource(EntityPlayerLocal player,int operation,int index,
        int itemType,ushort seed,long revision,string creationId,string sourceProof,out RebirthMusicTransferState offer)
    {
        offer=null;
        if(operation==1&&!RebirthMusicSourceProof.IsValid(sourceProof))return false;
        return PrepareCore(player,null,operation,index,itemType,seed,revision,creationId,true,true,sourceProof,out offer);
    }
    private static bool PrepareCore(EntityPlayer player,ClientInfo sender,int operation,int index,
        int itemType,ushort seed,long revision,string creationId,bool audiobook,bool local,string sourceProof,out RebirthMusicTransferState offer)
    {
        offer = null;
        RebirthWorldCharacterRecord record;
        if (local ? !ResolveLocal(player as EntityPlayerLocal,creationId,out record) : !Resolve(player,sender,creationId,out record))return false;
        var state = record.Support;
        if (state.PendingMusicTransfer != null)
        {
            // A new click cannot replace an unresolved operation. Replay the saved
            // offer so its owner receipt determines whether to finish or reject it.
            if (!SameCreation(state.PendingMusicTransfer.CreationId, creationId)) return false;
            offer = state.PendingMusicTransfer.Clone();
            return true;
        }
        if ((operation != 1 && operation != 2) || player.IsDead()
            || RebirthCharacterCreationHoldService.IsHeld(player)
            || !RebirthSurvivorGearService.HasEquippedWalkman(player)
            || revision != (audiobook ? state.AudiobookRevision : state.MusicRevision) || revision == long.MaxValue) return false;
        var pending = new RebirthMusicTransferState {
            TransactionId = Guid.NewGuid().ToString("N"), CreationId = record.Origin.CreationId,
            ExpectedRevision = revision, Operation = operation, LibraryIndex = index, IsAudiobook = audiobook
        };
        if (operation == 1)
        {
            if (audiobook ? state.AudiobookCassettes.Count >= RebirthAudiobookLibraryPersistence.Capacity : state.MusicCassettes.Count >= RebirthMusicLibraryService.Capacity) return false;
            ItemStack source = null;
            for (int area = 0; area < 2 && source == null; area++)
            {
                var slots = local ? (area==0 ? player.bag?.ItemGrid.items : player.inventory?.ItemGrid.items)
                    : RebirthPlayerDataInventory.ReadSlots(sender.latestPlayerData,area==0);
                if (slots == null) continue;
                int limit = area == 0 ? slots.Length : RebirthToolbeltCapacity.GetOwnedSlotCount(player, slots.Length);
                for (int slot = 0; slot < limit; slot++)
                {
                    var stack = slots[slot];
                    if (stack == null || stack.IsEmpty() || stack.itemValue.type != itemType || stack.itemValue.Seed != seed) continue;
                    if(sourceProof!=null&&!RebirthMusicSourceProof.Matches(stack.itemValue,sourceProof))continue;
                    source = stack; pending.SourceIsBag = area == 0; pending.SourceIndex = slot; break;
                }
            }
            if(source == null || source.itemValue?.ItemClass == null)return false;
            if(audiobook)
            {
                RebirthAudiobookDefinition definition;
                if(!RebirthProgressionRuntimeConfig.TryGetAudiobook(source.itemValue.ItemClass.GetItemName(),out definition)||definition==null)return false;
                pending.AudiobookSlotId=Guid.NewGuid().ToString("N");
            }
            else if(!RebirthMusicLibraryService.IsMusicCassette(source.itemValue.ItemClass.GetItemName()))return false;
            pending.ItemId = source.itemValue.ItemClass.GetItemName();
            pending.ItemData = RebirthMusicLibraryService.Encode(source.itemValue);
            pending.LibraryIndex = audiobook ? state.AudiobookCassettes.Count : state.MusicCassettes.Count;
        }
        else
        {
            if(audiobook)
            {
                if(index<0 || index>=state.AudiobookCassettes.Count)return false;
                var cassette=state.AudiobookCassettes[index];
                if(cassette==null)return false;
                pending.ItemId=cassette.ItemId;pending.ItemData=cassette.ItemData;pending.AudiobookSlotId=cassette.SlotId;
            }
            else
            {
                if (index < 0 || index >= state.MusicCassettes.Count) return false;
                var cassette = state.MusicCassettes[index];
                if (cassette == null) return false;
                pending.ItemId = cassette.ItemId; pending.ItemData = cassette.ItemData;
            }
        }
        if (System.Text.Encoding.UTF8.GetByteCount(pending.ToXml().ToString(System.Xml.Linq.SaveOptions.DisableFormatting))
            > NetPackageRebirthMusicTransferOffer.MaxPayloadBytes) return false;
        bool prepared = audiobook
            ? RebirthAudiobookTransferJournal.Prepare(state,pending,() => Save(player,record))
            : RebirthMusicTransferJournal.Prepare(state,pending,() => Save(player,record));
        if(!prepared)return false;
        offer = state.PendingMusicTransfer.Clone();
        return true;
    }

    public static bool Acknowledge(EntityPlayer player, ClientInfo sender, string creationId, string transactionId, bool applied)
    {
        RebirthWorldCharacterRecord record;
        if(!Resolve(player,sender,creationId,out record))return false;
        var pending=record.Support.PendingMusicTransfer;
        if(pending==null||!SameCreation(pending.CreationId,creationId)||pending.TransactionId!=transactionId)return false;
        var game=GameManager.Instance;
        var world=player.world;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        try
        {
            string saveRoot=GameIO.GetPlayerDataDir();
            string nativeOwner=sender.InternalId.CombinedString;
            if(string.IsNullOrEmpty(saveRoot)||string.IsNullOrEmpty(nativeOwner)||
                !HasSavedReceipt(sender,saveRoot,nativeOwner,transactionId,applied))return false;
            RebirthWorldCharacterRecord current;
            if(!ReferenceEquals(game,GameManager.Instance)||!ReferenceEquals(world,player.world)||
                !ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||
                !string.Equals(saveRoot,GameIO.GetPlayerDataDir(),StringComparison.Ordinal)||
                !string.Equals(nativeOwner,sender.InternalId?.CombinedString,StringComparison.Ordinal)||
                !Resolve(player,sender,creationId,out current)||!ReferenceEquals(current,record)||
                !ReferenceEquals(current.Support.PendingMusicTransfer,pending))return false;
        }
        catch(Exception){return false;}
        return Settle(player,record,pending,applied);
    }
    public static bool ApplyLocalPending(EntityPlayerLocal player,string creationId)
    {
        RebirthWorldCharacterRecord record;
        if(!ResolveLocal(player,creationId,out record))return false;
        var pending=record.Support.PendingMusicTransfer;
        if(pending==null||!SameCreation(pending.CreationId,creationId))return false;
        var game=GameManager.Instance;
        var world=player.world;
        string saveRoot,nativeOwner;
        try
        {
            saveRoot=GameIO.GetPlayerDataDir();
            nativeOwner=game.getPersistentPlayerID(null)?.CombinedString;
        }
        catch(Exception){return false;}
        if(string.IsNullOrEmpty(saveRoot)||string.IsNullOrEmpty(nativeOwner))return false;
        var result=RebirthMusicOwnerTransfer.Apply(player,creationId,pending);
        if(result==RebirthMusicOwnerTransferResult.Pending)return false;
        bool applied=result==RebirthMusicOwnerTransferResult.Applied;
        try
        {
            if(!IsCurrentLocalPending(player,creationId,game,world,saveRoot,nativeOwner,record,pending))return false;
            game.SaveLocalPlayerData();
            if(!IsCurrentLocalPending(player,creationId,game,world,saveRoot,nativeOwner,record,pending))return false;
            var saved=new PlayerDataFile();saved.Load(saveRoot,nativeOwner);
            if(!saved.bLoaded||saved.buffData==null||
                !RebirthMusicReceiptReader.Contains(saved.buffData.ToArray(),pending.TransactionId,applied))return false;
            if(!IsCurrentLocalPending(player,creationId,game,world,saveRoot,nativeOwner,record,pending))return false;
        }
        catch(Exception){return false;}
        return Settle(player,record,pending,applied);
    }

    private static bool IsCurrentLocalPending(EntityPlayerLocal player,string creationId,GameManager game,
        World world,string saveRoot,string nativeOwner,RebirthWorldCharacterRecord record,RebirthMusicTransferState pending)
    {
        RebirthWorldCharacterRecord current;
        return ReferenceEquals(game,GameManager.Instance)&&ReferenceEquals(world,player?.world)&&
            ResolveLocal(player,creationId,out current)&&ReferenceEquals(current,record)&&
            ReferenceEquals(current.Support.PendingMusicTransfer,pending)&&
            string.Equals(GameIO.GetPlayerDataDir(),saveRoot,StringComparison.Ordinal)&&
            string.Equals(game.getPersistentPlayerID(null)?.CombinedString,nativeOwner,StringComparison.Ordinal);
    }
    private static bool Settle(EntityPlayer player,RebirthWorldCharacterRecord record,RebirthMusicTransferState pending,bool applied)
    {
        bool settled;
        if(pending.IsAudiobook)
            settled=applied ? RebirthAudiobookTransferJournal.Commit(record.Support,pending.TransactionId,() => Save(player,record))
                : RebirthAudiobookTransferJournal.CancelRejected(record.Support,pending.TransactionId,() => Save(player,record));
        else
            settled=applied ? RebirthMusicTransferJournal.Commit(record.Support,pending.TransactionId,() => Save(player,record))
                : RebirthMusicTransferJournal.CancelRejected(record.Support,pending.TransactionId,() => Save(player,record));
        if(settled&&applied&&pending.IsAudiobook&&pending.Operation==1)
        {
            // Custody is already saved. Playback failure must not replay inventory transfer.
            try { string message;RebirthAudiobookListeningSessionService.TryBeginNextPending(player,out message); }
            catch(Exception error){Log.Warning("[REBIRTH Audiobook] Post-insertion listening deferred: "+error.GetType().Name);}
        }
        return settled;
    }

    private static bool ResolveLocal(EntityPlayerLocal player,string creationId,out RebirthWorldCharacterRecord record)
    {
        record=null;var game=GameManager.Instance;
        return player?.world!=null&&!player.world.IsRemote()&&game!=null&&
            ReferenceEquals(player.world,game.World)&&ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&
            ReferenceEquals(player.world.GetEntity(player.entityId),player)&&RebirthWorldCharacterRepository.IsServerAuthority&&
            RebirthSurvivorMode.IsEnabledForCurrentWorld()&&RebirthWorldCharacterService.TryGet(player,out record)&&
            record?.Support!=null&&record.IsComplete&&SameCreation(record.Origin?.CreationId,creationId);
    }

    private static bool Resolve(EntityPlayer player, ClientInfo sender, string creationId, out RebirthWorldCharacterRecord record)
    {
        record=null;
        var game=GameManager.Instance;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        RebirthStablePlayerIdentity owner,authenticated;
        return ThreadManager.IsMainThread()&&player?.world!=null&&!player.world.IsRemote()&&game!=null&&
            ReferenceEquals(player.world,game.World)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&
            sender?.InternalId!=null&&sender.entityId==player.entityId&&manager!=null&&manager.IsServer&&
            manager.Clients!=null&&ReferenceEquals(manager.Clients.ForEntityId(player.entityId),sender)&&
            RebirthWorldCharacterRepository.IsServerAuthority&&RebirthSurvivorMode.IsEnabledForCurrentWorld()&&
            RebirthWorldCharacterService.TryGetIdentity(player,out owner)&&owner!=null&&
            RebirthStablePlayerIdentity.TryFromClientInfo(sender,out authenticated)&&authenticated!=null&&
            owner.CanonicalId==authenticated.CanonicalId&&owner.StorageKey==authenticated.StorageKey&&
            RebirthWorldCharacterService.TryGet(player,out record)&&record?.Support!=null&&record.IsComplete&&
            record.StablePlayerKey==owner.StorageKey&&RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&
            SameCreation(record.Origin?.CreationId,creationId);
    }
    private static bool SameCreation(string left, string right)
    {
        return RebirthSurvivorRequestScope.Matches(left,right);
    }

    private static bool Save(EntityPlayer player, RebirthWorldCharacterRecord record)
    {
        RebirthStablePlayerIdentity identity;
        if (!RebirthWorldCharacterService.TryGetIdentity(player, out identity)) return false;
        RebirthWorldCharacterService.MarkDirty(record, "music-transfer");
        return RebirthWorldCharacterRepository.SaveIfDirty(identity, "music-transfer");
    }

    private static bool HasSavedReceipt(ClientInfo sender,string saveRoot,string nativeOwner,string transactionId,bool applied)
    {
        if (sender?.InternalId == null) return false;
        // Native Save returns void and swallows disk failures. Its in-memory flag
        // is cleared before final file replacement, so it is not proof of success.
        var saved = new PlayerDataFile();
        saved.Load(saveRoot,nativeOwner);
        if (!saved.bLoaded || saved.buffData == null) return false;
        return RebirthMusicReceiptReader.Contains(saved.buffData.ToArray(), transactionId, applied);

    }
}
