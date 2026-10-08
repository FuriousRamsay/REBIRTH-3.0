"""Check visible backpack guidance against authored equip rules; no runtime claim."""
from pathlib import Path
import csv
import re
import xml.etree.ElementTree as E

root = Path(__file__).resolve().parents[2]
with (root / 'Config/Localization.csv').open(encoding='utf-8-sig', newline='') as stream:
    loc = dict(row for row in csv.reader(stream) if len(row) == 2)
profiles = E.parse(root / 'Config/_Survivor/support_profiles.xml')
packs = [p for p in profiles.iter('support_profile') if p.get('gear_slot_id') == 'backpack']
assert len(packs) == 8, 'Review backpack tier coverage when adding/removing tiers'
for pack in packs:
    item = pack.get('gear_item_id')
    description = re.sub(r'\[[0-9A-Fa-f]{6}\]|\[-\]', '', loc[item + 'Desc'])
    lines = description.split('\\n')
    strength, constitution, bonus = (int(pack.get(k)) for k in ('min_strength', 'min_constitution', 'bag_slot_bonus'))
    requirement = 'None.' if strength == constitution == 0 else f'Strength {strength} and Constitution {constitution}.'
    assert lines[0] == 'Equip requirements: ' + requirement, (item, 'equip requirement mismatch')
    assert bonus % 13 == 0, (item, 'partial backpack row')
    rows = bonus // 13
    assert f'+{bonus} usable slots ({rows} row' + ('s' if rows != 1 else '') + ').' in lines[1], (item, 'capacity mismatch')
    assert 'Keeps the same 26 encumbered slots.' in lines, (item, 'encumbrance guidance missing')
belts = [p for p in profiles.iter('support_profile') if p.get('gear_slot_id') == 'belt' and p.get('toolbelt_slot_bonus')]
assert len(belts) == 6, 'Review toolbelt tier coverage'
for belt in belts:
    item = belt.get('gear_item_id')
    assert float(belt.get('min_strength', '0')) == float(belt.get('min_constitution', '0')) == 0, (item, 'update requirement description')
    description = re.sub(r'\[[0-9A-Fa-f]{6}\]|\[-\]', '', loc[item + 'Desc'])
    total = 4 + int(belt.get('toolbelt_slot_bonus'))
    assert 'No attribute requirements.' in description, item
    assert f'{total} total slots ({total + 2} with Tools at Hand).' in description, (item, 'capacity mismatch')
    assert 'remain for 30 minutes.' in description, (item, 'recovery duration missing')
print(f'PASS: {len(packs)} backpack and {len(belts)} toolbelt descriptions match authored requirements and bonuses. Source only.')

# Every other authored Survivor gear item must state its attribute requirements too.
other = [p for p in profiles.iter('support_profile') if p.get('kind') == 'survivor_gear' and p not in packs and p not in belts]
assert len(other) == 5, 'Review additional gear coverage'
for gear in other:
    item = gear.get('gear_item_id')
    assert float(gear.get('min_strength', '0')) == float(gear.get('min_constitution', '0')) == 0, (item, 'update requirement description')
    assert 'No attribute requirements.' in loc[item + 'Desc'], item
print(f'PASS: {len(other)} additional Survivor gear descriptions expose their attribute requirements. Source only.')
