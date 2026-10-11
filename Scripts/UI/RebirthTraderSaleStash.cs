using System;
using UnityEngine;
// One custody receipt for a whole sale: merchandise never occupies Backpack slots.
internal sealed class RebirthTraderSaleStash
{
    private readonly XUiC_RebirthTraderWorkspace owner;
    private EntityPlayerLocal player;
    private string creation;
    private bool running;
    private float nextRequest,deadline;
    private RebirthBackpackLibraryReceipt receipt;
    internal bool Running=>running;
    internal RebirthTraderSaleStash(XUiC_RebirthTraderWorkspace owner){this.owner=owner;}
    internal void Cancel(){running=false;player=null;receipt=null;}
    internal void Start()
    {
        if(running||RebirthConsoleInputGuardRuntime.BlocksGameplayInput())return;var ui=owner.xui;var p=ui?.playerUI?.entityPlayer;
        if(p?.world==null||p.IsDead()||!p.IsSpawned()||ui.IsUsingItemActionEntryUse||p.inventory.IsHoldingItemActionRunning()||!(ui.Trader?.Trader is EntityTrader trader)||ui.DragAndDropWindow?.IsEmpty()!=true||RebirthBackpackLibraryReservation.IsHeld(p)||RebirthGearOwnerReservation.IsHeld(p)||
            !RebirthBackpackSellStashClientViews.TryGet(p.world,p.entityId,out var view)||view.TransferPending||!RebirthBackpackSectionProjectionPolicy.Matches(p,view))return;
        player=p;creation=view.CreationId;receipt=null;
        if(p.world.IsRemote())
        {
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            connection.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerInventory>().Setup(p,true,true,false,false));
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthBackpackBatchSaleRequest>().Setup(p.entityId,trader.entityId,creation,view.GearRevision));
        }
        else if(!RebirthBackpackLibraryServer.PrepareBatchSale(p,null,creation,view.GearRevision,trader.entityId,out receipt))
        {GameManager.ShowTooltip(p,Localization.Get("xuiRebirthSellChanged"));Cancel();return;}
        running=true;nextRequest=0;deadline=Time.realtimeSinceStartup+30f;Advance();
    }
    internal void Advance()
    {
        if(!running)return;
        if(player?.world==null||!ReferenceEquals(owner.xui?.playerUI?.entityPlayer,player)){Cancel();return;}
        if(Time.realtimeSinceStartup>deadline){GameManager.ShowTooltip(player,Localization.Get("xuiRebirthSellDelayed"));Cancel();return;}
        if(Time.realtimeSinceStartup<nextRequest)return;nextRequest=Time.realtimeSinceStartup+.25f;
        bool remote=player.world.IsRemote();
        if(remote)
        {
            if(receipt==null&&RebirthBackpackLibraryClientOffers.TryGetCurrentOffer(player.world,player.entityId,out var offer)&&offer.IsBatchSale)receipt=offer;
            if(receipt!=null)RebirthBackpackLibraryClientOffers.AdvanceCurrentOffer(player.world,player.entityId,out _);
        }
        else RebirthBackpackLibraryServer.AdvanceLocalInteraction(player,creation,out _);
        if(receipt==null)return;
        RebirthBackpackLibrarySettlement settlement;
        bool done=remote?RebirthBackpackLibraryClientOffers.TryGetSettlement(player.world,player.entityId,Guid.Parse(receipt.TransactionId),out settlement):RebirthBackpackLibraryServer.TryGetLocalSettlement(player,creation,receipt.TransactionId,out settlement);
        if(!done)return;
        if(settlement?.Applied==true)Audio.Manager.PlayInsidePlayerHead("ui_trader_purchase",-1,0f,false,false);
        owner.xui?.Trader?.TraderWindowGroup?.RefreshTraderItems();
        RebirthBackpackSellStashClientViews.Request(player.world,player.entityId);Cancel();
    }
}
