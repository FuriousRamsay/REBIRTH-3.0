using System;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthHumanEquipment:XUiController
{
    private static XUiC_RebirthHumanEquipment active;
    private bool storageMode,nativeStorage,dataReady;private int storageOffset;
    private readonly ItemStack[] storage=new ItemStack[169];
    private EntityRebirthHumanoidNPC npc;private string stable,request;private bool visible,pending,syncing;private float nextRequest,requestAt;
    private readonly ItemStack[] equipped=new ItemStack[10];
    private XUiController[] gear;private XUiC_RebirthHumanEquipmentPlayerSlot[] cells;
    private XUiV_Label feedback,title,inspectName,inspectDescription,inspectStats,capacity;private XUiV_Sprite inspectIcon;
    private RebirthCharacterModelBinder model;private int selectedSource=-1,selectedGear=-1,offset;private ItemStack inspected=ItemStack.Empty.Clone();
    private XElement retained;private bool inspectingGear;private int modifySource=-1,holdingIndex,bagCapacity;
    private int queuedTarget=-1,queuedSource=-1;private string queuedFrame;
    internal bool Busy=>pending||retained!=null||queuedTarget>=0;
    internal bool RequestPending=>pending;
    public override void Init()
    {
        base.Init();gear=new XUiController[10];
        for(int i=0;i<gear.Length;i++){int index=i;gear[i]=GetChildById("npcEquipSlot"+i);if(gear[i]!=null){gear[i].OnPress+=(s,b)=>{if(b==0||b==-1)Interact(index,false);};gear[i].OnDrag+=(s,t,d)=>{if(t==EDragType.DragStart)Interact(index,true);};}}
        cells=new XUiC_RebirthHumanEquipmentPlayerSlot[52];
        for(int i=0;i<cells.Length;i++){cells[i]=GetChildById("npcEquipPlayer"+i) as XUiC_RebirthHumanEquipmentPlayerSlot;if(cells[i]!=null)cells[i].Owner=this;}
        feedback=Label("npcEquipFeedback");title=Label("npcEquipName");inspectName=Label("npcEquipInspectName");inspectDescription=Label("npcEquipInspectDescription");inspectStats=Label("npcEquipInspectStats");capacity=Label("npcEquipCapacity");
        inspectIcon=GetChildById("npcEquipInspectIcon")?.ViewComponent as XUiV_Sprite;model=new RebirthCharacterModelBinder(GetChildById("npcEquipModel"));
        Wire("npcEquipApply",()=>{if(selectedSource>=0&&selectedGear>=0)Begin(selectedGear,selectedSource);});
        Wire("npcEquipRemove",()=>{if(selectedGear>=0)Unequip(selectedGear);});
        Wire("npcEquipModify",Modify);
        Wire("npcEquipSort",()=>{if(!Busy&&xui.DragAndDropWindow.IsEmpty()){xui.playerUI.entityPlayer.bag.ItemGrid.Sort();RefreshCells();}});
        var area=GetChildById("npcEquipPlayerGrid");if(area!=null)area.OnScroll+=(s,d)=>{if(!Busy){offset=Math.Max(0,Math.Min(Math.Max(0,(xui.playerUI.entityPlayer.bag.SlotCount-1)/13-3),offset+(d<0?1:-1)));RefreshCells();}};
        for(int i=0;i<52;i++){int cell=i;var c=GetChildById("npcEquipCargo"+i);if(c!=null){c.OnPress+=(s,b)=>{if(b==0||b==-1)Interact(10+storageOffset*13+cell,false);};c.OnDrag+=(s,t,d)=>{if(t==EDragType.DragStart)Interact(10+storageOffset*13+cell,true);};}}
        var cargo=GetChildById("npcEquipStorageGrid");if(cargo!=null)cargo.OnScroll+=(s,d)=>{if(!Busy){storageOffset=Math.Max(0,Math.Min(Math.Max(0,(bagCapacity-1)/13-3),storageOffset+(d<0?1:-1)));RefreshStorage();}};
        for(int i=0;i<equipped.Length;i++)equipped[i]=ItemStack.Empty.Clone();
    }
    private XUiV_Label Label(string name)=>GetChildById(name)?.ViewComponent as XUiV_Label;
    private void Wire(string id,Action action){var c=GetChildById(id);if(c!=null)c.OnPress+=(s,b)=>{if(b==0||b==-1)action();};}
    internal bool NativeStorageFor(RebirthCompanionListEntry entry)=>nativeStorage&&npc!=null&&entry?.EntityId==npc.entityId;
    internal void Present(RebirthCompanionListEntry entry,bool show,bool cargo=false)
    {
        storageMode=cargo;
        visible=show;ViewComponent.IsVisible=show;
        if(!show){model?.Suspend();if(ReferenceEquals(active,this))active=null;return;}
        active=this;
        var found=xui.playerUI.entityPlayer.world.GetEntity(entry?.EntityId??-1) as EntityRebirthHumanoidNPC;
        if(found!=npc)
        {
            if(Busy)return;
            dataReady=false;bagCapacity=0;Array.Clear(storage,0,storage.Length);for(int i=0;i<equipped.Length;i++)equipped[i]=ItemStack.Empty.Clone();npc=found;stable=found?.RebirthRuntimeState?.StableId.ToString();request=null;retained=null;selectedGear=selectedSource=-1;nextRequest=0;model?.Clear();
        }
        if(npc==null||!RebirthHumanEquipment.Human(npc))
        {Set(feedback,"xuiRbNpcEquipmentHumanOnly",true);foreach(var c in gear)SetActive(c,false);foreach(var c in cells)SetActive(c,false);model?.Clear();return;}
        SetActive(GetChildById("npcEquipCharacter"),!cargo);SetActive(GetChildById("npcEquipLoadout"),!cargo);SetActive(GetChildById("npcEquipStorage"),cargo);
        Set(title,entry?.Name??npc.EntityName);RefreshCells();RefreshStorage();
    }
    public override void Update(float dt)
    {
        base.Update(dt);if(!visible||npc==null||!ReferenceEquals(active,this))return;
        var player=xui.playerUI.entityPlayer;
        if(pending&&Time.realtimeSinceStartup>=requestAt+5f)pending=false;
        if(player.world!=npc.world||npc.IsDead()||!ReferenceEquals(player.world.GetEntity(npc.entityId),npc))
        {pending=false;queuedTarget=-1;retained=null;Set(feedback,"xuiRbNpcEquipmentUnavailable",true);model?.Suspend();return;}
        if(Time.realtimeSinceStartup>=nextRequest)
        {
            nextRequest=Time.realtimeSinceStartup+.75f;
            if(!pending)
            {
                if(queuedTarget>=0)
                {
                    if(queuedSource>=player.bag.SlotCount||RebirthHumanEquipment.Frame(player.bag.ItemGrid[queuedSource])!=queuedFrame){queuedTarget=-1;Set(feedback,"xuiRbNpcEquipmentConflict",true);}
                    else Send(1,queuedTarget,queuedSource,queuedFrame);
                }
                else Send(retained==null?0:2,-1,-1,retained==null?"":(string)retained.Attribute("id"));
            }
            else if(Time.realtimeSinceStartup>=requestAt+5f){pending=false;}
            RefreshCells();model?.Bind(npc,false);
        }
    }
    private void Send(int action,int target,int source,string argument)
    {
        if(npc==null)return;request=Guid.NewGuid().ToString("N");pending=true;requestAt=Time.realtimeSinceStartup;
        var player=xui.playerUI.entityPlayer;
        if(!player.world.IsRemote())AcceptReply(RebirthHumanEquipment.Handle(player,npc,action,target,source,argument));
        else SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthHumanEquipmentRequest>().Setup(player.entityId,npc.entityId,action,target,source,request,argument,stable));
    }
    public static void Receive(int player,string token,string payload)
    {
        var panel=active;if(panel==null||panel.xui.playerUI.entityPlayer.entityId!=player||panel.request!=token)return;
        try{panel.AcceptReply(XElement.Parse(payload));}catch(Exception e){Log.Warning("[REBIRTH NPC Equipment UI] "+e.GetType().Name);panel.pending=false;}
    }
    private void AcceptReply(XElement root)
    {
        pending=false;
        if(root.Attribute("error")!=null){string error=(string)root.Attribute("error");if(error!="xuiRbNpcEquipmentWaitSave")queuedTarget=-1;Set(feedback,error,true);return;}
        if((int?)root.Attribute("entity")!=npc.entityId||(string)root.Attribute("stable")!=stable)return;
        foreach(var node in root.Elements("slot")){int i=(int)node.Attribute("id");if(i<0||i>=10)throw new InvalidOperationException();equipped[i]=RebirthHumanEquipment.Decode(node.Value);}
        queuedTarget=-1;var previousPlan=retained;retained=root.Element("swap");
        nativeStorage=(int?)root.Attribute("native")==1&&(int?)root.Attribute("legacy")==0;
        if((int?)root.Attribute("native")==1&&npc.world.IsRemote())npc.Buffs.SetCustomVar(RebirthHumanEquipment.NativeMode,1f,false);
        foreach(var row in root.Element("storage")?.Elements("cell")??Enumerable.Empty<XElement>())
        {int index=(int)row.Attribute("id");if(index<0||index>=169)throw new InvalidOperationException();storage[index]=RebirthHumanEquipment.Decode(row.Value);}
        dataReady=true;holdingIndex=(int?)root.Attribute("holding")??-1;bagCapacity=(int?)root.Attribute("capacity")??0;
        if(retained!=null)
        {
            int source=(int)retained.Attribute("source");var p=xui.playerUI.entityPlayer;string marker="rbNpcEquip_"+(string)retained.Attribute("id");
            if((string)retained.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(p)){Set(feedback,"xuiRbNpcEquipmentConflict",true);return;}
            if(source<0||source>=p.bag.SlotCount){Set(feedback,"xuiRbNpcEquipmentConflict",true);return;}
            string before=(string)retained.Element("before"),after=(string)retained.Element("after");float phase=p.Buffs.GetCustomVar(marker);
            string frame=RebirthHumanEquipment.Frame(p.bag.ItemGrid[source]);
            if(phase!=0f&&phase!=1f&&phase!=2f||(phase==0f&&frame!=before)||(phase==1f&&frame!=before&&frame!=after)||phase!=2f&&p.Died!=(int)retained.Attribute("deaths")){Set(feedback,"xuiRbNpcEquipmentConflict",true);return;}
            if(phase!=2f)
            {
                if(OtherOwnerHold(p)){Set(feedback,"xuiRbNpcEquipmentBusy",true);return;}
                p.Buffs.SetCustomVar(marker,1f,true);GameManager.Instance.SaveLocalPlayerData();
                if(frame!=after)p.bag.SetSlot(source,RebirthHumanEquipment.Decode(after));
                if(RebirthHumanEquipment.Frame(p.bag.ItemGrid[source])!=after){Set(feedback,"xuiRbNpcEquipmentConflict",true);return;}
                p.Buffs.SetCustomVar(marker,2f,true);GameManager.Instance.SaveLocalPlayerData();
            }
            else GameManager.Instance.SaveLocalPlayerData();
            Set(feedback,"xuiRbNpcEquipmentWaitSave",true);nextRequest=Time.realtimeSinceStartup+.5f;
        }
        else
        {
            if(previousPlan!=null){xui.playerUI.entityPlayer.Buffs.RemoveCustomVar("rbNpcEquip_"+(string)previousPlan.Attribute("id"));GameManager.Instance.SaveLocalPlayerData();}
            string settled=(string)root.Attribute("settled");if(!string.IsNullOrEmpty(settled)&&xui.playerUI.entityPlayer.Buffs.GetCustomVar("rbNpcEquip_"+settled)!=0f){xui.playerUI.entityPlayer.Buffs.RemoveCustomVar("rbNpcEquip_"+settled);GameManager.Instance.SaveLocalPlayerData();}
            Set(feedback,"xuiRbNpcEquipmentHint",true);model?.Bind(npc,false);
            if(modifySource>=0){selectedSource=modifySource;modifySource=-1;inspectingGear=false;Modify();}
        }
        RefreshEquipment();RefreshCells();RefreshStorage();
    }
    private void Begin(int slot,int source)
    {
        if(!dataReady||Busy||OtherOwnerHold(xui.playerUI.entityPlayer)||npc==null||source<0||source>=xui.playerUI.entityPlayer.bag.SlotCount||!xui.DragAndDropWindow.IsEmpty())return;
        var stack=xui.playerUI.entityPlayer.bag.ItemGrid[source];if(!RebirthHumanEquipment.Accept(slot,stack)){Set(feedback,"xuiRbNpcEquipmentWrongItem",true);return;}
        queuedTarget=slot;queuedSource=source;queuedFrame=RebirthHumanEquipment.Frame(stack);
        GameManager.Instance.SaveLocalPlayerData();Send(1,slot,source,queuedFrame);
    }
    private void Interact(int slot,bool dragging)
    {
        if(!dataReady||Busy||npc==null)return;selectedGear=slot;var cursor=xui.DragAndDropWindow;var stack=cursor.CurrentStack;
        if(!stack.IsEmpty())
        {
            if(OtherOwnerHold(xui.playerUI.entityPlayer)){Set(feedback,"xuiRbNpcEquipmentBusy",true);return;}
            if(!RebirthHumanEquipment.Accept(slot,stack)){Set(feedback,"xuiRbNpcEquipmentWrongItem",true);return;}
            var items=xui.playerUI.entityPlayer.bag.ItemGrid.items;
            int source=Array.FindIndex(items,s=>s==null||s.IsEmpty());
            if(source<0||!stack.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack,source)){Set(feedback,"xuiRbNpcEquipmentPlayerFull",true);return;}
            xui.playerUI.entityPlayer.bag.SetSlot(source,stack.Clone());
            cursor.SetCurrentStack(ItemStack.Empty.Clone(),false);
            if(source>=0)Begin(slot,source);return;
        }
        Inspect(slot<10?equipped[slot]:storage[slot-10]??ItemStack.Empty.Clone(),true);if(dragging||InputUtils.ShiftKeyPressed)Unequip(slot);
    }
    private void Unequip(int slot)
    {
        var items=xui.playerUI.entityPlayer.bag.ItemGrid.items;int source=Array.FindIndex(items,s=>s==null||s.IsEmpty());
        if(source<0){Set(feedback,"xuiRbNpcEquipmentPlayerFull",true);return;}Begin(slot,source);
    }
    internal void SelectPlayer(XUiC_RebirthHumanEquipmentPlayerSlot slot){if(Busy)return;selectedSource=slot.SlotNumber;Inspect(slot.ItemStack,false);}
    private void Inspect(ItemStack stack,bool gearItem)
    {
        inspected=stack?.Clone()??ItemStack.Empty.Clone();inspectingGear=gearItem;
        if(inspectIcon!=null)inspectIcon.SpriteName=inspected.IsEmpty()?"":inspected.itemValue.ItemClass.GetIconName();
        Set(inspectName,inspected.IsEmpty()?"":Localization.Get(inspected.itemValue.ItemClass.GetItemName()));
        Set(inspectDescription,inspected.IsEmpty()?"":XUiC_RebirthCraftingItemContext.ResolveDescription(inspected,xui));
        string stats=inspected.IsEmpty()||inspected.itemValue.MaxUseTimes<=0?"":string.Format(Localization.Get("xuiRbNpcEquipmentCondition"),Mathf.RoundToInt(inspected.itemValue.PercentUsesLeft*100f));
        if(!inspected.IsEmpty())
        {
            var display=UIDisplayInfoManager.Current.GetDisplayStatsForTag(inspected.itemValue.ItemClass.DisplayType);
            if(display!=null)foreach(var stat in display.DisplayStats.Take(2))
                stats+="\n"+(stat.TitleOverride??UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType))+": "+RebirthItemStatColors.NativeValue(inspected,xui.playerUI.entityPlayer,stat);
            if(inspectingGear&&selectedGear==9)stats+="\n"+string.Format(Localization.Get("xuiRbNpcEquipmentNpcCapacity"),bagCapacity);
        }
        Set(inspectStats,stats);
    }
    private void Modify()
    {
        // Native modification must own a real player source; unequip first rather than modifying a mirror.
        if(Busy||!xui.DragAndDropWindow.IsEmpty())return;
        if(inspectingGear)
        {
            if(selectedGear>=0){modifySource=Array.FindIndex(xui.playerUI.entityPlayer.bag.ItemGrid.items,s=>s==null||s.IsEmpty());if(modifySource>=0)Unequip(selectedGear);}
            return;
        }
        if(selectedSource<offset*13||selectedSource>=offset*13+cells.Length)
        {offset=Math.Max(0,Math.Min(Math.Max(0,(xui.playerUI.entityPlayer.bag.SlotCount-1)/13-3),selectedSource/13));RefreshCells();}
        var slot=cells.FirstOrDefault(c=>c!=null&&c.SlotNumber==selectedSource);if(slot==null||slot.ItemStack.IsEmpty()||Busy)return;
        slot.InfoWindow=xui.GetChildByType<XUiC_ItemInfoWindow>();if(slot.InfoWindow==null)return;
        var action=new ItemActionEntryAssemble(slot);action.RefreshEnabled();if(action.Enabled)action.OnActivated();else action.OnDisabledActivate();
    }
    private void RefreshEquipment()
    {
        for(int i=0;i<gear.Length;i++)
        {
            var c=gear[i];SetActive(c,true);var icon=c?.GetChildById("itemIcon")?.ViewComponent as XUiV_Sprite;
            if(icon!=null)icon.SpriteName=equipped[i].IsEmpty()?"":equipped[i].itemValue.ItemClass.GetIconName();
            var count=c?.GetChildById("count")?.ViewComponent as XUiV_Label;if(count!=null)count.Text=equipped[i].IsEmpty()?"":equipped[i].count.ToString();
            var bar=c?.GetChildById("condition")?.ViewComponent;
            if(bar!=null){bool has=!equipped[i].IsEmpty()&&equipped[i].itemValue.MaxUseTimes>0;bar.IsVisible=has;if(has)bar.Size=new Vector2i(Math.Max(1,(int)((c.ViewComponent.Size.x-6)*equipped[i].itemValue.PercentUsesLeft)),5);}
            if(i==4||i==5)Set(Label("npcEquipWeaponName"+i),equipped[i].IsEmpty()?"":Localization.Get(equipped[i].itemValue.ItemClass.GetItemName()));
        }
        Set(Label("npcEquipWeaponStatus"),equipped[5].IsEmpty()?"":string.Format(Localization.Get("xuiRbNpcEquipmentLoaded"),equipped[5].itemValue.Meta)+(holdingIndex==1?"\n"+Localization.Get("xuiRbNpcEquipmentInUse"):""));
    }
    private void RefreshStorage()
    {
        Set(Label("npcEquipNpcStorageCapacity"),string.Format(Localization.Get("xuiRbNpcEquipmentStorageSummary"),storage.Take(bagCapacity).Count(s=>s!=null&&!s.IsEmpty()),bagCapacity));
        for(int i=0;i<52;i++)
        {
            var c=GetChildById("npcEquipCargo"+i);if(c==null)continue;int index=storageOffset*13+i;SetActive(c,storageMode&&index<bagCapacity);
            var stack=index<storage.Length?storage[index]:null;
            var icon=c.GetChildById("itemIcon")?.ViewComponent as XUiV_Sprite;if(icon!=null)icon.SpriteName=stack==null||stack.IsEmpty()?"":stack.itemValue.ItemClass.GetIconName();
            var text=c.GetChildById("count")?.ViewComponent as XUiV_Label;if(text!=null)text.Text=stack==null||stack.IsEmpty()?"":stack.count.ToString();
        }
    }
    private void RefreshCells()
    {
        if(cells==null)return;syncing=true;
        try{var p=xui.playerUI.entityPlayer;for(int i=0;i<cells.Length;i++)if(cells[i]!=null){int index=offset*13+i;bool show=index<p.bag.SlotCount;SetActive(cells[i],show);if(show&&!cells[i].AssembleLock){cells[i].SlotNumber=index;cells[i].ItemStack=p.bag.ItemGrid[index].Clone();cells[i].InfoWindow=xui.GetChildByType<XUiC_ItemInfoWindow>();cells[i].ViewComponent.EventOnPress=!Busy;cells[i].ViewComponent.EventOnDrag=!Busy;}}Set(capacity,string.Format(Localization.Get("xuiRbNpcEquipmentPlayerCapacity"),p.bag.GetUsedSlotCount(),p.bag.SlotCount));}
        finally{syncing=false;}
    }
    internal void PlayerChanged(int index,ItemStack stack){if(!syncing&&!Busy)xui.playerUI.entityPlayer.bag.SetSlot(index,stack.Clone());}
    private static bool OtherOwnerHold(EntityPlayerLocal p)=>p==null||RebirthGearOwnerReservation.IsHeld(p)||RebirthBackpackLibraryReservation.IsHeld(p)||RebirthCookingOutputDelivery.IsHeld(p)||
        RebirthLogisticsOwnerReceipt.IsHeld(p)||AdvancedFarmingHarvestOwnerDelta.IsHeld(p)||AdvancedFarmingWaterExchangeOriginal.IsHeld(p)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(p)||
        RebirthCamperEnergyOwnerHold.Blocks(p.bag)||RebirthCamperEnergyOwnerHold.Blocks(p.inventory)||RebirthCamperStationNativeActorPayment.Blocks(p.bag)||RebirthCamperStationNativeActorPayment.Blocks(p.inventory)||RebirthPlacedOriginal.IsHeld(p);
    private static void Set(XUiV_Label label,string text,bool localize=false){if(label!=null)label.Text=localize?Localization.Get(text):text;}
    private static void SetActive(XUiController c,bool show){if(c?.ViewComponent!=null)c.ViewComponent.IsVisible=show;}
    internal void Shutdown(){dataReady=false;visible=false;pending=false;retained=null;queuedTarget=-1;request=null;npc=null;model?.Clear();if(ReferenceEquals(active,this))active=null;}
}
[Preserve]
public sealed class XUiC_RebirthHumanEquipmentPlayerSlot:XUiC_ItemStack
{
    internal XUiC_RebirthHumanEquipment Owner;
    public override void Init(){base.Init();StackLocation=StackLocationTypes.Backpack;SlotChangedEvent+=(i,s)=>Owner?.PlayerChanged(i,s);}
    public override void updateItemInfoWindow(XUiC_ItemStack stack){}
    public override void HandleClickComplete(){base.HandleClickComplete();Owner?.SelectPlayer(this);}
    public override void Update(float dt){if(Owner?.Busy==true)return;base.Update(dt);}
}





