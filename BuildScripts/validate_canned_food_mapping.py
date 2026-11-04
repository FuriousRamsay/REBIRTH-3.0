"""Check REBIRTH canned-food overrides against installed native item payloads (no game input)."""
from pathlib import Path
import copy
import json
from lxml import etree as ET

root = Path(__file__).resolve().parents[1]
native = ET.parse(str(root.parents[1] / 'Data/Config/items.xml'))
patched = copy.deepcopy(native)
patches = ET.parse(str(root / 'Config/_Metabolism/items.xml'))
factor = float(ET.parse(str(root / 'Config/_Metabolism/metabolism.xml')).find('global').get('mlPerHydrationPoint'))
names = [i.get('name') for i in native.xpath('/items/item[starts-with(@name,"foodCan")]')
         if not i.get('name').endswith('Schematic')]
for patch in patches.getroot():
    xpath = patch.get('xpath')
    if not xpath:
        continue
    for match in list(patched.xpath(xpath)):
        if not isinstance(match, ET._Element):
            continue
        item = match if match.tag == 'item' else next((a for a in match.iterancestors('item')), None)
        if item is None or item.get('name') not in names:
            continue
        if patch.tag == 'append':
            for child in patch:
                match.append(copy.deepcopy(child))
        elif patch.tag == 'remove':
            match.getparent().remove(match)

def payload(tree, name):
    item = tree.xpath('/items/item[@name=$n]', n=name)[0]
    parent = item.find('property[@name="Extends"]')
    props = payload(tree, parent.get('value')) if parent is not None else {}
    for prop in item.findall('property'):
        if prop.get('name'):
            props[prop.get('name')] = prop.get('value')
    return props

rows = []
for name in names:
    original = native.xpath('/items/item[@name=$n]', n=name)[0]
    food = original.xpath('.//triggered_effect[@cvar="$foodAmountAdd"]/@value')
    water = original.xpath('.//triggered_effect[@cvar="$waterAmountAdd"]/@value')
    expected_food = sum(map(float, food))
    expected_water = sum(map(float, water)) * factor
    values = payload(patched, name)
    assert float(values['RebirthNutritionUnits']) == expected_food, name + ' nutrition'
    assert float(values['RebirthFoodWaterMl']) == expected_water, name + ' hydration'
    item = patched.xpath('/items/item[@name=$n]', n=name)[0]
    assert not item.xpath('.//triggered_effect[@cvar="$foodAmountAdd" or @cvar="$waterAmountAdd"]'), name + ' double consumption'
    # Health and farmer healing effects are deliberately preserved.
    for cvar in ('foodHealthAmount', 'foodHealthFarmer'):
        query = './/triggered_effect[@cvar=$c]/@value'
        assert original.xpath(query, c=cvar) == item.xpath(query, c=cvar), name + ' healing changed'
    rows.append(dict(item=name, nutrition=expected_food, waterMl=expected_water))

report = root / '_Documentation/Metabolism/CANNED_FOOD_NATIVE_MAPPING_20261103.json'
report.write_text(json.dumps(dict(mlPerHydrationPoint=factor, checks='15 native item payloads, inherited properties, no duplicate food/water, healing retained', items=rows), indent=2))
print('PASS all', len(rows), 'canned foods match native nutrition and converted native hydration; healing retained.')
