"""Add missing raw-crop and player-grown harvest descriptions, using actual declarations."""
from pathlib import Path
import csv, io, json
from lxml import etree as ET
ROOT = Path(__file__).resolve().parents[2]
path = ROOT / 'Config/Localization.csv'
raw = path.read_bytes()
loc = {r[0]: r[1] for r in csv.reader(io.StringIO(raw.decode('utf-8-sig'))) if len(r) >= 2}
descriptions = {}
for source in (ROOT / 'Config/_Farming/_Crops/Items').glob('*.xml'):
    for item in ET.parse(str(source)).findall('.//item'):
        name = item.get('name', '')
        if not name.startswith('foodCrop') or item.find("property[@class='Action0']/property[@name='Class'][@value='Eat']") is None:
            continue
        descriptions[name + 'Desc'] = ('[B58CFF]Food ingredient.[-] Eat it or use it in a recipe that calls for it. '
            'In REBIRTH progression, food must digest before it replenishes Nutrition. Cooking can make better use of your harvest.')
for source in (ROOT / 'Config/_Farming/_Crops/Blocks').glob('*.xml'):
    for block in ET.parse(str(source)).findall('.//block'):
        name = block.get('name', '')
        parent = block.find("property[@name='Extends']")
        if not name.endswith('3HarvestPlayer') or parent is None:
            continue
        parent_key = parent.get('value') + 'Desc'
        if parent_key not in loc:
            continue
        descriptions[name + 'Desc'] = ('[B58CFF]Ready to harvest.[-] A mature crop grown by a player. '
            'Harvest it to collect the crop.')
additions = [(k, v) for k, v in descriptions.items() if k not in loc]
if additions:
    buf = io.StringIO(newline='')
    csv.writer(buf, lineterminator='\r\n').writerows(additions)
    with path.open('ab') as f:
        if not raw.endswith(b'\n'): f.write(b'\r\n')
        f.write(buf.getvalue().encode('utf-8'))
    (ROOT / '_Documentation/Description_Color_Audit/crop_description_additions.json').write_text(
        json.dumps(additions, indent=2), encoding='utf-8')
print(json.dumps({'added': len(additions)}))
