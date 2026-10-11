"""Compare the prepared paused16 CodexTest saw after a normal save/relaunch.
Does not launch/quit, prepare fixtures, open UI or modify station state.
Native snapshot fields only; not complete metadata or multiplayer coverage.
"""
import json,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'TraderVoices'))
from test_live import call,root
out=root/'_Documentation/CraftingAudit_20261010'
baseline=json.loads((out/'LIVE_QUEUE_RELOAD_PAUSED16.json').read_text(encoding='utf-8'))
ping=call('/ping')
assert ping['state']=='ingame' and ping['pid']!=baseline['pid'],ping
assert call('/state',sections='world')['world']['gameName']=='CodexTest'
arrival=call('/surroundings')
after=call('/station',x=22,y=44,z=997)
before=baseline['before']
fields=['station','queueCapacity','queueCount','queue','outputCapacity','output','tools','fuel','burning']
differences={key:{'before':before[key],'after':after[key]} for key in fields if before[key]!=after[key]}
result={'pid':ping['pid'],'arrival':arrival,'before':before,'after':after,'differences':differences,'passed':not differences,'scope':'native exposed snapshot fields, closed paused saw, single player'}
(out/'LIVE_QUEUE_RELOAD_COMPARISON.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
assert not after['accessed'], 'Must compare before station activation'
assert not differences,json.dumps(differences,indent=2)
print('PASS exact paused16 queue/order/batches/timers/owners and exposed output/tools/fuel across game restart; metadata beyond snapshot and multiplayer not covered')
