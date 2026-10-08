#!/usr/bin/env python3
from __future__ import annotations
import argparse, sys, xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

MELEE=[
'skill.spears','skill.clubs','skill.swords','skill.axes','skill.batons','skill.hammers','skill.knives','skill.scythes','skill.knuckles','skill.unarmed']
RANGED=[
'skill.archery','skill.pistols','skill.revolvers','skill.heavy_handguns','skill.shotguns','skill.assault_rifles','skill.tactical_rifles','skill.long_range_rifles']
ALL=MELEE+RANGED
SPECIAL={'gunHandgunT3SMG5':'skill.pistols','gunMGT3M60':'skill.assault_rifles','gunMGT2TacticalAR':'skill.tactical_rifles','gunRifleT3SniperRifle':'skill.long_range_rifles'}

@dataclass
class Result:
    passed:int=0
    failures:list[str]=field(default_factory=list)
    def check(self,name,ok,detail=''):
        if ok:self.passed+=1
        else:self.failures.append(name+(f' :: {detail}' if detail else ''))

def signed(v,n,p):
    v=max(-50,min(100,v))
    return n*(-v/50) if v<0 else p*(v/100) if v>0 else 0.0

def run(root:Path)->Result:
    r=Result()
    cfg=ET.parse(root/'Config/_Survivor/skill_sources.xml').getroot()
    wf=cfg.find('weapon_family')
    r.check('weapon_family authoring exists',wf is not None)
    if wf is None:return r
    vals={k:float(wf.get(k)) for k in [
        'passive_sync_seconds','melee_stamina_negative','melee_stamina_positive','melee_speed_negative','melee_speed_positive',
        'ranged_reload_negative','ranged_reload_positive','ranged_handling_negative','ranged_handling_positive',
        'ranged_spread_negative','ranged_spread_positive','ranged_recoil_negative','ranged_recoil_positive']}
    r.check('passive sync throttled',.10<=vals['passive_sync_seconds']<=1.0)
    for prefix,nk,pk,wantneg,wantpos in [
        ('melee stamina','melee_stamina_negative','melee_stamina_positive',1,-1),
        ('melee speed','melee_speed_negative','melee_speed_positive',-1,1),
        ('ranged reload','ranged_reload_negative','ranged_reload_positive',-1,1),
        ('ranged handling','ranged_handling_negative','ranged_handling_positive',-1,1),
        ('ranged spread','ranged_spread_negative','ranged_spread_positive',1,-1),
        ('ranged recoil','ranged_recoil_negative','ranged_recoil_positive',1,-1)]:
        weak=signed(-50,vals[nk],vals[pk]); zero=signed(0,vals[nk],vals[pk]); strong=signed(100,vals[nk],vals[pk])
        r.check(prefix+' weak sign',weak*wantneg>0,f'{weak}')
        r.check(prefix+' zero neutral',abs(zero)<1e-9)
        r.check(prefix+' strong sign',strong*wantpos>0,f'{strong}')
        r.check(prefix+' bounded',abs(weak)<=.25 and abs(strong)<=.50,f'{weak},{strong}')

    maps={e.get('skill'):e for e in cfg.findall('./combat/map')}
    r.check('all 18 weapon maps authored',all(x in maps for x in ALL),f'missing={[x for x in ALL if x not in maps]}')
    r.check('exact 10 melee ids',len(MELEE)==10 and len(set(MELEE))==10)
    r.check('exact 8 ranged ids',len(RANGED)==8 and len(set(RANGED))==8)
    for item,skill in SPECIAL.items():
        names=(maps[skill].get('item_names') or '').split(',')
        r.check('special route '+item,item in names,f'{skill} names={names}')

    prog=ET.parse(root/'Config/_Survivor/progression.xml').getroot()
    skills={e.get('id'):e for e in prog.findall('.//skill')}
    r.check('18 skills defined',all(x in skills for x in ALL))
    r.check('signed bounds retained',all(skills[x].get('min')=='-50' and skills[x].get('max')=='100' for x in ALL))
    r.check('qualified hit LBD retained',all('qualified' in (skills[x].get('lbd_source') or '').lower() for x in ALL))

    buffs=ET.parse(root/'Config/buffs.xml').getroot()
    buff=buffs.find(".//buff[@name='RebirthSurvivorWeaponFamilyPassives']")
    r.check('weapon passive buff exists',buff is not None)
    if buff is not None:
        p= buff.findall('.//passive_effect')
        names=[x.get('name') for x in p]
        expected={'StaminaLoss','AttacksPerMinute','ReloadSpeedMultiplier','WeaponHandling','SpreadMultiplierHip','SpreadMultiplierAiming','KickDegreesVerticalMin','KickDegreesVerticalMax','KickDegreesHorizontalMin','KickDegreesHorizontalMax'}
        r.check('native handling surfaces exact',set(names)==expected,f'actual={sorted(set(names))}')
        forbidden={'EntityDamage','BlockDamage','RoundsPerMinute','MagazineSize','RoundRayCount','BurstRoundCount','ProjectileVelocity'}
        r.check('forbidden output mechanics absent',not(set(names)&forbidden),f'found={set(names)&forbidden}')
        r.check('melee primary secondary stamina',sum(1 for x in p if x.get('name')=='StaminaLoss')==2 and {x.get('tags') for x in p if x.get('name')=='StaminaLoss'}=={'primary','secondary'})
        r.check('ranged spread only hip aim',{x.get('name') for x in p if (x.get('name') or '').startswith('SpreadMultiplier')}=={'SpreadMultiplierHip','SpreadMultiplierAiming'})
        r.check('four recoil axes',sum(1 for x in p if (x.get('name') or '').startswith('KickDegrees'))==4)
        r.check('buff hidden',buff.get('hidden')=='true')

    service=(root/'Scripts/Survivor/Progression/RebirthWeaponFamilySkillService.cs').read_text(errors='replace')
    installer=(root/'Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs').read_text(errors='replace')
    router=(root/'Scripts/Survivor/Progression/RebirthSkillEventRouter.cs').read_text(errors='replace')
    award=(root/'Scripts/Survivor/Progression/RebirthSkillAwardService.cs').read_text(errors='replace')
    debug=(root/'Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs').read_text(errors='replace')
    r.check('service installed','RebirthWeaponFamilySkillService.Install()' in installer)
    r.check('client snapshot bridge','RebirthSurvivorClientState.GetOwnerStateSnapshot()' in service)
    r.check('server record bridge','RebirthWorldCharacterService.TryGet(player, out r)' in service)
    r.check('held item classifier','ClassifyCombat(held)' in service)
    r.check('non chunk7 skills excluded',all(x in service for x in ['skill.deployable_turrets','skill.drone_operations','skill.explosives']) is False, 'exclusions are by whitelist')
    # explicit whitelist is the exclusion contract; these strings must not be in either HashSet.
    r.check('deployable/drone/explosive not whitelisted',all(x not in service.split('private static bool installed')[0] for x in ['skill.deployable_turrets','skill.drone_operations','skill.explosives']))
    r.check('existing combat router retained','ClassifyCombat(response.Source.AttackingItem)' in router and 'CombatPer100Damage' in router)
    r.check('shotgun anti pellet window retained','ClampCombatAward' in router and 'ShotgunWindowMax' in award)
    r.check('no trigger pull LBD in weapon service','TryAward(' not in service)
    r.check('no direct damage authoring in service','EntityDamage' not in service and 'RoundsPerMinute' not in service and 'MagazineSize' not in service)
    r.check('weapon debug command','mode=="weapons"' in debug and 'RebirthWeaponFamilySkillService.BuildDebugReport' in debug)
    r.check('weapon vector command','RebirthWeaponFamilySkillVectorHarness.RunAll()' in debug)
    return r

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]); a=ap.parse_args()
    r=run(a.root.resolve()); total=r.passed+len(r.failures); status='PASS' if not r.failures else 'FAIL'
    print(f'REBIRTH Survivor Chunk 7 Weapon-family vectors: {status}')
    print(f'passed={r.passed}/{total} failed={len(r.failures)} compile_validation_claimed=False')
    for x in r.failures: print('FAIL',x)
    return 0 if not r.failures else 1
if __name__=='__main__':sys.exit(main())
