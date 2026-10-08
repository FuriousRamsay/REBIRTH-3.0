#!/usr/bin/env python3
from pathlib import Path
import csv, sys, xml.etree.ElementTree as ET

R=Path(__file__).resolve().parents[2]
checks=[]
def ck(name,cond,detail=''):
    ok=bool(cond); checks.append((name,ok,detail)); print(('PASS ' if ok else 'FAIL ')+name+((' :: '+detail) if detail and not ok else '')); return ok
def text(rel):
    p=R/rel
    return p.read_text(encoding='utf-8-sig',errors='ignore') if p.is_file() else ''
def xml(rel): return ET.parse(R/rel).getroot()

# Project integrity.
xmls=list(R.rglob('*.xml')); bad=[]
for p in xmls:
    try: ET.parse(p)
    except Exception as e: bad.append((p.relative_to(R).as_posix(),str(e)))
ck('all project XML parses',not bad,str(bad[:4]))
rows=list(csv.reader((R/'Config/Localization.csv').open(encoding='utf-8-sig',newline='')))
keys=[r[0].strip() for r in rows[1:] if r and r[0].strip()]
dups=sorted({k for k in keys if keys.count(k)>1})
ck('localization duplicate keys zero',not dups,str(dups[:8]))

common=text('Scripts/Survivor/Provenance/RebirthProvenanceCommon.cs')
item=text('Scripts/Survivor/Provenance/RebirthItemProvenanceAdapter.cs')
placed=text('Scripts/Survivor/Provenance/RebirthPlacedWorkmanshipService.cs')
crop=text('Scripts/Survivor/Provenance/RebirthCropProvenanceAdapter.cs')
infra=text('Scripts/Survivor/Progression/RebirthInfrastructureWorkService.cs')
installer=text('Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs')
pickup=text('Scripts/BlockPickup/RebirthBlockPickupService.cs')
te=text('Scripts/AdvancedFarming/AdvancedFarmingTileEntities.cs')
plant=text('Scripts/AdvancedFarming/BlockPlantGrowingRebirth.cs')
farmutil=text('Scripts/AdvancedFarming/RebirthUtilities.cs')
catchup=text('Scripts/AdvancedFarming/AdvancedFarmingCatchupService.cs')
debug=text('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs')
patches=text('Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs')

for rel,s in [
 ('RebirthProvenanceCommon.cs',common),('RebirthItemProvenanceAdapter.cs',item),
 ('RebirthPlacedWorkmanshipService.cs',placed),('RebirthCropProvenanceAdapter.cs',crop)]:
    ck(rel+' exists',bool(s))

# Authority and identity capture.
ck('provenance identity is Rebirth-only','!RebirthSurvivorMode.IsEnabledForCurrentWorld()' in common)
ck('provenance identity refuses remote authority','world != null && world.IsRemote()' in common)
ck('provenance identity uses stable server identity','RebirthStablePlayerIdentity.TryResolveServerEntity' in common)
ck('provenance identity derives immutable world origin','RebirthWorldCharacterService.TryGet' in common and 'record.Origin.BackgroundId' in common)
ck('provenance identity derives bonus centrally','RebirthBackgroundBonusService.TryGetSignatureBonus' in common)

# Item provenance and non-stacking.
for token in ['RebirthProvVersion','RebirthProvCreator','RebirthProvBackground','RebirthProvBonus','RebirthProvSkill','RebirthProvWorkmanship','RebirthProvOriginalMaxUseTimes','RebirthProvBatch']:
    ck('item metadata '+token,token in item)
ck('item original max baseline captured','OriginalMaxUseTimes = Math.Max(0f, value.MaxUseTimes)' in item)
ck('item provenance stack compatibility compares authored state','AreStackCompatible(ItemValue a, ItemValue b)' in item and 'OriginalMaxUseTimes' in item and 'BatchToken' in item)
ck('stack guard patches verified CanStackWith signature','nameof(ItemStack.CanStackWith)' in item and 'typeof(ItemStack), typeof(bool)' in item)
ck('stack guard also protects electrical component','RebirthElectricalItemProvenance.AreStackCompatible' in item)
ck('stack guard installed','RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthProvenanceItemStackPatch))' in installer)

# Ordinary placed-block provenance sidecar.
ck('placed sidecar persistence file','PlacedWorkmanship.dat' in placed)
ck('placed sidecar server-owned lifecycle','GameStarting.RegisterHandler' in placed and 'GameUpdate.RegisterHandler' in placed and 'WorldShuttingDown.RegisterHandler' in placed)
ck('placed capture Rebirth/server gate','world.IsRemote()' in placed and 'IsServer()' in placed and '!RebirthSurvivorMode.IsEnabledForCurrentWorld()' in placed)
ck('placed capture construction Skill','"skill.construction"' in placed)
ck('placed capture technical trap Skill','"skill.deployable_turrets"' in placed)
ck('technical trap source IDs audited',all(x in placed for x in ['"autoTurret"','"bladeTrap"','"dartTrap"','"electricfencepost"','"m60Turret"','"shotgunTurret"']))
ck('crop provenance not duplicated in placed sidecar','current.Block is BlockPlantGrowingRebirth' in placed)
ck('ordinary powered provenance not duplicated in placed sidecar','current.Block is BlockPowered && !technical' in placed)
ck('placed pickup serializes provenance onto item','RebirthItemProvenanceAdapter.Stamp' in placed and 'NetPackageRebirthPlacedProvenancePickup' in placed)
ck('placement restore reads carried provenance','TryReadCarried' in placed and 'RebirthItemProvenanceAdapter.TryRead' in placed)
ck('upgrade preserves provenance record','HandleUpgrade' in placed and 'records.TryGetValue(key,out r)' in placed)
ck('placement patch uses verified PlaceBlock signature','nameof(Block.PlaceBlock)' in placed and 'typeof(WorldBase)' in placed and 'typeof(BlockPlacement.Result)' in placed and 'typeof(EntityAlive)' in placed)
ck('placed provenance service installed','RebirthPlacedWorkmanshipService.Install(HarmonyInstance)' in installer)

# Pickup bridge ordering and cleanup.
idx_send=pickup.find('RebirthPlacedWorkmanshipPickupBridge.SendBeforePickup')
idx_e_send=pickup.find('RebirthElectricalPickupBridge.SendBeforePickup')
idx_pick=pickup.find('PickupBlockServer',max(idx_send,idx_e_send))
idx_remove=pickup.find('RebirthPlacedWorkmanshipService.Remove',idx_pick)
ck('pickup provenance sent before block removal',idx_send>=0 and idx_e_send>=0 and idx_pick>idx_send and idx_pick>idx_e_send)
ck('pickup provenance sidecars removed after pickup commit',idx_remove>idx_pick and 'RebirthInfrastructureWorkService.RemoveRecord(blockPos)' in pickup[idx_pick:])
ck('fresh pickup item receives both provenance components','RebirthPlacedWorkmanshipPickupBridge.ApplyPending' in pickup and 'RebirthElectricalPickupBridge.ApplyPending' in pickup)
ck('destroyed blocks clean provenance via existing authoritative callback','nameof(Block.OnBlockDestroyedBy)' in patches and 'RebirthPlacedWorkmanshipService.Remove(_bvRef.BlockPosition)' in patches and 'RebirthInfrastructureWorkService.RemoveRecord(_bvRef.BlockPosition)' in patches)

# Electrical configuration provenance is an independent component and v1-compatible.
ck('electrical persistence bumped to v2','PersistenceVersion = 2' in infra)
ck('electrical v1 persistence remains loadable','version<1||version>PersistenceVersion' in infra and 'if(version>=2)' in infra)
for token in ['ConfiguredByStableId','ConfiguredByBackgroundId','ConfiguredByBonusId','ConfiguredSkillValue']:
    ck('electrical record '+token,token in infra)
ck('electrical config author captured on successful service','RebirthProvenanceIdentity.TryCapture(player,out author)' in infra and 'r.ConfiguredByStableId=author.StablePlayerId' in infra)
ck('electrical item provenance is separate component','RebirthElectricalItemProvenance' in infra and 'RebirthElectricalProvVersion' in infra)
ck('electrical pickup/redeploy bridge exists','RebirthElectricalPickupBridge' in infra and 'RestorePickedUpConfiguration' in infra)
# Ensure PC017 did not wire bonus identity into power calculation.
adjust=infra[infra.find('public static int AdjustPowerUsed'):infra.find('public static bool TryService')]
ck('no Signature Bonus condition in electrical power calculation','ConfiguredByBonusId' not in adjust and 'background_bonus.' not in adjust and 'HasBonus' not in adjust)

# Crop provenance uses existing tile entity and preserves lifecycle transitions.
for token in ['RebirthGrowerStableId','RebirthGrowerBackgroundId','RebirthGrowerBonusId','RebirthPlantingSkillValue','RebirthProvenanceWorldTime']:
    ck('crop tile provenance '+token,token in te)
ck('crop persistence uses reserved version family','PersistencyVersionFamily = 0xC700' in te and 'PersistencyVersionWithProvenance = 0xC701' in te)
ck('crop persistence uses existing safe version-header pattern','GetLegacyForkVersion()' in te and 'baseStream.Position = versionPosition' in te and 'WritePayload(stream, true)' in te)
ck('crop provenance is Farmer-specific capture only','RebirthBackgroundBonusService.HasBonus(player,"background_bonus.rapid_cultivation")' in crop)
ck('crop provenance capture is server/Rebirth gated','world.IsRemote()' in crop and '!RebirthSurvivorMode.IsEnabledForCurrentWorld()' in crop)
ck('crop OnBlockAdded applies pending state','RebirthCropProvenanceAdapter.ApplyPending(world, blockPos)' in plant)
ck('crop OnBlockAdded captures planting author','RebirthCropProvenanceAdapter.CapturePlanting(world, blockPos, addedByPlayer)' in plant)
ck('harvest/reseed preserves crop provenance','CaptureSnapshot(world, cropPos)' in farmutil and 'RestoreSnapshot(world, cropPos, provenance)' in farmutil)
ck('growth stage swap preserves crop provenance','CaptureSnapshot(world, pos)' in catchup and 'RestoreSnapshot(world, pos, provenance)' in catchup)
# Foundation only: no growth multiplier/timing mutation in adapter.
ck('crop provenance adapter does not alter growth timing',all(x not in crop for x in ['GrowthMultiplier','growthMultiplier','AccumulatedTicks +=','GameTimerTicks +=','LastProcessedWorldSeconds +=']))

# Primitive armor supersession contract from PC014 matrix.
cp=xml('Config/_Survivor/crafting_progression.xml')
primitive={'armorPrimitiveHelmet','armorPrimitiveOutfit','armorPrimitiveGloves','armorPrimitiveBoots'}
entries={e.get('id'):e for e in cp.findall('.//recipe') if e.get('id') in primitive}
ck('four primitive armor manifest rows retained',set(entries)==primitive)
ck('primitive armor policy universal',all(e.get('policy')=='universal' for e in entries.values()))
ck('primitive armor has no Skill/Knowledge/Capability gate',all(not e.get('primary_skill') and not e.get('knowledge') and not e.get('capability') for e in entries.values()))
cap=xml('Config/_Survivor/capabilities.xml')
active_prim_caps=[e for e in cap.findall('.//capability') if (e.get('target_id') or '') in primitive]
ck('primitive armor direct capabilities removed',not active_prim_caps,str([e.get('id') for e in active_prim_caps]))
rk=xml('Config/_Survivor/recipe_knowledge.xml')
active_prim_rk=[e for e in rk.findall('.//recipe') if e.get('name') in primitive]
ck('primitive armor recipe Knowledge mappings removed',not active_prim_rk)
ck('legacy primitive Knowledge ID retained','pattern.tailoring.primitive_armor' in text('Config/_Survivor/progression.xml'))
ck('legacy primitive item ID retained','rebirthPatternPrimitiveArmor' in text('Config/_Survivor/items.xml') and 'rebirthPatternPrimitiveArmor' in text('Config/_Survivor/literature.xml'))
ck('legacy primitive pattern no longer distributed',all('rebirthPatternPrimitiveArmor' not in text(rel) for rel in ['Config/_Survivor/loot.xml','Config/_Survivor/traders.xml','Config/_Survivor/literature_distribution.xml','Config/_Survivor/backgrounds.xml']))
ck('legacy primitive item description no longer claims recipe gate','Primitive armor is now universally craftable in Rebirth' in text('Config/Localization.csv'))
ck('advanced Tailoring gate retained','capability.recipe.armorrogueboots' in text('Config/_Survivor/capabilities.xml') and 'pattern.tailoring.rogue_armor' in text('Config/_Survivor/recipe_knowledge.xml'))

# Debug is read-only and consolidated.
ck('consolidated provenance debug command registered','rbsurvivor provenance' in debug and 'ExecuteProvenance' in debug)
prov_section=debug[debug.find('private static void ExecuteProvenance'):debug.find('private static void ExecuteAdvancedDisciplines')]
ck('provenance debug command contains no mutation verbs', all(v not in prov_section for v in ['StampCreated(','CapturePlacement(','TryService(','RemoveRecord(','Save()']))

# No duplicate bonus entitlement state and no later signature effects activated in the new foundation.
ck('no separate SignatureBonusId persisted','SignatureBonusId' not in text('Scripts/Survivor/Persistence/RebirthSurvivorProfileModels.cs') and 'SignatureBonusId' not in text('Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs'))
new_foundation='\n'.join([common,item,placed,crop])
for forbidden in ['1.15f','1.50f','0.50f reduction','Master Restoration','Power Saver','Professional Cooking','Master Mixologist']:
    ck('foundation does not activate '+forbidden,forbidden not in new_foundation)

# Simple source delimiter gate for changed C# files.
changed_cs=[R/x for x in [
 'Scripts/Survivor/Provenance/RebirthProvenanceCommon.cs','Scripts/Survivor/Provenance/RebirthItemProvenanceAdapter.cs',
 'Scripts/Survivor/Provenance/RebirthPlacedWorkmanshipService.cs','Scripts/Survivor/Provenance/RebirthCropProvenanceAdapter.cs',
 'Scripts/Survivor/Progression/RebirthInfrastructureWorkService.cs','Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs',
 'Scripts/AdvancedFarming/AdvancedFarmingTileEntities.cs','Scripts/AdvancedFarming/RebirthUtilities.cs',
 'Scripts/AdvancedFarming/AdvancedFarmingCatchupService.cs','Scripts/AdvancedFarming/BlockPlantGrowingRebirth.cs',
 'Scripts/BlockPickup/RebirthBlockPickupService.cs','Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs',
 'Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs']]
for p in changed_cs:
    s=p.read_text(encoding='utf-8-sig',errors='ignore')
    ck('delimiter '+p.name,s.count('{')==s.count('}'),f"{{={s.count('{')} }}={s.count('}')}")

passed=sum(1 for _,ok,_ in checks if ok); failed=len(checks)-passed
print(f'PC017_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={len(xmls)}')
sys.exit(0 if failed==0 else 1)
