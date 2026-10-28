using System;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthCamperModules : XUiController
{
    private readonly XUiC_RebirthCamperModuleSlot[] slots = new XUiC_RebirthCamperModuleSlot[RebirthCamperCraftingStation.ModuleSlots];
    private Func<bool> current;
    private Func<int, ItemStack> read;
    private Func<ItemStack, bool> accepts;
    private Func<int, ItemStack, bool> transfer;
    private Func<int, int, bool> quickTransfer;
    private Func<int?> activeQueueSnapshot;
    private Action release;
    private bool opened;
    private bool crafting;
    private bool heldPointerDrag;
    private bool inspectingHeld;
    private float nextHeldInspection;
    private int refusalWarnings;
    private int preferredRemoval = -1;
    private int preferredInstallation = -1;
    internal EntityVehicle BoundVehicle { get; set; }
    private readonly RebirthWindowHudScope hud = new RebirthWindowHudScope();
    private XUiController body;
    private Vector2i layoutSize = new Vector2i(-1, -1);
    private int summaryInstalled,summaryCapacity;private float nextSummary;
    public new bool IsOpen => opened;
    public bool CanInteract => opened && current != null && current();

    public override void Init()
    {
        base.Init();
        body = GetChildById("camperBody");
        for (int index = 0; index < slots.Length; index++)
        {
            slots[index] = GetChildById("camperModule" + index) as XUiC_RebirthCamperModuleSlot;
            if (slots[index] == null) throw new InvalidOperationException("Camper management requires twenty module positions.");
            slots[index].Bind(this, index);
        }
    }

    // Session coordinator supplies authenticated snapshot and transfer functions.
    // This view never treats client-side slot edits as authoritative inventory.
    public void Bind(string title, Func<bool> isCurrent, Func<int, ItemStack> getModule,
        Func<ItemStack, bool> canInstall, Func<int, ItemStack, bool> requestTransfer, Action closeSession = null, bool craftingStations = false,
        Func<int,int,bool> requestQuickTransfer = null,Func<int?> getActiveQueues = null)
    {
        if (isCurrent == null || getModule == null || canInstall == null || requestTransfer == null)
            throw new ArgumentException("Complete management session required.");
        current = isCurrent; read = getModule; accepts = canInstall; transfer = requestTransfer; release = closeSession;
        crafting = craftingStations;
        quickTransfer=requestQuickTransfer;
        activeQueueSnapshot=getActiveQueues;
        var filter=GetChildById("sectionFilter")?.ViewComponent as XUiV_Button;
        if(filter!=null)
        {
            // Match the storage/stations entries in RebirthCamperRadial.
            filter.UIAtlas="UIAtlas";
            filter.DefaultSpriteName=crafting?"ui_game_symbol_workbench":"ui_game_symbol_purgesupplies";
            filter.HoverSpriteName=filter.SelectedSpriteName=filter.DefaultSpriteName;
            filter.ToolTip=Localization.Get(crafting?"xuiRebirthCamperFilterStations":"xuiRebirthCamperFilterContainers");
        }
        var label = GetChildById("camperManagementTitle")?.ViewComponent as XUiV_Label;
        if (label != null) label.Text = title;
        var instructions = GetChildById("camperModuleInstructions")?.ViewComponent as XUiV_Label;
        if (instructions != null) instructions.Text = Localization.Get(crafting ? "xuiRebirthCamperStationInstallHelp" : "xuiRebirthCamperStorageInstallHelp");
        RefreshModules();
        inspectingHeld=false;Inspect(null);
        var bag = RebirthCraftingInventoryBridge.GetBag(xui);
        (GetChildById("sellBagScroll") as XUiC_RebirthCharacterOverviewList)?.SetItemCount(((bag?.ItemGrid.items.Length ?? 0) + 12) / 13, string.Empty);
    }

    public void RefreshModules()
    {
        int installed = 0, capacity = 0;
        for (int index = 0; index < slots.Length; index++)
        {
            ItemStack stack = read == null ? ItemStack.Empty : read(index);
            if (slots[index] != null) slots[index].Present(stack);
            if (stack != null && !stack.IsEmpty())
            {
                installed++;
                int count; if (!crafting && RebirthCamperStorageContainer.TryGetCapacity(stack, out count)) capacity += count;
            }
        }
        var summary = GetChildById("camperInstallationSummary")?.ViewComponent as XUiV_Label;
        if (summary != null) summary.Text = string.Format(Localization.Get(crafting ? "xuiRebirthCamperStationSummary" : "xuiRebirthCamperStorageSummary"), installed, capacity);
        summaryInstalled=installed;summaryCapacity=capacity;RefreshSummary();
    }
    private void SummaryLabel(string id,string text)
    {var label=GetChildById(id)?.ViewComponent as XUiV_Label;if(label!=null)label.Text=text;}
    private void RefreshSummary()
    {
        SummaryIcon("camperInstalledIcon",crafting?"ui_game_symbol_workbench":"ui_game_symbol_purgesupplies");
        SummaryIcon("camperSecondaryIcon",crafting?"ui_game_symbol_clock":"ui_game_symbol_loot_sack");
        SummaryLabel("camperInstalledCaption",crafting?"INSTALLED STATIONS":"CONTAINERS");
        SummaryLabel("camperInstalledValue",summaryInstalled+" / "+slots.Length);
        SummaryLabel("camperSecondaryCaption",crafting?"ACTIVE QUEUES":"TOTAL CARGO");
        string value=(RebirthCamperStorageCapacity.BaseSlots+summaryCapacity)+" slots";
        if(crafting)
        {
            value="Unavailable";
            int? projected=CanInteract?activeQueueSnapshot?.Invoke():null;
            if(projected.HasValue&&projected.Value>=0&&projected.Value<=slots.Length)value=projected.Value.ToString();
            // Existing authoritative native queues only. No runtime factory,
            // projected fake zero or decoder-created tile in a UI binding.
            if(CanInteract&&BoundVehicle?.world!=null&&!BoundVehicle.world.IsRemote()
                &&ReferenceEquals(GameManager.Instance?.World,BoundVehicle.world)
                &&ReferenceEquals(BoundVehicle.world.GetEntity(BoundVehicle.entityId),BoundVehicle)
                &&RebirthCamperModuleRuntime.TryGetExisting(BoundVehicle,out var runtime))
            {
                int count=0;for(int i=0;i<slots.Length;i++)
                    if(runtime.Stations.GetSlot(i)?.State.hasRecipeInQueue()==true)count++;
                value=count.ToString();
            }
        }
        SummaryLabel("camperSecondaryValue",value);
    }
    private void SummaryIcon(string id,string sprite)
    {var icon=GetChildById(id)?.ViewComponent as XUiV_Sprite;if(icon!=null){icon.UIAtlas="UIAtlas";icon.SpriteName=sprite;}}
    public override void OnOpen()
    {
        refusalWarnings=0;
        base.OnOpen(); opened = true;
        // Installed modules are presentation snapshots, not native modifier
        // slots. The native item-action inspector casts Part controllers to
        // modifier slots; use this window's owned inspector instead.
        foreach (var slot in slots) if (slot != null) slot.InfoWindow = null;
        RefreshModules();
    }
    public override void Update(float dt)
    {
        using var captureTiming = RebirthGameBridgeFrames.MeasureUi(crafting ? 2 : 1);
        if(opened && !CanInteract)
        { opened=false; xui.playerUI.windowManager.Close((GUIWindow)windowGroup); return; }
        if(opened)
        {
            var held=xui.DragAndDropWindow?.CurrentStack;
            if(held!=null&&!held.IsEmpty())
            {
                if(!inspectingHeld||UnityEngine.Time.realtimeSinceStartup>=nextHeldInspection)
                {Inspect(held);nextHeldInspection=UnityEngine.Time.realtimeSinceStartup+.2f;}
                inspectingHeld=true;
            }
            else if(inspectingHeld){Inspect(null);inspectingHeld=false;}
            var pointer=xui.playerUI.CursorController;
            if(pointer.GetMouseButton(UICamera.MouseButton.LeftButton)&&xui.DragAndDropWindow?.IsEmpty()==false)
                heldPointerDrag=true;
            if(pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton))
            {if(heldPointerDrag&&!InputUtils.ShiftKeyPressed)DropHeldAtPointer();heldPointerDrag=false;}
        }
        base.Update(dt);
        if (opened) { hud.Maintain(xui); Layout();if(UnityEngine.Time.realtimeSinceStartup>=nextSummary){nextSummary=UnityEngine.Time.realtimeSinceStartup+.5f;RefreshSummary();} }
    }
    private void Layout()
    {
        if (ViewComponent == null) return;
        Vector2i position, size; RebirthScreenLayout.GetScreenBounds(xui, out position, out size);
        if (layoutSize.x == size.x && layoutSize.y == size.y) return;
        layoutSize = size;
        float scale = Math.Min(Math.Max(1, size.x - 16) / 1856f, Math.Max(1, size.y - 78) / 813f);
        ViewComponent.Position = new Vector2i(position.x + 8, position.y - 70);
        ViewComponent.Size = new Vector2i(UnityEngine.Mathf.RoundToInt(1856 * scale), UnityEngine.Mathf.RoundToInt(813 * scale));
        ViewComponent.TryUpdatePosition();
        if (body?.ViewComponent?.UiTransform != null) body.ViewComponent.UiTransform.localScale = new UnityEngine.Vector3(scale, scale, 1f);
    }
    public void Inspect(ItemStack stack)
    {
        bool valid = stack != null && !stack.IsEmpty();
        var name = GetChildById("camperInspectName")?.ViewComponent as XUiV_Label;
        var description = GetChildById("camperInspectDescription")?.ViewComponent as XUiV_Label;
        var icon = GetChildById("camperInspectIcon")?.ViewComponent as XUiV_Sprite;
        var capacityLabel=GetChildById("camperInspectCapacity")?.ViewComponent as XUiV_Label;
        if(capacityLabel!=null)capacityLabel.Text=string.Empty;
        if (name != null) name.Text = valid ? Localization.Get(stack.itemValue.ItemClass.GetItemName()) : string.Empty;
        if (description != null)
        {
            description.Text = valid ? XUiC_RebirthCraftingItemContext.ResolveDescription(stack, xui) : string.Empty;
            // Same native/functional-container lookup as installation, using
            // the exact selected stack rather than the camper's total cargo.
            var single=valid?stack.Clone():null;if(single!=null)single.count=1;
            if(valid&&!crafting&&RebirthCamperStorageContainer.TryGetCapacity(single,out int selectedCapacity))
            {
                const string key="xuiRebirthCamperStorageContainerCapacityFormat";string format=Localization.Get(key);
                if(string.IsNullOrEmpty(format)||format==key)format="Container capacity: {0} slots";
                // Exact value color from xuiRebirthEstimatedStackSale; retain the
                // caption's purple and include the units in the yellow value.
                if(capacityLabel!=null)capacityLabel.Text="[B58CFF]"+string.Format(format.Replace("{0}","[-][D6C978]{0}"),selectedCapacity)+"[-]";
                else description.Text += "\n\n"+string.Format(format,selectedCapacity);
            }
        }
        if (icon != null) { icon.IsVisible = valid; if (valid) { icon.SpriteName = stack.itemValue.GetPropertyOverride(ItemClass.PropCustomIcon, stack.itemValue.ItemClass.GetIconName()); icon.Color = stack.itemValue.ItemClass.GetIconTint(stack.itemValue); } }
        var count=GetChildById("camperSelectedCount")?.ViewComponent as XUiV_Label;
        if(count!=null)count.Text=valid&&stack.count>1?stack.count.ToString():string.Empty;
        var durability=GetChildById("camperSelectedDurability");
        bool quality=valid&&stack.itemValue.Quality>0;
        bool durable=valid&&stack.itemValue.MaxUseTimes>0;
        if(durability?.ViewComponent!=null)durability.ViewComponent.IsVisible=quality||durable;
        var fill=durability?.GetChildById("fill")?.ViewComponent as XUiV_FilledSprite;
        if(fill!=null){fill.Fill=durable?UnityEngine.Mathf.Clamp01((stack.itemValue.MaxUseTimes-stack.itemValue.UseTimes)/(float)stack.itemValue.MaxUseTimes):1f;fill.Color=quality?(UnityEngine.Color)QualityInfo.GetQualityColor(stack.itemValue.Quality):UnityEngine.Color.white;}
        var number=durability?.GetChildById("qualityNumber")?.ViewComponent as XUiV_Label;
        if(number!=null)number.Text=quality?stack.itemValue.Quality.ToString():string.Empty;
        var remaining=GetChildById("camperSelectedDurabilityCount")?.ViewComponent as XUiV_Label;
        if(remaining!=null){remaining.IsVisible=durable;remaining.Text=durable?UnityEngine.Mathf.Max(0,UnityEngine.Mathf.RoundToInt(stack.itemValue.MaxUseTimes-stack.itemValue.UseTimes))+" / "+stack.itemValue.MaxUseTimes:string.Empty;}
        var item=valid?stack.itemValue.ItemClass:null;
        var display=item==null?null:UIDisplayInfoManager.Current.GetDisplayStatsForTag(item.IsBlock()?Block.list[stack.itemValue.type].DisplayType:item.DisplayType);
        for(int index=0;index<7;index++)
        {
            var title=GetChildById("camperStatName"+index)?.ViewComponent as XUiV_Label;
            var value=GetChildById("camperStatValue"+index)?.ViewComponent as XUiV_Label;
            var stat=display!=null&&index<display.DisplayStats.Count?display.DisplayStats[index]:null;
            if(title!=null)title.Text=stat==null?string.Empty:stat.TitleOverride??UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType);
            if(value!=null)value.Text=stat==null?string.Empty:RebirthItemStatColors.Format(RebirthItemStatColors.NativeValue(stack,xui.playerUI.entityPlayer,stat));
        }
    }
    public override void Cleanup() { hud.Restore(); base.Cleanup(); }
    public override void OnClose()
    {
        RebirthCamperTrace.Write("management.close",BoundVehicle,"opened="+opened);
        var close = release; release = null;
        hud.Restore();
        opened = false; current = null; read = null; accepts = null; transfer = null; quickTransfer=null;activeQueueSnapshot=null;
        preferredRemoval = -1;
        preferredInstallation = -1;
        heldPointerDrag=false;
        BoundVehicle = null;
        inspectingHeld=false;Inspect(null);
        foreach (var slot in slots) if (slot != null) slot.InfoWindow = null;
        base.OnClose();
        close?.Invoke();
    }
    internal bool Accepts(ItemStack stack)
    {
        if (!CanInteract || !accepts(stack)) return false;
        if (!crafting) return true;
        var single = stack.Clone(); single.count = 1;
        BlockValue candidate; WorkstationData data;
        if (!RebirthCamperCraftingStation.TryResolve(single, out candidate, out data)) return false;
        for (int index = 0; index < slots.Length; index++)
        {
            BlockValue installed; WorkstationData installedData;
            if (RebirthCamperCraftingStation.TryResolve(read(index), out installed, out installedData)
                && installed.type == candidate.type) return false;
        }
        return true;
    }
    internal bool IsInstallable(ItemStack stack)
    {
        return RebirthCamperManagementAccess.Accepts(stack,crafting);
    }
    internal void DropHeldAtPointer()
    {
        if(!CanInteract||xui.DragAndDropWindow==null||xui.DragAndDropWindow.IsEmpty())return;
        var camera=xui.playerUI.camera;
        if(camera==null)return;
        var ray=camera.ScreenPointToRay((UnityEngine.Vector3)xui.playerUI.CursorController.GetScreenPosition());
        foreach(var slot in slots)
            if(HitSlot(slot,ray)){RebirthCamperTrace.Write("drag.end-module",BoundVehicle,"slot="+slot.SlotNumber);Transfer(slot.SlotNumber);return;}
        var backpack=GetChildByType<XUiC_RebirthBackpackSectionBackpack>();
        if(backpack==null)return;
        foreach(var cell in backpack.itemControllers)
            if(cell!=null&&!cell.IsLocked&&!cell.StackLock&&!cell.UserLockedSlot&&HitSlot(cell,ray))
            {
                RebirthCamperTrace.Write("drag.end-backpack",BoundVehicle,"slot="+cell.SlotNumber);
                cell.HandleStackSwap();return;
            }
    }
    private static bool HitSlot(XUiController slot,UnityEngine.Ray ray)
    {
        var view=slot?.ViewComponent;
        var collider=view?.UiTransform?.GetComponent<UnityEngine.BoxCollider>();
        UnityEngine.RaycastHit hit;
        return view!=null&&view.IsVisible&&collider!=null&&collider.enabled
            &&collider.gameObject.activeInHierarchy&&collider.Raycast(ray,out hit,10000f);
    }
    internal void InstallPreferred(XUiC_RebirthBackpackSectionNativeSlot source)
    {
        RebirthCamperTrace.Write("preferred.install",BoundVehicle,"source="+source?.SlotNumber+" current="+CanInteract+" accepts="+(source!=null&&Accepts(source.ItemStack))+" cursorEmpty="+xui.DragAndDropWindow?.IsEmpty());
        // Native AttributeLock marks encumbered cells for presentation; it does
        // not prevent moving their contents. Honor actual inventory locks.
        if (source == null || source.IsStorage || source.UserLockedSlot || source.IsLocked || source.StackLock
            || !CanInteract || !Accepts(source.ItemStack) || xui.DragAndDropWindow == null
            || !xui.DragAndDropWindow.IsEmpty())
        {ReportRefusal("preferred-install.admission",-1,source?.SlotNumber??-1,"Source/current/cursor admission refused",source?.UserLockedSlot,source?.StackLock,source?.IsLocked);return;}
        int destination = -1;
        for (int index = 0; index < slots.Length; index++)
        {
            var installed = read(index);
            if (installed == null || installed.IsEmpty()) { destination = index; break; }
        }
        if (destination < 0){ReportRefusal("preferred-install.destination",-1,source.SlotNumber,"No free module destination",source.UserLockedSlot,source.StackLock,source.IsLocked);return;}
        // Quick transfers retain native backpack custody throughout preparation.
        preferredInstallation=destination;
        try
        {
            quickTransfer?.Invoke(destination,source.SlotNumber);
            var installed=read(destination);
            if(installed==null || installed.IsEmpty()){ReportRefusal("preferred-install.result",destination,source.SlotNumber,"Coordinator did not install; inspect authoritative refusal/pending save",source.UserLockedSlot,source.StackLock,source.IsLocked);preferredInstallation=-1;}
        }
        catch(Exception error) {ReportRefusal("preferred-install.exception",destination,source.SlotNumber,error.GetType().Name+": "+error.Message,source.UserLockedSlot,source.StackLock,source.IsLocked);preferredInstallation=-1; throw; }
    }
    internal void Transfer(int slot)
    {
        RebirthCamperTrace.Write("transfer",BoundVehicle,"slot="+slot+" current="+CanInteract+" cursor="+xui.DragAndDropWindow?.CurrentStack?.count);
        if (!CanInteract){ReportRefusal("cursor.admission",slot,-1,"Current management session unavailable");return;}
        var cursor = xui.GetChildByType<XUiC_DragAndDropWindow>();
        ItemStack source = cursor == null ? ItemStack.Empty : cursor.CurrentStack;
        if (source != null && !source.IsEmpty() && !Accepts(source)){ReportRefusal("cursor.item",slot,-1,"Held item is not accepted by this module view");return;}
        // Replacing an installed module requires a separate removal, including
        // capacity checks; the native part swap must never replace its clone.
        if (source != null && !source.IsEmpty() && read(slot) != null && !read(slot).IsEmpty()){ReportRefusal("cursor.destination",slot,-1,"Module already installed; remove separately");return;}
        // Coordinator reserves cursor custody and applies the authenticated reply;
        // do not clear/debit it here after merely sending a request.
        try
        {
            if (transfer(slot, source == null ? ItemStack.Empty : source.Clone())) RefreshModules();
            else ReportRefusal("cursor.result",slot,-1,"Coordinator returned false; may be refused or awaiting native save; remote details require server log");
        }
        catch(Exception error){ReportRefusal("cursor.exception",slot,-1,error.GetType().Name+": "+error.Message);throw;}
    }
    internal void RemovePreferred(int slot)
    {
        RebirthCamperTrace.Write("preferred.remove",BoundVehicle,"slot="+slot+" current="+CanInteract+" cursorEmpty="+xui.DragAndDropWindow?.IsEmpty());
        if(!CanInteract || xui.DragAndDropWindow==null || !xui.DragAndDropWindow.IsEmpty()){ReportRefusal("preferred-remove.admission",slot,-1,"Current management/cursor admission refused");return;}
        var bag=RebirthCraftingInventoryBridge.GetBag(xui);
        var backpack=GetChildByType<XUiC_RebirthBackpackSectionBackpack>();
        int destination=backpack?.FindQuickRemovalDestination()??-1;
        if(bag==null||destination<0){ReportRefusal("preferred-remove.destination",slot,destination,"No native unlocked empty backpack destination");return;}
        preferredRemoval=slot;
        try
        {
            quickTransfer?.Invoke(slot,destination);
            var remaining=read(slot);
            // Both admitted delegates disable accepts while a transfer is
            // pending. An unchanged installed item with transfers enabled
            // means planning refused the gesture without taking custody.
            if(preferredRemoval>=0 && remaining!=null && !remaining.IsEmpty() && accepts(remaining))
            {ReportRefusal("preferred-remove.result",slot,destination,"Installed module unchanged with admission enabled; coordinator refused planning");preferredRemoval=-1;}
        }
        catch(Exception error) {ReportRefusal("preferred-remove.exception",slot,destination,error.GetType().Name+": "+error.Message);preferredRemoval=-1; throw; }
    }
    internal void ReportRefusal(string phase,int module,int source,string reason,bool? uiUserLocked=null,bool? uiStackLocked=null,bool? uiLocked=null)
    {
        // Only callers at actual gesture/refusal boundaries use this method.
        // Keep the budget across refreshes; only an actual OnOpen resets it.
        if(refusalWarnings>=16)return;refusalWarnings++;
        var bag=xui?.playerUI?.entityPlayer?.bag;var locks=bag?.LockedSlots;
        string nativeLock=source>=0&&locks!=null&&source<locks.Length?locks[source].ToString():"unknown";
        var cargoLocks=BoundVehicle?.bag?.ItemGrid?.SlotLocks;int lockedCargo=0;
        for(int i=0;cargoLocks!=null&&i<cargoLocks.Length;i++)if(cargoLocks[i])lockedCargo++;
        string clean=(reason??"").Replace('\r',' ').Replace('\n',' ');if(clean.Length>360)clean=clean.Substring(0,360);
        string detail="phase="+phase+" module="+module+" source="+source+" ownerLocked="+nativeLock+
            " uiUserLocked="+(uiUserLocked?.ToString()??"unknown")+" uiStackLocked="+(uiStackLocked?.ToString()??"unknown")+" uiLocked="+(uiLocked?.ToString()??"unknown")+
            " lockEditMode="+(GetChildByType<XUiC_RebirthBackpackSectionBackpack>()?.LockMode.ToString()??"unknown")+
            " cargoSlots="+(BoundVehicle?.bag?.ItemGrid?.Length??-1)+" cargoLockLength="+(cargoLocks?.Length??-1)+" cargoLockedCount="+lockedCargo+" reason="+clean;
        Log.Warning("[REBIRTH CamperRefusal] "+detail);
        RebirthCamperTrace.Write("management.refused",BoundVehicle,detail);
    }
    internal void CompletePreferredRemoval(RebirthCamperTransferSnapshot before,RebirthCamperTransferSnapshot after)
    {
        if(after.BackpackSlot>=0)
        {
            preferredInstallation=-1;preferredRemoval=-1;
            GetChildByType<XUiC_RebirthBackpackSectionBackpack>()?.SetStacks(RebirthCraftingInventoryBridge.GetBag(xui).ItemGrid.items);
            RefreshModules();return;
        }
        if(preferredInstallation>=0 && CanInteract)
        {
            int target=preferredInstallation;
            bool installationConfirmed=crafting
                ? before.Stations.GetSlot(target)==null && after.Stations.GetSlot(target)!=null
                : before.Storage.GetSlot(target)==null && after.Storage.GetSlot(target)!=null;
            var held=xui.DragAndDropWindow;
            if(installationConfirmed && held!=null && RebirthCamperRemoteStationGridTransfer.Same(held.CurrentStack,after.Cursor))
            {
                preferredInstallation=-1;
                // Only an acknowledged quick installation returns its unused
                // native cursor remainder. Drag gestures keep that remainder.
                if(!held.IsEmpty())held.PlaceItemBackInInventory();
                RefreshModules();
            }
        }
        if(preferredRemoval<0 || !CanInteract)return;
        int slot=preferredRemoval;
        bool removed=crafting ? before.Stations.GetSlot(slot)!=null&&after.Stations.GetSlot(slot)==null
            : before.Storage.GetSlot(slot)!=null&&after.Storage.GetSlot(slot)==null;
        if(!removed)return;
        var confirmedCursor=after.Cursor;
        if(confirmedCursor?.itemValue?.Metadata?.ContainsKey(RebirthCamperStationRemovalContents.ContentsKey)==true)
            confirmedCursor=RebirthCamperStationRemovalContents.WithoutContents(confirmedCursor);
        var installed=read(preferredRemoval);
        if(installed!=null&&!installed.IsEmpty())return;
        var cursor=xui.DragAndDropWindow;
        if(cursor==null || confirmedCursor==null || confirmedCursor.IsEmpty()
            || !RebirthCamperRemoteStationGridTransfer.Same(cursor.CurrentStack,confirmedCursor))return;
        preferredRemoval=-1;
        cursor.PlaceItemBackInInventory();
        RefreshModules();
    }
    internal void CancelPreferredRemoval() { preferredRemoval=-1; preferredInstallation=-1; }
}

[Preserve]
public sealed class XUiC_RebirthCamperModuleSlot : XUiC_BasePartStack
{
    private XUiC_RebirthCamperModules owner;
    private bool dragWired;
    private bool presented;
    public void Bind(XUiC_RebirthCamperModules view, int slot)
    {
        owner = view; SlotNumber = slot; StackLocation = XUiC_ItemStack.StackLocationTypes.Part;
        if(!dragWired)
        {
            dragWired=true;
            OnDrag+=(sender,type,delta)=>{if(type==EDragType.DragEnd)owner?.DropHeldAtPointer();};
        }
        InfoWindow = null; emptySpriteName = string.Empty;
        selectColor = new UnityEngine.Color32(76,55,100,255);
        pressColor = selectColor;
        holdingColor = selectColor;
    }
    public void Present(ItemStack stack)
    {
        // Refresh requests inspect the complete authoritative snapshot, but
        // unchanged cells must not run the native setter: fresh clones make
        // every cell dirty, and empty cells also trigger SelectedChanged(false).
        bool empty = stack == null || stack.IsEmpty();
        if (presented && (empty ? ItemStack == null || ItemStack.IsEmpty()
            : ItemStack != null && ItemStack.Equals(stack))) return;
        presented = true;
        ItemStack = stack == null ? ItemStack.Empty.Clone() : stack.Clone();
        // The native ItemStack setter already refreshes the changed bindings.
        ApplySlotBorder();
    }
    public override string GetAtlas() => "ItemIconAtlas";
    public override string GetPartName() => ItemStack == null || ItemStack.IsEmpty() ? string.Empty : ItemClass.GetLocalizedItemName();
    public override void SetEmptySpriteName() { emptySpriteName = string.Empty; }
    public override bool CanSwap(ItemStack stack) => owner != null && owner.Accepts(stack);
    public override bool CanRemove() => owner != null && owner.CanInteract && ItemStack != null && !ItemStack.IsEmpty();
    public override void SwapItem() { owner?.Transfer(SlotNumber); }
    public override void OnHovered(bool over) { if (over) owner?.Inspect(ItemStack); base.OnHovered(over); ApplySlotBorder(); }
    public override void SelectedChanged(bool selected)
    {
        // Native part rows replace this sprite with menu_empty/select_row.
        // Keep module-cell geometry stable at the selection event itself.
        // The native override only writes sprite/color; selection ownership
        // remains in XUiC_SelectableEntry.IsSelected, which invokes this hook.
        // Native deselection can invoke the hook before updating IsSelected.
        ApplySlotBorder(selected);
    }
    public override void Update(float dt)
    {
        // Route this native gesture directly to the acknowledged owner. The
        // non-virtual native preferred method is also patched for gamepad calls.
        if(isOver && ViewComponent.UiTransformIsHovered && InputUtils.ShiftKeyPressed
            && xui.playerUI.CursorController.GetMouseButtonUp(UICamera.MouseButton.LeftButton))
        { TransferPreferred(); ApplySlotBorder(); return; }
        // Native part input arms a release only when the press began in this
        // slot. Arm the same handler when a held native cursor enters during
        // a drag, so releasing over a module uses its authenticated transfer.
        if(isOver && ViewComponent.UiTransformIsHovered && xui.DragAndDropWindow?.IsEmpty()==false
            && xui.playerUI.CursorController.GetMouseButton(UICamera.MouseButton.LeftButton))lastClicked=true;
        base.Update(dt);
        ApplySlotBorder();
    }
    private void ApplySlotBorder(bool? selected = null)
    {
        var fill = background?.ViewComponent as XUiV_Sprite;
        if (fill == null) return;
        // Use the native item-stack border with a separate persistent fill.
        // BasePartStack normally paints an entire selected row; these are slots.
        var color = isOver || (selected ?? IsSelected) ? new UnityEngine.Color32(181,140,255,255)
            : new UnityEngine.Color32(96,96,96,255);
        if (fill.SpriteName != "menu_empty3px") fill.SpriteName = "menu_empty3px";
        if (fill.Sprite != null && fill.Sprite.spriteName != "menu_empty3px")
            fill.SetSpriteImmediately("menu_empty3px");
        UnityEngine.Color desiredColor = color;
        if (fill.Color != desiredColor) fill.Color = desiredColor;
        if (fill.Sprite != null && fill.Sprite.color != desiredColor)
            fill.SetColorImmediately(desiredColor);
    }
    internal void TransferPreferred()
    {
        // Native shift-click directly adds this view's clone to the backpack.
        // Route to the same committed cursor removal instead. A held cursor
        // already owns an item and cannot become a second transfer destination.
        if (owner == null || !CanRemove() || xui.DragAndDropWindow == null
            || !xui.DragAndDropWindow.IsEmpty()) return;
        owner.RemovePreferred(SlotNumber);
    }
}
