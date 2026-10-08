#!/usr/bin/env python3
from pathlib import Path
import re, xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
errors=[]
menu=(ROOT/'Config/XUi_Menu/windows.xml').read_text(encoding='utf-8')
manager=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs').read_text(encoding='utf-8')
backgrounds=ET.parse(ROOT/'Config/_Survivor/backgrounds.xml').getroot()
ET.parse(ROOT/'Config/XUi_Menu/windows.xml')
# New layout geometry.
for token in [
    'name="selectedIdentityPanel" pos="16,-48" width="884" height="220"',
    'name="selectedPlayerPreview" pos="590,-8" width="278" height="203"',
    'name="profileSummaryChoices" pos="16,-282" width="884" height="370"',
    'name="profileStartingItemRow0" pos="576,-138" width="272" height="28"',
    'name="profileStartingItemIcon0"',
    'name="profileStartingItemName0"',
    'name="profileStartingItemMeta0"',
]:
    if token not in menu: errors.append('missing layout token: '+token)
# Starting items must live in the lower summary panel, not the identity panel.
i0=menu.find('<rect name="selectedIdentityPanel"')
i1=menu.find('</rect>', i0)
# Use explicit known section boundary because nested rects exist.
sum_start=menu.find('<rect name="profileSummaryChoices"',i0)
identity_segment=menu[i0:sum_start]
summary_end=menu.find('<label name="selectedProfileBonuses"',sum_start)
summary_segment=menu[sum_start:summary_end]
if 'selectedProfileStartingItems' in identity_segment: errors.append('starting items still in identity panel')
if 'profileStartingItemRow0' not in summary_segment: errors.append('starting items not rendered as rows under weaknesses')
# All current backgrounds must fit the complete progression/weakness columns.
max_progression=0; max_weakness=0; max_progression_id=''; max_weakness_id=''
for b in backgrounds.findall('.//background'):
    pos=neg=0
    for s in b.findall('./starting_skills/skill'):
        try: value=float(s.get('value','0'))
        except ValueError: value=0
        if value>0: pos+=1
        elif value<0: neg+=1
    knowledge=len(b.findall('./starting_knowledge/knowledge'))
    if pos+knowledge>max_progression:
        max_progression=pos+knowledge; max_progression_id=b.get('id','')
    if neg>max_weakness:
        max_weakness=neg; max_weakness_id=b.get('id','')
if max_progression>10: errors.append(f'progression capacity 10 too small: {max_progression} {max_progression_id}')
if max_weakness>2: errors.append(f'weakness capacity 2 too small: {max_weakness} {max_weakness_id}')
# Hunter regression: seven positive skills + one knowledge must render as 8 entries.
hunter=next((b for b in backgrounds.findall('.//background') if b.get('id')=='background.hunter'),None)
if hunter is None: errors.append('hunter background missing')
else:
    hp=sum(1 for s in hunter.findall('./starting_skills/skill') if float(s.get('value','0'))>0)
    hk=len(hunter.findall('./starting_knowledge/knowledge'))
    if (hp,hk)!=(7,1): errors.append(f'hunter expected 7 skills + 1 knowledge, got {hp}+{hk}')
# Rows compacted while keeping the name font at 16.
for i in range(20):
    m=re.search(rf'<rect name="profileDetailRow{i}"[^>]*height="(\d+)"',menu)
    if not m or int(m.group(1))!=29: errors.append(f'row {i} not height 29')
    m=re.search(rf'<sprite name="profileDetailIcon{i}"[^>]*width="(\d+)" height="(\d+)"',menu)
    if not m or m.group(1)!='22' or m.group(2)!='22': errors.append(f'row {i} icon not 22x22')
    m=re.search(rf'<label name="profileDetailName{i}"[^>]*font_size="(\d+)"',menu)
    if not m or m.group(1)!='16': errors.append(f'row {i} name font changed')
for token in [
    'private const int ProgressionVisibleRows = 10;',
    'private const int TraitVisibleRows = 8;',
    'private const int WeaknessVisibleRows = 2;',
    'RenderDetailSection(0, ProgressionVisibleRows, progression, 0, false);',
    'RenderDetailSection(TraitBaseIndex, TraitVisibleRows, traits, traitOffset, true);',
    'RenderDetailSection(WeaknessBaseIndex, WeaknessVisibleRows, weaknesses, 0, false);',
    'RebirthSurvivorUiText.StartingItemsBullets(background)',
]:
    if token not in manager: errors.append('missing controller token: '+token)
print(f'Profile manager compact summary audit errors={len(errors)} max_progression={max_progression}({max_progression_id}) max_weakness={max_weakness}({max_weakness_id})')
for e in errors: print('ERROR:',e)
raise SystemExit(1 if errors else 0)
