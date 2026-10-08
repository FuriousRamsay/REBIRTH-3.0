"""Read-only source inventory: UI colors first, then custom description coverage.
Includes both option branches; not a runtime XML patch interpreter.
"""
from pathlib import Path
from collections import Counter, defaultdict
import argparse, csv, json, re, sys
from lxml import etree as ET
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / '_Documentation/Description_Color_Audit'
args = argparse.ArgumentParser()
args.add_argument('--phase', choices=('initial', 'current'), default='current')
phase = args.parse_args().phase
if phase == 'current': OUT = OUT / 'current'
OUT.mkdir(parents=True, exist_ok=True)
sys.path.insert(0, str(Path(__file__).resolve().parent))
from audit_restore_legacy_icons import source_definitions

def write(name, rows):
    (OUT / name).write_text(json.dumps(rows, indent=2, ensure_ascii=False), encoding='utf-8')

colors = []
for path in sorted((ROOT / 'Config').rglob('*.xml')):
    doc = ET.parse(str(path))
    # Feature-owned workstation/metabolism/vehicle layouts need not live under XUi.
    # Include actual UI nodes, as well as XPath-only patches in conventional UI folders.
    if not any('xui' in part.lower() for part in path.parts) and not doc.xpath(
            '//window | //window_group | //rect | //label | //sprite | //button | //textfield'):
        continue
    for node in doc.getroot().iter():
        if not isinstance(node.tag, str): continue
        owner = next((p.get('name') for p in [node, *node.iterancestors()]
                      if p.tag in ('window', 'window_group') and p.get('name')), '(shared/patch)')
        for attr, value in node.attrib.items():
            if 'color' in attr.lower():
                colors.append({'file': str(path.relative_to(ROOT)), 'screen': owner,
                    'line': node.sourceline, 'node': node.get('name', node.tag),
                    'attribute': attr, 'value': value, 'kind': 'xml'})
for path in sorted((ROOT / 'Scripts').rglob('*.cs')):
    for line, text in enumerate(path.read_text(encoding='utf-8-sig').splitlines(), 1):
        for match in re.finditer(r'\[[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?\]|new Color32\([^)]*\)', text):
            colors.append({'file': str(path.relative_to(ROOT)), 'screen': path.stem,
                'line': line, 'node': '', 'attribute': '', 'value': match[0], 'kind': 'code'})
write('colors_by_screen.json', colors)
summary = defaultdict(Counter)
for row in colors: summary[row['file'] + ' :: ' + row['screen']][row['value']] += 1
write('color_summary.json', {k: dict(v.most_common()) for k, v in summary.items()})

def localization(path):
    with path.open(encoding='utf-8-sig', newline='') as f:
        reader = csv.DictReader(f)
        return {r['Key']: r.get('english', '') for r in reader if r.get('Key')}
loc = localization(ROOT / 'Config/Localization.csv')
native = ROOT.parent.parent / 'Data/Config'
native_loc = localization(native / 'Localization.csv')
records, sources = source_definitions(ROOT)
# Keep mutations visible instead of implying declaration coverage evaluates patches.
# This inventory deliberately does not merge mutually exclusive option branches.
mutations = []
for path in sorted(sources):
    doc = ET.parse(str(path))
    for node in doc.xpath('//*[@xpath]'):
        xpath = node.get('xpath', '')
        props = [dict(p.attrib) for p in node.findall('property')
                 if p.get('name') in ('DescriptionKey', 'Extends')]
        if not props and 'DescriptionKey' not in xpath and 'Extends' not in xpath:
            continue
        mutations.append({'file': str(path.relative_to(ROOT)), 'line': node.sourceline,
            'operation': node.tag, 'xpath': xpath, 'attributes': dict(node.attrib), 'text': (node.text or '').strip(),
            'properties': props, 'status': 'requires_patch_semantics_review'})
write('description_inheritance_mutations.json', mutations)
lookup = {key: row[0] for key, row in records.items()}
native_keys = set()
for filename, tag in [('items', 'item'), ('blocks', 'block'), ('item_modifiers', 'item_modifier')]:
    doc = ET.parse(str(native / (filename + '.xml')))
    for node in doc.findall(tag):
        key = (tag, node.get('name')); native_keys.add(key)
        lookup.setdefault(key, node)

def property_value(key, prop, seen=()):
    if key in seen or key not in lookup: return None
    node = lookup[key]
    value = node.find("property[@name='" + prop + "']")
    if value is not None: return value.get('value')
    parent = node.find("property[@name='Extends']")
    if parent is None or prop in parent.get('param1', '').split(','): return None
    return property_value((key[0], parent.get('value')), prop, seen + (key,))

rows = []
native_override_rows = []
for key, (node, source, line) in sorted(records.items()):
    desc_key = property_value(key, 'DescriptionKey') or key[1] + 'Desc'
    text = loc.get(desc_key, native_loc.get(desc_key, ''))
    plain = re.sub(r'\[[^]]*\]', '', text).replace('\\n', ' ')
    words = len(plain.split())
    display_name = loc.get(key[1], native_loc.get(key[1], key[1]))
    normalize = lambda value: re.sub(r'\W+', '', value).casefold()
    name_only = bool(plain.strip()) and normalize(plain) == normalize(display_name)
    destination = native_override_rows if key in native_keys else rows
    destination.append({'type': key[0], 'id': key[1], 'source': source, 'line': line,
        'name': display_name, 'descriptionKey': desc_key,
        'description': text, 'words': words,
        'status': 'missing' if not text.strip() else 'name_only' if name_only else 'long' if words > 65 else 'present',
        'explicitDescriptionKey': node.find("property[@name='DescriptionKey']") is not None,
        'colors': re.findall(r'\[([0-9a-fA-F]{6})\]', text)})
write('custom_descriptions.json', rows)
write('native_override_descriptions.json', native_override_rows)
with (OUT/'custom_descriptions.csv').open('w', newline='', encoding='utf-8-sig') as f:
    writer=csv.DictWriter(f, fieldnames=list(rows[0])); writer.writeheader(); writer.writerows(rows)
counts = {'colors': len(colors), 'screens_and_shared_files': len(summary), 'customDefinitions': len(rows),
          'descriptionStatus': dict(Counter(r['status'] for r in rows)),
          'nativeOverrideDefinitions': len(native_override_rows),
          'nativeOverrideDescriptionStatus': dict(Counter(r['status'] for r in native_override_rows)),
          'descriptionInheritanceMutations': len(mutations),
          'scope': 'Both conditional branches; declarations and Extends only. DescriptionKey XPath mutations require separate review.'}
write('summary.json', counts)
(OUT/'AUDIT.txt').write_text('REBIRTH screen colors and custom descriptions â€” source audit\n'
    'Target: installed REBIRTH 3.0 / 7DTD 3.2 b10. Local source report; no package created.\n'
    f'Audit phase: {phase}. Includes per-screen XML attributes and C# rich text/Color32 literals.\n'
    'Custom description count excludes native IDs; native_override_descriptions.json separately audits REBIRTH declarations replacing them. Both reports include reachable items, blocks and modifiers in both option branches.\n'
    'Inheritance is respected: native clones are not automatically missing descriptions. Explicit DescriptionKey patches and dependency-localized keys still need reconciliation.\n'
    'This is a source inventory, not runtime verification or an approved final rewrite.\n\n' + json.dumps(counts, indent=2), encoding='utf-8')
print(json.dumps(counts, indent=2))
