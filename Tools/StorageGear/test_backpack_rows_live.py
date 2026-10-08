"""All eight backpack tiers, real input transfers and constant encumbrance. CodexTest only."""
from test_capacity_live import *
out=root/"_Documentation/BackpackRows";out.mkdir(exist_ok=True)
assert call('/ping')['state']=='ingame'
call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
call('/console','POST',cmd='debuff god')
call('/ui','POST',action='open',window='rebirthSurvivorCharacter')
for slot in ('Belt','Backpack'):
 if state()['survivorGear'].get(slot.lower()):remove(slot)
time.sleep(2);initial=state();assert initial['backpackCapacity']==52;baseline=initial['toolbeltCapacity'];locked=initial['encumbranceSlots'];assert locked==26,initial
(out/'full_gear_initial.json').write_text(json.dumps(initial,indent=2))
results=[]
tiers=[(n,'backpack',c) for n,c in [('Daypack',65),('ExpandedDaypack',78),('FieldPack',91),('ExpandedFieldPack',104),('HikingPack',117),('ExpandedHikingPack',130),('ExpeditionPack',143),('ExpandedExpeditionPack',156)]]
def verify(item,slot,expected,before):
 time.sleep(1.5) # Native passive buffs refresh on their game tick.
 st=state();key='toolbeltCapacity' if slot=='belt' else 'backpackCapacity'
 assert st['survivorGear'].get(slot)==item,(item,st['survivorGear'])
 assert st[key]==expected,(item,st[key],expected)
 if slot=='belt':assert st['visibleToolbeltSlots']==expected,st['visibleToolbeltSlots']
 assert st['encumbranceSlots']==locked,(item,st['encumbranceSlots'],locked)
 assert count(st,item)==before-1,(item,count(st,item),before)
 return st
try:
 for a in ('strength','constitution'):call('/gearfixture','POST',confirm='CodexTest',attribute=a,value=90,potential=100)
 for suffix,slot,expected in tiers:
  item='rebirthGear'+suffix
  if not count(state(),item):call('/give','POST',item=item,count=1)
  assert not any(n.get('item') for n in call('/ui/tree',window='dragAndDrop')['nodes']),('cursor before',item)
  before=count(state(),item);equip(item);st=verify(item,slot,expected,before)
  shot=call('/screenshot','POST',name='backpack_rows_'+suffix)['path']
  remove(slot.title());time.sleep(1.5);st=state();assert not st['survivorGear'].get(slot);assert count(st,item)==before
  assert st['backpackCapacity']==52 and st['toolbeltCapacity']==baseline
  visible(item);source=next(x['slot'] for x in state()['backpack'] if x['name']==item)
  call('/ui/drag','POST',**{'from.item':item,'to.id':'survivorGearInput'+slot.title()});time.sleep(.8)
  st=verify(item,slot,expected,before)
  # Reuse the emptied source cell, guaranteed within the original capacity.
  occupied={x['slot'] for x in st['backpack']};target=source if source not in occupied else next(i for i in range(52) if i not in occupied)
  start=max(0,min(target//5-2,6))
  call('/ui/scroll','POST',id='characterBackpackScroll',delta=20);time.sleep(.4)
  if start:call('/ui/scroll','POST',id='characterBackpackScroll',delta=-start/10);time.sleep(.5)
  result=call('/ui/drag','POST',**{'from.id':'survivorGearInput'+slot.title(),'to.id':'characterBagSlot'+str(target)});time.sleep(.8)
  (out/'last_drag.json').write_text(json.dumps({'targetIndex':target,'before':st,'input':result},indent=2))
  time.sleep(1.5);end=state();assert not end['survivorGear'].get(slot);assert count(end,item)==before,(item,end['survivorGear'],count(end,item),before)
  assert end['backpackCapacity']==52 and end['toolbeltCapacity']==baseline and end['encumbranceSlots']==locked
  assert not any(n.get('item') for n in call('/ui/tree',window='dragAndDrop')['nodes']),('cursor after',item)
  results.append({'item':item,'capacity':expected,'unencumberedSlots':st['unencumberedSlots'],'encumbranceSlots':locked,'shiftIn':True,'shiftOut':True,'dragIn':True,'dragOut':True,'conserved':True,'screenshot':shot})
  (out/'full_gear_results.json').write_text(json.dumps(results,indent=2));print('PASS',item,'capacity',expected,'4 transfers; encumbrance',locked,flush=True)
finally:
 for a in ('strength','constitution'):
  old=initial['gearAttributes'][a];call('/gearfixture','POST',confirm='CodexTest',attribute=a,value=old['Current'],potential=old['Potential'])
 call('/console','POST',cmd='debuff god')
