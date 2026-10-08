#!/usr/bin/env python3
from pathlib import Path
import sys, re, xml.etree.ElementTree as ET

root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
checks=[]
def ck(cond,msg):
    ok=bool(cond); checks.append((ok,msg)); print(('PASS' if ok else 'FAIL')+' | '+msg)
def text(rel): return (root/rel).read_text(encoding='utf-8',errors='ignore')

uit=text('Scripts/Survivor/UI/RebirthSurvivorUiText.cs')
creator=text('Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs')
manager=text('Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs')
gate=text('Scripts/Survivor/UI/XUiC_RebirthSpawnSelectionSurvivorGate.cs')
first=text('Scripts/Survivor/UI/RebirthSurvivorFirstEntryUiService.cs')
menu=text('Config/XUi_Menu/windows.xml')
hunter=text('Scripts/Survivor/Backgrounds/RebirthCombatPatrolTrackingSignatureService.cs')
black=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBlackMagicService.cs')
beast=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBeastmasterService.cs')
placed=text('Scripts/Survivor/Provenance/RebirthPlacedWorkmanshipService.cs')

# Shared presentation order contract.
ck('OrderedStartingKnowledgeIds' in uit,'shared Starting Knowledge ordering helper exists')
ck('StartingKnowledgeDisplayGroup' in uit,'shared Starting Knowledge display-group resolver exists')
ck('StartsWith("recipebook."' in uit and 'return 1;' in uit,'recipe books sort after techniques/non-recipe Knowledge')
ck('StartsWith("recipe."' in uit and 'return 2;' in uit,'individual recipes sort after recipe books')
ck('StringComparer.CurrentCultureIgnoreCase.Compare(nameA, nameB)' in uit,'entries sort alphabetically by localized display name inside each Knowledge type')
ck('ResolveDefinitionName(a)' in uit and 'ResolveDefinitionName(b)' in uit,'alphabetical ordering uses player-facing names')

# Typed icons remain distinct.
ck('StartsWith("recipebook."' in uit and 'return "rb_bonus_bookworm"' in uit,'recipe books retain dedicated book icon')
ck('StartsWith("procedure.cooking."' in uit and 'return "rb_skill_cooking"' in uit,'Cooking procedures retain dedicated procedure icon')
ck('return "rb_ui_knowledge";' in uit,'individual recipes retain recipe/Knowledge icon fallback')

# Experience Starting Knowledge uses three visible rows then native scrolling.
ck('BackgroundKnowledgeRows = 3' in creator,'Experience Starting Knowledge has three visible rows')
ck('BackgroundKnowledgeTrackHeight = 90' in creator,'Experience Starting Knowledge native scroll track matches three-row viewport')
for n in range(3):
    ck(f'name="backgroundKnowledgeRow{n}"' in menu,f'XUi_Menu authors Starting Knowledge row {n}')
ck('name="backgroundKnowledgeNativeScrollHost"' in menu and '<defaultscrollbar/>' in menu,'Experience Starting Knowledge uses default/native scrollbar')
ck('name="backgroundKnowledgeNativeScrollView"' in menu and 'height="90"' in menu[menu.find('name="backgroundKnowledgeNativeScrollView"'):menu.find('name="backgroundKnowledgeNativeScrollView"')+180],'Experience Starting Knowledge scrollbar viewport is 90 high')
ck('OrderedStartingKnowledgeIds(bg.StartingKnowledgeIds)' in creator,'Experience screen renders ordered Starting Knowledge')

# Review / reusable profile summary: Skills first, ordered Knowledge second.
creator_review=creator[creator.find('List<string> knowledge = bg != null'):creator.find('List<RebirthTraitDefinition>',creator.find('List<string> knowledge = bg != null'))]
ck('OrderedStartingKnowledgeIds(bg.StartingKnowledgeIds)' in creator_review,'Review combined list uses ordered Knowledge')
ck(creator_review.find('StartingSkills') < creator_review.find('OrderedStartingKnowledgeIds'),'Review builds Skills before Knowledge')
manager_block=manager[manager.find('List<ProfileDetailDisplay> progression'):manager.find('RenderDetailSection',manager.find('List<ProfileDetailDisplay> progression'))]
ck('OrderedStartingKnowledgeIds(background.StartingKnowledgeIds)' in manager_block,'Survivor Profiles summary uses ordered Knowledge')
ck(manager_block.find('StartingSkills') < manager_block.find('OrderedStartingKnowledgeIds'),'Survivor Profiles summary builds Skills before Knowledge')

# First-spawn route: no more giant embedded browser presentation.
ck('ViewComponent.IsVisible = true' not in gate,'spawnselection bridge never makes legacy embedded browser visible')
ck(gate.count('ApplyNativeLayout(')==1,'legacy native spawn layout mutator is no longer called')
ck('OpenStandaloneSpawnSelector("OnOpen")' in gate or 'RequestStandaloneSpawnSelector("OnOpen")' in gate,'spawnselection OnOpen routes mandatory first entry to standalone Survivor Profiles')
ck('XUiC_RebirthSurvivorProfileManager.OpenForSpawnSelection(xui)' in gate,'spawn bridge delegates to full-screen reusable profile manager')
ck('mandatory Survivor Profiles group is not registered; native spawn remains locked' in gate,'missing profile group fails closed instead of exposing Spawn')
ck('if (stagedSelectionReady) return false;' in first,'staged profile hides first-entry selector before returning to native spawn')
ck('profileManagerOwnsTransition = XUiC_RebirthSurvivorProfileManager.IsSpawnSelectionMode' in first,'profile manager owns stage-to-native-spawn transition')
ck('SetGateVisible(false, "selection-staged")' in first,'staging explicitly clears legacy embedded gate')

# Full-screen spawn selection manager safety and Ready-only policy.
open_start=manager.find('public static void OpenForSpawnSelection')
open_end=manager.find('public static bool OpenStandard',open_start)
open_block=manager[open_start:open_end]
ck('FindWindowGroupByName(WindowGroupId)' in open_block,'spawn selection preflights full-screen profile group before opening selector')
ck('manager.Open((GUIWindow)target.windowGroup, false, true)' in open_block,'full-screen profile manager uses V3.2-safe non-modal GUIWindow overlay overload')
ck('manager.Close("spawnselection")' not in open_block and 'if (!manager.IsWindowOpen("spawnselection")) manager.Open("spawnselection", true);' in open_block,'mandatory full-screen selector preserves/restores native spawnselection fail-closed')
ck('compatibility.Kind != RebirthSurvivorProfileCompatibilityKind.Ready' in manager,'spawn selection lists only Ready profiles')
ck('if (!manager.IsWindowOpen("spawnselection")) manager.Open("spawnselection", true);' in manager,'successful Select For World returns to stock spawn screen')
ck('Survivor Profiles BACK during mandatory first-entry selection; leaving world.' in manager and 'RebirthSurvivorFirstEntryUiService.LeaveWorld();' in manager,'Back from mandatory first-entry profile step safely leaves world')

# V3.2 fatal Harmony parameter-name compatibility from supplied log.
for name,src in [('Hunter',hunter),('Black Magic',black),('Beastmaster',beast)]:
    segment=src[src.find('InitLocalActivationCommands')-220:src.find('InitLocalActivationCommands')+700]
    ck('Action<EntityActivationCommand> __0' in segment,f'{name} activation patch uses positional V3.2 callback argument')
    ck('addCallback' not in segment,f'{name} activation patch no longer depends on old addCallback parameter name')
ck('public static void Prefix(WorldBase __0,BlockPlacement.Result __1,EntityAlive __2,ref int __state)' in placed,'placed-workmanship PlaceBlock Prefix uses positional V3.2 arguments')
ck('public static void Postfix(WorldBase __0,BlockPlacement.Result __1,EntityAlive __2,ref int __state)' in placed,'placed-workmanship PlaceBlock Postfix uses positional V3.2 arguments')
patch_block=placed[placed.find('public static class RebirthPlacedWorkmanshipPlacementPatch'):placed.find('/// <summary>',placed.find('public static class RebirthPlacedWorkmanshipPlacementPatch'))]
ck('BlockPlacement.Result _result' not in patch_block,'placed-workmanship patch no longer binds removed _result parameter name')

# Key C# structural balance for files touched by this fix.
for rel in [
'Scripts/Survivor/UI/RebirthSurvivorUiText.cs',
'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs',
'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs',
'Scripts/Survivor/UI/XUiC_RebirthSpawnSelectionSurvivorGate.cs',
'Scripts/Survivor/UI/RebirthSurvivorFirstEntryUiService.cs',
'Scripts/Survivor/Backgrounds/RebirthCombatPatrolTrackingSignatureService.cs',
'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBlackMagicService.cs',
'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBeastmasterService.cs',
'Scripts/Survivor/Provenance/RebirthPlacedWorkmanshipService.cs']:
    src=text(rel); ck(src.count('{')==src.count('}'),f'brace counts balanced: {Path(rel).name}')

xml_count=0; errs=[]
for p in root.rglob('*.xml'):
    xml_count+=1
    try: ET.parse(p)
    except Exception as e: errs.append((str(p.relative_to(root)),str(e)))
ck(not errs,f'all project XML parses | XML={xml_count}')

passed=sum(1 for ok,_ in checks if ok); failed=len(checks)-passed
print(f'PC035_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={xml_count}')
if failed:
    for ok,msg in checks:
        if not ok: print('FAILED_CHECK | '+msg)
sys.exit(1 if failed else 0)
