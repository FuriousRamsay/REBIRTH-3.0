#!/usr/bin/env python3
from pathlib import Path
import sys, xml.etree.ElementTree as ET, re

root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
checks=[]
def ck(cond,msg):
    ok=bool(cond); checks.append((ok,msg)); print(('PASS' if ok else 'FAIL')+' | '+msg)
def text(rel): return (root/rel).read_text(encoding='utf-8',errors='ignore')

menu=text('Config/XUi_Menu/windows.xml')
manager=text('Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs')
gate=text('Scripts/Survivor/UI/XUiC_RebirthSpawnSelectionSurvivorGate.cs')
hunter=text('Scripts/Survivor/Backgrounds/RebirthCombatPatrolTrackingSignatureService.cs')
placed=text('Scripts/Survivor/Provenance/RebirthPlacedWorkmanshipService.cs')
work=text('Scripts/Survivor/Backgrounds/RebirthWorkmanshipSignatureService.cs')
repair=text('Scripts/Survivor/Repair/RebirthRepairSignatureService.cs')
loot=text('Config/_Survivor/loot.xml')
traders=text('Config/_Survivor/traders.xml')
recipes=text('Config/_Survivor/recipes.xml')

# Screenshot 1: conditional tooltip on actual NGUI truncation only.
ck('private int progressionTooltipSyncFrames;' in manager,'progression tooltip waits for final NGUI layout')
ck('label.label.processedText' in manager and 'NGUIText.StripSymbols' in manager,'tooltip compares final processed/ellipsized text with full label')
ck('bool truncated = !string.Equals(plainFull, plainProcessed, StringComparison.Ordinal);' in manager,'tooltip condition is actual rendered truncation')
ck('if (truncated) tooltip = full;' in manager,'full name tooltip is assigned only for truncated text')
ck('if (label != null) label.ToolTip = tooltip;' in manager,'label hover receives conditional tooltip')
ck('hit.ViewComponent.ToolTip = tooltip;' in manager,'overlay click target receives same conditional tooltip')
helper_start=manager.find('private void RefreshProgressionTruncationTooltips')
helper_end=manager.find('private void RenderStartingItems',helper_start)
ck('for (int i = 0; i < ProgressionVisibleRows; i++)' in manager[helper_start:helper_end], 'tooltip scope is Skills & Knowledge rows only')

# Screenshots 3/4: larger, darker-but-translucent, localization-safe signature bonus panels.
tree=ET.parse(root/'Config/XUi_Menu/windows.xml')
xmlroot=tree.getroot()
def named(name):
    for e in xmlroot.iter():
        if e.attrib.get('name')==name: return e
    return None
exp=named('backgroundSignatureBonusPanel'); rev=named('reviewSignatureBonusPanel')
ck(exp is not None and int(exp.attrib.get('height','0'))>=110,'Experience Signature Bonus panel is taller')
ck(rev is not None and int(rev.attrib.get('height','0'))>=145,'Review Signature Bonus panel is taller')
for panel,label in [(exp,'Experience'),(rev,'Review')]:
    bg=next((c for c in list(panel if panel is not None else []) if c.tag=='sprite'),None)
    alpha=int((bg.attrib.get('color','0,0,0,0').split(',')[-1])) if bg is not None else 0
    ck(230 <= alpha < 255,f'{label} Signature Bonus panel is less transparent while retaining background visibility')
ck(named('backgroundSignatureBonusName') is not None and int(named('backgroundSignatureBonusName').attrib.get('font_size','0'))>=21,'Experience Signature Bonus name font enlarged')
ck(named('backgroundSignatureBonusDescription') is not None and int(named('backgroundSignatureBonusDescription').attrib.get('font_size','0'))>=15,'Experience Signature Bonus description font enlarged')
ck(named('reviewSignatureBonusName') is not None and int(named('reviewSignatureBonusName').attrib.get('font_size','0'))>=22,'Review Signature Bonus name font enlarged')
ck(named('reviewSignatureBonusDescription') is not None and int(named('reviewSignatureBonusDescription').attrib.get('font_size','0'))>=16,'Review Signature Bonus description font enlarged')
ck(named('backgroundSignatureBonusDescription').attrib.get('overflow')=='shrinkcontent','Experience localized bonus description shrinks instead of truncating when required')
ck(named('reviewSignatureBonusDescription').attrib.get('overflow')=='shrinkcontent','Review localized bonus description shrinks instead of truncating when required')

# Yellow runtime-init message / fatal Harmony error from supplied log.
activation=hunter[hunter.find('private static bool Activated'):hunter.find('\n    }',hunter.find('private static bool Activated'))+6]
ck('EntityActivationCommand __0' in activation and 'EntityPlayerLocal __1' in activation,'Hunter OnEntityActivated patch uses positional V3.2 Harmony arguments')
ck('EntityActivationCommand command=__0;' in activation and 'EntityPlayerLocal focusingPlayer=__1;' in activation,'Hunter patch maps positional arguments internally')

# XML loader errors from supplied log.
ck('resourcePlantFibers' not in recipes and 'resourceYuccaFibers' in recipes,'invalid resourcePlantFibers recipe ingredient replaced with current resourceYuccaFibers')
loot_root=ET.parse(root/'Config/_Survivor/loot.xml').getroot(); loot_children=list(loot_root)
loot_insert=next((e for e in loot_children if e.tag=='insertBefore' and "groupToolsTiered" in e.attrib.get('xpath','')),None)
loot_ref=next((e for e in loot_children if e.tag=='append' and "groupToolsTiered" in e.attrib.get('xpath','')),None)
loot_defs={e.attrib.get('name') for e in list(loot_insert if loot_insert is not None else []) if e.tag=='lootgroup'}
ck(loot_insert is not None and loot_ref is not None and loot_children.index(loot_insert)<loot_children.index(loot_ref),'audio loot definitions patch before first native groupToolsTiered reference')
ck({'rebirthAudioEquipment','rebirthAudiobookCassettes','rebirthAudiobookDiscoveryCassettes'} <= loot_defs,'all referenced audio loot groups are predeclared')
tr_root=ET.parse(root/'Config/_Survivor/traders.xml').getroot(); tr_children=list(tr_root)
tr_insert=next((e for e in tr_children if e.tag=='insertBefore' and "traderGeneral" in e.attrib.get('xpath','')),None)
tr_ref=next((e for e in tr_children if e.tag=='append' and "traderGeneral" in e.attrib.get('xpath','')),None)
tr_defs={e.attrib.get('name') for e in list(tr_insert if tr_insert is not None else []) if e.tag=='trader_item_group'}
ck(tr_insert is not None and tr_ref is not None and tr_children.index(tr_insert)<tr_children.index(tr_ref),'audio trader definitions patch before first traderGeneral reference')
ck({'rebirthAudioTraderTheory','rebirthAudioTraderDiscovery','rebirthAudioTraderMusic','rebirthAudioTraderEquipment','rebirthAudioTrader'} <= tr_defs,'all referenced audio trader groups are predeclared')

# XUi_InGame patch warnings: every conditional <if> must be owned by <conditional>.
def conditional_if_ok(p):
    r=ET.parse(p).getroot()
    bad=[]
    def walk(parent):
        for child in list(parent):
            if child.tag=='if' and parent.tag!='conditional': bad.append(child)
            walk(child)
    walk(r); return not bad
for rel in ['Config/XUi_InGame/templates.xml','Config/XUi_InGame/windows.xml','Config/XUi_InGame/xui.xml']:
    ck(conditional_if_ok(root/rel),f'no naked unsupported <if> patch nodes: {Path(rel).name}')

# Reflection warning flood + V3.2 OnBlockDamaged source contract.
placed_scan=placed[placed.find('public static class RebirthPlacedWorkmanshipPatchInstaller'):placed.find('public static class RebirthPlacedWorkmanshipPlacementPatch')]
ck('AccessTools.DeclaredMethod(t,nameof(Block.PlaceBlock)' not in placed_scan and 'BindingFlags.DeclaredOnly' in placed_scan,'PlaceBlock scanner uses silent declared-only reflection instead of noisy AccessTools misses')
ck('BlockValueRef __1' in work and 'ref int __3' in work and 'bool __7' in work,'placed durability prefix matches V3.2 BlockValueRef OnBlockDamaged positions')
ck('typeof(BlockValueRef)' in work and 'typeof(WorldBase),typeof(int),typeof(Vector3i)' not in work,'OnBlockDamaged scanner targets current V3.2 signature')
ck('AccessTools.DeclaredMethod(t,nameof(Block.OnBlockDamaged)' not in work and 't.GetMethod(nameof(Block.OnBlockDamaged)' in work,'OnBlockDamaged scanner is silent for inherited-method misses')
install=repair[repair.find('if (harmony != null)'):repair.find('ModEvents.GameUpdate.RegisterHandler')]
ck(install.count('PatchDeathMethod(harmony, typeof(EntityAlive))')==1 and 'typeof(EntityPlayer)' not in install and 'typeof(EntityPlayerLocal)' not in install,'death baseline patches EntityAlive once without invalid EntityPlayer declared-method probe')
ck('AccessTools.DeclaredMethod(type, "OnEntityDeath")' not in repair,'death method lookup no longer emits AccessTools missing-method warnings')

# Spawn visual failure: defer replacement window until the frame after native spawn OnOpen.
onopen=gate[gate.find('public override void OnOpen()'):gate.find('public override void Update',gate.find('public override void OnOpen()'))]
update=gate[gate.find('public override void Update(float dt)'):gate.find('public override void OnClose',gate.find('public override void Update(float dt)'))]
ck('RequestStandaloneSpawnSelector("OnOpen")' in onopen and 'OpenStandaloneSpawnSelector("OnOpen")' not in onopen,'native spawn OnOpen queues rather than immediately replacing itself')
ck('if (ProcessStandaloneSpawnSelectorRequest()) return;' in update,'queued Survivor Profiles redirect executes from stable Update frame and stops updating the closed native group')
ck('standaloneRedirectPending' in gate and 'retry:' in gate,'spawn redirect fails closed and retries if replacement group is one frame late')
ck(gate.count('OpenStandaloneSpawnSelector(')==2,'standalone spawn selector open occurs only in deferred processor plus method declaration')

# Basic structural/static validation.
xml_count=0; xml_errors=[]
for p in root.rglob('*.xml'):
    xml_count+=1
    try: ET.parse(p)
    except Exception as e: xml_errors.append((p,e))
ck(not xml_errors,f'all project XML parses | XML={xml_count}')
for rel in [
'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs',
'Scripts/Survivor/UI/XUiC_RebirthSpawnSelectionSurvivorGate.cs',
'Scripts/Survivor/Backgrounds/RebirthCombatPatrolTrackingSignatureService.cs',
'Scripts/Survivor/Backgrounds/RebirthWorkmanshipSignatureService.cs',
'Scripts/Survivor/Provenance/RebirthPlacedWorkmanshipService.cs',
'Scripts/Survivor/Repair/RebirthRepairSignatureService.cs']:
    src=text(rel); ck(src.count('{')==src.count('}'),f'brace counts balanced: {Path(rel).name}')

passed=sum(1 for ok,_ in checks if ok); failed=len(checks)-passed
print(f'PC036_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={xml_count}')
if failed:
    for ok,msg in checks:
        if not ok: print('FAILED_CHECK | '+msg)
sys.exit(1 if failed else 0)
