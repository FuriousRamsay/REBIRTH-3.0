from pathlib import Path
import re,sys,csv,xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[2]; checks=[]
def ck(n,c): checks.append((n,bool(c)))
def text(p): return (R/p).read_text(encoding='utf-8')
xml=list((R/'Config').rglob('*.xml'));bad=[]
for p in xml:
 try:ET.parse(p)
 except Exception as e:bad.append((p,e))
ck('all Config XML parses',not bad)
adv=ET.parse(R/'Config/_Survivor/advanced_disciplines.xml').getroot();ck('advanced schema 8',adv.get('schema_version')=='8');sp=adv.find('special_panthers');ck('special panther ledger exists',sp is not None)
routes={x.get('id'):x for x in sp.findall('route')} if sp is not None else {};ck('exactly two routes',set(routes)=={'witch_doctor','berserker'});ck('AH minimum 25',sp is not None and sp.get('minimum_animal_handling')=='25');ck('capacity cost 2',sp is not None and sp.get('capacity_cost')=='2');ck('one each',sp is not None and sp.get('maximum_owned_per_route')=='1')
zanimals=adv.find('./black_magic_target_classification/zombie_animals');ck('infected binding AH 25',zanimals is not None and zanimals.get('beastmaster_tame')=='false' and zanimals.get('binding_animal_handling_minimum')=='25')
wd=routes.get('witch_doctor');br=routes.get('berserker')
ck('Horror exact identity',wd is not None and wd.get('entity_class')=='FuriousRamsayNPCHorrorPanther');ck('Rage exact identity',br is not None and br.get('entity_class')=='FuriousRamsayNPCRagePanther')
ck('both Panther Handling',wd is not None and br is not None and wd.get('animal_knowledge')==br.get('animal_knowledge')=='knowledge.animal_handling.panther_handling')
ck('WD cross discipline',wd is not None and wd.get('discipline')=='discipline.witch_doctor' and wd.get('discipline_skill')=='skill.black_magic' and wd.get('discipline_skill_minimum')=='50' and wd.get('discipline_knowledge')=='knowledge.black_magic.panther_binding')
ck('Berserker cross discipline',br is not None and br.get('discipline')=='discipline.berserker' and br.get('discipline_skill')=='skill.rage' and br.get('discipline_skill_minimum')=='50' and br.get('discipline_knowledge')=='knowledge.rage.controlled')
# relations no background
for did,kids in [('discipline.witch_doctor',{'skill.animal_handling','knowledge.animal_handling.panther_handling','knowledge.black_magic.panther_binding'}),('discipline.berserker',{'skill.animal_handling','knowledge.animal_handling.panther_handling','knowledge.rage.controlled'})]:
 d=adv.find("discipline[@id='%s']"%did); rel={x.get('node_id') for x in d.findall('relation')} if d is not None else set();ck(did+' cross links',kids<=rel);ck(did+' no background relation',not any((x or '').startswith('background.') for x in rel))
prog=ET.parse(R/'Config/_Survivor/progression.xml').getroot();know={x.get('id'):x for x in prog.find('knowledge')};ck('Panther Handling knowledge', 'knowledge.animal_handling.panther_handling' in know);ck('Panther Binding knowledge','knowledge.black_magic.panther_binding' in know);ck('Chunk J acquisition deferred',all('Chunk J' in (know[k].get('explorer_status') or '') for k in ['knowledge.animal_handling.panther_handling','knowledge.black_magic.panther_binding']))
# entities
tamed=text('Config/_NPC/tamed_wild_animals.xml');ck('Horror extends current mountain lion','name="FuriousRamsayNPCHorrorPanther" extends="RebirthTamedAnimalMountainLion"' in tamed);ck('Rage extends current mountain lion','name="FuriousRamsayNPCRagePanther" extends="RebirthTamedAnimalMountainLion"' in tamed);ck('no broken Panther002/003 mesh refs','Panther002_FR' not in tamed and 'Panther003_FR' not in tamed)
buffs=text('Config/_Survivor/special_panthers.xml');ck('Horror assist buff','RebirthHorrorPantherSupport' in buffs and 'RebirthSpecialPantherAssist, RebirthUtils' in buffs);ck('Rage monitor/active/cooldown',all(x in buffs for x in ['RebirthRagePantherMonitor','RebirthRagePantherActive','RebirthRagePantherCooldown']))
svc=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthSpecialPantherService.cs');ck('Rebirth mode gate','RebirthSurvivorMode.IsEnabledForCurrentWorld()' in svc);ck('server authority gate','world.IsRemote()' in svc and 'RebirthWorldCharacterRepository.IsServerAuthority' in svc);ck('discipline gate','HasDiscipline(player,route.DisciplineId)' in svc);ck('AH gate','minimumAnimalHandling' in svc and 'RebirthAnimalHandlingService.GetPlayerSkill' in svc);ck('knowledge gates','HasKnowledge(player,route.AnimalKnowledgeId)' in svc and 'HasKnowledge(player,route.DisciplineKnowledgeId)' in svc);ck('route ownership cap','maximumOwnedPerRoute' in svc and 'CountOwnedRoute' in svc);ck('Animal Capacity commit gate','CountAnimalCapacity' in svc and 'used+capacityCost>cap' in svc);ck('global safety gate','TryGetOwnershipCounts' in svc and 'total>=globalCap' in svc);ck('persistent companion reuse','EntityRebirthDogCompanion' in svc and 'SetRebirthOwner' in svc and 'SetRebirthOrder' in svc and 'RebirthDogStateService.Ensure' in svc);ck('special panther animal capacity record','d.IsTamedWild=true' in svc and 'd.AnimalCapacityCost=capacityCost' in svc and 'd.SpeciesCategory="special_panther"' in svc);ck('one exact route breed','d.BreedId="special-panther:"+route.Id' in svc)
black=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBlackMagicService.cs');ck('Horror delegates to modern Black Magic','TrySpecialPantherDominate' in black and 'return TryDominate(world,player,target,out reason);' in black);ck('special wrapper requires Witch Doctor','TrySpecialPantherDominate' in black and 'HasWitchDoctor(player)' in black)
ck('assist invokes wrapper','RebirthBlackMagicService.TrySpecialPantherDominate' in svc);ck('assist bounded chance','AssistChance=.20f' in svc and 'now-last<5f' in svc)
# PC002 runtime invariant
beast=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBeastmasterService.cs');ck('PC002 Beastmaster hard exclusion intact','IsBeastmasterAnimalHardExcluded' in beast and 'zombie class leaked into Beastmaster species' in beast);ck('special service has no Beastmaster acquisition requirement','DisciplineBeastmaster' not in svc and 'HasBeastmaster' not in svc)
bound=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBoundUndeadService.cs');ck('infected animal binding AH cross gate','ValidateInfectedAnimalBindingHandling(target, player, out reason)' in bound and 'HasBeastmaster(' not in bound and 'DisciplineBeastmaster' not in bound)
# installer/debug
inst=text('Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs');ck('service installed','RebirthSpecialPantherService.Install()' in inst)
con=text('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs');ck('consolidated panther debug','rbsurvivor panther [status|vectors|deploy' in con and 'ExecuteSpecialPanther' in con);ck('debug deploy gated',"deploy rejected: enable 'rbsurvivor debug on' first" in con)
# loc
rows=list(csv.reader((R/'Config/Localization.csv').open(encoding='utf-8-sig')));keys=[x[0].strip() for x in rows[1:] if x and x[0].strip()];ck('localization duplicates zero',len(keys)==len(set(keys)));ks=set(keys);ck('Panther Handling localization','xuiRebirthKnowledgePantherHandling' in ks);ck('Panther Binding localization','xuiRebirthKnowledgePantherBinding' in ks)
# delimiters
for rel in ['Scripts/Survivor/Progression/AdvancedDisciplines/RebirthSpecialPantherService.cs','Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBlackMagicService.cs','Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs']:
 s=text(rel);ck('delimiter '+Path(rel).name,s.count('{')==s.count('}') and s.count('(')==s.count(')'))
failed=[n for n,v in checks if not v]
for n,v in checks:print(('PASS ' if v else 'FAIL ')+n)
print(f'PC011_STATIC_CHECKS={len(checks)} PASSED={len(checks)-len(failed)} FAILED={len(failed)} XML={len(xml)}')
sys.exit(1 if failed else 0)
