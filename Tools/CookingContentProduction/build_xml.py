"""Build the approved cooking content after all artwork has been completed."""
import csv
import io
import json
import xml.etree.ElementTree as ET
from content_model import ROOT, CAT, PRODUCTION, CATALOGUE, FOODS, ASSETS, recipe, station, food_stats

DEST = ROOT / 'Config/_Cooking'

def prop(parent, name, value):
    return ET.SubElement(parent, 'property', name=name, value=str(value))

def write_xml(path, root):
    ET.indent(root, space='  ')
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(ET.tostring(root, encoding='unicode')+'\n', encoding='utf-8')

def patch():
    root = ET.Element('configs')
    return root, ET.SubElement(root, 'append', xpath='/items')

def food_item(parent, key):
    r, s = recipe(key), food_stats(key)
    item = ET.SubElement(parent, 'item', name=r['game_id'])
    drink = key in ('N37', 'N38', 'L11')
    values = {
        'Tags': 'drink,rebirthSurvivorFood' if drink else 'food,foodSkill,rebirthSurvivorFood',
        'HoldType': 3 if drink else 31, 'DisplayType': 'foodWater',
        'Meshfile': ('@:Other/Items/Food/masonJarRedTeaPrefab.prefab' if drink else
                     '@:Other/Items/Food/parcelGenericPrefab.prefab' if 'oven' in (r['station'] or '').lower() or key.startswith('P') else
                     '@:Other/Items/Food/foodPotPrefab.prefab'),
        'DropMeshfile': '@:Other/Items/Misc/sack_droppedPrefab.prefab',
        'Material': 'Morganic', 'Stacknumber': 1 if drink else 100,
        'EconomicValue': max(10, round(float(s['nutrition'])*3)),
        'CustomIcon': ASSETS[key]['icon'], 'Group': 'Food/Cooking',
        'DescriptionKey': r['game_id']+'Desc',
        'CreativeMode': 'Player',
        'RebirthMetabolismType': 'Drink' if drink else 'Food',
        'RebirthNutritionUnits': s['nutrition'],
        'RebirthFoodWaterMl': s['water'], 'RebirthStomachVolumeMl': 300,
        'RebirthDigestionProfile': 'normal', 'RebirthFoodSafetyProfile': 'safe',
        'RebirthMoodFoodProfile': 'simple', 'RebirthMoodInfluence': s['comfort'],
        'RebirthDietTags': ','.join(s['tags']), 'RebirthMoodVarietyFamily': s['family'],
    }
    if drink:
        volume = max(1, float(s['water']))
        values.update(RebirthLiquidProfile='tea', RebirthContainerCapacityMl=volume,
            RebirthInitialVolumeMl=volume, RebirthManualSipMl=min(125,volume), RebirthAutoSipMl=50,
            RebirthHydrationEquippable='true', RebirthAutoSipSafe='true',
            RebirthLiquidHydrationYield=1, RebirthFoodWaterMl=0,
            RebirthReusableContainer='false', RebirthEmptyItem='drinkJarEmpty')
    for name, value in values.items():
        prop(item, name, value)
    action = ET.SubElement(item, 'property', {'class':'Action0'})
    prop(action, 'Class', 'ItemActionConsumeMetabolismRebirth, RebirthUtils')
    prop(action, 'Delay', 1)
    prop(action, 'Sound_start', 'player_drinking' if drink else 'player_eating')
    if drink:
        group = ET.SubElement(item, 'effect_group', name='RebirthLiquidVolume')
        ET.SubElement(group, 'passive_effect', name='DegradationMax', operation='base_set', value=str(volume))
    return item

def literature_item(parent, task):
    item = ET.SubElement(parent, 'item', name=task['icon'])
    for name, value in {'Extends':'resourcePaper', 'Tags':'book,rebirthLiterature',
                        'Stacknumber':1, 'EconomicValue':120, 'EconomicBundleSize':1,
                        'Group':'Books', 'CustomIcon':task['icon'],
                        'DescriptionKey':task['icon']+'Desc'}.items():
        prop(item, name, value)
    action = ET.SubElement(item, 'property', {'class':'Action0'})
    prop(action, 'Class', 'ItemActionStudyLiteratureRebirth, RebirthUtils')
    prop(action, 'Delay', 0.1)
    prop(action, 'UseAnimation', 'false')
    prop(action, 'Consume', 'false')
    category = task['category']
    if category in ('book','magazine'):
        prop(item, 'HoldType', 21)
        prop(item, 'Meshfile', '@:Other/Items/Misc/bookPrefab.prefab')
    table = CATALOGUE[{'card':'cards','book':'books','magazine':'magazines'}[category]]
    record = next(row for row in table if row['id']==task['key'])
    prop(item, 'RebirthCookingLiteratureKind', category)
    covers = [record['recipe']] if category=='card' else record['covers']
    prop(item, 'RebirthCookingCoveredRecipes', ','.join(recipe(key)['game_id'] for key in covers))
    if category!='card':
        prop(item, 'RebirthCookingPreparationEffect', record['effect'])
    return item

def register_study():
    literature_path = ROOT/'Config/_Survivor/literature.xml'
    progression_path = ROOT/'Config/_Survivor/progression.xml'
    literature = ET.parse(literature_path).getroot()
    progression = ET.parse(progression_path).getroot()
    knowledge = progression.find('knowledge')
    existing_items = {n.get('id') for n in literature.findall('item')}
    existing_knowledge = {n.get('id') for n in knowledge}
    for task in ASSETS.values():
        category = task['category']
        if category not in ('card','book','magazine'):
            continue
        ident = task['icon']
        marker = 'literature.read.'+ident
        if ident not in existing_items:
            ET.SubElement(literature, 'item', id=ident, kind='discovery', knowledge=marker,
                study_seconds=str({'card':20,'book':120,'magazine':45}[category]))
        if marker not in existing_knowledge:
            ET.SubElement(knowledge, 'knowledge', id=marker, name_key=ident,
                associated_skills='skill.cooking')
    write_xml(literature_path, literature)
    write_xml(progression_path, progression)
    distribution_path = ROOT/'Config/_Survivor/literature_distribution.xml'
    distribution = ET.parse(distribution_path).getroot()
    for category in ('card','book','magazine'):
        tier_id = 'cooking.'+category
        old = distribution.find("tier[@id='"+tier_id+"']")
        if old is not None:
            distribution.remove(old)
        titles = [t for t in ASSETS.values() if t['category']==category]
        tier = ET.SubElement(distribution, 'tier', id=tier_id, relative_weight='1.00',
                             count=str(len(titles)), loot_entry_group='rebirthCooking'+category.title()+'Pool')
        for task in titles:
            ET.SubElement(tier, 'item', id=task['icon'])
    write_xml(distribution_path, distribution)

def register_crafting(keys):
    path = ROOT/'Config/_Survivor/crafting_progression.xml'
    root = ET.parse(path).getroot()
    rows = root.find('recipes')
    existing = {n.get('id') for n in rows}
    for key in keys:
        ident = recipe(key)['game_id']
        if ident not in existing:
            ET.SubElement(rows, 'recipe', id=ident, policy='universal',
                source='cooking_catalogue_20260915', source_status='authored',
                implementation='policy_declared', family=food_stats(key)['family'],
                decision_status='approved', primary_skill='skill.cooking')
    root.set('manifest_recipe_count', str(len(rows)))
    write_xml(path, root)

def compact_cooking_reference(record):
    focus = record['focus'].rstrip('.')
    for herb in ('basil', 'dill', 'sage'):
        if focus.lower() == 'permanent ' + herb + ' pairing guidance plus covered practice':
            focus = 'seasoning with ' + herb
    covered = record['covers']
    examples = ', '.join(recipe(k)['title'] for k in covered[:2])
    if len(covered) > 2:
        examples += ' and ' + str(len(covered) - 2) + ' other recipes'
    return ('[B58CFF]Study once; keep or share afterward.[-]\\n'
            + 'Topic: ' + focus + '.\\nExamples: ' + examples + '.\\n'
            + 'Cooking skill grows through practice.')

def cooking_food_description(key):
    components = {
        'P01': 'Wheat flour processed at the mortar and pestle. Use it as an ingredient in dough and other recipes.',
        'P02': 'Oats processed at the mortar and pestle. Use them as an ingredient in porridge and other recipes.',
        'P03': 'A sweet cane syrup used as an ingredient in other recipes.',
        'P04': 'A cranberry sauce used as an ingredient in other recipes.'
    }
    if key in components:
        return components[key]
    return ('A serving of ' + recipe(key)['title']
            + '. Nutrition and comfort reflect this dish; personal diet and recent meal variety affect enjoyment.')

def localization(keys):
    path = ROOT/'Config/Localization.csv'
    text = path.read_text(encoding='utf-8-sig')
    rows = list(csv.reader(io.StringIO(text)))
    existing = {row[0] for row in rows if row}
    labels = {row[0]:row[1] for row in rows if len(row)>1}
    native_rows = list(csv.DictReader((ROOT.parent.parent/'Data/Config/Localization.csv').open(encoding='utf-8-sig')))
    for row in native_rows:
        labels.setdefault(row.get('Key',''),row.get('english',''))
    labels.update({recipe(k)['game_id']:recipe(k)['title'] for k in CATALOGUE['recipes']})
    additions = []
    for key in keys:
        r = recipe(key)
        additions += [(r['game_id'],r['title']), (r['game_id']+'Desc',
            cooking_food_description(key))]
    for task in ASSETS.values():
        category = task['category']
        if category not in ('card','book','magazine'):
            continue
        description = {'card':'Reusable recipe reference. Study to remember this title. You can share the card afterward; reading does not raise Cooking skill.',
            'book':'Reusable cooking reference. Study to remember this title; the book remains available to keep or share. Cooking skill improves through practice.',
            'magazine':'Reusable cooking technique reference. Study to remember this issue; the magazine remains available to keep or share. Cooking skill improves through practice.'}[category]
        table = CATALOGUE[{'card':'cards','book':'books','magazine':'magazines'}[category]]
        record = next(row for row in table if row['id']==task['key'])
        if category=='card':
            ingredients = recipe(record['recipe'])['ingredients']
            description += ' Ingredients: '+', '.join(str(i['count'])+' '+labels.get(i['id'],i['id']) for i in ingredients)+'.'
        else:
            description = compact_cooking_reference(record)
        additions += [(task['icon'],task['title']), (task['icon']+'Desc',description)]
    output = io.StringIO(newline='')
    writer = csv.writer(output, lineterminator='\n')
    for row in additions:
        if row[0] not in existing:
            writer.writerow(row)
            existing.add(row[0])
    path.write_text(text.rstrip('\r\n')+'\n'+output.getvalue(), encoding='utf-8')

def build_content():
    import stations
    pending = [t['key'] for t in ASSETS.values()
               if t['status'] not in ('generated', 'approved_reference', 'reused_legacy')]
    if pending:
        raise SystemExit('Artwork must be completed first: '+str(len(pending))+' remaining')
    items, body = patch()
    recipes = ET.Element('configs')
    recipe_body = ET.SubElement(recipes, 'append', xpath='/recipes')
    keys = list(FOODS)+['P01','P02','P03','P04']+[k for k in CATALOGUE['recipes'] if k.startswith('L')]
    for key in keys:
        r = recipe(key)
        food_item(body, key)
        area, tool = station(key)
        attributes = dict(name=r['game_id'], count=str(r['yield_count']), craft_time=str(food_stats(key)['seconds']))
        if area:
            attributes['craft_area'] = area
        if tool:
            attributes['craft_tool'] = tool
        node = ET.SubElement(recipe_body, 'recipe', attributes)
        for ingredient in r['ingredients']:
            ET.SubElement(node, 'ingredient', name=ingredient['id'], count=str(ingredient['count']))
    for task in ASSETS.values():
        if task['category'] in ('card','book','magazine'):
            literature_item(body, task)
    for ident, icon in [('FuriousRamsayBakingPan','FR_BakingPan_icon'),('rebirthCookingFryingPan','rebirthCookingFryingPan')]:
        item = ET.SubElement(body, 'item', name=ident)
        for name, value in {'CustomIcon':icon, 'HoldType':45, 'CreativeMode':'Player',
                            'Meshfile':'@:Other/Items/Misc/sackPrefab.prefab',
                            'DropMeshfile':'@:Other/Items/Misc/sack_droppedPrefab.prefab',
                            'Material':'Miron', 'Stacknumber':1, 'Tags':ident,
                            'Group':'Tools/Traps,Food/Cooking',
                            'DescriptionKey':ident+'Desc', 'EconomicValue':200}.items():
            prop(item, name, value)
        node = ET.SubElement(recipe_body, 'recipe', name=ident, count='1', craft_time='60', craft_area='workbench')
        ET.SubElement(node, 'ingredient', name='resourceForgedIron', count='10')
    for ident, (title, window, costs) in stations.STATIONS.items():
        attrs = dict(name=ident, count='1', craft_time='120')
        if window:
            attrs['craft_area']='workbench'
        node = ET.SubElement(recipe_body, 'recipe', attrs)
        for name, count in costs.items():
            ET.SubElement(node, 'ingredient', name=name, count=str(count))
    for key in ('skillet','pickles','dried','potpie'):
        task = ASSETS.get('existing-'+key)
        if task:
            node = ET.SubElement(items, 'set', xpath="/items/item[@name='"+recipe(key)['game_id']+"']/property[@name='CustomIcon']/@value")
            node.text = task['icon']
    write_xml(DEST/'items.xml', items)
    write_xml(DEST/'recipes.xml', recipes)

def include(path, filename, conditional=False):
    text = path.read_text(encoding='utf-8-sig')
    if 'filename="'+filename+'"' in text:
        return
    line = '  <include filename="'+filename+'"/>\n'
    if conditional:
        line = '  <conditional><if cond="character_progression(\'Rebirth\')">\n'+line+'  </if></conditional>\n'
    text = text.replace('</configs>', line+'</configs>')
    path.write_text(text, encoding='utf-8')

def finish_integration():
    import stations
    import loot
    blocks, windows, xui = stations.build()
    write_xml(DEST/'blocks.xml', blocks)
    write_xml(DEST/'windows.xml', windows)
    write_xml(DEST/'xui.xml', xui)
    write_xml(DEST/'loot.xml', loot.build())
    for name in ('items','recipes','loot'):
        include(ROOT/('Config/_Rebirth/'+name+'.xml'), '../_Cooking/'+name+'.xml')
    include(ROOT/'Config/blocks.xml', '_Cooking/blocks.xml', conditional=True)
    include(ROOT/'Config/XUi_InGame/windows.xml', '../_Cooking/windows.xml', conditional=True)
    include(ROOT/'Config/XUi_InGame/xui.xml', '../_Cooking/xui.xml', conditional=True)
    include(ROOT/'Config/XUi_InGame/windows.xml', '../_Cooking/workspace_windows.xml', conditional=True)
    include(ROOT/'Config/XUi_InGame/xui.xml', '../_Cooking/workspace_xui.xml', conditional=True)
    keys = list(FOODS)+['P01','P02','P03','P04']+[k for k in CATALOGUE['recipes'] if k.startswith('L')]
    register_study()
    register_crafting(keys)
    localization(keys)
    path = ROOT/'Config/Localization.csv'
    rows = [(ident,title) for ident,(title,_,_) in stations.STATIONS.items()]
    rows += [('FuriousRamsayBakingPan','Baking Pan'),('rebirthCookingFryingPan','Frying Pan'),
             ('FuriousRamsayBakingPanDesc','An oven tool for baking and roasting.'),
             ('rebirthCookingFryingPanDesc','A stovetop tool for frying.')]
    existing = {r[0] for r in csv.reader(io.StringIO(path.read_text())) if r}
    with path.open('a',newline='',encoding='utf-8') as output:
        writer = csv.writer(output,lineterminator='\n')
        writer.writerows(row for row in rows if row[0] not in existing)
    manifest_path = ROOT/'Config/_Survivor/crafting_progression.xml'
    manifest = ET.parse(manifest_path).getroot()
    recipes = manifest.find('recipes')
    existing = {r.get('id') for r in recipes}
    for ident in list(stations.STATIONS)+['FuriousRamsayBakingPan','rebirthCookingFryingPan']:
        if ident not in existing:
            ET.SubElement(recipes,'recipe',id=ident,policy='universal',source='cooking_catalogue_20260915',
                source_status='authored',implementation='policy_declared',family='construction.workstation',
                decision_status='approved',primary_skill='skill.construction')
    manifest.set('manifest_recipe_count',str(len(recipes)))
    write_xml(manifest_path,manifest)

if __name__ == '__main__':
    build_content()
    finish_integration()
