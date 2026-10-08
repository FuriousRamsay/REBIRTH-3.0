"""Add missing discovery-book text from configured knowledge; preserve existing rows."""
from pathlib import Path
import csv, io, json
from lxml import etree as ET

ROOT = Path(__file__).resolve().parents[2]
path = ROOT / 'Config/Localization.csv'
raw = path.read_bytes()
loc = {r[0]: r[1] for r in csv.reader(io.StringIO(raw.decode('utf-8-sig'))) if len(r) >= 2}
knowledge = {n.get('id'): n.get('name_key') for n in ET.parse(str(ROOT / 'Config/_Survivor/progression.xml')).findall('.//knowledge')}
additions, unresolved = [], []
for item in ET.parse(str(ROOT / 'Config/_Survivor/literature.xml')).findall('item'):
    key = item.get('id') + 'Desc'
    if item.get('kind') != 'discovery' or key in loc:
        continue
    topic = loc.get(knowledge.get(item.get('knowledge')))
    if not topic:
        unresolved.append({'item': item.get('id'), 'knowledge': item.get('knowledge')})
        continue
    description = (f'[B58CFF]Study to learn: {topic}.[-]\\n'
        'Your character remembers this knowledge. Open Character > Progression to check its uses and any further requirements.\\n'
        '[B58CFF]Reusable:[-] keep it or share it. Reading it again gives no extra progress.')
    additions.append((key, description))
if additions:
    buf = io.StringIO(newline='')
    csv.writer(buf, lineterminator='\r\n').writerows(additions)
    with path.open('ab') as f:
        if not raw.endswith(b'\n'): f.write(b'\r\n')
        f.write(buf.getvalue().encode('utf-8'))
    (ROOT / '_Documentation/Description_Color_Audit/discovery_description_additions.json').write_text(
        json.dumps(additions, indent=2), encoding='utf-8')
print(json.dumps({'added': len(additions), 'unresolved': unresolved}, indent=2))
