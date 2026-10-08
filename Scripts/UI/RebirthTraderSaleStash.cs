using System;
using UnityEngine;
// Bulk sale stages each stack through the existing authoritative section-transfer receipt,
// then invokes the native sale action on its real Backpack slot. No custom wallet mutation.
internal sealed class RebirthTraderSaleStash
{
    private readonly XUiC_RebirthTraderWorkspace owner;
    private EntityPlayerLocal player;
    private World world;
    private object trader;
    private string creation;
    private long revision;
    private int sectionIndex,bagIndex,quantity,nextIndex;
    private ItemStack source;
    private RebirthBackpackLibraryReceipt receipt;
    private RebirthBackpackQuickTransferContinuation gate;
    private bool running;
    private float nextRequest,deadline;
    internal bool Running=>running;
    internal RebirthTraderSaleStash(XUiC_RebirthTraderWorkspace owner){this.owner=owner;}
    internal void Cancel(){running=false;player=null;world=null;trader=null;creation=null;source=null;receipt=null;gate=null;}
    private void Stop(string key){var p=player;Cancel();if(p!=null)GameManager.ShowTooltip(p,Localization.Get(key));}
    internal void Start()
    {
        if(running)return;
        var p=owner.xui?.playerUI?.entityPlayer;
        if(p?.world==null||owner.xui.Trader?.Trader==null||
            !RebirthBackpackSellStashClientViews.TryGet(p.world,p.entityId,out var view)||
            !RebirthBackpackSectionProjectionPolicy.Matches(p,view)||view.TransferPending)return;
        player=p;world=p.world;trader=owner.xui.Trader.Trader;creation=view.CreationId;
        running=true;nextIndex=0;receipt=null;source=null;nextRequest=0;
        Advance();
    }
    internal void Advance()
    {
        if(!running)return;
        var ui=owner.xui;
        if(!owner.windowGroup.isShowing||!ReferenceEquals(ui.playerUI?.entityPlayer,player)||
            !ReferenceEquals(player.world,world)||!ReferenceEquals(ui.Trader?.Trader,trader)||
            RebirthBackpackSectionProjectionPolicy.GetCreationId(player)!=creation)
        {Cancel();return;}
        if(Time.realtimeSinceStartup>=nextRequest)
        {
            nextRequest=Time.realtimeSinceStartup+1f;
            RebirthBackpackSellStashClientViews.Request(world,player.entityId);
            if(source!=null)
            {
                if(world.IsRemote())
                {if(RebirthBackpackLibraryClientOffers.TryGetCurrentOffer(world,player.entityId,out _))RebirthBackpackLibraryClientOffers.AdvanceCurrentOffer(world,player.entityId,out _);}
                else RebirthBackpackLibraryServer.AdvanceLocalInteraction(player,creation,out _);
            }
        }
        if(source!=null&&Time.realtimeSinceStartup>deadline){Stop("xuiRebirthSellDelayed");return;}
        if(!RebirthBackpackSellStashClientViews.TryGet(world,player.entityId,out var view)||
            !RebirthBackpackSectionProjectionPolicy.Matches(player,view))return;
        if(source!=null)
        {
            if(Time.realtimeSinceStartup>deadline){Stop("xuiRebirthSellDelayed");return;}
            if(receipt==null&&world.IsRemote()&&RebirthBackpackLibraryClientOffers.TryGetCurrentOffer(world,player.entityId,out var offered))Bind(offered);
            if(receipt==null||gate==null)return;
            RebirthBackpackLibrarySettlement settlement;
            bool found=world.IsRemote()
                ?RebirthBackpackLibraryClientOffers.TryGetSettlement(world,player.entityId,Guid.Parse(receipt.TransactionId),out settlement)
                :RebirthBackpackLibraryServer.TryGetLocalSettlement(player,creation,receipt.TransactionId,out settlement);
            if(!found)return;
            if(!settlement.Applied){Stop("xuiRebirthSellRetry");return;}
            if(view.TransferPending||view.GearRevision!=revision+1)return;
            var cells=new ItemStack[view.Capacity];for(int i=0;i<cells.Length;i++)view.TryGetSlot(i,out cells[i]);
            view.TryGetSlot(sectionIndex,out var current);
            if(!gate.TryAdvanceProjection(player,world,creation,view.GearRevision,false,settlement,view.BackpackItemId,cells,current,out _))
            {Stop("xuiRebirthSellChanged");return;}
            if(!receipt.TryGetImages(out _,out _,out _,out var expected)||player.bag?.ItemGrid?.items==null||bagIndex>=player.bag.ItemGrid.items.Length||
                !RebirthStationGridIngredients.IsSameStackSnapshot(player.bag.ItemGrid.items[bagIndex],expected))
            {Stop("xuiRebirthSellChanged");return;}
            var slots=owner.GetChildByType<XUiC_RebirthCraftingInventory>()?.GetItemStackControllers();
            if(slots==null||bagIndex>=slots.Length){Stop("xuiRebirthSellChanged");return;}
            var slot=slots[bagIndex];slot.ForceRefreshItemStack();slot.InfoWindow=owner.Details;
            owner.Details.SetItemStack(slot,true);owner.Details.BuySellCounter.SetCount(quantity);
            var action=new ItemActionEntrySell(slot);action.RefreshEnabled();
            if(!action.Enabled){action.OnDisabledActivate();Cancel();return;}
            int before=slot.ItemStack.count;action.OnActivated(); if(!running)return;
            if(slot.ItemStack.count!=before-quantity){Cancel();return;}
            source=null;receipt=null;gate=null;
        }
        if(view.TransferPending)return;
        while(nextIndex<view.Capacity)
        {
            int index=nextIndex++;
            if(!view.TryGetSlot(index,out var stack))continue;
            int count=RebirthBackpackSaleQuote.TradableCount(ui,stack);
            if(count<=0)continue;
            var quote=stack.Clone();quote.count=count;
            if(!RebirthItemSaleEstimate.TryGet(ui,quote,out int price)||price<=0)continue;
            var bag=player.bag?.ItemGrid?.items;if(bag==null){Stop("xuiRebirthSellChanged");return;}
            int empty=-1;for(int i=0;i<bag.Length;i++)if((bag[i]==null||bag[i].IsEmpty())&&
                !(player.bag.LockedSlots!=null&&i<player.bag.LockedSlots.Length&&player.bag.LockedSlots[i])){empty=i;break;}
            if(empty<0){Stop("xuiRebirthSellBagFull");return;}
            sectionIndex=index;bagIndex=empty;quantity=count;revision=view.GearRevision;source=stack.Clone();deadline=Time.realtimeSinceStartup+30f;
            RebirthBackpackLibraryReceipt prepared=null;
            bool accepted=world.IsRemote()
                ?RebirthBackpackLibraryClientOffers.RequestPrepareSellStash(world,player.entityId,true,empty,index,count,false)
                :RebirthBackpackLibraryServer.PrepareLocalSellStash(player,creation,revision,true,empty,index,count,false,out prepared);
            if(!accepted){Stop("xuiRebirthSellChanged");return;}
            if(prepared!=null)Bind(prepared); if(!running)return;
            RebirthBackpackSellStashClientViews.Request(world,player.entityId);
            return;
        }
        Cancel();
    }
    private void Bind(RebirthBackpackLibraryReceipt candidate)
    {
        if(candidate==null||!candidate.IsSellStash||!candidate.IsBag||candidate.Deposit||candidate.CreationId!=creation||
            candidate.ExpectedGearRevision!=revision||candidate.InventorySlot!=bagIndex||candidate.LibrarySlot!=sectionIndex||candidate.Quantity!=quantity||
            !RebirthBackpackQuickTransferContinuation.TryCreate(player,world,candidate,source,out var continuation))
        {Stop("xuiRebirthSellChanged");return;}
        receipt=candidate;gate=continuation;
    }
}