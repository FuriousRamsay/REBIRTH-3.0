from pathlib import Path
from lxml import etree as E
from copy import deepcopy
import json
r=Path.cwd(); q=E.parse(str(r/'../../Data/Config/quests.xml')); loot=E.parse(str(r/'../../Data/Config/loot.xml'))
items=E.parse(str(r/'../../Data/Config/items.xml'))
patch=E.parse(str(r/'Config/_Rebirth/quests.xml'))
for op in patch.getroot():
 if not isinstance(op.tag,str): continue
 hits=q.xpath(op.get('xpath')); assert hits,op.get('xpath')
 for node in hits:
  if op.tag=='remove': node.getparent().remove(node)
  elif op.tag=='append':
   for child in op: node.append(deepcopy(child))
  elif op.tag=='setattribute': node.set(op.get('name'),op.text or '')
  else: raise AssertionError(op.tag)
for tier in range(1,7):
 node=q.xpath(f"/quests/quest[@id='quest_tier{tier}complete']")[0]
 auto=node.xpath("reward[@id='vehicleBicyclePlaceable' and not(@ischosen='true')]")
 assert len(auto)==1
 for reward in node.xpath("reward[@ischosen='true' and @type='Item']"):
  assert items.xpath('/items/item[@name=$n]',n=reward.get('id')),reward.get('id')
 assert not node.xpath("reward[@ischosen='true' and contains(@id,'PartsBundle')]")
# Follow native loot groups from every selected quest reward to find indirect vehicle prizes.
def walk(group,seen):
 if group in seen:return []
 seen=seen|{group}; out=[]
 for item in loot.xpath('/lootcontainers/lootgroup[@name=$n]/item',n=group):
  if item.get('group'):out+=walk(item.get('group'),seen)
  if item.get('name'):out.append(item.get('name'))
 return out
bad=[]
for reward in q.xpath("/quests/quest/reward[@ischosen='true']"):
 names=walk(reward.get('id'),set()) if reward.get('type')=='LootItem' else [reward.get('id','')]
 for n in names:
  if (n.startswith('vehicle') and n.endswith('Placeable')) or n in ['questRewardMinibikePartsBundle','questRewardMotorcyclePartsBundle','questReward4x4PartsBundle','questRewardGyrocopterPartsBundle']:
   bad.append((reward.getparent().get('id'),n))
assert not bad,bad
print(json.dumps({'pass':True,'tiers':6,'automaticVehicles':6,'selectableVehiclePrizes':bad}))
