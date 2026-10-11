from pathlib import Path
import xml.etree.ElementTree as E
root=Path(__file__).resolve().parents[2];d=root/'_Documentation/CraftingAudit_20261010'
a=E.parse(d/'before_forge_anvil/effective_recipes.xml').getroot();b=E.parse(d/'EFFECTIVE_recipes_rebirth.xml').getroot()
def shape(n):return n.tag,sorted(n.attrib.items()),(n.text or '').strip(),[shape(c) for c in n]
assert len(a)==len(b);changed=[]
for old,new in zip(a,b):
    if old.get('craft_area')=='forge' and not old.get('craft_tool'):
        assert new.get('craft_tool')=='toolAnvil';changed.append(new.get('name'));del new.attrib['craft_tool']
    assert shape(old)==shape(new),new.get('name')
assert len(changed)==55
items=E.parse(d/'EFFECTIVE_items_rebirth.xml').getroot()
assert len(items.findall("item[@name='toolAnvil']"))==1
print('PASS 55 forge variants gain only toolAnvil; tool exists; explicit crucible requirements and all remaining recipe fields unchanged. Runtime not tested.')
