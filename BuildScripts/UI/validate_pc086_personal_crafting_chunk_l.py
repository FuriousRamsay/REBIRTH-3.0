from pathlib import Path
from lxml import etree
import csv,re,sys

root=Path(__file__).resolve().parents[2]
checks=[]
def check(name,ok,detail=''):
    checks.append((name,bool(ok),str(detail) if detail else ''))
def text(rel):
    p=root/rel
    return p.read_text(encoding='utf-8',errors='ignore') if p.exists() else ''

rels={
 'windows':'Config/XUi_InGame/windows.xml',
 'templates':'Config/XUi_InGame/templates.xml',
 'xui':'Config/XUi_InGame/xui.xml',
 'loc':'Config/Localization.csv',
 'presentation':'Scripts/Crafting/UI/XUiC_RebirthCraftingPresentation.cs',
 'root':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs',
 'state':'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingState.cs',
 'inventory':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs',
 'invbridge':'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs',
 'invscroll':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs',
 'queue':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs',
 'queueentry':'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs',
 'coordinator':'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingCoordinator.cs',
 'audit':'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutAudit.cs',
 'native':'Scripts/Input/RebirthNativeControls.cs',
 'debug':'Scripts/Debug/Commands/ConsoleCmdRebirthCraftingUi.cs',
 'fullvalidator':'BuildScripts/UI/validate_rebirth_ui_redesign.py',
}
for rel in rels.values(): check('exists '+rel,(root/rel).exists())
T={k:text(v) for k,v in rels.items()}

# Core XML parsing.
docs={}
for key in ('windows','templates','xui'):
    try:
        docs[key]=etree.parse(str(root/rels[key])); check(key+'.xml parses',True)
    except Exception as e:
        docs[key]=None; check(key+'.xml parses',False,e)

# PC072 personal-only migration cleanup.
legacy_literals=[
 ('legacy non-station window','rebirthCraftingQueueNonStation',T['windows']),
 ('legacy non-station queue template','<rebirth_crafting_queue_card>',T['templates']),
 ('legacy queue template controller','RebirthRecipeStack, RebirthUtils',T['templates']),
 ('legacy unused unlock template','<rebirth_crafting_unlock_row>',T['templates']),
 ('legacy queue subclass','XUiC_RebirthRecipeStack : XUiC_RecipeStack',T['presentation']),
 ('legacy cancel-hint localization key','xuiRebirthQueueCancelHint,',T['loc']),
]
for name,literal,source in legacy_literals:
    check(name+' retired',literal not in source)

# Station dependencies intentionally retained.
check('station queue window retained','name="rebirthCraftingQueueStation" width="330" height="124"' in T['windows'])
check('station queue remains native CraftingQueue-backed','name="content" pos="0,-46" width="330" height="78" controller="CraftingQueue"' in T['windows'])
check('station queue uses real recipe_stack','<recipe_stack name="0"/>' in T['windows'])
check('seven station registrations preserved',T['xui'].count('name="rebirthCraftingQueueStation"')==7,T['xui'].count('name="rebirthCraftingQueueStation"'))
check('shared station recipe list remains','windowCraftingList' in T['xui'] and 'name="recipes" depth="2" rows="8" cols="1"' in T['windows'])
check('shared station crafting info controller retained','RebirthCraftingInfoWindow, RebirthUtils' in T['windows'])
check('shared station outcome projection retained','RebirthCraftOutcomePanel, RebirthUtils' in T['windows'])
check('shared ingredient card retained','<rebirth_crafting_ingredient_card>' in T['templates'])

# Final Rebirth Personal Crafting ownership.
x=T['xui']
check('public crafting route retained','window_group[@name=\'crafting\']' in x)
check('Rebirth controller conditional override retained','RebirthPersonalCrafting, RebirthUtils' in x)
check('Rebirth custom group removes all old child windows','<remove xpath="/xui/window_group[@name=\'crafting\']/window"/>' in x)
check('only custom root appended to Rebirth crafting group','<window name="rebirthPersonalCraftingRoot" anchor="Center"/>' in x)
check('native automatic backpack disabled only in Rebirth override','name="open_backpack_on_open">false</setattribute>' in x)
check('legacy nonstation queue not registered in xui','rebirthCraftingQueueNonStation' not in x)

custom=None
if docs.get('windows') is not None:
    nodes=docs['windows'].xpath("//window[@name='rebirthPersonalCraftingRoot']")
    check('exactly one Rebirth Personal Crafting root',len(nodes)==1,len(nodes))
    custom=nodes[0] if len(nodes)==1 else None
if custom is not None:
    xml=etree.tostring(custom,encoding='unicode')
    # Structural concept accounting.
    required_ids=[
      'rebirthCraftingTopZone','rebirthCraftingWorldStatus','rebirthCraftingBodyZone',
      'rebirthCraftingLeftZone','rebirthCraftingCenterZone','rebirthCraftingRightZone','rebirthCraftingTopTabs',
      'rebirthCraftingRecipesRegion','rebirthCraftingOutcomeRegion','rebirthCraftingDetailsRegion',
      'rebirthCraftingRequirementsRegion','rebirthCraftingInventoryRegion','rebirthCraftingQueueRegion',
      'rebirthCraftingItemContext'
    ]
    for rid in required_ids:
        check('custom root contains '+rid,len(custom.xpath(".//*[@name='%s']"%rid))==1)
    check('9 recipe presentation hit targets (8 visible + smooth buffer)',len(custom.xpath(".//*[starts-with(@name,'rebirthCraftingRecipeRowHit')]"))==9,
          len(custom.xpath(".//*[starts-with(@name,'rebirthCraftingRecipeRowHit')]")))
    check('6 requirement cards',len(custom.xpath(".//*[starts-with(@name,'rebirthCraftingRequirementCard')]"))==6,
          len(custom.xpath(".//*[starts-with(@name,'rebirthCraftingRequirementCard')]")))
    check('4 native-derived queue entries',len(custom.xpath(".//rebirth_personal_crafting_queue_entry[starts-with(@name,'rebirthCraftingQueueSlot')]"))==4,
          len(custom.xpath(".//rebirth_personal_crafting_queue_entry[starts-with(@name,'rebirthCraftingQueueSlot')]")))
    check('no shrinkcontent in custom root','overflow="shrinkcontent"' not in xml)
    for forbidden in ('windowNonPagingHeader','windowCraftingList','craftingInfoPanel','backpack','itemInfoPanel','emptyInfoPanel','windowCraftingQueue'):
        check('forbidden native visual absent: '+forbidden,len(custom.xpath(".//*[@name='%s']"%forbidden))==0)

# Backpack is viewport-only, not capacity.
ib=T['invbridge']; inv=T['inventory']; scroll=T['invscroll']
check('crafting backpack has 12 columns','public const int Columns = 12;' in ib)
check('crafting backpack has 4 visible rows','public const int VisibleRows = 4;' in ib)
check('physical capacity derives from Bag slots','bag.GetSlots().Length' in ib)
check('scroll row count derives from physical slots','totalRows = Math.Max(1, (physical + Columns - 1) / Columns);' in scroll)
check('single Bag slot write remains authoritative','bag.SetSlot(slotNumber' in inv)
check('presenter never bulk-serializes authored cells','SetItemStacks' not in inv)
check('encumbered state remains native CarryCapacity-backed','GetUnencumberedSlotCount(xui)' in inv and 'slot.AttributeLock' in inv)
check('locked slot persistence remains','PersistUserLockedSlots' in inv and 'ApplyLockedSlots' in inv)

# Queue/native craft authority.
check('final queue subclasses native CraftingQueue','XUiC_RebirthCraftingQueue : XUiC_CraftingQueue' in T['queue'])
check('final queue entries subclass native RecipeStack','XUiC_RebirthCraftingQueueEntry : XUiC_RecipeStack' in T['queueentry'])
check('legacy queue subclass no longer needed','XUiC_RebirthRecipeStack' not in T['queue']+T['queueentry'])

# Integrated state/no-overlap/input hardening remains.
for literal in ['RebirthPersonalCraftingCoordinator','RunLayoutAudit(','GetLayoutAuditReport()','MaintainInputOwnership();','HandleCancelOrBack()','PrepareForExternalRoute()']:
    check('root retains '+literal,literal in T['root'])
check('layout audit checks inventory minimum height','inventory.H < 170' in T['audit'])
check('hotkey gate remains type-specific','group as XUiC_RebirthPersonalCrafting' in T['native'] and 'GetChildByType<XUiC_RebirthPersonalCrafting>()' in T['native'])
check('hotkey gate requires custom state open','controller.State.IsOpen' in T['native'])

# Final consolidated debug report path.
r=T['root']; d=T['debug']
for literal in ['ActiveInstance','GetFinalAcceptanceDebugReport()','forbiddenNativeChildren','visibleRows','requirements=','queue=','progression=','GetChunkKInputReport()','GetLayoutAuditReport()']:
    check('final debug report contains '+literal,literal in r)
check('active instance set on open','ActiveInstance = this;' in r)
check('active instance cleared on close','ReferenceEquals(ActiveInstance, this)' in r and 'ActiveInstance = null' in r)
check('debug command is Debug-build gated',d.lstrip().startswith('#if DEBUG') and d.rstrip().endswith('#endif'))
check('debug command name rbcraftui','"rbcraftui"' in d)
check('debug command can print final report','GetFinalAcceptanceDebugReport()' in d)
check('debug guides use existing gated bool','RebirthPersonalCraftingState.LayoutDebugEnabled' in d)

# Localization duplicate check and removed key check.
keys=[]
try:
    with open(root/rels['loc'],encoding='utf-8-sig',newline='') as f:
        for row in csv.reader(f):
            if row and row[0].strip(): keys.append(row[0].strip())
except Exception as e: check('Localization readable',False,e)
dups=sorted({k for k in keys if keys.count(k)>1})
check('no duplicate nonblank localization keys',not dups,dups[:10])
check('obsolete queue cancel hint localization absent','xuiRebirthQueueCancelHint' not in keys)
for k in ['xuiRebirthQueueEmpty','xuiRebirthQueueCrafting','xuiRebirthQueueQueued','xuiRebirthQueueCancelTooltip']:
    check('final queue localization retained '+k,k in keys)

# All Config XML parses.
xml_files=list((root/'Config').rglob('*.xml'))
xml_errors=[]
for p in xml_files:
    try: etree.parse(str(p))
    except Exception as e: xml_errors.append((str(p.relative_to(root)),str(e)))
check('all Config XML parses',not xml_errors,f"{len(xml_files)-len(xml_errors)}/{len(xml_files)}"+((' '+str(xml_errors[:3])) if xml_errors else ''))

# C# delimiter smoke checks on changed source files. Not a compiler substitute.
for rel in [rels['presentation'],rels['root'],rels['debug']]:
    s=text(rel)
    scrub=re.sub(r'//.*?$|/\*.*?\*/|@?"(?:""|\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'','',s,flags=re.M|re.S)
    check('balanced braces '+Path(rel).name,scrub.count('{')==scrub.count('}'),f"{scrub.count('{')}/{scrub.count('}')}")
    check('balanced parentheses '+Path(rel).name,scrub.count('(')==scrub.count(')'),f"{scrub.count('(')}/{scrub.count(')')}")

# Full validator was updated so cleanup is not misclassified as a regression.
fv=T['fullvalidator']
check('full validator knows PC086 cleanup','PC086 cleanup failed' in fv)
check('full validator no longer unconditionally requires legacy nonstation queue',"'nonstation_queue'" not in fv)

passed=sum(ok for _,ok,_ in checks); failed=len(checks)-passed
for name,ok,detail in checks:
    print(('PASS' if ok else 'FAIL')+' - '+name+((' :: '+detail) if detail else ''))
print(f'\nPC086 Chunk L validation: {passed} PASS / {failed} FAIL')
sys.exit(0 if failed==0 else 1)
