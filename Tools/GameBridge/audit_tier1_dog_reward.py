from pathlib import Path
from lxml import etree as E
from copy import deepcopy
import json
r=Path(__file__).resolve().parents[2]
q=E.parse(str(r/'../../Data/Config/quests.xml'))
for op in E.parse(str(r/'Config/_Rebirth/quests.xml')).getroot():
 if not isinstance(op.tag,str):continue
 hits=q.xpath(op.get('xpath'));assert hits,op.get('xpath')
 for n in hits:
  if op.tag=='remove':n.getparent().remove(n)
  elif op.tag=='append':
   for c in op:n.append(deepcopy(c))
  elif op.tag=='setattribute':n.set(op.get('name'),op.text or '')
  elif op.tag=='set':
   if isinstance(n,E._ElementUnicodeResult) and n.is_attribute:
    n.getparent().set(n.attrname,op.text or '')
   elif isinstance(n,E._Element):n.text=op.text or ''
   else:raise AssertionError('Unsupported set target: '+op.get('xpath'))
  else:raise AssertionError(op.tag)
t=q.xpath("/quests/quest[@id='quest_tier1complete']")[0]
for name in ('BlueDog3','vehicleBicyclePlaceable'):
 rewards=t.xpath('reward[@id=$name]',name=name)
 assert len(rewards)==1 and rewards[0].get('value')=='1' and rewards[0].get('ischosen')!='true'
assert not q.xpath("/quests/quest[@id!='quest_tier1complete']/reward[@id='BlueDog3']")
assert E.parse(str(r/'Config/items.xml')).xpath("//item[@name='BlueDog3']")
assert (r/'UIAtlases/ItemIconAtlas/BlueDog3.png').is_file()
recipes=E.parse(str(r/'Config/recipes.xml')).xpath("//recipe[ingredient[@name='BlueDog3']]")
assert len(recipes)==8
blocks=E.parse(str(r/'Config/_NPC/dog_blocks.xml'))
for recipe in recipes:
 assert recipe.getparent().tag=="append" and recipe.getparent().get("xpath")=="/recipes"
 assert recipe.getparent().getparent().get("cond")=="character_progression('Rebirth')"
 assert recipe.getparent().getparent().getparent().tag=="conditional"
 assert len(recipe.findall('ingredient'))==1 and recipe.find('ingredient').get('count')=='1'
 assert recipe.get('count')=='1' and recipe.get('craft_area') is None
 assert blocks.xpath("//block[@name=$name]/property[@name='Class' and @value='RebirthDogDeployBlock, RebirthUtils']",name=recipe.get('name'))
# Assert both destination metadata and the source trader's offered introduction.
trader_route=[('trader_rekt_quests',2,'TraderBob','trader_bob','desert'),
 ('trader_bob_quests',3,'TraderHugh','trader_hugh','snow'),
 ('trader_hugh_quests',4,'TraderJoel','trader_joel','wasteland'),
 ('trader_joel_quests',5,'TraderJen','trader_jen','burnt')]
for source,tier,tag,location,biome in trader_route:
 quest_id=f'tier{tier}_nexttrader'
 quest=q.xpath('/quests/quest[@id=$id]',id=quest_id)
 assert len(quest)==1,quest_id
 assert quest[0].xpath("objective[@type='Goto']/property[@name='location_tag']/@value")==[tag]
 assert quest[0].xpath("objective[@type='Goto']/property[@name='location_name']/@value")==[location]
 assert quest[0].xpath("property[@name='offer_key']/@value")==['quest_next_trader_'+biome]
 assert len(q.xpath('/quests/quest_list[@id=$source]/quest[@id=$id]',source=source,id=quest_id))==1
assert not q.xpath("/quests/quest_list[@id='trader_jen_quests']/quest[starts-with(@id,'tier') and contains(@id,'nexttrader')]")
print(json.dumps({'pass':True,'tier1Automatic':['vehicleBicyclePlaceable','BlueDog3'],'breedRecipes':[x.get('name') for x in recipes],'traderRoute':[row[2] for row in trader_route],'scope':'static patch application, item/icon references, eight handcraft conversions and trader destination/source-list routing; runtime not run'}))

