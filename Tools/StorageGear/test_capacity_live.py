"""Live capacity qualification via authenticated Game Bridge input. Disposable CodexTest only."""
from pathlib import Path
import json,time,urllib.request,urllib.parse
root=Path(__file__).resolve().parents[2]
out=root/'_Documentation/GearTransfers';out.mkdir(exist_ok=True)
session=json.loads((root/'Tools/GameBridge/out/session.json').read_text(encoding='utf-8-sig'))
def call(path,method='GET',**args):
 req=urllib.request.Request('http://127.0.0.1:'+str(session['port'])+path+'?'+urllib.parse.urlencode(args),headers={'X-Bridge-Token':session['token']},method=method,data=b'' if method=='POST' else None)
 with urllib.request.urlopen(req,timeout=60) as r: data=json.load(r)
 assert data.get('ok',False), data
 return data
def state(): return call('/state',sections='inventory,world')
def visible(item):
 st=state(); itemrow=next(x['slot']//5 for x in st['backpack'] if x['name']==item)
 start=max(0,min(itemrow-2,(st['backpackCapacity']+4)//5-5))
 call('/ui/scroll','POST',id='characterBackpackScroll',delta=20);time.sleep(.3)
 if start:call('/ui/scroll','POST',id='characterBackpackScroll',delta=-start/10);time.sleep(.3)
def equip(item):
 visible(item); call('/ui/click','POST',window='rebirthSurvivorCharacter',item=item,shift=1); time.sleep(.6)
def remove(slot):
 call('/ui/click','POST',id='survivorGearInput'+slot,shift=1);time.sleep(.6)
def count(st,item):return sum(x['count'] for k in ('backpack','toolbelt') for x in st[k] if x['name']==item)
if __name__=='__main__':
 assert call('/ping')['state']=='ingame'
 call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST');call('/console','POST',cmd='buff god');time.sleep(1);call('/console','POST',cmd='debuff god')
 initial=state();(out/'capacity_initial.json').write_text(json.dumps(initial,indent=2))
 assert not initial['survivorGear'].get('belt') and not initial['survivorGear'].get('backpack'),initial['survivorGear']
 baseline=initial['toolbeltCapacity']; assert initial['backpackCapacity']==45
 results=[]
 try:
  for a in ('strength','constitution'): call('/gearfixture','POST',confirm='CodexTest',attribute=a,value=90,potential=100)
  call('/ui','POST',action='open',window='rebirthSurvivorCharacter')
  tiers=[('UtilityBelt','belt',baseline+2),('FieldBelt','belt',baseline+4),('TacticalBelt','belt',baseline+6),('ArtisanBelt','belt',baseline+8),('IndustrialBelt','belt',baseline+10),('ExpeditionBelt','belt',baseline+12),('Daypack','backpack',50),('ExpandedDaypack','backpack',55),('FieldPack','backpack',60),('ExpandedFieldPack','backpack',65),('HikingPack','backpack',70),('ExpandedHikingPack','backpack',75),('ExpeditionPack','backpack',80),('ExpandedExpeditionPack','backpack',90)]
  for suffix,slot,expected in tiers:
   item='rebirthGear'+suffix
   if count(state(),item)==0:call('/give','POST',item=item,count=1)
   before=count(state(),item);equip(item); st=state()
   key='toolbeltCapacity' if slot=='belt' else 'backpackCapacity'
   assert st['survivorGear'].get(slot)==item,(item,st['survivorGear'])
   assert st[key]==expected,(item,st[key],expected)
   assert st['encumbranceSlots']==initial['encumbranceSlots'],(item,st['encumbranceSlots'],initial['encumbranceSlots'])
   assert count(st,item)==before-1
   shot=call('/screenshot','POST',name='capacity_'+suffix)['path']
   result={'item':item,'capacity':st[key],'desiredBackpack':st['desiredBackpackCapacity'],'visibleToolbelt':st['visibleToolbeltSlots'],'screenshot':shot,'unencumberedSlots':st['unencumberedSlots'],'encumbranceSlots':st['encumbranceSlots']}
   remove(slot.title());st=state();assert st[key]==(baseline if slot=='belt' else 45),(item,st[key]);assert count(st,item)==before
   result.update({'removedCapacity':st[key],'itemConserved':True,'passed':True});results.append(result)
   (out/'capacity_results.json').write_text(json.dumps(results,indent=2));print('PASS',item,expected,'->',st[key],flush=True)
 finally:
  for a in ('strength','constitution'):
   old=initial['gearAttributes'][a];call('/gearfixture','POST',confirm='CodexTest',attribute=a,value=old['Current'],potential=old['Potential'])
  call('/console','POST',cmd='debuff god')
