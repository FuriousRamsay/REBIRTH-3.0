from pathlib import Path
from lxml import etree
import csv, re, sys

root=Path(__file__).resolve().parents[2]
checks=[]
def check(name, ok, detail=''):
    checks.append((name,bool(ok),detail))
def text(rel):
    p=root/rel
    return p.read_text(errors='ignore') if p.exists() else ''

rels={
 'windows':'Config/XUi_InGame/windows.xml',
 'xui':'Config/XUi_InGame/xui.xml',
 'loc':'Config/Localization.csv',
 'root':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs',
 'native':'Scripts/Input/RebirthNativeControls.cs',
 'layout':'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs',
 'state':'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingState.cs',
 'catalogue':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs',
 'inventory':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs',
 'invscroll':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs',
 'invslot':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs',
 'context':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs',
 'actions':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingActions.cs',
 'queue':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs',
 'queueentry':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs',
 'req':'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingRequirementProjectionService.cs',
 'command':'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingCommandBridge.cs',
 'nav':'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingNavigationService.cs',
}
for r in rels.values(): check('exists '+r,(root/r).exists())
T={k:text(v) for k,v in rels.items()}

# XML parses and custom root exists.
try:
    wdoc=etree.parse(str(root/rels['windows'])); check('windows.xml parses',True)
except Exception as e:
    wdoc=None; check('windows.xml parses',False,str(e))
try:
    xdoc=etree.parse(str(root/rels['xui'])); check('xui.xml parses',True)
except Exception as e:
    xdoc=None; check('xui.xml parses',False,str(e))
custom=None
if wdoc is not None:
    nodes=wdoc.xpath("//window[@name='rebirthPersonalCraftingRoot']")
    check('exactly one custom root',len(nodes)==1,str(len(nodes)))
    custom=nodes[0] if len(nodes)==1 else None

# Explicit input ownership lifecycle.
r=T['root']
for phrase in [
 'CaptureInputStateBeforeOpen();','AcquireInputOwnership();','MaintainInputOwnership();',
 'HandleCancelOrBack()','ReleaseFocusedTextInput();','ReleaseInputOwnership();',
 'windowGroup.isEscClosable = false;','cursor.SetCursorHidden(false);',
 'cursor.SetNavigationLockView(','player.ClearMovementInputs();','player.SetControllable(false);',
 'Input.GetKeyDown(KeyCode.Escape)','PermanentActions.Cancel.WasReleased','GUIActions.Cancel.WasReleased',
 'context.ClearSelection();','windowManager.Close((GUIWindow)windowGroup)',
 'cursor.SetNavigationLockView((XUiView)null);','cursor.SetCursorHidden(cursorHiddenBeforeOpen);',
 'manager?.ResetActionSets();','GetChunkKInputReport()']:
    check('input lifecycle contains '+phrase, phrase in r)
check('input capture occurs before base OnOpen', r.find('CaptureInputStateBeforeOpen();') < r.find('base.OnOpen();'))
check('input ownership acquired after base OnOpen', r.find('base.OnOpen();') < r.find('AcquireInputOwnership();'))
check('focused input cleared before base OnClose', r.find('ReleaseFocusedTextInput();') < r.find('base.OnClose();'))
check('final ownership release after base OnClose', r.find('base.OnClose();') < r.find('ReleaseInputOwnership();'))
check('movement block reasserted every update', r.find('MaintainInputOwnership();') > r.find('public override void Update(float dt)'))
check('cancel uses one-frame latch', 'lastCancelFrame == Time.frameCount' in r and 'lastCancelFrame = Time.frameCount;' in r)
check('item context is first back layer', r.find('context.ClearSelection();') < r.find('windowManager.Close((GUIWindow)windowGroup)'))
check('top-route close does not blindly restore gameplay', 'anotherUiOwnsInput' in r and 'SetControllable(!anotherUiOwnsInput)' in r)
check('route-transition latch suppresses gameplay restore', 'suppressGameplayRestoreOnClose' in r and 'PrepareForExternalRoute()' in r)
nav=T['nav']
check('every external top-tab route marks input handoff', nav.count('PrepareExternalRoute(source);') == 6, str(nav.count('PrepareExternalRoute(source);')))
check('navigation handoff reaches root controller', 'owner?.PrepareForExternalRoute();' in nav)
check('controller gets explicit root navigation lock', 'rebirthPersonalCraftingRoot' in r and 'SetNavigationTarget(initial.ViewComponent)' in r)
check('keyboard does not force controller target', 'CurrentInputStyle != PlayerInputManager.InputStyle.Keyboard' in r)

# REBIRTH hotkeys have an independent hard gate even if native window-manager state regresses.
n=T['native']
for phrase in ['IsRebirthPersonalCraftingOpen(EntityPlayerLocal player)','IsWindowOpen("crafting")','XUiController group = player.PlayerUI.xui.FindWindowGroupByName("crafting")','controller.State.IsOpen']:
    check('native controls crafting gate '+phrase,phrase in n)
check('on-foot action dispatcher explicitly blocks custom Crafting', re.search(r'CanDispatchOnFootAction[\s\S]{0,700}IsRebirthPersonalCraftingOpen\(player\)[\s\S]{0,120}return false;',n) is not None)
check('on-foot block reason identifies custom Crafting', 'rebirth-personal-crafting-open' in n)
check('companion style shortcut also respects custom Crafting', re.search(r'TryConsumeCompanionCardStyleToggle[\s\S]{0,600}IsRebirthPersonalCraftingOpen\(player\)',n) is not None)

# Search is a real native text input and controller graph has explicit escape bridges.
if custom is not None:
    def one(name):
        x=custom.xpath(".//*[@name='%s']"%name); return x[0] if len(x)==1 else None
    search=one('rebirthCraftingRecipeSearch')
    check('search field exists',search is not None)
    if search is not None:
        check('search uses textfield',search.tag=='textfield')
        check('search_field enabled',search.get('search_field')=='true')
        check('search has native clear button',search.get('clear_button')=='true')
        check('search virtual-keyboard events do not propagate',search.get('vk_propagates_events')=='false')
        check('search nav left to favorites',search.get('nav_left')=='btnRebirthCraftingFavoriteFilter')
        check('search nav up to top tabs',search.get('nav_up')=='btnRebirthCraftingTabCrafting')
        check('search nav down to first recipe',search.get('nav_down')=='rebirthCraftingRecipeRowHit0')
    fav=one('btnRebirthCraftingFavoriteFilter')
    check('favorites controller bridge exists',fav is not None)
    if fav is not None:
        check('favorites nav up',fav.get('nav_up')=='btnRebirthCraftingTabCrafting')
        check('favorites nav right',fav.get('nav_right')=='rebirthCraftingRecipeSearch')
        check('favorites nav down',fav.get('nav_down')=='rebirthCraftingRecipeRowHit0')
    for tab in ['Crafting','Character','Map','Skills','Quests','Challenges','Players']:
        e=one('btnRebirthCraftingTab'+tab)
        check('tab '+tab+' has down bridge',e is not None and e.get('nav_down')=='btnRebirthCraftingFavoriteFilter')
    row7=one('rebirthCraftingRecipeRowHit7')
    check('last recipe row is not a downward navigation trap',row7 is not None and row7.get('nav_down')=='btnRebirthCraftingViewKnowledge')
    knowledge=one('btnRebirthCraftingViewKnowledge')
    check('knowledge action bridges back to recipe list',knowledge is not None and knowledge.get('nav_left')=='rebirthCraftingRecipeRowHit7')
    for b in ['btnRebirthCraftingCraft','btnRebirthCraftingFavorite','btnRebirthCraftingTrack']:
        e=one(b); check(b+' has explicit controller navigation',e is not None and any(e.get(k) for k in ['nav_left','nav_right','nav_up','nav_down']))

# All player-facing static strings in custom root are localization keys or dynamic bindings.
if custom is not None:
    literal=[]; keyrefs=[]
    for e in custom.iter():
        if e.get('text_key'): keyrefs.append(e.get('text_key'))
        if e.get('tooltip_key'): keyrefs.append(e.get('tooltip_key'))
        for attr in ['text','tooltip']:
            v=e.get(attr)
            if v and not v.startswith('{'):
                # numeric/keyboard display values are allowed; English prose is not.
                if re.search(r'[A-Za-z]{2,}',v): literal.append((e.get('name') or e.tag,attr,v))
    check('no hard-coded English prose in custom root',len(literal)==0,str(literal[:8]))
    # localization CSV key coverage
    loc_keys=set()
    try:
        with open(root/rels['loc'],encoding='utf-8-sig',newline='') as f:
            for row in csv.reader(f):
                if row: loc_keys.add(row[0])
    except Exception: pass
    missing=[k for k in keyrefs if k and k.startswith('xuiRebirth') and k not in loc_keys]
    check('custom root localization keys resolve',len(missing)==0,str(sorted(set(missing))[:12]))
    rootxml=etree.tostring(custom,encoding='unicode')
    check('no shrinkcontent in custom root','overflow="shrinkcontent"' not in rootxml)
    check('readable text clamps rather than shrinks',rootxml.count('overflow="clampcontent"')>=60,str(rootxml.count('overflow="clampcontent"')))

# Responsive layout and wide 15x4 viewport invariants.
layout=T['layout']; state=T['state']; inv=T['inventory']; scroll=T['invscroll']
for phrase in ['XUi.GetXUiScreenSize()','BottomHudReserveFallback','ResolveBottomHudReserve()','RootMinWidth = 1180','RootMinHeight = 610','inventoryMinHeight = 170']:
    check('responsive layout '+phrase,phrase in layout)
for res in ['1280','1366','1920','2560','3440']:
    # PC084 validator already simulates these; ensure the inherited validator remains packaged.
    check('PC084 inherited responsive simulation covers '+res,res in text('BuildScripts/UI/validate_pc084_personal_crafting_chunk_j.py'))
check('12 columns fill viewport width','public const int Columns = 12;' in text('Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs'))
check('4 rows remain visible viewport','public const int VisibleRows = 4;' in text('Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs'))
check('physical Bag still controls capacity','bag.GetSlots().Length' in text('Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs'))
check('scroll rows derive from physical Bag','totalRows = Math.Max(1, (physical + Columns - 1) / Columns);' in scroll)
check('single-slot Bag write remains authoritative','bag.SetSlot(slotNumber' in inv)
check('presenter still cannot bulk resize Bag','SetItemStacks' not in inv)

# Multiplayer/native authority: presentation remains client-side, transactions remain native/REBIRTH-authorized.
a=T['actions']; q=T['queue']; qe=T['queueentry']; req=T['req']; cmd=T['command']
check('Craft action still delegates through native ItemActionEntryCraft','ItemActionEntryCraft' in cmd)
check('custom queue remains native CraftingQueue subclass','XUiC_CraftingQueue' in q)
check('queue entries remain native RecipeStack subclass','XUiC_RecipeStack' in qe)
check('requirements still project Remote Resources','RemoteResource' in req)
check('requirements include Backpack/Toolbelt authority','PlayerInventory' in req or 'Backpack' in req)
check('custom command bridge does not invent network package', 'NetPackage' not in cmd)
check('root controller does not create network packages','NetPackage' not in r)
check('inventory presenter does not create network packages','NetPackage' not in inv)

# Base Game isolation remains structural and new input gate is type-specific.
x=T['xui']
check('custom crafting override remains Rebirth conditional',"<if cond=\"character_progression('Rebirth')\">" in x)
check('custom controller set only inside conditional patch',"name=\"controller\">RebirthPersonalCrafting, RebirthUtils" in x)
check('Base Game native visible children are only removed in Rebirth branch','<remove xpath="/xui/window_group[@name=\'crafting\']/window"/>' in x)
check('input hotkey gate requires custom controller type','group as XUiC_RebirthPersonalCrafting' in n and 'GetChildByType<XUiC_RebirthPersonalCrafting>()' in n)

# Practical C# delimiter smoke checks for changed files.
for rel in [rels['root'],rels['native'],rels['nav']]:
    s=text(rel)
    scrub=re.sub(r'//.*?$|/\*.*?\*/|@?"(?:""|\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'','',s,flags=re.M|re.S)
    check('balanced braces '+Path(rel).name,scrub.count('{')==scrub.count('}'),f"{scrub.count('{')}/{scrub.count('}')}")
    check('balanced parentheses '+Path(rel).name,scrub.count('(')==scrub.count(')'),f"{scrub.count('(')}/{scrub.count(')')}")

passed=sum(ok for _,ok,_ in checks); failed=len(checks)-passed
for name,ok,detail in checks:
    print(('PASS' if ok else 'FAIL')+' - '+name+((' :: '+detail) if detail else ''))
print(f'\nPC085 Chunk K validation: {passed} PASS / {failed} FAIL')
sys.exit(0 if failed==0 else 1)
