from pathlib import Path
import xml.etree.ElementTree as E
import csv,hashlib
root=Path.cwd(); d=root/'_Documentation/CraftingAudit_20261010'
a=E.parse(d/'PRE_OUTFIT_recipes.xml').getroot();b=E.parse(d/'EFFECTIVE_recipes_rebirth.xml').getroot()
def shape(n):return (n.tag,tuple(sorted(n.attrib.items())),(n.text or '').strip(),tuple(shape(c) for c in n))
assert len(a)==len(b)
changed=0
for old,new in zip(a,b):
 target=old.get('name','').startswith('armor') and not old.get('name','').startswith('armorPrimitive') and any(k in old.get('name','') for k in ('Helmet','Outfit','Gloves','Boots'))
 if target:
  assert new.get('craft_area')=='SonjaOutfitDesigner'
  assert new.get('craft_tool')=='FuriousRamsayArmorCrafting'
  for attr in ('craft_area','craft_tool'):
   if attr in old.attrib:new.set(attr,old.get(attr))
   else:new.attrib.pop(attr,None)
  changed+=1
 assert shape(old)==shape(new),old.get('name')
assert changed==60,changed
items=E.parse(d/'EFFECTIVE_items_rebirth.xml').getroot(); tool=items.findall("item[@name='FuriousRamsayArmorCrafting']");assert len(tool)==1
assert tool[0].find("property[@name='CustomIcon']").get('value')=='FR_ArmorCrafting_icon'
legacy=Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die 2.6/Mods/zzz_REBIRTH__Core')
assert (root/'UIAtlases/ItemIconAtlas/FR_ArmorCrafting_icon.png').read_bytes()==(legacy/'UIAtlases/ItemIconAtlas/FR_ArmorCrafting_icon.png').read_bytes()
loot=E.parse(root/'../../Data/Config/loot.xml').getroot();assert loot.find("lootgroup[@name='groupSavageCountryCrate']") is not None
traders=E.parse(root/'../../Data/Config/traders.xml').getroot();assert traders.find("trader_info[@id='8']") is not None
rows=list(csv.DictReader((root/'Config/Localization.csv').open(encoding='utf-8-sig')))
for key in ('FuriousRamsayArmorCrafting','FuriousRamsayArmorCraftingDesc'):
 assert len([r for r in rows if r['Key']==key and r['english']])==1,key
print('PASS: 60 routes/tools changed; every other recipe attribute/ingredient preserved; tool/icon/localization and native acquisition targets exist. Runtime pending; final composition assertions follow.')
# The current progression is authoritative over legacy station routing.
progression={n.get('id'):n for n in E.parse(root/'Config/_Survivor/crafting_progression.xml').getroot().find('recipes')}
current=E.parse(d/'EFFECTIVE_recipes_rebirth.xml').getroot()
routed=[n for n in current if n.get('craft_area')=='SonjaOutfitDesigner']
assert len(routed)==60
assert all(progression[n.get('name')].get('primary_skill')=='skill.tailoring' for n in routed)
for n in current:
 if n.get('name','').startswith('armorPrimitive'):
  assert n.get('craft_area','')=='' and not n.get('craft_tool'),n.attrib
loot=E.parse(d/'EFFECTIVE_loot_rebirth.xml').getroot()
traders=E.parse(d/'EFFECTIVE_traders_rebirth.xml').getroot()
assert len(loot.findall("lootgroup[@name='groupSavageCountryCrate']/item[@name='FuriousRamsayArmorCrafting']"))==1
assert len(traders.findall("trader_info[@id='8']/trader_items/item[@name='FuriousRamsayArmorCrafting']"))==1
print('PASS: all 60 routes retain explicit Tailoring authority; primitive backpack routes unchanged; final loot/trader acquisition each present once.')
