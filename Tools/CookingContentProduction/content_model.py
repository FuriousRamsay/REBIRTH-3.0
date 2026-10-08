"""Resolve catalogue identities and existing stat inheritance for XML production."""
from pathlib import Path
import json
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
CAT = ROOT / '_Documentation/FeatureDesign/SurvivorSkillSystem/CookingCatalogue_20260915'
PRODUCTION = CAT / 'Production'
CATALOGUE = json.loads((CAT / 'COOKING_LITERATURE_CATALOGUE.json').read_text())
FOODS = {r['code']: r for r in json.loads((CAT / 'NEW_CROP_RECIPE_PROPOSAL.json').read_text())}
ASSETS = {t['key']: t for t in json.loads((PRODUCTION / 'asset_manifest.json').read_text())['tasks']}
DECISIONS = json.loads((PRODUCTION / 'integration_decisions.json').read_text())
ANCHORS = json.loads((PRODUCTION / 'source_audit.json').read_text())['metabolism_anchors']
IDS = {k: ('rebirthCookingFood'+k if k.startswith(('N', 'P')) else r['game_id'])
       for k, r in CATALOGUE['recipes'].items()}
COMPONENT_IDS = {CATALOGUE['recipes'][k]['game_id']: IDS[k] for k in ['P01','P02','P03','P04']}
DIET_TAGS = {}
for node in ET.parse(ROOT / 'Config/_Survivor/food_content.xml').getroot().findall('append'):
    match = re.fullmatch(r"/items/item\[@name='([^']+)'\]", node.get('xpath', ''))
    tags = node.find("property[@name='RebirthDietTags']")
    if match and tags is not None:
        DIET_TAGS[match.group(1)] = tags.get('value').split(',')

def recipe(key):
    row = dict(CATALOGUE['recipes'][key])
    row.update(DECISIONS['legacy_recipe_overrides'].get(key, {}))
    row['ingredients'] = [dict(id=COMPONENT_IDS.get(i['id'], i['id']), count=int(i['count'])) for i in row['ingredients']]
    row['yield_count'] = int(row['yield_count'])
    row['game_id'] = IDS[key]
    return row

def station(key):
    description = (recipe(key)['station'] or 'cold').lower()
    if key in ('P01','P02'):
        return 'WorkbenchMortarPestle001_FR', None
    if 'cold' in description or 'no heat' in description:
        return None, None
    if 'oven' in description:
        return 'WorkbenchIronOven001_FR', 'FuriousRamsayBakingPan'
    if 'frying pan' in description:
        return 'WorkbenchGasStove001_FR', 'rebirthCookingFryingPan'
    if description.startswith('stovetop'):
        return 'WorkbenchGasStove001_FR', 'toolCookingPot'
    return 'campfire', 'toolCookingPot'

def food_stats(key):
    if key in FOODS:
        r = FOODS[key]
        return dict(nutrition=r['target_nutrition'], water=r['water_ml'], comfort=r['base_comfort'],
                    tags=r['diet_tags'], family=r['variety_family'], seconds=r['time_seconds'])
    r = recipe(key)
    override = DECISIONS['legacy_recipe_overrides'].get(key)
    if override:
        return dict(nutrition=override['nutrition_per_portion'], water=override['water_ml_per_portion'],
                    comfort=override['base_comfort'], tags=override['diet_tags'], family='survivor_stew', seconds=90)
    total = 0
    for ingredient in r['ingredients']:
        name = ingredient['id']
        if name in IDS.values() and name.startswith('rebirthCookingFoodP'):
            value = food_stats(name[-3:])['nutrition']
        else:
            values = ANCHORS.get(name)
            if values is None and name not in {'resourceCropGoldenrodPlant','resourceCropChrysanthemumPlant','resourceYuccaFibers'}:
                raise ValueError('Missing nutrition source for '+key+': '+name)
            value = float((values or {}).get('RebirthNutritionUnits', 0))
        total += ingredient['count'] * value
    component = key.startswith('P')
    nutrition = round(total / r['yield_count'], 1)
    if not component:
        nutrition = min(50, round(nutrition * 1.1, 1))
    tags = set()
    for ingredient in r['ingredients']:
        name = ingredient['id']
        if name in DIET_TAGS:
            tags.update(DIET_TAGS[name])
        elif name == 'drinkJarBoiledWater':
            continue
        elif any(s in name for s in ('Salmon','Tuna')):
            tags.add('Fish')
        elif any(s in name for s in ('Beef','Chicken','Lamb','Chili','Catfood','Dogfood','RawMeat','Soup','Pasta','Ravioli','Stock')):
            tags.add('Meat')
        else:
            tags.add('Plant')
    return dict(nutrition=nutrition, water=0 if component else 250, comfort=0 if component else 3,
                tags=sorted(tags), family=('cooking_component' if component else 'woodland_smoothie' if key=='L11' else 'survivor_stew'), seconds=60)

if __name__ == '__main__':
    rows = {}
    for key in list(FOODS)+['P01','P02','P03','P04']+[k for k in CATALOGUE['recipes'] if k.startswith('L')]:
        rows[key] = dict(recipe=recipe(key), stats=food_stats(key), station=station(key), icon=ASSETS[key]['icon'])
    (PRODUCTION / 'resolved_content_model.json').write_text(json.dumps(rows, indent=2)+'\n')
    print(json.dumps({'resolved_items': len(rows), 'new_recipe_ids': len(set(r['recipe']['game_id'] for r in rows.values()))}))
