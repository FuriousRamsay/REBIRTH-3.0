"""Check the approved catalogue's dependencies before generating game XML."""
from pathlib import Path
from collections import Counter
import json
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
CAT = ROOT / '_Documentation/FeatureDesign/SurvivorSkillSystem/CookingCatalogue_20260915'
catalogue = json.loads((CAT / 'COOKING_LITERATURE_CATALOGUE.json').read_text())
foods = json.loads((CAT / 'NEW_CROP_RECIPE_PROPOSAL.json').read_text())
known = set()
for path in [ROOT.parent.parent / 'Data/Config/items.xml', *ROOT.glob('Config/**/*.xml')]:
    try:
        tree = ET.parse(path)
    except ET.ParseError:
        continue
    for node in tree.iter('item'):
        if node.get('name') and node.find('property') is not None:
            known.add(node.get('name'))
planned = {r['game_id'] for r in catalogue['recipes'].values() if r.get('game_id')}
missing = {}
for key, recipe in catalogue['recipes'].items():
    bad = sorted({i['id'] for i in recipe['ingredients']} - known - planned)
    if bad:
        missing[key] = bad
report = {
    'recipe_identities': len(catalogue['recipes']),
    'new_foods': len(foods),
    'cards': len(catalogue['cards']),
    'books': len(catalogue['books']),
    'magazines': len(catalogue['magazines']),
    'unknown_ingredient_ids': missing,
    'over_nine_ingredient_types': [r['code'] for r in foods if len({i['id'] for i in r['ingredients']}) > 9],
    'new_food_station_requirements': dict(Counter(r['station'] for r in foods)),
    'catalogue_holds_to_resolve': {k: r['status'] for k, r in catalogue['recipes'].items() if 'HOLD' in r.get('status', '')},
}
(CAT / 'Production/content_preflight.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
