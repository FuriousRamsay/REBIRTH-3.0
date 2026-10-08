#!/usr/bin/env python3
from pathlib import Path
import sys
root=Path(__file__).resolve().parents[2]
files={
'ai':root/'Scripts/Rebirth/NPC/Combat/RebirthNpcTargetingAndCombatAi.cs',
'emergency':root/'Scripts/Rebirth/NPC/Combat/RebirthNpcCombatEmergencySystem.cs',
'lifecycle':root/'Scripts/Rebirth/NPC/Foundation/RebirthNpcLifecycle.cs',
'admin':root/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs'}
text={k:p.read_text(encoding='utf-8') for k,p in files.items()}
tests=[]
def check(name,cond): tests.append((name,bool(cond)))
for token in ['RebirthNpcTargetingService','IRebirthNpcTargetCandidateSource','HasLineOfSight','DefaultRange','RebirthNpcTargetLock','Invalidate']:
 check('targeting_'+token,token in text['ai'])
for token in ['ThreatensOwner','score+=1000f','owner-protection']:
 check('owner_priority_'+token,token in text['ai'])
for token in ['combat.survivor','combat.bandit','combat.dog','combat.panther']:
 check('category_'+token,token in text['ai'])
for token in ['RebirthNpcDamageChannel.Melee','RebirthNpcDamageChannel.Hitscan','RebirthNpcDamageChannel.Projectile','RebirthNpcDamageChannel.Area']:
 check('channel_'+token,token in text['ai'] or token in text['emergency'])
check('friendly_damage_actual_path','RebirthNpcFriendlyFirePolicy.CanDamage' in text['emergency'])
check('projectile_passthrough','ShouldProjectilePassThrough' in text['ai'] and 'hostileCollision' in text['ai'])
check('weapon_use','RequestAuthoritativeUse' in text['ai'] and 'SubmitTacticalOrder' in text['ai'])
check('injury_connection','ApplyDamage(RebirthNpcStableId attackerId' in text['emergency'])
check('lifecycle_init','RebirthNpcCategoryCombatAiRegistry.EnsureInitialized' in text['lifecycle'])
check('lifecycle_services',all(x in text['lifecycle'] for x in ['typeof(RebirthNpcTargetingService).FullName','typeof(RebirthNpcFriendlyFirePolicy).FullName','typeof(RebirthNpcCategoryCombatAiRegistry).FullName']))
check('admin',all(x in text['admin'] for x in ['combatpolicy','combatqualify']))
check('active_presence_filter','Presence==RebirthNpcPresenceState.Active' in text['ai'])
check('allied_filter','AreAllied(actor,target)' in text['ai'])
check('loaded_filter','!c.Loaded||!c.Alive' in text['ai'])
check('structural_braces',all(v.count('{')==v.count('}') for v in text.values()))
for n,ok in tests: print(f'{n}={"PASS" if ok else "FAIL"}')
print(f'result={"PASS" if all(x[1] for x in tests) else "FAIL"} tests={len(tests)}')
sys.exit(0 if all(x[1] for x in tests) else 2)
