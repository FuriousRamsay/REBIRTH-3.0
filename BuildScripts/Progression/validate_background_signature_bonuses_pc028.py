#!/usr/bin/env python3
from pathlib import Path
import argparse,re,xml.etree.ElementTree as ET

ap=argparse.ArgumentParser();ap.add_argument('project_root',nargs='?');ap.add_argument('--project-root',dest='project_root_opt');a=ap.parse_args()
root=Path(a.project_root_opt or a.project_root or '.').resolve()
checks=[]
def ck(name,ok,detail=''):
    checks.append((name,bool(ok),detail));print(('PASS' if ok else 'FAIL')+' | '+name+((' | '+str(detail)) if detail else ''))
def text(rel): return (root/rel).read_text(encoding='utf-8',errors='ignore')
def exists(rel): return (root/rel).exists()

prog=ET.parse(root/'Config/_Survivor/progression.xml').getroot()
skills=prog.findall('./skills/skill'); skillids={x.get('id','') for x in skills}
knowledge={x.get('id',''):x for x in prog.findall('./knowledge/knowledge')}
ck('authoritative Skill count 48',len(skills)==48,len(skills))
for sid in ('skill.animal_handling','skill.drink_preparation','skill.trading','skill.teaching'):
    row=next((x for x in skills if x.get('id')==sid),None)
    ck('Skill exists '+sid,row is not None)
    ck('Skill LBD source '+sid,row is not None and bool(row.get('lbd_source','').strip()))
    ck('Skill source key '+sid,row is not None and bool(row.get('source_key','').strip()))
    icon='rb_skill_'+sid[6:].replace('.','_').replace('-','_')+'.png'
    ck('Skill icon '+sid,exists('UIAtlases/RebirthSurvivorIcons/'+icon),icon)
ck('Rage Skill icon exists',exists('UIAtlases/RebirthSurvivorIcons/rb_skill_rage.png'))
ck('Drink Preparation theory associates new Skill','skill.drink_preparation' in knowledge['knowledge.drink.preparation'].get('associated_skills','').split(','))
ck('Trader familiarity associates Trading','skill.trading' in knowledge['knowledge.trader.familiarity'].get('associated_skills','').split(','))
for kid in ('knowledge.animal_handling.basic_dog_training','knowledge.animal_handling.working_dog_training','knowledge.animal_handling.animal_behavior'):
    ck('Animal Handling Knowledge '+kid,kid in knowledge and 'skill.animal_handling' in knowledge[kid].get('associated_skills','').split(','))

bonus=ET.parse(root/'Config/_Survivor/background_bonuses.xml').getroot()
bonuses=bonus.findall('./bonuses/bonus') if bonus.find('./bonuses') is not None else bonus.findall('.//bonus')
ck('27 Signature Bonuses remain authoritative',len(bonuses)==27,len(bonuses))
for b in bonuses:
    ck('bonus UI metadata '+b.get('id',''),bool(b.get('background_id')) and bool(b.get('name_key')) and bool(b.get('description_key')) and bool(b.get('icon_key')))

ui=text('Scripts/Survivor/UI/RebirthSurvivorUiText.cs')
for token in ('TryGetSignatureBonus','SignatureBonusName','SignatureBonusDescription','SignatureBonusIcon','BuildSignatureBonusSummary','BuildSkillRelationshipHint'):
    ck('shared UI helper '+token,token in ui)
for sid in ('skill.animal_handling','skill.drink_preparation','skill.trading','skill.teaching'):
    ck('relationship hint '+sid,sid in ui)
ck('relationship wording rejects background gate','not background-gated' in ui or 'remains available to every Rebirth survivor' in ui)

creator=text('Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs'); menu=text('Config/XUi_Menu/windows.xml')
for token in ('backgroundSignatureBonusIcon','backgroundSignatureBonusName','backgroundSignatureBonusDescription','reviewSignatureBonusIcon','reviewSignatureBonusName','reviewSignatureBonusDescription'):
    ck('Creator controller '+token,token in creator)
    ck('Creator XML '+token,token in menu)
ck('Creator selected Background bonus panel','backgroundSignatureBonusPanel' in menu)
ck('Creator final review bonus panel','reviewSignatureBonusPanel' in menu)
ck('Creator uses authoritative bonus icon','SignatureBonusIcon(backgroundId)' in creator)
ck('Creator review uses authoritative bonus icon','SignatureBonusIcon(reviewBackgroundId)' in creator)

chooser=text('Scripts/Survivor/UI/XUiC_RebirthChooseSurvivor.cs')
ck('pre-spawn profile shows Signature Bonus','BuildSignatureBonusSummary(profile.BackgroundId, true)' in chooser)
ig=text('Config/XUi_InGame/windows.xml'); character=text('Scripts/Survivor/UI/XUiC_RebirthSurvivorCharacter.cs')
ck('Character Origin Signature Bonus section','BuildSignatureBonusSummary(bg.Id, true)' in character)
ck('Character selected Skill relationship hint','BuildSkillRelationshipHint(selectedSkill.Id)' in character)
ck('Character Skills controller capacity 48','private const int SkillRows = 48;' in character)
ck('Character Progression controller capacity 48','private const int ProgressionSkillRows = 48;' in character)
for prefix in ('survivorSkillRow','survivorProgressionSkillRow'):
    rows=set(int(x) for x in re.findall(r'name="'+prefix+r'(\d+)"',ig))
    ck(prefix+' exactly 0..47',rows==set(range(48)),len(rows))
for prefix in ('btnSurvivorSkillExplore','btnSurvivorProgressionSkill'):
    rows=set(int(x) for x in re.findall(r'name="'+prefix+r'(\d+)"',ig))
    ck(prefix+' exactly 0..47',rows==set(range(48)),len(rows))
ck('Skills retains native scrollbar','name="survivorSkillsContent"' in ig and '<defaultscrollbar/>' in ig)
ck('Progression retains native scrollbar','name="survivorProgressionSkillsContent"' in ig and '<defaultscrollbar/>' in ig)
ck('expanded skill content height','name="survivorSkillsContent"' in ig and 'height="2016"' in ig)
ck('expanded progression skill content height','name="survivorProgressionSkillsContent"' in ig and 'height="2016"' in ig)

# Advanced Discipline graph/UI
gdef=text('Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphDefinition.cs')
greg=text('Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphRegistry.cs')
proj=text('Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerUiProjection.cs')
explorer=text('Scripts/Survivor/UI/XUiC_RebirthProgressionExplorer.cs')
for token in ('Discipline','RequiresDiscipline','UnlocksDiscipline'):
    ck('graph enum '+token,token in gdef)
ck('Explorer legend includes Discipline','RebirthProgressionGraphNodeType.Discipline' in explorer and 'progressionLegendTypeDiscipline' in ig)
ck('Background graph includes Signature Bonus','BuildBackgroundDescription' in greg and 'SignatureBonusName(background.Id)' in greg)
ck('Background graph explicitly rejects normal prerequisite interpretation','xuiRebirthBackgroundBonusNotPrerequisite' in greg)
ck('no Signature Bonus requirement edge code','SignatureBonusName(background.Id)' in greg and 'RequiresSkill' not in greg[greg.index('BuildBackgroundDescription'):greg.index('AddKnowledgeAssociationEdges')])

adv=ET.parse(root/'Config/_Survivor/advanced_disciplines.xml').getroot(); disciplines=adv.findall('./discipline'); paths=adv.findall('./explorer_paths/path')
ck('three Advanced Disciplines authored',len(disciplines)==3,len(disciplines))
for did in ('discipline.beastmaster','discipline.witch_doctor','discipline.berserker'):
    d=next((x for x in disciplines if x.get('id')==did),None);ck('discipline '+did,d is not None and bool(d.get('name_key')) and bool(d.get('description_key')) and bool(d.get('initiation_action')))
for pid in ('predator_handling','pack_bonding','apex_handling','mind_control_i','binding_normal','rage_basic','rage_controlled','rage_offensive','rage_blood','horror_panther','rage_panther'):
    ck('Explorer path '+pid,any(x.get('id')==pid for x in paths))
rageoff=next((x for x in paths if x.get('id')=='rage_offensive'),None)
ck('Offensive Rage relates Rage Capsule',rageoff is not None and rageoff.get('related_recipe')=='rebirthRageCapsule')
ck('graph consumes related recipe', 'Attribute("related_recipe")' in greg and 'advanced discipline outcome' in greg)
ck('Rage Capsule authored item','<item name="rebirthRageCapsule">' in text('Config/_Survivor/items.xml'))
ck('Rage Capsule authored recipe','<recipe name="rebirthRageCapsule"' in text('Config/_Survivor/recipes.xml'))
ck('Rage Capsule real action class','ItemActionUseRageCapsuleRebirth' in text('Config/_Survivor/items.xml') and exists('Scripts/Survivor/Progression/AdvancedDisciplines/ItemActionUseRageCapsuleRebirth.cs'))
ck('Rage Capsule use gate described','xuiRebirthExplorerActionRageOffensiveDesc' in text('Config/Localization.csv'))
ck('zombie animals excluded from Beastmaster',adv.find('./beastmaster_hard_exclusions') is not None and 'animalZombie' in text('Config/_Survivor/advanced_disciplines.xml'))
ck('summoning unavailability remains explicit','runtime_policy="fail_closed_when_entity_asset_missing"' in text('Config/_Survivor/advanced_disciplines.xml'))
ck('special panther routes present',adv.find('./special_panthers') is not None and len(adv.findall('./special_panthers/route'))==2)
ck('skill relationship hint enters Explorer focus summary','BuildSkillRelationshipHint(focus.Id)' in proj)

loc=text('Config/Localization.csv')
for key in ('xuiRebirthSignatureBonus','xuiRebirthSignatureBonusNone','xuiRebirthSignatureBonusNoneDesc','xuiRebirthProgressionRelatedSignatureBonus','xuiRebirthBackgroundBonusNotPrerequisite','xuiRebirthSkillRelationAnimalHandling','xuiRebirthSkillRelationDrinkPreparation','xuiRebirthSkillRelationTrading','xuiRebirthSkillRelationTeaching'):
    ck('localization '+key,re.search(r'(?m)^'+re.escape(key)+r',',loc) is not None)
# localization duplicates for added keys
keys=[line.split(',',1)[0] for line in loc.splitlines() if line and not line.startswith('#')]
for key in ('xuiRebirthSignatureBonus','xuiRebirthSignatureBonusNone','xuiRebirthSignatureBonusNoneDesc','xuiRebirthProgressionRelatedSignatureBonus','xuiRebirthBackgroundBonusNotPrerequisite','xuiRebirthSkillRelationAnimalHandling','xuiRebirthSkillRelationDrinkPreparation','xuiRebirthSkillRelationTrading','xuiRebirthSkillRelationTeaching'):
    ck('localization unique '+key,keys.count(key)==1,keys.count(key))

# Rebirth-only ownership: no native/base file paths are introduced by Chunk O source references.
ck('Creator remains Rebirth controller','controller="RebirthSurvivorCreator, RebirthUtils"' in menu)
ck('pre-spawn remains Rebirth controller','controller="RebirthChooseSurvivor, RebirthUtils"' in ig)
ck('Character remains Rebirth controller','controller="RebirthSurvivorCharacter, RebirthUtils"' in ig)
ck('Explorer remains Rebirth controller','controller="RebirthProgressionExplorer, RebirthUtils"' in ig)

xml_files=list(root.rglob('*.xml')); bad=[]
for f in xml_files:
    try: ET.parse(f)
    except Exception as e: bad.append((str(f.relative_to(root)),str(e)))
ck('all XML parse',not bad,('XML='+str(len(xml_files)) if not bad else bad[:3]))

passed=sum(1 for _,ok,_ in checks if ok); failed=len(checks)-passed
print(f'PC028_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={len(xml_files)}')
raise SystemExit(0 if failed==0 else 1)
