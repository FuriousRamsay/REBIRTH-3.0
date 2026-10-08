#!/usr/bin/env python3
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
checks=[]

def check(name, ok, detail=''):
    checks.append((name, bool(ok), detail))

menu_path=ROOT/'Config/XUi_Menu/windows.xml'
ingame_path=ROOT/'Config/XUi_InGame/windows.xml'
menu=menu_path.read_text(encoding='utf-8')
ingame=ingame_path.read_text(encoding='utf-8')
manager=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs').read_text(encoding='utf-8')
chooser=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthChooseSurvivor.cs').read_text(encoding='utf-8')
creator=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text(encoding='utf-8')
loc=(ROOT/'Config/Localization.csv').read_text(encoding='utf-8-sig')

# XML remains well formed.
for rel in ['Config/XUi_Menu/windows.xml','Config/XUi_InGame/windows.xml']:
    try:
        ET.parse(ROOT/rel); ok=True
    except Exception:
        ok=False
    check(rel+'.xml_parse',ok)

# Compile-fix regression: properties are never passed by ref.
check('compilefix.no_positive_property_ref','ref model.PositiveTraitOffset' not in creator)
check('compilefix.no_negative_property_ref','ref model.NegativeTraitOffset' not in creator)
check('compilefix.positive_local_roundtrip',all(x in creator for x in ['int offset = model.PositiveTraitOffset;','model.PositiveTraitOffset = offset;']))
check('compilefix.negative_local_roundtrip',all(x in creator for x in ['int offset = model.NegativeTraitOffset;','model.NegativeTraitOffset = offset;']))

# Survivor Profiles approved concept.
for token in ['btnCreateProfile','profileSearchInput','btnProfileSort','profileManagerScrollTrackInput','profileManagerScrollThumb',
              'selectedProfileArt','selectedProfileTitle','selectedProfileSubtitle','selectedProfileDescription',
              'selectedProfileBonuses','selectedProfileWeaknesses','btnEditProfile','btnDeleteProfile','profileSelectionTimingHelp']:
    check('profile.xml.'+token, token in menu)
check('profile.create_visible','name="btnCreateProfile"' in menu and 'visible="false"' not in re.search(r'<labeledbutton name="btnCreateProfile"[^>]*>',menu).group(0))
check('profile.six_cards',all(f'name="profileRow{i}"' in menu for i in range(6)) and 'name="profileRow6"' not in menu)
check('profile.row_portraits',all(f'name="profileArt{i}"' in menu for i in range(6)))
check('profile.native_experience_aspect','name="selectedProfileArt" pos="20,-46" width="876" height="293"' in menu)
check('profile.no_prev_next',all(x not in menu for x in ['btnProfilePagePrev','btnProfilePageNext']))
check('profile.controller_portraits','RebirthPlayerProfilePortraitBinder[] portraitBinders' in manager and '.BindCached(playerProfileName)' in manager)
check('profile.controller_search',all(x in manager for x in ['Search_OnChanged','ProfileMatches','ApplyFilterAndSort']))
check('profile.controller_sort',all(x in manager for x in ['Sort_OnPressed','sortMode = (sortMode + 1) % 3']))
check('profile.controller_create','XUiC_RebirthSurvivorCreator.OpenCreate(xui, RefreshAfterCreator);' in manager)
check('profile.manager_no_early_use','btnUseProfile' not in menu and 'PreferProfileForNextSelection' not in manager and 'manager.Open("playGamePaging", true)' not in manager)
check('profile.chooser_is_authoritative',all(x in chooser for x in ['btnUseSelectedSurvivor','Use_OnPressed','EvaluateCompatibility(profile)','RequestCommit(player, selection, profile)']) and 'PreferProfileForNextSelection' not in chooser)
check('profile.created_date','profile.CreatedAtUtc.ToLocalTime()' in manager)
check('profile.aggregate_bonus','positiveSkillPoints' in manager and 'xuiRebirthSurvivorSkillPoints' in manager)
check('profile.aggregate_weakness','negativeSkillPoints' in manager)
check('profile.no_authored_definitions','Authored Definitions' not in menu and 'Authored Definitions' not in manager)

# Diet approved concept; the real project has four diets, not concept-only invented options.
for xml_label, xml in [('menu',menu),('ingame',ingame)]:
    for token in ['creatorDietPanel','dietFoodSearchInput','btnDietFoodFilter0','btnDietFoodFilter1','btnDietFoodFilter2','btnDietFoodFilter3','btnDietFoodFilter4',
                  'dietAllowedFoodsHeading','dietRestrictedFoodsHeading','dietSummaryPanel','dietSelectedName','dietPointBonus','dietEffectsText','dietBenefitsText','dietChallengesText']:
        check('diet.'+xml_label+'.'+token,token in xml)
    check('diet.'+xml_label+'.four_diets',all(f'name="dietRow{i}"' in xml for i in range(4)) and 'name="dietRow4"' not in xml)
check('diet.search_handler',all(x in creator for x in ['DietFoodSearch_OnChanged','dietFoodSearchText']))
check('diet.filter_handler',all(x in creator for x in ['DietFoodFilter_OnPressed','DietFoodMatchesFilter']))
check('diet.filter_domains',all(x in creator for x in ['HasDietTag(food, "Fish")','HasDietTag(food, "Plant")','HasDietTag(food, "Egg")','HasDietTag(food, "Honey")','HasDietTag(food, "Meat")','HasDietTag(food, "AnimalFat")']))
check('diet.allowed_restricted_data_driven',all(x in creator for x in ['allowedAll.Add(food)','restrictedAll.Add(food)','RebirthDietSatisfactionService.IsCompatible']))
check('diet.effects_data_driven','BuildDietEffects(diet, allowedAll.Count, restrictedAll.Count)' in creator)
check('diet.summary_actual_rule','diet.RuleSummary' in creator)
check('diet.no_rotting_flesh_hardcode','Rotting Flesh' not in creator)

# Menu and in-game creator copies must retain exact parity.
def extract_window(text,name):
    start=text.index(f'<window name="{name}"')
    pat=re.compile(r'<(/?)window\b[^>]*?(\/?)>')
    depth=0
    for m in pat.finditer(text,start):
        if m.group(1)=='/': depth-=1
        elif m.group(2)!='/': depth+=1
        if depth==0: return text[start:m.end()]
    return ''
check('creator.menu_ingame_parity',extract_window(menu,'rebirthSurvivorCreatorWindow')==extract_window(ingame,'rebirthSurvivorCreatorWindow'))

# Required localization entries are unique.
keys=['xuiRebirthSurvivorProfilesIntro','xuiRebirthSurvivorAvailableProfiles','xuiRebirthSurvivorProfilePreview',
      'xuiRebirthSurvivorUseThisProfile','xuiRebirthSurvivorSkillPoints','xuiRebirthSurvivorSelectDiet',
      'xuiRebirthDietFilterAll','xuiRebirthDietFilterFish','xuiRebirthDietFilterPlant','xuiRebirthDietFilterEggHoney',
      'xuiRebirthDietFilterMeatFat','xuiRebirthSurvivorDietEffects','xuiRebirthSurvivorDietBenefits','xuiRebirthSurvivorDietChallenges']
for key in keys:
    count=len(re.findall(r'^'+re.escape(key)+r',',loc,flags=re.M))
    check('localization.unique.'+key,count==1,f'count={count}')

failed=[x for x in checks if not x[1]]
print('REBIRTH Survivor Profile/Diet UI Revision 7:', 'PASS' if not failed else 'FAIL')
for name,ok,detail in checks:
    if not ok: print('FAIL',name,detail)
print(f'passed={len(checks)-len(failed)}/{len(checks)}')
print('compile_validation_claimed=False')
raise SystemExit(1 if failed else 0)
