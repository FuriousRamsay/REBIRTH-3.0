from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[2]
errors=[]
for rel in [Path('Config/XUi_Menu/windows.xml'),Path('Config/XUi_InGame/windows.xml')]:
    text=(ROOT/rel).read_text(encoding='utf-8')
    for token in [
        'backgroundSkillSummaryPanel" pos="24,-220" width="300"',
        'backgroundKnowledgeSummaryPanel" pos="334,-220" width="256" height="150"',
        'backgroundWeaknessAndPointsPanel" pos="600,-220" width="260" height="150"',
        'backgroundStartingItemsPanel" pos="334,-380" width="526" height="178"',
        'backgroundTraitPointsValue" pos="226,-136"',
        'text_key="xuiRebirthSurvivorBackgroundListBonusTraitPoints"',
    ]:
        if token not in text: errors.append(f'{rel}: missing approved layout token {token}')
    for i in range(8):
        if f'backgroundStartingItemRow{i}' not in text: errors.append(f'{rel}: missing starting item row {i}')
    # Verify the item grid actually has both columns and four rows.
    expected={0:(0,-40,256),1:(266,-40,260),2:(0,-74,256),3:(266,-74,260),4:(0,-108,256),5:(266,-108,260),6:(0,-142,256),7:(266,-142,260)}
    for i,(x,y,w) in expected.items():
        if f'backgroundStartingItemRow{i}" pos="{x},{y}" width="{w}"' not in text:
            errors.append(f'{rel}: row {i} not aligned to Knowledge/Weakness column geometry')

# Verify every currently authored Experience fits the approved no-scroll layout.
import xml.etree.ElementTree as ET
r=ET.parse(ROOT/'Config/_Survivor/backgrounds.xml').getroot()
max_pos=max_knowledge=max_items=max_negative=0
for bg in r.findall('.//background'):
    pos=neg=0
    for s in bg.findall('./starting_skills/*'):
        raw=s.get('value')
        if raw is None:
            pos += 1
            continue
        try: value=float(raw)
        except ValueError: value=0
        if value>0: pos+=1
        elif value<0: neg+=1
    max_pos=max(max_pos,pos)
    max_negative=max(max_negative,neg)
    max_knowledge=max(max_knowledge,len(bg.findall('./starting_knowledge/*')))
    max_items=max(max_items,len(bg.findall('./starting_items/*')))
if max_pos>9: errors.append(f'authored positive skills exceed visible compact list: {max_pos}>9')
if max_knowledge>2: errors.append(f'authored knowledge exceeds visible list: {max_knowledge}>2')
if max_negative>2: errors.append(f'authored weaknesses exceed visible list: {max_negative}>2')
if max_items>8: errors.append(f'authored starting items exceed visible grid: {max_items}>8')

cs=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text(encoding='utf-8')
for token in ['BackgroundStartingItemRows = 8','BackgroundStartingItemTrackHeight = 132','IsVisible = total > BackgroundStartingItemRows']:
    if token not in cs: errors.append(f'creator: missing {token}')
if errors:
    print('FAIL')
    for e in errors: print(' -',e)
    raise SystemExit(1)
print('PASS: approved Experience reorganization is present in menu and in-game creator UI.')
