"""Parent-executed real-input discovery in CodexTest; no crafting or inventory writes."""
import argparse,json
from pathlib import Path
from types import SimpleNamespace
from camper_consolidated_regression_live import Runner,need
from camper_regression_profile import capture

def main():
    p=argparse.ArgumentParser();p.add_argument('prefix');args=p.parse_args()
    prefix=Path(args.prefix); report={'requests':[]}; runner=Runner(report,Path(str(prefix)+'_inputs.json'))
    ingress={'schema':1,'reviewed':True,'existingInstalledStations':True,'reloadMode':'continuing','workbenchTools':[],
             'session':runner.scope,'screen':[1280,720],'stages':{}}
    def stage(name,proof,actions):
        start=len(report['requests'])
        for a in actions:
            runner.call(a['endpoint'],'POST',**a.get('args',{}))
            if a.get('wait'):
                import time;time.sleep(a['wait'])
        ingress['stages'][name]={'proof':proof,'actions':actions,'evidence':report['requests'][start:]}
        Path(str(prefix)+'_ingress.json').write_text(json.dumps(ingress,indent=2),encoding='utf-8')
    def act(ep,wait=.25,**kw):return {'endpoint':ep,'args':kw,'wait':wait}
    def radial(x,y):return [act('/lookat',entity=718),act('/press',wait=.9,action='permanent.Activate',hold=1),
                           act('/ui/hover',x=x,y=y),act('/release',wait=.5,action='permanent.Activate')]
    def cap(kind,recipe=None,batches=1):
        capture(SimpleNamespace(output=str(prefix)+'_'+kind+'.json',fixture=718,original=718,
            kind=kind,recipe=recipe,batches=batches,existing_installed_stations=True))
    ui=runner.call('/ui/tree')
    if 'workstation_workbench' in ui['openWindows']:
        runner.call('/key','POST',name='Escape')
    need(not any(w in runner.call('/ui/tree')['openWindows'] for w in ['ingameMenu','vehicle','workstation_campfire','workstation_workbench']), 'close unexpected UI first')
    stage('openManagement','management',radial(520,308));cap('management')
    stage('closeManagement','closed',[act('/key',name='Escape')])
    stage('openWorkbench','workbench',radial(575,250)+[act('/ui/click',id='camperStationTab1'),
        act('/ui/type',wait=.6,id='rebirthCraftingRecipeSearch',value='Duct Tape')])
    runner.call('/ui/click','POST',text='Duct Tape');cap('workbench','resourceDuctTape',40)
    stage('closeWorkbench','closed',[act('/key',name='Escape')])
    stage('openCampfire','campfire',radial(575,250)+[act('/ui/click',id='camperStationTab0')])
    runner.call('/ui/click','POST',text='Baked Potato');cap('campfire','foodBakedPotato',20)
    stage('closeCampfire','closed',[act('/key',name='Escape')])
    stage('mountDriver','driver',[act('/lookat',entity=718),act('/key',wait=.8,name='E')]);cap('driver')
    stage('dismountDriver','closed',[act('/key',wait=.8,name='E')]);cap('closed')
    runner.errors();report['passed']=True;runner.save()
    print('Discovery complete:',str(prefix)+'_ingress.json')
if __name__=='__main__':main()
