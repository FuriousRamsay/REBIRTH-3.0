"""Compare native legacy and text-only alert projections in disposable CodexTest."""
import json,sys,time
from pathlib import Path
sys.path.insert(0,str(Path('Tools/StorageGear').resolve()))
from test_capacity_live import call
out=Path('_Documentation/PerformanceAudit_20261010/LIVE_BRIDGE_ALERT_AUDIT.json')
rows={'ping':call('/ping'),'surroundings':call('/surroundings'),'checks':[]}
assert rows['ping']['state']=='ingame'
call('/cleararea','POST',radius=40);call('/restore','POST')
for stat in ('hydration','nutrition','energy'):call('/console','POST',cmd='rbmet set '+stat+' 95')
for _ in range(5):
 w=call('/ui')['openWindows']
 if not any(x in w for x in ('ingameMenu','crafting','rebirthSurvivorCharacter')):break
 call('/key','POST',name='Escape');time.sleep(.3)
for label in ('idle','crafting','character'):
 if label=='crafting':call('/key','POST',name='Tab')
 if label=='character':call('/ui/click','POST',text='Character')
 time.sleep(2)
 windows=call('/ui')['openWindows']
 if label=='crafting':assert 'crafting' in windows,windows
 if label=='character':assert 'rebirthSurvivorCharacter' in windows,windows
 for iteration in range(3):
  result=call('/ui/alertaudit');result.update(surface=label,windows=windows,iteration=iteration)
  rows['checks'].append(result);out.write_text(json.dumps(rows,indent=2))
  assert result['equal'],result
  print('PASS',label,iteration,result['currentCount'],'texts',flush=True)
rows['errors']=call('/log',since=rows['ping']['lastLogSeq'],level='error')
out.write_text(json.dumps(rows,indent=2));assert rows['errors']['count']==0
