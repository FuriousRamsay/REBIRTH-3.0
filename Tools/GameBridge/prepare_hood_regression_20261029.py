"""Prepare real-input hood cases from an explicit current /camper capture. No game calls."""
import argparse, copy, json
from pathlib import Path
from prepare_camper_surface_scenarios import generate

def prepare(observation):
    entity=int(observation['entity']);p=observation['presentation']
    base=generate(observation)
    files={}
    for mode in ('travel','camp'):
        scenario=copy.deepcopy(base['camper_hood_ladders_'+mode+'.json'])
        scenario['name']='20261029 fitted hood support and native ladder '+mode
        for step in scenario['steps']:
            checks=step.get('expectCamper',{}).get('checks',[])
            if any(c['field']=='presentation.playerFeetLocalY' for c in checks):
                checks[:]=[c for c in checks if c['field']!='presentation.playerFeetLocalY']
                checks.extend([{'field':'presentation.playerFeetLocalY','op':'>','value':.95},
                               {'field':'presentation.playerFeetLocalY','op':'<','value':1.5},
                               {'field':'presentation.playerFeetLocalZ','op':'<','value':2.25}])
        files['camper_hood_regression_'+mode+'_20261029.json']=scenario
    def expected(checks,timeout=0):
        return {'expectCamper':{'entity':entity,'timeout':timeout,'checks':checks}}
    def check(field,value):return {'field':field,'op':'==','value':value}
    postride=copy.deepcopy(files['camper_hood_regression_travel_20261029.json'])
    postride['name']='20261029 mounted rebind then real exit and hood jump'
    postride['precondition']='Lead-authorized disposable copy only. Already seated as native driver using real vehicle interaction, no UI, no movement input, stationary same truck/pose as observed. Guard ON, fly/noclip OFF. This scenario does NOT mount or reposition the truck.'
    postride['steps'][:0]=[expected([check('localPlayerAttachedEntityId',entity),
        {'field':'vehicleDriverEntityId','op':'==','expectedField':'localPlayerEntityId'},
        check('presentation.mode','Travel'),check('presentation.bodyInstalled',True),
        {'field':'presentation.forwardVelocity','op':'>','value':-.05},
        {'field':'presentation.forwardVelocity','op':'<','value':.05}]),
        {'wait':6.25},{'console':'camperdiag '+str(entity)},
        {'action':{'command':'key','args':{'name':'E'}}},{'wait':.5},
        expected([check('localPlayerAttachedEntityId',-1)],3)]
    files['camper_hood_regression_postride_20261029.json']=postride
    bare=copy.deepcopy(files['camper_hood_regression_travel_20261029.json'])
    bare['name']='20261029 stripped pickup hood control'
    bare['precondition']='Same stationary observed vehicle; lead removed addon legally using native UI in Travel after removing all required installed equipment. On foot, gameplay UI closed, guard ON, fly/noclip OFF. No automatic removal or save editing.'
    for step in bare['steps']:
        for c in step.get('expectCamper',{}).get('checks',[]):
            if c['field']=='presentation.bodyInstalled':c['value']=False
    # Bare pickup deliberately has no camper ladder access. End at supported hood.
    stop=next(i for i,s in enumerate(bare['steps']) if s.get('screenshot')=='hood_supported_travel')
    bare['steps']=bare['steps'][:stop+1]+[{'console':'camperdiag '+str(entity)},{'expectNoErrors':True}]
    files['camper_hood_regression_bare_20261029.json']=bare
    return files

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('observation',type=Path);parser.add_argument('output',type=Path)
    args=parser.parse_args();observation=json.loads(args.observation.read_text(encoding='utf-8-sig'))
    args.output.mkdir(parents=True,exist_ok=True)
    for name,scenario in prepare(observation).items():
        destination=args.output/name;destination.write_text(json.dumps(scenario,indent=2)+'\n');print(destination)
