"""Real-input pause/resume acceptance; disposable saw fixture22,44,997, installed blade.
Starts closed. Collects existing output, supplies test materials, leaves completed output.
Does not test durability wear or multiple jobs crossing a single native timestep.
"""
import json,sys,time
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'TraderVoices'))
from test_live import call,root
assert call('/state',sections='world')['world']['gameName']=='CodexTest'
p=dict(x=22,y=44,z=997); window='workstation_WorkbenchCircularMachine001_FR'
base=window+'/rebirthStationRootFuel/rebirthPersonalCraftingRoot/rebirthCraftingBodyZone/'
blade='FuriousRamsayCircularSawBlade'
def station():return call('/station',**p)
def tree():return call('/ui/tree',window=window)['nodes']
def open_station():assert window in call('/activate','POST',block=1,**p)['openWindows']
def shift_item(name,region):
    nodes=[n for n in tree() if n.get('item') and n['item']['name']==name and region in n['path']]
    assert len(nodes)==1,nodes
    call('/ui/click','POST',path=nodes[0]['path'],shift=1)
def queued():
    jobs=[n['queued'] for n in tree() if n.get('queued')]
    assert len(jobs)==1,jobs
    return jobs[0]
initial=station();assert not initial['accessed'] and initial['queueCount']==0
assert any(t['name']==blade for t in initial['tools'])
seq=call('/ping')['lastLogSeq']
call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
call('/give','POST',item='resourceWood',count=24);call('/give','POST',item='ammoGasCan',count=100)
open_station()
if initial['output']:shift_item('FuriousRamsayWoodPlank','windowOutput')
call('/ui/scroll','POST',id='rebirthCraftingInventoryScrollTrack',delta=-1)
shift_item('ammoGasCan','rebirthCraftingInventoryViewport')
call('/ui/click','POST',recipe='FuriousRamsayWoodPlank',exact=1)
call('/ui/type','POST',id='count_input',value='2')
call('/ui/click','POST',text='Turn On')
call('/ui/click','POST',text='Craft')
shift_item(blade,'windowToolsForge')
assert not any(n.get('item') and n['item']['name']==blade and 'windowToolsForge' in n['path'] for n in tree()), 'Native queued-tool lock prevented removal; pause precondition not reached'
a=queued();time.sleep(3);b=queued()
assert abs(a['timeLeft']-b['timeLeft'])<=.11,(a,b)
call('/key','POST',name='Escape')
c=station();time.sleep(3);d=station()
assert c['queueCount']==d['queueCount']==1 and not d['output'],(c,d)
assert abs(c['queue'][0]['secondsLeft']-d['queue'][0]['secondsLeft'])<.001,(c,d)
open_station();call('/ui/scroll','POST',id='rebirthCraftingInventoryScrollTrack',delta=-1)
shift_item(blade,'rebirthCraftingInventoryViewport');call('/key','POST',name='Escape')
time.sleep(25)
final=station()
assert final['queueCount']==0 and sum(s['count'] for s in final['output'] if s['name']=='FuriousRamsayWoodPlank')==12,final
assert any(t['name']==blade for t in final['tools'])
errors=call('/log',since=seq,level='error');assert errors['count']==0,errors
(root/'_Documentation/CraftingAudit_20261010/LIVE_SAW_TOOL_HOLD_TEST.json').write_text(json.dumps(dict(passed=True,initial=initial,openBefore=a,openAfter=b,closedBefore=c,closedAfter=d,final=final,errors=errors),indent=2))
print('PASS open/closed missing-tool pause, reinsertion resume,12plank output, retained blade, no errors')