"""Gear Equip labels and actions, through actual crafting selection and button presses."""
from test_capacity_live import *
from test_inventory_scroll_live import open_window
out=root/'_Documentation/InventoryPerformance'
assert call('/ping')['state']=='ingame'
call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
for key in ('hydration','nutrition','energy'):call('/console','POST',cmd='rbmet set '+key+' 95')
for a in ('strength','constitution'):call('/gearfixture','POST',confirm='CodexTest',attribute=a,value=90,potential=100)
results=[]
for item in ('rebirthGearDaypack','rebirthGearUtilityBelt'):
 if item in state()['survivorGear'].values():
  open_window('rebirthSurvivorCharacter');remove('Backpack' if 'Daypack' in item else 'Belt')
 if not count(state(),item):call('/give','POST',item=item)
 open_window('crafting')
 slot=next(s['slot'] for s in state()['backpack'] if s['name']==item)
 if state()['backpackCapacity']>52:call('/ui/scroll','POST',id='rebirthCraftingInventoryScrollTrack',delta=20);time.sleep(.3)
 if slot>=52:call('/ui/scroll','POST',id='rebirthCraftingInventoryScrollTrack',delta=-20);time.sleep(.5)
 call('/ui/hover','POST',window='crafting',item=item);time.sleep(.6)
 call('/ui/click','POST',window='crafting',item=item,button='right');time.sleep(1)
 t=call('/ui/tree');(out/(item+'_equip_ui.json')).write_text(json.dumps(t))
 assert any(n.get('text','').lower()=='equip' for n in t['nodes'])
 assert not any(n.get('text','').lower()=='use' for n in t['nodes'])
 call('/ui/click','POST',text='Equip');time.sleep(1.5)
 st=state();assert item in st['survivorGear'].values(),st['survivorGear']
 results.append({'item':item,'equipped':True,'capacity':st['backpackCapacity'],'toolbelt':st['toolbeltCapacity']})
 (out/'equip_results.json').write_text(json.dumps(results,indent=2))
 print('PASS crafting Equip',item,flush=True)
