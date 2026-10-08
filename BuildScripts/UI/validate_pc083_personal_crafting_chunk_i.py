from pathlib import Path
from lxml import etree
import re, sys

root=Path(__file__).resolve().parents[2]
checks=[]
def check(name,ok,detail=''):
    checks.append((name,bool(ok),detail))

files={
    'windows':root/'Config/XUi_InGame/windows.xml',
    'templates':root/'Config/XUi_InGame/templates.xml',
    'xui':root/'Config/XUi_InGame/xui.xml',
    'loc':root/'Config/Localization.csv',
    'root':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs',
    'layout':root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs',
    'state':root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingState.cs',
    'queue':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs',
    'entry':root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs',
    'bridge':root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingQueueBridge.cs',
}
for n,p in files.items(): check('exists '+p.relative_to(root).as_posix(),p.exists())
for n in ['windows','templates','xui']:
    try: etree.parse(str(files[n])); check('xml parse '+files[n].name,True)
    except Exception as e: check('xml parse '+files[n].name,False,str(e))

wt=files['windows'].read_text(errors='ignore'); tt=files['templates'].read_text(errors='ignore')
xt=files['xui'].read_text(errors='ignore'); loc=files['loc'].read_text(errors='ignore')
ot=files['root'].read_text(errors='ignore'); lt=files['layout'].read_text(errors='ignore')
st=files['state'].read_text(errors='ignore'); qt=files['queue'].read_text(errors='ignore')
et=files['entry'].read_text(errors='ignore'); bt=files['bridge'].read_text(errors='ignore')

# Rebirth-only isolation remains intact.
check('rebirth-only conditional retained',"character_progression('Rebirth')" in xt)
check('custom Rebirth crafting controller retained','RebirthPersonalCrafting, RebirthUtils' in xt)
check('native crafting children removed only in Rebirth branch','<remove xpath="/xui/window_group[@name=\'crafting\']/window"/>' in xt)
check('base group not globally replaced','<set xpath="/xui/window_group[@name=\'crafting\']"' not in xt)
check('root still captures XUiC_CraftingQueue contract','craftingQueue = GetChildByType<XUiC_CraftingQueue>();' in ot)

# Visible queue owns the one real runtime queue; hidden Chunk-E duplicate is gone.
qstart=wt.find('<rect name="rebirthCraftingQueueRegion"')
qwindow=wt[qstart:qstart+7000] if qstart>=0 else ''
check('queue region exists',qstart>=0)
check('visible queue controller exists','name="rebirthCraftingQueueController"' in qwindow)
check('visible queue uses Rebirth native subclass','controller="RebirthCraftingQueue, RebirthUtils"' in qwindow)
check('hidden Chunk-E runtime queue removed','rebirthCraftingRuntimeQueue' not in qwindow)
check('hidden stock recipe_stack queue removed','rebirthCraftingRuntimeQueueSlots' not in qwindow)
check('exactly four current runtime queue entries',qwindow.count('<rebirth_personal_crafting_queue_entry name="rebirthCraftingQueueSlot')==4,str(qwindow.count('<rebirth_personal_crafting_queue_entry name="rebirthCraftingQueueSlot')))
check('no fake 24 queue slots',all(x not in qwindow for x in ['rows="24"','cols="24"','capacity="24"','QueueCapacity = 24']))
check('active capacity binding authored','text="{rebirthqueueactivecapacity}"' in qwindow)

# Scroll future-proofing: standard scrollbar exists but hides at current four-slot capacity.
check('queue rows host authored','name="rebirthCraftingQueueRowsHost"' in qwindow)
check('queue scrollbar host authored','name="rebirthCraftingQueueScrollHost"' in qwindow)
check('queue uses stock defaultscrollbar','<defaultscrollbar/>' in qwindow)
check('queue has UIScrollView proxy','name="rebirthCraftingQueueScrollView"' in qwindow and 'name="rebirthCraftingQueueScrollProxy"' in qwindow)
check('scrollbar initially hidden for four slots','name="rebirthCraftingQueueScrollHost"' in qwindow and 'visible="false"' in qwindow)
check('queue controller detects capacity beyond visible rows','capacity > RebirthCraftingQueueBridge.VisibleRows' in qt)
check('queue controller maps scrollbar to first visible row','firstVisible' in qt and 'RebirthNativeScrollbarUtil.TryGetValue' in qt)
check('queue controller pushes row scroll to native scrollbar','RebirthNativeScrollbarUtil.TrySetValue' in qt)
check('mouse wheel scroll supported','OnScroll += HandleScroll' in qt and 'ScrollByDeltaInternal' in qt)
check('entry wheel events are wired once by queue','entries[i].OnScroll += HandleScroll' in qt and 'cancelHit.OnScroll += HandleScroll' in qt and 'protected override void OnScrolled' not in et)
check('visible row count is four, not capacity','VisibleRows = 4' in bt)

# Native queue authority is preserved rather than reimplemented.
check('queue subclass is native XUiC_CraftingQueue','XUiC_RebirthCraftingQueue : XUiC_CraftingQueue' in qt)
check('queue calls native Init','base.Init();' in qt)
check('queue calls native OnOpen','base.OnOpen();' in qt)
check('queue calls native Update','base.Update(dt);' in qt)
check('queue capacity derived from GetRecipesToCraft','queue.GetRecipesToCraft()' in bt)
check('active count derived from HasRecipe','entries[i].HasRecipe()' in bt)
check('queue does not manually call ClearQueue','ClearQueue(' not in qt)
check('queue does not manually pause crafting','HaltCrafting(' not in qt and 'ResumeCrafting(' not in qt)
check('queue does not manually shift recipes','CopyTo(' not in qt and 'SetRecipe(' not in qt)
check('bridge is read-only helper','SetRecipe(' not in bt and 'ClearRecipe(' not in bt and 'ForceCancel(' not in bt)

# Native entry authority and concept presentation.
check('entry subclass is native XUiC_RecipeStack','XUiC_RebirthCraftingQueueEntry : XUiC_RecipeStack' in et)
check('entry calls native Update transaction','base.Update(dt);' in et)
check('entry does not replace native output behavior','outputStack(' not in et and 'AddItemNoPartial' not in et)
check('entry does not manually refund ingredients','PlayerInventory.AddItem' not in et)
check('entry never calls ForceCancel','ForceCancel(' not in et)
check('execution order reversed for active row at top','NativeIndexToDisplayOrder' in bt and 'capacity - 1 - nativeIndex' in bt)
check('entry exposes queue index binding','case "rebirthqueueindex"' in et)
check('entry exposes localized recipe name binding','case "rebirthqueuename"' in et and 'GetLocalizedRecipeName(this)' in et)
check('entry exposes crafting/queued/empty status','case "rebirthqueuestatus"' in et and 'xuiRebirthQueueCrafting' in et and 'xuiRebirthQueueQueued' in et and 'xuiRebirthQueueEmpty' in et)
check('entry progress derives native timers','GetOneItemCraftTime()' in bt and 'GetRecipeCraftingTimeLeft()' in bt)
check('progress is current item, not invented probability','GetCurrentItemProgress' in bt and 'Success' not in et)
check('native timer child retained','name="timer"' in tt)
check('native count child retained','name="count"' in tt)
check('item icon uses native RecipeStack child id','name="itemIcon"' in tt)
check('entry has compact index icon name status progress time qty cancel',all(x in tt for x in [
    'name="rebirthQueueIndex"','name="itemIcon"','name="rebirthQueueName"','name="rebirthQueueStatus"',
    'name="rebirthQueueProgressFill"','name="timer"','name="count"','name="cancel"']))

# Dedicated cancel control: only X hit target invokes native RecipeStack HandleOnPress.
tstart=tt.find('<rebirth_personal_crafting_queue_entry>')
tend=tt.find('</rebirth_personal_crafting_queue_entry>',tstart)
template=tt[tstart:tend] if tstart>=0 and tend>tstart else ''
check('queue entry template exists',bool(template))
check('native cancel hit target is compact 30x30','name="background"' in template and 'width="30" height="30"' in template)
check('cancel hit target is pressable','name="background"' in template and 'on_press="true"' in template)
check('cancel hit target is gamepad selectable','name="background"' in template and 'gamepad_selectable="true"' in template)
check('visible cancel X authored','name="cancel"' in template and 'sprite="ui_game_symbol_x"' in template)
check('card body is not the native cancel background','name="rebirthQueueCardBg"' in template and 'name="background"' in template)
check('entry forces dedicated cancel visibility after native refresh','ForceDedicatedCancelPresentation();' in et)
check('entry leaves native HandleOnPress implementation untouched','HandleOnPress(' not in et)
check('no fake pause control','Pause' not in template and 'pause' not in template)
check('no fake clear-all control','Clear Queue' not in template and 'clearqueue' not in template.lower())

# Responsive right-column geometry and queue state integration.
check('root layout sizes visible queue controller','SetRect(owner.GetChildById("rebirthCraftingQueueController"), 0, 0, width, height);' in lt)
check('queue row height adapts to region','availableHeight' in qt and 'MinRowHeight' in qt and 'MaxRowHeight' in qt)
check('queue width reserves scrollbar only when needed','needsScroll ? ScrollbarWidth + 8 : 0' in qt)
check('queue records state in root coordinator',('owner?.State.RecordQueue(active, capacity);' in qt) or ('owner?.Coordinator?.RecordQueue(active, capacity);' in qt))
check('state stores queue active count','QueueActiveCount' in st)
check('state stores queue capacity','QueueCapacity' in st)
check('state has queue revision','QueueRevision' in st)
check('state RecordQueue only increments on change',('if (QueueActiveCount == activeCount && QueueCapacity == capacity)' in st) or ('state.QueueActiveCount == activeCount && state.QueueCapacity == capacity' in (root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingCoordinator.cs').read_text(errors='ignore')))

# No overlap / no geometry takeover introduced by the queue.
check('queue controller never resizes center zone','rebirthCraftingCenterZone' not in qt)
check('queue controller never resizes inventory','rebirthCraftingInventoryRegion' not in qt)
check('queue controller never resizes requirements','rebirthCraftingRequirementsRegion' not in qt)
check('queue entry never opens native item info','ItemInfoWindow' not in et)
check('queue entry never changes recipe catalogue selection','CurrentRecipe' not in et)

# Existing full-backpack invariants remain untouched.
inv=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs').read_text(errors='ignore')
scroll=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs').read_text(errors='ignore')
check('crafting inventory remains full Bag subclass','XUiC_RebirthCraftingInventory : XUiC_Backpack' in inv)
check('inventory visible columns use wide twelve-column viewport','Columns = 12' in (root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs').read_text(errors='ignore'))
check('inventory visible rows remain four','VisibleRows = 4' in (root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs').read_text(errors='ignore'))
check('inventory scrolling remains independent','XUiC_RebirthCraftingInventoryScroll' in scroll)
check('no 32-slot clamp introduced by Chunk I',all(x not in '\n'.join([qt,et,bt,template]) for x in ['MaxItemCount = 32','Take(32)','new ItemStack[32]']))

# Localization uniqueness.
for key in ['xuiRebirthQueueActiveCount','xuiRebirthQueueTime','xuiRebirthQueueQty','xuiRebirthQueueCancelTooltip']:
    check('localization '+key,loc.count(key+',')==1,str(loc.count(key+',')))

# PC079 validator must recognize the planned Chunk-I successor architecture.
pc079=(root/'BuildScripts/UI/validate_pc079_personal_crafting_chunk_e.py').read_text(errors='ignore')
check('PC079 validator accepts Chunk I queue successor','chunk_i_queue' in pc079 and 'no duplicate hidden and visible runtime queues' in pc079)

# Source delimiter smoke checks.
for key in ['root','layout','state','queue','entry','bridge']:
    text=files[key].read_text(errors='ignore')
    check('brace balance '+files[key].name,text.count('{')==text.count('}'),f"{text.count('{')}/{text.count('}')}")
    check('paren balance '+files[key].name,text.count('(')==text.count(')'),f"{text.count('(')}/{text.count(')')}")

passed=sum(1 for _,ok,_ in checks if ok); failed=[x for x in checks if not x[1]]
print(f'PC083 CHUNK I VALIDATION: {passed} PASS / {len(failed)} FAIL')
for name,ok,detail in failed: print('FAIL -',name,detail)
if failed: sys.exit(1)
