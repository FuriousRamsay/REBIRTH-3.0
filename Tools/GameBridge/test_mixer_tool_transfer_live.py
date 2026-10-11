"""Real UI regression for missing/present mixer battery. CodexTest only.
Precondition: supported cementMixer at26,44,997, queue empty, tool slots empty,
player within activation reach and no open UI. Does not launch/quit or place blocks.
"""
import json,sys,time
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'TraderVoices'))
from test_live import call,root
p=dict(x=26,y=44,z=997);window='workstation_cementMixer'
result={'passed':False}
def station():return call('/station',**p)
def tree():return call('/ui/tree',window=window)['nodes']
def warning():
    return [n for n in tree() if n.get('id')=='recipeRequiredToolText' or n['path'].endswith('/recipeRequiredToolText')]
def wait_warning(visible):
    deadline=time.monotonic()+3
    while True:
        rows=warning()
        if bool(rows)==visible:return rows
        assert time.monotonic()<deadline,'Tool warning did not reach expected visibility: '+str(visible)
        time.sleep(.2)
def click_item(region):
    for attempt in range(14):
        rows=[n for n in tree() if (n.get('item') or {}).get('name')=='carBattery' and region in n['path']]
        if rows:break
        call('/ui/scroll','POST',id='rebirthCraftingInventoryScrollTrack',delta=-1)
    assert rows,'Battery is not visible in '+region
    call('/ui/click','POST',path=rows[0]['path'],shift=1)
try:
    assert call('/state',sections='world')['world']['gameName']=='CodexTest'
    ping=call('/ping');result['pid']=ping['pid']
    result['arrival']=call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
    initial=station();result['initial']=initial
    assert initial['station']=='cementMixer' and not initial['accessed'] and initial['queueCount']==0 and not initial['tools'],initial
    call('/give','POST',item='carBattery',count=1)
    assert window in call('/activate','POST',block=1,**p)['openWindows']
    call('/ui/click','POST',recipe='resourceCrushedSand',exact=1)
    result['missingWarning']=wait_warning(True)
    click_item('rebirthCraftingInventoryViewport')
    result['equippedTree']=tree()
    installed=[n for n in result['equippedTree'] if (n.get('item') or {}).get('name')=='carBattery' and 'windowTools' in n['path']]
    assert len(installed)==1,installed
    wait_warning(False)
    assert not [n for n in result['equippedTree'] if (n.get('item') or {}).get('name')=='carBattery' and 'windowOutput' in n['path']]
    click_item('windowTools')
    result['removedWarning']=wait_warning(True)
    call('/key','POST',name='Escape')
    result['final']=station();assert not result['final']['tools'] and not result['final']['output'] and result['final']['queueCount']==0
    result['errors']=call('/log',since=ping['lastLogSeq'],level='error');assert result['errors']['count']==0,result['errors']
    result['passed']=True
    print('PASS mixer battery shift-click uses tool slot, missing-tool warning clears and returns on removal; no output diversion')
finally:
    (root/'_Documentation/CraftingAudit_20261010/LIVE_MIXER_TOOL_TRANSFER.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
