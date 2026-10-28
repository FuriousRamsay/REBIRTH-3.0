"""Prepare real-input cosmetic round trips from an observed CodexTest UI snapshot.

Does not connect to or control the game. Parent exports rbcamperregprofile after
giving the chosen items and opening the target camper's native vehicle window.
"""
import argparse
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]


def prepare(profile, ids, entity):
    catalog=json.loads((ROOT/'Resources/CamperWraps/catalog.json').read_text(encoding='utf-8'))['wraps']
    rows=[next(r for r in catalog if r['id']==i) for i in ids]
    slots=profile['visibleSlots']
    cosmetics=[s for s in slots if s.get('role')=='cosmetic' and s.get('frameVehicleEntityId')==entity and s.get('windowGroupVehicleEntityId')==entity]
    if len(cosmetics)!=1 or not cosmetics[0].get('empty'):
        raise ValueError('Require exactly one observed empty cosmetic slot on the specified native vehicle.')
    if any(s.get('dragCursor') and not s.get('empty',True) for s in slots):
        raise ValueError('Native drag cursor must be empty.')
    returns=[s['path'] for s in slots if s.get('contextBackpack') and s.get('presented') and s.get('intersectsViewport') and s.get('empty')]
    if len(returns)<len(rows):
        raise ValueError('Need one distinct visible empty backpack cell per tested wrap AFTER items were given.')
    for row in rows:
        sources=[s for s in profile['sources'] if s['name']==row['item']]
        if len(sources)!=1 or sources[0]['count']!=1:
            raise ValueError('Require exactly one physical backpack item '+row['item'])
    target=cosmetics[0]['path']
    steps=[{'action':{'command':'cleararea','args':{'radius':40}}},{'action':{'command':'restore','args':{}}},{'expectWindowOpen':'vehicle'}]
    for row,back in zip(rows,returns):
        item=row['item']
        steps.extend([
            {'action':{'command':'drag','args':{'from.item':item,'to.path':target}}},
            {'expectUiItem':{'path':target,'item':item,'min':1,'max':1}},
            {'expectNoItem':item},
            {'screenshot':'wrap_'+row['id']+'_installed_ui'},
            {'action':{'command':'drag','args':{'from.path':target,'to.path':back}}},
            {'expectUiItem':{'path':back,'item':item,'min':1,'max':1}},
            {'expectNoUi':{'path':target,'item':item}},
            {'expectNoErrors':True},
        ])
    steps.extend([{'key':{'name':'Escape'}},{'screenshot':'wrap_collection_removed_original_finish'},{'expectNoErrors':True}])
    return {'name':'Camper cosmetic wrap native install/remove round trips','stopOnFailure':True,
            'status':'PREPARED_NOT_RUN','preconditions':f'Disposable CodexTest. Camper entity {entity} native vehicle window already open, guard ON, no threats; exact physical items and empty return cells match exported profile. Generated paths are observations, not guessed selectors. Screenshots require human visual inspection. Separate qualification must install each item, close UI and inspect hood/both sides/strips, then pickup/re-place and save/reload, and repeat on a remote client.',
            'steps':steps}


def main():
    p=argparse.ArgumentParser();p.add_argument('--profile',required=True,type=Path);p.add_argument('--entity',required=True,type=int);p.add_argument('--ids',default='MidnightCartographer,TheHungryRoad,DeadZoneRunner,ShotgunMessiah,JensClinic,FuriousRamsaySignature');p.add_argument('--output',type=Path,default=ROOT/'Tools/GameBridge/tests/camper_wrap_collection.json');a=p.parse_args()
    result=prepare(json.loads(a.profile.read_text(encoding='utf-8-sig')),a.ids.split(','),a.entity)
    a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(a.output)


if __name__=='__main__':main()
