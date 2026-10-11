"""Check current composed mixer requirements, dependency and station alias.
Run Compose-Configuration recipes/blocks/items before use. No native UI claim.
"""
from pathlib import Path
import xml.etree.ElementTree as E
root=Path(__file__).resolve().parents[2]
out=root/'_Documentation/CraftingAudit_20261010'
recipes=E.parse(out/'EFFECTIVE_recipes_rebirth.xml').getroot()
mixer=recipes.findall("recipe[@craft_area='cementMixer']")
assert {r.get('name') for r in mixer}=={'resourceConcreteMix','resourceCrushedSand','terrGravel','terrAsphalt','terrStone'}
assert all(r.get('craft_tool')=='carBattery' for r in mixer)
items=E.parse(out/'EFFECTIVE_items_rebirth.xml').getroot()
assert len(items.findall("item[@name='carBattery']"))==1
blocks=E.parse(out/'EFFECTIVE_blocks_rebirth.xml').getroot()
station=blocks.find("block[@name='cementMixer']")
assert 'tools' in station.find("property[@class='Workstation']/property[@name='Modules']").get('value').split(',')
alias=blocks.find("block[@name='cntCollapsedCementMixer']")
assert alias.find("property[@name='RebirthCraftingStation']").get('value')=='cementMixer'
# Compare patch structures: exactly one selector was widened; its value and
# attribute remain unchanged. Cumulative recipe equality is checked separately.
old=E.parse(out/'before_mixer_battery/recipe_tools.xml').getroot()
new=E.parse(root/'Config/_Workstations/recipe_tools.xml').getroot()
# Subsequent forge baseline restoration has its own exact before/after regression.
for op in list(new):
    if op.get('xpath')=="/recipes/recipe[@craft_area='forge' and not(@craft_tool)]":
        assert op.get('name')=='craft_tool' and op.text=='toolAnvil'
        new.remove(op)
def op_shape(n): return n.tag,sorted(n.attrib.items()),(n.text or '').strip(),[op_shape(c) for c in n]
changes=[(a,b) for a,b in zip(old,new) if op_shape(a)!=op_shape(b)]
assert len(list(old))==len(list(new)) and len(changes)==1
assert changes[0][1].get('xpath')=="/recipes/recipe[@craft_area='cementMixer' and not(@craft_tool)]"
assert changes[0][1].get('name')=='craft_tool' and changes[0][1].text=='carBattery'
print('PASS five mixer recipes require existing battery; tools module and repairable alias connect; only absent mixer tool assignment changed. Runtime not tested.')
