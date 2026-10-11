from pathlib import Path
import xml.etree.ElementTree as E
import csv
root=Path(__file__).resolve().parents[2]; d=root/'_Documentation/CraftingAudit_20261010'
a=E.parse(d/'PRE_LATE_WORKBENCH_recipes.xml').getroot(); b=E.parse(d/'EFFECTIVE_recipes_rebirth.xml').getroot()
expected={r['Recipe']:r['LegacyTool'] for r in csv.DictReader((d/'LATE_TOOL_ASSIGNMENTS.csv').open(encoding='utf-8-sig')) if r['Station']=='workbench' and r['Matches']=='False' and not r['CurrentTool']}
def shape(n): return n.tag,sorted(n.attrib.items()),(n.text or '').strip(),[shape(c) for c in n]
assert len(a)==len(b)
changed=[]
for old,new in zip(a,b):
    name=old.get('name')
    if name in expected and old.get('craft_area')=='workbench':
        assert new.get('craft_tool')==expected[name],name
        assert old.get('craft_tool') is None,name
        del new.attrib['craft_tool']; changed.append(name)
    if old.get('craft_area')=='cementMixer':
        assert name in {'resourceConcreteMix','resourceCrushedSand','terrGravel','terrAsphalt','terrStone'},name
        assert old.get('craft_tool') is None and new.get('craft_tool')=='carBattery',name
        del new.attrib['craft_tool']
    if name=='FuriousRamsayWoodPlank' and old.get('craft_area')=='WorkbenchCircularMachine001_FR':
        assert old.get('craft_tool') is None and new.get('craft_tool')=='FuriousRamsayCircularSawBlade',name
        del new.attrib['craft_tool']
    if old.get('craft_area')=='forge' and old.get('craft_tool') is None:
        assert new.get('craft_tool')=='toolAnvil',name
        del new.attrib['craft_tool']
    assert shape(old)==shape(new),name
assert len(changed)==33 and set(changed)==set(expected),changed
print('PASS: exactly33 workbench tool assignments plus the five audited mixer battery requirements existing saw blade restoration and forge anvil baseline; all other recipe attributes, ingredients, outputs and effects preserved')