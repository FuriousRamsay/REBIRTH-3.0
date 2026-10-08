"""Verify native-merged drone mod pools and their intended live roots."""
from pathlib import Path
from lxml import etree as E
import json
root=Path(__file__).resolve().parents[2]
dump=Path.home()/'AppData/Roaming/7DaysToDie/Saves/West Xuyofu Territory/CodexTest/ConfigsDump'
loot=E.parse(str(dump/'loot.xml')); mods=E.parse(str(dump/'item_modifiers.xml')); quests=E.parse(str(dump/'quests.xml'))
groups={g.get('name'):g for g in loot.xpath('/lootcontainers/lootgroup')}
expected={'modRoboticDrone'+s+'Mod' for s in ('Cargo','Headlamp','ArmorPlating','Medic','MoraleBooster','StunWeapon')}
def leaves(name,seen=None):
    seen=set() if seen is None else seen
    assert name not in seen, 'cycle: '+name
    node=groups[name]; out=set(node.xpath('./item/@name'))
    for child in node.xpath('./item/@group'):out |= leaves(child,seen|{name})
    return out
assert expected <= set(mods.xpath('/item_modifiers/item_modifier/@name'))
assert leaves('rebirthDroneModFind')==expected
assert 'modRoboticDroneWeaponMod' not in leaves('rebirthDroneModFind')
for name,p in [('groupMoPowerA','0.25'),('groupMoPowerShelves','0.08')]:
    nodes=groups[name].xpath('./item[@group="rebirthDroneModFind"]')
    assert len(nodes)==1 and nodes[0].get('prob')==p and nodes[0].get('force_prob')=='true'
assert expected <= leaves('groupQuestMods')
assert quests.xpath('//reward[@type="LootItem" and contains(@id,"groupQuestMods")]')
assert loot.xpath('/lootcontainers/lootcontainer/item[@group="groupMoPower"]')
assert 'rebirthDroneModFind' in groups['groupMoPowerA'].xpath('./item/@group')
report={'result':'PASS','finished_mods':sorted(expected),'crate_extra_roll':0.25,'shelf_extra_roll':0.08,'trader_reward_root':'groupQuestMods','placeholder_excluded':True,'source':str(dump)}
out=root/'_Documentation/DroneProgression';out.mkdir(exist_ok=True)
(out/'loot_audit.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report))
