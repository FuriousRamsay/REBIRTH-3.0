from pathlib import Path
import re,sys,csv,xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[2]; checks=[]
def ck(n,c): checks.append((n,bool(c)))
def text(p): return (R/p).read_text(encoding='utf-8')
# XML
xml=list((R/'Config').rglob('*.xml')); bad=[]
for p in xml:
 try: ET.parse(p)
 except Exception as e: bad.append((p,e))
ck('all Config XML parses',not bad)
prog=ET.parse(R/'Config/_Survivor/progression.xml').getroot(); skills={x.get('id'):x for x in prog.find('skills')}; know={x.get('id') for x in prog.find('knowledge')}
ck('45+ skills (later approved normal Skills allowed)',len(skills)>=45); ck('rage advanced 0..100',skills.get('skill.rage') is not None and skills['skill.rage'].get('advanced')=='true' and skills['skill.rage'].get('min')=='0' and skills['skill.rage'].get('max')=='100')
for k in ['knowledge.rage.basic','knowledge.rage.controlled','knowledge.rage.offensive','knowledge.rage.blood']: ck(k,k in know)
adv=ET.parse(R/'Config/_Survivor/advanced_disciplines.xml').getroot(); ck('advanced schema >=7',int(adv.get('schema_version','0'))>=7); bers=adv.find("discipline[@id='discipline.berserker']"); ck('berserker exists',bers is not None)
req={(x.get('kind'),x.get('id'),x.get('minimum')) for x in bers.findall('requirement')} if bers is not None else set(); ck('breadth gate',('melee_breadth','melee.families','6') in req); ck('depth gate',('melee_depth','melee.families','3') in req); ck('trial live',('trial','trial.berserker.initiation',None) in req and not any(x.get('planned')=='true' for x in bers.findall('requirement')))
rels={x.get('node_id') for x in bers.findall('relation')}; melee={'skill.spears','skill.clubs','skill.swords','skill.axes','skill.batons','skill.hammers','skill.knives','skill.scythes','skill.knuckles','skill.unarmed'}; ck('all ten melee relations',melee<=rels); ck('rage relation','skill.rage' in rels); ck('no generic melee','skill.melee' not in rels)
cap=ET.parse(R/'Config/_Survivor/capabilities.xml').getroot().find("capability[@target_id='rebirthRageCapsule']"); cskills={(x.get('id'),x.get('minimum')) for x in cap.findall('./requires_all/skill')} if cap is not None else set(); ck('capsule chemistry+medicine only',cap is not None and cskills=={('skill.chemistry','40'),('skill.medicine','30')} and not cap.findall('./requires_all/knowledge'))
ck('capsule recipe', 'recipe name="rebirthRageCapsule"' in text('Config/_Survivor/recipes.xml')); ck('capsule item action','ItemActionUseRageCapsuleRebirth, RebirthUtils' in text('Config/_Survivor/items.xml'))
ids=text('Scripts/Survivor/Domain/RebirthSurvivorIds.cs'); ck('rage ids',all(x in ids for x in ['SkillRage','KnowledgeRageBasic','KnowledgeRageControlled','KnowledgeRageOffensive','KnowledgeRageBlood','TrialBerserkerInitiation']))
model=text('Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs'); ck('world schema 13+ (later migrations allowed)', bool(__import__('re').search(r'CurrentSchemaVersion\s*=\s*(1[3-9]|[2-9][0-9])',model))); ck('trial hits persistent field','BerserkerInitiationMeleeHits' in model)
mig=text('Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs'); ck('12->13 registered','Register(12, Migrate12To13)' in mig); ck('rage migration zero','rage=0;berserkerAutoGrant=false;berserkerMeleeHits=0' in mig)
svc=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthRageService.cs'); ck('ten melee family source','GetMeleeSkillIds()' in svc); ck('no retroactive first-hit xp','triggering hit starts Rage but never earns retroactive Rage XP' in svc); ck('energy repository integration','RebirthMetabolismStateRepository.GetOrCreate' in svc and 'm.Energy' in svc); ck('no food water drain','Food' not in svc and 'Water' not in svc); ck('bounded target awards','TargetRepeatSeconds' in svc and 'TargetAwards' in svc and 'AwardMinimum' in svc and 'AwardMaximum' in svc); ck('knowledge tier thresholds',all(x in svc for x in ['v<25f','v<50f','v<80f','KnowledgeRageControlled','KnowledgeRageOffensive','KnowledgeRageBlood']))
router=text('Scripts/Survivor/Progression/RebirthSkillEventRouter.cs'); ck('melee combat hook','RebirthRageService.OnMeaningfulMeleeDamage' in router and 'IsMeleeSkill(skillId)' in router)
weapon=text('Scripts/Survivor/Progression/RebirthWeaponFamilySkillService.cs'); ck('rage modifies existing melee handling only','GetStaminaDelta(player)' in weapon and 'GetSpeedDelta(player)' in weapon)
auth=text('Scripts/Survivor/Definitions/RebirthSurvivorAuthoringValidator.cs'); ck('rage no aptitude','Advanced Rage must not generate Survivor-creation Aptitudes' in auth)
net=text('Scripts/Survivor/Support/RebirthSurvivorSupportNetPackages.cs'); ck('capsule server action','UseRageCapsule = 6' in net and 'RebirthRageCapsuleService.Consume' in net)
# invariant
beast=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBeastmasterService.cs'); ck('PC002 zombie-animal hard exclusion','IsBeastmasterAnimalHardExcluded' in beast)
# localization duplicate + keys
rows=list(csv.reader((R/'Config/Localization.csv').open(encoding='utf-8-sig'))); keys=[x[0].strip() for x in rows[1:] if x and x[0].strip()]; ck('localization duplicates zero',len(keys)==len(set(keys))); ks=set(keys)
for k in ['xuiRebirthSkillRage','xuiRebirthKnowledgeRageBasic','xuiRebirthKnowledgeRageControlled','xuiRebirthKnowledgeRageOffensive','xuiRebirthKnowledgeRageBlood','rebirthRageCapsule','rebirthRageCapsuleDesc']: ck('loc '+k,k in ks)
# lexical delimiter sanity on changed/new rage C# files
for rel in ['Scripts/Survivor/Progression/AdvancedDisciplines/RebirthRageService.cs','Scripts/Survivor/Progression/AdvancedDisciplines/RebirthRageCapsuleService.cs','Scripts/Survivor/Progression/AdvancedDisciplines/ItemActionUseRageCapsuleRebirth.cs']:
 s=text(rel); ck('delimiter '+Path(rel).name,s.count('{')==s.count('}') and s.count('(')==s.count(')'))
failed=[n for n,v in checks if not v]
for n,v in checks: print(('PASS ' if v else 'FAIL ')+n)
print(f'PC010_STATIC_CHECKS={len(checks)} PASSED={len(checks)-len(failed)} FAILED={len(failed)} XML={len(xml)}')
sys.exit(1 if failed else 0)
