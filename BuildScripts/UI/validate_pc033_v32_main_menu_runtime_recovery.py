#!/usr/bin/env python3
import argparse,csv,xml.etree.ElementTree as ET
from pathlib import Path

ap=argparse.ArgumentParser();ap.add_argument('project_root',nargs='?');ap.add_argument('--project-root',dest='project_root_opt');a=ap.parse_args()
root=Path(a.project_root_opt or a.project_root or '.').resolve()
checks=[]
def ck(name,cond,detail=''):
    checks.append((name,bool(cond),detail));print(('PASS' if cond else 'FAIL')+' | '+name+((' | '+str(detail)) if detail else ''))
def txt(rel): return (root/rel).read_text(encoding='utf-8',errors='ignore')

menu_xui=txt('Config/XUi_Menu/xui.xml'); menu_windows=txt('Config/XUi_Menu/windows.xml')
ui=txt('Scripts/Survivor/UI/RebirthSurvivorUiInstaller.cs')
pm=txt('Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs')
explorer=txt('Scripts/Survivor/UI/XUiC_RebirthProgressionExplorer.cs')
prog_inst=txt('Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs')
infra=txt('Scripts/Survivor/Progression/RebirthInfrastructureWorkService.cs')
validator=txt('Scripts/Survivor/Definitions/RebirthSurvivorAuthoringValidator.cs')
progression=ET.parse(root/'Config/_Survivor/progression.xml').getroot()
support=ET.parse(root/'Config/_Survivor/support_profiles.xml').getroot()
atlas=ET.parse(root/'UIAtlases/RebirthSurvivorIcons/settings.xml').getroot()

# V3.2 menu parser recovery.
ck('XUi_Menu xui has no unsupported if patch op',all(child.tag!='if' for child in ET.parse(root/'Config/XUi_Menu/xui.xml').getroot()))
ck('XUi_Menu windows has no unsupported if patch op',all(child.tag!='if' for child in ET.parse(root/'Config/XUi_Menu/windows.xml').getroot()))
for group in ('rebirthSurvivorProfiles','rebirthSurvivorCreator','rebirthProgressionExplorer'):
    ck('menu registers '+group, f'name="{group}"' in menu_xui)
for window in ('rebirthSurvivorProfilesWindow','rebirthSurvivorCreatorWindow','rebirthProgressionExplorerWindow'):
    ck('menu defines '+window, f'name="{window}"' in menu_windows)
ck('spawn overlay registered hidden by default','name="rebirthSurvivorSpawnGate"' in menu_windows and 'name="rebirthSurvivorSpawnGate"' in menu_windows and 'visible="false"' in menu_windows[menu_windows.find('name="rebirthSurvivorSpawnGate"'):menu_windows.find('name="rebirthSurvivorSpawnGate"')+300])
ck('main-menu Survivor button runtime Rebirth gate','button.ViewComponent.IsVisible = rebirthMode' in ui and 'ConfiguredMode != RebirthPlayerProgressionMode.Rebirth' in ui)
ck('main-menu Explorer button runtime Rebirth gate','MAIN-MENU wiring progressionExplorerHook=True rebirthMode=' in ui and 'ConfiguredMode != RebirthPlayerProgressionMode.Rebirth' in ui)
ck('Survivor group registration preflight','FindWindowGroupByName(WindowGroupId)' in ui and "leaving Main Menu open" in ui)
ck('Survivor open registration preflight','Cannot open Survivor Profiles' in pm)
ck('Survivor open failure restores Main Menu','manager.Open("mainMenu", true)' in pm)
ck('Survivor open uses V3.2 GUIWindow overload','manager.Open((GUIWindow)target.windowGroup, true, false)' in pm and 'true, false, false' not in pm)
ck('Explorer open uses V3.2 GUIWindow overload','windowManager.Open((GUIWindow)controller.windowGroup,true,false)' in explorer and 'true,false,false' not in explorer)

# Explorer authority is built before risky gameplay Harmony hooks.
i_adv=prog_inst.find('RebirthAdvancedDisciplineRegistry.Load()');i_graph=prog_inst.find('RebirthProgressionGraphRegistry.BuildFromCurrentAuthority()');i_infra=prog_inst.find('RebirthInfrastructureWorkService.Install')
ck('advanced discipline authority loads before infrastructure',0<=i_adv<i_infra,(i_adv,i_infra))
ck('graph builds before infrastructure',0<=i_graph<i_infra,(i_graph,i_infra))

# V3.2 construction hook: no frozen old or guessed new signature.
ck('construction patch uses dynamic Harmony target','[HarmonyPatch]\npublic static class RebirthConstructionDamageBlockCommitPatch' in infra and 'private static MethodBase TargetMethod()' in infra)
ck('construction patch scans OnBlockDamaged','candidate.Name!="OnBlockDamaged"' in infra)
ck('construction patch has no fixed DamageBlock attribute','[HarmonyPatch(typeof(Block),nameof(Block.DamageBlock)' not in infra)
ck('construction patch has no direct BlockValueRef compile dependency','typeof(BlockValueRef)' not in infra and 'BlockValueRef _' not in infra)
ck('construction position supports Vector3i','pt==typeof(Vector3i)' in infra)
ck('construction position supports reflected BlockValueRef','string.Equals(pt.Name,"BlockValueRef"' in infra and 'TryGetBlockPos' in infra and 'TryReadVector3iMember' in infra)
ck('construction position has AttackHitInfo fallback','hit.raycastHitPosition' in infra)
ck('construction hook failure does not abort progression','damage-commit hook unavailable; continuing progression installation' in infra)

# Startup authoring drift seen in supplied log.
skills={e.get('id'):e for e in progression.findall('./skills/skill')}
for sid in ('skill.drink_preparation','skill.trading','skill.teaching'):
    s=skills.get(sid);ck(sid+' signed runtime bounds',s is not None and s.get('min')=='-50' and s.get('max')=='100')
ck('validator separates unavailable from implementation state','Availability is a creation/selectability contract' in validator)
ck('validator no longer freezes Chunk 3 state counts','expected 39/13/13/72' not in validator and 'expectedPartialTraits' not in validator)
ck('validator accepts generic runtime trait targets','SupportsGenericTraitTarget(v)' in validator)

# Support effects must be in loader-owned <effects> containers.
profiles={e.get('id'):e for e in support.findall('./support_profile')}
for pid in ('gear.outerwear.cold_weather_lining','gear.outerwear.hot_weather_shell'):
    p=profiles.get(pid);ck(pid+' has effects container',p is not None and p.find('./effects/effect') is not None)

# Atlas registrations reported missing in live log.
sprites={e.get('name') for e in atlas.findall('./sprite')}
for spr in ('rb_trait_fast_reader','rb_trait_slow_reader','rb_skill_drink_preparation','rb_skill_trading','rb_skill_teaching','rb_skill_rage','rb_skill_black_magic'):
    ck('atlas registers '+spr,spr in sprites)
    ck('atlas file exists '+spr,(root/'UIAtlases/RebirthSurvivorIcons'/(spr+'.png')).is_file())

# Localization malformed comma row is quoted/parseable as two columns.
loc=root/'Config/Localization.csv';target_rows=[]
with loc.open(encoding='utf-8-sig',newline='') as f:
    for n,row in enumerate(csv.reader(f),1):
        if row and row[0]=='xuiRebirthKnowledgeCookingPreservation': target_rows.append((n,row))
ck('Cooking Preservation localization row is exactly two columns',len(target_rows)==1 and len(target_rows[0][1])==2,target_rows)

# All XML parse.
xml_files=list(root.rglob('*.xml'));xml_bad=[]
for p in xml_files:
    try: ET.parse(p)
    except Exception as e: xml_bad.append((str(p.relative_to(root)),str(e)))
ck('all project XML parses',not xml_bad,'XML='+str(len(xml_files)) if not xml_bad else xml_bad[:3])

# Basic C# delimiter sanity on every modified runtime file.
for rel in ('Scripts/Survivor/Progression/RebirthInfrastructureWorkService.cs','Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs','Scripts/Survivor/UI/RebirthSurvivorUiInstaller.cs','Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs','Scripts/Survivor/UI/XUiC_RebirthProgressionExplorer.cs','Scripts/Survivor/Definitions/RebirthSurvivorAuthoringValidator.cs'):
    s=txt(rel);ck('delimiter '+Path(rel).name,s.count('{')==s.count('}') and s.count('(')==s.count(')'))

passed=sum(1 for _,ok,_ in checks if ok);failed=len(checks)-passed
print(f'PC033_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={len(xml_files)}')
raise SystemExit(1 if failed else 0)
