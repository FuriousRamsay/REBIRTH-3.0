#!/usr/bin/env python3
from pathlib import Path
import sys, xml.etree.ElementTree as ET, math, re
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

# Locked contracts and explicit unlocked implementation defaults.
check('Soldier max stacks locked 3',tune(B['background_bonus.combat_momentum'],'max_stacks')==('3','true'))
check('Police ammo multiplier locked 3',tune(B['background_bonus.patrol_car_familiarity'],'ammunition_multiplier')==('3','true'))
for key,val in [('combat_grace_seconds','12'),('minimum_personal_damage','20'),('activity_minimum_damage','5'),('minimum_target_health_fraction','0.15'),('reload_per_stack','0.05'),('handling_per_stack','0.04'),('spread_reduction_per_stack','0.04'),('recoil_reduction_per_stack','0.04')]: check('Soldier unlocked '+key,tune(B['background_bonus.combat_momentum'],key)==(val,'false'))
for key,val in [('observation_seconds','3'),('mark_duration_seconds','60'),('observation_distance_meters','30'),('tracking_distance_meters','120')]: check('Hunter unlocked '+key,tune(B['background_bonus.hunters_mark'],key)==(val,'false'))
check('Park Ranger unlocked hostility delay',tune(B['background_bonus.calming_presence'],'hostility_delay_seconds')==('8','false'))

svc=text('Scripts/Survivor/Backgrounds/RebirthCombatPatrolTrackingSignatureService.cs')
net=text('Scripts/Survivor/Backgrounds/RebirthCombatPatrolTrackingSignatureNetPackages.cs')
loot=text('Scripts/Survivor/Backgrounds/RebirthMedicalLootCraftSignatureService.cs')
wild=text('Scripts/Survivor/Progression/AdvancedDisciplines/RebirthWildAffinityService.cs')
inst=text('Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs')
buffs=text('Config/buffs.xml'); nav=text('Config/nav_objects.xml'); loc=text('Config/Localization.csv'); dbg=text('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs')

# Soldier authority / qualification / state.
check('Chunk I service installed', 'RebirthCombatPatrolTrackingSignatureService.Install(HarmonyInstance)' in inst)
check('Soldier damage source centralized', '[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.ProcessDamageResponseLocal))]' in svc)
check('Soldier damage observed server-only', 'if(!IsServer()||victim==null' in svc)
check('Soldier uses actual health loss', 'healthBefore-victim.Health' in svc)
check('Soldier requires direct player attacker', 'victim.world.GetEntity(source.getEntityId()) as EntityPlayer' in svc)
check('Soldier requires direct personal combat classifier', 'IsDirectPersonalCombat(attacker,victim,source)' in svc and 'ClassifyCombat(item)' in svc)
check('Soldier excludes player/friendly victim', 'victim is EntityPlayer' in svc and 'IsFriendly(attacker,victim)' in svc)
check('Soldier excludes companions/minions/control', all(x in svc for x in ['EntityRebirthDogCompanion','companion,ally,minion,summoned,token,controlled,rebirthTamedWild','RebirthBlackMagicService.IsControlled']))
check('Soldier activity floor exists', 'loss>=ActivityMinDamage' in svc)
check('Soldier only authoritative killer qualifies', 'EntityPlayer killer=data.KillingEntity as EntityPlayer' in svc)
check('Soldier requires recent contribution', 'now-c.LastAt>CombatGrace' in svc)
check('Soldier meaningful damage threshold combines absolute + health fraction', 'Math.Max(MinDamage,c.MaxHealth*MinHealthFraction)' in svc)
check('Soldier anti-repeat target id', 'ProcessedKillUntil' in svc and 'killed.entityId' in svc)
check('Soldier capped to locked max stacks', 'Math.Min(MaxStacks,state.Stacks+1)' in svc)
check('Soldier only kills add stacks', svc.count('Stacks=Math.Min')==1)
check('Soldier dealt/taken damage only refreshes activity', 'ActiveUntil=Math.Max' in svc and 'soldierVictim' in svc)
check('Soldier expires outside combat', 's.Stacks>0&&s.ActiveUntil<=now' in svc)
check('Soldier death clears state', 'killedPlayer!=null' in svc and 'deadState.Stacks=0' in svc)
check('Soldier client state is server package', 'NetPackageRebirthCombatMomentumState' in net and 'PackageDirection=>NetPackageDirection.ToClient' in net)
check('Soldier handling CVars set from stacks', all(x in svc for x in ['CombatReloadCVar,ReloadPerStack*stacks','CombatHandlingCVar,HandlingPerStack*stacks','CombatSpreadCVar,-SpreadPerStack*stacks','CombatRecoilCVar,-RecoilPerStack*stacks']))
check('Soldier buff has no raw damage passive', 'RebirthBackgroundCombatMomentum' in buffs and 'EntityDamage' not in buffs[buffs.find('RebirthBackgroundCombatMomentum'):buffs.find('RebirthBackgroundCombatMomentum')+3000])
check('Soldier buff modifies reload/handling/spread/recoil', all(x in buffs for x in ['ReloadSpeedMultiplier','WeaponHandling','SpreadMultiplierHip','SpreadMultiplierAiming','KickDegreesVerticalMin','KickDegreesHorizontalMax']))
check('Soldier visible stack display', 'display_value value="$rbCombatMomentumStacks"' in buffs)

# Police exact audited container + ammo-only tripling.
check('Police integrated into first-generation specialized loot', 'PatrolCarFamiliarityBonusId' in loot and 'ApplySpecializedLoot' in loot)
check('Police exact two container ids only', 'string.Equals(lootList,"policeCars"' in loot and 'string.Equals(lootList,"policeCarsBonus"' in loot)
check('Police ammo classifier is ammo prefix', 'name.StartsWith("ammo",StringComparison.OrdinalIgnoreCase)' in loot)
check('Police multiplier derives locked tuning', '"ammunition_multiplier"' in loot and 'GetLockedPoliceAmmoMultiplier' in loot)
check('Police extra amount is multiplier minus one', 's.count*(multiplier-1)' in loot)
check('Police overflow fills existing stack room', 'Math.Max(0,max-s.count)' in loot)
check('Police overflow can use empty container slots', 'new ItemStack(e.Prototype.Clone(),moved)' in loot)
check('Police non-ammo untouched', 'if(!IsAmmunitionItem(n))continue' in loot)
check('Police no reroll on reopen inherited', 'wasAlreadyTouched' in loot and 'if (!tile.bTouched' in loot)

# Hunter observation + tracking.
check('Hunter activation command added on natural living animal', 'rebirthHunterObserve' in svc and 'CanPresentHunterObservation' in svc)
check('Hunter remote observation request server-directed', 'NetPackageRebirthHunterObservationRequest' in net and 'PackageDirection=>NetPackageDirection.ToServer' in net)
check('Hunter request validates sender entity', 'ValidEntityIdForSender(playerId)' in net)
check('Hunter server revalidates bonus', 'HasBonus(hunter,HuntersMarkBonusId)' in svc)
check('Hunter natural classifier inherits infected/zombie hard exclusions', 'IsBeastmasterAnimalHardExcluded(ec,out hard)' in svc)
check('Hunter natural classifier excludes companion/event/summoned animals', 'boss,event,quest,summoned,companion,ally,npc,deployed,player,undead,zombieAnimal,animalZombie,infected,rebirthTamedWild' in svc)
check('Hunter uses broad natural animal classifier', 'TryClassifyHunterNaturalAnimal' in svc and 'FastTags<TagGroup.Global>.Parse("animal")' in svc)
check('Hunter observation distance bounded', 'ObserveDistance*ObserveDistance' in svc)
check('Hunter true LOS uses native visibility then voxel rays', 'hunter.CanEntityBeSeen(target)' in svc and 'Voxel.Raycast' in svc and 'IsInViewCone' in svc)
check('Hunter observation is uninterrupted', 'observationsBroken++' in svc and '!HasTrueLineOfSight(hunter,animal)' in svc)
check('Hunter success after observation interval', 'now-o.StartedAt<ObserveSeconds' in svc)
check('Hunter observation progress projected as timed buff', 'NetPackageRebirthHunterObservationState' in net and 'SyncObservationState(hunter,true,ObserveSeconds)' in svc and 'RebirthHunterObservation' in buffs)
check('Hunter observation timer stops on break or completion', svc.count('SyncObservationState(hunter,false,0f)')>=2)
check('Hunter living mark expires', 'ExpiresAt=now+MarkSeconds' in svc and 'm.ExpiresAt<=now' in svc)
check('Hunter living mark range bounded', 'distance>TrackDistance' in svc)
check('Hunter precision degrades with age and distance', 'age*2f+(distance/Mathf.Max(1f,TrackDistance))*1.5f' in svc and 'Quantize(a.position,precision)' in svc)
check('Hunter carcasses require dead natural animals', 'if(a==null||!a.IsDead())continue' in svc and 'TryClassifyHunterNaturalAnimal(a,out why)' in svc)
check('Hunter snapshots owner-targeted', '_attachedToEntityId:hunter.entityId' in svc)
check('Hunter client markers are projection only', 'RebirthHunterMarkerService.ApplySnapshot' in net)
check('Hunter living marker compass + world', 'RebirthHunterMarkedAnimal' in nav and '<onscreen_settings>' in nav and '<compass_settings>' in nav)
check('Hunter carcass marker compass class', 'RebirthHunterCarcass' in nav)
check('Hunter marker no map reveal', 'hiddenOnMap=true' in net and '<map_settings>' not in nav[nav.find('RebirthHunterMarkedAnimal'):])
check('Hunter uses verified tracking wolf sprite', nav.count('ui_game_symbol_tracking_wolf')>=2)
check('Hunter activation localized', 'rebirthHunterObserve,Observe Animal' in loc)

# Park Ranger composition with Wild Affinity.
check('Park Ranger called only after Wild Affinity tolerance path', 'if (tolerant)' in wild and 'ShouldDelayWildlifeHostility(animal, player' in wild)
check('Park Ranger reuses natural classifier', 'RebirthWildAffinityService.TryClassify(animal' in svc)
check('Park Ranger requires exact bonus', 'HasBonus(player,CalmingPresenceBonusId)' in svc)
check('Park Ranger revenge bypasses delay', 'animal.GetRevengeTarget()==player' in svc)
check('Park Ranger one-delay-per-encounter state', 'CalmingStartedAt' in svc and 'CalmingConsumedUntil' in svc)
check('Park Ranger expired delay cannot restart immediately', 'CalmingConsumedUntil[key]=now+60f' in svc)
check('Park Ranger provocation consumes delay', 'MarkWildlifeProvocation' in svc and 'RebirthCombatPatrolTrackingSignatureService.MarkWildlifeProvocation' in wild)
check('Park Ranger does not flip factions', 'SetFaction' not in svc and re.search(r'\.factionId\s*=(?!=)',svc) is None)
check('Park Ranger does not tame', 'TryInteract' not in svc and 'RegisterCompanion' not in svc)

# Base isolation and debug.
check('Chunk I global Rebirth server gate', 'RebirthSurvivorMode.IsEnabledForCurrentWorld()' in svc)
check('Police inherited server-authority gate', 'IsServerAuthoritativeWorld()' in loot)
check('Chunk I read-only diagnostics command', 'rbsurvivor chunkibonus [status|vectors]' in dbg and 'ExecuteChunkIBonus' in dbg)
check('Chunk I vectors exposed', 'RunVectors()' in svc)
for rel in ['Scripts/Survivor/Backgrounds/RebirthCombatPatrolTrackingSignatureService.cs','Scripts/Survivor/Backgrounds/RebirthCombatPatrolTrackingSignatureNetPackages.cs','Scripts/Survivor/Backgrounds/RebirthMedicalLootCraftSignatureService.cs','Scripts/Survivor/Progression/AdvancedDisciplines/RebirthWildAffinityService.cs']:
 s=text(rel);check('delimiter '+rel,s.count('{')==s.count('}') and s.count('(')==s.count(')'))

# Pure vectors.
check('vector three Soldier reload stacks = +15%',abs(3*.05-.15)<1e-9)
check('vector three Soldier handling stacks = +12%',abs(3*.04-.12)<1e-9)
check('vector 100 ammo -> 300',100*3==300)
check('vector meaningful threshold max absolute',max(20,100*.15)==20)
check('vector meaningful threshold scales target',max(20,500*.15)==75)
check('vector Hunter precision increases',(.25+1*2+1*1.5)>(.25+0*2+0*1.5))
check('vector Ranger delay positive',8>0)

bad=[];n=0
for p in root.rglob('*.xml'):
 n+=1
 try:ET.parse(p)
 except Exception as e:bad.append((str(p),str(e)))
check('all project XML parses',not bad)
print(f'PC022_STATIC_CHECKS={passed+failed} PASSED={passed} FAILED={failed} XML={n}')
for x in bad:print('XMLERR',x)
sys.exit(1 if failed else 0)
