"""Resume existing paid CodexTest saw jobs after reload using real UI input.
No launch/quit or queue injection. Reports exact output and no repeated credit;
positive Construction credit is checked, not an independently calculated award.
"""
import json,sys,time
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'TraderVoices'))
from test_live import call,root
out=root/'_Documentation/CraftingAudit_20261010/LIVE_QUEUE_RELOAD_COMPLETION.json'
p=dict(x=22,y=44,z=997)
window='workstation_WorkbenchCircularMachine001_FR'
result={'passed':False}
def station(): return call('/station',**p)
def skills():
    rows=call('/state',sections='rebirth')['rebirth']['Skills']
    return {r['Id']:float(r['Value'])+float(r['Progress']) for r in rows if r['Id'] in ('skill.construction','skill.cooking')}
def total(s,field,name): return sum(v['count'] for v in s[field] if v['name']==name)
try:
    ping=call('/ping');result['pid']=ping['pid']
    assert ping['state']=='ingame'
    assert call('/state',sections='world')['world']['gameName']=='CodexTest'
    result['arrival']=call('/surroundings')
    call('/cleararea','POST',radius=40);call('/restore','POST')
    for stat in ('hydration','nutrition','energy'):call('/console','POST',cmd='rbmet set '+stat+' 95')
    initial=station();result['initial']=initial
    assert not initial['accessed'] and not initial['burning'] and initial['queueCount']>0,initial
    assert all(j['recipe']=='FuriousRamsayWoodPlank' for j in initial['queue'])
    expected=total(initial,'output','FuriousRamsayWoodPlank')+sum(j['batches']*j['outputPerBatch'] for j in initial['queue'])
    result['expectedOutput']=expected
    call('/give','POST',item='ammoGasCan',count=1000)
    assert window in call('/activate','POST',block=1,**p)['openWindows']
    for attempt in range(12):
        nodes=call('/ui/tree',window=window)['nodes']
        gas=[n for n in nodes if (n.get('item') or {}).get('name')=='ammoGasCan' and 'rebirthCraftingInventoryViewport' in n['path']]
        if gas:break
        call('/ui/scroll','POST',id='rebirthCraftingInventoryScrollTrack',delta=-1)
    assert gas,'No visible supplied fuel'
    call('/ui/click','POST',path=gas[0]['path'],shift=1)
    result['skillsBefore']=skills()
    call('/ui/click','POST',text='Turn On')
    call('/key','POST',name='Escape')
    deadline=time.monotonic()+420
    result['samples']=[]
    while True:
        s=station();result['samples'].append(s)
        print('queue='+str(s['queueCount'])+' output='+str(total(s,'output','FuriousRamsayWoodPlank')),flush=True)
        if s['queueCount']==0:break
        assert time.monotonic()<deadline,'Queue did not finish in 420 seconds'
        time.sleep(10)
    result['final']=s;result['skillsAfter']=skills()
    assert total(s,'output','FuriousRamsayWoodPlank')==expected,s
    assert s['tools']==initial['tools'],'Unexpected tool mutation'
    assert result['skillsAfter']['skill.construction']>result['skillsBefore']['skill.construction']
    assert result['skillsAfter']['skill.cooking']==result['skillsBefore']['skill.cooking']
    time.sleep(12)
    result['settled']=station();result['skillsSettled']=skills()
    assert result['settled']['output']==s['output'],'Repeated output after queue drained'
    assert result['skillsSettled']==result['skillsAfter'],'Repeated skill credit after queue drained'
    result['errors']=call('/log',since=ping['lastLogSeq'],level='error')
    assert result['errors']['count']==0,result['errors']
    result['passed']=True
    print('PASS resumed saved queue: exact output, Construction credit, no Cooking credit or repeated completion',flush=True)
finally:
    out.write_text(json.dumps(result,indent=2),encoding='utf-8')
