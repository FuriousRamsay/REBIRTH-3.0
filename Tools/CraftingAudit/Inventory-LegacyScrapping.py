"""Inventory legacy custom scrap rules separately from ordinary XML recipes.
This reports authored rules and current dependencies, not runtime compatibility.
"""
import csv
from collections import Counter
from pathlib import Path
import xml.etree.ElementTree as ET
root = Path(__file__).resolve().parents[2]
out = root / '_Documentation/CraftingAudit_20261010'
# Mod root is below 7 Days To Die/Mods; the sibling install is below common.
legacy = root.parents[2] / '7 Days To Die 2.6/Mods/zzz_REBIRTH__Utils/Config/Scrap.xml'
names = set()
for kind, tag in [('items', 'item'), ('blocks', 'block')]:
    names.update(n.get('name') for n in ET.parse(out/f'EFFECTIVE_{kind}_rebirth.xml').getroot().findall(tag))
rows=[]
for index, rule in enumerate(ET.parse(legacy).getroot().findall('scrap'), 1):
    matchers = {k:v for k,v in rule.attrib.items() if k.startswith('item')}
    outputs = rule.findall('ingredient')
    tools = [v.strip() for v in (rule.get('scrap_tools') or rule.get('scrap_tool','')).split(',') if v.strip()]
    deps = {n.get('name') for n in outputs} | set(tools)
    if rule.get('item'): deps.add(rule.get('item'))
    rows.append(dict(rule=index, station=rule.get('station',''), matcher=str(matchers),
        outputs=';'.join(f"{n.get('name')}:{n.get('count')}:{n.get('time', rule.get('time','default'))}" for n in outputs),
        tools=','.join(tools), missing_dependencies=','.join(sorted(deps-names)),
        status=('LEGACY_CUSTOM_TRANSACTION_NOT_IMPLEMENTED' if rule.get('station') else 'INVALID_LEGACY_RULE_NO_STATION')))
with (out/'LEGACY_SCRAP_RULES.csv').open('w', newline='', encoding='utf-8') as f:
    w=csv.DictWriter(f,fieldnames=list(rows[0])); w.writeheader(); w.writerows(rows)
print(f'{len(rows)} authored custom scrap rules; pattern expansion and priority require runtime reconciliation.')
for station,count in sorted(Counter(r['station'] for r in rows).items()):
    subset=[r for r in rows if r['station']==station]
    print(f"{station}: {count} rules; {sum(bool(r['missing_dependencies']) for r in subset)} with missing explicit dependencies")

