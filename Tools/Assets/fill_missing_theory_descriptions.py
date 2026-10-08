"""Fill absent theory-book descriptions from the authoritative literature catalogue.

Preserves existing localization lines byte-for-byte; never rewrites unrelated CSV rows.
Run the screen/color inventory first. This is one bounded part of the description audit.
"""
from pathlib import Path
import csv
import io
import json
from lxml import etree as ET

ROOT = Path(__file__).resolve().parents[2]
path = ROOT / 'Config/Localization.csv'
raw = path.read_bytes()
text = raw.decode('utf-8-sig')
loc = {row[0]: row[1] for row in csv.reader(io.StringIO(text)) if len(row) >= 2}
skills = ET.parse(str(ROOT / 'Config/_Survivor/progression.xml'))
names = {s.get('id'): loc.get(s.get('name_key')) for s in skills.findall('skills/skill')}
catalogue = ET.parse(str(ROOT / 'Config/_Survivor/literature.xml'))
additions = []
for entry in catalogue.findall('item'):
    if entry.get('kind') != 'theory':
        continue
    key = entry.get('id') + 'Desc'
    if key in loc:
        continue
    name = names[entry.get('skill')]
    assert name, entry.get('id')
    amount = entry.get('amount')
    assert float(amount) > 0
    description = (
        f'[B58CFF]Study to improve {name} Theory.[-]\\n'
        f'Base gain: {amount} Theory once per character. Learning bonuses and your Theory limit affect the gain. Practice to improve the skill itself.\\n'
        '[B58CFF]Reusable:[-] keep it or share it. Reading the same title again gives no extra Theory.'
    )
    additions.append((key, description))

if additions:
    buf = io.StringIO(newline='')
    writer = csv.writer(buf, lineterminator='\r\n')
    writer.writerows(additions)
    with path.open('ab') as stream:
        if not raw.endswith(b'\n'):
            stream.write(b'\r\n')
        stream.write(buf.getvalue().encode('utf-8'))

out = ROOT / '_Documentation/Description_Color_Audit/theory_description_additions.json'
if additions:
    out.write_text(json.dumps(additions, indent=2), encoding='utf-8')
print(json.dumps({'added': len(additions), 'existing_rows_preserved': True}))
