#!/usr/bin/env python3
from pathlib import Path
import sys,re,xml.etree.ElementTree as ET
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve();passed=failed=0

def check(name,ok):
 global passed,failed
 if ok: passed+=1;print('PASS',name)
 else: failed+=1;print('FAIL',name)
def text(rel):return (root/rel).read_text(encoding='utf-8',errors='replace')

bon=ET.parse(root/'Config/_Survivor/background_bonuses.xml').getroot();B={e.attrib['id']:e for e in bon.findall('bonus')}
def tune(b,key):
 for t in b.findall('tuning'):
  if t.attrib.get('key')==key:return(t.attrib.get('value'),t.attrib.get('locked'))
 return None
check('Built to Last locked +15%',tune(B['background_bonus.built_to_last'],'max_durability_multiplier')==('1.15','true'))
check('Heat Treatment locked +15%',tune(B['background_bonus.heat_treatment'],'max_durability_multiplier')==('1.15','true'))
check('Engineered Reliability locked +50%',tune(B['background_bonus.engineered_reliability'],'max_durability_multiplier')==('1.5','true'))
check('Parts Salvager tuning explicit unlocked',tune(B['background_bonus.parts_salvager'],'mechanical_component_multiplier')==('1.5','false'))
check('Power Saver tuning explicit unlocked',tune(B['background_bonus.power_saver'],'additional_power_savings')==('0.15','false'))

cfg=ET.parse(root/'Config/_Survivor/skill_sources.xml').getroot();w=cfg.find('workmanship')
check('workmanship tuning section exists',w is not None)
check('construction signed curve authored',w is not None and w.attrib.get('construction_durability_negative')=='-0.15' and w.attrib.get('construction_durability_positive')=='0.25')
check('technical trap signed curve authored',w is not None and w.attrib.get('technical_trap_durability_negative')=='-0.15' and w.attrib.get('technical_trap_durability_positive')=='0.25')

svc=text('Scripts/Survivor/Backgrounds/RebirthWorkmanshipSignatureService.cs')
net=text('Scripts/Survivor/Backgrounds/RebirthWorkmanshipSignatureNetPackages.cs')
placed=text('Scripts/Survivor/Provenance/RebirthPlacedWorkmanshipService.cs')
infra=text('Scripts/Survivor/Progression/RebirthInfrastructureWorkService.cs')
runtime=text('Scripts/Survivor/Progression/RebirthProgressionRuntimeConfig.cs')
craft=text('Scripts/Survivor/Progression/RebirthServiceCraftSkillService.cs')
installer=text('Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs')
dbg=text('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs')

check('all five Chunk G bonus IDs consumed',all(x in svc+infra for x in ['background_bonus.built_to_last','background_bonus.heat_treatment','background_bonus.engineered_reliability','background_bonus.power_saver','background_bonus.parts_salvager']))
check('workmanship service installed', 'RebirthWorkmanshipSignatureService.Install(HarmonyInstance)' in installer)
check('Rebirth-only server block guard','!world.IsRemote()' in svc and 'RebirthSurvivorMode.IsEnabledForCurrentWorld()' in svc)
check('block damage patches positive damage only','_damagePoints<=0' in svc and '_bBypassMaxDamage' in svc)
check('native repair/upgrade amount not multiplied','_damagePoints/multiplier' in svc and 'if(_damagePoints<=0' in svc)
check('block damage call-chain guard prevents downgrade double scaling','activeBlockDamagePositions' in svc and 'BlockDamagePostfix' in svc and 'postfix:new HarmonyMethod(postfix)' in svc)
check('construction skill curve loaded',all(x in runtime for x in ['ConstructionDurabilityNegative','ConstructionDurabilityPositive','construction_durability_negative','construction_durability_positive']))
check('trap skill curve loaded',all(x in runtime for x in ['TechnicalTrapDurabilityNegative','TechnicalTrapDurabilityPositive','technical_trap_durability_negative','technical_trap_durability_positive']))
check('construction bonus is additive after skill curve','multiplier+=Math.Max(0f,GetBonusMultiplierDelta(BuiltToLastBonusId' in svc)
check('engineer bonus is additive after skill curve','multiplier+=Math.Max(0f,GetBonusMultiplierDelta(EngineeredReliabilityBonusId' in svc)
check('effective max derived once from persisted baseline','OriginalBaseMaxDurability' in svc and 'CalculateEffectivePlacedMax' in svc)
check('upgrade preserves record not creator reassignment','if(records.TryGetValue(key,out r)&&r!=null)' in placed and 'r.OriginalBaseMaxDurability=Math.Max(0,current.Block.MaxDamage)' in placed)
check('pickup redeploy reads carried placed provenance','TryReadCarried(heldBeforePlacement' in placed and 'Kind="placed_block"' in placed)
check('technical trap set exact six',all(x in placed for x in ['"autoTurret"','"bladeTrap"','"dartTrap"','"electricfencepost"','"m60Turret"','"shotgunTurret"']))
check('electric fence uses Electrical Skill','if(n=="electricfencepost")return "skill.electrical"' in placed)
check('blade trap uses Metalworking Skill','if(n=="bladetrap")return "skill.metalworking"' in placed)
check('dart trap uses Mechanics Skill','if(n=="darttrap")return "skill.mechanics"' in placed)
check('turrets fall back to Deployable Turrets Skill','return "skill.deployable_turrets"' in placed)

check('Metal Worker feature flag now enabled','MetalworkingPersistentHeatTreatmentGradeEnabled = true' in craft)
check('Metal Worker only authoritative metalworking recipes','ClassifyRecipe(recipeName),"skill.metalworking"' in svc)
check('Metal Worker only durable outputs','item.MaxUseTimes<=0' in svc and 'nativeMax=Math.Max(0,item.MaxUseTimes)' in svc)
check('Metal Worker uses V3 condition adapter','RebirthItemConditionStatAdapter.TrySetEffectiveMaxUseTimes' in svc)
check('Metal Worker stamps item provenance','Kind="crafted_item"' in svc and 'BonusId=HeatTreatmentBonusId' in svc)
check('Metal Worker original crafted max becomes restoration cap','OriginalMaxUseTimes=applied' in svc)
check('Metal Worker nonstack guard','RebirthItemProvenanceAdapter.TryRead(item,out existing)' in svc)
check('local craft stamps before output through giveExp','DeclaredMethod(typeof(XUiC_RecipeStack),"giveExp"' in svc and 'LocalCraftPrefix' in svc)
check('remote metal craft uses server request','NetPackageRebirthMetalCraftSignatureRequest' in svc and 'ToServer' in net)
check('server validates recipe output identity','recipe.itemValueType!=itemType' in svc)
check('remote metal server commit patch','NetPackagePlayerInventory' in svc and 'NativeInventoryCommitPrefix' in svc)
check('server correction sends authoritative ItemValue','NetPackageRebirthMetalCraftCorrection' in net and 'corrected.Write(b)' in net)

check('normal Electrical Skill savings retained','ElectricalMaxPowerSavings' in infra and '*workmanship*condition' in infra)
check('Power Saver requires persisted configured authorship','ConfiguredByBonusId' in infra and 'background_bonus.power_saver' in infra and 'ConfiguredByBackgroundId' in infra and 'background.electrician' in infra)
check('Power Saver is additional not replacement','savings+=' in infra and 'additional_power_savings' in infra)
check('Power Saver condition-sensitive','additional_power_savings",0.15f),0f,0.35f)*condition' in infra)
check('placement captures electrical configured author','CaptureBuiltConfiguration' in infra and 'ConfiguredByStableId=author.StablePlayerId' in infra)
check('carried electrical configuration wins placement','if(FindRecordAt(pos)!=null)return true' in infra and 'RestorePickedUpConfiguration' in placed)
check('rewire/service replaces configured authorship','r.ConfiguredByStableId=author.StablePlayerId' in infra and 'r.ConfiguredByBonusId=author.BonusId' in infra)
check('power hook remains native PowerUsed','AdjustPowerUsed' in infra and 'get_PowerUsed' in infra)

check('Mechanic only actual resourceMechanicalParts output','string.Equals(_iv.ItemClass.GetItemName(),"resourceMechanicalParts"' in svc)
check('Mechanic requires vehicle classifier','IsVehicleBlockName(blockName)' in svc)
check('Mechanic requires salvage tool','ClassifyHarvestTool(_actionData.invData.itemValue),"skill.salvage"' in svc)
check('Mechanic measures actual collected delta','CountBefore=CountPlayerItem' in svc and 'delta=after-__state.CountBefore' in svc)
check('Mechanic remote request is server directed','NetPackageRebirthMechanicSalvageRequest' in svc and 'ToServer' in net)
check('Mechanic server rechecks Background','RebirthBackgroundBonusService.HasBonus(player,PartsSalvagerBonusId)' in svc)
check('Mechanic server rechecks distance','Constants.cDigAndBuildDistance+3f' in svc)
check('Mechanic server rechecks current/requested vehicle','currentName,requestedBlockName' in svc and 'carRespawner_FR' in svc)
check('Mechanic server clamps client count to authored Harvest profile','GetAuthoredVehicleMechanicalPartsMax(requestedBlockName)' in svc and 'baseCount=Math.Min(baseCount,authoredMax)' in svc and 'EnumDropEvent.Harvest' in svc and 'd.maxCount' in svc)
check('Mechanic server replay guard uses block damage','MechanicReplayDamage' in svc and 'damage<=previous' in svc)
check('Mechanic fractional multiplier uses server RNG','GetGameRandom().RandomFloat' in svc)
check('Mechanic grant travels server to owning client','NetPackageRebirthMechanicSalvageGrant' in svc and '_attachedToEntityId:playerId' in svc)
check('Mechanic excludes electrical/fuel/general salvage outputs','resourceElectricParts' not in svc and 'ammoGasCan' not in svc and 'resourceOil' not in svc)

check('debug exposes effective workmanship','RebirthWorkmanshipSignatureService.BuildDebugReport' in dbg and 'effectiveMax=' in svc)
check('Base Game does not gain signature writes','RebirthSurvivorMode.IsEnabledForCurrentWorld()' in svc and 'RebirthSurvivorMode.IsEnabledForCurrentWorld()' in infra)
for rel in ['Scripts/Survivor/Backgrounds/RebirthWorkmanshipSignatureService.cs','Scripts/Survivor/Backgrounds/RebirthWorkmanshipSignatureNetPackages.cs','Scripts/Survivor/Progression/RebirthInfrastructureWorkService.cs','Scripts/Survivor/Provenance/RebirthPlacedWorkmanshipService.cs']:
 s=text(rel);check('delimiter '+rel,s.count('{')==s.count('}') and s.count('(')==s.count(')'))

bad=[];n=0
for p in root.rglob('*.xml'):
 n+=1
 try:ET.parse(p)
 except Exception as e:bad.append((str(p),str(e)))

# Pure calculation vectors for authored durability curves and locked additive bonuses.
def signed_endpoint(skill,neg,pos):
    skill=max(-50.0,min(100.0,skill))
    return (skill/100.0)*pos if skill>=0 else ((-skill)/50.0)*neg
cn=float(w.attrib['construction_durability_negative']); cp=float(w.attrib['construction_durability_positive'])
tn=float(w.attrib['technical_trap_durability_negative']); tp=float(w.attrib['technical_trap_durability_positive'])
check('vector construction neutral = 1.00',abs((1+signed_endpoint(0,cn,cp))-1.00)<1e-6)
check('vector construction +100 = 1.25',abs((1+signed_endpoint(100,cn,cp))-1.25)<1e-6)
check('vector construction worker +100 = 1.40',abs((1+signed_endpoint(100,cn,cp)+(1.15-1))-1.40)<1e-6)
check('vector construction -50 = 0.85',abs((1+signed_endpoint(-50,cn,cp))-0.85)<1e-6)
check('vector technical +100 = 1.25',abs((1+signed_endpoint(100,tn,tp))-1.25)<1e-6)
check('vector engineer +100 = 1.75',abs((1+signed_endpoint(100,tn,tp)+(1.5-1))-1.75)<1e-6)
import math
check('vector virtual damage 100 at x1.40 = 72',math.ceil(100/1.40)==72)
check('all project XML parses',not bad)
print(f'PC020_STATIC_CHECKS={passed+failed} PASSED={passed} FAILED={failed} XML={n}')
for x in bad:print('XMLERR',x)
sys.exit(1 if failed else 0)
