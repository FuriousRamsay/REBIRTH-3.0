#!/usr/bin/env python3
from pathlib import Path
import sys, xml.etree.ElementTree as ET, re
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve(); passed=failed=0

def check(name,ok):
 global passed,failed
 if ok: passed+=1; print('PASS',name)
 else: failed+=1; print('FAIL',name)

def text(rel): return (root/rel).read_text(encoding='utf-8',errors='replace')

bon=ET.parse(root/'Config/_Survivor/background_bonuses.xml').getroot(); B={e.attrib['id']:e for e in bon.findall('bonus')}
def tune(b,key):
 for t in b.findall('tuning'):
  if t.attrib.get('key')==key:return (t.attrib.get('value'),t.attrib.get('locked'))
 return None
check('Trauma Specialist locked 2x',tune(B['background_bonus.trauma_specialist'],'treated_healing_multiplier')==('2','true'))
check('Bookworm locked 2x',tune(B['background_bonus.bookworm'],'literature_content_multiplier')==('2','true'))
check('Pharmacy implementation tuning explicit unlocked',tune(B['background_bonus.pharmacy_eye'],'useful_medication_copy_chance')==('0.75','false'))
check('Reagent implementation tuning explicit unlocked',tune(B['background_bonus.reagent_recovery'],'recovery_chance')==('0.25','false'))

svc=text('Scripts/Survivor/Backgrounds/RebirthMedicalLootCraftSignatureService.cs')
patch=text('Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs')
medxml=text('Config/_Survivor/background_bonus_medical.xml')
check('all four bonus IDs consumed',all(x in svc for x in ['background_bonus.trauma_specialist','background_bonus.pharmacy_eye','background_bonus.reagent_recovery','background_bonus.bookworm']))
check('Rebirth-only server authority guard','RebirthSurvivorMode.IsEnabledForCurrentWorld()' in svc and '!GameManager.Instance.World.IsRemote()' in svc)
check('Background ownership remains derived service',svc.count('RebirthBackgroundBonusService.HasBonus')>=4 and 'SignatureBonusId' not in svc)
check('Paramedic uses locked tuning key','treated_healing_multiplier' in svc)
check('Paramedic abrasion provenance marker', 'rbParamedicAbrasionTreatment' in svc and 'healAbrasionMult' in svc)
check('Paramedic leg provenance marker', 'rbParamedicLegTreatment' in svc and '$legTreatedCritHealingBase' in svc)
check('Paramedic arm provenance marker', 'rbParamedicArmTreatment' in svc and '$armTreatedCritHealingBase' in svc)
check('Paramedic anti-stacking markers checked before multiply', svc.count('<= 0f')>=3)
check('other-player successful treatment hook retained','RebirthMedicalLootCraftSignatureService.OnSuccessfulMedicalTreatment(__state.Healer,__state.Patient)' in patch)
check('self-treatment hook added','RebirthParamedicSelfTreatmentSignaturePatch' in patch and 'ItemActionEat' in patch)
check('self-treatment limited to medical tag','FastTags<TagGroup.Global>.Parse("medical")' in patch)
check('injury marker cleanup authored',all(x in medxml for x in ['rbParamedicAbrasionTreatment','rbParamedicLegTreatment','rbParamedicArmTreatment','onSelfBuffRemove']))
check('limb and abrasion cleanup targets authored',all(x in medxml for x in ['buffInjuryAbrasionTreated','buffLegSplinted','buffLegCast','buffArmSplinted','buffArmCast']))

check('loot patch captures pre-open touched state','RebirthBackgroundSpecializedLootPatch' in patch and 'WasTouched' in patch)
check('loot specialization happens after native open','private static void Postfix(LootManager __instance' in patch)
check('loot service rejects already touched', 'wasAlreadyTouched' in svc and 'if (!IsServerAuthoritativeWorld()' in svc)
check('Bookworm requires literature container','IsLiteratureContainer(originalLootList)' in svc)
check('Bookworm doubles existing eligible stacks only','MultiplyExistingEligibleStacks(tile.items, IsKnowledgeLiteratureItem, 2)' in svc)
check('Bookworm does not modify loot stage','LootStage' not in svc and 'GetHighestPartyLootStage' not in svc)
check('Bookworm audited families',all(x in svc for x in ['bookcase','bookpile','crackabook','bookshelf']))
check('Bookworm REBIRTH knowledge prefixes explicit',all(x in svc for x in ['rebirthFieldNotes','rebirthTheory','rebirthRecipe','rebirthManual','rebirthSchematic','rebirthGuide','rebirthCookbook','rebirthFormula','rebirthPattern']))
check('Bookworm audiobook knowledge included','rebirthAudio' in svc and 'Cassette' in svc)
check('Pharmacy requires pharmaceutical container','IsPharmaceuticalContainer(originalLootList)' in svc)
check('Pharmacy does not duplicate unrelated medical clutter','resourceCloth' not in svc and 'resourcePaper' not in svc and 'resourceScrapPolymers' not in svc)
check('Pharmacy useful medication classifier explicit','IsUsefulPharmaceuticalItem' in svc and 'StartsWith("drug"' in svc)
check('Pharmacy support medicines explicit',all(x in svc for x in ['rebirthSupportNicotinePatch','rebirthSupportProbioticCapsules','rebirthSupportRespiratoryInhaler']))
check('Pharmacy roll uses game random once per eligible stack','manager.Random.RandomFloat' in svc)
check('Pharmacy stack maximum respected','Stacknumber.Value' in svc and 'Math.Min(stack.count' in svc)

check('workstation completion calls reagent service','OnSuccessfulWorkstationCraft(__instance,crafterEntityID,recipeName,craftedCount)' in patch)
check('reagent service requires successful positive crafted count','craftedCount <= 0' in svc)
check('authored reagent roster exists','ReagentByRecipe' in svc)
check('audited herbal antibiotic reagent mapped','"drugHerbalAntibiotics", "resourcePotassiumNitratePowder"' in svc)
check('audited steroid reagent mapped','"drugSteroids", "resourceTestosteroneExtract"' in svc)
check('no generic recipe ingredient refunding','ingredients' not in svc.lower())
check('reagent recovery uses authoritative world RNG','GetGameRandom().RandomFloat' in svc)
check('reagent recovery deposits in workstation output','workstation.Output' in svc)
check('reagent output capacity preflight','if(capacity<count)return false;' in svc)
check('reagent recovery does not partially commit on insufficient space',svc.index('if(capacity<count)return false;') < svc.index('int remaining=count;'))

for rel in ['Scripts/Survivor/Backgrounds/RebirthMedicalLootCraftSignatureService.cs','Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs']:
 s=text(rel); check('delimiter '+rel,s.count('{')==s.count('}') and s.count('(')==s.count(')'))
bad=[]; n=0
for p in root.rglob('*.xml'):
 n+=1
 try: ET.parse(p)
 except Exception as e: bad.append((str(p),str(e)))
check('all project XML parses',not bad)
print(f'PC019_STATIC_CHECKS={passed+failed} PASSED={passed} FAILED={failed} XML={n}')
if bad:
 for x in bad:print('XMLERR',x)
sys.exit(1 if failed else 0)
