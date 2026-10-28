"""Parent-executed paired component fixture test; never launches/quits the game.

Run requires two parked, fueled native vehicles on separate clear flat roads,
camper in Travel, UI closed and plenty of driving room. Fixture stacks are
explicit test setup, driving/entry/exit are real input. VerifyReload is separate
after parent save/reload and accepts current reloaded entity IDs.
"""
import argparse
import json
import os
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner,need


def qualification(reply):
    rows=[]
    for result in reply.get('results',[]):
        for line in result.get('output',[]):
            try:value=json.loads(line)
            except (ValueError,TypeError):continue
            if isinstance(value,dict) and value.get('ok') is True:rows.append(value)
    need(len(rows)==1,'exact successful native qualification JSON required')
    return rows[0]


RECIPE_INGREDIENTS={
    'modVehicleCamperBody':dict(FuriousRamsayWoodPlank=40,FuriousRamsayScrews=160,resourceScrapIron=1000,
        resourceForgedIron=200,resourceForgedSteel=100,resourceMechanicalParts=30,resourceElectricParts=25,resourceScrapPolymers=80),
    'rebirthCamperTurret':dict(FuriousRamsayScrews=30,resourceScrapIron=150,resourceForgedIron=25,
        resourceForgedSteel=25,resourceMechanicalParts=15,resourceElectricParts=25,resourceScrapPolymers=20,motionsensor=1)}


def recipe_observation(reply,name):
    rows=[]
    for result in reply.get('results',[]):
        for line in result.get('output',[]):
            try:value=json.loads(line)
            except (ValueError,TypeError):continue
            if isinstance(value,dict) and value.get('schema')==1 and value.get('observationOnly') is True:rows.append(value)
    need(len(rows)==1,'exact one parsed native recipe observation required');v=rows[0]
    need(v['recipe']==name and v['outputItem']==name and v['outputPerBatch']==1 and v['batches']==1 and v['tier']==0,'native recipe identity/count mismatch')
    need(v['area']=='workbench' and v['requiredToolType']>0 and v['requiredTool']=='FuriousRamsayScrewdriver','native workbench/screwdriver requirement missing')
    need(len(v['ingredients'])==8 and {i['name']:i['perBatch'] for i in v['ingredients']}==RECIPE_INGREDIENTS[name],'native recipe material quantities mismatch')
    need(isinstance(v['nativeSecondsPerBatch'],(int,float)) and v['nativeSecondsPerBatch']>=0,'native duration unavailable')
    return v


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--camper',type=int,required=True);p.add_argument('--truck',type=int,required=True)
    p.add_argument('--mode',choices=('Observe','Run','VerifyReload'),default='Observe')
    p.add_argument('--baseline',type=Path);p.add_argument('--report',type=Path,required=True)
    a=p.parse_args();need(a.camper!=a.truck,'distinct exact fixtures required');need(not a.report.exists(),'fresh report required')
    need(os.environ.get('REBIRTH_BRIDGE_SESSION') and os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'),'exclusive parent session required')
    report={'schema':1,'mode':a.mode,'passed':False,'limits':['Flat clear fueled fixture required','No autonomous restart/save/quit',
        'UI and rendered quality still require parent screenshots','Fixture parts intentionally replace disposable test stacks','No forced velocity or custom engine start']}
    r=Runner(report,a.report);start=time.monotonic();driving=None
    def console(command):return qualification(r.call('/console','POST',cmd='rbvehiclepartsqual '+command))
    def observe(entity):return console('observe '+str(entity))
    def bounded():need(time.monotonic()-start<480,'480second batch limit exceeded')
    def park(entity):
        v=observe(entity)
        if v['localPlayerAttachedEntityId']!=entity:
            # A death, exit or lost seat must never turn vehicle cleanup into
            # blind on-foot movement or input to a different attached vehicle.
            need(v['driver']<0 and abs(v['forwardVelocity'])<.5,
                 'exact native driver lost; refuse cleanup movement')
            return v
        for _ in range(20):
            bounded();v=observe(entity)
            need(v['localPlayerAttachedEntityId']==entity,
                 'exact native driver lost during braking; refuse movement')
            if abs(v['forwardVelocity'])<.5:break
            r.call('/move','POST',forward=-1 if v['forwardVelocity']>0 else 1,seconds=.2)
        else:raise AssertionError('bounded native braking failed')
        # Native movementInput.jump is the wheel brake (inspected vehicle
        # PhysicsFixedUpdate); input is real Jump, never a direct field write.
        need(observe(entity)['localPlayerAttachedEntityId']==entity,
             'exact native driver lost before handbrake; refuse movement')
        r.call('/move','POST',forward=0,seconds=.2,jump=1)
        r.call('/stop','POST');v=observe(entity)
        need(abs(v['forwardVelocity'])<.5,'refuse unsafe native exit')
        if v['localPlayerAttachedEntityId']==entity:r.call('/key','POST',name='E')
        v=observe(entity);need(v['localPlayerAttachedEntityId']!=entity,'native exit failed');return v
    def drive(entity,label,allowed):
        nonlocal driving
        fixture=observe(entity);need(fixture['driver']<0 and abs(fixture['forwardVelocity'])<.5,'parked fixture required')
        need(fixture['localPlayerAttachedEntityId']<0 and fixture['fuel']>0,'on-foot fueled fixture required')
        pos=fixture['position'];r.call('/walkto','POST',x=pos[0]+3,z=pos[2],radius=.3,maxSeconds=30)
        # /activate entity is supported by ResolvePoint/RequestedTargetMatches:
        # it looks at the actual entity and injects native activation input.
        r.call('/activate','POST',entity=entity)
        attached=observe(entity);need(attached['driver']==attached['localPlayerEntityId'] and attached['localPlayerAttachedEntityId']==entity,'actual exact native driver required')
        driving=entity;r.call('/move','POST',forward=1,seconds=1)
        moving=observe(entity)
        if allowed:need(moving['engineRunning'] and moving['forwardVelocity']>.05,'real input produced no native forward driving')
        else:
            distance=sum((x-y)**2 for x,y in zip(moving['position'],attached['position']))**.5
            need(not moving['engineRunning'] and abs(moving['motorTorque'])<.001 and
                 abs(moving['nativeCurrentMotorTorquePercent'])<.001 and abs(moving['forwardVelocity'])<.5 and distance<.75,
                 'missing/spent component failed actual native engine/input gate')
        entry={'label':label,'fixture':fixture,'moving':moving,'allowed':allowed,
               'capture':r.call('/screenshot','POST',name='parts_'+label+'_actual_input')}
        park(entity);driving=None;return entry
    try:
        need(r.call('/state',sections='world')['world']['gameName']=='CodexTest','human save refused')
        r.start=r.call('/ping')['lastLogSeq'];report['surroundings']=r.call('/surroundings')
        need(r.call('/guard')['enabled'] is True,'native guard must remain enabled')
        if a.mode=='Run':
            report['cleararea']=r.call('/cleararea','POST',radius=40)
            report['restore']=r.call('/restore','POST')
        ids={'Camper80s':a.camper,'Base4x4':a.truck};report['before']={}
        for content,entity in ids.items():
            v=observe(entity);need(v['content']==content,'exact content mismatch');report['before'][content]=v
        if a.mode=='Run':
            report['recipes']={name:recipe_observation(r.call('/console','POST',cmd=f'rbcamperregrecipe {name} 1 0'),name)
                               for name in RECIPE_INGREDIENTS}
            report['selftest']=console('selftest');need(report['selftest']['passed'],'native selftest failed')
            report['drives']=[]
            for content,entity in ids.items():
                for quality,roll in ((1,0),(6,1)):
                    bounded();v=observe(entity);need(v['driver']<0 and abs(v['forwardVelocity'])<.5,'parked unoccupied fixture required')
                    need(v['localPlayerAttachedEntityId']<0 and v['fuel']>0,'on-foot actor and fueled fixture required')
                    fixture=console(f'setup {entity} {quality} {roll} confirmCodexTest')
                    need(fixture['operational'] and fixture['repairComplete'],'fixture native components invalid')
                    entry=drive(entity,f'{content}_q{quality}',True);entry.update(content=content,quality=quality)
                    report['drives'].append(entry)
                missing=console(f'remove {entity} Engine confirmCodexTest');need(not missing['operational'],'missing engine did not gate operation')
                report.setdefault('gates',[]).append(drive(entity,content+'_engine_missing',False))
                console(f'setup {entity} 3 .4 confirmCodexTest')
                spent=console(f'exhaust {entity} Engine confirmCodexTest');need(not spent['operational'],'exhausted engine did not gate operation')
                report['gates'].append(drive(entity,content+'_engine_exhausted',False))
                console(f'setup {entity} 3 .4 confirmCodexTest')
                console(f'remove {entity} HeadlightLeft confirmCodexTest')
                lights=console(f'remove {entity} HeadlightRight confirmCodexTest')
                need(lights['operational'] and not lights['repairComplete'],'headlight repair/operation distinction failed')
                report['gates'].append(drive(entity,content+'_headlights_missing',True))
                console(f'setup {entity} 3 .4 confirmCodexTest')
                before_wear=observe(entity);after_wear=before_wear
                for _ in range(5):
                    after_wear=console(f'wear {entity} 10 confirmCodexTest')
                    old={s['slot']:s['useTimes'] for s in before_wear['slots']}
                    if any(s['useTimes']>old[s['slot']] for s in after_wear['slots']):break
                else:raise AssertionError('five actual native hits produced no part-wear witness')
                until=time.monotonic()+3
                while after_wear['metadataRevision']!=after_wear['revision']:
                    need(time.monotonic()<until,'native part-wear metadata publication did not settle')
                    time.sleep(.1);after_wear=observe(entity)
                need(after_wear['metadataAssembly']==after_wear['assembly'] and
                     all(after_wear['metadataPartUseTimes'].get(s['slot'])==s['useTimes'] for s in after_wear['slots']),
                     'native metadata lost actual condition wear')
                report.setdefault('wear',{})[content]={'before':before_wear,'after':after_wear}
            for quality in (1,6):
                pair=[x for x in report['drives'] if x['quality']==quality]
                need(pair[0]['fixture']['performance']==pair[1]['fixture']['performance'] or
                     all(pair[0]['fixture']['performance'][k]==pair[1]['fixture']['performance'][k] for k in
                         ('AccelerationMultiplier','TorqueMultiplier','VelocityMultiplier','HandlingMultiplier','BrakingMultiplier')),'paired native multiplier mismatch')
        report['checkpoint']={content:observe(entity) for content,entity in ids.items()}
        if a.mode=='VerifyReload':
            need(a.baseline is not None,'prior baseline report required');before=json.loads(a.baseline.read_text())['checkpoint']
            for content,current in report['checkpoint'].items():
                need(current['assembly']==before[content]['assembly'] and current['slots']==before[content]['slots'],'save/load component identity or condition changed')
        need(r.call('/guard')['enabled'] is True,'native guard was disabled during batch')
        r.errors();report['passed']=True
    except Exception as error:report['failure']=type(error).__name__+': '+str(error)
    finally:
        if driving is not None:
            try:report['cleanup']=park(driving)
            except Exception as error:report['cleanupFailure']=str(error);report['passed']=False
        report['seconds']=round(time.monotonic()-start,3);r.save()
    return 0 if report['passed'] else 1


if __name__=='__main__':raise SystemExit(main())
