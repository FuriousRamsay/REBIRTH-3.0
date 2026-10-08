using System;

// Receipt delivery only. Owner inventory application is an explicit subsequent handshake.
public static class RebirthBackpackLibraryClientOffers
{
    private static EntityPlayerLocal owner;
    private static World boundWorld;
    private static object session;
    private static string creation;
    private static Guid completeTransaction;
    private static RebirthBackpackLibraryOwnerInbox inbox;
    private static RebirthBackpackLibrarySettlement lastSettlement;
    private static bool Resolve(World world,int playerId,out EntityPlayerLocal player,out object connection,out string current)
    {
        player=null;connection=null;current=null;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(world==null||!world.IsRemote()||GameManager.Instance==null||
            !ReferenceEquals(world,GameManager.Instance.World)||manager==null||manager.IsServer)return false;
        player=world.GetPrimaryPlayer();
        var native=manager.connectionToServer;
        if(player==null||player.entityId!=playerId||native==null||native.Length==0||native[0]==null||native[0].IsDisconnected()||
            !RebirthSurvivorRequestScope.TryNormalize(RebirthSurvivorClientState.GetProjectedCreationId(player),out current))return false;
        connection=native[0];return true;
    }
    // Reconnect or lost-delivery recovery asks for the existing server journal intent.
    // It never creates a new transfer, applies inventory or releases a reservation.
    public static bool RequestRecovery(World world,int playerId)
    {
        if(!Resolve(world,playerId,out var player,out var connection,out var current)||
            !player.IsSpawned()||player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player))return false;
        try
        {
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibraryRecoveryRequest));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibraryOfferChunk));
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
                NetPackageManager.GetPackage<NetPackageRebirthBackpackLibraryRecoveryRequest>().Setup(playerId,current));
            return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Library] Owner recovery request deferred: "+error.GetType().Name);return false;}
    }
    // Explicit UI intent only: success means the request was queued, never that custody moved.
    public static bool RequestPrepare(World world,int playerId,bool bag,int inventorySlot,int librarySlot,int quantity,bool deposit)
    {
        if(!Resolve(world,playerId,out var player,out var connection,out var current)||
            !player.IsSpawned()||player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player)||
            RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||quantity<=0||quantity>ushort.MaxValue||
            !RebirthBackpackLibraryClientViews.TryGet(world,playerId,out var view)||view.TransferPending||
            view.CreationId!=current||librarySlot<0||librarySlot>=view.Capacity)return false;
        var xui=player.PlayerUI?.xui;
        if(player.inventory==null||player.inventory.IsHoldingItemActionRunning()||xui==null||xui.IsUsingItemActionEntryUse||
            xui.DragAndDropWindow?.CurrentStack==null||!xui.DragAndDropWindow.CurrentStack.IsEmpty())return false;
        var slots=bag?player.bag?.ItemGrid.items:player.inventory.ItemGrid.items;
        int owned=slots==null?0:bag?slots.Length:RebirthToolbeltCapacity.GetOwnedSlotCount(player,slots.Length);
        if(inventorySlot<0||inventorySlot>=owned||slots[inventorySlot]==null)return false;
        try
        {
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibraryPrepareRequest));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibraryOfferChunk));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibrarySettleRequest));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibrarySettled));
            var request=NetPackageManager.GetPackage<NetPackageRebirthBackpackLibraryPrepareRequest>()
                .Setup(playerId,current,view.GearRevision,bag,inventorySlot,librarySlot,quantity,deposit);
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(request);
            return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Library] Owner preparation deferred: "+error.GetType().Name);return false;}
    }
    public static bool RequestPrepareCursor(World world,int playerId,long revision,int sectionSlot,int quantity,bool deposit,bool sell)
    {
        if(!Resolve(world,playerId,out var player,out _,out var current)||!player.IsSpawned()||player.IsDead()||
            RebirthCharacterCreationHoldService.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||
            RebirthGearOwnerReservation.IsHeld(player)||quantity<1||quantity>ushort.MaxValue)return false;
        var ui=player.PlayerUI?.xui;
        if(ui?.DragAndDropWindow?.CurrentStack==null||ui.IsUsingItemActionEntryUse||player.inventory==null||
            player.inventory.IsHoldingItemActionRunning())return false;
        try
        {
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackSectionCursorPrepareRequest));
            // Publish the native player frame ahead of intent, on the same ordered connection.
            // Server admission reads only its authenticated native frame; a stale frame is rejected.
            GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
                NetPackageManager.GetPackage<NetPackageRebirthBackpackSectionCursorPrepareRequest>()
                    .Setup(playerId,current,revision,sell,0,sectionSlot,quantity,deposit));
            return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Library] Cursor preparation deferred: "+error.GetType().Name);return false;}
    }
    public static bool RequestPrepareSellStash(World world,int playerId,bool bag,int inventorySlot,int librarySlot,int quantity,bool deposit)
    {
        if(!Resolve(world,playerId,out var player,out var connection,out var current)||
            !player.IsSpawned()||player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player)||
            RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||quantity<=0||quantity>ushort.MaxValue||
            !RebirthBackpackSellStashClientViews.TryGet(world,playerId,out var view)||view.TransferPending||
            view.CreationId!=current||librarySlot<0||librarySlot>=view.Capacity)return false;
        var xui=player.PlayerUI?.xui;
        if(player.inventory==null||player.inventory.IsHoldingItemActionRunning()||xui==null||xui.IsUsingItemActionEntryUse||
            xui.DragAndDropWindow?.CurrentStack==null||!xui.DragAndDropWindow.CurrentStack.IsEmpty())return false;
        var slots=bag?player.bag?.ItemGrid.items:player.inventory.ItemGrid.items;
        int owned=slots==null?0:bag?slots.Length:RebirthToolbeltCapacity.GetOwnedSlotCount(player,slots.Length);
        if(inventorySlot<0||inventorySlot>=owned||slots[inventorySlot]==null)return false;
        try
        {
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackSellStashPrepareRequest));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibraryOfferChunk));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibrarySettleRequest));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibrarySettled));
            var request=NetPackageManager.GetPackage<NetPackageRebirthBackpackSellStashPrepareRequest>()
                .Setup(playerId,current,view.GearRevision,bag,inventorySlot,librarySlot,quantity,deposit);
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(request);
            return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Sell Stash] Owner preparation deferred: "+error.GetType().Name);return false;}
    }
    public static bool Receive(World world,int playerId,RebirthBackpackLibraryOfferChunk chunk)
    {
        if(!Resolve(world,playerId,out var player,out var connection,out var current)||chunk==null||chunk.CreationId!=current)return false;
        if(!ReferenceEquals(boundWorld,world)||!ReferenceEquals(owner,player)||!ReferenceEquals(session,connection)||creation!=current)
        {Reset();boundWorld=world;owner=player;session=connection;creation=current;inbox=new RebirthBackpackLibraryOwnerInbox(player,current,connection);}
        if(!inbox.Receive(player,current,connection,chunk))return false;
        if(inbox.TryGetOffer(player,current,connection,chunk.TransactionId,out _))
            completeTransaction=chunk.TransactionId;
        return true;
    }
    // Only complete, authenticated receipt data is exposed; partial chunk identities are not offers.
    public static bool TryGetCurrentOffer(World world,int playerId,out RebirthBackpackLibraryReceipt receipt)
    {
        receipt=null;return completeTransaction!=Guid.Empty&&TryGetOffer(world,playerId,completeTransaction,out receipt);
    }
    public static bool TryGetOffer(World world,int playerId,Guid transaction,out RebirthBackpackLibraryReceipt receipt)
    {
        receipt=null;
        return Resolve(world,playerId,out var player,out var connection,out var current)&&ReferenceEquals(boundWorld,world)&&inbox!=null&&
            inbox.TryGetOffer(player,current,connection,transaction,out receipt);
    }
    // Explicit owner action after complete authenticated offer delivery.
    // Saving and settlement requests remain separate; application is not saved proof.
    public static RebirthBackpackLibraryOwnerResult ApplyOffer(World world,int playerId,Guid transaction)
    {
        if(!Resolve(world,playerId,out var player,out var connection,out var current)||
            !TryGetOffer(world,playerId,transaction,out var offer))return RebirthBackpackLibraryOwnerResult.Pending;
        try
        {
            // Refuse new custody if this session cannot request/receive its confirmation.
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibrarySettleRequest));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibrarySettled));
        }
        catch{return RebirthBackpackLibraryOwnerResult.Pending;}
        if(!RebirthBackpackLibraryReservation.TryAcquire(player,current,offer))return RebirthBackpackLibraryOwnerResult.Pending;
        var result=RebirthBackpackLibraryOwnerTransfer.Apply(player,current,offer);
        if(result==RebirthBackpackLibraryOwnerResult.Indeterminate&&
            RebirthBackpackLibraryOwnerTransfer.RecoverApplied(player,current,offer))return RebirthBackpackLibraryOwnerResult.Applied;
        return result;
    }
    // Explicit interaction step. Caller coalesces retries while awaiting the authenticated reply.
    // Native checkpoint scheduling is not disk proof; reservation release remains reply-owned.
    public static bool AdvanceCurrentOffer(World world,int playerId,out RebirthBackpackLibraryOwnerResult result)
    {
        result=RebirthBackpackLibraryOwnerResult.Pending;
        if(!TryGetCurrentOffer(world,playerId,out var offer)||!Guid.TryParse(offer.TransactionId,out var transaction))return false;
        result=ApplyOffer(world,playerId,transaction);
        if(result!=RebirthBackpackLibraryOwnerResult.Applied&&result!=RebirthBackpackLibraryOwnerResult.Rejected)return false;
        if(!RequestOwnerCheckpoint(world,playerId,transaction))return false;
        return RequestSettlement(world,playerId,transaction);
    }
    public static bool RequestOwnerCheckpoint(World world,int playerId,Guid transaction)
    {
        if(!Resolve(world,playerId,out var player,out var connection,out var current)||
            !TryGetOffer(world,playerId,transaction,out var offer)||
            !RebirthBackpackLibraryReservation.Matches(player,current,offer))return false;
        float result=player.Buffs.GetCustomVar(RebirthBackpackLibraryOwnerTransfer.ReceiptKey(offer));
        if(result!=1f&&result!=-1f)return false;
        try
        {
            NetPackageManager.GetPackageId(typeof(NetPackagePlayerData));
            // Native update sends full player data, including receipt and inventory.
            // Scheduling is not an acknowledgment of server disk persistence.
            GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
            return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Library] Owner checkpoint deferred: "+error.GetType().Name);return false;}
    }
    public static bool RequestSettlement(World world,int playerId,Guid transaction)
    {
        if(!Resolve(world,playerId,out var player,out var connection,out var current)||
            !TryGetOffer(world,playerId,transaction,out var offer)||!RebirthBackpackLibraryReservation.Matches(player,current,offer))return false;
        float result=player.Buffs.GetCustomVar(RebirthBackpackLibraryOwnerTransfer.ReceiptKey(offer));
        if(result!=1f&&result!=-1f)return false;
        try
        {
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibrarySettleRequest));
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthBackpackLibrarySettleRequest>().Setup(playerId,current,transaction,result==1f));
            return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Library] Settlement request deferred: "+error.GetType().Name);return false;}
    }
    public static bool ReceiveSettlement(World world,int playerId,Guid character,Guid transaction,long revision,bool applied)
        =>ReceiveSettlement(world,playerId,character.ToString("N"),transaction,revision,applied);
    public static bool ReceiveSettlement(World world,int playerId,string character,Guid transaction,long revision,bool applied)
    {
        if(!Resolve(world,playerId,out var player,out var connection,out var current)||!RebirthSurvivorRequestScope.Matches(character,current)||
            !TryGetOffer(world,playerId,transaction,out var offer)||revision!=offer.ExpectedGearRevision+1||
            player.Buffs.GetCustomVar(RebirthBackpackLibraryOwnerTransfer.ReceiptKey(offer))!=(applied?1f:-1f))return false;
        if(!RebirthBackpackLibraryReservation.ReleaseSettled(player,current,offer,
            receipt=>receipt.TransactionId==transaction.ToString("N")&&RebirthSurvivorRequestScope.Matches(character,receipt.CreationId)))return false;
        lastSettlement=RebirthBackpackLibrarySettlement.Create(offer,applied);
        inbox.Clear(connection,transaction);completeTransaction=Guid.Empty;return true;
    }
    // Read-only completion evidence for UI continuations. Never permission to repeat a move.
    // Bounded to the latest confirmed result in this exact owner/connection/character session.
    public static bool TryGetSettlement(World world,int playerId,Guid transaction,out RebirthBackpackLibrarySettlement result)
    {
        result=null;
        if(transaction==Guid.Empty||lastSettlement==null||
            !Resolve(world,playerId,out var player,out var connection,out var current)||
            !ReferenceEquals(boundWorld,world)||!ReferenceEquals(owner,player)||!ReferenceEquals(session,connection)||creation!=current||
            lastSettlement.CreationId!=current||lastSettlement.TransactionId!=transaction.ToString("N"))return false;
        result=lastSettlement;return true;
    }
    // Advisory refresh floor only; never an inventory application authority.
    internal static bool TryGetSettledRevision(World world,int playerId,out long revision)
    {
        revision=-1;var retained=lastSettlement;
        if(retained==null||!Guid.TryParseExact(retained.TransactionId,"N",out var transaction)||
            !TryGetSettlement(world,playerId,transaction,out var current)||!ReferenceEquals(retained,current))return false;
        revision=current.GearRevision;return revision>=0;
    }
    public static void Reset(){boundWorld=null;inbox=null;owner=null;session=null;creation=null;completeTransaction=Guid.Empty;lastSettlement=null;}
}
