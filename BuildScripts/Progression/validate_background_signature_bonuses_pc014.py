from pathlib import Path
import csv, hashlib, json, sys, xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[2]
D=R/'_Documentation/ProjectChanges'
checks=[]
def ck(name,cond,detail=''):
    checks.append((name,bool(cond),detail))
def txt(p): return (R/p).read_text(encoding='utf-8')

expected_pc014={
 'BuildScripts/Progression/validate_background_signature_bonuses_pc014.py',
 '_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC014_CHUNK_A_BASELINE_FREEZE_SHA256.txt',
 '_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC014_CHUNK_A_AUDIT_MATRIX_20260902.json',
 '_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC014_CHUNK_A_SOURCE_AUDIT_20260902.md',
 '_Documentation/ProjectChanges/REBIRTH_3_0_PROJECT_CHANGE_BACKGROUND_SIGNATURE_BONUSES_PC014_CHUNK_A_AUTHORITY_AUDIT_BASELINE_FREEZE_20260902.md',
 '_Documentation/ProjectChanges/REBIRTH_3_0_PROJECT_CHANGE_BACKGROUND_SIGNATURE_BONUSES_PC014_CHUNK_A_VALIDATION_20260902.txt',
 'CHANGED_FILE_MANIFEST_BACKGROUND_SIGNATURE_BONUSES_PC014_CHUNK_A_20260902.txt',
}
# Baseline freeze verification.
freeze=D/'REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC014_CHUNK_A_BASELINE_FREEZE_SHA256.txt'
s=freeze.read_text(encoding='utf-8')
start=s.index('BEGIN_CANONICAL_FILE_MANIFEST\n')+len('BEGIN_CANONICAL_FILE_MANIFEST\n')
end=s.index('END_CANONICAL_FILE_MANIFEST\n')
blob=s[start:end]
ck('canonical tree manifest digest',hashlib.sha256(blob.encode()).hexdigest()=='d5927a20fa6baa588c1a4019ff983157e7cb2c1a6f5d6f358ee5a8a27a9d260a')
baseline={}
for line in blob.splitlines():
    h,size,rel=line.split('\t',2); baseline[rel]=(h,int(size))
ck('frozen baseline file count',len(baseline)==4780,str(len(baseline)))
missing=[]; mismatch=[]
for rel,(h,size) in baseline.items():
    p=R/rel
    if not p.is_file(): missing.append(rel); continue
    b=p.read_bytes()
    if len(b)!=size or hashlib.sha256(b).hexdigest()!=h: mismatch.append(rel)
ck('frozen baseline files still present',not missing,str(missing[:5]))
ck('frozen baseline hashes unchanged',not mismatch,str(mismatch[:5]))
current={p.relative_to(R).as_posix() for p in R.rglob('*') if p.is_file()}
extra=current-set(baseline)
ck('only PC014 files added',extra<=expected_pc014,str(sorted(extra-expected_pc014)[:10]))
# Registry authority.
bg=ET.parse(R/'Config/_Survivor/backgrounds.xml').getroot(); bgs=[e.get('id') for e in bg.findall('background')]
ck('background schema 3',bg.get('schema_version')=='3')
ck('28 backgrounds',len(bgs)==28,str(len(bgs)))
ck('Metal Worker stable ID','background.welder_fabricator' in bgs)
ck('Park Ranger current stable ID','background.park_ranger_outdoor_guide' in bgs)
for old in ['background.doctor','background.nurse','background.security_guard','background.truck_driver','background.warehouse_worker']:
    ck('removed background absent '+old,old not in bgs)
prog=ET.parse(R/'Config/_Survivor/progression.xml').getroot(); skills=prog.find('skills'); knowledge=prog.find('knowledge')
ck('progression schema 3',prog.get('schema_version')=='3')
ck('45 current skills',skills is not None and len(list(skills))==45,str(len(list(skills)) if skills is not None else -1))
ck('206 current knowledge',knowledge is not None and len(list(knowledge))==206,str(len(list(knowledge)) if knowledge is not None else -1))
# Audit matrix completeness and locked Chunk A boundaries.
m=json.loads((D/'REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC014_CHUNK_A_AUDIT_MATRIX_20260902.json').read_text(encoding='utf-8'))
ck('parent project change',m.get('project_change')=='REBIRTH-3.0-BACKGROUND-SIGNATURE-BONUSES-001')
ck('Chunk A runtime disabled',m.get('gates',{}).get('no_runtime_bonus_enabled_in_chunk_a') is True)
ck('Rebirth-only contract',m.get('gates',{}).get('rebirth_only') is True and m.get('gates',{}).get('base_game_native_behavior_preserved') is True)
ck('server authority contract',m.get('gates',{}).get('server_authority_required') is True)
sysids={x['id'] for x in m['systems']}
required_systems={'background_registry_profile_persistence','skill_registry','knowledge_registry','repair_degradation_runtime','loot_generation','harvest_salvage','crop_growth','power_networks','trap_placement_provenance','trader_reward_ui','quest_rewards','item_custom_data_serialization'}
ck('12 required audit systems',sysids==required_systems,str(sorted(required_systems-sysids)))
# 2.6 drink classifications.
drinkids={x['id'] for x in m['drinks_2_6']}; classes={x['migration_class'] for x in m['drinks_2_6']}
required_drinks={'drinkJarRiverWater','drinkJarBoiledWater','drinkJarPureMineralWater','drinkJarYuccaJuice','drinkJarGoldenRodTea','drinkJarHoneyTea','drinkJarRedTea','drinkYuccaJuiceSmoothie','drinkCanMegaCrush','drinkJarCoffee','drinkJarBlackStrapCoffee','drinkJarBeer','drinkJarGrandpasMoonshine','drinkJarGrandpasAwesomeSauce','drinkJarGrandpasLearningElixir','drinkJarGrandpasForgettingElixir','drinkJarFullRespecAdmin','FuriousRamsayMindControlDrink','FuriousRamsayMindControlPotion'}
ck('2.6 drink audit complete',drinkids==required_drinks,str(sorted(required_drinks-drinkids)))
ck('migration classes constrained',classes<={'native/current','port','adapt','obsolete/duplicate'},str(classes))
bydrink={x['id']:x for x in m['drinks_2_6']}
ck('legacy mind control drink obsolete',bydrink['FuriousRamsayMindControlDrink']['migration_class']=='obsolete/duplicate')
ck('legacy mind control potion obsolete',bydrink['FuriousRamsayMindControlPotion']['migration_class']=='obsolete/duplicate')
ck('Blackstrap uncertainty recorded','binding_absent' in bydrink['drinkJarBlackStrapCoffee']['status'])
ck('Mega Crush uncertainty recorded','binding_absent' in bydrink['drinkCanMegaCrush']['status'])
# Icon route + collision audit.
icons=m['icons']; keys=[x['sprite_key'] for x in icons]
ck('27 bonus icon reservations',len(icons)==27 and len(set(keys))==27,str(len(icons)))
ck('Clean Slate has no bonus icon',all(x['background_id']!='background.clean_slate' for x in icons))
ck('all icon routes Survivor atlas',all(x['route']=='UIAtlases/RebirthSurvivorIcons/' for x in icons))
settings=ET.parse(R/'UIAtlases/RebirthSurvivorIcons/settings.xml').getroot(); existing={e.get('name') for e in settings.iter('sprite') if e.get('name')}
pngs={p.stem for p in (R/'UIAtlases/RebirthSurvivorIcons').glob('*.png')}
ck('reserved sprite keys not registered',not (set(keys)&existing),str(sorted(set(keys)&existing)))
ck('reserved sprite PNGs not present',not (set(keys)&pngs),str(sorted(set(keys)&pngs)))
# Supersession exact set.
supids={x['id'] for x in m['supersession']}
ck('five supersession contracts',supids=={'primitive_armor_tailoring_gate','bartender_progression','salesperson_progression','teacher_teaching','advanced_disciplines_ui'},str(supids))
# Current source constraints explicitly present.
met=txt('Config/_Metabolism/items.xml')
for item in ['drinkJarBeer','drinkJarCoffee','drinkJarGoldenRodTea','drinkJarGrandpasAwesomeSauce','drinkJarGrandpasLearningElixir','drinkJarGrandpasMoonshine','drinkJarHoneyTea','drinkJarPureMineralWater','drinkJarRedTea','drinkJarRiverWater','drinkJarYuccaJuice','drinkYuccaJuiceSmoothie']:
    ck('metabolism route '+item,("item[@name='"+item+"']") in met and 'ItemActionConsumeMetabolismRebirth' in met)
ck('Blackstrap not silently assumed metabolism-bound',"item[@name='drinkJarBlackStrapCoffee']" not in met)
ck('Mega Crush not silently assumed metabolism-bound',"item[@name='drinkCanMegaCrush']" not in met)
ms=txt('Scripts/Metabolism/RebirthMetabolismService.cs')
ck('preserved item-use effect bridge source-audited','FirePreservedUseEffects' in ms)
ck('item metadata reused by metabolism','SetMetadata(VolumeKey' in txt('Scripts/Metabolism/Consumption/RebirthConsumableResolver.cs'))
ck('existing harvest architecture retained',(R/'Scripts/Features/RebirthAttackHarvestReplacementService.cs').is_file() and (R/'Scripts/Features/RebirthHarvestSystemsPolicy.cs').is_file())
ck('existing Advanced Farming retained',(R/'Scripts/AdvancedFarming/BlockPlantGrowingRebirth.cs').is_file())
# All project XML + localization duplicate-free.
xmls=list(R.rglob('*.xml')); bad=[]
for p in xmls:
    try: ET.parse(p)
    except Exception as e: bad.append((str(p.relative_to(R)),str(e)))
ck('all project XML parses',not bad,str(bad[:3]))
rows=list(csv.reader((R/'Config/Localization.csv').open(encoding='utf-8-sig'))); loc=[x[0].strip() for x in rows[1:] if x and x[0].strip()]
ck('localization duplicates zero',len(loc)==len(set(loc)))
# No gameplay/config source introduced by Chunk A additions.
for rel in extra:
    ck('Chunk A addition non-runtime '+rel,rel.startswith('_Documentation/ProjectChanges/') or rel.startswith('BuildScripts/Progression/') or rel.startswith('CHANGED_FILE_MANIFEST_'))
failed=[x for x in checks if not x[1]]
for n,v,d in checks: print(('PASS ' if v else 'FAIL ')+n+((' :: '+d) if d and not v else ''))
print(f'PC014_STATIC_CHECKS={len(checks)} PASSED={len(checks)-len(failed)} FAILED={len(failed)} BASELINE_FILES={len(baseline)} XML={len(xmls)}')
sys.exit(1 if failed else 0)
