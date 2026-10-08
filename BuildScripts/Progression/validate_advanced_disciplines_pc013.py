from pathlib import Path
import csv, re, sys, xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[2]
checks=[]
def ck(name, cond, detail=''):
    checks.append((name,bool(cond),detail))
def text(rel): return (R/rel).read_text(encoding='utf-8')
# Parse every project XML, not only Config.
xmls=list(R.rglob('*.xml')); bad=[]
for p in xmls:
    try: ET.parse(p)
    except Exception as e: bad.append((str(p.relative_to(R)),str(e)))
ck('all project XML parses',not bad,str(bad[:3]))
adv=ET.parse(R/'Config/_Survivor/advanced_disciplines.xml').getroot()
prog=ET.parse(R/'Config/_Survivor/progression.xml').getroot()
skills={e.get('id'):e for e in prog.find('skills')}; knowledge={e.get('id'):e for e in prog.find('knowledge')}
ck('advanced disciplines schema current',adv.get('schema_version')=='8')
ck('three advanced disciplines',{e.get('id') for e in adv.findall('discipline')}=={'discipline.beastmaster','discipline.witch_doctor','discipline.berserker'})
ck('45+ skills (later approved normal Skills allowed)',len(skills)>=45,'actual='+str(len(skills)))
ck('advanced skills present',skills.get('skill.black_magic') is not None and skills.get('skill.rage') is not None)
# PC002 zombie animal boundary.
ex=adv.find('beastmaster_hard_exclusions'); exclasses={e.get('id') for e in ex.findall('entity_class')} if ex is not None else set(); extags={e.get('id') for e in ex.findall('tag')} if ex is not None else set()
ck('zombie dog excluded from Beastmaster','animalZombieDog' in exclasses)
ck('zombie bear excluded from Beastmaster','animalZombieBear' in exclasses)
ck('zombie/infected tags excluded',{'zombie','infected','zombieAnimal'}<=extags)
z=adv.find('./black_magic_target_classification/zombie_animals')
ck('zombie animals Black Magic only',z is not None and z.get('beastmaster_tame')=='false' and z.get('wild_affinity')=='false')
ck('infected handling cross gate',z is not None and z.get('binding_animal_handling_minimum')=='25' and z.get('binding_knowledge')=='knowledge.animal_handling.infected_behavior')
ck('infected behavior knowledge exists','knowledge.animal_handling.infected_behavior' in knowledge)
# Rage is fully data driven.
r=adv.find('./tunables/rage')
required_attrs=['breadth_level','breadth_families','depth_level','depth_families','trial_melee_hits','activation_energy','offensive_energy_extra','blood_energy_extra','duration_seconds','target_repeat_seconds','award_per_100_damage','award_min','award_max','basic_stamina_delta','controlled_stamina_delta','offensive_stamina_delta','blood_stamina_delta','basic_speed_delta','controlled_speed_delta','offensive_speed_delta','blood_speed_delta','blood_requires_blood_moon']
for a in required_attrs: ck('rage tunable '+a,r is not None and r.get(a) is not None)
ck('blood rage authored Blood Moon only',r is not None and r.get('blood_requires_blood_moon')=='true')
rage=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthRageService.cs')
ck('Rage loads authoring XML','LoadTunables();' in rage and 'XDocument.Load(RebirthAdvancedDisciplineRegistry.SourcePath)' in rage)
ck('Rage balance fields not const','public const int TrialHitsRequired' not in rage and 'public const float BreadthLevel' not in rage)
ck('Blood Rage source-audited API gate','SkyManager.IsBloodMoonVisible()' in rage and 'Blood Rage can only be activated during an active Blood Moon.' in rage)
ck('exact tier resolver','TryResolveTier' in rage and 'Unknown Rage tier' in rage)
ck('controlled study+skill gate','v<25f' in rage and 'KnowledgeRageControlled' in rage)
ck('offensive study+skill gate','v<50f' in rage and 'KnowledgeRageOffensive' in rage)
ck('blood study+skill gate','v<80f' in rage and 'KnowledgeRageBlood' in rage)
ck('Energy only activation','RebirthMetabolismStateRepository.GetOrCreate' in rage and 'm.Energy' in rage and 'Food' not in rage and 'Water' not in rage)
ck('first hit nonretroactive','triggering hit starts Rage but never earns retroactive Rage XP' in rage)
ck('bounded Rage LBD','AwardMinimum' in rage and 'AwardMaximum' in rage and 'TargetRepeatSeconds' in rage)
ck('Rage Base isolation','Rage is available only when Character Progression is Rebirth.' in rage)
# Rage Capsule authority/consumption ordering.
caps=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthRageCapsuleService.cs')
ck('capsule server authority','RebirthWorldCharacterRepository.IsServerAuthority' in caps)
ck('capsule offensive exact activation','TryActivate(p,"offensive",out reason)' in caps)
ck('capsule consumes after activation',caps.find('TryActivate(p,"offensive",out reason)') < caps.find('stack.count--'))
# Special Panthers and Black Magic authority.
sp=adv.find('special_panthers'); routes={e.get('id'):e for e in sp.findall('route')} if sp is not None else {}
ck('two special panther routes',set(routes)=={'witch_doctor','berserker'})
ck('exact Horror identity',routes.get('witch_doctor') is not None and routes['witch_doctor'].get('entity_class')=='FuriousRamsayNPCHorrorPanther')
ck('exact Rage Panther identity',routes.get('berserker') is not None and routes['berserker'].get('entity_class')=='FuriousRamsayNPCRagePanther')
spc=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthSpecialPantherService.cs')
for token,name in [('RebirthSurvivorMode.IsEnabledForCurrentWorld()','panther Base isolation'),('RebirthWorldCharacterRepository.IsServerAuthority','panther server authority'),('HasDiscipline(player,route.DisciplineId)','panther discipline authority'),('HasKnowledge(player,route.AnimalKnowledgeId)','panther Animal Knowledge gate'),('HasKnowledge(player,route.DisciplineKnowledgeId)','panther discipline Knowledge gate'),('CountAnimalCapacity','panther Animal Capacity'),('TryGetOwnershipCounts','panther global capacity')]: ck(name,token in spc)
ck('infected binding Knowledge server check','infectedAnimalHandlingKnowledgeId' in spc and 'Beastmaster still cannot tame infected animals.' in spc)
black=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBlackMagicService.cs'); bound=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBoundUndeadService.cs'); beast=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBeastmasterService.cs')
ck('Horror Panther delegates Black Magic authority','TrySpecialPantherDominate' in black and 'return TryDominate(world,player,target,out reason);' in black)
ck('infected binding route calls cross gate','ValidateInfectedAnimalBindingHandling(target, player, out reason)' in bound)
ck('Beastmaster hard exclusion runtime','IsBeastmasterAnimalHardExcluded' in beast)
# Explorer/Knowledge/Literature carry through K.
paths=adv.find('explorer_paths'); ck('25 Explorer technique paths',paths is not None and len(paths.findall('path'))==25)
for e in paths.findall('path') if paths is not None else []:
    for attr, bag in [('skill',skills),('secondary_skill',skills),('knowledge',knowledge),('secondary_knowledge',knowledge)]:
        v=e.get(attr)
        if v: ck('Explorer ref '+e.get('id','')+' '+attr,v in bag,v)
lit=ET.parse(R/'Config/_Survivor/literature.xml').getroot(); advanced_lit=[e for e in lit.findall('item') if e.get('id','').startswith(('rebirthGuideInfectedAnimalBehavior','rebirthGuidePantherHandling','rebirthManualBlackMagicPantherBinding','rebirthManualMindControl','rebirthManualUndeadConditioning','rebirthManualBindingRituals','rebirthManualFeralBinding','rebirthManualRadiatedBinding','rebirthManualChargedBinding','rebirthManualInfernalBinding','rebirthManualLesserSummoning','rebirthManualGreaterSummoning','rebirthManualArmyOfTheDead','rebirthManualControlledRage','rebirthManualOffensiveRage','rebirthManualBloodRage'))]
ck('17 advanced literature titles',len(advanced_lit)==17)
items=text('Config/_Survivor/items.xml'); loot=text('Config/_Survivor/loot.xml'); traders=text('Config/_Survivor/traders.xml'); dist=text('Config/_Survivor/literature_distribution.xml')
for e in advanced_lit:
    iid=e.get('id');
    for label,blob in [('item',items),('loot',loot),('trader',traders),('distribution',dist)]: ck(iid+' '+label,iid in blob)
# Persistence/migration/network.
model=text('Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs'); mig=text('Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs'); mh=text('Scripts/Survivor/Debug/RebirthSurvivorMigrationVectorHarness.cs')
ck('world schema 13+ (later migrations allowed)', bool(re.search(r'CurrentSchemaVersion\s*=\s*(1[3-9]|[2-9][0-9])',model)))
ck('12 to 13 migration registered','Register(12, Migrate12To13)' in mig)
ck('12 to 13 neutral Rage audit','rage=0;berserkerAutoGrant=false;berserkerMeleeHits=0' in mig)
ck('dedicated 12 to 13 migration vector','TestSchema12RageMigration' in mh and 'Berserker auto-granted during migration' in mh)
netmodel=text('Scripts/Survivor/Network/RebirthSurvivorNetworkModels.cs'); codec=text('Scripts/Survivor/Network/RebirthSurvivorNetworkCodec.cs')
ck('network protocol 7','public const int Version = 7;' in netmodel)
ck('trial/accomplishment owner state','AccomplishmentIds' in netmodel and 'CompletedTrialIds' in netmodel)
ck('trial/accomplishment codec','AccomplishmentIds' in codec and 'CompletedTrialIds' in codec)
# Consolidated debug + acceptance.
console=text('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs'); acc=text('Scripts/Survivor/Debug/RebirthAdvancedDisciplinesAcceptance.cs')
ck('Rage debug mutation gated',"[REBIRTH Rage] activate rejected: enable 'rbsurvivor debug on' first." in console)
ck('Panther debug mutation gated',"deploy rejected: enable 'rbsurvivor debug on' first" in console)
ck('consolidated acceptance command','disciplines acceptance' in console and 'RebirthAdvancedDisciplinesAcceptance' in console)
ck('acceptance never self approves','releaseApproved=False' in acc and 'static diagnostics never self-approve' in acc)
ck('acceptance roles include SP P2P dedicated','single_player_host,p2p_host,p2p_client,dedicated_client' in acc)
ck('acceptance covers Base isolation','base_game_isolation' in acc)
ck('release acceptance Skill count remains at least PC013 baseline',('GetCurrentSkillIds().Length' in text('Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptance.cs')) or (re.search(r'expected=(\d+)',text('Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptance.cs')) is not None and int(re.search(r'expected=(\d+)',text('Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptance.cs')).group(1))>=45))
# Historical validators no longer stale.
pc010=text('BuildScripts/Progression/validate_advanced_disciplines_pc010.py'); pc011=text('BuildScripts/Progression/validate_advanced_disciplines_pc011.py')
ck('PC010 validator accepts later schema','advanced schema >=7' in pc010)
ck('PC011 validator dynamic root',"Path(__file__).resolve().parents[2]" in pc011 and '/mnt/data/advdisc_i_work' not in pc011)
# Localization duplicate-free.
rows=list(csv.reader((R/'Config/Localization.csv').open(encoding='utf-8-sig'))); keys=[x[0].strip() for x in rows[1:] if x and x[0].strip()]
ck('localization duplicates zero',len(keys)==len(set(keys)))
# New/changed C# delimiter sanity.
for rel in ['Scripts/Survivor/Progression/AdvancedDisciplines/RebirthRageService.cs','Scripts/Survivor/Debug/RebirthAdvancedDisciplinesAcceptance.cs','Scripts/Survivor/Debug/RebirthSurvivorMigrationVectorHarness.cs','Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs']:
    s=text(rel); ck('delimiter '+Path(rel).name,s.count('{')==s.count('}') and s.count('(')==s.count(')'))
failed=[x for x in checks if not x[1]]
for n,v,d in checks:
    print(('PASS ' if v else 'FAIL ')+n+((' :: '+d) if d and not v else ''))
print(f'PC013_STATIC_CHECKS={len(checks)} PASSED={len(checks)-len(failed)} FAILED={len(failed)} XML={len(xmls)}')
sys.exit(1 if failed else 0)
