"""Apply reviewed missing descriptions; derive vehicle part text from actual VehicleSlot properties."""
from pathlib import Path
import csv, io, json
from lxml import etree as ET
ROOT = Path(__file__).resolve().parents[2]
path = ROOT / 'Config/Localization.csv'
raw = path.read_bytes()
loc = {r[0]: r[1] for r in csv.reader(io.StringIO(raw.decode('utf-8-sig'))) if len(r) >= 2}
descriptions = json.loads((Path(__file__).parent / 'verified_item_descriptions.json').read_text(encoding='utf-8'))
for item in ET.parse(str(ROOT / 'Config/_Vehicles/items.xml')).findall('.//item'):
    if item.find("property[@name='VehicleSlot']") is None:
        continue
    descriptions[item.get('name') + 'Desc'] = ('[B58CFF]Vehicle restoration part.[-] '
        'Install in a matching slot on a repairable vehicle. Check the vehicle\'s parts list to see which part it needs.')
additions = [(k, v) for k, v in descriptions.items() if k not in loc]
if additions:
    buf = io.StringIO(newline='')
    csv.writer(buf, lineterminator='\r\n').writerows(additions)
    with path.open('ab') as f:
        if not raw.endswith(b'\n'): f.write(b'\r\n')
        f.write(buf.getvalue().encode('utf-8'))
    (ROOT / '_Documentation/Description_Color_Audit/verified_item_description_additions.json').write_text(
        json.dumps(additions, indent=2), encoding='utf-8')
(ROOT / '_Documentation/Description_Color_Audit/verified_item_description_catalogue.json').write_text(
    json.dumps(descriptions, indent=2), encoding='utf-8')
print(json.dumps({'added': len(additions)}))
