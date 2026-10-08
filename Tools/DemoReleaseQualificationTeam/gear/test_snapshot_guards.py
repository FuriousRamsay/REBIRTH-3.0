"""Synthetic offline oracle guard checks; no native or network calls."""
import contextlib,copy,io,json
from pathlib import Path
from qualify_snapshots import verify
item='rebirthGearDaypack'
before={'ok':True,'backpack':[{'slot':0,'name':item,'count':1,'quality':1}],'toolbelt':[], 'survivorGear':{'belt':'rebirthGearUtilityBelt'},'backpackCapacity':52,'toolbeltCapacity':10}
equipped=copy.deepcopy(before);equipped['backpack']=[];equipped['survivorGear']['backpack']=item;equipped['backpackCapacity']=65
returned=copy.deepcopy(before)
cases=[]
def run(name,a,b,c,passes):
    try:
        with contextlib.redirect_stdout(io.StringIO()): verify(a,b,c,item,'backpack',13)
        result=True
    except AssertionError as exc:
        result=False;detail=str(exc)
    assert result is passes,name
    cases.append({'case':name,'passed':True,'expectedAccepted':passes,'rejection':None if result else detail})
run('valid synthetic roundtrip',before,equipped,returned,True)
r=copy.deepcopy(returned);r['survivorGear']['belt']='substitute';run('returned other gear mutation',before,equipped,r,False)
for field,bad in [('survivorGear',None),('survivorGear',[]),('survivorGear',{'belt':None}),('backpack',None),('backpack',{}),('toolbelt',None),('toolbelt',[None])]:
    r=copy.deepcopy(returned);r[field]=bad;run('malformed '+field+' '+repr(bad),before,equipped,r,False)
r=copy.deepcopy(returned);del r['survivorGear'];run('missing gear map',before,equipped,r,False)
report={'kind':'offline synthetic guard checks','nativeExecuted':False,'cases':cases}
Path(__file__).with_name('oracle_guard_results.json').write_text(json.dumps(report,indent=2)+'\n')
print('PASS',len(cases),'offline synthetic oracle checks; native qualification not performed')
