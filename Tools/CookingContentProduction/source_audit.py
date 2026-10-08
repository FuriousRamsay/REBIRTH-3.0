"""Capture source dependencies and balance anchors without changing game configuration."""
from pathlib import Path
import json
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / '_Documentation/FeatureDesign/SurvivorSkillSystem/CookingCatalogue_20260915/Production'
OLD = ROOT.parents[2] / '7 Days To Die 2.6/Mods/zzz_REBIRTH__Core'
stations = {}
for block in ET.parse(OLD / 'Config/blocks.xml').iter('block'):
    name = block.get('name', '')
    if name in {'WorkbenchGasStove001_FR', 'WorkbenchIronOven001_FR', 'WorkbenchMortarPestle001_FR'}:
        stations[name] = ET.tostring(block, encoding='unicode')
anchors = {}
for node in ET.parse(ROOT / 'Config/_Metabolism/items.xml').getroot():
    if node.tag != 'append':
        continue
    match = re.fullmatch(r"/items/item\[@name='([^']+)'\]", node.get('xpath', ''))
    if match:
        values = {p.get('name'): p.get('value') for p in node.findall('property')}
        if 'RebirthNutritionUnits' in values:
            anchors[match.group(1)] = values
native = {n.get('name'): n for n in ET.parse(ROOT.parent.parent / 'Data/Config/items.xml').getroot().findall('item')}
def inherited(name, seen=None):
    seen = set() if seen is None else seen
    if name in seen:
        raise ValueError('Circular item inheritance: ' + name)
    seen.add(name)
    node = native.get(name)
    parent = node.find("property[@name='Extends']") if node is not None else None
    values = inherited(parent.get('value'), seen) if parent is not None else {}
    values.update(anchors.get(name, {}))
    return values
resolved = {name: inherited(name) for name in native}
resolved.update(anchors)
report = {'legacy_root': str(OLD), 'station_definitions': stations,
          'metabolism_anchors': {k: v for k, v in resolved.items() if v},
          'industrial_bundle_bytes': (OLD / 'Resources/FR_Industrial.unity3d').stat().st_size}
(OUT / 'source_audit.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps({'stations': list(stations), 'nutrition_anchors': len(anchors), 'bundle_bytes': report['industrial_bundle_bytes']}))
