#!/usr/bin/env python3
from __future__ import annotations
import argparse, sys, xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

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

def txt(root:Path,rel:str)->str:
    p=root/rel
    return p.read_text(encoding='utf-8',errors='replace') if p.exists() else ''

def run(root:Path)->Result:
    r=Result()
    sources=ET.parse(root/'Config/_Survivor/skill_sources.xml').getroot()
    complex_node=sources.find('complex_systems')
    r.check('complex authoring exists',complex_node is not None)
    if complex_node is None:return r
    keys=['passive_sync_seconds','explosives_damage_negative','explosives_damage_positive','explosives_block_award','drone_damage_negative','drone_damage_positive','drone_stun_cycle_negative','drone_stun_cycle_positive','drone_shock_award','drone_shock_repeat_seconds']
    for k in keys:r.check('complex attr '+k,complex_node.get(k) is not None)
    f=lambda k:float(complex_node.get(k))
    eweak=signed(-50,f('explosives_damage_negative'),f('explosives_damage_positive'))
    ezero=signed(0,f('explosives_damage_negative'),f('explosives_damage_positive'))
    estrong=signed(100,f('explosives_damage_negative'),f('explosives_damage_positive'))
    r.check('explosives weak penalty',-.35<=eweak<0,str(eweak));r.check('explosives neutral',abs(ezero)<1e-9);r.check('explosives strong bonus',0<estrong<=.35,str(estrong))
    dweak=signed(-50,f('drone_damage_negative'),f('drone_damage_positive')); dzero=signed(0,f('drone_damage_negative'),f('drone_damage_positive')); dstrong=signed(100,f('drone_damage_negative'),f('drone_damage_positive'))
    r.check('drone damage weak penalty',-.35<=dweak<0,str(dweak));r.check('drone damage neutral',abs(dzero)<1e-9);r.check('drone damage strong bonus',0<dstrong<=.35,str(dstrong))
    cweak=signed(-50,f('drone_stun_cycle_negative'),f('drone_stun_cycle_positive')); czero=signed(0,f('drone_stun_cycle_negative'),f('drone_stun_cycle_positive')); cstrong=signed(100,f('drone_stun_cycle_negative'),f('drone_stun_cycle_positive'))
    r.check('drone stun weak slower',0<cweak<=.35,str(cweak));r.check('drone stun neutral',abs(czero)<1e-9);r.check('drone stun strong faster',-.35<=cstrong<0,str(cstrong))
    r.check('explosive block award bounded',0<f('explosives_block_award')<=.35);r.check('drone shock award bounded',0<f('drone_shock_award')<=.35);r.check('drone repeat meaningful',f('drone_shock_repeat_seconds')>=1)

    maps={e.get('skill'):e for e in sources.findall('./combat/map')}
    for sid in ['skill.explosives','skill.deployable_turrets','skill.drone_operations']:
        r.check('combat map '+sid,sid in maps)
    r.check('turret exact items','gunBotT1JunkSledge' in (maps['skill.deployable_turrets'].get('item_names') or '') and 'gunBotT2JunkTurret' in (maps['skill.deployable_turrets'].get('item_names') or ''))
    r.check('drone exact item','gunBotT3JunkDrone' in (maps['skill.drone_operations'].get('item_names') or ''))

    buffs=ET.parse(root/'Config/buffs.xml').getroot()
    cb=buffs.find(".//buff[@name='RebirthSurvivorComplexSkillPassives']")
    r.check('complex passive buff exists',cb is not None)
    effects=[(e.get('name'),e.get('operation'),e.get('value'),e.get('tags','')) for e in cb.findall('.//passive_effect')] if cb is not None else []
    names=[x[0] for x in effects]
    r.check('native explosion entity passive','ExplosionEntityDamage' in names)
    r.check('native explosion block passive','ExplosionBlockDamage' in names)
    r.check('drone native entity damage passive',any(x[0]=='EntityDamage' and 'DroneOperations' in x[3] for x in effects))
    r.check('all complex passives percent add',all(x[1]=='perc_add' for x in effects),str(effects))
    r.check('no construction passive',not any('Construction' in str(x) for x in effects))
    r.check('no electrical passive',not any('Electrical' in str(x) for x in effects))
    r.check('no chemistry potency passive',not any('Chemistry' in str(x) or 'potency' in str(x).lower() for x in effects))

    svc=txt(root,'Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs')
    patches=txt(root,'Scripts/Survivor/Progression/RebirthComplexSkillSystemPatches.cs')
    router=txt(root,'Scripts/Survivor/Progression/RebirthSkillEventRouter.cs')
    installer=txt(root,'Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs')
    config=txt(root,'Scripts/Survivor/Progression/RebirthProgressionRuntimeConfig.cs')
    debug=txt(root,'Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs')
    vector=txt(root,'Scripts/Survivor/Progression/RebirthComplexSkillSystemVectorHarness.cs')
    prog=ET.parse(root/'Config/_Survivor/progression.xml').getroot(); skills={e.get('id'):e for e in prog.findall('.//skill')}

    r.check('complex service exists',bool(svc))
    r.check('explosives sync cvar','ExplosivesDamageCVar' in svc and 'skill.explosives' in svc and 'SyncNativePassiveEffects' in svc)
    r.check('drone damage sync cvar','DroneDamageCVar' in svc and 'skill.drone_operations' in svc)
    r.check('server authority gate','connection.IsServer' in svc and 'RebirthWorldCharacterRepository.IsServerAuthority' in svc)
    r.check('explosion changed blocks gate','ChangedBlockPositions.Count <= 0' in svc)
    r.check('explosion classified exact','ClassifyCombat(sourceItem)' in svc and 'skill.explosives' in svc)
    r.check('explosion award once fixed','ExplosivesBlockAward' in svc and '"explosion:block"' in svc)
    r.check('no per block award loop','foreach' not in svc[svc.find('OnExplosionBlocksCompleted'):svc.find('OnDroneShockCompleted')])
    r.check('drone entity reflection','AccessTools.Field(typeof(DroneWeapons.Weapon), "entity")' in svc)
    r.check('drone cooldown reflection','AccessTools.Field(typeof(DroneWeapons.Weapon), "cooldownTimer")' in svc)
    r.check('drone owner resolution','drone.belongsPlayerId' in svc and 'GetEntity(drone.belongsPlayerId)' in svc)
    r.check('drone real shocked gate','HasBuff("buffShocked")' in svc)
    r.check('drone repeat anti grind','DroneShockRepeatSeconds' in svc and '"drone-shock:" + target.entityId' in svc)
    r.check('native stun duration untouched','SetBuffDuration' not in svc and 'buffShocked"' in svc)
    r.check('remote device detection uses creator','source.CreatorEntityId' in svc and 'creator is EntityTurret || creator is EntityDrone' in svc)
    r.check('armor remote suppression','IsRemoteOwnedDeviceSource(target.world, response.Source)' in router and 'OnArmoredCombat(player)' in router)
    r.check('combat lbd remains central','ClassifyCombat(response.Source.AttackingItem)' in router and 'TryAward(player,skillId' in router.replace(' ',''))
    r.check('no turret-specific client packet','NetPackage' not in svc and 'NetPackage' not in patches)

    r.check('explosion harmony patch','typeof(Explosion)' in patches and 'AttackBlocks' in patches and 'OnExplosionBlocksCompleted' in patches)
    r.check('drone stun harmony patch','typeof(DroneWeapons.StunBeamWeapon)' in patches and 'Fire' in patches and 'OnDroneShockCompleted' in patches)
    r.check('service installed','RebirthComplexSkillSystemService.Install()' in installer)
    r.check('complex patches installed','RebirthSurvivorExplosiveBlockSkillPatch' in installer and 'RebirthSurvivorDroneStunSkillPatch' in installer)
    r.check('runtime config parses complex','root.Element("complex_systems")' in config and 'ExplosivesDamageNegative' in config and 'DroneStunCyclePositive' in config)
    r.check('debug complex command','mode=="complex"' in debug and 'RebirthComplexSkillSystemService.BuildDebugReport' in debug)
    r.check('debug vectors command','RebirthComplexSkillSystemVectorHarness.RunAll()' in debug)
    r.check('csharp vector harness exists','construction gate closed' in vector and 'chemistry payload gate closed' in vector)

    r.check('construction hard gate false','public const bool ConstructionWorkActionEnabled = false;' in svc)
    r.check('electrical hard gate false','public const bool ElectricalWorkmanshipEnabled = false;' in svc)
    r.check('chem payload hard gate false','public const bool ChemistryPayloadPotencyEnabled = false;' in svc)
    r.check('turret scaling hard gate false','public const bool DeployableTurretPerformanceScalingEnabled = false;' in svc)
    r.check('no work action implementation','WaitForSeconds' not in svc and 'IEnumerator' not in svc and 'ConstructionWorkAction' not in patches)
    r.check('no electrical persistence implementation','TileEntityPowered' not in svc and 'PowerItem' not in svc and 'ElectricalWorkmanship' not in patches)
    r.check('no item metadata potency','ItemValue metadata' not in svc and 'CustomData' not in svc and 'Metadata' not in svc)
    r.check('no chemistry explosive stamping','skill.chemistry' not in svc and 'ChemistryPayloadPotencyEnabled = false' in svc)

    for sid in ['skill.explosives','skill.deployable_turrets','skill.drone_operations','skill.construction','skill.electrical','skill.chemistry']:
        r.check('skill defined '+sid,sid in skills)
        if sid in skills:r.check('skill bounds '+sid,skills[sid].get('min')=='-50' and skills[sid].get('max')=='100')
    r.check('explosives feasibility honest','NEW REBIRTH SKILL HOOK' in skills['skill.explosives'].get('feasibility',''))
    r.check('turret feasibility honest','performance scaling deferred' in skills['skill.deployable_turrets'].get('feasibility','').lower())
    r.check('drone feasibility honest','stun-cycle/shock LBD implemented preboot' in skills['skill.drone_operations'].get('feasibility',''))
    r.check('construction feasibility deferred','SOURCE-AUDIT' in skills['skill.construction'].get('feasibility','') and 'disabled' in skills['skill.construction'].get('feasibility',''))
    r.check('electrical feasibility deferred','SOURCE-AUDIT' in skills['skill.electrical'].get('feasibility','') and 'disabled' in skills['skill.electrical'].get('feasibility',''))
    r.check('chem potency feasibility deferred','payload-potency' in skills['skill.chemistry'].get('feasibility','') and 'disabled' in skills['skill.chemistry'].get('feasibility',''))
    return r

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);a=ap.parse_args()
    r=run(a.root.resolve());total=r.passed+len(r.failures);status='PASS' if not r.failures else 'FAIL'
    print(f'REBIRTH Survivor Chunk 10 Complex Skill vectors: {status}')
    print(f'passed={r.passed}/{total} failed={len(r.failures)} compile_validation_claimed=False')
    for x in r.failures:print('FAIL',x)
    return 0 if not r.failures else 1
if __name__=='__main__':sys.exit(main())
