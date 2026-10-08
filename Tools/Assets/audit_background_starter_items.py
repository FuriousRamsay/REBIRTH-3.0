"""Read-only validation of every authored background kit against reachable definitions."""
from pathlib import Path
from collections import Counter
import csv, json
from lxml import etree as ET
from audit_restore_legacy_icons import source_definitions

ROOT = Path(__file__).resolve().parents[2]
records, _ = source_definitions(ROOT)
known = {name for kind, name in records if kind in ('item', 'block', 'item_modifier')}
for filename, tag in [('items', 'item'), ('blocks', 'block'), ('item_modifiers', 'item_modifier')]:
    doc = ET.parse(str(ROOT.parent.parent / 'Data/Config' / (filename + '.xml')))
    known.update(n.get('name') for n in doc.findall(tag))
rows, errors = [], []
with (ROOT / 'Config/Localization.csv').open(encoding='utf-8-sig', newline='') as source:
    labels = dict(row for row in csv.reader(source) if len(row) == 2)
for background in ET.parse(str(ROOT / 'Config/_Survivor/backgrounds.xml')).findall('background'):
    kit = []
    for item in background.findall('starting_items/item'):
        entry = dict(item.attrib)
        entry['resolved'] = item.get('id') in known
        kit.append(entry)
        if not entry['resolved']:
            errors.append(background.get('id') + ': unresolved ' + item.get('id'))
        label_key = item.get('name_key', '')
        entry['displayName'] = labels.get(label_key, '')
        if not entry['displayName'].strip():
            errors.append(background.get('id') + ': missing starter label ' + label_key)
        for attribute in ('count', 'quality'):
            raw = item.get(attribute)
            if raw is None:
                continue
            try:
                valid = int(raw) > 0
            except ValueError:
                valid = False
            if not valid:
                errors.append(background.get('id') + ': invalid ' + attribute + ' for ' + item.get('id', '<missing>'))
    for item, count in Counter(k['id'] for k in kit).items():
        if count > 1:
            errors.append(background.get('id') + ': repeated item ' + item)
    rows.append({'background': background.get('id'), 'items': kit})
result = {'backgrounds': len(rows), 'entries': sum(len(r['items']) for r in rows),
          'errors': errors, 'kits': rows,
          'scope': 'Source definitions in both conditional branches; runtime availability still requires validation.'}
out = ROOT / '_Documentation/Survivor/STARTER_ITEM_DEFINITION_AUDIT.json'
out.write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k != 'kits'}, indent=2))
raise SystemExit(bool(errors))
