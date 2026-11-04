from pathlib import Path
from lxml import etree as E
import json,copy,hashlib
r=Path.cwd();d=Path('E:/_Haven/shared/CodexPurgeRemote20261102/Saves/Navezgane/CodexPurgeRemote20261102/ConfigsDump');ids={'modArmor'+x for x in ['Strength','Perception','Fortitude','Agility','Intellect']};result={}
for category,patch in [('loot','Config/_Rebirth/loot_skill_tag_cleanup.xml'),('traders','Config/_Rebirth/traders.xml'),('quests','Config/_Rebirth/quests.xml')]:
 tree=E.parse(str(d/(category+'.xml')));ops=E.parse(patch).xpath('/configs/remove[contains(@xpath,"modArmorStrength")]');assert len(ops)==1
 before=list(tree.xpath(ops[0].get('xpath')));assert before
 affected={n.getparent() for n in before}
 for n in before:n.getparent().remove(n)
 assert not tree.xpath(ops[0].get('xpath'))
 empty=[g.get('name') for g in affected if g.tag in ['lootgroup','trader_item_group'] and not g.findall('item')];assert not empty,empty
 if category=='quests':
  q=tree.xpath('/quests/quest[@id="quest_reward_mutated_zombie_slayer"]')[0];assert q.find('reward') is not None
 result[category]={'removed':len(before),'after':0,'nonemptyPools':True,'dumpSHA256':hashlib.sha256((d/(category+'.xml')).read_bytes()).hexdigest()}
# These five chip identifiers are not embedded in native bundle output lists.
items=E.parse(str(d/'items.xml'))
assert not [e.attrib for e in items.xpath('//property[@name="Create_item" or @name="Random_item"]') if set(e.get('value','').split(','))&ids]
for category in ['loot','traders','quests']:
 main=E.parse('Config/'+category+'.xml');nodes=main.xpath('//include[@filename=$f]',f='_Rebirth/'+('loot_skill_tag_cleanup.xml' if category=='loot' else category+'.xml'));assert len(nodes)==1 and nodes[0].getparent().get('cond')=="character_progression('Rebirth')"
result['scope']='Exact source removals on retained native REBIRTH dumps; BaseGame mode includes isolated; no post-edit native dump claim.'
Path('_Documentation/PlayerReports_20261102/VanillaAttributeMods/offline-verification.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))

