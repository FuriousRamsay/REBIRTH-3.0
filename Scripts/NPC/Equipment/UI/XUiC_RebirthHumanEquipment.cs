using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthHumanEquipment:XUiController
{
    private const int CargoPool=117;
    private static XUiC_RebirthHumanEquipment active;
    private bool storageMode,nativeStorage,legacyFree,dataReady;private int storageOffset;
    private readonly ItemStack[] storage=new ItemStack[169];
    private EntityRebirthHumanoidNPC npc;private string stable,request;private bool visible,pending,syncing;private float nextRequest,requestAt;
    private readonly ItemStack[] equipped=new ItemStack[10];
    private ItemStack lockpicks=ItemStack.Empty.Clone();
    private XUiC_RebirthHumanEquipmentGearSlot lockpickControl;
    private XUiController[] gear;private XUiC_RebirthHumanEquipmentPlayerSlot[] cells;
    private XUiV_Label feedback,title,inspectName,inspectDescription,inspectStats,inspectCount,inspectSummary,capacity;private XUiV_Sprite inspectIcon;
    private readonly XUiV_Label[] inspectStatNames=new XUiV_Label[7],inspectStatValues=new XUiV_Label[7];
    private XUiC_RebirthReadonlyItemSlot inspectNative;
    private int selectedSource=-1,selectedGear=-1,offset;private ItemStack inspected=ItemStack.Empty.Clone();
    private XElement retained;private bool inspectingGear;private int modifySource=-1,holdingIndex,bagCapacity;
    private int queuedTarget=-1,queuedSource=-1;private string queuedFrame,queuedGuard;
    // NPC-origin drag: visual ghost only. The real player cursor never holds the NPC stack; the saved exchange
    // runs on release over a player cell.
    private int draggingNpc=-1,dragReleaseSeen=-1;private string draggingFrame;
    private EntityPlayerLocal draggingPlayer;private World draggingWorld;
    private XUiC_RebirthReadonlyItemSlot dragPreview;
    internal bool NpcDragActive=>draggingNpc>=0;
    // Deferred SDCS preview, ON by default. It reuses the Character Overview binder (same call the player's
    // Character window makes on open) but never binds in the open frame: only after the snapshot arrived, 30 frames
    // later, on a non-hitch frame, once per NPC and worn-gear token. The single atomic BuildSnapshot call is logged as
    // "[REBIRTH NPC Equipment UI] model preview ms=" for the profiler batch; set false to fall back to the placeholder.
    internal static bool ModelPreviewEnabled=true;
    private RebirthCharacterModelBinder modelBinder;
    private long modelStageMs;
    // One bounded profiler line per open: "[REBIRTH NPC Equipment] model ms: snapshot= prepare= finish= bind= firstPaint= mode=".
    private float openAt=-1f,snapshotAt=-1f;private int summaryFrame;private string summaryMode;private long summaryBindMs;private bool summaryDone=true;
    private void ScheduleModelSummary(string mode,long bindMs){if(summaryDone)return;summaryMode=mode;summaryBindMs=bindMs;summaryFrame=Time.frameCount+1;}
    private void EmitModelSummary()
    {
        if(summaryDone||summaryFrame==0||Time.frameCount<summaryFrame||npc==null)return;
        summaryDone=true;summaryFrame=0;
        float now=Time.realtimeSinceStartup;
        Log.Out("[REBIRTH NPC Equipment] model ms: snapshot="+(snapshotAt<0f||openAt<0f?-1:(int)((snapshotAt-openAt)*1000f))+
            " prepare="+(modelBinder?.LastPrepareMs??0)+" finish="+(modelBinder?.LastFinishMs??0)+" bind="+summaryBindMs+
            " firstPaint="+(openAt<0f?-1:(int)((now-openAt)*1000f))+" mode="+summaryMode+" entity="+npc.entityId);
    }
    private bool modelShown;private int modelDueFrame,modelAttempts;private string modelToken,gearToken,failedToken;
    internal bool Busy=>pending||retained!=null||queuedTarget>=0;
    internal bool RequestPending=>pending;
    public override void Init()
    {
        base.Init();gear=new XUiController[10];
        RebirthHumanEquipmentCellClickPatch.Install();
        // Gear and cargo are real XUiC_ItemStack controls; their native click callback (HandleClickComplete) is the
        // only adapter into the saved exchange. Index 10 of gearSlots is the Lockpicks control (logical 179).
        for(int i=0;i<gear.Length;i++){gear[i]=GetChildById("npcEquipSlot"+i);if(gear[i] is XUiC_RebirthHumanEquipmentGearSlot g){g.Owner=this;g.Logical=i;}}
        lockpickControl=GetChildById("npcEquipSlot179") as XUiC_RebirthHumanEquipmentGearSlot;
        if(lockpickControl!=null){lockpickControl.Owner=this;lockpickControl.Logical=RebirthHumanEquipment.LockpicksSlot;}
        cells=new XUiC_RebirthHumanEquipmentPlayerSlot[52];
        for(int i=0;i<cells.Length;i++){cells[i]=GetChildById("npcEquipPlayer"+i) as XUiC_RebirthHumanEquipmentPlayerSlot;if(cells[i]!=null)cells[i].Owner=this;}
        feedback=Label("npcEquipFeedback");title=Label("npcEquipName");inspectName=Label("npcEquipInspectName");inspectDescription=Label("npcEquipInspectDescription");inspectStats=Label("npcEquipInspectStats");inspectCount=Label("npcEquipInspectCount");inspectSummary=Label("npcEquipInspectSummary");capacity=Label("npcEquipCapacity");
        inspectIcon=GetChildById("npcEquipInspectIcon")?.ViewComponent as XUiV_Sprite;
        inspectNative=GetChildById("npcEquipSelectedNative") as XUiC_RebirthReadonlyItemSlot;
        for(int i=0;i<inspectStatNames.Length;i++){inspectStatNames[i]=Label("npcEquipInspectStatName"+i);inspectStatValues[i]=Label("npcEquipInspectStatValue"+i);}
        // The existing Character model binder creates and reads back a fresh SDCS rig
        // synchronously. NPC equipment must remain responsive while the same native
        // inventory and item inspectors are available; show the authored placeholder.
        SetActive(GetChildById("npcEquipModel"),false);
        SetActive(GetChildById("npcEquipAppearancePlaceholder"),true);
        if(ModelPreviewEnabled){modelBinder=new RebirthCharacterModelBinder(GetChildById("npcEquipModel"),true,"[REBIRTH NPC Equipment]");modelBinder.LogStages=false;}
        // No Equip/Unequip/Modify substitute buttons: gear moves only by native drag/drop and shift-click.
        Wire("npcEquipSort",()=>{if(!Busy&&xui.DragAndDropWindow.IsEmpty()){xui.playerUI.entityPlayer.bag.ItemGrid.Sort();RefreshCells();}});
        var area=GetChildById("npcEquipPlayerGrid");if(area!=null)area.OnScroll+=(s,d)=>{if(!Busy){offset=Math.Max(0,Math.Min(Math.Max(0,(xui.playerUI.entityPlayer.bag.SlotCount-1)/13-3),offset+(d<0?1:-1)));RefreshCells();}};
        for(int i=0;i<CargoPool;i++){if(GetChildById("npcEquipNativeCargo"+i) is XUiC_RebirthHumanEquipmentCargoSlot c)c.Owner=this;}
        ApplyCellInput();
        dragPreview=GetChildById("npcEquipDragPreview") as XUiC_RebirthReadonlyItemSlot;
        SetActive(dragPreview,false);
        var cargo=GetChildById("npcEquipStorageGrid");if(cargo!=null)cargo.OnScroll+=(s,d)=>{if(!Busy){storageOffset=Math.Max(0,Math.Min(Math.Max(0,(bagCapacity-1)/13-8),storageOffset+(d<0?1:-1)));RefreshStorage();}};
        for(int i=0;i<equipped.Length;i++)equipped[i]=ItemStack.Empty.Clone();
    }
    // Resolved once per presentation (null is cached too); Present clears it so a reopened tree is searched again.
    private XUiC_ItemInfoWindow infoWindow;private bool infoResolved;
    // The shared native ItemInfoWindow (itemInfoPanel, incl. REBIRTH quick-stack tabs) that belongs to THIS window group.
    // A window of another group (backpack) would be invisible here, so only the local one is used.
    private XUiC_ItemInfoWindow ItemInfo{get{if(!infoResolved){infoWindow=windowGroup?.Controller?.GetChildByType<XUiC_ItemInfoWindow>();infoResolved=true;}return infoWindow;}}
    // Shared pane in use: needs the window in the companion group and the action-suppression patch.
    // Otherwise the legacy hand-built rows remain visible as a temporary fallback.
    private bool SharedInfo=>ItemInfo!=null&&RebirthHumanEquipmentInfoActionsPatch.Healthy;
    private XUiC_ItemStack selectedNpcCell;private int selectedNpcSlot=-1;
    private void HideSharedInfo()
    {
        selectedNpcCell=null;selectedNpcSlot=-1;
        var info=infoResolved?infoWindow:null;
        if(info!=null&&SharedInfo){info.ClearSelectedStacks();if(info.ViewComponent!=null)info.ViewComponent.IsVisible=false;}
    }
    // Place the shared pane over the selected-item card (window and panel offsets are static in the XML).
    private void ApplySharedInfoLayout()
    {
        bool shared=SharedInfo;
        SetActive(GetChildById("npcEquipSelected"),!shared);
        var info=ItemInfo;var host=GetParentByType<XUiC_RebirthCompanions>();
        if(!shared||info?.ViewComponent==null||host?.ViewComponent==null)return;
        var origin=host.ViewComponent.Position;
        info.ViewComponent.Position=new Vector2i(origin.x+368+752+56,origin.y-120-48);
        info.ViewComponent.TryUpdatePosition();
    }
    // Gear/cargo cells take input only when the native-cancel patch is healthy; otherwise they are display only.
    private void ApplyCellInput()
    {
        bool on=RebirthHumanEquipmentCellClickPatch.Healthy;
        foreach(var c in gear)if(c?.ViewComponent!=null)c.ViewComponent.EventOnPress=on;
        if(lockpickControl?.ViewComponent!=null)lockpickControl.ViewComponent.EventOnPress=on;
        for(int i=0;i<CargoPool;i++){var cell=GetChildById("npcEquipNativeCargo"+i);if(cell?.ViewComponent!=null)cell.ViewComponent.EventOnPress=on;}
    }
    private XUiV_Label Label(string name)=>GetChildById(name)?.ViewComponent as XUiV_Label;
    private void Wire(string id,Action action){var c=GetChildById(id);if(c!=null)c.OnPress+=(s,b)=>{if(b==0||b==-1)action();};}
    internal bool NativeStorageFor(RebirthCompanionListEntry entry)=>nativeStorage&&npc!=null&&entry?.EntityId==npc.entityId;
    internal void Present(RebirthCompanionListEntry entry,bool show,bool cargo=false)
    {
        bool wasVisible=visible, modeChanged=storageMode!=cargo;
        if(show&&!wasVisible){infoResolved=false;ApplySharedInfoLayout();openAt=Time.realtimeSinceStartup;snapshotAt=-1f;summaryDone=false;summaryFrame=0;}
        storageMode=cargo;
        visible=show;ViewComponent.IsVisible=show;
        if(!show){CancelNpcDrag();HideSharedInfo();TearDownModelPreview();dataReady=false;nextRequest=0f;if(ReferenceEquals(active,this))active=null;return;}
        active=this;
        var found=xui.playerUI.entityPlayer.world.GetEntity(entry?.EntityId??-1) as EntityRebirthHumanoidNPC;
        if(wasVisible&&!modeChanged&&ReferenceEquals(found,npc))
        {Set(title,entry?.Name??npc?.EntityName);return;}
        if(modeChanged){TearDownModelPreview();dataReady=false;nextRequest=0f;}
        if(found!=npc)
        {
            if(Busy)return;
            TearDownModelPreview(true);dataReady=false;nativeStorage=false;legacyFree=false;bagCapacity=0;Array.Clear(storage,0,storage.Length);for(int i=0;i<equipped.Length;i++)equipped[i]=ItemStack.Empty.Clone();lockpicks=ItemStack.Empty.Clone();npc=found;stable=found?.RebirthRuntimeState?.StableId.ToString();request=null;retained=null;selectedGear=selectedSource=-1;selectedNpcCell=null;selectedNpcSlot=-1;nextRequest=0;
        }
        if(npc==null||!RebirthHumanEquipment.Human(npc))
        {Set(feedback,"xuiRbNpcEquipmentHumanOnly",true);foreach(var c in gear)SetActive(c,false);foreach(var c in cells)SetActive(c,false);return;}
        SetActive(GetChildById("npcEquipCharacter"),!cargo);SetActive(GetChildById("npcEquipLoadout"),!cargo);SetActive(GetChildById("npcEquipStorage"),cargo);
        SetActive(GetChildById("npcEquipHeaderEquipment"),!cargo);SetActive(GetChildById("npcEquipHeaderInventory"),cargo);
        SetActive(GetChildById("npcEquipApply"),!cargo);
        SetActive(GetChildById("npcEquipRemove"),!cargo);
        Set(title,entry?.Name??npc.EntityName);RefreshCells();RefreshStorage();
    }
    public override void Update(float dt)
    {
        base.Update(dt);if(!visible||npc==null||!ReferenceEquals(active,this))return;
        var player=xui.playerUI.entityPlayer;
        if(pending&&Time.realtimeSinceStartup>=requestAt+5f)pending=false;
        if(player.world!=npc.world||npc.IsDead()||!ReferenceEquals(player.world.GetEntity(npc.entityId),npc))
        {CancelNpcDrag();pending=false;queuedTarget=-1;retained=null;Set(feedback,"xuiRbNpcEquipmentUnavailable",true);return;}
        UpdateNpcDrag();
        EmitModelSummary();
        UpdateModelPreview();
        if((Busy||!dataReady)&&Time.realtimeSinceStartup>=nextRequest)
        {
            // A full native bag snapshot is expensive on the listen server. Read once
            // on presentation and then only while a durable exchange is settling.
            nextRequest=Time.realtimeSinceStartup+0.75f;
            if(!pending)
            {
                if(queuedTarget>=0)
                {
                    if(queuedSource>=player.bag.SlotCount||RebirthHumanEquipment.Frame(player.bag.ItemGrid[queuedSource])!=queuedFrame){queuedTarget=-1;Set(feedback,"xuiRbNpcEquipmentConflict",true);}
                    else Send(1,queuedTarget,queuedSource,queuedGuard);
                }
                else Send(retained==null?0:2,-1,-1,retained==null?"":(string)retained.Attribute("id"));
            }
            else if(Time.realtimeSinceStartup>=requestAt+5f){pending=false;}
            RefreshCells();
        }
    }
    // Bind only after the tab is shown and a snapshot arrived, >=30 frames later, on a settled frame, once per
    // NPC and worn-gear token (armor 0-3 and backpack 9). Never in the open frame.
    private void UpdateModelPreview()
    {
        if(modelBinder==null||storageMode||!dataReady||Busy||npc==null)return;
        // Token is rebuilt only after a snapshot reply (AcceptReply nulls it), not every frame.
        if(gearToken==null)gearToken=npc.entityId+"|"+RebirthHumanEquipment.Frame(equipped[0])+RebirthHumanEquipment.Frame(equipped[1])+
            RebirthHumanEquipment.Frame(equipped[2])+RebirthHumanEquipment.Frame(equipped[3])+RebirthHumanEquipment.Frame(equipped[9]);
        string token=gearToken;
        if(token==modelToken&&!modelShown)
        {
            // Reopen of the same NPC and gear: the binder kept the finished image (Suspend), so Bind(false) only
            // reattaches it. If it cannot, fall back to the normal deferred build below.
            if(modelBinder.Bind(npc,false)){modelShown=true;ScheduleModelSummary("reattach",0);SetActive(GetChildById("npcEquipAppearancePlaceholder"),false);SetActive(GetChildById("npcEquipModel"),true);}
            else modelToken=null;
            return;
        }
        if(token==modelToken||(token==failedToken&&modelAttempts>=3))return;
        // A finished image of this NPC and appearance (e.g. the Companion Overview portrait) is reused without any build.
        if(modelDueFrame==0&&!modelBinder.IsStaging&&modelBinder.TryAttachCached(npc))
        {
            modelToken=token;modelShown=true;failedToken=null;modelAttempts=0;modelDueFrame=0;ScheduleModelSummary("cache-hit",0);
            SetActive(GetChildById("npcEquipAppearancePlaceholder"),false);SetActive(GetChildById("npcEquipModel"),true);
            return;
        }
        // Second half of a staged build (rig prepared on an earlier frame): finish now, no settle gate, so the
        // prepared rig does not linger across frames.
        if(!modelBinder.IsStaging)
        {
            if(modelDueFrame==0){modelDueFrame=Time.frameCount+30;return;}
            if(Time.frameCount<modelDueFrame)return;
            // Prefer a settled frame, but never wait forever on a slow client.
            if(Time.unscaledDeltaTime>0.05f&&Time.frameCount<modelDueFrame+60)return;
        }
        var timer=System.Diagnostics.Stopwatch.StartNew();
        // Staged: frame A creates the SDCS rig and frames the camera, frame B renders and reads back, so the two heavy
        // halves never share a frame. The binder returns true only when the finished image was installed.
        bool ok=modelBinder.BindStaged(npc);
        timer.Stop();
        modelStageMs+=timer.ElapsedMilliseconds;
        if(!ok&&modelBinder.IsStaging)return;
        modelDueFrame=0;
        long totalMs=modelStageMs;modelStageMs=0;
        if(ok)
        {
            modelToken=token;modelShown=true;failedToken=null;modelAttempts=0;ScheduleModelSummary("built",totalMs);
            SetActive(GetChildById("npcEquipAppearancePlaceholder"),false);SetActive(GetChildById("npcEquipModel"),true);
        }
        else
        {
            if(failedToken!=token){failedToken=token;modelAttempts=0;}
            // One log line per NPC+token; at most 3 attempts, then the placeholder stays until the gear token changes.
            if(modelAttempts++==0)Log.Out("[REBIRTH NPC Equipment UI] model preview ms="+totalMs+" ok=False entity="+npc.entityId);
        }
    }
    // dispose=false (tab hide / mode change): Suspend keeps the finished image for a cheap reopen of the same NPC and gear.
    // dispose=true (NPC change / shutdown): Clear destroys it.
    private void TearDownModelPreview(bool dispose=false)
    {
        if(dispose){modelBinder?.Clear();modelToken=null;}else modelBinder?.Suspend();
        modelShown=false;modelDueFrame=0;modelStageMs=0;gearToken=null;failedToken=null;modelAttempts=0;
        SetActive(GetChildById("npcEquipModel"),false);SetActive(GetChildById("npcEquipAppearancePlaceholder"),true);
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
        if(root.Attribute("error")!=null){string error=(string)root.Attribute("error");bool wasSwap=queuedTarget>=0;if(error!="xuiRbNpcEquipmentWaitSave")queuedTarget=-1;
            // A failing initial read must not re-poll the full snapshot every 0.75 s.
            if(!dataReady)nextRequest=Time.realtimeSinceStartup+4f;
            // The displayed cell no longer matches the server: drop the cache so the next Update refetches
            // instead of re-sending the same stale expected frame.
            // Never write 0: Handle maps any exception to Conflict, so a persistent read failure must stay backed off.
            if(error=="xuiRbNpcEquipmentStale"||error=="xuiRbNpcEquipmentConflict"){dataReady=false;nextRequest=Time.realtimeSinceStartup+(wasSwap?0.1f:4f);}
            Set(feedback,error,true);return;}
        if((int?)root.Attribute("entity")!=npc.entityId||(string)root.Attribute("stable")!=stable)return;
        foreach(var node in root.Elements("slot"))
        {
            int i=(int)node.Attribute("id");
            if(i==RebirthHumanEquipment.LockpicksSlot){lockpicks=RebirthHumanEquipment.Decode(node.Value);continue;}
            if(i<0||i>=equipped.Length)throw new InvalidOperationException();
            equipped[i]=RebirthHumanEquipment.Decode(node.Value);
        }
        queuedTarget=-1;var previousPlan=retained;retained=root.Element("swap");
        nativeStorage=(int?)root.Attribute("native")==1&&(int?)root.Attribute("legacy")==0;
        legacyFree=(int?)root.Attribute("legacy")==0;
        if((int?)root.Attribute("native")==1&&npc.world.IsRemote())npc.Buffs.SetCustomVar(RebirthHumanEquipment.NativeMode,1f,false);
        foreach(var row in root.Element("storage")?.Elements("cell")??Enumerable.Empty<XElement>())
        {int index=(int)row.Attribute("id");if(index<0||index>=169)throw new InvalidOperationException();storage[index]=RebirthHumanEquipment.Decode(row.Value);}
        dataReady=true;gearToken=null;if(snapshotAt<0f)snapshotAt=Time.realtimeSinceStartup;holdingIndex=(int?)root.Attribute("holding")??-1;bagCapacity=(int?)root.Attribute("capacity")??0;
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
            Set(feedback,"xuiRbNpcEquipmentHint",true);
            if(modifySource>=0){selectedSource=modifySource;modifySource=-1;inspectingGear=false;Modify();}
        }
        RefreshEquipment();RefreshCells();RefreshStorage();
        // The shared pane reads the proxy cell's stack; refresh it after the snapshot replaced the display.
        if(selectedNpcCell!=null&&selectedNpcSlot>=0&&SharedInfo)Inspect(Displayed(selectedNpcSlot),true,selectedNpcCell);
    }
    private void Begin(int slot,int source)
    {
        if(!dataReady||Busy||OtherOwnerHold(xui.playerUI.entityPlayer)||npc==null||source<0||source>=xui.playerUI.entityPlayer.bag.SlotCount||!xui.DragAndDropWindow.IsEmpty())return;
        var stack=xui.playerUI.entityPlayer.bag.ItemGrid[source];if(!RebirthHumanEquipment.Accept(slot,stack)){Set(feedback,"xuiRbNpcEquipmentWrongItem",true);return;}
        queuedTarget=slot;queuedSource=source;queuedFrame=RebirthHumanEquipment.Frame(stack);
        // Compare-and-swap: the server rejects the swap if the NPC cell no longer holds what this window displayed.
        queuedGuard=RebirthP2NpcEquipmentSlots.GuardedOperation(stack,Displayed(slot));
        GameManager.Instance.SaveLocalPlayerData();Send(1,slot,source,queuedGuard);
    }
    private ItemStack Displayed(int slot)
    {
        var shown=slot==RebirthHumanEquipment.LockpicksSlot?lockpicks:slot>=0&&slot<10?equipped[slot]:slot>=10&&slot-10<storage.Length?storage[slot-10]:null;
        return shown??ItemStack.Empty.Clone();
    }
    // Native click callback of a gear/cargo XUiC_ItemStack (cursor drop is claimed by RebirthNativeInventoryPointerDrop.Arm
    // and arrives here too). The proxy cell never swaps the cursor itself, so nothing is cloned or duplicated before
    // the saved exchange commits; the display is refreshed from the next snapshot.
    internal void NpcGestureCancelled(){Set(feedback,"xuiRbNpcEquipmentHint",true);}
    // Native drag start is XUiC_ItemStack.Update: press held + pointer moved past PickupSnapDistance on a filled cell
    // with an empty cursor -> SwapItem (left). The patch routes that call here instead of letting it clone the NPC
    // stack onto the player cursor.
    internal void NpcDragStarted(XUiC_ItemStack cell)
    {
        int slot=cell is XUiC_RebirthHumanEquipmentGearSlot g?g.Logical:10+cell.SlotNumber;
        var player=xui?.playerUI?.entityPlayer as EntityPlayerLocal;var pointer=xui?.playerUI?.CursorController;
        if(player==null||pointer==null||draggingNpc>=0||!dataReady||Busy||npc==null||!xui.DragAndDropWindow.IsEmpty()){NpcGestureCancelled();return;}
        var stack=Displayed(slot);if(stack.IsEmpty())return;
        RebirthInventoryGestureTrace.Write(cell,"npcEquip.dragStart."+slot,false);
        if(!pointer.GetMouseButton(UICamera.MouseButton.LeftButton))
        {
            // Gamepad pickup has no pointer release to route: behave like shift-click to the first free cell.
            selectedGear=slot;Inspect(stack,true,cell);Unequip(slot);return;
        }
        draggingNpc=slot;draggingFrame=RebirthHumanEquipment.Frame(stack);draggingPlayer=player;draggingWorld=player.world;dragReleaseSeen=-1;
        selectedGear=slot;Inspect(stack,true,cell);
        dragPreview?.Show(stack);SetActive(dragPreview,true);
    }
    private void CancelNpcDrag()
    {
        draggingNpc=-1;draggingFrame=null;draggingPlayer=null;draggingWorld=null;dragReleaseSeen=-1;
        SetActive(dragPreview,false);
    }
    private void UpdateNpcDrag()
    {
        if(draggingNpc<0)return;
        var pointer=xui?.playerUI?.CursorController;if(pointer==null){CancelNpcDrag();return;}
        if(dragPreview?.ViewComponent?.UiTransform!=null&&ViewComponent?.UiTransform!=null)
        {
            var ray=xui.playerUI.camera.ScreenPointToRay(pointer.GetScreenPosition());
            var plane=new Plane(ViewComponent.UiTransform.forward,ViewComponent.UiTransform.position);
            if(plane.Raycast(ray,out float distance))
            {
                var local=ViewComponent.UiTransform.InverseTransformPoint(ray.GetPoint(distance));
                dragPreview.ViewComponent.Position=new Vector2i((int)local.x,(int)local.y);
                dragPreview.ViewComponent.TryUpdatePosition();
            }
        }
        // Player cells claim the release on the frame the button comes up; the drag is dropped one frame later.
        if(pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton)||!pointer.GetMouseButton(UICamera.MouseButton.LeftButton))
        {if(dragReleaseSeen<0)dragReleaseSeen=Time.frameCount;else if(Time.frameCount>dragReleaseSeen)CancelNpcDrag();}
    }
    // Called from the player cell's Update (same native hover/collider test as RebirthNativeInventoryPointerDrop.Arm).
    internal void NpcDragRelease(XUiC_RebirthHumanEquipmentPlayerSlot cell)
    {
        if(draggingNpc<0)return;
        var ui=xui;var pointer=ui?.playerUI?.CursorController;
        if(pointer==null||!pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton))return;
        var view=cell.ViewComponent;var collider=view?.UiTransform?.GetComponent<BoxCollider>();var camera=ui.playerUI.camera;
        if(view?.IsVisible!=true||!view.Enabled||!cell.isOver||!view.UiTransformIsHovered||!collider||!collider.enabled||
            !collider.gameObject.activeInHierarchy||!camera||!collider.Raycast(camera.ScreenPointToRay(pointer.GetScreenPosition()),out _,10000f))return;
        int target=draggingNpc;string frame=draggingFrame;
        bool same=ReferenceEquals(draggingPlayer,ui.playerUI.entityPlayer)&&ReferenceEquals(draggingWorld,draggingPlayer?.world)&&
            ReferenceEquals(draggingWorld,GameManager.Instance?.World)&&RebirthHumanEquipment.Frame(Displayed(target))==frame;
        int destination=cell.SlotNumber;
        CancelNpcDrag();
        RebirthInventoryGestureTrace.Write(cell,"npcEquip.dragDrop."+target+">"+destination,false);
        if(!same||destination<0||destination>=ui.playerUI.entityPlayer.bag.SlotCount){Set(feedback,"xuiRbNpcEquipmentConflict",true);return;}
        // Exact chosen bag cell: an empty cell receives the NPC item; an occupied cell the NPC slot accepts is swapped
        // by the same guarded exchange (Begin checks Accept for the occupant).
        Begin(target,destination);
    }
    internal void NpcCellClicked(XUiC_ItemStack cell,bool shiftMove=false)
    {
        // A release over an NPC cell during an NPC-origin drag (including the source) is not a click.
        if(draggingNpc>=0){NpcGestureCancelled();return;}
        // A shift-click while holding a stack must not act as a drop.
        if(shiftMove&&!xui.DragAndDropWindow.IsEmpty()){NpcGestureCancelled();return;}
        int slot=cell is XUiC_RebirthHumanEquipmentGearSlot g?g.Logical:10+cell.SlotNumber;
        RebirthInventoryGestureTrace.Write(cell,"npcEquip.click."+slot,false);
        Interact(slot,cell);
    }
    private void Interact(int slot,XUiC_ItemStack cell)
    {
        if(!dataReady||Busy||npc==null)return;selectedGear=slot;var cursor=xui.DragAndDropWindow;var stack=cursor.CurrentStack;
        if(!stack.IsEmpty())
        {
            if(OtherOwnerHold(xui.playerUI.entityPlayer)){Set(feedback,"xuiRbNpcEquipmentBusy",true);return;}
            if(!RebirthHumanEquipment.Accept(slot,stack)){Set(feedback,"xuiRbNpcEquipmentWrongItem",true);return;}
            var items=xui.playerUI.entityPlayer.bag.ItemGrid.items;
            int source=Array.FindIndex(items,s=>s==null||s.IsEmpty());
            if(source<0||!stack.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack,source)){Set(feedback,"xuiRbNpcEquipmentPlayerFull",true);return;}
            var bag=xui.playerUI.entityPlayer.bag;
            bag.SetSlot(source,stack.Clone());
            if(RebirthHumanEquipment.Frame(bag.ItemGrid[source])!=RebirthHumanEquipment.Frame(stack))
            {bag.SetSlot(source,ItemStack.Empty.Clone());Set(feedback,"xuiRbNpcEquipmentConflict",true);return;}
            // The held native cursor and backpack cell must not both be present
            // when the player preimage is saved. Clear the cursor first, verify it,
            // and only then publish the bag cell consumed by the server exchange.
            cursor.SetCurrentStack(ItemStack.Empty.Clone(),false);
            if(!cursor.CurrentStack.IsEmpty())
            {bag.SetSlot(source,ItemStack.Empty.Clone());Set(feedback,"xuiRbNpcEquipmentConflict",true);return;}
            // Begin saves the player preimage once, after the cursor is verified empty.
            Begin(slot,source);return;
        }
        selectedNpcSlot=slot;
        Inspect(Displayed(slot),true,cell);
        if(cell!=null&&!Displayed(slot).IsEmpty())cell.IsSelected=true;
        // NPC -> player: shift-click moves the item to the first free bag cell; a drag (NpcDragStarted ... NpcDragRelease)
        // targets a chosen bag cell through a visual ghost, never a cloned stack on the real player cursor.
        if(InputUtils.ShiftKeyPressed&&!Displayed(slot).IsEmpty())Unequip(slot);
    }
    private void Unequip(int slot)
    {
        var items=xui.playerUI.entityPlayer.bag.ItemGrid.items;int source=Array.FindIndex(items,s=>s==null||s.IsEmpty());
        if(source<0){Set(feedback,"xuiRbNpcEquipmentPlayerFull",true);return;}Begin(slot,source);
    }
    internal void SelectPlayer(XUiC_RebirthHumanEquipmentPlayerSlot slot)
    {
        if(Busy)return;
        var held=xui.DragAndDropWindow;
        var selection=held!=null&&!held.IsEmpty()?held.ItemStackControl:slot;
        selectedSource=ReferenceEquals(selection,slot)?slot.SlotNumber:-1;
        Inspect(selection?.ItemStack,false,selection);
    }
    internal bool TryShiftPlayer(XUiC_RebirthHumanEquipmentPlayerSlot slot)
    {
        if(!InputUtils.ShiftKeyPressed||slot.ItemStack.IsEmpty())return false;
        // This view owns the backpack gesture. Never let the native preferred-location move
        // run while its guarded NPC exchange is waiting or has no usable snapshot.
        if(Busy||!dataReady){Set(feedback,"xuiRbNpcEquipmentWaitSave",true);return true;}
        if(!storageMode)
        {
            // A never-native NPC (native=0) is accepted by Handle on its first write exactly like drag/drop;
            // only a legacy custody inventory blocks. Never fall through to the vanilla shift-click.
            if(!legacyFree){Set(feedback,"xuiRbNpcEquipmentUnavailable",true);return true;}
            // Equipment view: choose the gear slot from the item, then use the same guarded swap as drag/drop.
            int gear=GearSlotFor(slot.ItemStack);
            selectedSource=slot.SlotNumber;Inspect(slot.ItemStack,false,slot);
            RebirthInventoryGestureTrace.Write(slot,"npcEquip.shiftToGear."+gear,false);
            if(gear<0){Set(feedback,"xuiRbNpcEquipmentWrongItem",true);return true;}
            Begin(gear,selectedSource);
            return true;
        }
        if(!nativeStorage){Set(feedback,"xuiRbNpcEquipmentUnavailable",true);return true;}
        int target=Array.FindIndex(storage,0,bagCapacity,s=>s==null||s.IsEmpty());
        if(target<0){Set(feedback,"xuiRbNpcEquipmentOverflow",true);return true;}
        selectedSource=slot.SlotNumber;Inspect(slot.ItemStack,false,slot);
        RebirthInventoryGestureTrace.Write(slot,"npcEquip.shiftToCargo."+target,false);
        Begin(10+target,selectedSource);
        return true;
    }
    // First logical gear slot the server's Accept allows; ammo prefers an empty ammo cell.
    private int GearSlotFor(ItemStack stack)
    {
        foreach(int s in new[]{0,1,2,3,9,4,5,RebirthHumanEquipment.LockpicksSlot})
            if(RebirthHumanEquipment.Accept(s,stack))return s;
        int fallback=-1;
        for(int s=6;s<=8;s++)
        {
            if(!RebirthHumanEquipment.Accept(s,stack))continue;
            if(fallback<0)fallback=s;
            if(equipped[s]==null||equipped[s].IsEmpty())return s;
        }
        return fallback;
    }
    private void Inspect(ItemStack stack,bool gearItem,XUiC_ItemStack source=null)
    {
        inspected=stack?.Clone()??ItemStack.Empty.Clone();inspectingGear=gearItem;
        if(gearItem&&source!=null){selectedNpcCell=source;}else if(!gearItem)selectedNpcCell=null;
        if(SharedInfo)
        {
            // One shared selected-item pane for NPC gear, NPC cargo and the player backpack. NPC proxy cells carry
            // the displayed stack; their Drop/Scrap/Take/Wear actions are removed by RebirthHumanEquipmentInfoActionsPatch.
            var shared=ItemInfo;
            if(source!=null&&!inspected.IsEmpty())shared.SetItemStack(source,true);
            else{shared.ClearSelectedStacks();if(shared.ViewComponent!=null)shared.ViewComponent.IsVisible=false;}
            return;
        }
        inspectNative?.Show(inspected,source);
        var nativeSource=source??inspectNative;
        // Fallback only (no shared pane available in this window group): hand-built read-only rows below.
        var itemInfo=gearItem?null:ItemInfo;
        if(itemInfo!=null&&nativeSource!=null&&!inspected.IsEmpty())itemInfo.SetItemStack(nativeSource,true);
        if(inspectIcon!=null)
        {
            if(inspected.IsEmpty())inspectIcon.SpriteName="";
            else
            {
                var value=inspected.itemValue;var item=value.ItemClassOrMissing;
                inspectIcon.SpriteName=value.GetPropertyOverride(ItemClass.PropCustomIcon,item.GetIconName());
                inspectIcon.Color=item.GetIconTint(value);
            }
        }
        Set(inspectCount,inspected.IsEmpty()?"":inspected.count.ToString());
        Set(inspectName,inspected.IsEmpty()?"":Localization.Get(inspected.itemValue.ItemClass.GetItemName()));
        Set(inspectDescription,inspected.IsEmpty()?"":XUiC_RebirthCraftingItemContext.ResolveDescription(inspected,xui));
        Set(inspectSummary,inspected.IsEmpty()?"":XUiC_RebirthCraftingItemContext.BuildSummary(inspected));
        string[] names=new string[inspectStatNames.Length],values=new string[inspectStatValues.Length];
        RebirthSelectedItemRows.Build(xui,inspected,null,null,names.Length,names,values);
        for(int i=0;i<names.Length;i++){Set(inspectStatNames[i],names[i]);Set(inspectStatValues[i],values[i]);}
        RebirthSelectedDurability.Render(this,"npcEquipInspectDurability",nativeSource);
        Set(inspectStats,"");
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
            var c=gear[i];SetActive(c,true);
            if(c is XUiC_ItemStack stackControl)stackControl.ItemStack=equipped[i].Clone();
            if(i>=4)Set(Label("npcEquipRowStatus"+i),RowStatus(equipped[i],i==5));
        }
        if(lockpickControl!=null){SetActive(lockpickControl,true);lockpickControl.ItemStack=lockpicks.Clone();}
        Set(Label("npcEquipRowStatus"+RebirthHumanEquipment.LockpicksSlot),RowStatus(lockpicks,false));
    }
    // Weapons & pack rows: item name (and count / loaded ammo / in-use) or the localized "Empty".
    private string RowStatus(ItemStack stack,bool ranged)
    {
        if(stack==null||stack.IsEmpty())return Localization.Get("xuiRbNpcEquipmentSlotEmpty");
        string text=Localization.Get(stack.itemValue.ItemClass.GetItemName());
        if(stack.count>1)text+=" x"+stack.count;
        if(ranged)text+="\n"+string.Format(Localization.Get("xuiRbNpcEquipmentLoaded"),stack.itemValue.Meta)+(holdingIndex==1?" - "+Localization.Get("xuiRbNpcEquipmentInUse"):"");
        return text;
    }
    // Shared conditional scrollbar: track and thumb are visible only when content rows exceed the viewport rows.
    private void RenderGridScroll(string trackId,string thumbId,int left,int heightPx,int viewportRows,int totalRows,int offsetRows)
    {
        bool show=totalRows>viewportRows;
        SetActive(GetChildById(trackId),show);
        RebirthScrollbarPresentation.RenderThumb(GetChildById(thumbId)?.ViewComponent,heightPx,viewportRows,totalRows,offsetRows,6,left,-58);
    }
    private void RefreshStorage()
    {
        // Equipment mode hides the entire cargo host. Its 117 native slots are
        // refreshed on entry to Inventory, so avoid cloning them during Equipment open/replies.
        if(!storageMode)return;
        Set(Label("npcEquipNpcStorageCapacity"),string.Format(Localization.Get("xuiRbNpcEquipmentStorageSummary"),storage.Take(bagCapacity).Count(s=>s!=null&&!s.IsEmpty()),bagCapacity));
        for(int i=0;i<CargoPool;i++)
        {
            var c=GetChildById("npcEquipCargo"+i);if(c==null)continue;int index=storageOffset*13+i;SetActive(c,storageMode&&index<bagCapacity);
            var stack=index<storage.Length?storage[index]:null;
            var native=c.GetChildById("npcEquipNativeCargo"+i) as XUiC_RebirthHumanEquipmentCargoSlot;
            if(native!=null)
            {
                native.SlotNumber=index;
                native.ItemStack=stack?.Clone()??ItemStack.Empty.Clone();
                // Never the native info window: its action list (Drop/Scrap) must not exist on an NPC proxy cell.
                native.InfoWindow=null;
            }
        }
        RenderGridScroll("npcEquipStorageTrack","npcEquipStorageThumb",722,576,9,(bagCapacity+12)/13,storageOffset);
    }
    private void RefreshCells()
    {
        if(cells==null)return;syncing=true;
        try{var p=xui.playerUI.entityPlayer;for(int i=0;i<cells.Length;i++)if(cells[i]!=null){int index=offset*13+i;bool show=index<p.bag.SlotCount;SetActive(cells[i],show);if(show&&!cells[i].AssembleLock){cells[i].SlotNumber=index;cells[i].ItemStack=p.bag.ItemGrid[index].Clone();cells[i].InfoWindow=ItemInfo;cells[i].ViewComponent.EventOnPress=!Busy;cells[i].ViewComponent.EventOnDrag=!Busy;}}Set(capacity,string.Format(Localization.Get("xuiRbNpcEquipmentPlayerCapacity"),p.bag.GetUsedSlotCount(),p.bag.SlotCount));}
        finally{syncing=false;}
        RenderGridScroll("npcEquipPlayerTrack","npcEquipPlayerThumb",708,256,4,(xui.playerUI.entityPlayer.bag.SlotCount+12)/13,offset);
    }
    internal void PlayerChanged(int index,ItemStack stack){if(!syncing&&!Busy)xui.playerUI.entityPlayer.bag.SetSlot(index,stack.Clone());}
    private static bool OtherOwnerHold(EntityPlayerLocal p)=>p==null||RebirthGearOwnerReservation.IsHeld(p)||RebirthBackpackLibraryReservation.IsHeld(p)||RebirthCookingOutputDelivery.IsHeld(p)||
        RebirthLogisticsOwnerReceipt.IsHeld(p)||AdvancedFarmingHarvestOwnerDelta.IsHeld(p)||AdvancedFarmingWaterExchangeOriginal.IsHeld(p)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(p)||
        RebirthCamperEnergyOwnerHold.Blocks(p.bag)||RebirthCamperEnergyOwnerHold.Blocks(p.inventory)||RebirthCamperStationNativeActorPayment.Blocks(p.bag)||RebirthCamperStationNativeActorPayment.Blocks(p.inventory)||RebirthPlacedOriginal.IsHeld(p);
    private static void Set(XUiV_Label label,string text,bool localize=false){if(label!=null)label.Text=localize?Localization.Get(text):text;}
    private static void SetActive(XUiController c,bool show){if(c?.ViewComponent!=null)c.ViewComponent.IsVisible=show;}
    internal void Shutdown(){infoWindow=null;infoResolved=false;CancelNpcDrag();TearDownModelPreview(true);dataReady=false;visible=false;pending=false;retained=null;queuedTarget=-1;request=null;npc=null;if(ReferenceEquals(active,this))active=null;}
}
[Preserve]
public sealed class XUiC_RebirthHumanEquipmentPlayerSlot:XUiC_ItemStack
{
    internal XUiC_RebirthHumanEquipment Owner;
    public override void Init(){base.Init();StackLocation=StackLocationTypes.Backpack;SlotChangedEvent+=(i,s)=>Owner?.PlayerChanged(i,s);}
    // The release that ends an NPC-origin drag is not a click on this cell (it would also trigger a shift-equip).
    public override void HandleClickComplete(){if(Owner?.NpcDragActive==true)return;if(Owner?.TryShiftPlayer(this)==true)return;base.HandleClickComplete();Owner?.SelectPlayer(this);}
    public override void Update(float dt){if(Owner?.Busy==true)return;base.Update(dt);Owner?.NpcDragRelease(this);}
}
[Preserve]
public sealed class XUiC_RebirthHumanEquipmentCargoSlot:XUiC_ItemStack
{
    internal XUiC_RebirthHumanEquipment Owner;
    public override void Init()
    {
        base.Init();
        StackLocation=StackLocationTypes.LootContainer;
        // Native rendering, hover popup and click/drop callback; the click is routed to the saved
        // exchange adapter instead of base.HandleClickComplete (which would clone the NPC stack to the cursor).
        ViewComponent.EventOnDrag=false;
    }
    // Only reached from the native plain-click inspect (HandleItemInspect). Swap/pickup/move are cancelled by
    // RebirthHumanEquipmentCellClickPatch, so nothing is cloned or moved natively.
    public override void HandleClickComplete(){Owner?.NpcCellClicked(this);}
    public override void updateItemInfoWindow(XUiC_ItemStack stack){}
    public override void OnHovered(bool over)
    {
        RebirthCharacterItemStatsTooltip.Hover(this,over);
        base.OnHovered(over);
    }
}
// The native Update runs the real swap/pickup/shift-move BEFORE HandleClickComplete (which is only a callback at the
// end of HandleStackSwap/HandlePartialStackPickup/HandleDropOne/HandleItemInspect). For the NPC proxy cells those
// natives would clone the NPC stack to the cursor or the player's inventory, so they are cancelled here and the
// drop (HandleStackSwap) and shift-click (HandleMoveToPreferredLocation) are routed to the saved exchange instead.
[HarmonyPatch]
internal static class RebirthHumanEquipmentCellClickPatch
{
    private static readonly string[] Members={"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"};
    // False means the five natives could not all be cancelled: the NPC cells are then made inert (display only).
    internal static bool Healthy{get;private set;}
    private static bool attempted;
    internal static void Install()
    {
        if(attempted)return;
        attempted=true;
        try
        {
            foreach(string name in Members)
                if(AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes)==null)
                {Log.Error("[REBIRTH NPC Equipment UI] XUiC_ItemStack."+name+" not found; NPC gear/cargo cells are disabled to avoid item duplication.");return;}
            RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.npc.equipment.cells"),typeof(RebirthHumanEquipmentCellClickPatch));
            Healthy=true;
        }
        catch(Exception e){Log.Error("[REBIRTH NPC Equipment UI] cell patch failed ("+e.GetType().Name+"); NPC gear/cargo cells are disabled.");}
        RebirthHumanEquipmentInfoActionsPatch.Install();
    }
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in Members)
        {
            var method=AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes);
            if(method!=null)yield return method;
        }
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_ItemStack __instance,MethodBase __originalMethod)
    {
        // Native shift-move executes before HandleClickComplete. Route player cells here,
        // before the native inventory mutation, through the same saved exchange adapter.
        if(__instance is XUiC_RebirthHumanEquipmentPlayerSlot playerCell&&
            __originalMethod.Name=="HandleMoveToPreferredLocation"&&playerCell.Owner?.TryShiftPlayer(playerCell)==true)
        {__instance.lastClicked=false;return false;}
        XUiC_RebirthHumanEquipment owner;
        if(__instance is XUiC_RebirthHumanEquipmentGearSlot g)owner=g.Owner;
        else if(__instance is XUiC_RebirthHumanEquipmentCargoSlot c)owner=c.Owner;
        else return true;
        // Fail closed: an NPC cell never runs the native clone/swap, even if its owner was not wired.
        // Clear the native click latch so no later HandleClickComplete acts on this press.
        __instance.lastClicked=false;
        if(owner==null||!Healthy)return false;
        string name=__originalMethod.Name;
        if(name=="HandleStackSwap")owner.NpcCellClicked(__instance,false);
        else if(name=="HandleMoveToPreferredLocation")owner.NpcCellClicked(__instance,true);
        else if(name=="SwapItem")owner.NpcDragStarted(__instance);
        else owner.NpcGestureCancelled();
        return false;
    }
}
// The shared ItemInfoWindow builds Drop/Scrap/Take/Wear/Modify entries from the selected stack controller. NPC proxy
// cells are display-only, so for them the action list is cleared right after the native build (also on every dirty
// rebuild, because ItemInfoWindow.Update re-calls SetItemStack). Player backpack cells keep their full action list.
[HarmonyPatch]
internal static class RebirthHumanEquipmentInfoActionsPatch
{
    internal static bool Healthy{get;private set;}
    internal static void Install()
    {
        try
        {
            if(AccessTools.DeclaredMethod(typeof(XUiC_ItemActionList),nameof(XUiC_ItemActionList.SetCraftingActionList))==null)
            {Log.Error("[REBIRTH NPC Equipment UI] XUiC_ItemActionList.SetCraftingActionList not found; shared item info is disabled for NPC cells.");return;}
            RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.npc.equipment.infoactions"),typeof(RebirthHumanEquipmentInfoActionsPatch));
            Healthy=true;
        }
        catch(Exception e){Log.Error("[REBIRTH NPC Equipment UI] info action patch failed ("+e.GetType().Name+"); shared item info is disabled for NPC cells.");}
    }
    private static MethodBase TargetMethod()=>AccessTools.DeclaredMethod(typeof(XUiC_ItemActionList),nameof(XUiC_ItemActionList.SetCraftingActionList));
    private static void Postfix(XUiC_ItemActionList __instance,XUiC_ItemActionList.ItemActionListTypes _actionListType,XUiController itemController)
    {
        if(_actionListType!=XUiC_ItemActionList.ItemActionListTypes.Item)return;
        if(!(itemController is XUiC_RebirthHumanEquipmentGearSlot)&&!(itemController is XUiC_RebirthHumanEquipmentCargoSlot))return;
        __instance.SetCraftingActionList(XUiC_ItemActionList.ItemActionListTypes.None,null);
    }
}
[Preserve]
public sealed class XUiC_RebirthHumanEquipmentGearSlot:XUiC_ItemStack
{
    internal XUiC_RebirthHumanEquipment Owner;
    internal int Logical;
    public override void Init()
    {
        base.Init();
        StackLocation=StackLocationTypes.LootContainer;
        ViewComponent.EventOnDrag=false;
    }
    public override void HandleClickComplete(){Owner?.NpcCellClicked(this);}
    public override void updateItemInfoWindow(XUiC_ItemStack stack){}
    public override void OnHovered(bool over)
    {
        RebirthCharacterItemStatsTooltip.Hover(this,over);
        base.OnHovered(over);
    }
}





