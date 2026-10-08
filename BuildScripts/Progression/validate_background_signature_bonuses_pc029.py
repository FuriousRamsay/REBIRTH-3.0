#!/usr/bin/env python3
from pathlib import Path
import argparse,re,xml.etree.ElementTree as ET

ap=argparse.ArgumentParser(); ap.add_argument('project_root',nargs='?'); ap.add_argument('--project-root',dest='project_root_opt'); a=ap.parse_args()
root=Path(a.project_root_opt or a.project_root or '.').resolve()
checks=[]
def ck(name,ok,detail=''):
    checks.append((name,bool(ok),detail)); print(('PASS' if ok else 'FAIL')+' | '+name+((' | '+str(detail)) if detail else ''))
def text(rel): return (root/rel).read_text(encoding='utf-8-sig',errors='ignore')
def exists(rel): return (root/rel).exists()

def xml(rel): return ET.parse(root/rel).getroot()

# Authoritative release counts.
prog=xml('Config/_Survivor/progression.xml')
skills=prog.findall('./skills/skill'); skillids=[x.get('id','') for x in skills]
knowledge=prog.findall('./knowledge/knowledge'); knowledgeids=[x.get('id','') for x in knowledge]
bgs=xml('Config/_Survivor/backgrounds.xml').findall('./background')
bonuses=xml('Config/_Survivor/background_bonuses.xml').findall('./bonus')
adv=xml('Config/_Survivor/advanced_disciplines.xml'); disciplines=adv.findall('./discipline'); paths=adv.findall('./explorer_paths/path')
ck('authoritative backgrounds=28',len(bgs)==28,len(bgs))
ck('authoritative signature bonuses=27',len(bonuses)==27,len(bonuses))
ck('Clean Slate has no Signature Bonus',not any(x.get('background_id')=='background.clean_slate' for x in bonuses))
ck('authoritative Skills=48',len(skills)==48,len(skills))
ck('authoritative Skill IDs unique',len(set(skillids))==48,len(set(skillids)))
ck('authoritative Knowledge=206',len(knowledge)==206,len(knowledge))
ck('authoritative Knowledge IDs unique',len(set(knowledgeids))==206,len(set(knowledgeids)))
ck('authoritative Advanced Disciplines=3',len(disciplines)==3,len(disciplines))
ck('authoritative discipline Explorer paths=25',len(paths)==25,len(paths))
for sid in ('skill.animal_handling','skill.black_magic','skill.rage','skill.drink_preparation','skill.trading','skill.teaching'):
    ck('release Skill '+sid,sid in skillids)
for did in ('discipline.beastmaster','discipline.witch_doctor','discipline.berserker'):
    ck('release Discipline '+did,any(x.get('id')==did for x in disciplines))

# Current migration catalogue and final reconcile.
models=text('Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs')
migration=text('Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs')
policy=text('Scripts/Survivor/Persistence/RebirthSurvivorSkillMigrationPolicy.cs')
ck('world-character schema=16','CurrentSchemaVersion = 16' in models)
ck('schema15 migration registered','Register(15, Migrate15To16);' in migration)
ck('schema15 final reconcile method','Migrate15To16' in migration)
ck('final reconcile policy id','world-character-v15-to-v16-background-signature-final-reconcile-v1' in migration)
ck('final reconcile records derived bonus ownership','signatureBonusOwnershipDerived=true' in migration)
ck('final reconcile records provenance versioning','itemWorldProvenanceVersioned=true' in migration)
ck('final reconcile uses authoritative current IDs','RebirthSurvivorSkillMigrationPolicy.GetCurrentSkillIds()' in migration[migration.find('Migrate15To16'):])
ck('final reconcile missing-only Skill helper','EnsureSkillEntryIfMissing' in migration)
helper=migration[migration.find('private static void EnsureSkillEntryIfMissing'):migration.find('private static void EnsureSkillEntry(',migration.find('private static void EnsureSkillEntryIfMissing'))]
ck('missing-only helper returns for existing entry','return;' in helper and 'StringComparison.OrdinalIgnoreCase' in helper)
ck('missing-only helper initializes neutral value','new XAttribute("value","0")' in helper)
ck('missing-only helper preserves progression by not rewriting existing nodes','SetAttributeValue("value"' not in helper)
ck('final migration does not serialize signatureBonus element','new XElement("signatureBonus"' not in migration)

m=re.search(r'CurrentIds\s*=\s*new string\[\]\s*\{(.*?)\};',policy,re.S)
current_ids=re.findall(r'"(skill\.[^"]+)"',m.group(1)) if m else []
ck('migration current Skill allowlist=48',len(current_ids)==48,len(current_ids))
ck('migration allowlist IDs unique',len(set(current_ids))==48,len(set(current_ids)))
ck('migration allowlist exactly matches progression',set(current_ids)==set(skillids),f'policy={len(set(current_ids))} progression={len(set(skillids))}')

# Registry/provenance/persistence versions and ownership.
registry=text('Scripts/Survivor/Backgrounds/RebirthBackgroundBonusRegistry.cs')
bonus_service=text('Scripts/Survivor/Backgrounds/RebirthBackgroundBonusService.cs')
prov=text('Scripts/Survivor/Provenance/RebirthItemProvenanceAdapter.cs')
met=text('Scripts/Metabolism/RebirthMetabolismModels.cs')
infra=text('Scripts/Survivor/Progression/RebirthInfrastructureWorkService.cs')
placed=text('Scripts/Survivor/Provenance/RebirthPlacedWorkmanshipService.cs')
crop=text('Scripts/AdvancedFarming/AdvancedFarmingTileEntities.cs')
net=text('Scripts/Survivor/Network/RebirthSurvivorNetworkProtocol.cs') if exists('Scripts/Survivor/Network/RebirthSurvivorNetworkProtocol.cs') else ''
ck('bonus registry schema=1','public const int SchemaVersion = 1;' in registry)
ck('item provenance version=1','public const int CurrentVersion = 1;' in prov)
ck('metabolism persistence version=9','public const int CurrentVersion = 9;' in met)
ck('infrastructure persistence version=2','PersistenceVersion = 2' in infra)
ck('placed workmanship persistence version=1','private const ushort Version = 1;' in placed)
ck('crop provenance tile version token=0xC701','0xC701' in crop)
ck('bonus ownership resolves from Background','TryGetByBackground' in bonus_service or 'GetSignatureBonus' in bonus_service)
ck('no bonus entitlement persistence field in world model','SignatureBonusId' not in models and 'SignatureBonusEntitlement' not in models)

# Stale current-count assumptions removed from active runtime validation surfaces.
author=text('Scripts/Survivor/Definitions/RebirthSurvivorAuthoringValidator.cs')
release=text('Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptance.cs')
adaccept=text('Scripts/Survivor/Debug/RebirthAdvancedDisciplinesAcceptance.cs')
vectors=text('Scripts/Survivor/Debug/RebirthSurvivorMigrationVectorHarness.cs')
ck('authoring validator derives current Skill count','GetCurrentSkillIds()' in author and 'currentSkillIds.Length' in author)
ck('authoring validator no exact 45 Skill assertion','sk.Count!=45' not in author and 'Skills.Count != 45' not in author)
ck('release acceptance derives current Skill count','GetCurrentSkillIds().Length' in release)
ck('advanced discipline acceptance derives current Skill count','GetCurrentSkillIds().Length' in adaccept)
ck('migration vectors derive current Skill count','CurrentSkillCount()' in vectors)
ck('migration vectors no stale 47/47 assertion','Skill output is not 47/47' not in vectors and '!= 47' not in vectors)
for token in ('schema-13 Trading/Drink Preparation migration','schema-14 Teaching migration','schema-15 final catalogue reconciliation'):
    ck('migration vector registered '+token,token in vectors)
for meth in ('TestSchema13TradingDrinkMigration','TestSchema14TeachingMigration','TestSchema15FinalReconcile'):
    ck('migration vector method '+meth,meth in vectors)
ck('schema15 vector preserves signed existing values','schema15 reconciliation changed existing Skill values' in vectors)
ck('schema15 vector rejects duplicate bonus entitlement','migration created duplicate Signature Bonus entitlement state' in vectors)

# Final consolidated acceptance/debug command.
accept_rel='Scripts/Survivor/Debug/RebirthBackgroundSignatureBonusesAcceptance.cs'; accept=text(accept_rel)
cmd=text('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs')
ck('consolidated acceptance class exists',exists(accept_rel))
for method in ('BuildSummary','BuildBalanceDefaults','BuildAuthorityReport','BuildMigrationReport','RunVectors'):
    ck('consolidated acceptance method '+method,re.search(r'public static string '+method+r'\s*\(',accept) is not None)
for svc in ('RebirthSkillWaveAService','RebirthRepairSignatureService','RebirthWorkmanshipSignatureService','RebirthResourceSignatureService','RebirthCombatPatrolTrackingSignatureService','RebirthFoodFarmingButcherySignatureService','RebirthDrinkPreparationSignatureService','RebirthScavengerSalvageProfileService'):
    ck('summary includes '+svc,svc in accept)
for harness in ('RebirthRepairSignatureVectorHarness','RebirthSkillWaveAVectorHarness','RebirthTeachingVectorHarness','RebirthResourceFieldSkillVectorHarness','RebirthSurvivorMigrationVectorHarness','RebirthAdvancedDisciplinesAcceptance'):
    ck('vectors include '+harness,harness in accept)
ck('acceptance summary labels source/static vs live','SOURCE_STATIC_COMPLETE_LIVE_ACCEPTANCE_REQUIRED' in accept and 'runtimeEvidenceRequired=' in accept)
ck('acceptance authority says derived entitlement','duplicate entitlement field=False' in accept)
ck('acceptance migration reports schema15To16','schema15To16=' in accept)
ck('console help exposes consolidated bonuses command','rbsurvivor bonuses [summary|balance|authority|migration|vectors]' in cmd)
for alias in ('root=="bonuses"','root=="signaturebonuses"','root=="backgroundbonuses"'):
    ck('console alias '+alias,alias in cmd)
ck('console routes consolidated command','ExecuteBackgroundBonusAcceptance(p)' in cmd)
for mode in ('mode=="balance"','mode=="authority"','mode=="migration"','mode=="vectors"'):
    ck('console mode '+mode,mode in cmd)

# Current-data UI validators and broad floors.
hud=text('BuildScripts/UI/validate_rebirth_hud_tracker.py')
ui=text('BuildScripts/UI/validate_rebirth_ui_redesign.py')
craft=text('BuildScripts/Progression/validate_crafting_progression_coverage.py')
gates=text('BuildScripts/Progression/validate_recipe_skill_gates.py')
ck('HUD validator parses progression authority','progression.xml' in hud and 'authoritative_skill_count' in hud)
ck('HUD validator no 42-row current assumption','== 42' not in hud and '>= 42' not in hud and '42 Character Progression' not in hud)
ck('UI redesign validator has no 42 fallback','authoritative_skill_count=42' not in ui and 'authoritative_skill_count = 42' not in ui)
ck('UI redesign fails unavailable Skill authority','authoritative_skill_count' in ui and ('<=0' in ui or '<= 0' in ui))
ck('crafting validator current release floor 48',('>= 48' in craft or '>=48' in craft))
ck('recipe gate validator current release floor 48',('>= 48' in gates or '>=48' in gates))

# PC028 UI/atlas state retained.
ig=text('Config/XUi_InGame/windows.xml'); char=text('Scripts/Survivor/UI/XUiC_RebirthSurvivorCharacter.cs')
ck('Character Skill capacity remains 48','private const int SkillRows = 48;' in char)
ck('Character Progression Skill capacity remains 48','private const int ProgressionSkillRows = 48;' in char)
for prefix in ('survivorSkillRow','survivorProgressionSkillRow'):
    rows=set(int(x) for x in re.findall(r'name="'+prefix+r'(\d+)"',ig)); ck(prefix+' 0..47',rows==set(range(48)),len(rows))
ck('Character Skill scrollbar retained','name="survivorSkillsContent"' in ig and '<defaultscrollbar/>' in ig)
ck('Character Progression scrollbar retained','name="survivorProgressionSkillsContent"' in ig and '<defaultscrollbar/>' in ig)
for sid in skillids:
    icon='UIAtlases/RebirthSurvivorIcons/rb_skill_'+sid[6:].replace('.','_').replace('-','_')+'.png'
    ck('atlas icon '+sid,exists(icon),icon)

# Advanced Discipline release state retained.
gdef=text('Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphDefinition.cs')
greg=text('Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphRegistry.cs')
for token in ('Discipline','RequiresDiscipline','UnlocksDiscipline'):
    ck('graph type '+token,token in gdef)
ck('Explorer background description consumes Signature Bonus','SignatureBonusName(background.Id)' in greg)
ck('Offensive Rage retains Rage Capsule relation',any(x.get('id')=='rage_offensive' and x.get('related_recipe')=='rebirthRageCapsule' for x in paths))
ck('Beastmaster zombie-animal exclusion retained',adv.find('./beastmaster_hard_exclusions') is not None)
ck('summoning remains fail-closed','fail_closed_when_entity_asset_missing' in text('Config/_Survivor/advanced_disciplines.xml'))

# Final required documentation assets (written by PC029 before final validation).
for rel in (
'_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC029_CHUNK_P_SOURCE_AUDIT_20260902.md',
'_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC029_CHUNK_P_BALANCE_DEFAULTS_20260902.md',
'_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC029_CHUNK_P_LIVE_ACCEPTANCE_TEST_GUIDE_20260902.md',
'_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC029_CHUNK_P_AUTHORITATIVE_COUNTS_20260902.txt',
'_Documentation/ProjectChanges/REBIRTH_3_0_PROJECT_CHANGE_BACKGROUND_SIGNATURE_BONUSES_PC029_CHUNK_P_MIGRATION_DEBUG_VALIDATION_RELEASE_ACCEPTANCE_20260902.md',
'_Documentation/ProjectChanges/REBIRTH_3_0_PROJECT_CHANGE_BACKGROUND_SIGNATURE_BONUSES_PC029_CHUNK_P_VALIDATION_20260902.txt'):
    ck('PC029 documentation '+Path(rel).name,exists(rel))

# Whole XML tree parses.
xml_files=list(root.rglob('*.xml')); bad=[]
for f in xml_files:
    try: ET.parse(f)
    except Exception as e: bad.append((str(f.relative_to(root)),str(e)))
ck('all XML parse',not bad,('XML='+str(len(xml_files)) if not bad else bad[:3]))

passed=sum(1 for _,ok,_ in checks if ok); failed=len(checks)-passed
print(f'PC029_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={len(xml_files)}')
raise SystemExit(0 if failed==0 else 1)
