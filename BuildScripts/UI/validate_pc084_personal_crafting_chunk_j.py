from pathlib import Path
from lxml import etree
import math, re, sys

root = Path(__file__).resolve().parents[2]
checks = []

def check(name, ok, detail=''):
    checks.append((name, bool(ok), detail))

def txt(rel):
    p = root / rel
    return p.read_text(errors='ignore') if p.exists() else ''

files = {
    'windows':'Config/XUi_InGame/windows.xml',
    'templates':'Config/XUi_InGame/templates.xml',
    'xui':'Config/XUi_InGame/xui.xml',
    'root':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs',
    'state':'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingState.cs',
    'coord':'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingCoordinator.cs',
    'layout':'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs',
    'audit':'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutAudit.cs',
    'catalogue':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs',
    'details':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeDetails.cs',
    'requirements':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRequirements.cs',
    'outcome':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingOutcome.cs',
    'invbridge':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryBridge.cs',
    'inventory':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs',
    'invscroll':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs',
    'invslot':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs',
    'inventorybridge':'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs',
    'context':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs',
    'queue':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs',
}
for rel in files.values():
    check('exists ' + rel, (root/rel).exists())

parsed = {}
for k in ['windows','templates','xui']:
    try:
        parsed[k] = etree.parse(str(root/files[k]))
        check('xml parse ' + Path(files[k]).name, True)
    except Exception as e:
        check('xml parse ' + Path(files[k]).name, False, str(e))

T = {k:txt(v) for k,v in files.items()}

# Rebirth/Base Game isolation and full custom root ownership.
xui = T['xui']
check('Rebirth conditional remains', "<if cond=\"character_progression('Rebirth')\">" in xui)
check('Rebirth crafting route uses custom controller', "xpath=\"/xui/window_group[@name='crafting']\" name=\"controller\">RebirthPersonalCrafting, RebirthUtils" in xui)
check('Rebirth crafting suppresses automatic stock backpack', "name=\"open_backpack_on_open\">false" in xui)
check('Rebirth branch removes native visible crafting children', "<remove xpath=\"/xui/window_group[@name='crafting']/window\"/>" in xui)
check('Rebirth branch appends only custom root route child', '<window name="rebirthPersonalCraftingRoot" anchor="Center"/>' in xui)

# Single coordinator created before child Init.
r = T['root']
idx_coord = r.find('Coordinator = new RebirthPersonalCraftingCoordinator(this, State);')
idx_child = r.find('children[i].Init();')
check('one shared coordinator property exists', 'public RebirthPersonalCraftingCoordinator Coordinator { get; private set; }' in r)
check('coordinator constructed exactly once', r.count('new RebirthPersonalCraftingCoordinator(this, State)') == 1)
check('coordinator exists before child Init', idx_coord >= 0 and idx_child >= 0 and idx_coord < idx_child)
check('coordinator opens before base child open', r.find('Coordinator?.Open();') < r.find('base.OnOpen();'))
check('coordinator closes before native group close', r.find('Coordinator?.Close();') < r.find('base.OnClose();'))

coord = T['coord']
state = T['state']
for meth in ['SelectRecipe','SelectInventoryItem','ClearInventoryItem','RecordSearch','RecordCategory','RecordFavorites','RecordBatch','RecordMaterials','RecordQueue','RecordBackpack','BeginKnowledgeExplorer','CancelKnowledgeExplorerTransition']:
    check('coordinator owns ' + meth, (' ' + meth + '(') in coord)
check('state documents coordinator-only interaction ownership', 'all interaction/selection state is mutated only by' in state)
check('state exposes four explicit visual modes', all(x in state for x in ['Empty = 0','Recipe = 1','InventoryItem = 2','RecipeWithItemContext = 3']))
check('state includes search/filter/batch/material/queue/backpack/knowledge snapshot', all(x in state for x in [
    'SearchText','SearchActive','SelectedCategory','FavoritesOnly','BatchCount','MaterialsSufficient','MaterialsStateKnown',
    'QueueActiveCount','QueueCapacity','BackpackPhysicalSlots','BackpackUnencumberedSlots','BackpackUsedSlots',
    'KnowledgeExplorerActive','KnowledgeReturnCount']))
check('state exposes interaction summary for audits', 'public string InteractionSummary' in state)

# No child controller owns cross-region state directly.
child_keys = ['catalogue','details','requirements','outcome','invbridge','invslot','context','queue']
for k in child_keys:
    body = T[k]
    check(k + ' does not call legacy SetMode', '.SetMode(' not in body)
    check(k + ' does not directly assign owner.State selection fields', not re.search(r'owner\?*\.?State\.(?:Mode|SelectedRecipeName|SelectedInventorySlot|ItemContextOpen|SearchText|SearchActive|SelectedCategory|FavoritesOnly|BatchCount|MaterialsSufficient|MaterialsStateKnown|QueueActiveCount|QueueCapacity|BackpackPhysicalSlots|BackpackUnencumberedSlots|BackpackUsedSlots|KnowledgeExplorerActive)\s*=', body))

# Every required interaction state reaches coordinator.
cat = T['catalogue']; det = T['details']; req = T['requirements']; out = T['outcome']; ib = T['invbridge']; slot = T['invslot']; ctx = T['context']; queue = T['queue']
check('catalogue records category', 'Coordinator?.RecordCategory' in cat)
check('catalogue records favorites', 'Coordinator?.RecordFavorites' in cat)
check('catalogue records search', 'Coordinator?.RecordSearch' in cat)
check('catalogue records selected recipe', 'Coordinator?.SelectRecipe' in cat)
check('details resolve coordinator recipe first', 'owner?.Coordinator?.SelectedRecipe ??' in det)
check('details record batch', 'Coordinator?.RecordBatch' in det)
check('details records Knowledge transition', 'Coordinator.BeginKnowledgeExplorer()' in det or 'Coordinator?.BeginKnowledgeExplorer()' in det)
check('details cancels failed Knowledge transition', 'CancelKnowledgeExplorerTransition' in det)
check('requirements resolve coordinator recipe first', 'owner?.Coordinator?.SelectedRecipe ??' in req)
check('requirements record batch', 'Coordinator?.RecordBatch' in req)
check('requirements record material sufficiency', 'Coordinator?.RecordMaterials' in req)
check('outcome resolves coordinator recipe first', 'owner?.Coordinator?.SelectedRecipe ??' in out)
check('backpack header records physical/unencumbered/used', 'Coordinator?.RecordBackpack(physical, unencumbered, used)' in ib)
check('inventory click selection routes through coordinated item context', 'context.SelectSlot(this)' in slot)
check('item context promotes same slot to context-open state', 'SelectInventoryItem(slot, true)' in ctx)
check('item context clear returns through coordinator', 'Coordinator?.ClearInventoryItem()' in ctx)
check('queue records active/capacity through coordinator', 'Coordinator?.RecordQueue(active, capacity)' in queue)

# Selection transition semantics.
check('same-recipe background refresh preserves context', 'if (selectedRecipe == recipe)' in coord and 'if (explicitInteraction && state.ItemContextOpen)' in coord)
check('explicit recipe interaction can dismiss item context', 'sameRecipeContext?.ClearSelectionFromCoordinator();' in coord)
check('new recipe transition dismisses item context via coordinator', 'context.ClearSelectionFromCoordinator();' in coord)
check('inventory selection preserves same-slot already-open context', 'keepExistingContext' in coord and 'contextOpen || keepExistingContext' in coord)
check('recipe plus item context is supported combined mode', 'VisualMode.RecipeWithItemContext' in coord)
check('Knowledge return recognized on reopen', 'bool returningFromKnowledge = state.KnowledgeExplorerActive;' in coord and 'state.KnowledgeReturnCount++;' in coord)
check('all interaction mutations request layout audit', 'owner?.RequestLayoutAudit();' in coord)

# Runtime no-overlap audit and stable outer geometry.
audit = T['audit']; layout = T['layout']
check('runtime audit evaluates actual ViewComponent rectangles', 'ViewComponent.Position' in audit and 'ViewComponent.Size' in audit)
for phrase in ['top/body','left/center','center/right','recipes/outcome','details/requirements','requirements/inventory','details/inventory']:
    check('audit separation ' + phrase, ('RequireSeparated("' + phrase + '"') in audit)
for phrase in ['top/root','body/root','left/body','center/body','right/body','recipes/left','outcome/left','details/center','requirements/center','inventory/center','queue/right']:
    check('audit containment ' + phrase, ('RequireContained("' + phrase + '"') in audit)
check('audit requires inventory below Requirements', 'inventory top is not fixed below Requirements' in audit)
check('audit requires 170px inventory region', 'inventory.H < 170' in audit)
check('audit requires full-height independent queue', 'queue.H != right.H' in audit)
check('item context is an exact Selected Recipe context swap', 'itemContext/center' in audit and 'Selected Recipe context swap' in audit)
check('audit computes stable geometry fingerprint', 'Fingerprint(ref fingerprint' in audit)
check('root rejects geometry changes at unchanged screen size', 'unexpectedGeometryChange' in r and 'outer geometry changed without screen-size change' in r)
check('layout audit reports interaction state', '[REBIRTH Crafting LayoutAudit]' in r and 'State.InteractionSummary' in r)
check('layout audit failures are errors', 'Log.Error(logLine)' in r)
check('successful audit logs are debug-gated except open audit', 'RebirthPersonalCraftingState.LayoutDebugEnabled' in r)

# Four-row inventory is reserved first on smaller logical screens.
check('layout reserves 170 minimum inventory height', 'const int inventoryMinHeight = 170;' in layout)
check('layout can reclaim Details down to 180', 'detailsHeight - 180' in layout)
check('layout can reclaim Requirements down to 110', 'requirementsHeight - 110' in layout)
check('inventory always follows Details + Requirements + fixed gaps', 'int inventoryY = -(detailsHeight + ZoneGap + requirementsHeight + ZoneGap);' in layout)
check('queue geometry independent of center split', 'SetRect(owner.GetChildById("rebirthCraftingQueueRegion"), 0, 0, width, height);' in layout)

# Text must clamp/wrap, never silently shrink inside the custom screen or custom queue cards.
if 'windows' in parsed:
    roots = parsed['windows'].xpath("//window[@name='rebirthPersonalCraftingRoot']")
    check('exactly one custom Personal Crafting root', len(roots) == 1)
    if roots:
        root_xml = etree.tostring(roots[0], encoding='unicode')
        check('no shrinkcontent in custom root', 'overflow="shrinkcontent"' not in root_xml)
        check('custom root uses clampcontent', root_xml.count('overflow="clampcontent"') >= 60, str(root_xml.count('overflow="clampcontent"')))
        check('stock itemInfoPanel absent from custom root', 'itemInfoPanel' not in root_xml)
        check('stock craftingInfoPanel absent from custom root', 'craftingInfoPanel' not in root_xml)
if 'templates' in parsed:
    qnodes = parsed['templates'].xpath('//rebirth_personal_crafting_queue_entry')
    check('custom queue entry template exists', len(qnodes) == 1)
    if qnodes:
        qxml = etree.tostring(qnodes[0], encoding='unicode')
        check('no shrinkcontent in custom queue template', 'overflow="shrinkcontent"' not in qxml)
        check('custom queue names/status clamp instead of shrink', qxml.count('overflow="clampcontent"') >= 2)

# 15x4 is viewport only, full Bag remains authoritative.
invb = T['inventorybridge']; inv = T['inventory']; scroll = T['invscroll']
check('Crafting inventory is twelve columns', 'public const int Columns = 12;' in invb)
check('Crafting inventory viewport is four rows', 'public const int VisibleRows = 4;' in invb)
check('authored 108 cells are only presentation ceiling', 'public const int AuthoredRows = 9;' in invb and '108' in invb)
check('physical count comes from authoritative Bag slots', 'bag.GetSlots().Length' in invb)
check('inventory writes only one authoritative Bag index', 'bag.SetSlot(slotNumber' in inv)
check('inventory does not bulk-serialize presenter into Bag', 'SetItemStacks' not in inv)
check('scroll total rows derive from physical Bag count', 'totalRows = Math.Max(1, (physical + Columns - 1) / Columns);' in scroll)
check('wheel moves one complete row', 'pixelOffset - effectiveCell' in scroll and 'pixelOffset + effectiveCell' in scroll)
check('thumb drag snaps to row boundary', 'Mathf.Round(pixelOffset / effectiveCell) * effectiveCell' in scroll)
check('controller focus ensures offscreen slot visible', 'EnsureSlotVisible' in slot and 'EnsureSlotVisible(SlotNumber)' in slot)
check('locked/encumbered authority remains native-backed', 'GetUnencumberedSlotCount' in invb and 'SetUserLockMode' in ib)

# Source delimiter smoke test for every custom crafting C# file.
cs_files = sorted((root/'Scripts/Crafting/UI/PersonalCrafting').glob('*.cs'))
for p in cs_files:
    s = p.read_text(errors='ignore')
    # strip strings/comments enough for a practical static smoke check
    scrub = re.sub(r'//.*?$|/\*.*?\*/|@?"(?:""|\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'', '', s, flags=re.M|re.S)
    check('balanced braces ' + p.name, scrub.count('{') == scrub.count('}'), f"{scrub.count('{')}/{scrub.count('}')}")
    check('balanced parentheses ' + p.name, scrub.count('(') == scrub.count(')'), f"{scrub.count('(')}/{scrub.count(')')}")

# Pure-Python simulation of Chunk-J layout arithmetic using fallback HUD reserve.
def clamp(v, lo, hi): return max(lo, min(hi, v))
def simulate(sw, sh):
    hm = clamp(round(sw*0.0125), 10, 28)
    tm = clamp(round(sh*0.0120), 8, 20)
    bottom = 132
    rw = max(1180, sw - hm*2); rh = max(610, sh - tm - bottom)
    rw = min(rw, max(1, sw-4)); rh = min(rh, max(1, sh-4))
    body_h = max(1, rh - 8*2 - 52 - 10)
    body_w = max(1, rw - 8*2)
    avail = max(1, body_w - 20)
    lw = max(300, round(avail*0.25)); rrw = max(300, round(avail*0.27)); cw = max(420, avail-lw-rrw)
    over = lw+cw+rrw-avail
    if over > 0:
        fs = min(over, max(0,lw-260)+max(0,rrw-260))
        lt = min(max(0,lw-260), (fs+1)//2); lw -= lt
        rt = min(max(0,rrw-260), fs-lt); rrw -= rt
        over -= lt+rt
        if over > 0: cw=max(320,cw-over)
    used=lw+cw+rrw
    if used < avail: cw += avail-used
    dh=clamp(round(body_h*0.34),190,300)
    qh=clamp(round(body_h*0.24),120,220)
    ih=body_h-dh-qh-20
    if ih < 170:
        deficit=170-ih
        take=min(deficit,max(0,dh-180)); dh-=take; deficit-=take
        take=min(deficit,max(0,qh-110)); qh-=take; deficit-=take
        ih=max(1,body_h-dh-qh-20)
    # local rectangles, positive down
    left=(0,0,lw,body_h); center=(lw+10,0,cw,body_h); right=(lw+10+cw+10,0,rrw,body_h)
    details=(0,0,cw,dh); reqr=(0,dh+10,cw,qh); inventory=(0,dh+10+qh+10,cw,ih)
    ok = True; errs=[]
    if ih < 170: ok=False; errs.append('inventory<170')
    if inventory[1] < reqr[1]+reqr[3]: ok=False; errs.append('inventory overlaps requirements')
    if left[0]+left[2] > center[0]: ok=False; errs.append('left/center')
    if center[0]+center[2] > right[0]: ok=False; errs.append('center/right')
    if right[0]+right[2] > body_w: ok=False; errs.append('right outside body')
    if inventory[1]+inventory[3] > body_h: ok=False; errs.append('inventory outside center')
    return ok, f"root={rw}x{rh} body={body_w}x{body_h} cols={lw}/{cw}/{rrw} center={dh}/{qh}/{ih}" + ((' errors='+','.join(errs)) if errs else '')

for sw,sh in [(1280,720),(1366,768),(1920,1080),(2560,1440),(3440,1440)]:
    ok,detail=simulate(sw,sh)
    check(f'layout simulation {sw}x{sh}', ok, detail)

passed=sum(1 for _,ok,_ in checks if ok); failed=len(checks)-passed
for name,ok,detail in checks:
    print(('PASS' if ok else 'FAIL') + ' - ' + name + ((' :: '+detail) if detail else ''))
print(f'\nPC084 Chunk J validation: {passed} PASS / {failed} FAIL')
sys.exit(0 if failed == 0 else 1)
