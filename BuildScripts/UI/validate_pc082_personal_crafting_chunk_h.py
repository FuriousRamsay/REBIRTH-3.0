from pathlib import Path
from lxml import etree
import re, sys

root=Path(__file__).resolve().parents[2]
checks=[]
def check(name,ok,detail=''):
    checks.append((name,bool(ok),detail))

files={
    'windows':root/'Config/XUi_InGame/windows.xml',
    'xui':root/'Config/XUi_InGame/xui.xml',
    'loc':root/'Config/Localization.csv',
    'context':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs',
    'slot':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs',
    'inventory':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs',
    'scroll':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs',
    'layout':root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs',
    'details':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeDetails.cs',
    'requirements':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRequirements.cs',
}
for n,p in files.items(): check('exists '+p.relative_to(root).as_posix(),p.exists())
for n in ['windows','xui']:
    try: etree.parse(str(files[n])); check('xml parse '+files[n].name,True)
    except Exception as e: check('xml parse '+files[n].name,False,str(e))

wt=files['windows'].read_text(errors='ignore'); xt=files['xui'].read_text(errors='ignore')
ct=files['context'].read_text(errors='ignore'); st=files['slot'].read_text(errors='ignore')
it=files['inventory'].read_text(errors='ignore'); sct=files['scroll'].read_text(errors='ignore')
lt=files['layout'].read_text(errors='ignore'); dt=files['details'].read_text(errors='ignore')
rt=files['requirements'].read_text(errors='ignore'); loc=files['loc'].read_text(errors='ignore')

# Rebirth-only isolation / stock Base Game remains outside this branch.
check('rebirth-only conditional retained',"character_progression('Rebirth')" in xt)
check('custom Rebirth crafting controller retained','RebirthPersonalCrafting, RebirthUtils' in xt)
check('native backpack auto-open remains disabled only in Rebirth branch','name="open_backpack_on_open">false</setattribute>' in xt)
check('native itemInfo excluded from Rebirth crafting group by full native-child replacement',"<remove xpath=\"/xui/window_group[@name='crafting']/window\"/>" in xt and '<window name=\"rebirthPersonalCraftingRoot\" anchor=\"Center\"/>' in xt)
check('no unconditional base crafting replacement','<set xpath="/xui/window_group[@name=\'crafting\']"' not in xt)

# Find the custom Personal Crafting root and inventory region.
rstart=wt.find('<window name="rebirthPersonalCraftingRoot"')
rend=wt.find('</window>',rstart)
rootwin=wt[rstart:rend if rend>rstart else None] if rstart>=0 else ''
istart=wt.find('<rect name="rebirthCraftingInventoryRegion"',rstart)
iend=wt.find('<rect name="rebirthCraftingRightZone"',istart)
iwindow=wt[istart:iend if iend>istart else None] if istart>=0 else ''
cstart=wt.find('<rect name="rebirthCraftingItemContext"',rstart)
cend=wt.find('<rect name="rebirthCraftingRequirementsRegion"',cstart)
cwindow=wt[cstart:cend if cend>cstart else None] if cstart>=0 else ''
check('custom context is outside inventory and in top-center context sequence','name="rebirthCraftingItemContext"' in cwindow and 'name="rebirthCraftingItemContext"' not in iwindow)
check('custom context controller authored','controller="RebirthCraftingItemContext, RebirthUtils"' in cwindow)
check('context initially hidden','name="rebirthCraftingItemContext"' in cwindow and 'visible="false"' in cwindow)
check('context blocks click-through','name="rebirthCraftingItemContext"' in cwindow and 'disablefallthrough="true"' in cwindow)
check('context has dark Rebirth surface','name="rebirthCraftingItemContextBg"' in cwindow and 'color="17,17,21,248"' in cwindow)
check('context has red primary frame','name="rebirthCraftingItemContextFrame"' in cwindow and 'color="64,64,72,255"' in cwindow)
check('context has purple rule/title language','name="rebirthCraftingItemContextHeaderRule"' in cwindow and '228,18,21' in cwindow and '196,158,255' in cwindow)
check('context has no close X control','name="btnRebirthCraftingItemContextClose"' not in cwindow)
check('context has item icon/name/summary/description',all(x in cwindow for x in [
    'rebirthCraftingItemContextIcon','rebirthCraftingItemContextName','rebirthCraftingItemContextSummary','rebirthCraftingItemContextDescription']))
check('context exposes tooltip binding','tooltip="{rebirthitemcontexttooltip}"' in cwindow)

# Native action authority, custom visual shell.
check('context uses native ItemActionList','name="rebirthCraftingItemActionList"' in cwindow and 'controller="ItemActionList"' in cwindow)
check('exactly five visible native action entry hosts',len(re.findall(r'name="rebirthCraftingItemAction[0-4]"[^>]*controller="ItemActionEntry"',cwindow))==5)
for i in range(5):
    marker='name="rebirthCraftingItemAction'+str(i)+'"'
    pos=cwindow.find(marker)
    next_pos=cwindow.find('name="rebirthCraftingItemAction'+str(i+1)+'"',pos+1) if i < 4 else cwindow.find('</rect>\n          </rect>',pos)
    block=cwindow[pos:next_pos if next_pos>pos else len(cwindow)] if pos>=0 else ''
    check('action '+str(i)+' has native required child contract',all(x in block for x in ['name="background"','name="icon"','name="name"','name="gamepadIcon"','name="keyboardButton"']))
check('context calls native Item action list type','SetCraftingActionList(XUiC_ItemActionList.ItemActionListTypes.Item, selectedSlot)' in ct)
check('context clears native action events/list on close','ItemActionListTypes.None' in ct)
check('context does not hand-author Use action','new ItemActionEntryUse' not in ct)
check('context does not hand-author Drop action','new ItemActionEntryDrop' not in ct)
check('context does not hand-author Scrap action','new ItemActionEntryScrap' not in ct)
check('context does not hand-author Equip action','new ItemActionEntryEquip' not in ct)
check('context does not hand-author Repair action','new ItemActionEntryRepair' not in ct)

# Slot selection is custom and stock ItemInfo is hard-blocked.
check('custom slot overrides native item-info refresh','public override void updateItemInfoWindow' in st)
slot_no_comments=re.sub(r'//.*','',st)
check('slot item-info override never calls base',re.search(r'base\.updateItemInfoWindow\s*\(',slot_no_comments) is None)
check('slot click completion notifies context','public override void HandleClickComplete()' in st and 'context.SelectSlot(this)' in st)
check('empty slot clears stale context after native click semantics','context.ClearSelection()' in st and 'base.HandleClickComplete()' in st)
check('controller focus still auto-scrolls','EnsureSlotVisible(SlotNumber)' in st)
check('inventory clears inherited InfoWindow before base projection','itemControllers[i].InfoWindow = null' in it and it.find('itemControllers[i].InfoWindow = null') < it.find('base.SetStacks(stackList)'))
check('inventory clears inherited InfoWindow after base projection','slot.InfoWindow = null' in it and it.find('slot.InfoWindow = null') > it.find('base.SetStacks(stackList)'))
check('context repeats ItemInfo null guard on selection','selectedSlot.InfoWindow = null' in ct)
check('no ItemInfoWindow controller is authored in custom context','ItemInfoWindow' not in cwindow)

# Selected Item is now an approved state-alternative for the Selected Recipe surface.
check('context never changes catalogue CurrentRecipe','CurrentRecipe =' not in ct and '.CurrentRecipe=' not in ct)
check('context swaps Selected Recipe visibility','SetRecipeDetailsVisible(false)' in ct and 'SetRecipeDetailsVisible(true)' in ct)
check('context never resizes requirements region','rebirthCraftingRequirementsRegion' not in ct)
check('context never resizes queue region','rebirthCraftingQueueRegion' not in ct)
check('root layout service sizes item context to details surface','rebirthCraftingItemContext' in lt and 'rebirthCraftingDetailsRegion' in lt)
check('existing details controller remains recipe-owned','XUiC_RebirthCraftingRecipeDetails' in dt and 'selectedRecipe' in dt)
check('existing requirements controller remains independent','XUiC_RebirthCraftingRequirements' in rt and 'CurrentRecipe' in rt)

# In-place context swap keeps selected source visible while using the top detail surface.
check('context occupies the selected recipe surface','true context swap' in ct.lower() and 'recipeDetailsRegion.ViewComponent.Size' in ct)
check('context closes if selected source scrolls away','!scroll.IsSlotVisible(selectedSlot.SlotNumber)' in ct and ('HideContext(true)' in ct or 'HideContext(true, true)' in ct))
check('scroll exposes visible-row query','public int GetVisibleRowIndex(int slotNumber)' in sct)
check('scroll exposes visible-slot query','public bool IsSlotVisible(int slotNumber)' in sct)
check('scroll still uses four visible rows','VisibleRows = RebirthCraftingInventoryBridge.VisibleRows' in sct)
check('context does not own backpack viewport constants','Columns =' not in ct and 'VisibleRows =' not in ct)

# Refresh after real item action mutations.
check('context subscribes to real backpack change event','OnBackpackItemsChanged += BackpackItemsChanged' in ct)
check('context unsubscribes on close','OnBackpackItemsChanged -= BackpackItemsChanged' in ct)
check('bag mutation refreshes custom inventory','RefreshAuthoritativePresentation(true)' in ct)
check('bag mutation refreshes requirements','GetChildByType<XUiC_RebirthCraftingRequirements>()?.RefreshNow()' in ct)
check('bag mutation refreshes selected recipe outcome chain','GetChildByType<XUiC_RebirthCraftingRecipeDetails>()?.RefreshNow()' in ct)
check('item fingerprint detects stack mutation','BuildFingerprint(selectedSlot.ItemStack)' in ct and 'value.UseTimes.GetHashCode()' in ct and 'stack.count' in ct)

# Item information is real item data, not fake combat/crafting stats.
check('item icon comes from real ItemClass','itemClass.GetIconName()' in ct and 'itemClass.GetIconTint(value)' in ct)
check('item name comes from native ItemStack','selectedSlot.ItemNameText' in ct)
check('description uses real item description key','GetItemDescriptionKey()' in ct)
check('summary uses real quality when present','value.HasQuality' in ct and 'value.Quality' in ct)
check('summary uses real durability when present','value.MaxUseTimes > 0' in ct and 'value.PercentUsesLeft' in ct)
check('no invented damage stat','Damage' not in ct)
check('no invented success/failure stat','Success Chance' not in ct and 'Failure' not in ct)

# Existing full backpack behavior remains intact from Chunk G.
check('inventory remains XUiC_Backpack behavior subclass','XUiC_RebirthCraftingInventory : XUiC_Backpack' in it)
check('custom inventory uses twelve columns','cols="12"' in iwindow)
check('custom inventory still authors >4 rows','rows="7"' in iwindow)
check('scrollbar track/thumb remain authored','rebirthCraftingInventoryScrollTrack' in iwindow and 'rebirthCraftingInventoryScrollThumb' in iwindow)
check('encumbered boundary remains native CarryCapacity-backed','GetUnencumberedSlotCount(xui)' in it and 'AttributeLock = authoritative && i >= unencumbered' in it)
check('no 32-slot capacity clamp introduced',all(b not in '\n'.join([ct,st,it,sct,iwindow]) for b in ['MaxItemCount = 32','Take(32)','new ItemStack[32]']))

# Localization uniqueness.
for key in ['xuiRebirthSelectedItem','xuiRebirthItemContextCloseTooltip','xuiRebirthItemReady','xuiRebirthCount']:
    check('localization '+key,loc.count(key+',')==1,str(loc.count(key+',')))

# Source delimiter smoke checks.
for key in ['context','slot','inventory','scroll']:
    text=files[key].read_text(errors='ignore')
    check('brace balance '+files[key].name,text.count('{')==text.count('}'),f"{text.count('{')}/{text.count('}')}")
    check('paren balance '+files[key].name,text.count('(')==text.count(')'),f"{text.count('(')}/{text.count(')')}")

passed=sum(1 for _,ok,_ in checks if ok); failed=[x for x in checks if not x[1]]
print(f'PC082 CHUNK H VALIDATION: {passed} PASS / {len(failed)} FAIL')
for name,ok,detail in failed: print('FAIL -',name,detail)
if failed: sys.exit(1)
