"""Current allocation capture; no A/B claim. Existing owned profiled CodexTest only."""
import json,sys,time
from pathlib import Path
sys.path.insert(0,str(Path('Tools/StorageGear').resolve()))
from test_capacity_live import call
out=Path('_Documentation/PerformanceAudit_20261010')
profile=Path(sys.argv[1]);record=Path(sys.argv[2])
def read():
 for attempt in range(10):
  try:return json.loads(record.read_text(encoding='utf-8-sig'))
  except (ValueError,OSError):time.sleep(.2)
 raise RuntimeError('Recording unreadable')
def mark(label):Path(str(profile)+'.mark').write_text(label)
assert call('/ping')['state']=='ingame'
rows={'ping':call('/ping'),'surroundings':call('/surroundings')}
call('/cleararea','POST',radius=40);call('/restore','POST');call('/console','POST',cmd='settime 1 9 0')
for stat in ('hydration','nutrition','energy'):call('/console','POST',cmd='rbmet set '+stat+' 95')
# Needs guard remains enabled. Require actual window state for each segment.
for _ in range(5):
 w=call('/ui')['openWindows']
 if not any(x in w for x in ('ingameMenu','crafting','rebirthSurvivorCharacter')):break
 call('/key','POST',name='Escape');time.sleep(.5)
rows['segments']=[]
for label in ('idle','crafting','character'):
 if label=='crafting':call('/key','POST',name='Tab')
 if label=='character':call('/ui/click','POST',text='Character')
 time.sleep(3)
 windows=call('/ui')['openWindows']
 if label=='crafting':assert 'crafting' in windows,windows
 if label=='character':assert 'rebirthSurvivorCharacter' in windows,windows
 if label=='idle':assert not any(x in windows for x in ('ingameMenu','crafting','rebirthSurvivorCharacter')),windows
 mark('alloc_current_'+label);time.sleep(60);before=read();time.sleep(40);after=read()
 seconds=after['AllocSeconds']-before['AllocSeconds'];assert seconds>0,'No allocation samples'
 result={'label':label,'windows':windows,'allocationSeconds':seconds,'endWindows':call('/ui')['openWindows']}
 for key in ('AllocByType','AllocBySite'):
  result[key]=sorted([{'name':k,'bytes':v-before.get(key,{}).get(k,0),'bytesPerSecond':(v-before.get(key,{}).get(k,0))/seconds} for k,v in after[key].items() if v>before.get(key,{}).get(k,0)],key=lambda x:-x['bytes'])
 rows['segments'].append(result);(out/'LIVE_ALLOCATION_CAPTURE.json').write_text(json.dumps(rows,indent=2));print(label,seconds,'allocation seconds',flush=True)
mark('alloc_current_end');rows['errors']=call('/log',since=rows['ping']['lastLogSeq'],level='error');(out/'LIVE_ALLOCATION_CAPTURE.json').write_text(json.dumps(rows,indent=2));assert rows['errors']['count']==0
print('Captured three post-load segments; sampling overhead and no baseline prevent FPS improvement claim',flush=True)
