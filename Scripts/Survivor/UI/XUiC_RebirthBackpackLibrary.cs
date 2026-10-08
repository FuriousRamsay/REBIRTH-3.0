using System;
using System.Collections.Generic;


// Native inventory changes use the journal receipt handshake; display projections never authorize them.
public sealed class XUiC_RebirthBackpackLibrary : XUiController
{
    private readonly RebirthWindowHudScope hud=new RebirthWindowHudScope();
    private sealed class Binding { internal XUiController Control; internal XUiEvent_OnPressEventHandler Handler; }
    private readonly List<Binding> bindings=new List<Binding>();
    private readonly XUiController[] slots=new XUiController[RebirthBackpackLibraryPolicy.MaxSlots];
    private readonly XUiController[] highlights=new XUiController[RebirthBackpackLibraryPolicy.MaxSlots];
    private readonly XUiV_Sprite[] icons=new XUiV_Sprite[RebirthBackpackLibraryPolicy.MaxSlots];
    private readonly XUiV_Label[] counts=new XUiV_Label[RebirthBackpackLibraryPolicy.MaxSlots];
    private XUiV_Label capacity,status,purpose,inspectName,inspectDescription;
    private XUiController storeAction,takeAction,dragPreview;
    private XUiV_Sprite dragPreviewIcon;private XUiV_Label dragPreviewCount;
    private XUiV_Sprite inspectIcon; private int selectedBag=-1; private ItemStack selectedBagImage;
    private XUiC_RebirthCharacterOverviewList backpackScroll;
    private XUiController workspaceBody; private Vector2i layoutSize=new Vector2i(-1,-1);
    private readonly XUiController[] storageControls=new XUiController[RebirthBackpackLibraryPolicy.MaxSlots];
    private EntityPlayerLocal dragOwner;private RebirthBackpackLibraryView dragView;private ItemStack dragImage;
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
    private RebirthBackpackLibraryView displayed;
    private readonly List<int> available=new List<int>();
    private readonly List<ItemStack> availableImages=new List<ItemStack>();
    private readonly XUiV_Label[] availableLabels=new XUiV_Label[169];
    private readonly ItemStack[] bagImages=new ItemStack[169];
    private readonly XUiController[] bagControls=new XUiController[169]; private readonly XUiV_Sprite[] bagIcons=new XUiV_Sprite[169];
    private string feedback;
    private bool requestOutstanding;
    private long requestedRevision;
    private RebirthBackpackLibraryView requestedView;
    private EntityPlayerLocal interactionOwner;
    private World interactionWorld;
    private string interactionCreation;
    private DateTime nextFinish;
    private int continuationRemaining;
    private float continuationTimer;
    private RebirthBackpackLibraryStudyIntent studyIntent;
    private bool studySelecting;
    private float studySelectionDeadline;
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
            ?RebirthBackpackLibraryClientOffers.RequestPrepareCursor(player.world,player.entityId,view.GearRevision,slot.SlotNumber,quantity,deposit,false)
            :RebirthBackpackLibraryServer.PrepareLocalCursor(player,view.CreationId,view.GearRevision,slot.SlotNumber,quantity,deposit,false,out offer);
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
            if(RebirthBackpackLibraryPolicy.IsLearningMaterial(inspection.itemValue))nativeActions.AddActionListEntry(new RebirthBackpackSectionReadEntry(slot,Study));
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
        Unbind();studyIntent=null;studySelecting=false;
        base.Init();nativeActions=GetChildById("theoryNativeActions") as XUiC_ItemActionList;
        dragPreview=GetChildById("theoryDragPreview");dragPreviewIcon=GetChildById("theoryDragPreviewIcon")?.ViewComponent as XUiV_Sprite;dragPreviewCount=GetChildById("theoryDragPreviewCount")?.ViewComponent as XUiV_Label;
        storeAction=GetChildById("theoryDeposit");takeAction=GetChildById("theoryWithdraw");
        backpackScroll=GetChildById("theoryBagScroll") as XUiC_RebirthCharacterOverviewList;
        workspaceBody=GetChildById("theoryBody");inspectName=GetChildById("theoryInspectName")?.ViewComponent as XUiV_Label;inspectDescription=GetChildById("theoryInspectDescription")?.ViewComponent as XUiV_Label;inspectIcon=GetChildById("theoryInspectIcon")?.ViewComponent as XUiV_Sprite;
        purpose=GetChildById("theoryPurpose")?.ViewComponent as XUiV_Label;
        capacity=GetChildById("theoryCapacity")?.ViewComponent as XUiV_Label;
        status=GetChildById("theoryStatus")?.ViewComponent as XUiV_Label;
        for(int i=0;i<slots.Length;i++)
        {
            slots[i]=GetChildById("theorySlot"+i);
            highlights[i]=GetChildById("theorySelected"+i);
            icons[i]=GetChildById("theoryIcon"+i)?.ViewComponent as XUiV_Sprite;
            counts[i]=GetChildById("theoryCount"+i)?.ViewComponent as XUiV_Label;storageControls[i]=GetChildById("theoryChoose"+i);
        }
        for(int i=0;i<169;i++)
        {
            int row=i;bagControls[i]=GetChildById("theoryAvailable"+i);bagIcons[i]=GetChildById("theoryAvailableIcon"+i)?.ViewComponent as XUiV_Sprite;
            availableLabels[i]=GetChildById("theoryAvailableLabel"+i)?.ViewComponent as XUiV_Label;
        }
        Bind("theoryDeposit",(s,b)=>Deposit(selectedBag));
        Bind("theoryStudy",(s,b)=>Study());
        Bind("theoryWithdraw",(s,b)=>Withdraw());
        Bind("theoryFinish",(s,b)=>Finish());
        Bind("theoryPrev",(s,b)=>{page=Math.Max(0,page-1);Render();});
        Bind("theoryNext",(s,b)=>{if((page+1)*8<available.Count)page++;Render();});
        Bind("theoryClose",(s,b)=>xui.playerUI.windowManager.Close("rebirthBackpackLibrary"));
    }
    public override void Cleanup(){hud.Restore();Unbind();studyIntent=null;studySelecting=false;base.Cleanup();}
    public override void OnClose(){hud.Restore();CancelQuick();CancelDrag();selectedBag=-1;selectedBagImage=null;studyIntent=null;studySelecting=false;base.OnClose();}
    public override void OnOpen(){CancelQuick();CancelDrag();selectedBag=-1;selectedBagImage=null;layoutSize=new Vector2i(-1,-1);backpackScroll?.ResetPosition();studyIntent=null;base.OnOpen();refresh=0;requestRefresh=0;selected=-1;page=0;feedback=null;displayed=null;Request();Render();}
    public override void Update(float dt)
    {
        base.Update(dt);if(!IsOpen)return;hud.Maintain(xui);Layout();BindQuickReceipt();UpdateDragPreview();
        AdvanceStudy();if(!IsOpen)return;
        if(continuationRemaining>0)
        {
            continuationTimer+=dt;
            if(continuationTimer>=2f&&DateTime.UtcNow>=nextFinish)
            {
                continuationTimer=0;continuationRemaining--;Finish();
                if(continuationRemaining==0&&requestOutstanding)feedback="xuiRebirthTheoryDelayed";
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
    private bool Current(out EntityPlayerLocal player,out RebirthBackpackLibraryView view)
    {
        player=xui?.playerUI?.entityPlayer;view=null;
        return player!=null&&RebirthBackpackLibraryClientViews.TryGet(player.world,player.entityId,out view)&&
            ReferenceEquals(view,displayed)&&RebirthBackpackSectionProjectionPolicy.Matches(player,view)&&!requestOutstanding&&!view.TransferPending&&!(RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player));
    }
    private void Deposit(int row,bool quick=false)
    {
        if(!quick)CancelQuick();
        int index=row;
        if(!Current(out var player,out var view)){feedback="xuiRebirthTheoryChanged";Render();return;}
        if(row<0||row>=169||index>=available.Count||selectedBagImage==null||selectedBagImage.IsEmpty()||!RebirthBackpackLibraryPolicy.IsLearningMaterial(selectedBagImage.itemValue))return;
        var bag=player.bag?.ItemGrid.items;int inventory=available[index];
        if(bag==null||inventory>=bag.Length||!RebirthStationGridIngredients.IsSameStackSnapshot(bag[inventory],selectedBagImage))
        {feedback="xuiRebirthTheoryChanged";Render();return;}
        if(player.bag.LockedSlots!=null&&inventory<player.bag.LockedSlots.Length&&player.bag.LockedSlots[inventory])return;
        if(RebirthBackpackSectionDestination.TryFind(bag[inventory],view.Capacity,i=>view.TryGetSlot(i,out var cell)?cell:null,null,out var destination,out var quantity))
        {Prepare(player,view,inventory,destination,quantity,true,quick);return;}
        feedback="xuiRebirthTheoryFull";Render();
    }
    private void Study()
    {
        CancelQuick();
        if(!Current(out var player,out var view)){feedback="xuiRebirthTheoryChanged";Render();return;}
        var belt=player.inventory?.ItemGrid.items;
        int owned=belt==null?0:RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length);
        int destination=-1;
        for(int i=0;i<owned;i++)if(belt[i]!=null&&belt[i].IsEmpty()){destination=i;break;}
        if(destination<0){feedback="xuiRebirthTheoryStudyBeltFull";Render();return;}
        if(!RebirthBackpackLibraryStudyIntent.TryCreate(player,view,selected,destination,out var intent))
        {feedback="xuiRebirthTheoryStudyUnavailable";Render();return;}
        RebirthBackpackLibraryReceipt offer=null;
        bool accepted=player.world.IsRemote()
            ?RebirthBackpackLibraryClientOffers.RequestPrepare(player.world,player.entityId,false,destination,selected,1,false)
            :RebirthBackpackLibraryServer.PrepareLocal(player,view.CreationId,view.GearRevision,false,destination,selected,1,false,out offer);
        if(!accepted){feedback="xuiRebirthTheoryChanged";Render();return;}
        studyIntent=intent;studySelecting=false;
        if(offer!=null&&!intent.TryBind(player,offer))studyIntent=null;
        continuationRemaining=10;continuationTimer=0;requestOutstanding=true;
        requestedRevision=view.GearRevision;requestedView=view;displayed=null;selected=-1;
        feedback="xuiRebirthTheoryStudyPreparing";Request();Render();
    }
    private void AdvanceStudy()
    {
        var intent=studyIntent;if(intent==null)return;
        var player=xui?.playerUI?.entityPlayer;
        if(player?.world==null||player.IsDead()){studyIntent=null;return;}
        if(string.IsNullOrEmpty(intent.TransactionId))
        {
            if(!player.world.IsRemote())return;
            if(!RebirthBackpackLibraryClientOffers.TryGetCurrentOffer(player.world,player.entityId,out var offer))return;
            if(!intent.TryBind(player,offer)){studyIntent=null;return;}
        }
        RebirthBackpackLibrarySettlement outcome;
        bool settled=player.world.IsRemote()
            ?RebirthBackpackLibraryClientOffers.TryGetSettlement(player.world,player.entityId,Guid.Parse(intent.TransactionId),out outcome)
            :RebirthBackpackLibraryServer.TryGetLocalSettlement(player,RebirthBackpackSectionProjectionPolicy.GetCreationId(player),intent.TransactionId,out outcome);
        if(!settled)return;
        if(!intent.TryGetSettledBook(player,outcome,out var book))
        {studyIntent=null;feedback="xuiRebirthTheoryStudyUnavailable";return;}
        // Native local holding changes complete after a holster delay. Recheck next update.
        if(!studySelecting)
        {
            studySelecting=true;studySelectionDeadline=UnityEngine.Time.unscaledTime+3f;
            player.inventory.SetHoldingItemIdx(intent.ToolbeltSlot);return;
        }
        var held=player.inventory.holdingItemItemValue;
        if(player.inventory.holdingItemIdx!=intent.ToolbeltSlot||held==null||
            held.type!=book.itemValue.type||held.Seed!=book.itemValue.Seed)
        {
            if(UnityEngine.Time.unscaledTime>=studySelectionDeadline)
            {studyIntent=null;feedback="xuiRebirthTheoryStudyUnavailable";}
            return;
        }
        studyIntent=null;
        xui.playerUI.windowManager.Close("rebirthBackpackLibrary");
        ItemActionStudyLiteratureRebirth.Dispatch(player,book.itemValue);
    }
    private void Withdraw(bool quick=false)
    {
        if(!quick)CancelQuick();
        if(!Current(out var player,out var view)){feedback="xuiRebirthTheoryChanged";Render();return;}
        if(!view.TryGetSlot(selected,out var item)||item.IsEmpty()){feedback="xuiRebirthTheorySelect";Render();return;}
        var bag=player.bag?.ItemGrid.items;if(bag==null)return;
        if(RebirthBackpackSectionDestination.TryFind(item,bag.Length,i=>bag[i],i=>player.bag.LockedSlots!=null&&i<player.bag.LockedSlots.Length&&player.bag.LockedSlots[i],out var destination,out var quantity))
        {Prepare(player,view,destination,selected,quantity,false,quick);return;}
        feedback="xuiRebirthTheoryBagFull";Render();
    }
    private void Prepare(EntityPlayerLocal player,RebirthBackpackLibraryView view,int inventory,int library,int count,bool deposit,bool quick=false)
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
            ?RebirthBackpackLibraryClientOffers.RequestPrepare(player.world,player.entityId,true,inventory,library,count,deposit)
            :RebirthBackpackLibraryServer.PrepareLocal(player,view.CreationId,view.GearRevision,true,inventory,library,count,deposit,out prepared);
        if(accepted&&quick)
        {
            quickOwner=player;quickWorld=player.world;quickCreation=view.CreationId;quickRevision=view.GearRevision;
            quickDeposit=deposit;quickSourceIndex=deposit?inventory:library;quickSourceImage=source;
            if(prepared!=null)BindQuickReceipt(prepared);
        }
        feedback=accepted?"xuiRebirthTheoryRequested":"xuiRebirthTheoryChanged";
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
        if(receipt==null||quickOwner==null||receipt.IsSellStash!=false||!receipt.IsBag||
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
        AdvanceStudy(); // Bind an offer before manual recovery can retire it.
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
        feedback="xuiRebirthTheoryFinishing";Request();Render();
    }
    private void Request()
    {
        var player=xui?.playerUI?.entityPlayer;
        if(player!=null)RebirthBackpackLibraryClientViews.Request(player.world,player.entityId);
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
        RebirthBackpackLibraryView view=null;
        if(player!=null)RebirthBackpackLibraryClientViews.TryGet(player.world,player.entityId,out view);
        if(view!=null&&!RebirthBackpackSectionProjectionPolicy.Matches(player,view))
        { view=null;CancelDrag();selected=-1;selectedBag=-1;selectedBagImage=null; }
        // Existing Update/Request refresh throttle remains; no pending receipt/custody/cache reset.
        // A fresh idle projection permits retry only; it cannot cancel a journal or release custody.
        if(requestOutstanding&&view!=null&&!ReferenceEquals(view,requestedView)&&view.GearRevision>=requestedRevision&&
            !view.TransferPending&&player!=null&&!(RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)))
        {
            requestOutstanding=false;requestedView=null;continuationRemaining=0;continuationTimer=0;
            feedback=view.GearRevision>requestedRevision?"xuiRebirthTheoryComplete":"xuiRebirthTheoryRetry";
        }
        string currentCreation=player==null?string.Empty:RebirthBackpackSectionProjectionPolicy.GetCreationId(player);
        if(!ReferenceEquals(interactionOwner,player)||!ReferenceEquals(interactionWorld,player?.world)||interactionCreation!=currentCreation)
        {
            CancelQuick();requestOutstanding=false;requestedView=null;continuationRemaining=0;continuationTimer=0;requestedRevision=-1;nextFinish=DateTime.MinValue;selected=-1;selectedBag=-1;selectedBagImage=null;feedback=null;displayed=null;
            studyIntent=null;studySelecting=false;
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
        bool hasStats=RebirthBackpackSectionStats.Render(this,inspection,"theory");
        if(inspectDescription!=null)inspectDescription.Size=new Vector2i(hasStats?350:760,200);
        var bagCaption=GetChildById("theoryBagCapacity")?.ViewComponent as XUiV_Label;
        bagCaption?.SetTextImmediately(bag==null?"":RebirthCraftingInventoryBridge.GetUsedSlotCount(xui)+"/"+bag.Length+" | "+RebirthCraftingInventoryBridge.GetEncumberedUsedSlotCount(xui)+" ENCUMBERED");
        bool hasInspection=inspection!=null&&!inspection.IsEmpty()&&inspection.itemValue?.ItemClass!=null;
        if(inspectName!=null)inspectName.Text=hasInspection?Localization.Get(inspection.itemValue.ItemClass.GetItemName()):string.Empty;
        if(inspectIcon!=null){inspectIcon.IsVisible=hasInspection;if(hasInspection){inspectIcon.SpriteName=inspection.itemValue.GetPropertyOverride(ItemClass.PropCustomIcon,inspection.itemValue.ItemClass.GetIconName());inspectIcon.Color=inspection.itemValue.ItemClass.GetIconTint(inspection.itemValue);}}
        if(inspectDescription!=null)inspectDescription.Text=hasInspection?XUiC_RebirthCraftingItemContext.ResolveDescription(inspection,xui):string.Empty;
        (GetChildById("theorySelectedCount")?.ViewComponent as XUiV_Label)?.SetTextImmediately(hasInspection&&RebirthConsumableResolver.TryResolve(inspection.itemValue,out var liquid)&&liquid.IsDrink?RebirthLiquidContainerService.FormatVolume(RebirthLiquidContainerService.GetRemainingMl(inspection.itemValue,liquid)):hasInspection?inspection.count.ToString():"");
        bool ready=Current(out _,out _);
        if(storeAction?.ViewComponent!=null)storeAction.ViewComponent.Enabled=ready&&selectedBag>=0&&hasInspection&&RebirthBackpackLibraryPolicy.IsLearningMaterial(inspection.itemValue);
        if(takeAction?.ViewComponent!=null)takeAction.ViewComponent.Enabled=ready&&selected>=0&&selectedBag<0&&hasInspection;
        if(purpose!=null)
        {
            string purposeId=string.Empty;int purposeCount;
            if(hasInspection)purposeId=inspection.itemValue.ItemClass.GetItemName();else if(view!=null)view.TryGetDisplaySlot(selected,out purposeId,out purposeCount);
            RebirthAudiobookDefinition audio;RebirthLiteratureDefinition literature;
            bool audiobook=RebirthProgressionRuntimeConfig.TryGetAudiobook(purposeId,out audio)&&audio!=null;
            if(audiobook)purposeId=audio.SourceLiteratureId;
            string key="xuiRebirthTheoryPurposeSelect";
            if(RebirthProgressionRuntimeConfig.TryGetLiterature(purposeId,out literature)&&literature!=null)
                key=string.Equals(literature.Kind,"theory",StringComparison.OrdinalIgnoreCase)?"xuiRebirthTheoryPurposeTheory":
                    string.Equals(literature.Kind,"discovery",StringComparison.OrdinalIgnoreCase)?"xuiRebirthTheoryPurposeRecipe":"xuiRebirthTheoryPurposeReference";
            else if(string.Equals(purposeId,"rebirthSupportLeisurePuzzleBook",StringComparison.Ordinal))key="rebirthSupportLeisurePuzzleBookDesc";
            else if(string.Equals(purposeId,"noteDuke01",StringComparison.Ordinal))key="noteDuke01Desc";
            else if(!string.IsNullOrEmpty(purposeId))
            {
                string descriptionKey=ItemClass.GetItem(purposeId,false)?.ItemClass?.GetItemDescriptionKey();
                key=!string.IsNullOrEmpty(descriptionKey)&&Localization.Exists(descriptionKey)
                    ?descriptionKey:"xuiRebirthTheoryStorageHelp";
            }
            purpose.Text=hasInspection&&!RebirthBackpackLibraryPolicy.IsLearningMaterial(inspection.itemValue)?Localization.Get("xuiRebirthTheoryNotMaterial"):(audiobook?Localization.Get("xuiRebirthTheoryPurposeAudio")+" ":string.Empty)+Localization.Get(key);
        }
        if(capacity!=null)capacity.Text=view==null?Localization.Get(player!=null&&RebirthBackpackLibraryClientViews.IsNoBackpack(player.world,player.entityId)?"xuiRebirthTheoryNoBackpack":"xuiRebirthTheoryWaiting"):view.OccupiedSlots+" / "+view.Capacity;
        if(status!=null)status.Text=Localization.Get(view!=null&&view.TransferPending?"xuiRebirthTheoryPending":feedback??"xuiRebirthTheoryStorageHelp");
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
            if(slots[i]?.ViewComponent!=null)slots[i].ViewComponent.ToolTip=item==null?Localization.Get("xuiRebirthTheoryEmpty"):Localization.Get(item.GetItemName());
        }
    }
}