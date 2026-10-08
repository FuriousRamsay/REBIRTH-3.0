"""Offline assertions for native bridge state captures. No HTTP/native actions."""
import argparse,json
from collections import Counter
from pathlib import Path

def load(path):
    value=json.loads(Path(path).read_text(encoding='utf-8-sig'))
    validate(value)
    return value

def validate(value):
    assert isinstance(value,dict), 'State must be an object'
    assert value.get('ok') is True, 'Not a successful native state reply'
    for key in ('backpack','toolbelt','survivorGear','backpackCapacity','toolbeltCapacity'):
        assert key in value, 'Missing '+key
    assert isinstance(value['survivorGear'],dict), 'Gear map must be an object'
    assert all(isinstance(k,str) and isinstance(v,str) for k,v in value['survivorGear'].items()), 'Malformed gear map'
    for area in ('backpack','toolbelt'):
        assert isinstance(value[area],list), 'Inventory must be an array'
        for entry in value[area]:
            assert isinstance(entry,dict), 'Inventory entry must be an object'
            assert isinstance(entry.get('name'),str), 'Missing item name'
            assert type(entry.get('count')) is int and entry['count']>0, 'Invalid item count'
    return value

def stacks(state):
    # Bridge omits full ItemValue bytes/modifications; this is DISPLAY fingerprint only.
    result=Counter()
    for section in ('backpack','toolbelt'):
        for entry in state[section]:
            assert isinstance(entry.get('count'),int) and entry['count']>0
            image={k:v for k,v in entry.items() if k not in ('slot','count')}
            result[json.dumps(image,sort_keys=True)]+=entry['count']
    return result

def verify(before,equipped,returned,item,slot,bonus):
    for snapshot in (before,equipped,returned): validate(snapshot)
    assert not before['survivorGear'].get(slot), 'Initial gear slot occupied'
    assert equipped['survivorGear'].get(slot)==item, 'Equipped identity mismatch'
    assert not returned['survivorGear'].get(slot), 'Gear not returned'
    key='backpackCapacity' if slot=='backpack' else 'toolbeltCapacity'
    assert equipped[key]==before[key]+bonus, 'Equipped capacity mismatch'
    assert returned[key]==before[key], 'Returned capacity mismatch'
    count=lambda st:sum(e['count'] for area in ('backpack','toolbelt') for e in st[area] if e['name']==item)
    assert count(equipped)==count(before)-1
    assert count(returned)==count(before)
    assert stacks(before)==stacks(returned), 'Displayed item properties/counts changed'
    # Reject changes to other gear while testing this route.
    assert {k:v for k,v in before['survivorGear'].items() if k!=slot}=={k:v for k,v in equipped['survivorGear'].items() if k!=slot}
    assert {k:v for k,v in before['survivorGear'].items() if k!=slot}=={k:v for k,v in returned['survivorGear'].items() if k!=slot}, 'Other gear changed on return'
    print('PASS native captured identity/capacity/display-stack roundtrip; full serialized custody, host transport and reconnect NOT QUALIFIED')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('before');p.add_argument('equipped');p.add_argument('returned')
    p.add_argument('--item',required=True);p.add_argument('--slot',choices=['backpack','belt'],required=True);p.add_argument('--bonus',type=int,required=True)
    a=p.parse_args();verify(load(a.before),load(a.equipped),load(a.returned),a.item,a.slot,a.bonus)


