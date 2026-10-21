"""Prepared local fixtures and existing functional scenarios, one leased game session.
No launch/restart, inventory replacement, injected queues or output grants.
"""
import argparse
from collections import Counter
import hashlib
import json
import math
import os
from pathlib import Path
import subprocess
import time
from camper_consolidated_regression_live import Runner, need
from run_gamebridge_batch import last_json

HERE = Path(__file__).resolve().parent
MEDICINE = 'medicalFirstAidBandage'
HUD = {'toolTip','CalloutGroup','chatoutput','toolbelt','dragAndDrop','compass','interactionPrompt','saveIndicator'}


def one(rows, message):
    need(len(rows) == 1, message + ': matches=' + str(len(rows)))
    return rows[0]


def inventory_images(observed):
    return Counter((row['itemData'], row['count']) for area in ('bag','belt') for row in observed[area] if row['count'])


def tree(runner):
    result = runner.call('/ui/tree', all=1)
    need(result.get('truncated') is False, 'full visible UI required')
    return result


def idle_owner(runner):
    value = runner.call('/gearobserve')
    need(value['gameName'] == 'CodexTest' and value['remote'] is False, 'local disposable owner required')
    need(value['cursor']['count'] == 0, 'cursor occupied; preserve custody and stop')
    for key in ('gearHeld','libraryHeld','resourcePending','creationHeld','offerPending'):
        need(value.get(key) is False, 'pending original custody: ' + key)
    need(not value.get('intentMarkers'), 'pending original inventory intent')
    cooking=runner.call('/auditstate',domain='cookingAdmission')['domains']['cookingAdmission']
    need(cooking.get('clientHeld') == 0 and cooking.get('ownerDeltaHeld') is False, 'original cooking custody remains pending; inspect without setup mutation')
    return value


def stable(runner):
    need(runner.call('/state', sections='world')['world']['gameName'] == 'CodexTest', 'real save refused')
    idle_owner(runner)  # Refuse pending custody before any fixture setup.
    runner.call('/cleararea', 'POST', radius=40)
    runner.call('/restore', 'POST')
    runner.guard_idle()
    current = runner.call('/state', sections='ui')
    need(set(current['openWindows']) <= HUD, 'close prior/unowned windows before fixture setup')
    return idle_owner(runner)


def drag(runner, source, target):
    # Fixture setup uses the established native drag + click-to-place adapter.
    # placedByClick is retained as evidence; this does not qualify strict drag release.
    before=tree(runner)
    src=one([n for n in before['nodes'] if n['path']==source], 'exact fixture source')
    dst=one([n for n in before['nodes'] if n['path']==target], 'exact fixture destination')
    need(src.get('item') and dst.get('item') is None, 'positive source and empty target required')
    idle_owner(runner)
    reply=runner.call('/ui/drag','POST',**{'from.path':source,'to.path':target,'strict':0})
    need(reply.get('stillHeld') is None, 'native fixture placement retained custody; stop without replay')
    need(idle_owner(runner)['cursor']['count']==0, 'fixture retained native cursor')
    after=tree(runner)
    final_src=one([n for n in after['nodes'] if n['path']==source], 'original fixture source after placement')
    final_dst=one([n for n in after['nodes'] if n['path']==target], 'original fixture destination after placement')
    need(final_src.get('item') is None and final_dst.get('item')==src['item'], 'native fixture transfer differs from exact source count/item')
    return {'gesture':'native-drag-and-place-fixture','from':source,'to':target,'nativeReply':reply}


def visible_item(value, name, region):
    return one([n for n in value['nodes'] if (n.get('item') or {}).get('name') == name and region in n['path']], 'unique visible ' + name)


def backpack_only_medicine(runner):
    before = stable(runner)
    runner.call('/key','POST',name='Tab')
    need('crafting' in tree(runner)['openWindows'], 'native inventory did not open')
    moves=[]
    for attempt in range(16):
        value=tree(runner)
        sources=[n for n in value['nodes'] if n['path'].startswith('toolbelt/') and (n.get('item') or {}).get('name') == MEDICINE]
        if not sources:
            break
        targets=[n for n in value['nodes'] if n.get('ctrl') == 'XUiC_RebirthCraftingInventorySlot' and n.get('item') is None and n.get('rect') and n['rect'][2]>0 and n['rect'][3]>0]
        need(targets, 'free visible backpack cell required; no deletion or replacement')
        move=drag(runner,sources[0]['path'],targets[0]['path'])
        moves.append(move)
    else:
        raise AssertionError('too many toolbelt bandage stacks')
    after=idle_owner(runner)
    need(inventory_images(before)==inventory_images(after), 'fixture transfer changed native item images/counts')
    named=runner.call('/state',sections='inventory')
    need(not any(s.get('name') == MEDICINE and s.get('count',0) for s in named['toolbelt']), 'bandages remain on belt')
    closed=runner.call('/key','POST',name='B')
    need('permanent.Character' in closed.get('actions',[]) and 'crafting' not in runner.call('/state',sections='ui')['openWindows'], 'owned Crafting did not close through verified B shortcut')
    runner.report.setdefault('preparations',[]).append({'kind':'backpack-only-medicine','before':before,'after':after,'moves':moves})
    runner.save()


def scenario(runner, path, label):
    before=runner.call('/ping')
    command=['powershell','-NoProfile','-ExecutionPolicy','Bypass','-File',str(HERE/'gamebridge.ps1'),'test',str(path)]
    completed=subprocess.run(command,cwd=HERE,env=os.environ.copy(),capture_output=True,text=True,timeout=300)
    (runner.path.parent/(label+'-stdout.txt')).write_text(completed.stdout,encoding='utf-8')
    (runner.path.parent/(label+'-stderr.txt')).write_text(completed.stderr,encoding='utf-8')
    result=last_json(completed.stdout)
    runner.report.setdefault('scenarios',[]).append({'label':label,'template':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'result':result})
    runner.save()
    need(completed.returncode == 0 and result.get('passed') is True, 'functional scenario failed: '+label)
    need(runner.call('/log',since=before['lastLogSeq'],level='error')['count'] == 0, 'new game error in '+label)


def health(runner):
    for name in ('survival_lowhealth','heal_instinct'):
        backpack_only_medicine(runner)
        scenario(runner,HERE/'tests'/f'{name}.json',name)
    runner.call('/restore','POST')
    runner.guard_idle()


def campfire(runner):
    stable(runner)
    inventory=runner.call('/state',sections='inventory,player,world')
    capacity=inventory['toolbeltCapacity']
    need(type(capacity) is int and 0 < capacity <= 48, 'actual owned toolbelt capacity required')
    occupied={row['slot'] for row in inventory['toolbelt'] if row.get('count',0)}
    empty=[i for i in range(capacity) if i not in occupied]
    need(empty, 'free toolbelt slot required for native placement')
    slot=empty[-1]+1
    before=runner.call('/findblocks',name='campfire',radius=6,limit=40)
    existing={tuple(row['position']) for row in before['blocks']}
    runner.call('/give','POST',item='campfire',count=1,toolbelt=slot)
    runner.call('/select','POST',slot=slot)
    p=inventory['player']['position']
    ground={'x':math.floor(p[0])-2,'y':math.floor(p[1])-1,'z':math.floor(p[2])}
    aim=runner.call('/lookat','POST',**ground,block=1)
    target=aim['target']
    need(target.get('block') and not target.get('entity') and target['block']['position']==list(ground.values()), 'exact native ground target required')
    need(target['block']['face'] == 'Top', 'native top-face placement required')
    runner.call('/press','POST',action='Secondary',seconds=.2)
    time.sleep(.5)
    placed=runner.call('/findblocks',name='campfire',radius=6,limit=40)
    new=[row for row in placed['blocks'] if row['name']=='campfire' and tuple(row['position']) not in existing]
    fire=one(new,'one newly native-placed campfire')
    pos=dict(zip(('x','y','z'),fire['position']))
    station=runner.call('/station',**pos)
    need(station['station']=='campfire' and station['queueCount']==0 and not station['accessed'], 'fresh empty native campfire required')
    need(station.get('output') == [] or not any(s.get('count',0) for s in station['output']), 'preserve any existing output')
    for name,count in (('resourceWood',30),('toolCookingPot',1),('foodCropPotato',3)):
        runner.call('/give','POST',item=name,count=count)
    opened=runner.call('/activate','POST',**pos,block=1)
    need('workstation_campfire' in opened['openWindows'], 'placed campfire did not open through native Activate')
    for name,controller in (('toolCookingPot','XUiC_RebirthCookingToolSlot'),('resourceWood','XUiC_RebirthCookingFuelSlot')):
        value=tree(runner)
        source=visible_item(value,name,'rebirthCraftingInventoryViewport')
        destinations=[n for n in value['nodes'] if n.get('ctrl')==controller and n.get('item') is None and n.get('rect')]
        need(destinations, 'empty visible native cooking grid required: '+controller)
        drag(runner,source['path'],destinations[0]['path'])
    runner.call('/key','POST',name='Escape')
    ready=runner.call('/station',**pos)
    runner.report.setdefault('preparations',[]).append({'kind':'native-campfire-placement','beforeCampfires':before,'groundTarget':target,'position':pos,'freshStation':station,'preparedStation':ready})
    template=HERE/'tests/cooking_campfire.json'
    data=json.loads(template.read_text(encoding='utf-8-sig'))
    for step in data['steps']:
        if 'walkto' in step:
            step['walkto'].update(x=p[0],z=p[2])
        if 'activate' in step:
            step['activate'].update(pos)
    data['observedFixture']={'position':pos,'templateSha256':hashlib.sha256(template.read_bytes()).hexdigest(),'bindingOnly':True}
    bound=runner.path.parent/'cooking_campfire_observed.json'
    bound.write_text(json.dumps(data,indent=2),encoding='utf-8')
    runner.save()
    scenario(runner,bound,'native_placed_campfire_cooking')
    runner.report['finalStation']=runner.call('/station',**pos)
    runner.save()


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--phase',choices=['health','campfire'],required=True)
    parser.add_argument('--report',type=Path,required=True)
    args=parser.parse_args()
    need(os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN') and os.environ.get('REBIRTH_BRIDGE_SESSION'), 'run inside the exclusive batch orchestrator')
    report={'status':'RUNNING','phase':args.phase,'scope':'Actual local native-input scenarios with fixture setup recorded separately; no remote/dedicated acceptance'}
    runner=Runner(report,args.report)
    try:
        runner.start=runner.call('/ping')['lastLogSeq']
        (health if args.phase=='health' else campfire)(runner)
        runner.errors()
        report['status']='PASS'
    except BaseException as error:
        report['status']='FAIL'
        report['error']=type(error).__name__+': '+str(error)
        raise
    finally:
        runner.save()


if __name__=='__main__':
    main()
