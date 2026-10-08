#!/usr/bin/env python3
from __future__ import annotations
import argparse,csv,json,re,sys,xml.etree.ElementTree as ET
from dataclasses import dataclass,field
from pathlib import Path

@dataclass
class Result:
    passed:int=0
    failures:list[str]=field(default_factory=list)
    warnings:list[str]=field(default_factory=list)
    counts:dict=field(default_factory=dict)
    def check(self,name,ok,detail=''):
        if ok:self.passed+=1
        else:self.failures.append(name+(f' :: {detail}' if detail else ''))

def signed(v,n,p):
    v=max(-50,min(100,float(v)))
    return n*(-v/50.0) if v<0 else p*(v/100.0) if v>0 else 0.0

def txt(root,rel):
    p=root/rel
    return p.read_text(encoding='utf-8',errors='replace') if p.exists() else ''

def run(root:Path, matrix_path:Path|None=None, runtime_matrix_path:Path|None=None)->Result:
    r=Result()
    ss=ET.parse(root/'Config/_Survivor/skill_sources.xml').getroot()
    prog=ET.parse(root/'Config/_Survivor/progression.xml').getroot()
    accept=ss.find('release_acceptance')
    r.check('release acceptance authoring exists',accept is not None)
    if accept is None:return r
    bands=[int(x) for x in accept.get('bands','').split(',') if x.strip()]
    r.check('representative bands exact',bands==[-50,-25,0,25,50,75,100],str(bands))
    r.check('runtime evidence required',accept.get('runtime_evidence_required')=='true')
    r.check('debug release default false',accept.get('debug_mutation_default')=='false')
    r.check('network roles exact',accept.get('required_network_roles')=='host,p2p_client,dedicated_client')
    required_persistence=set((accept.get('required_persistence_cases') or '').split(','))
    for case in ['logout_login','profile_reuse','new_world_same_name','delete_recreate_same_name','schema_migration']:
        r.check('persistence case '+case,case in required_persistence)

    skills=prog.findall('.//skill'); ids=[s.get('id') for s in skills]
    r.check('42 active skills',len(ids)==42 and len(set(ids))==42,f'count={len(ids)} unique={len(set(ids))}')
    for s in skills:
        r.check('runtime bounds '+s.get('id','?'),s.get('min')=='-50' and s.get('max')=='100')

    # Signed curve audit at all representative bands.
    curves=[]
    def add(group,label,nk,pk):
        e=ss.find(group); r.check('curve group '+group,e is not None)
        if e is None:return
        n=float(e.get(nk)); p=float(e.get(pk)); curves.append((label,n,p))
    add('wave_a','lockpick_time','lockpick_negative_time','lockpick_positive_time')
    add('wave_a','lockpick_break','lockpick_negative_break','lockpick_positive_break')
    add('wave_a','barter','barter_negative','barter_positive')
    add('wave_a','athletics_jump','athletics_negative_jump','athletics_positive_jump')
    add('wave_a','athletics_stamina','athletics_negative_stamina','athletics_positive_stamina')
    add('wave_a','stealth_noise','stealth_negative_noise','stealth_positive_noise')
    add('wave_a','armor_burden','armor_negative_burden','armor_positive_recovery')
    add('resource_field','resource_harvest','resource_harvest_negative','resource_harvest_positive')
    add('resource_field','farming_harvest','farming_harvest_negative','farming_harvest_positive')
    add('resource_field','animal_harvest','animal_harvest_negative','animal_harvest_positive')
    add('weapon_family','melee_stamina','melee_stamina_negative','melee_stamina_positive')
    add('weapon_family','melee_speed','melee_speed_negative','melee_speed_positive')
    add('weapon_family','ranged_reload','ranged_reload_negative','ranged_reload_positive')
    add('weapon_family','ranged_handling','ranged_handling_negative','ranged_handling_positive')
    add('weapon_family','ranged_spread','ranged_spread_negative','ranged_spread_positive')
    add('weapon_family','ranged_recoil','ranged_recoil_negative','ranged_recoil_positive')
    add('service_crafting','craft_time','craft_time_negative','craft_time_positive')
    add('service_crafting','repair_time','repair_time_negative','repair_time_positive')
    add('service_crafting','repair_amount','repair_amount_negative','repair_amount_positive')
    add('service_crafting','mechanics_repair','mechanics_repair_negative','mechanics_repair_positive')
    add('service_crafting','medicine_reserve','medicine_reserve_negative','medicine_reserve_positive')
    add('complex_systems','explosives_damage','explosives_damage_negative','explosives_damage_positive')
    add('complex_systems','drone_damage','drone_damage_negative','drone_damage_positive')
    add('complex_systems','drone_stun_cycle','drone_stun_cycle_negative','drone_stun_cycle_positive')
    max_hand=float(accept.get('max_abs_handling_delta')); max_dmg=float(accept.get('max_abs_damage_delta'))
    matrix=[]
    for label,n,p in curves:
        vals=[signed(b,n,p) for b in bands]
        r.check('curve neutral '+label,abs(vals[bands.index(0)])<1e-9,str(vals))
        r.check('curve finite bounded '+label,all(abs(v)<=max_hand+1e-9 for v in vals),str(vals))
        # linear signed curves must be monotonic between -50..0 and 0..100 in magnitude from neutral.
        r.check('curve linear negative '+label,abs(vals[1]-n*.5)<1e-9,f'{vals[1]} vs {n*.5}')
        r.check('curve linear positive '+label,abs(vals[3]-p*.25)<1e-9,f'{vals[3]} vs {p*.25}')
        for b,v in zip(bands,vals): matrix.append((label,b,v,n,p))
    for label in ['explosives_damage','drone_damage']:
        vals=[row[2] for row in matrix if row[0]==label]
        r.check('damage curve tighter bound '+label,all(abs(v)<=max_dmg+1e-9 for v in vals),str(vals))
    r.counts['signed_curves']=len(curves); r.counts['balance_rows']=len(matrix)

    # Tracking's non-linear but bounded authored curve.
    rf=ss.find('resource_field')
    td=[float(rf.get(k)) for k in ['tracking_min_distance','tracking_neutral_distance','tracking_max_distance']]
    ta=[float(rf.get(k)) for k in ['tracking_negative_acquire_seconds','tracking_neutral_acquire_seconds','tracking_positive_acquire_seconds']]
    r.check('tracking distance ordered',0<td[0]<td[1]<td[2],str(td))
    r.check('tracking acquisition ordered',ta[0]>ta[1]>ta[2]>0,str(ta))

    # LBD award ceiling and anti-grind/source-legitimacy audit.
    max_award=float(accept.get('max_single_award'))
    awards=ss.find('awards'); nums=[]
    for k,v in awards.attrib.items():
        if any(x in k for x in ['award','max','completion','install','replace','hotwire','repair','treatment','output']):
            try: nums.append((k,float(v)))
            except: pass
    for group in ['wave_a','resource_field','complex_systems']:
        e=ss.find(group)
        for k,v in e.attrib.items():
            if 'award' in k:
                try: nums.append((k,float(v)))
                except: pass
    r.check('single authored awards bounded',all(0<=v<=max_award+1e-9 for _,v in nums),str([x for x in nums if x[1]>max_award]))
    r.counts['award_fields']=len(nums)

    award=txt(root,'Scripts/Survivor/Progression/RebirthSkillAwardService.cs')
    router=txt(root,'Scripts/Survivor/Progression/RebirthSkillEventRouter.cs')
    wave=txt(root,'Scripts/Survivor/Progression/RebirthSkillWaveAService.cs')
    fieldsvc=txt(root,'Scripts/Survivor/Progression/RebirthResourceFieldSkillService.cs')
    complexsvc=txt(root,'Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs')
    debug=txt(root,'Scripts/Survivor/Debug/RebirthSurvivorDebug.cs')
    cmd=txt(root,'Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs')
    releasecs=txt(root,'Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptance.cs')
    vectorcs=txt(root,'Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptanceVectorHarness.cs')
    r.check('server award authority', 'player.world.IsRemote()' in award and 'RebirthWorldCharacterRepository.IsServerAuthority' in award)
    r.check('central anti repeat dictionary','LastAwards' in award and 'minimumInterval>0f' in award)
    r.check('combat meaningful damage','response.Strength<=0' in router and '"combat:" + target.entityId' in router or '"combat:"+target.entityId' in router)
    r.check('combat per target anti repeat','0.05f' in router and 'TryAward(player,skillId' in router.replace(' ',''))
    r.check('shotgun window anti pellet spam','ClampCombatAward' in router and 'ShotgunWindowMax' in award)
    r.check('harvest completion not hit spam','OnBlockHarvestCompleted' in router and 'OnBlockDestroyedBy' in txt(root,'Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs'))
    r.check('barter anti loop','BarterGlobalSeconds' in wave and 'BarterRepeatSeconds' in wave and 'BarterLoopSeconds' in wave)
    r.check('athletics traversal threshold','AthleticsDistance' in wave and 'player.AttachedToEntity != null' in wave)
    r.check('stealth real threat gate','StealthThreatRadius' in wave and 'EntityZombie' in wave)
    r.check('armor burden movement gate','ArmorDistance' in wave and 'OnArmoredCombat' in wave)
    r.check('animal carcass guard','AnimalProcessingRepeatSeconds' in fieldsvc and 'IsDead()' in fieldsvc)
    r.check('tracking repeat guard','TrackingRepeatSeconds' in fieldsvc)
    r.check('explosion block one award','ChangedBlockPositions.Count <= 0' in complexsvc and '"explosion:block"' in complexsvc and 'foreach' not in complexsvc[complexsvc.find('OnExplosionBlocksCompleted'):complexsvc.find('OnDroneShockCompleted')])
    r.check('drone shock repeat guard','DroneShockRepeatSeconds' in complexsvc)
    r.check('debug gate reset false','public static bool Enabled { get; private set; }' in debug and 'Enabled = false;' in debug)
    r.check('no client trusted award packet','NetPackage' not in complexsvc and 'ValidEntityIdForSender' in txt(root,'Scripts/Survivor/Progression/RebirthSkillWaveANetPackages.cs'))

    # Release diagnostics and explicit blockers.
    r.check('acceptance debug command','mode=="acceptance"' in cmd and 'BuildBalanceBands' in cmd and 'BuildAuthorityReport' in cmd)
    r.check('acceptance csharp diagnostic',bool(releasecs) and 'releaseApproved=False' in releasecs and 'runtimeEvidenceRequired=True' in releasecs)
    r.check('acceptance csharp vectors',bool(vectorcs) and 'releaseApproved=False' in vectorcs)
    for token in ['ConstructionWorkActionEnabled','ElectricalWorkmanshipEnabled','ChemistryPayloadPotencyEnabled','DeployableTurretPerformanceScalingEnabled']:
        r.check('explicit complex gate '+token,token in releasecs)

    # Static release state is intentionally BLOCKED until real runtime evidence exists.
    blockers=['compile','host','p2p_client','dedicated_client','performance','construction_electrical']
    r.counts['runtime_blockers']=len(blockers)
    r.warnings.append('RELEASE_NOT_APPROVED: runtime evidence is required; this environment cannot compile/launch 7DTD or produce host/P2P/dedicated evidence.')
    r.warnings.append('ACTIVE_SKILL_BLOCKER: Construction and Electrical remain active Skill definitions but their complex runtime systems are explicitly disabled pending source/runtime acceptance.')

    if matrix_path:
        matrix_path.parent.mkdir(parents=True,exist_ok=True)
        with matrix_path.open('w',newline='',encoding='utf-8') as f:
            w=csv.writer(f); w.writerow(['effect','skill_band','delta','negative_at_minus50','positive_at_100'])
            for row in matrix:w.writerow(row)
    if runtime_matrix_path:
        runtime_matrix_path.parent.mkdir(parents=True,exist_ok=True)
        rows=[
          ('compile_against_installed_managed','compile','REQUIRED_RUNTIME','BLOCKED','No compiler/game references in this environment'),
          ('new_character_creation','creator','REQUIRED_RUNTIME','PENDING','Host + P2P + dedicated client'),
          ('profile_reuse_across_worlds','persistence','REQUIRED_RUNTIME','PENDING','Verify stable profile identity and no progression leakage'),
          ('edit_review_flow','creator','REQUIRED_RUNTIME','PENDING','Full UI navigation/scroll/click smoke'),
          ('schema_migration','persistence','STATIC_PLUS_RUNTIME','PENDING','Static vectors pass; live save migration still required'),
          ('logout_login','persistence','REQUIRED_RUNTIME','PENDING','Origin/current Skills/Trait/Diet/condition identity'),
          ('delete_recreate_same_world_name','persistence','REQUIRED_RUNTIME','PENDING','No stale world-character/metabolism state'),
          ('host_authority','network','REQUIRED_RUNTIME','PENDING','Progression server-owned'),
          ('p2p_client_authority','network','REQUIRED_RUNTIME','PENDING','Joined client cannot self-award'),
          ('dedicated_client_authority','network','REQUIRED_RUNTIME','PENDING','Dedicated parity'),
          ('lbd_exploit_matrix','progression','STATIC_PLUS_RUNTIME','PENDING','Static guards pass; live macro/repeat tests required'),
          ('debug_release_behavior','release','STATIC_PLUS_RUNTIME','PENDING','Gate defaults false; live release build smoke'),
          ('performance_60s_observation','performance','REQUIRED_RUNTIME','PENDING','Frame/GC/server tick observation'),
          ('construction_runtime','skill.construction','REQUIRED_RUNTIME','BLOCKED','Complex Work Action intentionally disabled'),
          ('electrical_runtime','skill.electrical','REQUIRED_RUNTIME','BLOCKED','Persistent workmanship intentionally disabled'),
          ('chemistry_payload_potency','skill.chemistry','OPTIONAL_DEFERRED','DEFERRED','Chunk 8 Chemistry craft path active; payload metadata intentionally disabled'),
          ('turret_performance_scaling','skill.deployable_turrets','OPTIONAL_DEFERRED','DEFERRED','Owner-attributed LBD active; performance scaling intentionally disabled'),
        ]
        with runtime_matrix_path.open('w',newline='',encoding='utf-8') as f:
            w=csv.writer(f);w.writerow(['test','area','evidence_type','status','acceptance_note']);w.writerows(rows)
        r.counts['runtime_matrix_rows']=len(rows)
    return r

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);ap.add_argument('--matrix',type=Path);ap.add_argument('--runtime-matrix',type=Path);ap.add_argument('--json',type=Path);args=ap.parse_args()
    res=run(args.root.resolve(),args.matrix,args.runtime_matrix); total=res.passed+len(res.failures); status='PASS' if not res.failures else 'FAIL'
    print(f'REBIRTH Survivor Chunk 11 release-acceptance static vectors: {status}')
    print(f'passed={res.passed}/{total}')
    print('release_approved=False')
    print('runtime_evidence_required=True')
    print('compile_validation_claimed=False')
    for w in res.warnings:print('WARN: '+w)
    for f in res.failures:print('FAIL: '+f)
    if args.json:
        args.json.parent.mkdir(parents=True,exist_ok=True);args.json.write_text(json.dumps({'status':status,'passed':res.passed,'failed':len(res.failures),'counts':res.counts,'warnings':res.warnings,'failures':res.failures,'release_approved':False,'runtime_evidence_required':True,'compile_validation_claimed':False},indent=2)+'\n')
    return 0 if not res.failures else 1
if __name__=='__main__':sys.exit(main())
