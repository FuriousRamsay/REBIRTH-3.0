"""Audit every effective workstation, including zero-recipe stations and inherited aliases.
Inputs are composed native + REBIRTH XML; external mods and runtime-generated recipes
are explicitly outside this inventory, so missing routes are findings, not auto-fixes.
"""
import csv
from pathlib import Path
import xml.etree.ElementTree as ET
root = Path(__file__).resolve().parents[2]
out = root / '_Documentation/CraftingAudit_20261010'
blocks = {b.get('name'): b for b in ET.parse(out/'EFFECTIVE_blocks_rebirth.xml').getroot().findall('block')}
recipes = ET.parse(out/'EFFECTIVE_recipes_rebirth.xml').getroot().findall('recipe')
def properties(name, chain=()):
    if name in chain: raise ValueError('Block inheritance cycle: '+str(chain+(name,)))
    block = blocks.get(name)
    if block is None: raise ValueError('Missing base block '+name)
    parent = block.find("property[@name='Extends']")
    values = properties(parent.get('value'), chain+(name,)) if parent is not None else {}
    def walk(node, prefix=''):
        for p in node.findall('property'):
            if p.get('class'): walk(p,prefix+p.get('class')+'/')
            elif p.get('name'): values[prefix+p.get('name')] = p.get('value','')
    walk(block)
    return values
rows=[]
for name in blocks:
    p=properties(name)
    cls=p.get('Class','')
    areas=p.get('Workstation/CraftingAreaRecipes',name if cls in ('Workstation','Campfire','Forge') else '')
    # Legacy storage shells retain parent properties but explicitly cease being stations.
    if not areas or cls in ('CompositeTileEntity','Loot'): continue
    accepted=set(areas.split(','))
    matched=[r for r in recipes if r.get('craft_area','') in accepted]
    tools=sorted({r.get('craft_tool') for r in matched if r.get('craft_tool')})
    modules=p.get('Workstation/Modules','')
    findings=[]
    if not matched: findings.append('NO_XML_RECIPES: review runtime routes')
    if tools and 'tools' not in modules.split(','): findings.append('RECIPE_TOOLS_WITHOUT_MODULE')
    rows.append(dict(station=name,block_class=cls,runtime_station=p.get('RebirthCraftingStation',name),areas=areas,recipe_count=len(matched),modules=modules,
                     recipe_tools=','.join(tools),fuel_type=p.get('FuelType',''),
                     window=p.get('WorkstationWindow','(native default)'),findings='; '.join(findings)))
path=out/'ALL_STATION_MATRIX.csv'
with path.open('w',newline='',encoding='utf-8') as f:
    w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(sorted(rows,key=lambda r:r['station']))
print(f'{len(rows)} station definitions including inherited aliases; {sum(bool(r["findings"]) for r in rows)} require review.')
for row in rows:
    if row['findings']: print(row['station']+': '+row['findings'])
