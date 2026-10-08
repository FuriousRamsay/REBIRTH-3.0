"""Validate cooking content references against native and mod definitions."""
from pathlib import Path
from collections import Counter
import csv
import json
import xml.etree.ElementTree as ET
from content_model import ROOT, PRODUCTION, ASSETS, DIET_TAGS, CATALOGUE
from card_policy import NO_CARD_ITEMS

def validate_policy_implementations(progression):
    """Mirror the runtime registry's policy/implementation contract."""
    errors = []
    allowed = {'universal': {'policy_declared'}, 'disabled': {'runtime_disabled'},
               'gated': {'existing_capability', 'planned_capability'}}
    seen = set()
    for node in progression.findall('recipes/recipe'):
        ident = node.get('id', '').strip()
        policy = node.get('policy', '').strip().lower()
        implementation = node.get('implementation', '').strip().lower()
        if not ident or ident.lower() in seen:
            errors.append('Missing or duplicate crafting policy ID '+ident)
        seen.add(ident.lower())
        if implementation not in allowed.get(policy, set()):
            errors.append('Invalid crafting implementation '+ident+': '+policy+'/'+implementation)
        if not node.get('family', '').strip():
            errors.append('Missing crafting family '+ident)
        capability = node.get('capability', '').strip()
        if policy == 'gated':
            if not capability or not node.get('primary_skill', '').strip():
                errors.append('Missing gated crafting capability or skill '+ident)
        elif capability:
            errors.append('Non-gated crafting recipe references capability '+ident)
    return errors

def validate():
    errors = []
    native = ROOT.parent.parent/'Data/Config'
    covered_native = {CATALOGUE['recipes'][card['recipe']]['game_id'] for card in CATALOGUE['cards']}
    for name in covered_native & NO_CARD_ITEMS:
        errors.append('Basic preparation must not have a card '+name)
    for recipe in ET.parse(native/'recipes.xml').iter('recipe'):
        name = recipe.get('name', '')
        if (name.startswith(('food','drink')) and name not in
                (NO_CARD_ITEMS | {'foodRawMeat','foodRawMeatBundle','drinkJarEmpty'}) and name not in covered_native):
            errors.append('Missing native food/drink recipe card '+name)
    items, blocks = set(), set()
    for folder in (native, ROOT/'Config'):
        paths = list(folder.rglob('*.xml'))
        for path in paths:
            try:
                root = ET.parse(path).getroot()
            except ET.ParseError:
                continue
            items.update(n.get('name') for n in root.iter('item') if n.get('name') and n.find('property') is not None)
            blocks.update(n.get('name') for n in root.iter('block') if n.get('name') and n.find('property') is not None)
    known = items|blocks
    directory = ROOT/'Config/_Cooking'
    materials = {n.get('id') for n in ET.parse(native/'materials.xml').iter('material')}
    for path in (ROOT/'Config').rglob('*.xml'):
        if path.name == 'materials.xml':
            materials.update(n.get('id') for n in ET.parse(path).iter('material'))
    for filename in ('blocks.xml', 'items.xml'):
        for node in ET.parse(directory/filename).iter():
            if node.tag not in ('block', 'item'):
                continue
            material = node.find("property[@name='Material']")
            if material is not None and material.get('value') not in materials:
                errors.append('Unknown material '+str(material.get('value'))+' on '+str(node.get('name')))
    definitions = ET.parse(directory/'items.xml').getroot()
    recipes = ET.parse(directory/'recipes.xml').getroot()
    names = [n.get('name') for n in definitions.iter('item')]
    item_nodes = {n.get('name'): n for n in definitions.iter('item')}
    errors += ['Duplicate cooking item '+name for name,count in Counter(names).items() if count>1]
    for folder in (native, ROOT/'Config'):
        for path in folder.rglob('*.xml'):
            if directory in path.parents:
                continue
            try:
                other = ET.parse(path).getroot()
            except ET.ParseError:
                continue
            for node in other.iter('item'):
                if node.get('name') in names and node.find('property') is not None:
                    errors.append('Cooking item already defined in '+str(path)+': '+node.get('name'))
    for node in recipes.iter('recipe'):
        if node.get('name') not in known:
            errors.append('Unknown recipe output '+str(node.get('name')))
        if len(node.findall('ingredient'))>9:
            errors.append('Recipe exceeds ingredient grid: '+node.get('name'))
        for ingredient in node.findall('ingredient'):
            if ingredient.get('name') not in known:
                errors.append('Unknown ingredient '+ingredient.get('name'))
            if int(ingredient.get('count'))<=0:
                errors.append('Invalid ingredient count '+node.get('name'))
        output = item_nodes.get(node.get('name'))
        if output is not None and output.find("property[@name='RebirthMetabolismType']") is not None:
            diet = output.find("property[@name='RebirthDietTags']")
            tags = set(diet.get('value', '').split(',')) if diet is not None else set()
            for ingredient in node.findall('ingredient'):
                required = set(DIET_TAGS.get(ingredient.get('name'), [])) & {
                    'Meat', 'Fish', 'Egg', 'Dairy', 'Honey', 'AnimalFat', 'AnimalProduct'}
                if required - tags:
                    errors.append('Missing animal-ingredient diet tags '+node.get('name')+': '+str(sorted(required-tags)))
        tool = node.get('craft_tool')
        if tool and tool not in items:
            errors.append('Unknown cooking tool '+tool)
        area = node.get('craft_area')
        if area and area not in blocks and area not in {'campfire','workbench'}:
            errors.append('Unknown crafting area '+area)
    for node in definitions.iter('item'):
        icon = node.find("property[@name='CustomIcon']")
        if icon is not None and not (ROOT/'UIAtlases/ItemIconAtlas'/(icon.get('value')+'.png')).is_file():
            errors.append('Missing icon '+icon.get('value'))
    loot = ET.parse(directory/'loot.xml').getroot()
    native_loot = ET.parse(native/'loot.xml').getroot()
    groups = {n.get('name') for n in native_loot.iter('lootgroup')}
    groups.update(n.get('name') for n in loot.iter('lootgroup'))
    for node in loot.iter('item'):
        if node.get('name') and node.get('name') not in known:
            errors.append('Unknown loot item '+node.get('name'))
        if node.get('group') and node.get('group') not in groups:
            errors.append('Unknown loot group '+node.get('group'))
    for node in loot.findall('append'):
        if native_loot.find(node.get('xpath').replace('/lootcontainers/','',1)) is None:
            errors.append('Missing native loot target '+node.get('xpath'))
    for node in ET.parse(directory/'blocks.xml').getroot().iter('property'):
        value = node.get('value','')
        if value.startswith('#@modfolder:'):
            resource = value.split(':',1)[1].split('?',1)[0]
            if not (ROOT/resource).is_file():
                errors.append('Missing station bundle '+resource)
    windows = set()
    for path in (native/'XUi_InGame/windows.xml', ROOT/'Config/XUi_InGame/windows.xml', directory/'windows.xml'):
        windows.update(n.get('name') for n in ET.parse(path).iter('window') if n.get('name'))
    groups = ET.parse(directory/'xui.xml').getroot()
    for group in groups.iter('window_group'):
        for window in group.findall('window'):
            if window.get('name') not in windows:
                errors.append('Unknown workstation window '+window.get('name'))
    progression = ET.parse(ROOT/'Config/_Survivor/crafting_progression.xml').getroot()
    errors.extend(validate_policy_implementations(progression))
    registered = {n.get('id'): n for n in progression.find('recipes')}
    for node in recipes.iter('recipe'):
        record = registered.get(node.get('name'))
        if record is None or record.get('policy')!='universal':
            errors.append('Missing unrestricted crafting registration '+node.get('name'))
    localization = {r[0] for r in csv.reader((ROOT/'Config/Localization.csv').open(encoding='utf-8-sig')) if r}
    for name in names:
        if name not in localization:
            errors.append('Missing item name '+name)
    literature = ET.parse(ROOT/'Config/_Survivor/literature.xml').getroot()
    markers = {n.get('id') for n in ET.parse(ROOT/'Config/_Survivor/progression.xml').getroot().find('knowledge')}
    studied = {n.get('id'):n for n in literature}
    for task in ASSETS.values():
        if task['status'] not in ('generated', 'approved_reference', 'reused_legacy'):
            errors.append('Unfinished artwork '+task['key'])
        if task['category'] in ('card','book','magazine'):
            row = studied.get(task['icon'])
            if row is None or row.get('kind')!='discovery' or row.get('knowledge') not in markers:
                errors.append('Invalid study registration '+task['icon'])
    report = {'items':len(names), 'recipes':len(list(recipes.iter('recipe'))), 'errors':errors,
              'in_game_validation':'Not performed; imported 2.6 station bundles need a 3.2 load check.'}
    (PRODUCTION/'xml_validation.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)

if __name__=='__main__':
    validate()
