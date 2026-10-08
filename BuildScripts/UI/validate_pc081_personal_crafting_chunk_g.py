from pathlib import Path
from lxml import etree
import re, sys, csv

root=Path(__file__).resolve().parents[2]
checks=[]
def check(name,ok,detail=''):
    checks.append((name,bool(ok),detail))

files={
    'windows':root/'Config/XUi_InGame/windows.xml',
    'xui':root/'Config/XUi_InGame/xui.xml',
    'loc':root/'Config/Localization.csv',
    'bridge':root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs',
    'slot':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs',
    'inventory':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs',
    'header':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryBridge.cs',
    'scroll':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs',
    'layout':root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs',
    'generic_patch':root/'Scripts/Survivor/UI/RebirthSurvivorBackpackUiPatches.cs',
}
for n,p in files.items(): check('exists '+p.relative_to(root).as_posix(),p.exists())
for n in ['windows','xui']:
    try: etree.parse(str(files[n])); check('xml parse '+files[n].name,True)
    except Exception as e: check('xml parse '+files[n].name,False,str(e))

wt=files['windows'].read_text(errors='ignore'); xt=files['xui'].read_text(errors='ignore')
bt=files['bridge'].read_text(errors='ignore'); st=files['slot'].read_text(errors='ignore')
it=files['inventory'].read_text(errors='ignore'); ht=files['header'].read_text(errors='ignore')
sct=files['scroll'].read_text(errors='ignore'); lt=files['layout'].read_text(errors='ignore')
gt=files['generic_patch'].read_text(errors='ignore'); loc=files['loc'].read_text(errors='ignore')

# Rebirth-only mode isolation / native Base Game preservation.
check('rebirth-only conditional retained',"character_progression('Rebirth')" in xt)
check('rebirth custom crafting controller retained','name="controller">RebirthPersonalCrafting, RebirthUtils</setattribute>' in xt)
check('rebirth disables automatic native backpack','name="open_backpack_on_open">false</setattribute>' in xt)
check('base game crafting not globally replaced','<set xpath="/xui/window_group[@name=\'crafting\']"' not in xt)

# Inventory XML composition.
istart=wt.find('<rect name="rebirthCraftingInventoryRegion"')
iend=wt.find('<rect name="rebirthCraftingRightZone"',istart)
iwindow=wt[istart:iend if iend>istart else None] if istart>=0 else ''
check('custom inventory region controller','controller="RebirthCraftingInventoryBridge, RebirthUtils"' in iwindow)
check('custom scroll controller','controller="RebirthCraftingInventoryScroll, RebirthUtils"' in iwindow)
check('custom backpack presenter controller','controller="RebirthCraftingInventory, RebirthUtils"' in iwindow)
check('custom item-stack controller','controller="RebirthCraftingInventorySlot, RebirthUtils"' in iwindow)
check('grid has twelve columns','cols="12"' in iwindow)
check('grid authors more than four rows','rows="7"' in iwindow)
check('grid repeats custom slot content','repeat_content="true"' in iwindow)
check('viewport uses clipping','name="rebirthCraftingInventoryViewport"' in iwindow and 'clipping="softclip"' in iwindow)
check('scroll track authored','name="rebirthCraftingInventoryScrollTrack"' in iwindow)
check('scroll thumb authored','name="rebirthCraftingInventoryScrollThumb"' in iwindow)
check('inventory lock control authored','name="btnRebirthCraftingInventoryLock"' in iwindow and 'rb_backpack_lock' in iwindow)
check('inventory sort control authored','name="btnRebirthCraftingInventorySort"' in iwindow and 'rb_backpack_sort' in iwindow)
check('viewport-only comment documents non-capacity','15 x 4 is the visible viewport' in iwindow and 'beyond Bag.SlotCount' in iwindow)

# Wide 15 x 4 VIEWPORT, never a 32-slot storage definition.
check('bridge defines twelve columns','public const int Columns = 12;' in bt)
check('bridge defines four visible rows','public const int VisibleRows = 4;' in bt)
check('authored presentation exceeds 32 slots','public const int AuthoredRows = 9;' in bt and 'AuthoredSlotCount = Columns * AuthoredRows' in bt)
combined='\n'.join([bt,st,it,ht,sct,lt,iwindow])
for bad in ['MaxItemCount = 32','BagSize = 32','capacity = 32','Take(32)','new ItemStack[32]','SetSlots(ItemStack.CreateArray(32']:
    check('no 32-slot clamp '+bad,bad not in combined)

# Authoritative Bag size and safe single-index writes.
check('physical count comes from real Bag slots','bag.GetSlots().Length' in bt)
check('unencumbered count comes from native CarryCapacity','PassiveEffects.CarryCapacity' in bt and 'GetUnencumberedSlotCount' in bt)
check('inventory inherits native Backpack behavior','XUiC_RebirthCraftingInventory : XUiC_Backpack' in it)
check('slot inherits native ItemStack behavior','XUiC_RebirthCraftingInventorySlot : XUiC_ItemStack' in st)
check('custom slot write overrides native handler','public override void HandleSlotChangedEvent' in it)
check('slot write validates physical prefix','slotNumber >= physical' in it)
check('slot write updates one real Bag index','bag.SetSlot(slotNumber' in it)
check('slot write does not call base serialization',re.search(r'\bbase\.HandleSlotChangedEvent\s*\(', re.sub(r'//.*', '', it)) is None)
check('custom inventory never calls Bag.SetSlots','.SetSlots(' not in it and '.SetSlots(' not in bt)
check('hidden tail disconnects slot change event','slot.SlotChangedEvent -= handleSlotChangedDelegate' in it)
check('hidden tail is emptied','ItemStack.Empty.Clone()' in it)
check('tail visibility tied to authoritative physical count','bool authoritative = i < physical' in it and 'IsVisible = authoritative' in it)

# Encumbrance and user locked slots.
check('encumbered slot threshold uses native CarryCapacity bridge','GetUnencumberedSlotCount(xui)' in it and 'AttributeLock = authoritative && i >= unencumbered' in it)
check('encumbered slots use native AttributeLock','slot.AttributeLock = authoritative && i >= unencumbered' in it)
check('locked slots read from Bag.LockedSlots','bag.LockedSlots' in bt)
check('user lock state applied to native ItemStack','slot.UserLockedSlot' in bt)
check('user locks persisted back to Bag','bag.LockedSlots = locked' in bt)
check('lock mode toggles native rectSlotLock','GetChildById("rectSlotLock")' in it)
check('locked slots persisted on close','PersistUserLockedSlots();' in it and 'public override void OnClose()' in it)
check('sort preserves locked slots','SortStacks(_ignoredSlots: ignored)' in it)

# Scrolling, input, row alignment, controller focus.
check('scroll rows derived from physical slots','totalRows = Math.Max(1, (physical + Columns - 1) / Columns)' in sct)
check('scroll max derives content minus viewport','totalRows * effectiveCell - viewportHeight' in sct)
check('four complete rows fit without rounding clipping','cellByHeight = Math.Max(1, availableH / VisibleRows)' in sct and 'Math.Min(cellByWidth, cellByHeight)' in sct)
check('mouse wheel scroll wired','EventOnScroll = true' in sct and 'OnScroll += HandleScroll' in sct)
check('raw mouse wheel fallback','Input.GetAxis("Mouse ScrollWheel")' in sct and 'IsMouseOverInventoryArea()' in sct)
check('wheel moves whole rows','SetPixelOffset(pixelOffset - effectiveCell)' in sct and 'SetPixelOffset(pixelOffset + effectiveCell)' in sct)
check('track clicking supported','track.OnPress += Track_OnPress' in sct and 'normalized * MaxPixelOffset' in sct)
check('thumb dragging supported','thumb.OnDrag += Thumb_OnDrag' in sct and 'EDragType.DragStart' in sct and 'EDragType.DragEnd' in sct)
check('thumb drag snaps to row on release','Mathf.Round(pixelOffset / effectiveCell) * effectiveCell' in sct)
check('native slot press/drag events preserved','slot.ViewComponent.EventOnPress = true' in sct and 'slot.ViewComponent.EventOnDrag = true' in sct)
check('controller selection auto-scrolls slot into view','EnsureSlotVisible(SlotNumber)' in st and 'public void EnsureSlotVisible(int slotNumber)' in sct)

# Generic backpack remains 7-column and independent.
check('generic backpack patch branches custom inventory','__instance is XUiC_RebirthCraftingInventory' in gt)
check('custom branch retains eight columns','grid.Columns = RebirthCraftingInventoryBridge.Columns' in gt)
check('generic branch retains seven-column row math','(physical + 6) / 7' in gt)

# Layout owns inventory internals, not fixed native window geometry.
check('responsive layout calls inventory internals','ApplyInventoryInternalLayout(width, inventoryHeight)' in lt)
check('responsive layout sizes scroll region','rebirthCraftingInventoryScroll' in lt and 'scrollHeight' in lt)

# Localization uniqueness for Chunk G controls.
for key in ['xuiRebirthCraftingInventoryUnencumbered','xuiRebirthCraftingInventoryLockTooltip','xuiRebirthCraftingInventorySortTooltip']:
    check('localization '+key,loc.count(key+',')==1,str(loc.count(key+',')))

# Source smoke checks and no removed identifier residue.
for key in ['bridge','slot','inventory','header','scroll','layout','generic_patch']:
    text=files[key].read_text(errors='ignore')
    check('brace balance '+files[key].name,text.count('{')==text.count('}'),f"{text.count('{')}/{text.count('}')}")
    check('paren balance '+files[key].name,text.count('(')==text.count(')'),f"{text.count('(')}/{text.count(')')}")
check('no stale WheelPixels reference','WheelPixels' not in sct)

passed=sum(1 for _,ok,_ in checks if ok); failed=[x for x in checks if not x[1]]
print(f'PC081 CHUNK G VALIDATION: {passed} PASS / {len(failed)} FAIL')
for name,ok,detail in failed: print('FAIL -',name,detail)
if failed: sys.exit(1)
