#!/usr/bin/env python3
from pathlib import Path
import re, xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
checks=[]
def c(name,ok,detail=''): checks.append((name,bool(ok),detail))
menu=(ROOT/'Config/XUi_Menu/windows.xml').read_text()
ingame=(ROOT/'Config/XUi_InGame/windows.xml').read_text()
manager=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs').read_text()
creator=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text()
store=(ROOT/'Scripts/Survivor/Persistence/RebirthSurvivorProfileStore.cs').read_text()
for rel in ['Config/XUi_Menu/windows.xml','Config/XUi_InGame/windows.xml']:
    try: ET.parse(ROOT/rel); ok=True
    except Exception as e: ok=False
    c(rel+'.parse',ok)
# Profile manager
c('profile.no_routine_review_badge','case RebirthSurvivorProfileCompatibilityKind.NeedsReview: return "[D6C978]REVIEW[-]";' not in manager)
c('profile.valid_hash_mismatch_ready','definitions updated; saved selections remain valid' in store)
c('profile.wide_native_art_xml','name="selectedProfileArt" pos="20,-46" width="876" height="293"' in menu)
c('profile.wide_native_art_binder','new RebirthSurvivorArtTextureBinder(selectedArt, 1672f / 560f)' in manager)
c('profile.full_width_art', 'name="selectedProfileArt" pos="20,-46" width="876"' in menu)
c('profile.readable_row_summary', bool(re.search(r'name="profileSummary0"[^>]*font_size="16"[^>]*color="220,220,225,255"',menu)))
c('profile.detail_scroll','name="profileDetailScroll"' in menu and '<defaultscrollbar/>' in menu)
c('profile.detail_icons',all(f'name="profileDetailIcon{i}"' in menu for i in range(18)))
c('profile.detail_controller',all(x in manager for x in ['RenderSelectedProfileDetails','SkillIconKey(skill.SkillId)','rb_ui_knowledge','diet.IconKey','trait.IconKey']))
c('profile.detail_fitted_scroll','SetControllerSize(profileDetailScrollContent, 846, Math.Max(162, entries.Count * 48));' in manager)
# Diet
c('diet.allowed_starts_at_top','const int allowedHeadingY = 0;' in creator and 'const int allowedStartY = -38;' in creator)
c('diet.dynamic_scroll_height','fittedContentHeight' in creator and 'SetControllerSize(dietDetailScrollContent, 930, fittedContentHeight);' in creator)
c('diet.no_fixed_2500','creatorDietDetailScrollContent" pos="0,0" width="930" height="2500"' not in menu)
c('diet.summary_scroll','name="dietSummaryScroll"' in menu and 'name="dietSummaryScrollContent"' in menu)
c('diet.summary_readable',all(x in menu for x in ['name="dietSelectedSummary" pos="4,-44" width="294" height="92" font_size="17"','name="dietEffectsText" pos="10,-44" width="284" height="106" font_size="17"','name="dietBenefitsText" pos="10,-44" width="284" height="78" font_size="17"','name="dietChallengesText" pos="10,-44" width="284" height="78" font_size="17"','name="dietFoodLegend" pos="10,-12" width="284" height="70" font_size="17"']))
c('diet.left_descriptions_readable',all(re.search(fr'name="dietSummary{i}"[^>]*font_size="16"[^>]*color="220,220,225,255"',menu) for i in range(4)))
c('diet.food_names_readable',bool(re.search(r'name="dietFoodName0"[^>]*height="48" font_size="16"',menu)))
# Traits
c('traits.six_taller_visible','private const int TraitSideRows = 6;' in creator)
c('traits.row_height',all(f'name="positiveTraitRow{i}" pos="6,' in menu and re.search(fr'name="positiveTraitRow{i}"[^>]*height="64"',menu) for i in range(6)))
c('traits.names_larger',all(re.search(fr'name="positiveTraitName{i}"[^>]*font_size="19"',menu) for i in range(6)))
c('traits.descriptions_larger',all(re.search(fr'name="positiveTraitSummary{i}"[^>]*font_size="16"[^>]*color="220,220,225,255"',menu) for i in range(6)))
c('traits.middle_larger','name="traitSelectedText" pos="0,0" width="500" height="118" font_size="18"' in menu and 'name="traitFocusedDetails" pos="0,0" width="438" height="128" font_size="18"' in menu)
c('traits.middle_scrollbars',menu.count('<defaultscrollbar/>')>=5)
# creator parity
def extract_window(text,name):
    st=text.index(f'<window name="{name}"'); pat=re.compile(r'<(/?)window\b[^>]*?(\/?)>'); d=0
    for m in pat.finditer(text,st):
        if m.group(1)=='/': d-=1
        elif m.group(2)!='/': d+=1
        if d==0: return text[st:m.end()]
    return ''
c('creator.parity',extract_window(menu,'rebirthSurvivorCreatorWindow')==extract_window(ingame,'rebirthSurvivorCreatorWindow'))
failed=[x for x in checks if not x[1]]
print('REBIRTH Survivor Readability Revision 8:', 'PASS' if not failed else 'FAIL')
for name,ok,detail in failed: print('FAIL',name,detail)
print(f'passed={len(checks)-len(failed)}/{len(checks)}')
print('compile_validation_claimed=False')
raise SystemExit(1 if failed else 0)
