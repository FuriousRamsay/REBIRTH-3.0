"""Real-input full-queue scrolling/cancellation acceptance in the disposable save."""
import json,sys,time
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'TraderVoices'))
from test_live import call,root
p=dict(x=14,y=45,z=998);window='workstation_workbench'
assert call('/state',sections='world')['world']['gameName']=='CodexTest'
call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
call('/give','POST',item='resourceForgedIron',count=160)
call('/walkto','POST',x=14,z=997)
opened=call('/activate','POST',block=1,**p)
assert window in opened['openWindows'],opened
def tree():return call('/ui/tree',window=window)
def queue(t):return [n for n in t['nodes'] if n.get('queued')]
call('/ui/type','POST',id='rebirthCraftingRecipeSearch',value='Baking Pan')
# Use the visible recipe label; the exact recipe token is verified from the clicked result.
call('/ui/click','POST',text='Baking Pan')
call('/ui/type','POST',id='count_input',value='1')
for i in range(16):
 if len(queue(tree()))>=16:break
 call('/ui/click','POST',text='Craft')
before=tree()
# The tree includes only shown controllers; queue readback is authoritative after close.
shots=[call('/screenshot','POST',name='audit_queue_scroll_top')]
for i in range(24):call('/ui/scroll','POST',id='rebirthCraftingQueueScrollTrack',delta=-1)
time.sleep(.4)
after=tree();shots.append(call('/screenshot','POST',name='audit_queue_scroll_bottom'))
(root/'_Documentation/CraftingAudit_20261010/LIVE_QUEUE_SCROLL_INSPECTION.json').write_text(json.dumps(dict(before=before,after=after,screenshots=shots),indent=2))
print(json.dumps(shots))
print('Captured scroll evidence; inspect bottom card geometry before claiming PASS')
