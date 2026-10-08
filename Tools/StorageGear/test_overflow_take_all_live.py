from test_capacity_live import *
out=root/'_Documentation/InventoryPerformance'
assert call('/ping')['state']=='ingame'
call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST');call('/console','POST',cmd='debuff god')
call('/ui','POST',action='open',window='rebirthSurvivorCharacter');time.sleep(1)
if state()['survivorGear'].get('belt'):remove('Belt')
if not state()['survivorGear'].get('backpack'):
 if not count(state(),'rebirthGearDaypack'):call('/give','POST',item='rebirthGearDaypack')
 equip('rebirthGearDaypack')
assert state()['backpackCapacity']==65
filler='meleeWpnClubT1BaseballBat'
for i in range(state()['toolbeltCapacity']):
 if not any(s['slot']==i for s in state()['toolbelt']):call('/give','POST',item=filler,toolbelt=i+1)
for _ in range(70):
 st=state()
 if len(st['backpack'])>=65:break
 call('/give','POST',item=filler)
assert len(state()['backpack'])==65
oldbags={b['id'] for b in call('/entities',radius=8,contains='rebirthGearRecoveryBackpack')['entities']}
remove('Backpack');time.sleep(1)
assert state()['backpackCapacity']==52 and len(state()['backpack'])==52
bag=next(b for b in call('/entities',radius=8,contains='rebirthGearRecoveryBackpack')['entities'] if b['id'] not in oldbags)
bid=bag['id'];before=sum(x['count'] for x in bag['contents']);assert before==13,bag
call('/key','POST',name='Escape');call('/lookat','POST',entity=bid);call('/activate','POST');time.sleep(1)
call('/key','POST',name='R');time.sleep(1)
remaining=next(b for b in call('/entities',radius=8,contains='rebirthGearRecoveryBackpack')['entities'] if b['id']==bid)
assert sum(x['count'] for x in remaining['contents'])==before,remaining
print('PASS full inventory Take All retains all 13 overflow items',flush=True)
(out/'overflow_take_all_full.json').write_text(json.dumps({'before':bag,'after':remaining,'inventory':state()},indent=2))
# Close loot, drop one filler from the backpack through the normal item action.
call('/key','POST',name='Escape');call('/ui','POST',action='open',window='crafting');time.sleep(1)
call('/ui/click','POST',window='crafting',item=filler,button='right');time.sleep(.3);call('/ui/click','POST',window='crafting',text='Drop');time.sleep(.7)
assert len(state()['backpack'])==51,len(state()['backpack'])
call('/key','POST',name='Escape');call('/lookat','POST',entity=bid);call('/activate','POST');time.sleep(.7)
call('/key','POST',name='R');time.sleep(1)
remaining=next(b for b in call('/entities',radius=8,contains='rebirthGearRecoveryBackpack')['entities'] if b['id']==bid)
assert sum(x['count'] for x in remaining['contents'])==before-1,remaining
assert len(state()['backpack'])==52
call('/key','POST',name='R');time.sleep(.5)
again=next(b for b in call('/entities',radius=8,contains='rebirthGearRecoveryBackpack')['entities'] if b['id']==bid)
assert sum(x['count'] for x in again['contents'])==before-1
(out/'overflow_take_all_partial.json').write_text(json.dumps({'after':again,'inventory':state()},indent=2))
print('PASS one free slot transfers one item; remaining 12 survive repeated Take All',flush=True)

