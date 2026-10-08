"""Run prepare, quit/relaunch CodexTest, then verify. Uses real drag input."""
from test_capacity_live import *
import sys
out=root/'_Documentation/BackpackRows'
assert call('/ping')['state']=='ingame'
call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
call('/console','POST',cmd='debuff god')
call('/ui','POST',action='open',window='rebirthSurvivorCharacter')
if sys.argv[1]=='prepare':
 for a in ('strength','constitution'):call('/gearfixture','POST',confirm='CodexTest',attribute=a,value=90,potential=100)
 call('/gearfixture','POST',confirm='CodexTest',background='background.scavenger')
 if state()['survivorGear'].get('backpack')!='rebirthGearExpandedExpeditionPack':equip('rebirthGearExpandedExpeditionPack')
 st=state();assert count(st,'resourceGoldNugget')==3
 if any(x['name']=='resourceGoldNugget' for x in st['backpack']):
  visible('resourceGoldNugget')
  assert not any(x['slot']==2 for x in st['toolbelt'])
  call('/ui/drag','POST',**{'from.item':'resourceGoldNugget','to.path':'toolbelt/windowToolbelt/toolbelt/inventory/2'})
 call('/ui/scroll','POST',id='characterBackpackScroll',delta=-20);time.sleep(.6)
 result=call('/ui/drag','POST',**{'from.item':'resourceGoldNugget','to.id':'characterBagSlot168'})
 assert result['stillHeld'] is None
 time.sleep(2)
else:
 call('/ui/scroll','POST',id='characterBackpackScroll',delta=-20);time.sleep(.6)
st=state()
assert st['backpackCapacity']==169 and st['encumbranceSlots']==26,st
assert any(x['slot']==168 and x['name']=='resourceGoldNugget' and x['count']==3 for x in st['backpack'])
node=next(n for n in call('/ui/tree',window='rebirthSurvivorCharacter')['nodes'] if n.get('id')=='characterBagSlot168')
assert node['item']['name']=='resourceGoldNugget' and 330<=node['rect'][1]<=580,node
st['screenshot']=call('/screenshot','POST',name='backpack169_'+sys.argv[1])['path']
(out/('persistence_'+sys.argv[1]+'.json')).write_text(json.dumps(st,indent=2))
print('PASS',sys.argv[1],': 169 physical /143 usable /26 encumbered; final slot168 holds3 gold',flush=True)
