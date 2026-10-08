"""Audit the actual merged disposable-save XML, never a simulated patch result."""
from pathlib import Path
from lxml import etree as E
import json
import hashlib
from datetime import datetime, timezone
ROOT=Path(__file__).resolve().parents[2]
DUMP=Path.home()/'AppData/Roaming/7DaysToDie/Saves/West Xuyofu Territory/CodexTest/ConfigsDump'
docs={n:E.parse(str(DUMP/(n+'.xml'))) for n in ('items','blocks','recipes','loot','traders','quests','challenges','progression')}
known=set(docs['items'].xpath('/items/item/@name'))|set(docs['blocks'].xpath('/blocks/block/@name'))
known.update(E.parse(str(DUMP/'item_modifiers.xml')).xpath('/item_modifiers/item_modifier/@name'))
groups={n.get('name'):n for n in docs['loot'].xpath('/lootcontainers/lootgroup')}
tradegroups=set(docs['traders'].xpath('//trader_item_group/@name'))
literature=E.parse(str(ROOT/'Config/_Survivor/literature.xml'))
checks={}
checks['magazine_leaves']=[n.get('name') for k in ('loot','traders') for n in docs[k].xpath('//item[@name]') if n.get('name').endswith('SkillMagazine')]
checks['missing_loot_groups']=sorted({n.get('group') for n in docs['loot'].xpath('//item[@group]')}-groups.keys())
checks['missing_progression_pool_items']=sorted({n.get('name') for g,nodes in groups.items() if g.startswith('rebirth') for n in nodes.findall('item') if n.get('name')}-known)
checks['recipe_cassettes']=[n.get('name') for k in ('loot','traders') for n in docs[k].xpath('//item[@name]') if n.get('name','').startswith(('rebirthAudioManual','rebirthAudioRecipe','rebirthAudioSchematic','rebirthAudioPattern','rebirthAudioFormula','rebirthAudioGuide','rebirthAudioCookbook'))]
forbidden={'meleeWpnSpearT0StoneSpear','meleeWpnClubT0WoodenClub','meleeWpnSledgeT0StoneSledgehammer','meleeWpnClubT1BaseballBat','gunBowT0PrimitiveBow','gunBowT1WoodenBow'}
checks['primitive_weapon_loot']=sorted(set(docs['loot'].xpath('//item/@name'))&forbidden)
checks['primitive_weapon_direct_rewards']=sorted(set(docs['quests'].xpath('//reward[@type="Item"]/@id'))&forbidden)
checks['skill_point_quest_rewards']=docs['quests'].xpath('//quest[reward[@type="SkillPoints"]]/@id')
checks['level_skill_points_zero']=docs['progression'].xpath('/progression/level/@skill_points_per_level')==['0']
checks['learning_challenges_count']=len(docs['challenges'].xpath('/challenges/challenge[starts-with(@name,"rebirthLesson")]'))
authored_lessons=set(E.parse(str(ROOT/'Config/_Rebirth/challenges.xml')).xpath('//challenge/@name'))
loaded_lessons=set(docs['challenges'].xpath('/challenges/challenge[starts-with(@name,"rebirthLesson")]/@name'))
checks['missing_learning_challenges']=sorted(authored_lessons-loaded_lessons)
checks['unexpected_learning_challenges']=sorted(loaded_lessons-authored_lessons)

# Traverse normal loot-container roots and quest reward pools, rather than accepting orphan groups.
reachable=set(); pending=list(docs['loot'].xpath('/lootcontainers/lootcontainer/item/@group'))
for reward in docs['quests'].xpath('//reward[@type="LootItem"]/@id'):pending.extend(reward.split(','))
while pending:
    g=pending.pop()
    if g in reachable or g not in groups:continue
    reachable.add(g);pending.extend(groups[g].xpath('./item/@group'))
loot_items=set(docs['loot'].xpath('/lootcontainers/lootcontainer/item/@name'))
for g in reachable:loot_items.update(groups[g].xpath('./item/@name'))
trade_nodes={n.get('name'):n for n in docs['traders'].xpath('/traders/trader_item_groups/trader_item_group')}
trade_reachable=set(); trade_pending=list(docs['traders'].xpath('/traders/trader_info//item/@group'))
trade_items=set(docs['traders'].xpath('/traders/trader_info//item/@name'))
while trade_pending:
    g=trade_pending.pop()
    if g in trade_reachable or g not in trade_nodes:continue
    trade_reachable.add(g)
    trade_pending.extend(trade_nodes[g].xpath('./item/@group'))
    trade_items.update(trade_nodes[g].xpath('./item/@name'))
checks['missing_trader_groups']=sorted(set(docs['traders'].xpath('//item/@group'))-trade_nodes.keys())
checks['reachable_trader_groups']=len(trade_reachable)
outputs=set(docs['recipes'].xpath('/recipes/recipe/@name'))
checks['unreachable_literature']=sorted(set(literature.xpath('//item/@id'))-loot_items-trade_items)
checks['custom_recipe_inputs_without_source']=sorted({n for n in docs['recipes'].xpath('//ingredient/@name') if n.lower().startswith('rebirth')}-loot_items-trade_items-outputs)
capabilities=E.parse(str(ROOT/'Config/_Survivor/crafting_progression.xml'))
grants=set(literature.xpath('//item[@kind="discovery"]/@knowledge'))
checks['recipe_knowledge_without_literature']=sorted(set(capabilities.xpath('//recipe/@knowledge'))-grants)
checks['reachable_loot_groups']=len(reachable)
checks['all_loot_groups']=len(groups)
dump_evidence = {}
for name in (*docs.keys(), 'item_modifiers'):
    source = DUMP/(name+'.xml')
    dump_evidence[name] = {'modifiedUtc': datetime.fromtimestamp(source.stat().st_mtime, timezone.utc).isoformat(),
                           'sha256': hashlib.sha256(source.read_bytes()).hexdigest()}
report={'source':str(DUMP),'auditedUtc':datetime.now(timezone.utc).isoformat(),
        'dumpEvidence':dump_evidence,'checks':checks,'limitations':[
        'This validates the recorded game dump, not necessarily current source. A fresh game load is required to regenerate merged configuration after edits.',
        'Graph availability does not prove balanced drop frequency, skill-gate accessibility, or actual trader turn-in.','Weapon output and tier balance require live profile comparisons.']}
path=ROOT/'_Documentation/Progression_Loot/merged_distribution_audit.json'
path.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(checks,indent=2))
failure_keys=('magazine_leaves','missing_loot_groups','missing_progression_pool_items',
              'recipe_cassettes','primitive_weapon_loot','primitive_weapon_direct_rewards',
              'skill_point_quest_rewards','missing_trader_groups','unreachable_literature',
              'custom_recipe_inputs_without_source','recipe_knowledge_without_literature',
              'missing_learning_challenges','unexpected_learning_challenges')
if any(checks[k] for k in failure_keys) or not checks['level_skill_points_zero'] or not authored_lessons:
    raise SystemExit(1)
