#!/usr/bin/env python3
from __future__ import annotations
import argparse, sys, xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

@dataclass
class Result:
    passed:int=0
    failures:list[str]=field(default_factory=list)
    def check(self,name:str,ok:bool,detail:str=""):
        if ok:self.passed+=1
        else:self.failures.append(name+(f" :: {detail}" if detail else ""))

def f(e,name): return float(e.get(name))
def signed(v,n,p):
    v=max(-50,min(100,v))
    return n*(-v/50) if v<0 else p*(v/100) if v>0 else 0.0

def tracking_tier(v):
    v=max(-50,min(100,v))
    if v<=0:return 1
    if v<=25:return 2
    if v<=50:return 3
    if v<=75:return 4
    return 5

def lerp(a,b,t): return a+(b-a)*t

def tracking_distance(v,e):
    v=max(-50,min(100,v)); mn=f(e,'tracking_min_distance'); z=f(e,'tracking_neutral_distance'); mx=f(e,'tracking_max_distance')
    if v<0:return lerp(z,mn,-v/50)
    if v>0:return lerp(z,mx,v/100)
    return z

def acquire(v,e):
    v=max(-50,min(100,v)); neg=f(e,'tracking_negative_acquire_seconds'); z=f(e,'tracking_neutral_acquire_seconds'); pos=f(e,'tracking_positive_acquire_seconds')
    if v<0:return lerp(z,neg,-v/50)
    if v>0:return lerp(z,pos,v/100)
    return z

def run(root:Path)->Result:
    r=Result()
    cfg=ET.parse(root/'Config/_Survivor/skill_sources.xml').getroot()
    rf=cfg.find('resource_field')
    r.check('resource_field authoring exists',rf is not None)
    if rf is None:return r

    # Signed yield endpoints and zero-neutral contract.
    groups=[
        ('resource',f(rf,'resource_harvest_negative'),f(rf,'resource_harvest_positive')),
        ('farming',f(rf,'farming_harvest_negative'),f(rf,'farming_harvest_positive')),
        ('animal',f(rf,'animal_harvest_negative'),f(rf,'animal_harvest_positive')),
    ]
    for name,neg,pos in groups:
        r.check(f'{name} weak reduces yield',signed(-50,neg,pos)<0)
        r.check(f'{name} zero neutral',abs(signed(0,neg,pos))<1e-8)
        r.check(f'{name} strong raises yield',signed(100,neg,pos)>0)
        r.check(f'{name} weak bounded',signed(-50,neg,pos)>=-.25)
        r.check(f'{name} strong bounded',signed(100,neg,pos)<=.35)

    # Tracking skill curve: weak still has easy-animal baseline, positive expands tiers/range.
    dw,dz,ds=tracking_distance(-50,rf),tracking_distance(0,rf),tracking_distance(100,rf)
    aw,az,ass=acquire(-50,rf),acquire(0,rf),acquire(100,rf)
    r.check('tracking distances ordered',dw<dz<ds,f'{dw},{dz},{ds}')
    r.check('tracking strong native range ceiling',abs(ds-150)<=1e-6 and ds<=150)
    r.check('tracking weak retains short baseline',dw>=25 and tracking_tier(-50)==1)
    r.check('tracking zero easy tier',tracking_tier(0)==1)
    r.check('tracking positive tiers ordered',[tracking_tier(x) for x in [1,26,51,76,100]]==[2,3,4,5,5])
    r.check('tracking acquisition improves',aw>az>ass,f'{aw},{az},{ass}')
    r.check('tracking acquisition never instant',ass>=.5)
    r.check('tracking crouch spam repeat guard',f(rf,'tracking_repeat_seconds')>=30)
    r.check('tracking follow distance meaningful',f(rf,'tracking_follow_distance')>=10)
    r.check('tracking awards bounded',0<f(rf,'tracking_acquire_award')<=.5 and 0<f(rf,'tracking_follow_award')<=.25)
    r.check('resource sample throttled',f(rf,'sample_seconds')>=.5 and f(rf,'passive_sync_seconds')>=.25)
    r.check('resource teleport guard',3<=f(rf,'max_sample_distance')<=20)

    # Exact authoring surfaces and no cross-domain/global HarvestCount.
    buffs=ET.parse(root/'Config/buffs.xml').getroot()
    buff=buffs.find(".//buff[@name='RebirthSurvivorResourceFieldPassives']")
    r.check('resource field passive buff exists',buff is not None)
    if buff is not None:
        passives=buff.findall('.//passive_effect')
        harvest=[(p.get('name'),p.get('operation'),p.get('value'),p.get('tags','')) for p in passives if p.get('name')=='HarvestCount']
        expected={
            ('HarvestCount','perc_add','@$rbSurvivorSkillResourceHarvest','oreWoodHarvest,salvageHarvest'),
            ('HarvestCount','perc_add','@$rbSurvivorSkillFarmingHarvest','cropHarvest,wildCropsHarvest'),
            ('HarvestCount','perc_add','@$rbSurvivorSkillAnimalHarvest','butcherHarvest'),
        }
        r.check('harvest passive surface exact',set(harvest)==expected,f'actual={harvest}')
        alltags=','.join(x[3] for x in harvest)
        r.check('no generic allHarvest multiplier','allHarvest' not in alltags and 'allToolsHarvest' not in alltags)
        tracking=[p for p in passives if p.get('name')=='Tracking']
        trtags={p.get('tags','') for p in tracking}
        r.check('native tracking animal tiers exact',trtags=={'perkAT01','perkAT02','perkAT03','perkAT04','perkAT05'},f'tags={sorted(trtags)}')
        r.check('no zombie tracking tags','zombie' not in ','.join(trtags).lower())
        td=[p for p in passives if p.get('name')=='TrackDistance']
        r.check('native track distance bridge',len(td)==1 and td[0].get('value')=='@$rbSurvivorSkillTrackingDistance')
        r.check('buff hidden',buff.get('hidden')=='true')

    service=(root/'Scripts/Survivor/Progression/RebirthResourceFieldSkillService.cs').read_text(errors='replace')
    config=(root/'Scripts/Survivor/Progression/RebirthProgressionRuntimeConfig.cs').read_text(errors='replace')
    patches=(root/'Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs').read_text(errors='replace')
    installer=(root/'Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs').read_text(errors='replace')
    router=(root/'Scripts/Survivor/Progression/RebirthSkillEventRouter.cs').read_text(errors='replace')
    debug=(root/'Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs').read_text(errors='replace')
    adv=(root/'Scripts/AdvancedFarming/RebirthUtilities.cs').read_text(errors='replace')
    skill_sources=(root/'Config/_Survivor/skill_sources.xml').read_text(errors='replace')
    vector=(root/'Scripts/Survivor/Progression/RebirthResourceFieldSkillVectorHarness.cs').read_text(errors='replace')

    r.check('service installed','RebirthResourceFieldSkillService.Install()' in installer)
    r.check('resource tool classifier exists','ClassifyHarvestTool' in config)
    r.check('native harvest context patch exists','GameUtils.HarvestOnAttack' in patches and 'BeginResourceHarvestAttack' in patches and 'EndResourceHarvestAttack' in patches)
    r.check('resource harvest contextual skill selection','BeginResourceHarvestAttack' in service and 'ClassifyHarvest(actionData.invData.itemValue, blockName)' in service and all(x in service for x in ['skill.mining','skill.logging','skill.salvage']))
    r.check('client owner snapshot bridge','RebirthSurvivorClientState.GetOwnerStateSnapshot()' in service)
    r.check('server world record bridge','RebirthWorldCharacterService.TryGet(player, out r)' in service)
    r.check('server tracking authority gate','RebirthWorldCharacterRepository.IsServerAuthority' in service)
    r.check('tracking uses crouch','GetCustomVar("_crouching")' in service)
    r.check('tracking requires animal entity classes','entity is EntityAnimal' in service and 'entity is EntityEnemyAnimal' in service)
    r.check('tracking scans perkAT only',all(x in service for x in ['perkAT01','perkAT02','perkAT03','perkAT04','perkAT05']))
    r.check('tracking acquisition anti-repeat','resource:tracking:acquire:' in service and 'TrackingRepeatSeconds' in service)
    r.check('tracking following requires movement','FollowDistance' in service and 'ResourceFieldMaxSampleDistance' in service)
    r.check('animal processing already-dead state','WasDead' in patches and 'OnAnimalProcessingDamage' in patches)
    r.check('animal processing server only','target.world.IsRemote()' in service)
    r.check('animal processing tool evidence','IsAnimalProcessingTool(source.AttackingItem)' in service)
    r.check('animal processing not combat award','if (__state.WasDead)' in patches and 'return;' in patches)
    r.check('animal processing per-carcass anti-grind','AnimalProcessingRepeatSeconds' in service and 'animal_processing_repeat_seconds' in skill_sources and '>= 300f' in vector)
    r.check('existing block completion LBD preserved','OnBlockHarvestCompleted' in router and 'Block.OnBlockDestroyedBy' in patches)
    r.check('advanced farming LBD preserved','OnFarmingHarvestCompleted' in adv)
    r.check('advanced farming one-crop invariant preserved','intentionally one crop item per completed plant' in adv and 'new ItemStack(itemValue, 1)' in adv)
    r.check('no crop growth mutation in service',all(x not in service for x in ['Grow', 'growthTime', 'PlantGrowing']))
    r.check('resource debug command','mode=="resource"' in debug and 'RebirthResourceFieldSkillService.BuildDebugReport' in debug)
    r.check('resource vector debug command','RebirthResourceFieldSkillVectorHarness.RunAll()' in debug)

    # Skill metadata remains exactly the six approved Chunk-6 IDs.
    prog=ET.parse(root/'Config/_Survivor/progression.xml').getroot()
    skills={e.get('id'):e for e in prog.findall('.//skill')}
    six=['skill.mining','skill.logging','skill.salvage','skill.farming','skill.animal_processing','skill.tracking']
    r.check('six resource field skills authored',all(x in skills for x in six))
    r.check('tracking metadata keeps zombie deferred','zombie tracking remains deferred' in skills['skill.tracking'].get('feasibility','').lower())
    r.check('farming metadata says growth unchanged','crop growth unchanged' in skills['skill.farming'].get('feasibility','').lower())
    return r

def main()->int:
    ap=argparse.ArgumentParser(); ap.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]); args=ap.parse_args()
    result=run(args.root.resolve()); total=result.passed+len(result.failures); status='PASS' if not result.failures else 'FAIL'
    print(f'REBIRTH Survivor Chunk 6 Resource/Field vectors: {status}')
    print(f'passed={result.passed}/{total}')
    print('compile_validation_claimed=False')
    for x in result.failures: print('FAIL: '+x)
    return 0 if not result.failures else 1
if __name__=='__main__':sys.exit(main())
