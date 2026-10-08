"""Fixed-position window samples at test and maximum available window resolutions (5120 request clamps to 3440 here).
Run only after the hitch runner is done; optional first argument is its native profile path.
These are sequential observations, not a pre/post optimization A/B verdict.
"""
import sys,time,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'TraderVoices'))
from test_live import call, root, console
out=root/'_Documentation/ReleaseFollowup/resolution_performance.json'
profile=Path(sys.argv[1]) if len(sys.argv)>1 else None
results=[]
modal=('crafting','windowpaging','rebirthSurvivorCharacter','rebirthJournal','rebirthProgressionExplorer','ingameMenu','map','quests','challenges','players')
def close():
 for _ in range(6):
  if not any(w in modal for w in call('/ui')['openWindows']):return
  call('/key','POST',name='Escape');time.sleep(.5)
 raise AssertionError('Could not close menu')
def inventory():
 for _ in range(3):
  close();time.sleep(.5);call('/key','POST',name='Tab');time.sleep(1)
  if 'crafting' in call('/ui')['openWindows']:return
 raise AssertionError('Crafting did not open')
def measure(label,expected,width,height):
 console('settime 1 9 0')
 for stat in ('hydration','nutrition','energy'):console('rbmet set '+stat+' 95')
 if profile:Path(str(profile)+'.reset').write_text('')
 samples=[]
 for _ in range(2):
  time.sleep(5.5)
  windows=call('/ui')['openWindows']
  assert expected is None or expected in windows,(label,windows)
  samples.append(call('/state',sections='performance')['performance'])
 tree=call('/ui/tree');assert tree['screen']==[width,height],tree['screen']
 row={'label':label,'resolution':[width,height],'samples':samples,'windows':windows}
 inventoryState=call('/state',sections='inventory,player')
 row['backpackCapacity']=inventoryState['backpackCapacity']
 row['encumbranceSlots']=inventoryState['encumbranceSlots']
 row['player']=inventoryState['player']
 row['instrumented']=profile is not None
 if profile:
  signal=Path(str(profile)+'.dump');signal.write_text('')
  for _ in range(20):
   if not signal.exists():break
   time.sleep(.1)
  assert not signal.exists(),'Profiler did not dump'
  dest=root/('_Documentation/ReleaseFollowup/cpu_'+label+'.json')
  dest.write_bytes(profile.read_bytes());row['cpuProfile']=str(dest)
 if label.endswith(('_crafting','_character')):
  row['screenshot']=call('/screenshot','POST',name='performance_'+label)['path']
 results.append(row);out.write_text(json.dumps(results,indent=2))
 print(label,[round(s['fps'],1) for s in samples],flush=True)
assert call('/ping')['state']=='ingame'
call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
try:
 for width,height in ((1280,720),(3440,1440)):
  close();console(f'gfx res {width} {height}');time.sleep(2)
  measure(f'{width}_idle_before',None,width,height)
  for label,expected in [('Crafting','crafting'),('Character','rebirthSurvivorCharacter'),('Map','map'),('Quests','quests'),('Challenges','challenges'),('Players','players'),('Journal','rebirthJournal')]:
   inventory()
   if label!='Crafting':call('/ui/click','POST',text=label);time.sleep(.7)
   measure(f'{width}_{label.lower()}',expected,width,height)
  close();measure(f'{width}_idle_after',None,width,height)
finally:
 close();console('gfx res 1280 720')
