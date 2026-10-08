"""Additional window and idle measurements without native-profiler injection."""
from test_capacity_live import *
from test_inventory_scroll_live import open_window,close
import os
mark=Path(os.environ.get('REBIRTH_PROFILE_MARK',str(root/'_Documentation/InventoryPerformance/cached_frames.json.mark')))
out=root/'_Documentation/InventoryPerformance'
def measure(label,window=None):
 time.sleep(2);tree=call('/ui/tree')
 assert window is None or window in tree['openWindows'],tree['openWindows']
 mark.write_text(label)
 for _ in range(3):
  time.sleep(5)
  assert window is None or window in call('/ui')['openWindows'],call('/ui')
 mark.write_text('transition');time.sleep(.2)
 print('MEASURED',label,flush=True)
 (out/(label+'_ui.json')).write_text(json.dumps(tree))
if __name__=='__main__':
 call('/console','POST',cmd='settime 1 9 0');call('/surroundings');call('/cleararea','POST',radius=40)
 for key in ('hydration','nutrition','energy'):call('/console','POST',cmd='rbmet set '+key+' 95')
 for i in range(2):
  close();measure('repeat_idle_'+str(i))
  open_window('crafting');measure('repeat_crafting_'+str(i),'crafting')
 open_window('rebirthSurvivorCharacter')
 for tab in ('Progression','Condition','Statistics','Metabolism','Overview'):
  call('/ui/click','POST',text=tab);measure('character_'+tab.lower(),'rebirthSurvivorCharacter')
 close();call('/activate','POST',x=12,y=45,z=997,block=1);time.sleep(1)
 if 'workstation_campfire' not in call('/ui')['openWindows']:call('/activate','POST',x=12,y=45,z=997,block=1)
 measure('cooking','workstation_campfire');close()
