"""Reconcile actual native player, crate and fixture-storage item counts."""
import json
from collections import Counter
from pathlib import Path
root=Path(__file__).resolve().parents[2]
stage=root/'Tools/Profiler/out/purge-b27'
def read(name):return json.loads((stage/name).read_text(encoding='utf-8-sig'))
def inventory(row):return CounterSum(row.get('backpack',[])+row.get('toolbelt',[]))
def CounterSum(items):
    out=Counter()
    for item in items:
        if item and item.get('name'):out[item['name']]+=item['count']
    return out
before=inventory(read('collection-before-inventory.json'))
crate=CounterSum([n['item'] for n in read('collection-delayed-ui.json')['nodes'] if n['id'].startswith('contextContainerGridSlot') and n.get('item')])
stored=CounterSum([n['item'] for n in read('collection-space-stored-ui.json')['nodes'] if n['id'].startswith('contextContainerGridSlot') and n.get('item')])
after=inventory(read('custody-Access-084650-after-inventory.json'))
expected=before+crate;actual=after+stored
delta={k:actual[k]-expected[k] for k in set(expected)|set(actual) if actual[k]!=expected[k]}
observation=json.loads(read('custody-Access-084650-after-collection.json')['results'][0]['output'][0])
result={'scope':'Native item-name/count conservation only; not per-instance mod/quality identity or remote-client acceptance.',
        'reward':dict(crate),'storedTestWeapons':dict(stored),'countDelta':delta,
        'crateRemoved':not any(c['id']==644 for c in observation['nativeSupplyCrates']),
        'account':observation['supply']}
result['pass']=not delta and result['crateRemoved'] and result['account']=={'observedCredits':187,'spentCredits':180,'earnedDrops':4,'deliveredDrops':4,'inFlight':0,'deliveryPhase':'none'}
(stage/'collection-conservation-review.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
assert result['pass'],'Collection conservation failed: inspect raw snapshots'
