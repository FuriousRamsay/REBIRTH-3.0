using System;
using System.Linq;
using HarmonyLib;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthCamperEnergy:XUiController
{
    private Func<bool> current;private Action release;private Action<RebirthCamperEnergyCommand,Guid,int,bool,int> command;
    private RebirthCamperEnergySnapshot snapshot;private RebirthCamperEnergyPoiSnapshot poi;private Guid selected;
    private bool opened,busy=true;private float nextRefresh;
    private string status;
    private int powerTraceCount;private Guid powerObservedSource;private string powerObserved;
    private bool boundsAllowed,boundsShown;
    // Six eligible actor slots; retain legacy positions for exact removal.
    private readonly XUiC_RebirthCamperEnergySlot[] cards=new XUiC_RebirthCamperEnergySlot[12];
    private readonly XUiC_RebirthCamperEnergySlot[] components=new XUiC_RebirthCamperEnergySlot[6];
    private int sourceCapacity;
    private XUiC_RebirthCharacterOverviewList scroll;
    private int backpackCount=-1;
    private readonly RebirthCamperEnergyHeldPointer heldPointer=new RebirthCamperEnergyHeldPointer();
    private XUiC_ItemStack[] gestureBackpackCells;
    internal bool PointerReleaseConsumed=>heldPointer.Consumed;
    internal long Revision=>snapshot?.Revision??-1;
    internal bool CanInteract=>opened&&!busy&&current?.Invoke()==true;
    internal string GestureState=>"opened="+opened+" busy="+busy+" current="+(current?.Invoke()==true)+" selected="+(selected!=Guid.Empty)+" revision="+Revision+
        " ownerHeld="+RebirthCamperEnergyOwnerHold.Blocks(xui.playerUI.entityPlayer.bag)+" nativeActorHeld="+RebirthCamperStationNativeActorPayment.Blocks(xui.playerUI.entityPlayer.bag);
    public override void Init()
    {
        base.Init();
        for(int i=0;i<components.Length;i++)
        {components[i]=GetChildById("energyComponent"+i) as XUiC_RebirthCamperEnergySlot;if(components[i]==null)throw new InvalidOperationException("Energy native component slot missing.");components[i].Bind(this,i);}
        for(int i=0;i<cards.Length;i++)
        {
            int index=i;cards[i]=GetChildById("energySourceCard"+i) as XUiC_RebirthCamperEnergySlot;
            if(cards[i]==null)throw new InvalidOperationException("Energy native source card missing.");
            cards[i].Bind(this,-2-i);
            cards[i].OnMouseUpDown+=(s,pressed)=>{var source=SourceAt(index);if(pressed&&opened&&source!=null){selected=source.Id;Paint();}};
        }
        Button("energyModeToggle",ToggleMode);
        Button("energyRefuel",()=>Send(RebirthCamperEnergyCommand.Refuel));
        Button("energyPower",()=>Send(RebirthCamperEnergyCommand.Enabled,!(Selected()?.IsOn??false)));
        Button("energyPoiLights",()=>Send(RebirthCamperEnergyCommand.PoiLights,!(poi?.IsOn??false)));
        Button("energyBounds",ToggleBounds);
        scroll=GetChildByType<XUiC_RebirthCharacterOverviewList>();scroll?.BindOwner(()=>opened&&windowGroup.isShowing);
        // Native DragEnd belongs to the original dragged cell and runs before
        // owner Update. Resolve its current release target while custody is held.
        var backpackCells=GetChildByType<XUiC_Backpack>()?.GetItemStackControllers();
        gestureBackpackCells=backpackCells;
        if(backpackCells!=null)foreach(var cell in backpackCells)
            if(cell!=null)
            {
                var source=cell;
                source.OnMouseUpDown+=(s,pressed)=>TraceBackpackGesture(source,"source.mouse", "pressed="+pressed);
                source.OnDrag+=(s,type,delta)=>
                {
                    if(type!=EDragType.Dragging)TraceBackpackGesture(source,"source.drag","drag="+type);
                    if(type==EDragType.DragEnd)DropHeldAtPointer();
                };
            }
    }
    private void Button(string id,Action click)
    {
        var node=GetChildById(id);if(!(node?.ViewComponent is XUiV_Sprite))throw new InvalidOperationException("Energy crafting action sprite missing: "+id);
        node.OnPress+=(s,b)=>{if((b==-1||b==0)&&node.ViewComponent.Enabled)click();};
        node.OnHover+=(s,over)=>ActionVisual(s,over);
        ActionVisual(node,false);
    }
    private static void ActionVisual(XUiController node,bool over)
    {
        var sprite=node?.ViewComponent as XUiV_Sprite;if(sprite==null)return;
        bool highlight=over&&sprite.Enabled;
        sprite.SpriteName=highlight?"ui_game_select_row":"menu_empty2px";
        sprite.Color=highlight?UnityEngine.Color.white:new UnityEngine.Color32(240,240,244,255);
    }
    internal void Bind(EntityVehicle vehicle,Func<bool> isCurrent,Action close,Action<RebirthCamperEnergyCommand,Guid,int,bool,int> send)
    {heldPointer.Reset();current=isCurrent;release=close;command=send;snapshot=null;poi=null;selected=Guid.Empty;busy=true;sourceCapacity=0;boundsAllowed=false;boundsShown=false;}
    internal void Present(RebirthCamperEnergySnapshot observed,RebirthCamperEnergyPoiSnapshot observedPoi,bool waiting,string observedStatus=null,bool canShowBounds=false,bool shownBounds=false,int capacity=0)
    {snapshot=observed;poi=observedPoi;busy=waiting;status=observedStatus;sourceCapacity=capacity==3||capacity==6?capacity:0;boundsAllowed=canShowBounds;boundsShown=shownBounds;if(Selected()==null)selected=snapshot?.Sources.FirstOrDefault()?.Id??Guid.Empty;ObservePowerRequest();Paint();}
    private void ToggleBounds()
    {
        if(!opened||current?.Invoke()!=true)return;
        if(!boundsAllowed||snapshot?.Mode!=RebirthCamperEnergyMode.Camp){status="xuiRebirthCamperEnergyBoundsCampOnly";Paint();return;}
        command?.Invoke(RebirthCamperEnergyCommand.Bounds,Guid.Empty,-1,!boundsShown,-1);
    }
    private void ToggleMode()
    {
        if(!CanInteract)return;
        if(snapshot?.Mode==RebirthCamperEnergyMode.Travel)Send(RebirthCamperEnergyCommand.Camp);
        else if(snapshot?.Mode==RebirthCamperEnergyMode.Camp)Send(RebirthCamperEnergyCommand.Travel);
        // Deploying/Packing/unknown remain disabled and cannot dispatch.
    }
    internal void SelectSource(Guid id){selected=id;Paint();}
    private RebirthCamperEnergySourceSnapshot Selected()=>snapshot?.Sources.FirstOrDefault(s=>s.Id==selected);
    public override bool GetBindingValueInternal(ref string value,string name)
    {
        if(name=="energyfuelfraction"||name=="energyfuelvisible")
        {
            var source=Selected();bool valid=source?.Kind==RebirthCamperEnergySourceKind.Generator&&source.MaxFuel>0;
            value=name=="energyfuelvisible"?valid.ToString():valid?UnityEngine.Mathf.Clamp01(source.CurrentFuel/(float)source.MaxFuel).ToString(System.Globalization.CultureInfo.InvariantCulture):"0";
            return true;
        }
        return base.GetBindingValueInternal(ref value,name);
    }
    private RebirthCamperEnergySourceSnapshot SourceAt(int position)=>snapshot?.Sources.FirstOrDefault(s=>s.Position==position);
    private int FirstEmptySource(){for(int i=0;i<sourceCapacity;i++)if(SourceAt(i)==null)return i;return -1;}
    private void Send(RebirthCamperEnergyCommand action,bool on=false)
    {
        if(action==RebirthCamperEnergyCommand.Enabled)
        {
            PowerTrace("request","source="+selected+" revision="+Revision+" observedOn="+(Selected()?.IsOn.ToString()??"missing")
                +" requestedOn="+on+" admitted="+CanInteract+" mode="+snapshot?.Mode+" busy="+busy);
            if(CanInteract&&selected!=Guid.Empty){powerObservedSource=selected;powerObserved=null;}
        }
        if(!CanInteract)return;
        if((action==RebirthCamperEnergyCommand.Remove||action==RebirthCamperEnergyCommand.Refuel||action==RebirthCamperEnergyCommand.Enabled)&&selected==Guid.Empty)return;
        if(action==RebirthCamperEnergyCommand.PoiLights&&(snapshot?.Mode!=RebirthCamperEnergyMode.Camp||
            (on?!RebirthCamperEnergyDeviceAdapter.CanSupplyPoiLights(snapshot,poi,out _):poi?.IsOn!=true)))return;
        int destination=action==RebirthCamperEnergyCommand.Remove&&InputUtils.ShiftKeyPressed?EmptyBackpackSlot():-1;
        if(action==RebirthCamperEnergyCommand.Remove&&InputUtils.ShiftKeyPressed&&destination<0)return;
        command(action,selected,-1,on,destination);
    }
    private void PowerTrace(string stage,string detail)
    {if(powerTraceCount>=12)return;powerTraceCount++;Log.Out("[REBIRTH EnergyPowerUI] stage="+stage+" "+detail);}
    private void ObservePowerRequest()
    {
        if(powerObservedSource==Guid.Empty)return;
        var observed=snapshot?.Sources.FirstOrDefault(s=>s.Id==powerObservedSource);
        string value="source="+powerObservedSource+" revision="+Revision+" on="+(observed?.IsOn.ToString()??"missing")
            +" status="+observed?.Status+" busy="+busy+" admission="+status;
        if(value==powerObserved)return;powerObserved=value;PowerTrace("observed",value);
    }
    private void EnableButton(string id,bool enabled)
    {
        var node=GetChildById(id);var view=node?.ViewComponent;
        if(view!=null){view.Enabled=enabled;view.IsNavigatable=enabled;view.IsSnappable=enabled;if(!enabled)ActionVisual(node,false);}
        var color=enabled?new UnityEngine.Color32(240,240,244,255):new UnityEngine.Color32(120,120,128,255);
        var label=GetChildById(id=="energyPoiLights"?"energyPoiLabel":id+"Label")?.ViewComponent as XUiV_Label;
        if(label!=null)label.Color=color;
        var icon=GetChildById(id+"Icon")?.ViewComponent as XUiV_Sprite;if(icon!=null)icon.Color=color;
    }
    internal void Transfer(int slot)
    {
        if(slot<=-2){TransferSource(-2-slot);return;}
        bool admitted=CanInteract&&(slot<0||selected!=Guid.Empty);
        RebirthCamperEnergyGestureTrace.Write("ui.transfer","slot="+slot+" admitted="+admitted+" "+GestureState+" cursor "+RebirthCamperEnergyGestureTrace.Stack(xui.DragAndDropWindow?.CurrentStack));
        if(admitted)command(RebirthCamperEnergyCommand.Component,selected,slot,false,-1);
    }
    private void TransferSource(int index)
    {
        if(!CanInteract||snapshot==null||index<0||index>=cards.Length)return;
        var installed=SourceAt(index);
        if(installed!=null){if(xui.DragAndDropWindow?.IsEmpty()==true)command(RebirthCamperEnergyCommand.Remove,installed.Id,index,false,-1);}
        else if(index<sourceCapacity)command(RebirthCamperEnergyCommand.Install,Guid.Empty,index,false,-1);
    }
    internal bool CanSwapSlot(int slot,ItemStack stack)
    {
        if(!CanInteract||stack==null||!stack.IsEmpty()&&stack.count!=1)return false;
        if(slot>=0)return true;
        int index=-2-slot;
        if(snapshot==null||index<0||index>=cards.Length)return false;
        if(SourceAt(index)!=null)return stack.IsEmpty();
        if(index>=sourceCapacity||stack.IsEmpty())return false;
        var item=stack.itemValue?.ItemClass;
        var block=item?.IsBlock()==true?Block.GetBlockValue(item.GetItemName()).Block as BlockPowerSource:null;
        string kind=block?.Properties.GetString("Class");
        return kind=="Generator"||kind=="BatteryBank"||kind=="SolarPanel";
    }
    internal void DropHeldAtPointer()
    {
        RebirthCamperEnergyGestureTrace.Write("pointer.drag-end",GestureState+" cursor "+RebirthCamperEnergyGestureTrace.Stack(xui.DragAndDropWindow?.CurrentStack));
        if(opened)heldPointer.Release(xui,CanInteract,cards,components);
    }
    private void TraceBackpackGesture(XUiC_ItemStack source,string stage,string detail)
    {
        if(!opened)return;
        var view=source.ViewComponent;
        RebirthCamperEnergyGestureTrace.Write(stage,"slot="+source.SlotNumber+" id="+view.ID+" "+detail+
            " dragEnabled="+view.EventOnDrag+" pressEnabled="+view.EventOnPress+" enabled="+view.Enabled+" wasDragging="+view.wasDragging+
            " cursor "+RebirthCamperEnergyGestureTrace.Stack(xui.DragAndDropWindow?.CurrentStack));
    }
    private int EmptyBackpackSlot()
    {
        var player=xui.playerUI.entityPlayer;var bag=player.bag;var locks=bag.LockedSlots;
        var cells=GetChildByType<XUiC_Backpack>()?.GetItemStackControllers();
        if(cells==null)return -1;
        // CarryCapacity marks native encumbrance appearance (AttributeLock),
        // not the physical storage boundary. Use the same native destination
        // admission as working management reverse transfers.
        foreach(var cell in cells)
        {
            int index=cell?.SlotNumber??-1;
            if(index>=0&&index<bag.ItemGrid.Length&&!cell.UserLockedSlot&&!cell.IsLocked&&!cell.StackLock&&
                bag.ItemGrid.items[index].IsEmpty()&&(locks==null||index>=locks.Length||!locks[index]))return index;
        }
        return -1;
    }
    internal void PreferredComponent(int slot)
    {
        if(slot<=-2)
        {
            int index=-2-slot;
            var source=SourceAt(index);
            if(!CanInteract||source==null||xui.DragAndDropWindow?.IsEmpty()!=true)return;
            int sourceDestination=EmptyBackpackSlot();if(sourceDestination>=0)command(RebirthCamperEnergyCommand.Remove,source.Id,index,false,sourceDestination);
            return;
        }
        if(!CanInteract||selected==Guid.Empty||slot<0||slot>=6||xui.DragAndDropWindow?.IsEmpty()!=true)return;
        if(Selected()?.Components[slot].IsEmpty()!=false)return;
        int destination=EmptyBackpackSlot();if(destination>=0)command(RebirthCamperEnergyCommand.Component,selected,slot,false,destination);
    }
    internal bool PreferredBackpack(XUiC_ItemStack source)
    {
        if(source?.StackLocation!=XUiC_ItemStack.StackLocationTypes.Backpack)return false;
        var value=source.ItemStack;if(value?.itemValue?.ItemClass==null||value.IsEmpty())return false;
        var block=value.itemValue.ItemClass.IsBlock()?Block.GetBlockValue(value.itemValue.ItemClass.GetItemName()).Block as BlockPowerSource:null;
        if(block!=null){string kind=block.Properties.GetString("Class");if(kind!="Generator"&&kind!="BatteryBank"&&kind!="SolarPanel")block=null;}
        var chosen=Selected();int target=-1;
        if(block==null&&chosen!=null)
        {
            var native=Block.GetBlockValue(chosen.Installed.itemValue.ItemClass.GetItemName()).Block as BlockPowerSource;
            var expected=native==null?null:ItemClass.GetItemClass(native.SlotItemName);
            if(expected?.Id!=value.itemValue.type)return false;
            var slots=chosen.Components;for(int i=0;i<slots.Length;i++)if(slots[i].IsEmpty()){target=i;break;}
        }
        if(block==null&&target<0)return false;
        // These recognized native source/component gestures own this route even
        // when busy/locked: native fallback must not move the displayed clone.
        if(!CanInteract||value.count!=1||source.UserLockedSlot||source.StackLock||source.IsLocked||xui.DragAndDropWindow?.IsEmpty()!=true)return true;
        if(block!=null){int empty=FirstEmptySource();if(snapshot==null||empty<0)return true;command(RebirthCamperEnergyCommand.Install,Guid.Empty,empty,false,source.SlotNumber);}
        else command(RebirthCamperEnergyCommand.Component,selected,target,false,source.SlotNumber);return true;
    }
    private void Label(string id,string value)
    {var label=GetChildById(id)?.ViewComponent as XUiV_Label;if(label!=null)label.Text=value;}
    private void Paint()
    {
        string mode=snapshot==null?Localization.Get("xuiRebirthCamperEnergyWaiting"):Localization.Get("xuiRebirthEnergyMode"+snapshot.Mode);
        Label("energyMode",snapshot!=null&&snapshot.TransitionSeconds>0?string.Format(Localization.Get("xuiRebirthCamperEnergyTransitionFormat"),mode,UnityEngine.Mathf.CeilToInt(snapshot.TransitionSeconds).ToString()):mode);
        Label("energyPowerSummary",snapshot==null?"Unavailable":snapshot.SuppliedWatts+" / "+snapshot.DemandWatts+" W");
        Label("energyRuntimeSummary",RuntimeEstimateText());
        Label("energyRangeSummary",snapshot?.Range>0?snapshot.Range.ToString("0.#")+" m":"Unavailable");
        Label("energyTransition",snapshot?.TransitionSeconds>0?mode+": "+UnityEngine.Mathf.CeilToInt(snapshot.TransitionSeconds)+"s":string.Empty);
        Label("energySummary",snapshot==null?string.Empty:string.Format(Localization.Get("xuiRebirthCamperEnergySummaryFormat"),snapshot.DemandWatts,snapshot.SuppliedWatts,snapshot.Range.ToString("0.#")));
        Label("energyCustody",!string.IsNullOrEmpty(status)?Localization.Get(status):busy?Localization.Get("xuiRebirthCamperEnergySaving"):string.Empty);
        for(int i=0;i<cards.Length;i++)
        {
            var source=SourceAt(i);
            bool visible=snapshot!=null&&(source!=null||i<sourceCapacity);cards[i].ViewComponent.IsVisible=visible;if(!visible)continue;
            if(source==null){cards[i].Present(ItemStack.Empty);Label("energySourceName"+i,Localization.Get("xuiRebirthCamperEnergyEmptySource"));Label("energySourceStatus"+i,string.Empty);continue;}
            cards[i].Present(source.Installed);Label("energySourceName"+i,source.Installed.itemValue.ItemClass.GetLocalizedItemName());
            Label("energySourceStatus"+i,SourceStatusText(source));
        }
        var chosen=Selected();Label("energySelectedName",chosen==null?Localization.Get("xuiRebirthCamperEnergySelectSource"):chosen.Installed.itemValue.ItemClass.GetLocalizedItemName());
        var selectedIcon=GetChildById("energySelectedIcon")?.ViewComponent as XUiV_Sprite;
        if(selectedIcon!=null)
        {
            selectedIcon.IsVisible=chosen!=null;
            if(chosen!=null){var item=chosen.Installed.itemValue;selectedIcon.SpriteName=item.GetPropertyOverride(ItemClass.PropCustomIcon,item.ItemClass.GetIconName());selectedIcon.Color=item.ItemClass.GetIconTint(item);}
        }
        Label("energySelectedStatus",chosen==null?string.Empty:SourceStatusText(chosen));
        Label("energyFuel",chosen?.Kind==RebirthCamperEnergySourceKind.Generator?string.Format(Localization.Get("xuiRebirthCamperEnergyFuelFormat"),chosen.CurrentFuel,chosen.MaxFuel):
            chosen==null?string.Empty:string.Format(Localization.Get("xuiRebirthCamperEnergyChargeFormat"),chosen.CurrentPower));
        var cells=chosen?.Components;for(int i=0;i<components.Length;i++)components[i].Present(cells==null?ItemStack.Empty:cells[i]);
        bool camp=snapshot?.Mode==RebirthCamperEnergyMode.Camp;
        Label("energyPoiStatus",PoiStatusText(camp));
        string sourceKind=Localization.Get("xuiRebirthCamperEnergy"+(chosen?.Kind==RebirthCamperEnergySourceKind.Battery?
            "BatteryBank":chosen?.Kind==RebirthCamperEnergySourceKind.Solar?"SolarBank":"Generator"));
        Label("energyPowerLabel",string.Format(Localization.Get(chosen?.IsOn==true?
            "xuiRebirthCamperEnergyTurnSourceOffFormat":"xuiRebirthCamperEnergyTurnSourceOnFormat"),sourceKind));
        Label("energyPoiLabel",Localization.Get(poi?.IsOn==true?"xuiRebirthCamperEnergyPoiOff":"xuiRebirthCamperEnergyPoiOn"));
        Label("energyBoundsLabel",Localization.Get(boundsShown?"xuiRebirthCamperEnergyHideBounds":"xuiRebirthCamperEnergyShowBounds"));
        EnableButton("energyBounds",boundsAllowed&&snapshot?.Mode==RebirthCamperEnergyMode.Camp);
        Label("energyModeToggleLabel",Localization.Get(camp||snapshot?.Mode==RebirthCamperEnergyMode.Deploying?"xuiRebirthCamperEnergyTravel":"xuiRebirthCamperEnergyCamp"));
        EnableButton("energyModeToggle",CanInteract&&(camp||snapshot?.Mode==RebirthCamperEnergyMode.Travel));
        EnableButton("energyPower",CanInteract&&chosen!=null);
        EnableButton("energyRefuel",CanInteract&&chosen?.Kind==RebirthCamperEnergySourceKind.Generator);
        EnableButton("energyPoiLights",CanInteract&&camp&&(poi?.IsOn==true||RebirthCamperEnergyDeviceAdapter.CanSupplyPoiLights(snapshot,poi,out _)));
        GetChildById("energyFuelTrack")?.RefreshBindings();GetChildById("energyFuelFill")?.RefreshBindings();
    }
    private string PoiStatusText(bool camp)
    {
        if(snapshot==null||poi==null)return Localization.Get("xuiRebirthCamperEnergyWaiting");
        string location=poi.InRange&&!string.IsNullOrEmpty(poi.PoiName)?
            "[D0A0FF]"+Localization.Get(poi.PoiName)+"[-]":string.Empty;
        if(!camp)
        {
            string instruction=Localization.Get(snapshot.Mode==RebirthCamperEnergyMode.Travel?
                "xuiRebirthCamperEnergyPoiEnterCamp":"xuiRebirthCamperEnergyPoiTransition");
            return (location.Length>0?location:Localization.Get("xuiRebirthCamperEnergyPoiOutside"))+"\n"+instruction;
        }
        if(!poi.InRange)return Localization.Get("xuiRebirthCamperEnergyPoiOutside");
        if(poi.SupportedLights==0)return string.Format(Localization.Get("xuiRebirthCamperEnergyPoiNoEligibleFormat"),location);
        string value=string.Format(Localization.Get("xuiRebirthCamperEnergyPoiFormat"),
            location,poi.SupportedLights,poi.DemandWatts);
        if(poi.SupportedLights>0&&!RebirthCamperEnergyDeviceAdapter.CanSupplyPoiLights(snapshot,poi,out int available))
            value+="\n"+(poi.RequiredWatts<0?Localization.Get("xuiRebirthCamperEnergyPoiPowerUnknown"):
                string.Format(Localization.Get("xuiRebirthCamperEnergyPoiPowerNeededFormat"),poi.RequiredWatts,available));
        return value;
    }
    private static string SourceStatusText(RebirthCamperEnergySourceSnapshot source)
    {
        bool components=source.Components.Any(item=>!item.IsEmpty());
        string reason=null;
        if(!components)reason=source.Kind==RebirthCamperEnergySourceKind.Generator?"NoEngines":
            source.Kind==RebirthCamperEnergySourceKind.Battery?"NoBatteries":"NoSolarCells";
        else if(!source.IsOn)reason="SourceOff";
        else if(source.Kind==RebirthCamperEnergySourceKind.Generator&&source.CurrentFuel==0&&source.CurrentPower==0)reason="NoFuel";
        else if(source.Kind==RebirthCamperEnergySourceKind.Battery&&source.CurrentPower==0&&
            source.Components.All(item=>item.IsEmpty()||item.itemValue.MaxUseTimes-item.itemValue.UseTimes<1f))reason="NoCharge";
        else if(source.Kind==RebirthCamperEnergySourceKind.Solar&&source.Status==RebirthCamperEnergySourceStatus.Obstructed)reason="NoSunlight";
        return Localization.Get(reason==null?"xuiRebirthEnergyStatus"+source.Status:"xuiRebirthCamperEnergy"+reason);
    }
    private string RuntimeEstimateText()
    {
        // Format only the authoritative helper/wire result; never infer an
        // estimate from icon ratings, generic fuel units or an old packet.
        if(snapshot==null)return "Unavailable";
        if(snapshot.RuntimeEstimateStatus==RebirthCamperEnergyEstimateStatus.Idle)return "Idle";
        if(snapshot.RuntimeEstimateStatus==RebirthCamperEnergyEstimateStatus.DaylightSupported)return "Daylight\nsupports current load";
        var seconds=snapshot.EstimatedRuntimeSeconds;
        if(snapshot.RuntimeEstimateStatus!=RebirthCamperEnergyEstimateStatus.Finite||!seconds.HasValue
            ||double.IsNaN(seconds.Value)||double.IsInfinity(seconds.Value)||seconds.Value<0)return "Unavailable";
        double rounded=Math.Ceiling(seconds.Value),hours=Math.Floor(rounded/3600),minutes=Math.Floor((rounded%3600)/60);
        string value=hours>=1?hours.ToString("0")+"h "+minutes.ToString("0")+"m":minutes>=1?minutes.ToString("0")+"m "+(rounded%60).ToString("0")+"s":rounded.ToString("0")+"s";
        return snapshot.RuntimeEstimateSolarConditional?value+"\nCurrent daylight/load":value;
    }
    public override void OnOpen(){powerTraceCount=0;powerObservedSource=Guid.Empty;powerObserved=null;heldPointer.Reset();RebirthCamperEnergyGestureTrace.Reset();opened=true;base.OnOpen();RebirthCamperStationScreenFrame.Apply(this);backpackCount=-1;RefreshBackpackRows();Paint();
        var actual=GetChildByType<XUiC_Backpack>()?.GetItemStackControllers();
        RebirthCamperEnergyGestureTrace.Write("source.subscriptions","initCount="+(gestureBackpackCells?.Length??-1)+" openCount="+(actual?.Length??-1)+
            " sameArray="+ReferenceEquals(gestureBackpackCells,actual)+" sameCells="+(actual!=null&&gestureBackpackCells!=null&&actual.Length==gestureBackpackCells.Length&&actual.Where((cell,index)=>!ReferenceEquals(cell,gestureBackpackCells[index])).Any()==false));
    }
    private void RefreshBackpackRows()
    {
        int count=xui.playerUI.entityPlayer.bag.ItemGrid.Length;if(count==backpackCount)return;backpackCount=count;
        var backpack=GetChildByType<XUiC_Backpack>();var slots=backpack?.GetItemStackControllers();
        if(slots!=null)for(int i=0;i<slots.Length;i++)slots[i].ViewComponent.IsVisible=i<count;
        int rows=(count+12)/13;scroll?.SetItemCount(rows,string.Empty);
        var content=scroll?.GetChildById("listContent")?.ViewComponent;
        if(content!=null)content.Size=new Vector2i(871,Math.Max(0,rows*67-2));
    }
    public override void Update(float dt)
    {
        if(opened&&current?.Invoke()!=true){xui.playerUI.windowManager.Close((GUIWindow)windowGroup);return;}
        if(opened)heldPointer.Update(xui,CanInteract,cards,components);
        base.Update(dt);if(!opened)return;RebirthCamperStationScreenFrame.Apply(this);RefreshBackpackRows();
        if(UnityEngine.Time.realtimeSinceStartup>=nextRefresh){nextRefresh=UnityEngine.Time.realtimeSinceStartup+.5f;command?.Invoke(RebirthCamperEnergyCommand.Query,Guid.Empty,-1,false,-1);}
    }
    public override void OnClose()
    {var close=release;heldPointer.Reset();opened=false;release=null;current=null;command=null;try{base.OnClose();}finally{close?.Invoke();}}
}

[Preserve]
public sealed class XUiC_RebirthCamperEnergySlot:XUiC_BasePartStack
{
    private XUiC_RebirthCamperEnergy owner;
    private bool traceWired;
    internal void Bind(XUiC_RebirthCamperEnergy view,int slot)
    {
        owner=view;SlotNumber=slot;StackLocation=XUiC_ItemStack.StackLocationTypes.Part;InfoWindow=null;emptySpriteName=string.Empty;
        if(traceWired)return;traceWired=true;
        OnDrag+=(s,type,delta)=>{Trace("slot.native-drag","drag="+type);if(type==EDragType.DragEnd)owner?.DropHeldAtPointer();};
        OnPress+=(s,button)=>Trace("slot.native-press","button="+button);
    }
    private void Trace(string stage,string extra)
    {
        try
        {
        var cursor=xui?.playerUI?.CursorController;
        RebirthCamperEnergyGestureTrace.Write(stage,"slot="+SlotNumber+" "+extra+" over="+isOver+" uiHover="+ViewComponent.UiTransformIsHovered+
            " eventPress="+ViewComponent.EventOnPress+" lastClicked="+lastClicked+" left="+(cursor?.GetMouseButton(UICamera.MouseButton.LeftButton)==true)+
            " down="+(cursor?.GetMouseButtonDown(UICamera.MouseButton.LeftButton)==true)+" up="+(cursor?.GetMouseButtonUp(UICamera.MouseButton.LeftButton)==true)+
            " shift="+InputUtils.ShiftKeyPressed+" input="+xui.playerUI.playerInput.GUIActions.LastInputType+" "+owner?.GestureState+
            " cursor "+RebirthCamperEnergyGestureTrace.Stack(xui.DragAndDropWindow?.CurrentStack));
        }
        catch{} // Trace must not affect native slot input/admission.
    }
    internal void Present(ItemStack stack){ItemStack=stack.Clone();RefreshBindings();}
    public override string GetAtlas()=>"ItemIconAtlas";
    public override string GetPartName()=>ItemStack==null||ItemStack.IsEmpty()?string.Empty:ItemClass.GetLocalizedItemName();
    public override void SetEmptySpriteName(){emptySpriteName=string.Empty;}
    public override bool CanSwap(ItemStack stack)
    {bool admitted=owner?.CanSwapSlot(SlotNumber,stack)==true;Trace("slot.can-swap","admitted="+admitted+" incoming "+RebirthCamperEnergyGestureTrace.Stack(stack));return admitted;}
    public override bool CanRemove()=>owner?.CanInteract==true&&ItemStack?.IsEmpty()==false;
    public override void SwapItem(){if(owner?.PointerReleaseConsumed==true)return;Trace("slot.swap",string.Empty);owner?.Transfer(SlotNumber);}
    public override void Update(float dt)
    {
        bool boundary=(isOver||ViewComponent.UiTransformIsHovered)&&
            (xui.playerUI.CursorController.GetMouseButtonUp(UICamera.MouseButton.LeftButton)||xui.playerUI.CursorController.GetMouseButtonDown(UICamera.MouseButton.LeftButton)||
                xui.DragAndDropWindow?.IsEmpty()==false&&xui.playerUI.CursorController.GetMouseButton(UICamera.MouseButton.LeftButton)&&!lastClicked);
        if(boundary)Trace("slot.update-before",string.Empty);
        if(isOver&&ViewComponent.UiTransformIsHovered&&xui.DragAndDropWindow?.IsEmpty()==false&&xui.playerUI.CursorController.GetMouseButton(UICamera.MouseButton.LeftButton))lastClicked=true;
        base.Update(dt);
        if(boundary)Trace("slot.update-after",string.Empty);
    }
}
[HarmonyPatch(typeof(XUiC_BasePartStack),nameof(XUiC_BasePartStack.HandleMoveToPreferredLocation))]
internal static class RebirthCamperEnergyPreferredGuard
{
    private static bool Prefix(XUiC_BasePartStack __instance)
    {
        if(!(__instance is XUiC_RebirthCamperEnergySlot slot))return true;
        slot.GetParentByType<XUiC_RebirthCamperEnergy>()?.PreferredComponent(slot.SlotNumber);return false;
    }
}
[HarmonyPatch(typeof(XUiC_ItemStack),nameof(XUiC_ItemStack.HandleMoveToPreferredLocation))]
internal static class RebirthCamperEnergyBackpackPreferredGuard
{
    private static bool Prefix(XUiC_ItemStack __instance)=>__instance.GetParentByType<XUiC_RebirthCamperEnergy>()?.PreferredBackpack(__instance)!=true;
}
