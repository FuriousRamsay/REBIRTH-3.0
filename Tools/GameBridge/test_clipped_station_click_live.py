"""Live regression for clipped-slot bridge clicks; CodexTest fixture only.
Outfit Designer at16,45,998 must hold one Armor Crafting Tools in slot0.
Backpack's first empty slot must be in its fourth row (33..43), which is
partially clipped at the default1280x720 station layout. Starts/ends closed.
"""
import json,sys,time,urllib.error
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'TraderVoices'))
from test_live import call,root,session
assert call('/state',sections='world')['world']['gameName'] == 'CodexTest', 'Disposable CodexTest session required'
assert call('/ping')['state']=='ingame'
call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
station=dict(x=16,y=45,z=998)
tool='FuriousRamsayArmorCrafting'
window='workstation_SonjaOutfitDesigner'
base=window+'/rebirthStationRootBasic/rebirthPersonalCraftingRoot/rebirthCraftingBodyZone/'
initial=call('/station',**station)
assert not initial['accessed'] and initial['tools']==[dict(name=tool,count=1,slot=0)],initial
seq=call('/ping')['lastLogSeq']
opened=call('/activate','POST',block=1,**station)
assert window in opened['openWindows'],opened
call('/ui/click','POST',path=base+'rebirthCraftingRightZone/windowToolsForge/content/inventory/0',shift=1)
tree=call('/ui/tree')
slots=[n for n in tree['nodes'] if n.get('item',{} ) and n['item']['name']==tool and '/rebirthCraftingInventoryViewport/inventory/' in n['path']]
assert len(slots)==1,slots
node=slots[0]
assert 33<=int(node['id'])<=43, 'Fixture must return tool into partially clipped fourth row'
before=call('/state',sections='inventory')
try:
    reply=call('/ui/click','POST',path=node['path'],shift=1)
except urllib.error.HTTPError as exc:
    rejection=json.load(exc)
    assert exc.code==409 and 'clipped or covered' in str(rejection),rejection
else: raise AssertionError('Clipped click falsely succeeded: '+str(reply))
after=call('/state',sections='inventory')
assert before['backpack']==after['backpack'],'Rejected click mutated backpack'
call('/ui/scroll','POST',id='rebirthCraftingInventoryScrollTrack',delta=-1)
time.sleep(.5)
accepted=call('/ui/click','POST',path=node['path'],shift=1)
call('/key','POST',name='Escape')
final=call('/station',**station)
assert not final['accessed'] and final['tools']==initial['tools'],final
assert final['output']==initial['output'],'Tool routed to output'
errors=call('/log',since=seq,level='error')
assert errors['count']==0,errors
result=dict(passed=True,rejected=rejection,accepted=accepted,initial=initial,final=final,errors=errors)
(root/'_Documentation/CraftingAudit_20261010/LIVE_CLIPPED_CLICK_TEST.json').write_text(json.dumps(result,indent=2))
print('PASS: clipped slot rejected without mutation; scroll + same slot click transfers to tool slot; output unchanged')
