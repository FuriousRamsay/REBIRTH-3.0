"""Offline linkage audit. Does not claim live-game behavior verification."""
from pathlib import Path
import csv, json
import xml.etree.ElementTree as E

ROOT=Path(__file__).resolve().parents[2]
def xml(path): return E.parse(ROOT/path).getroot()
def text(path): return (ROOT/path).read_text(encoding='utf-8-sig')
condition='Scripts/Survivor/Condition/RebirthConditionTraitModifierService.cs'
gameplay='Scripts/Survivor/Progression/RebirthTraitGameplayModifierService.cs'
creation='Scripts/Survivor/Creation/RebirthSurvivorCreationValidator.cs'
stress='Scripts/Survivor/Condition/RebirthStressService.cs'
support='Scripts/Survivor/Support/RebirthTraitSupportService.cs'
profiles={e.get('id'):e for e in xml('Config/_Survivor/condition_profiles.xml').iter('modifier_profile')}
supports=xml('Config/_Survivor/support_profiles.xml')
rows=[]
for trait in xml('Config/_Survivor/traits.xml').findall('trait'):
    ident=trait.get('id'); profile=profiles[trait.get('modifier_id')]
    assert profile.get('implementation_state') in ('IMPLEMENTED','CREATION_ONLY'), ident
    targets=[]; consumers=[]
    for c in profile.findall('component'):
        target=c.get('target'); targets.append(target)
        if c.get('phase')=='creation':
            consumer=gameplay if target=='inventory.unencumbered_slots' else creation
        elif target.startswith('stress.'): consumer=stress
        elif target.startswith(('skill.gain.','craft.time.','barter.','harvest.','vehicle.','medicine.','literature.','stamina.heavy')): consumer=gameplay
        else: consumer=condition
        code=text(consumer)
        prefix=next((p for p in ('attribute.','skill.start.','skill.gain.','craft.time.') if target.startswith(p)),target)
        assert prefix in code,(ident,target,consumer)
        consumers.append(consumer)
    if not targets:
        matching=[s for s in supports if s.get('habit_trait_id')==ident or any(t.get('id')==ident for t in s.iter('trait'))]
        assert matching, ident
        targets=['support:'+s.get('id') for s in matching];consumers=[support]
    rows.append([ident,profile.get('implementation_state'),'; '.join(targets),'; '.join(sorted(set(consumers))),profile.get('effect_summary')])

items={e.get('name'):e for e in xml('Config/_Survivor/items.xml').iter('item')}
recipes={e.get('name'):e for e in xml('Config/_Survivor/recipes.xml').iter('recipe')}
ownership={e.get('id'):e for e in xml('Config/_Survivor/crafting_progression.xml').iter('recipe')}
caps={e.get('id'):e for e in xml('Config/_Survivor/capabilities.xml').iter('capability')}
loot=xml('Config/_Rebirth/storage_loot.xml')
lootitems={e.get('name') for e in loot.iter('item') if e.get('name')}
nativeitems={e.get('name') for e in xml('../../Data/Config/items.xml').iter('item')}
for suffix in ('Daypack','UtilityBelt','FieldPack','HikingPack','ExpeditionPack','FieldBelt','TacticalBelt'):
    name='rebirthGear'+suffix
    assert name in items and name in recipes and name in ownership,name
    for ingredient in recipes[name].findall('ingredient'): assert ingredient.get('name') in items.keys()|nativeitems,ingredient.attrib
    if suffix in ('Daypack','UtilityBelt'):
        assert name not in lootitems
        assert ownership[name].get('policy')=='universal'
        assert recipes[name].get('craft_area') is None
    else: assert ownership[name].get('capability') in caps
nativegroups={e.get('name') for e in xml('../../Data/Config/loot.xml').iter('lootgroup')}
for a in loot.findall('append'):
    if "lootgroup[@name='" in a.get('xpath',''): assert a.get('xpath').split("[@name='")[1].split("'")[0] in nativegroups
for e in loot.iter('item'):
    if e.get('loot_prob_template'): assert e.get('loot_prob_template') in {p.get('name') for p in loot.iter('lootprobtemplate')}
assert '1800f' in text('Scripts/Survivor/Support/RebirthGearOverflow.cs')
bag=next(e for e in xml('Config/entityclasses.xml').iter('entity_class') if e.get('name')=='rebirthGearRecoveryBackpack')
assert bag.find("property[@name='TimeStayAfterDeath']").get('value')=='3600'
guides=xml('Resources/Journal/entries.xml')
assert len([e for e in guides if e.get('trigger')=='intro'])==1
assert not any(e.get('id','').startswith('Skill_') for e in guides)
assert guides.find("entry[@id='Medicine']/image") is None
assert 'UnreadCount > 0' in text('Scripts/UI/RebirthJournalGuideService.cs')
assert 'journalImageInput' in text('Scripts/UI/XUiC_RebirthJournal.cs')
for f in ['Config/XUi_InGame/windows.xml','Config/XUi_InGame/journal_guides.xml','Config/buffs.xml','Config/_Rebirth/challenges.xml']:xml(f)
out=ROOT/'_Documentation/TraitsAndStorage';out.mkdir(exist_ok=True)
with (out/'trait_connections.csv').open('w',newline='',encoding='utf-8') as f:
    w=csv.writer(f);w.writerow(['Trait','Implementation','Effect targets','Consumer source','Authored effect']);w.writerows(rows)
result={'status':'PASS','authored_traits':len(rows),'storage_recipes':7,'loot_items':len(lootitems),'journal_topics':len(guides),'runtime_tested':False}
(out/'audit.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result))
