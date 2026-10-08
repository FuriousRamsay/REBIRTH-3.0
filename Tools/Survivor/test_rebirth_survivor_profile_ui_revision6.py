#!/usr/bin/env python3
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
checks=[]

def check(name, ok, detail=''):
    checks.append((name, bool(ok), detail))

loc=(ROOT/'Config/Localization.csv').read_text(encoding='utf-8-sig')
menu=(ROOT/'Config/XUi_Menu/windows.xml').read_text(encoding='utf-8')
ingame=(ROOT/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
manager=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs').read_text(encoding='utf-8')
creator=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text(encoding='utf-8')
factory=(ROOT/'Scripts/Survivor/Definitions/RebirthSkillAptitudeTraitFactory.cs').read_text(encoding='utf-8')
validator=(ROOT/'Scripts/Survivor/Definitions/RebirthSurvivorAuthoringValidator.cs').read_text(encoding='utf-8')
traits=(ROOT/'Config/_Survivor/traits.xml').read_text(encoding='utf-8')

# XML well formed
for rel in ['Config/XUi_Menu/windows.xml','Config/XUi_InGame/windows.xml','Config/_Survivor/traits.xml']:
    try:
        ET.parse(ROOT/rel); ok=True
    except Exception as exc:
        ok=False
    check(rel+'.xml_parse', ok)

# Player-facing terminology; internal BackgroundId remains intentionally compatible.
for key, value in [
    ('xuiRebirthSurvivorStepBackground','2. EXPERIENCE'),
    ('xuiRebirthSurvivorStepBackgroundShort','EXPERIENCE'),
    ('xuiRebirthSurvivorReviewBackground','EXPERIENCE'),
    ('xuiRebirthSurvivorBackground','Experience')]:
    check('localization.'+key, f'{key},{value}' in loc, value)
check('localization.no_authored_definitions', 'Authored definitions' not in loc)

# Aptitude Level III removed from generated catalogue and authored conflicts.
check('aptitudes.max_tier_2', 'public const int MaxTier = 2;' in factory)
check('aptitudes.generate_through_max', 'tier <= MaxTier' in factory)
check('aptitudes.parse_rejects_above_max', 'parsedTier > MaxTier' in factory)
check('aptitudes.validator_84', 'expected 84 Skill Aptitudes' in validator or '84' in validator)
check('aptitudes.no_tier3_xml_refs', not re.search(r'trait\.aptitude\.[^"\s]+\.3"', traits))

# Traits use independent scrollbars, not per-pane paging buttons.
for label, xml in [('menu',menu),('ingame',ingame)]:
    check(label+'.positive_trait_scrollbar', all(x in xml for x in ['positiveTraitScrollTrackInput','positiveTraitScrollThumb']))
    check(label+'.negative_trait_scrollbar', all(x in xml for x in ['negativeTraitScrollTrackInput','negativeTraitScrollThumb']))
    check(label+'.no_trait_page_buttons', all(x not in xml for x in ['btnPositiveTraitPrev','btnPositiveTraitNext','btnNegativeTraitPrev','btnNegativeTraitNext']))
check('controller.trait_thumb_drag', all(x in creator for x in ['PositiveTraitThumbDrag','NegativeTraitThumbDrag','UpdateScrollBar']))

# Survivor Profile manager list rows expose real labels and use a scrollbar.
for i in range(6):
    check(f'profile.row{i}_labels', all(x in menu for x in [f'profileName{i}',f'profileSummary{i}',f'profileStatus{i}',f'profileSelection{i}']))
check('profile.scrollbar', all(x in menu for x in ['profileManagerScrollTrackInput','profileManagerScrollThumb']))
check('profile.no_page_buttons', all(x not in menu for x in ['btnProfilePagePrev','btnProfilePageNext']))
check('profile.controller_name_binding', 'SetLabel(rowNames[i], profile.ProfileName);' in manager)
check('profile.controller_summary_binding', 'BuildProfileRowSummary(profile, experience, diet)' in manager)
check('profile.controller_scroll', all(x in manager for x in ['ProfileListScroll','ProfileThumbDrag','UpdateScrollBar']))

# Selected artwork uses exact 16:9 dimensions and binder.
check('profile.selected_art_native_wide_xml', 'name="selectedProfileArt" pos="20,-46" width="876" height="293"' in menu)
check('profile.selected_placeholder_native_wide_xml', 'name="selectedProfileArtPlaceholder" pos="20,-46" width="876" height="293"' in menu)
check('profile.selected_art_native_wide_binder', 'new RebirthSurvivorArtTextureBinder(selectedArt, 1672f / 560f)' in manager)

# Detail presentation no longer exposes implementation metadata and is grouped.
check('profile.no_authored_metadata', all(x not in manager for x in ['xuiRebirthSurvivorAuthoredVersion','Authored definitions']))
check('profile.identity_section', all(x in manager for x in ['BuildProfileSubtitle','BuildProfileDescription']))
check('profile.status_section', all(x in manager for x in ['BuildStartingBonuses','BuildStartingWeaknesses']))
check('profile.experience_label', '"Experience"' in manager)

# Creator menu/in-game copies must remain identical.
def creator_window(text):
    start=text.index('<window name="rebirthSurvivorCreatorWindow"')
    pat=re.compile(r'<(/?)window\b[^>]*?(\/?)>')
    depth=0
    for m in pat.finditer(text,start):
        if m.group(1)=="/": depth-=1
        elif m.group(2)!="/": depth+=1
        if depth==0: return text[start:m.end()]
    return ''
check('creator.menu_ingame_parity', creator_window(menu)==creator_window(ingame) and bool(creator_window(menu)))

failed=[x for x in checks if not x[1]]
print('REBIRTH Survivor Profile/Experience UI Revision 6 vectors:', 'PASS' if not failed else 'FAIL')
for name, ok, detail in checks:
    print(('PASS ' if ok else 'FAIL ')+name+((' — '+detail) if detail else ''))
print(f'pass={len(checks)-len(failed)} fail={len(failed)} total={len(checks)}')
print('compile_validation_claimed=False')
raise SystemExit(1 if failed else 0)
