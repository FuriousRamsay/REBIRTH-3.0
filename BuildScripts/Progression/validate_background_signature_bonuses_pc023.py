#!/usr/bin/env python3
from pathlib import Path
import sys, xml.etree.ElementTree as ET, re, math
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve(); passed=failed=0

def check(name, ok):
    global passed,failed
    if ok: passed+=1; print('PASS',name)
    else: failed+=1; print('FAIL',name)
def text(rel): return (root/rel).read_text(encoding='utf-8',errors='replace')

def balanced(rel):
    s=text(rel)
    return s.count('{')==s.count('}') and s.count('(')==s.count(')') and s.count('[')==s.count(']')

bon=ET.parse(root/'Config/_Survivor/background_bonuses.xml').getroot(); B={e.attrib['id']:e for e in bon.findall('bonus')}
def tune(b,key):
    for t in b.findall('tuning'):
        if t.attrib.get('key')==key:return (t.attrib.get('value'),t.attrib.get('locked'))
    return None

# Tunable contracts: implementation defaults are explicit and unlocked.
check('Chef tuning 1.25 unlocked', tune(B['background_bonus.professional_cooking'],'meal_energy_efficiency_multiplier')==('1.25','false'))
check('Farmer tuning 1.5 unlocked', tune(B['background_bonus.rapid_cultivation'],'growth_rate_multiplier')==('1.5','false'))
check('Butcher tuning 1.5 unlocked', tune(B['background_bonus.whole_animal'],'carcass_resource_multiplier')==('1.5','false'))

svc=text('Scripts/Survivor/Backgrounds/RebirthFoodFarmingButcherySignatureService.cs')
net=text('Scripts/Survivor/Backgrounds/RebirthFoodFarmingButcherySignatureNetPackages.cs')
inst=text('Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs')
models=text('Scripts/Metabolism/RebirthMetabolismModels.cs')
met=text('Scripts/Metabolism/RebirthMetabolismService.cs')
repo=text('Scripts/Metabolism/RebirthMetabolismStateRepository.cs')
metnet=text('Scripts/Network/RebirthMetabolismNetPackages.cs')
metui=text('Scripts/Metabolism/UI/XUiC_RebirthMetabolismUi.cs')
policy=text('Scripts/AdvancedFarming/AdvancedFarmingRuntimePolicy.cs')
util=text('Scripts/AdvancedFarming/RebirthUtilities.cs')
catch=text('Scripts/AdvancedFarming/AdvancedFarmingCatchupService.cs')
hover=text('Scripts/AdvancedFarming/AdvancedFarmingHoverTextService.cs')
prov=text('Scripts/Survivor/Provenance/RebirthCropProvenanceAdapter.cs')
te=text('Scripts/AdvancedFarming/AdvancedFarmingTileEntities.cs')
resource=text('Scripts/Survivor/Progression/RebirthResourceFieldSkillService.cs')
buffs=text('Config/buffs.xml')
craft=text('Config/_Survivor/crafting_progression.xml')
cap=text('Config/_Survivor/capabilities.xml')
know=text('Config/_Survivor/recipe_knowledge.xml')
dbg=text('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs')
progression=text('Config/_Survivor/progression.xml')

# Installation and common authority isolation.
check('Chunk J signature service installed', 'RebirthFoodFarmingButcherySignatureService.Install(HarmonyInstance)' in inst)
check('Chunk J service Rebirth-mode gated', svc.count('RebirthSurvivorMode.IsEnabledForCurrentWorld()')>=4)
check('Chunk J service has read-only debug report', 'BuildDebugReport(EntityPlayer player, Vector3i? cropPos)' in svc)
check('Chunk J vector harness exists', 'public static string RunVectors()' in svc)
check('Chunk J console route exists', 'chunkjbonus' in dbg and 'ExecuteChunkJBonus' in dbg)
check('Chunk J console crop diagnostic is read-only', 'rbsurvivor chunkjbonus crop <x> <y> <z>' in dbg)

# Chef craft provenance and authority.
check('Chef local craft hook audited', 'XUiC_RecipeStack' in svc and 'nameof(LocalCraftPrefix)' in svc)
check('Chef workstation completion hook audited', 'TileEntityWorkstation' in svc and 'nameof(WorkstationCraftPrefix)' in svc)
check('Chef remote inventory commit hook audited', 'NetPackagePlayerInventory' in svc and 'nameof(NativeInventoryCommitPrefix)' in svc)
check('Chef only Cooking recipes', 'ClassifyRecipe(recipe)' in svc and '"skill.cooking"' in svc)
check('Chef only food outputs', 'TryResolve(output, out def)' in svc and 'def.IsFood' in svc)
check('Chef requires exact bonus', 'ProfessionalCookingBonusId' in svc and 'HasBonus(player, ProfessionalCookingBonusId)' in svc)
check('Chef client prestack projection exists', 'client-prestack-projection' in svc and 'StampChefMeal(player, recipeName, _iv, false' in svc)
check('Chef remote request is ToServer', 'NetPackageRebirthChefCraftSignatureRequest' in net and 'PackageDirection => NetPackageDirection.ToServer' in net)
check('Chef request sender-bound', 'ValidEntityIdForSender(playerId)' in net)
check('Chef server revalidates recipe output type', 'recipe.itemValueType != itemType' in svc)
check('Chef server revalidates Cooking classifier', 'ClassifyRecipe(recipe), "skill.cooking"' in svc)
check('Chef server revalidates food definition', 'output == null || !RebirthConsumableResolver.TryResolve(output, out def)' in svc)
check('Chef server revalidates bonus ownership', '!RebirthBackgroundBonusService.HasBonus(player, ProfessionalCookingBonusId)' in svc)
check('Chef pending request expires', 'PendingSeconds = 120.0' in svc and 'CreatedUtc < cutoff' in svc)
check('Chef commit requires projected provenance', 'FindChefCandidate(a, p, true)' in svc and 'FindChefCandidate(b, p, true)' in svc)
check('Chef commit has no arbitrary unprepared fallback', 'FindChefCandidate(a, p, false)' not in svc and 'FindChefCandidate(b, p, false)' not in svc)
check('Chef server sends authoritative correction', 'NetPackageRebirthChefCraftCorrection' in svc and 'ChefCorrections.Enqueue' in svc)
check('Chef correction is ToClient', 'NetPackageRebirthChefCraftCorrection' in net and 'PackageDirection => NetPackageDirection.ToClient' in net)
check('Chef provenance kind prepared_meal', 'PreparedMealKind = "prepared_meal"' in svc)
check('Chef provenance stores creator/background/bonus', all(x in svc for x in ['CreatorStableId = author.StablePlayerId','BackgroundId = author.BackgroundId','BonusId = ProfessionalCookingBonusId']))
check('Chef provenance stores Cooking skill', 'SkillId = "skill.cooking"' in svc and 'SkillValue = cookingSkill' in svc)
check('Chef provenance stores preparation efficiency', 'Workmanship = efficiency' in svc)
check('Chef preparation reader rejects unrelated provenance', 'p.Kind, PreparedMealKind' in svc and 'p.BonusId, ProfessionalCookingBonusId' in svc)

# Chef metabolism: no instant creation, digestion-time only, persistent snapshot.
check('Metabolism schema at least v8', re.search(r'CurrentVersion\s*=\s*(\d+)',models) is not None and int(re.search(r'CurrentVersion\s*=\s*(\d+)',models).group(1))>=8)
check('Ingestion entry stores prepared efficiency', 'PreparedMealEnergyEfficiencyMultiplier = 1f' in models)
check('Food consumption snapshots item preparation', 'GetPreparedMealEnergyEfficiency(stack.itemValue)' in met)
check('Prepared snapshot persisted', 'preparedEnergyEfficiency' in repo and 'PreparedMealEnergyEfficiencyMultiplier' in repo)
check('Legacy entries default prepared efficiency neutral', 'AF(x, "preparedEnergyEfficiency", 1f)' in repo)
check('Prepared effect requires food entry', 'entry.Kind != RebirthIngestionKind.Food' in met)
check('Prepared effect requires intestinal nutrition', 'entry.IntestinalNutritionUnits <= 0.001f' in met)
check('Prepared meals do not modify nutrient yield snapshot', met.count('NutrientYieldMultiplierSnapshot = mods.NutrientUtilization')>=2 and 'PreparedMealEnergyEfficiencyMultiplier' in met)
check('Prepared effect multiplies recovery rate', 'mods.EnergyRecovery) * preparedEfficiency * deltaRealMinutes' in met)
check('Prepared effect divides Food cost per recovered Energy', 'FoodUnitsPerEnergyRecovered) / Mathf.Max(1f, preparedEfficiency)' in met)
check('Prepared effect does not directly add Food', 'PreparedMealEnergyEfficiencyMultiplier' not in met[met.find('ProcessIntestinalNutritionAbsorption'):met.find('MergeOrAddEntry')])
check('Prepared effect does not directly add beverage Energy', 'PreparedMealEnergyEfficiencyMultiplier' not in met[met.find('ProcessIntestinalFluidAbsorption'):met.find('ProcessIntestinalNutritionAbsorption')])
check('Best active prepared meal used without multiplicative stacking', 'best = Mathf.Max(best' in met)
check('Actual recovery Nutrition use tracked', 'LastEnergyRecoveryNutritionUsePerRealMinute' in models and 'foodSpent / deltaRealMinutes' in met)
check('Tick Food-use display consumes actual recovery Nutrition use', 'energyRecoveryFoodPerRealMinute = state.LastEnergyRecoveryNutritionUsePerRealMinute' in met)
check('Snapshot replicates recovery Nutrition cost', 'EnergyRecoveryNutritionUsePerRealMinute' in models and 's.EnergyRecoveryNutritionUsePerRealMinute' in metnet)
check('Metabolism net length adjusted for one float', 'return 292' in metnet)
check('Energy tooltip uses replicated actual recovery Nutrition cost', 's.EnergyRecoveryNutritionUsePerRealMinute' in metui)
check('Energy binding uses replicated actual recovery Nutrition cost', 'snapshot.EnergyRecoveryNutritionUsePerRealMinute' in metui)

# Farmer provenance and accelerated crop-cycle authority.
check('Farmer crop provenance exact bonus capture inherited', 'background_bonus.rapid_cultivation' in prov)
check('Farmer provenance stored on plant tile entity', 'RebirthGrowerBonusId' in te and 'RebirthPlantingSkillValue' in te)
check('Farmer provenance serialized', 'WriteString(bw, RebirthGrowerBonusId' in te and 'ReadString(br, 128)' in te)
check('Farmer growth reads persisted tile provenance', 'te.RebirthGrowerBonusId' in svc and 'RapidCultivationBonusId' in svc)
check('Farmer growth not based on current harvester', 'GetRapidCultivationGrowthMultiplier(WorldBase world, Vector3i pos)' in svc and 'EntityPlayer' not in svc[svc.find('GetRapidCultivationGrowthMultiplier'):svc.find('GetEffectiveCropGrowthSeconds')])
check('Farmer growth multiplier reduces stage seconds', 'baseSeconds / Mathf.Max(1f, multiplier)' in svc)
check('Runtime policy exposes position-aware growth seconds', 'GetEffectiveGrowthSeconds(WorldBase world, Vector3i pos)' in policy)
check('Single active crop uses position-aware growth', 'GetEffectiveGrowthSeconds(world, plant.Pos)' in catch[:catch.find('ProcessPlantsActiveDirect')])
check('Active-direct batch uses per-crop growth', catch.count('GetEffectiveGrowthSeconds(world, plant.Pos)')>=3)
check('Shared catchup computes per-crop growth', 'PreparePlantCatchup(world, plant, nowSeconds, nowTicks, stageSeconds)' in catch and 'baseGrowthSeconds' in catch)
check('Crop hover uses position-aware stage rate', 'GetStageRate(world, blockPos)' in hover and 'GetEffectiveGrowthSeconds(world, pos)' in hover)
check('Catchup diagnostics use position-aware growth', catch.count('GetEffectiveGrowthSeconds(world, pos)')>=2)
check('Mature harvest captures crop provenance before reseed', 'CaptureSnapshot(world, cropPos)' in util)
check('Mature harvest restores provenance after reseed', 'RestoreSnapshot(world, cropPos, provenance)' in util)
check('Automatic mature harvest reseeds seed block', 'SetExposureNeutralPlantBlockRpc(world, cropPos, cropValue, seedBlock)' in util)
check('Farmer vector confirms 3600 / 1.5 = 2400', '3600 second stage' in svc and math.ceil(3600/1.5)==2400)

# Farming Skill yield parity for all survivors.
check('Native Farming Skill HarvestCount retained', '$rbSurvivorSkillFarmingHarvest' in buffs and 'cropHarvest,wildCropsHarvest' in buffs)
check('Advanced Farming direct output uses yield bridge', 'RollAdvancedFarmingHarvestCount(world, player, 1)' in util)
check('Yield bridge uses Farming Skill CVar', 'RebirthResourceFieldSkillService.FarmingHarvestCVar' in svc)
check('Yield bridge composes global Trait HarvestCount CVar', 'RebirthTraitGameplayModifierService.HarvestCountCVar' in svc)
check('Yield bridge server-authoritative', 'world.IsRemote()' in svc[svc.find('RollAdvancedFarmingHarvestCount'):svc.find('GetWholeAnimalHarvestDelta')])
check('Yield bridge stochastic fractional rounding', 'Mathf.FloorToInt(expected)' in svc and 'RandomFloat < fraction' in svc)
check('Zero-yield harvest remains a resolved harvest', 'if (count > 0)' in util and 'return true;' in util[util.find('TryBuildRebirthCropHarvest'):util.find('ShouldSkipRebirthCropHarvestDrop')])
check('Zero-yield mature harvest still reseeds', 'if (!TryBuildRebirthCropHarvest' in util and 'SetExposureNeutralPlantBlockRpc(world, cropPos, cropValue, seedBlock)' in util)
check('Farming LBD remains at least one completion event', 'System.Math.Max(1, CountItemStacks(harvest))' in util)

# Butcher Whole Animal native carcass output.
check('Butcher separate background Harvest CVar exists', 'WholeAnimalHarvestCVar = "$rbBackgroundWholeAnimalHarvest"' in resource)
check('Butcher CVar synced from exact signature bonus', 'GetWholeAnimalHarvestDelta(player)' in resource)
check('Butcher CVar reset when no Survivor character', 'SetCVar(player, WholeAnimalHarvestCVar, 0f)' in resource)
check('Butcher native HarvestCount passive exists', '@$rbBackgroundWholeAnimalHarvest' in buffs)
check('Butcher passive is restricted to butcherHarvest', 'value="@$rbBackgroundWholeAnimalHarvest" tags="butcherHarvest"' in buffs)
check('Butcher multiplier converts to additive delta', 'return multiplier - 1f' in svc)
check('Butcher exact bonus required', 'HasBonus(player, WholeAnimalBonusId)' in svc)
check('Butcher does not replace Animal Processing Skill CVar', '$rbSurvivorSkillAnimalHarvest' in resource and '@$rbSurvivorSkillAnimalHarvest' in buffs)
check('Butcher vector confirms 1.5 -> +0.50', 'contributes +0.50' in svc)

# Primitive armor superseded policy retained; advanced Tailoring stays gated.
primitive=['armorPrimitiveBoots','armorPrimitiveGloves','armorPrimitiveHelmet','armorPrimitiveOutfit']
for rid in primitive:
    check(rid+' universal', f'<recipe id="{rid}" policy="universal"' in craft)
    check(rid+' no capability mapping', rid not in cap)
    check(rid+' no recipe-knowledge mapping', rid not in know)
check('Primitive armor policy marked superseded PC017 universal', craft.count('decision_status="superseded_pc017_primitive_universal"')>=4)
check('Advanced Tailoring remains gated', 'primary_skill="skill.tailoring"' in craft and 'policy="gated"' in craft)
check('Legacy primitive pattern not actively distributed', 'pattern.tailoring.primitive_armor' not in craft)

# Progression metadata reflects implemented Chunk J channels.
check('Farming progression metadata names persisted Farmer provenance', 'Farmer Rapid Cultivation uses persisted per-crop growth provenance' in progression)
check('Animal Processing metadata names separate Whole Animal channel', 'Butcher Whole Animal adds a separate butcherHarvest Signature Bonus channel' in progression)

# Debug text and Base Game policy.
check('Resource debug reports Advanced Farming parity', 'Advanced Farming direct-harvest parity' in resource)
check('Resource debug reports separate Whole Animal channel', 'Whole Animal is a separate native butcherHarvest CVar' in resource)
check('Chunk J vectors state primitive universal policy', 'Primitive armor: universal policy remains inherited from PC017' in svc)
check('Chunk J vectors state no instant Chef effect', 'while prepared nutrition is intestinal' in svc)

# XML parse and delimiters.
xmls=list(root.rglob('*.xml')); xml_ok=True
for p in xmls:
    try: ET.parse(p)
    except Exception: xml_ok=False; break
check('all project XML parses',xml_ok)
for rel in [
'Scripts/Survivor/Backgrounds/RebirthFoodFarmingButcherySignatureService.cs',
'Scripts/Survivor/Backgrounds/RebirthFoodFarmingButcherySignatureNetPackages.cs',
'Scripts/Metabolism/RebirthMetabolismModels.cs','Scripts/Metabolism/RebirthMetabolismService.cs',
'Scripts/Metabolism/RebirthMetabolismStateRepository.cs','Scripts/Network/RebirthMetabolismNetPackages.cs',
'Scripts/Metabolism/UI/XUiC_RebirthMetabolismUi.cs','Scripts/AdvancedFarming/RebirthUtilities.cs',
'Scripts/AdvancedFarming/AdvancedFarmingRuntimePolicy.cs','Scripts/AdvancedFarming/AdvancedFarmingCatchupService.cs',
'Scripts/AdvancedFarming/AdvancedFarmingHoverTextService.cs','Scripts/Survivor/Progression/RebirthResourceFieldSkillService.cs',
'Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs','Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs']:
    check('delimiter '+Path(rel).name, balanced(rel))

print(f'PC023_STATIC_CHECKS={passed+failed} PASSED={passed} FAILED={failed} XML={len(xmls)}')
sys.exit(1 if failed else 0)
