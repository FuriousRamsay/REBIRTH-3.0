import sys,time,json,shutil
from pathlib import Path
sys.path.insert(0,'Tools/StorageGear')
from test_capacity_live import call,root
out=root/'_Documentation/InventoryPerformance'; phase=sys.argv[1]
p=Path(sys.argv[2]) if len(sys.argv)>2 else max((root/'Tools/Profiler/out').glob('profile_*.json'),key=lambda x:x.stat().st_mtime)
call('/surroundings');call('/cleararea','POST',radius=40)
for label,window in [('idle',None),('crafting','crafting'),('character','rebirthSurvivorCharacter'),('map','map'),('quests','quests'),('challenges','challenges'),('journal','rebirthJournal'),('players','players')]:
 Path(str(p)+'.mark').write_text('transition');time.sleep(.2)
 if window:
  if 'crafting' not in call('/ui')['openWindows']:
   call('/ui','POST',action='open',window='crafting');time.sleep(1)
   if 'crafting' not in call('/ui')['openWindows']:
    call('/ui','POST',action='open',window='crafting');time.sleep(1)
  if label!='crafting':
   call('/ui/click','POST',text=label);time.sleep(1)
   if window not in call('/ui')['openWindows']:call('/ui/click','POST',text=label)
 else:
  for _ in range(4):
   if not any(w in call('/ui')['openWindows'] for w in ('crafting','rebirthSurvivorCharacter','map','quests','challenges','rebirthJournal','players')):break
   call('/key','POST',name='Escape');time.sleep(.3)
 time.sleep(2)
 tree=call('/ui/tree')
 assert window is None or window in tree['openWindows'],tree['openWindows']
 Path(str(p)+'.mark').write_text(phase+'_'+label)
 for _ in range(5):
  time.sleep(5)
  tree=call('/ui')
  assert window is None or window in tree['openWindows'],(label,tree['openWindows'])
 Path(str(p)+'.mark').write_text('transition');time.sleep(.2)
 # PlayRecorder owns dump/reset. Competing requests can capture an empty reset.
 (out/(phase+'_'+label+'_ui.json')).write_text(json.dumps(call('/ui/tree')))
 print('CAPTURED',phase,label,flush=True)
Path(str(p)+'.mark').write_text(phase+'_end')
