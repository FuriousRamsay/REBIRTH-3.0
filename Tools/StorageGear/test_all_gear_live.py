"""All fourteen gear items, real input transfers and constant encumbrance. CodexTest only."""
from test_capacity_live import *
assert call('/ping')['state']=='ingame'
call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
call('/console','POST',cmd='buff god');time.sleep(1);call('/console','POST',cmd='debuff god')
call('/ui','POST',action='open',window='rebirthSurvivorCharacter')
for slot in ('Belt','Backpack'):
 if state()['survivorGear'].get(slot.lower()):remove(slot)
time.sleep(2);initial=state();assert initial['backpackCapacity']==45;baseline=initial['toolbeltCapacity'];locked=initial['encumbranceSlots']
(out/'full_gear_initial.json').write_text(json.dumps(initial,indent=2))
results=[]
tiers=[(n+'Belt','belt',6+i*2) for i,n in enumerate(['Utility','Field','Tactical','Artisan','Industrial','Expedition'])]+[(n,'backpack',c) for n,c in [('Daypack',50),('ExpandedDaypack',55),('FieldPack',60),('ExpandedFieldPack',65),('HikingPack',70),('ExpandedHikingPack',75),('ExpeditionPack',80),('ExpandedExpeditionPack',90)]]
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
  shot=call('/screenshot','POST',name='six_tiers_'+suffix)['path']
  remove(slot.title());time.sleep(1.5);st=state();assert not st['survivorGear'].get(slot);assert count(st,item)==before
  assert st['backpackCapacity']==45 and st['toolbeltCapacity']==baseline
  visible(item);source=next(x['slot'] for x in state()['backpack'] if x['name']==item)
  call('/ui/drag','POST',**{'from.item':item,'to.id':'survivorGearInput'+slot.title()});time.sleep(.8)
  st=verify(item,slot,expected,before)
  # Reuse the emptied source cell, guaranteed within the original capacity.
  occupied={x['slot'] for x in st['backpack']};target=source if source not in occupied else next(i for i in range(45) if i not in occupied)
  start=max(0,min(target//5-2,4)) # Keep scroll valid after shrinking to 45 cells; target must not move mid-drag.;call('/ui/scroll','POST',id='characterBackpackScroll',delta=20);time.sleep(.2)
  if start:call('/ui/scroll','POST',id='characterBackpackScroll',delta=-start/10);time.sleep(.2)
  result=call('/ui/drag','POST',**{'from.id':'survivorGearInput'+slot.title(),'to.id':'characterBagSlot'+str(target)});time.sleep(.8)
  (out/'last_drag.json').write_text(json.dumps({'targetIndex':target,'before':st,'input':result},indent=2))
  time.sleep(1.5);end=state();assert not end['survivorGear'].get(slot);assert count(end,item)==before,(item,end['survivorGear'],count(end,item),before)
  assert end['backpackCapacity']==45 and end['toolbeltCapacity']==baseline and end['encumbranceSlots']==locked
  assert not any(n.get('item') for n in call('/ui/tree',window='dragAndDrop')['nodes']),('cursor after',item)
  results.append({'item':item,'capacity':expected,'unencumberedSlots':st['unencumberedSlots'],'encumbranceSlots':locked,'shiftIn':True,'shiftOut':True,'dragIn':True,'dragOut':True,'conserved':True,'screenshot':shot})
  (out/'full_gear_results.json').write_text(json.dumps(results,indent=2));print('PASS',item,'capacity',expected,'4 transfers; encumbrance',locked,flush=True)
finally:
 for a in ('strength','constitution'):
  old=initial['gearAttributes'][a];call('/gearfixture','POST',confirm='CodexTest',attribute=a,value=old['Current'],potential=old['Potential'])
 call('/console','POST',cmd='debuff god')
