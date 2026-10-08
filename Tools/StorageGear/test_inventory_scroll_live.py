"""Real UI scrolling at expanded capacity; asserts content moves before recording captures."""
from test_capacity_live import *
out=root/'_Documentation/InventoryPerformance'; results=[]
def save(): (out/'scroll_results.json').write_text(json.dumps(results,indent=2))
def close():
 for _ in range(6):
  windows=call('/ui')['openWindows']
  if not any(w in windows for w in ('crafting','rebirthSurvivorCharacter','creative','rebirthContextNavigation','looting','map','quests','challenges','rebirthJournal','players')) and not any(w.startswith('workstation_') for w in windows):return
  call('/key','POST',name='Escape');time.sleep(.4)
def open_window(window):
 close()
 for _ in range(2):
  call('/ui','POST',action='open',window=window);time.sleep(.7)
  if window in call('/ui')['openWindows']:return
 raise AssertionError(call('/ui'))
def check(label,window,target,slotctrl):
 assert window in call('/ui')['openWindows'],call('/ui')
 call('/ui/scroll','POST',**target,delta=20);time.sleep(.5)
 def slots():return [(n['path'],n.get('rect')) for n in call('/ui/tree')['nodes'] if n.get('ctrl')==slotctrl]
 before=slots()
 for i in range(3):
  call('/ui/scroll','POST',**target,delta=-1)
  shot=call('/screenshot','POST',name='verified_scroll_'+label+'_'+str(i))['path']
  results.append({'label':label,'window':window,'screenshot':shot});save();time.sleep(.2)
 after=slots();assert before!=after,(label,'content did not move')
 results.append({'label':label,'moved':True,'before':before,'after':after});save()
 call('/ui/scroll','POST',**target,delta=20);time.sleep(.5)
 print('PASS scrolling '+label,flush=True)
if __name__=='__main__':
 call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
 for key in ('hydration','nutrition','energy'):call('/console','POST',cmd='rbmet set '+key+' 95')
 open_window('crafting');check('crafting','crafting',{'id':'rebirthCraftingInventoryScrollTrack'},'XUiC_RebirthCraftingInventorySlot')
 open_window('rebirthSurvivorCharacter');check('character','rebirthSurvivorCharacter',{'id':'characterBackpackScroll'},'XUiC_RebirthCharacterBackpackSlot')
 close();call('/activate','POST',x=12,y=45,z=997,block=1);time.sleep(1)
 if 'workstation_campfire' not in call('/ui')['openWindows']:call('/activate','POST',x=12,y=45,z=997,block=1);time.sleep(1)
 check('cooking','workstation_campfire',{'id':'rebirthCraftingInventoryScrollTrack'},'XUiC_RebirthCraftingInventorySlot')
 close()
 print('Core scroll surfaces complete',flush=True)
