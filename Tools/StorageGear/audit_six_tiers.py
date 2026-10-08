from pathlib import Path
import xml.etree.ElementTree as E,csv,json,hashlib
from PIL import Image
root=Path('.');names=['Utility','Field','Tactical','Artisan','Industrial','Expedition'];ids=['rebirthGear'+n+'Belt' for n in names]
files=['items','recipes','support_profiles','capabilities','crafting_progression','recipe_knowledge'];trees={f:E.parse(root/f'Config/_Survivor/{f}.xml') for f in files}
loc=dict((r[0],r[1]) for r in csv.reader((root/'Config/Localization.csv').open(encoding='utf-8-sig')) if len(r)>1)
loot=E.parse(root/'Config/_Rebirth/storage_loot.xml');hashes=set();results=[]
for i,item in enumerate(ids):
 node=trees['items'].find(f'.//item[@name="{item}"]');assert node is not None
 assert node.find('property[@name="CustomIcon"]').get('value')==item
 im=Image.open(root/f'UIAtlases/ItemIconAtlas/{item}.png');assert im.size==(160,160) and im.mode=='RGBA' and im.getextrema()[3][0]==0
 h=hashlib.sha256(im.tobytes()).hexdigest();assert h not in hashes;hashes.add(h)
 recipe=trees['recipes'].find(f'.//recipe[@name="{item}"]');assert recipe is not None
 valid={n.get('name') for n in E.parse(root/'../../Data/Config/items.xml').iter('item')}|{n.get('name') for n in trees['items'].iter('item')}
 for ingredient in recipe:assert ingredient.get('name') in valid,ingredient.attrib
 p=trees['support_profiles'].find(f'.//support_profile[@gear_item_id="{item}"]');assert int(p.get('toolbelt_slot_bonus'))==2+i*2
 assert item in loc and item+'Desc' in loc
 if i:
  assert trees['capabilities'].find(f'.//capability[@target_id="{item}"]') is not None
  assert trees['crafting_progression'].find(f'.//recipe[@id="{item}"]') is not None
  assert trees['recipe_knowledge'].find(f'.//recipe[@name="{item}"]') is not None
  assert loot.find(f'.//item[@name="{item}"]') is not None
 else:assert loot.find(f'.//item[@name="{item}"]') is None
 results.append({'item':item,'slots':6+2*i,'iconUnique':True,'recipeAndProgression':True})
for f in ['Config/buffs.xml','Config/_Survivor/background_bonuses.xml']:E.parse(root/f)
(root/'_Documentation/GearTransfers/static_six_tiers.json').write_text(json.dumps(results,indent=2));print('PASS: six unique RGBA icons, all recipes/progression/loot/localization mappings; starter absent from loot')
