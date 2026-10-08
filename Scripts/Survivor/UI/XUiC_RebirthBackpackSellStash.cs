using System;
using System.Collections.Generic;


// Native inventory changes use the journal receipt handshake; display projections never authorize them.
public sealed class XUiC_RebirthBackpackSellStash : XUiController
{
    private readonly RebirthWindowHudScope hud=new RebirthWindowHudScope();
    private sealed class Binding { internal XUiController Control; internal XUiEvent_OnPressEventHandler Handler; }
    private readonly List<Binding> bindings=new List<Binding>();
    private readonly XUiController[] slots=new XUiController[RebirthBackpackSellStashPolicy.MaxSlots];
    private readonly XUiController[] highlights=new XUiController[RebirthBackpackSellStashPolicy.MaxSlots];
    private readonly XUiV_Sprite[] icons=new XUiV_Sprite[RebirthBackpackSellStashPolicy.MaxSlots];
    private readonly XUiV_Label[] counts=new XUiV_Label[RebirthBackpackSellStashPolicy.MaxSlots];
    private XUiV_Label capacity,status,purpose,inspectName,inspectDescription;
    private XUiController storeAction,takeAction,dragPreview;
    private XUiV_Sprite dragPreviewIcon;private XUiV_Label dragPreviewCount;
    private XUiV_Sprite inspectIcon; private int selectedBag=-1; private ItemStack selectedBagImage;
    private XUiC_RebirthCharacterOverviewList backpackScroll;
    private XUiController workspaceBody; private Vector2i layoutSize=new Vector2i(-1,-1);
    private readonly XUiController[] storageControls=new XUiController[RebirthBackpackSellStashPolicy.MaxSlots];
    private EntityPlayerLocal dragOwner;private RebirthBackpackSellStashView dragView;private ItemStack dragImage;
    private EntityPlayerLocal quickOwner;
    private World quickWorld;
    private string quickCreation;
    private long quickRevision;
    private int quickSourceIndex;
    private bool quickDeposit;
    private ItemStack quickSourceImage;
    private RebirthBackpackLibraryReceipt quickReceipt;
    private RebirthBackpackQuickTransferContinuation quickGate;
    private float refresh,requestRefresh;
    private int selected=-1,page;
    private RebirthBackpackSellStashView displayed;
    private readonly List<int> available=new List<int>();
    private readonly List<ItemStack> availableImages=new List<ItemStack>();
    private readonly XUiV_Label[] availableLabels=new XUiV_Label[169];
    private readonly ItemStack[] bagImages=new ItemStack[169];
    private readonly XUiController[] bagControls=new XUiController[169]; private readonly XUiV_Sprite[] bagIcons=new XUiV_Sprite[169];
    private string feedback;
    private bool requestOutstanding;
    private long requestedRevision;
    private RebirthBackpackSellStashView requestedView;
    private EntityPlayerLocal interactionOwner;
    private World interactionWorld;
    private string interactionCreation;
    private DateTime nextFinish;
    private int continuationRemaining;
    private float continuationTimer;
    private XUiC_ItemActionList nativeActions;
    public void NativeSelect(XUiC_RebirthBackpackSectionNativeSlot slot,bool bag)
    {
        if(slot==null||slot.ItemStack==null)return;
        selectedBag=bag?slot.SlotNumber:-1;selected=bag?-1:slot.SlotNumber;
        selectedBagImage=bag?slot.ItemStack.Clone():null;
        Render();
    }
    public void NativeTransfer(XUiC_RebirthBackpackSectionNativeSlot slot,bool bag,bool half,bool one,bool quick)
    {
        if(slot==null||!Current(out var player,out var view))return;
        NativeSelect(slot,bag);
        if(quick){if(bag)Deposit(slot.SlotNumber,true);else Withdraw(true);return;}
        if(bag)return;
        var cursor=xui.DragAndDropWindow.CurrentStack;
        bool deposit=!cursor.IsEmpty();
        if(!view.TryGetSlot(slot.SlotNumber,out var stored)||stored==null)return;
        if(!RebirthStationGridIngredients.IsSameStackSnapshot(stored,slot.ItemStack))return;
        var source=deposit?cursor:stored;
        if(source.IsEmpty())return;
        int quantity=one?1:half&&!deposit?Math.Max(1,source.count/2):source.count;
        var destination=deposit?stored:cursor;
        if(!destination.IsEmpty())
        {
            if(RebirthBackpackLibraryTransfer.CanMerge(source,destination))
                quantity=Math.Min(quantity,Math.Max(0,destination.itemValue.ItemClass.MaxCount-destination.count));
            else if(!deposit||one)return;
        }
        if(quantity<1)return;
        RebirthBackpackLibraryReceipt offer;
        bool accepted=player.world.IsRemote()
            ?RebirthBackpackLibraryClientOffers.RequestPrepareCursor(player.world,player.entityId,view.GearRevision,slot.SlotNumber,quantity,deposit,true)
            :RebirthBackpackLibraryServer.PrepareLocalCursor(player,view.CreationId,view.GearRevision,slot.SlotNumber,quantity,deposit,true,out offer);
        if(!accepted)return;
        CancelQuick();requestOutstanding=true;requestedRevision=view.GearRevision;requestedView=view;
        continuationRemaining=10;continuationTimer=0;displayed=null;selected=-1;selectedBag=-1;selectedBagImage=null;
        Finish();Request();Render();
    }
    private void NativeActions(ItemStack inspection)
    {
        if(nativeActions==null)return;
        var slot=selectedBag>=0?bagControls[selectedBag] as XUiC_RebirthBackpackSectionNativeSlot:
            selected>=0&&selected<slots.Length?slots[selected] as XUiC_RebirthBackpackSectionNativeSlot:null;
        bool has=slot!=null&&inspection!=null&&!inspection.IsEmpty();
        nativeActions.SetCraftingActionList(has&&selectedBag>=0?XUiC_ItemActionList.ItemActionListTypes.Item:XUiC_ItemActionList.ItemActionListTypes.None,has?slot:null);
        if(has&&selectedBag>=0){RebirthCharacterGearEquipEntry.Adapt(nativeActions,slot);RebirthEditorActions.Adapt(nativeActions,slot);}
        else if(has)
        {
            nativeActions.AddActionListEntry(new RebirthBackpackSectionTakeEntry(slot));
            
        }
        nativeActions.Update(0f);
        int count=nativeActions.itemActionEntries.Count;
        var entries=nativeActions.entryList;
        for(int i=0;i<entries.Count;i++)
        {
            var v=entries[i].ViewComponent;if(v==null)continue;
            v.IsVisible=i<count;if(v.UiTransform!=null)v.UiTransform.gameObject.SetActive(i<count);
        }
    }
    public override void Init()
    {
        Unbind();
        base.Init();nativeActions=GetChildById("sellNativeActions") as XUiC_ItemActionList;
        dragPreview=GetChildById("sellDragPreview");dragPreviewIcon=GetChildById("sellDragPreviewIcon")?.ViewComponent as XUiV_Sprite;dragPreviewCount=GetChildById("sellDragPreviewCount")?.ViewComponent as XUiV_Label;
        storeAction=GetChildById("sellDeposit");takeAction=GetChildById("sellWithdraw");
        backpackScroll=GetChildById("sellBagScroll") as XUiC_RebirthCharacterOverviewList;
        workspaceBody=GetChildById("sellBody");inspectName=GetChildById("sellInspectName")?.ViewComponent as XUiV_Label;inspectDescription=GetChildById("sellInspectDescription")?.ViewComponent as XUiV_Label;inspectIcon=GetChildById("sellInspectIcon")?.ViewComponent as XUiV_Sprite;
        purpose=GetChildById("sellPurpose")?.ViewComponent as XUiV_Label;
        capacity=GetChildById("sellCapacity")?.ViewComponent as XUiV_Label;
        status=GetChildById("sellStatus")?.ViewComponent as XUiV_Label;
        for(int i=0;i<slots.Length;i++)
        {
            slots[i]=GetChildById("sellSlot"+i);
            highlights[i]=GetChildById("sellSelected"+i);
            icons[i]=GetChildById("sellIcon"+i)?.ViewComponent as XUiV_Sprite;
            counts[i]=GetChildById("sellCount"+i)?.ViewComponent as XUiV_Label;storageControls[i]=GetChildById("sellChoose"+i);
        }
        for(int i=0;i<169;i++)
        {
            int row=i;bagControls[i]=GetChildById("sellAvailable"+i);bagIcons[i]=GetChildById("sellAvailableIcon"+i)?.ViewComponent as XUiV_Sprite;
            availableLabels[i]=GetChildById("sellAvailableLabel"+i)?.ViewComponent as XUiV_Label;
        }
        Bind("sellDeposit",(s,b)=>Deposit(selectedBag));
        Bind("sellWithdraw",(s,b)=>Withdraw());
        Bind("sellFinish",(s,b)=>Finish());
        Bind("sellPrev",(s,b)=>{page=Math.Max(0,page-1);Render();});
        Bind("sellNext",(s,b)=>{if((page+1)*8<available.Count)page++;Render();});
        Bind("sellClose",(s,b)=>xui.playerUI.windowManager.Close("rebirthBackpackSellStash"));
    }
    public override void Cleanup(){hud.Restore();Unbind();base.Cleanup();}
    public override void OnClose(){hud.Restore();CancelQuick();CancelDrag();selectedBag=-1;selectedBagImage=null;base.OnClose();}
    public override void OnOpen(){CancelQuick();CancelDrag();selectedBag=-1;selectedBagImage=null;layoutSize=new Vector2i(-1,-1);backpackScroll?.ResetPosition();base.OnOpen();refresh=0;requestRefresh=0;selected=-1;page=0;feedback=null;displayed=null;Request();Render();}
    public override void Update(float dt)
    {
        base.Update(dt);if(!IsOpen)return;hud.Maintain(xui);Layout();BindQuickReceipt();UpdateDragPreview();

        if(continuationRemaining>0)
        {
            continuationTimer+=dt;
            if(continuationTimer>=2f&&DateTime.UtcNow>=nextFinish)
            {
                continuationTimer=0;continuationRemaining--;Finish();
                if(continuationRemaining==0&&requestOutstanding)feedback="xuiRebirthSellDelayed";
            }
        }
        refresh+=dt;requestRefresh+=dt;if(refresh<1f)return;
        refresh=0;if(requestRefresh>=5f){requestRefresh=0;Request();}
        Render();AdvanceQuick();
    }
    private void Bind(string id,XUiEvent_OnPressEventHandler handler)
    {
        var control=GetChildById(id);
        if(control==null)return;
        if(control is XUiC_SimpleButton button)button.OnPressed+=handler;
        else control.OnPress+=handler;
        bindings.Add(new Binding{Control=control,Handler=handler});
    }
    private void Unbind()
    {
        CancelQuick();CancelDrag();
        foreach(var binding in bindings)
            if(binding.Control is XUiC_SimpleButton button)button.OnPressed-=binding.Handler;
            else binding.Control.OnPress-=binding.Handler;
        bindings.Clear();
    }
    private void CancelDrag(){dragOwner=null;dragView=null;dragImage=null;if(dragPreview?.ViewComponent!=null)dragPreview.ViewComponent.IsVisible=false;}
    private void UpdateDragPreview()
    {
        var preview=dragPreview?.ViewComponent;
        if(preview==null)return;
        if(dragImage==null||!IsOpen){preview.IsVisible=false;return;}
        if(!Current(out var player,out var view)||!ReferenceEquals(player,dragOwner)||!ReferenceEquals(view,dragView))
        {CancelDrag();return;}
        if(!RebirthInventoryDropHitTest.TryGetLocalPointer(workspaceBody,out var pointer)){preview.IsVisible=false;return;}
        if(dragPreviewIcon!=null)dragPreviewIcon.SpriteName=dragImage.itemValue.ItemClass.GetIconName();
        if(dragPreviewCount!=null)dragPreviewCount.Text=dragImage.count.ToString();
        preview.Position=new Vector2i(pointer.x+16,pointer.y-16);preview.TryUpdatePosition();preview.IsVisible=true;
    }
    private static bool ShiftHeld()=>UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift)||UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift);
    private bool Current(out EntityPlayerLocal player,out RebirthBackpackSellStashView view)
    {
        player=xui?.playerUI?.entityPlayer;view=null;
        return player!=null&&RebirthBackpackSellStashClientViews.TryGet(player.world,player.entityId,out view)&&
            ReferenceEquals(view,displayed)&&RebirthBackpackSectionProjectionPolicy.Matches(player,view)&&!requestOutstanding&&!view.TransferPending&&!(RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player));
    }
    private void Deposit(int row,bool quick=false)
    {
        if(!quick)CancelQuick();
        int index=row;
        if(!Current(out var player,out var view)){feedback="xuiRebirthSellChanged";Render();return;}
        if(row<0||row>=169||index>=available.Count||selectedBagImage==null||selectedBagImage.IsEmpty()||!RebirthBackpackSellStashPolicy.IsStorableItem(selectedBagImage.itemValue))return;
        var bag=player.bag?.ItemGrid.items;int inventory=available[index];
        if(bag==null||inventory>=bag.Length||!RebirthStationGridIngredients.IsSameStackSnapshot(bag[inventory],selectedBagImage))
        {feedback="xuiRebirthSellChanged";Render();return;}
        if(player.bag.LockedSlots!=null&&inventory<player.bag.LockedSlots.Length&&player.bag.LockedSlots[inventory])return;
        if(RebirthBackpackSectionDestination.TryFind(bag[inventory],view.Capacity,i=>view.TryGetSlot(i,out var cell)?cell:null,null,out var destination,out var quantity))
        {Prepare(player,view,inventory,destination,quantity,true,quick);return;}
        feedback="xuiRebirthSellFull";Render();
    }
    private void Withdraw(bool quick=false)
    {
        if(!quick)CancelQuick();
        if(!Current(out var player,out var view)){feedback="xuiRebirthSellChanged";Render();return;}
        if(!view.TryGetSlot(selected,out var item)||item.IsEmpty()){feedback="xuiRebirthSellSelect";Render();return;}
        var bag=player.bag?.ItemGrid.items;if(bag==null)return;
        if(RebirthBackpackSectionDestination.TryFind(item,bag.Length,i=>bag[i],i=>player.bag.LockedSlots!=null&&i<player.bag.LockedSlots.Length&&player.bag.LockedSlots[i],out var destination,out var quantity))
        {Prepare(player,view,destination,selected,quantity,false,quick);return;}
        feedback="xuiRebirthSellBagFull";Render();
    }
    private void Prepare(EntityPlayerLocal player,RebirthBackpackSellStashView view,int inventory,int library,int count,bool deposit,bool quick=false)
    {
        CancelQuick();
        ItemStack source=null;
        if(quick)
        {
            var bag=player.bag?.ItemGrid.items;
            if(deposit){if(bag!=null&&inventory>=0&&inventory<bag.Length)source=bag[inventory]?.Clone();}
            else view.TryGetSlot(library,out source);
            if(source==null||source.IsEmpty())return;
        }
        RebirthBackpackLibraryReceipt prepared=null;
        bool accepted=player.world.IsRemote()
            ?RebirthBackpackLibraryClientOffers.RequestPrepareSellStash(player.world,player.entityId,true,inventory,library,count,deposit)
            :RebirthBackpackLibraryServer.PrepareLocalSellStash(player,view.CreationId,view.GearRevision,true,inventory,library,count,deposit,out prepared);
        if(accepted&&quick)
        {
            quickOwner=player;quickWorld=player.world;quickCreation=view.CreationId;quickRevision=view.GearRevision;
            quickDeposit=deposit;quickSourceIndex=deposit?inventory:library;quickSourceImage=source;
            if(prepared!=null)BindQuickReceipt(prepared);
        }
        feedback=accepted?"xuiRebirthSellRequested":"xuiRebirthSellChanged";
        if(accepted){continuationRemaining=10;continuationTimer=0;requestOutstanding=true;requestedRevision=view.GearRevision;requestedView=view;displayed=null;selected=-1;Request();}
        Render();
    }
    private void CancelQuick(){quickOwner=null;quickWorld=null;quickCreation=null;quickSourceImage=null;quickReceipt=null;quickGate=null;}
    private void BindQuickReceipt()
    {
        if(quickOwner==null||quickReceipt!=null)return;
        var player=xui?.playerUI?.entityPlayer;
        if(!ReferenceEquals(player,quickOwner)||!ReferenceEquals(player?.world,quickWorld)||
            RebirthBackpackSectionProjectionPolicy.GetCreationId(player)!=quickCreation){CancelQuick();return;}
        if(player.world.IsRemote()&&RebirthBackpackLibraryClientOffers.TryGetCurrentOffer(player.world,player.entityId,out var receipt))
            BindQuickReceipt(receipt);
    }
    private void BindQuickReceipt(RebirthBackpackLibraryReceipt receipt)
    {
        if(receipt==null||quickOwner==null||receipt.IsSellStash!=true||!receipt.IsBag||
            receipt.CreationId!=quickCreation||receipt.ExpectedGearRevision!=quickRevision||receipt.Deposit!=quickDeposit||
            (quickDeposit?receipt.InventorySlot:receipt.LibrarySlot)!=quickSourceIndex||
            !RebirthBackpackQuickTransferContinuation.TryCreate(quickOwner,quickWorld,receipt,quickSourceImage,out var gate))
        {CancelQuick();return;}
        quickReceipt=receipt;quickGate=gate;
    }
    private void AdvanceQuick()
    {
        BindQuickReceipt();
        if(quickOwner==null||quickGate==null||quickReceipt==null||!Current(out var player,out var view))return;
        RebirthBackpackLibrarySettlement settled=null;
        bool found=player.world.IsRemote()
            ?RebirthBackpackLibraryClientOffers.TryGetSettlement(player.world,player.entityId,Guid.Parse(quickReceipt.TransactionId),out settled)
            :RebirthBackpackLibraryServer.TryGetLocalSettlement(player,quickCreation,quickReceipt.TransactionId,out settled);
        if(!found)return;
        var bag=player.bag?.ItemGrid.items;ItemStack source=null;
        if(quickDeposit){if(bag!=null&&quickSourceIndex>=0&&quickSourceIndex<bag.Length)source=bag[quickSourceIndex];}
        else view.TryGetSlot(quickSourceIndex,out source);
        var cells=new ItemStack[view.Capacity];for(int i=0;i<cells.Length;i++)view.TryGetSlot(i,out cells[i]);
        bool deposit=quickDeposit;int index=quickSourceIndex;
        bool admitted=quickGate.TryAdvanceProjection(player,player.world,view.CreationId,view.GearRevision,view.TransferPending,
            settled,view.BackpackItemId,cells,source,out var remainder);
        CancelQuick();
        if(!admitted||remainder.IsEmpty()||bag==null)return;
        if(deposit)
        {
            if(player.bag.LockedSlots!=null&&index<player.bag.LockedSlots.Length&&player.bag.LockedSlots[index])return;
            if(RebirthBackpackSectionDestination.TryFind(remainder,view.Capacity,i=>cells[i],null,out var destination,out var count))
                Prepare(player,view,index,destination,count,true,true);
        }
        else if(RebirthBackpackSectionDestination.TryFind(remainder,bag.Length,i=>bag[i],
            i=>player.bag.LockedSlots!=null&&i<player.bag.LockedSlots.Length&&player.bag.LockedSlots[i],out var destination,out var count))
            Prepare(player,view,destination,index,count,false,true);
    }
    private void Finish()
    {
        BindQuickReceipt();

        if(!IsOpen)return;
        var player=xui?.playerUI?.entityPlayer;if(player==null)return;
        if(DateTime.UtcNow<nextFinish)return;nextFinish=DateTime.UtcNow.AddSeconds(2);
        if(player.world.IsRemote())
        {
            if(!RebirthBackpackLibraryClientOffers.TryGetCurrentOffer(player.world,player.entityId,out _))
                RebirthBackpackLibraryClientOffers.RequestRecovery(player.world,player.entityId);
            else RebirthBackpackLibraryClientOffers.AdvanceCurrentOffer(player.world,player.entityId,out _);
        }
        else RebirthBackpackLibraryServer.AdvanceLocalInteraction(player,RebirthBackpackSectionProjectionPolicy.GetCreationId(player),out _);
        feedback="xuiRebirthSellFinishing";Request();Render();
    }
    private void Request()
    {
        var player=xui?.playerUI?.entityPlayer;
        if(player!=null)RebirthBackpackSellStashClientViews.Request(player.world,player.entityId);
    }
    private void Layout()
    {
        if(ViewComponent==null)return;Vector2i position,size;RebirthScreenLayout.GetScreenBounds(xui,out position,out size);
        if(layoutSize.x==size.x&&layoutSize.y==size.y)return;layoutSize=size;
        float scale=Math.Min(Math.Max(1,size.x-16)/1856f,Math.Max(1,size.y-78)/813f);
        ViewComponent.Position=new Vector2i(position.x+8,position.y-70);
        ViewComponent.Size=new Vector2i(UnityEngine.Mathf.RoundToInt(1856*scale),UnityEngine.Mathf.RoundToInt(813*scale));ViewComponent.TryUpdatePosition();
        if(workspaceBody?.ViewComponent?.UiTransform!=null)workspaceBody.ViewComponent.UiTransform.localScale=new UnityEngine.Vector3(scale,scale,1f);
    }
    public ItemStack SelectedItemStack { get; private set; }
    private void Render()
    {
        var player=xui?.playerUI?.entityPlayer;
        RebirthBackpackSellStashView view=null;
        if(player!=null)RebirthBackpackSellStashClientViews.TryGet(player.world,player.entityId,out view);
        if(view!=null&&!RebirthBackpackSectionProjectionPolicy.Matches(player,view))
        { view=null;CancelDrag();selected=-1;selectedBag=-1;selectedBagImage=null; }
        // Existing Update/Request refresh throttle remains; no pending receipt/custody/cache reset.
        // A fresh idle projection permits retry only; it cannot cancel a journal or release custody.
        if(requestOutstanding&&view!=null&&!ReferenceEquals(view,requestedView)&&view.GearRevision>=requestedRevision&&
            !view.TransferPending&&player!=null&&!(RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)))
        {
            requestOutstanding=false;requestedView=null;continuationRemaining=0;continuationTimer=0;
            feedback=view.GearRevision>requestedRevision?"xuiRebirthSellComplete":"xuiRebirthSellRetry";
        }
        string currentCreation=player==null?string.Empty:RebirthBackpackSectionProjectionPolicy.GetCreationId(player);
        if(!ReferenceEquals(interactionOwner,player)||!ReferenceEquals(interactionWorld,player?.world)||interactionCreation!=currentCreation)
        {
            CancelQuick();requestOutstanding=false;requestedView=null;continuationRemaining=0;continuationTimer=0;requestedRevision=-1;nextFinish=DateTime.MinValue;selected=-1;selectedBag=-1;selectedBagImage=null;feedback=null;displayed=null;
            
            interactionOwner=player;interactionWorld=player?.world;interactionCreation=currentCreation;
        }
        if(!ReferenceEquals(displayed,view))
        {
            if(displayed==null||view==null||displayed.CreationId!=view.CreationId||displayed.GearRevision!=view.GearRevision)selected=-1;
            displayed=view;
        }
        available.Clear();availableImages.Clear();var bag=player?.bag?.ItemGrid.items;
        for(int i=0;i<169;i++)
        {
            bool exists=bag!=null&&i<bag.Length;
            if(exists)available.Add(i);
            var cell=exists?bag[i]:null;bagImages[i]=cell?.Clone();var item=cell!=null&&!cell.IsEmpty()?cell.itemValue.ItemClass:null;
            var control=bagControls[i];if(control?.ViewComponent!=null)control.ViewComponent.IsVisible=exists;
            var icon=bagIcons[i];
            if(icon!=null){icon.IsVisible=item!=null;if(item!=null)icon.SpriteName=item.GetIconName();}
            if(availableLabels[i]!=null)availableLabels[i].Text=item==null?string.Empty:cell.count.ToString();
            if(control?.ViewComponent!=null)control.ViewComponent.ToolTip=item==null?string.Empty:Localization.Get(item.GetItemName());
        }
        backpackScroll?.SetItemCount(((bag?.Length??0)+12)/13,string.Empty);
        ItemStack inspection=null;
        if(selectedBag>=0&&bag!=null&&selectedBag<bag.Length&&RebirthStationGridIngredients.IsSameStackSnapshot(bag[selectedBag],selectedBagImage))inspection=selectedBagImage;
        else if(selectedBag>=0){selectedBag=-1;selectedBagImage=null;}
        if(inspection==null&&view!=null)view.TryGetSlot(selected,out inspection);
        SelectedItemStack=inspection;NativeActions(inspection);
        RebirthSelectedDurability.Render(this,"sellSelectedDurability",selectedBag>=0?bagControls[selectedBag]:selected>=0&&selected<slots.Length?slots[selected]:null);
        bool hasStats=RebirthBackpackSectionStats.Render(this,inspection,"sell");
        if(inspectDescription!=null)inspectDescription.Size=new Vector2i(hasStats?350:760,200);
        var bagCaption=GetChildById("sellBagCapacity")?.ViewComponent as XUiV_Label;
        bagCaption?.SetTextImmediately(bag==null?"":RebirthCraftingInventoryBridge.GetUsedSlotCount(xui)+"/"+bag.Length+" | "+RebirthCraftingInventoryBridge.GetEncumberedUsedSlotCount(xui)+" ENCUMBERED");
        bool hasInspection=inspection!=null&&!inspection.IsEmpty()&&inspection.itemValue?.ItemClass!=null;
        if(inspectName!=null)inspectName.Text=hasInspection?Localization.Get(inspection.itemValue.ItemClass.GetItemName()):string.Empty;
        if(inspectIcon!=null){inspectIcon.IsVisible=hasInspection;if(hasInspection){inspectIcon.SpriteName=inspection.itemValue.GetPropertyOverride(ItemClass.PropCustomIcon,inspection.itemValue.ItemClass.GetIconName());inspectIcon.Color=inspection.itemValue.ItemClass.GetIconTint(inspection.itemValue);}}
        if(inspectDescription!=null)inspectDescription.Text=hasInspection?XUiC_RebirthCraftingItemContext.ResolveDescription(inspection,xui):string.Empty;
        (GetChildById("sellSelectedCount")?.ViewComponent as XUiV_Label)?.SetTextImmediately(hasInspection&&RebirthConsumableResolver.TryResolve(inspection.itemValue,out var liquid)&&liquid.IsDrink?RebirthLiquidContainerService.FormatVolume(RebirthLiquidContainerService.GetRemainingMl(inspection.itemValue,liquid)):hasInspection?inspection.count.ToString():"");
        bool ready=Current(out _,out _);
        if(storeAction?.ViewComponent!=null)storeAction.ViewComponent.Enabled=ready&&selectedBag>=0&&hasInspection&&RebirthBackpackSellStashPolicy.IsStorableItem(inspection.itemValue);
        if(takeAction?.ViewComponent!=null)takeAction.ViewComponent.Enabled=ready&&selected>=0&&selectedBag<0&&hasInspection;
        if(purpose!=null)purpose.Text=Localization.Get("xuiRebirthSellStoragePurpose");
        if(capacity!=null)capacity.Text=view==null?Localization.Get(player!=null&&RebirthBackpackSellStashClientViews.IsNoBackpack(player.world,player.entityId)?"xuiRebirthSellNoBackpack":"xuiRebirthSellWaiting"):view.OccupiedSlots+" / "+view.Capacity;
        if(status!=null)status.Text=Localization.Get(view!=null&&view.TransferPending?"xuiRebirthSellPending":feedback??"xuiRebirthSellStorageHelp");
        for(int i=0;i<slots.Length;i++)
        {
            bool enabled=view!=null&&i<view.Capacity;
            if(slots[i] is XUiC_RebirthBackpackSectionNativeSlot native){native.SlotNumber=i;ItemStack full=null;if(enabled)view.TryGetSlot(i,out full);native.ItemStack=full?.Clone()??ItemStack.Empty.Clone();}
            var highlight=highlights[i]?.ViewComponent;if(highlight!=null)highlight.IsVisible=enabled&&i==selected;
            if(slots[i]?.ViewComponent!=null)slots[i].ViewComponent.IsVisible=enabled;
            string itemId=string.Empty;int count=0;if(enabled)view.TryGetDisplaySlot(i,out itemId,out count);
            var item=string.IsNullOrEmpty(itemId)?null:ItemClass.GetItem(itemId,false)?.ItemClass;
            if(icons[i]!=null){icons[i].IsVisible=item!=null;if(item!=null)icons[i].SpriteName=item.GetIconName();}
            if(counts[i]!=null)counts[i].Text=item==null?string.Empty:count.ToString();
            if(slots[i]?.ViewComponent!=null)slots[i].ViewComponent.ToolTip=item==null?Localization.Get("xuiRebirthSellEmpty"):Localization.Get(item.GetItemName());
        }
    }
}