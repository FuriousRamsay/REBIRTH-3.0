from test_capacity_live import *
assert call('/ping')['state']=='ingame'
initial=state();assert not initial['survivorGear'].get('belt') and not initial['survivorGear'].get('backpack')
original=initial['gearBackground'];locked=initial['encumbranceSlots'];results=[]
try:
 for a in ('strength','constitution'):call('/gearfixture','POST',confirm='CodexTest',attribute=a,value=90,potential=100)
 for background in ('background.maintenance_technician','background.scavenger'):
  call('/gearfixture','POST',confirm='CodexTest',background=background);time.sleep(2)
  st=state();base=50 if background.endswith('scavenger') else 45;belt=6 if background.endswith('maintenance_technician') else 4
  assert st['backpackCapacity']==base and st['toolbeltCapacity']==belt,st
  assert st['encumbranceSlots']==locked,st
  for suffix,slot,capacity in [('ExpeditionBelt','belt',belt+12),('ExpandedExpeditionPack','backpack',base+45)]:
   item='rebirthGear'+suffix;before=count(state(),item);equip(item);time.sleep(2);st=state();key='toolbeltCapacity' if slot=='belt' else 'backpackCapacity'
   assert st[key]==capacity,(background,item,st[key],capacity)
   if slot=='belt':assert st['visibleToolbeltSlots']==capacity,st['visibleToolbeltSlots']
   assert st['encumbranceSlots']==locked
   shot=call('/screenshot','POST',name='background_'+background.split('.')[-1]+'_'+suffix)['path']
   results.append({'background':background,'item':item,'capacity':capacity,'encumbranceSlots':locked,'unencumberedSlots':st['unencumberedSlots'],'screenshot':shot,'passed':True})
   remove(slot.title());time.sleep(2);assert count(state(),item)==before
   print('PASS',background,item,capacity,'encumbrance',locked,flush=True)
  (out/'background_storage_results.json').write_text(json.dumps(results,indent=2))
finally:
 call('/gearfixture','POST',confirm='CodexTest',background=original)
 for a in ('strength','constitution'):
  old=initial['gearAttributes'][a];call('/gearfixture','POST',confirm='CodexTest',attribute=a,value=old['Current'],potential=old['Potential'])
