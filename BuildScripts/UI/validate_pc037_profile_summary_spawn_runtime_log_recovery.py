#!/usr/bin/env python3
from pathlib import Path
import sys, xml.etree.ElementTree as ET

root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
checks=[]
def ck(cond,msg):
    ok=bool(cond); checks.append((ok,msg)); print(('PASS' if ok else 'FAIL')+' | '+msg)
def text(rel): return (root/rel).read_text(encoding='utf-8',errors='ignore')

menu=text('Config/XUi_Menu/windows.xml')
menu_xui=text('Config/XUi_Menu/xui.xml')
manager=text('Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs')
gate=text('Scripts/Survivor/UI/XUiC_RebirthSpawnSelectionSurvivorGate.cs')
black=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBlackMagicService.cs')
beast=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBeastmasterService.cs')
players=text('Scripts/UI/Players/XUiC_RebirthPlayers.cs')
recipes=text('Config/_Survivor/recipes.xml')
loot=text('Config/_Survivor/loot.xml')
traders=text('Config/_Survivor/traders.xml')
ingame=text('Config/XUi_InGame/windows.xml')
trader_blocks=text('Config/_Trader/blocks.xml')

# Survivor Profiles summary target requested from screenshot feedback.
tree=ET.parse(root/'Config/XUi_Menu/windows.xml'); xr=tree.getroot()
def named(name):
    for e in xr.iter():
        if e.attrib.get('name')==name: return e
    return None
for n in ['selectedProfileTitle','selectedProfileSubtitle','selectedProfileSignatureHeading','selectedProfileSignatureName','selectedProfileSignatureDescription','selectedPlayerPreview']:
    ck(named(n) is not None,f'profile summary control exists: {n}')
ck(named('selectedProfileTitle').attrib.get('pos','').startswith('18,'),'profile name moved to left edge of summary')
ck(int(named('selectedProfileTitle').attrib.get('width','0'))>=540,'profile name has wide localization-safe column')
ck(int(named('selectedProfileSubtitle').attrib.get('width','0'))>=540,'Background/Diet text uses wide left column')
ck(named('selectedProfileSignatureDescription').attrib.get('overflow')=='shrinkcontent','Signature Bonus description shrinks for longer localizations')
ck(int(named('selectedProfileSignatureDescription').attrib.get('height','0'))>=50,'Signature Bonus description has multi-line vertical capacity')
art=named('selectedProfileArt'); art_parent=None
for e in xr.iter():
    if art is not None and art in list(e): art_parent=e; break
ck(art_parent is not None and art_parent.attrib.get('visible')=='false','duplicate selected Background artwork is hidden in summary')
ck(named('selectedProfileDescription').attrib.get('visible')=='false','old Background description summary label removed from visible layout')
ck(named('selectedProfileIdentityMeta').attrib.get('visible')=='false','old Player Profile / Created metadata removed from visible layout')
ck('lblSelectedSignatureHeading = Label("selectedProfileSignatureHeading")' in manager,'profile manager binds Signature Bonus heading')
ck('RebirthSurvivorUiText.SignatureBonusName(selectedProfile.BackgroundId)' in manager,'profile manager renders authoritative Signature Bonus name')
ck('RebirthSurvivorUiText.SignatureBonusDescription(selectedProfile.BackgroundId)' in manager,'profile manager renders authoritative Signature Bonus description')
ck('SetVisible(selectedArt, false);' in manager and 'SetVisible(selectedArtPlaceholder, false);' in manager,'controller does not re-show duplicate summary Background art')

# Fatal runtime installer Harmony failure from supplied 02:10 log.
def patch_slice(src):
    p=src.find('private static bool Activated(Entity __instance')
    return src[p:p+650]
for label,src in [('Black Magic',black),('Beastmaster',beast)]:
    sl=patch_slice(src)
    ck('EntityActivationCommand __0' in sl and 'EntityPlayerLocal __1' in sl,f'{label} OnEntityActivated uses V3.2 positional Harmony args')
    ck('EntityActivationCommand command = __0; EntityPlayerLocal focusingPlayer = __1;' in sl,f'{label} maps positional args internally')
    ck('EntityActivationCommand command,EntityPlayerLocal focusingPlayer' not in sl,f'{label} no longer binds obsolete parameter names')

# XML loader failures from supplied 02:10 log.
ck('resourceNitratePowder' not in recipes,'obsolete resourceNitratePowder removed')
ck('resourcePotassiumNitratePowder' in recipes,'Rage recipe uses existing resourcePotassiumNitratePowder')
loot_root=ET.parse(root/'Config/_Survivor/loot.xml').getroot(); lc=list(loot_root)
lit_loot=next((e for e in lc if e.tag=='insertBefore' and "groupJunk" in e.attrib.get('xpath','') and any(c.attrib.get('name')=='rebirthLiteratureDistribution' for c in list(e))),None)
ref_loot=next((e for e in lc if e.tag=='append' and "groupJunk" in e.attrib.get('xpath','') and any(c.attrib.get('group')=='rebirthLiteratureDistribution' for c in list(e))),None)
ck(lit_loot is not None,'literature loot definitions are inserted before native groupJunk')
ck(ref_loot is not None and lit_loot is not None and lc.index(lit_loot)<lc.index(ref_loot),'literature loot definition patch precedes its groupJunk reference patch')
tr_root=ET.parse(root/'Config/_Survivor/traders.xml').getroot(); tc=list(tr_root)
lit_tr=next((e for e in tc if e.tag=='insertBefore' and "traderGeneral" in e.attrib.get('xpath','') and any(c.attrib.get('name')=='rebirthLiteratureTrader' for c in list(e))),None)
ref_tr=next((e for e in tc if e.tag=='append' and "traderGeneral" in e.attrib.get('xpath','') and any(c.attrib.get('group')=='rebirthLiteratureTrader' for c in list(e))),None)
ck(lit_tr is not None,'literature trader definitions are inserted before native traderGeneral')
ck(ref_tr is not None and lit_tr is not None and tc.index(lit_tr)<tc.index(ref_tr),'literature trader definition patch precedes its traderGeneral reference patch')

# XUi errors/warnings from supplied log.
old_buff_path="/rect[@controller='BuffPopoutList']/panel[@name='item']"
ck(old_buff_path not in ingame,'obsolete V3.2 BuffPopoutList child-template XPath removed')
ck("/rect[@controller='BuffPopoutList']/@pos" in ingame,'verified BuffPopoutList top-level anchor remains')
ck('name="rebirthHudBuffLayoutOwner"' in ingame,'dynamic Rebirth HUD buff layout owner remains')
ck('[XuiBindComponent("rebirthGroupIdentityNameInput", true)]' in players,'Players group-name input has explicit XUi component binding')
ck('[XuiBindEvent("OnSubmitHandler", "rebirthGroupIdentityNameInput")]' in players,'Players submit event targets the bound field')
ck('groupNameInput = rebirthGroupIdentityNameInput ??' in players,'Players runtime alias uses bound input first')
ck('defaultselected="btnProgressionExplorerHome"' in menu_xui,'Progression Explorer default selection points at Home')
ck('name="btnProgressionExplorerHome"' in menu,'Progression Explorer Home control exists')
ck('name="param1">DowngradeBlock</setattribute>' in trader_blocks,'trader shelf empty/helper inheritance excludes the exact DowngradeBlock property')
ck('name="param1">Downgrade</setattribute>' not in trader_blocks,'obsolete trader shelf downgrade exclusion token removed to prevent downgrade cycles')

# Spawn/loading-screen regression. Keep native V3.2 spawnselection alive while selector is queued.
open_start=manager.find('public static void OpenForSpawnSelection')
open_end=manager.find('public static bool OpenStandard',open_start)
spawn_open=manager[open_start:open_end]
ck('manager.Open((GUIWindow)target.windowGroup, false, true);' in spawn_open,'mandatory Survivor Profiles opens as non-modal overlay above native spawnselection')
ck('manager.Close("spawnselection")' not in spawn_open,'mandatory selector no longer closes native spawnselection during redirect')
ck('spawnOpenBefore' in spawn_open and 'spawnOpenAfter' in spawn_open,'spawn overlay contract verifies native underlay survives open request')
ck('if (!opened || !spawnOpenAfter)' in spawn_open,'spawn overlay fails closed if either selector or native underlay is lost')
ck('Survivor Profiles overlay ACTUALLY SHOWING' in manager,'next-frame trace distinguishes actually showing selector from windowsToOpen queue state')
update=gate[gate.find('public override void Update(float dt)'):gate.find('public override void OnClose',gate.find('public override void Update(float dt)'))]
ck('if (ProcessStandaloneSpawnSelectorRequest()) return;' in update,'first-entry selector redirect remains deferred out of native OnOpen')
ck('overlaying full-screen Survivor Profiles above native spawnselection reason=' in gate,'spawn trace describes overlay rather than replacement')

# Structural checks.
xml_count=0; xml_errors=[]
for p in root.rglob('*.xml'):
    xml_count+=1
    try: ET.parse(p)
    except Exception as e: xml_errors.append((p,e))
ck(not xml_errors,f'all project XML parses | XML={xml_count}')
for rel in [
'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs',
'Scripts/Survivor/UI/XUiC_RebirthSpawnSelectionSurvivorGate.cs',
'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBlackMagicService.cs',
'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBeastmasterService.cs',
'Scripts/UI/Players/XUiC_RebirthPlayers.cs']:
    src=text(rel); ck(src.count('{')==src.count('}'),f'brace counts balanced: {Path(rel).name}')

passed=sum(1 for ok,_ in checks if ok); failed=len(checks)-passed
print(f'PC037_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={xml_count}')
if failed:
    for ok,msg in checks:
        if not ok: print('FAILED_CHECK | '+msg)
sys.exit(1 if failed else 0)
