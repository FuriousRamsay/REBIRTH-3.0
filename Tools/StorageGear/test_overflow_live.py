"""Occupied-slot removal: uses real clicks to place and remove items. CodexTest only."""
from test_capacity_live import *
assert call('/ping')['state']=='ingame'
call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST');call('/console','POST',cmd='buff god')
call('/ui','POST',action='open',window='rebirthSurvivorCharacter')
assert not state()['survivorGear'].get('backpack') and not state()['survivorGear'].get('belt')
results={}
equip('rebirthGearDaypack'); assert state()['backpackCapacity']==50
if count(state(),'resourceGoldNugget')==0:call('/give','POST',item='resourceGoldNugget',count=3)
assert count(state(),'resourceGoldNugget')==3
call('/ui/scroll','POST',id='characterBackpackScroll',delta=-20);time.sleep(.4)
call('/ui/drag','POST',**{'from.item':'resourceGoldNugget','to.id':'characterBagSlot49'});time.sleep(.6)
st=state(); assert any(x['slot']==49 and x['name']=='resourceGoldNugget' and x['count']==3 for x in st['backpack']),st['backpack']
results['occupiedBackpack']=st
remove('Backpack');st=state();assert st['backpackCapacity']==45;assert count(st,'resourceGoldNugget')==0
bags=call('/entities',radius=8,contains='rebirthGearRecoveryBackpack')['entities']
bag=next(b for b in bags if sum(x['count'] for x in b.get('contents',[]) if x['name']=='resourceGoldNugget')==3)
assert bag['configuredLifetimeSeconds']==3600,bag
results['backpackDrop']=bag
(out/'overflow_results.json').write_text(json.dumps(results,indent=2));print('PASS backpack 50->45; 3 gold in recovery bag',flush=True)
call('/ui','POST',action='close',window='rebirthSurvivorCharacter')
call('/lookat','POST',entity=bag['id']);call('/activate','POST');time.sleep(.8)
call('/screenshot','POST',name='gear_recovery_bag_open')
call('/ui/click','POST',item='resourceGoldNugget',shift=1);time.sleep(.4)
assert count(state(),'resourceGoldNugget')==3;results['backpackRecovered']=True
call('/key','POST',name='Escape');call('/ui','POST',action='open',window='rebirthSurvivorCharacter')
equip('rebirthGearTacticalBelt');assert state()['toolbeltCapacity']==10
existingAmmo=count(state(),'ammo9mmBulletHP')
call('/give','POST',item='ammo9mmBulletHP',count=11,toolbelt=10)
st=state();assert any(x['slot']==9 and x['name']=='ammo9mmBulletHP' and x['count']==11 for x in st['toolbelt'])
results['occupiedBelt']=st
remove('Belt');st=state();assert st['toolbeltCapacity']==4;assert count(st,'ammo9mmBulletHP')==existingAmmo
entities=call('/entities',radius=8)['entities']
drop=next(e for e in entities if e.get('item') and e['item']['name']=='ammo9mmBulletHP')
assert drop['item']['count']==11;assert 1700<drop['lifetimeSeconds']<=1800
results['beltDrop']=drop
(out/'overflow_results.json').write_text(json.dumps(results,indent=2));print('PASS belt 10->4; 11 rounds in 1800-second drop',flush=True)
call('/ui','POST',action='close',window='rebirthSurvivorCharacter');call('/lookat','POST',entity=drop['id']);call('/activate','POST');time.sleep(.5)
results['beltRecovered']=count(state(),'ammo9mmBulletHP')==existingAmmo+11
call('/console','POST',cmd='debuff god')
(out/'overflow_results.json').write_text(json.dumps(results,indent=2));assert results['beltRecovered'];print('PASS both overflow stacks recovered',flush=True)
