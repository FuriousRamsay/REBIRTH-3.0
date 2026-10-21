"""Bounded real-input workspace checks. Parent opens the actual camper radial UI.

Requires an existing exclusive batch capability; never launches or quits a game.
This checks corner selection, filters and unchanged native actor inventory only.
Installation/drag/CCTV/combat must be qualified by their separate live flows.
"""
import argparse
import json
import os
from pathlib import Path
import time
from camper_consolidated_regression_live import Runner

def need(value,message):
    if not value: raise AssertionError(message)

def main():
    p=argparse.ArgumentParser();p.add_argument('--entity',type=int,required=True);p.add_argument('--report',required=True)
    args=p.parse_args();out=Path(args.report)
    need(not out.exists(),'preserve previous evidence')
    need(os.environ.get('REBIRTH_BRIDGE_SESSION') and os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'),'parent exclusive session required')
    bridge=Runner({},out.with_suffix('.requests.json'),session_path=os.environ['REBIRTH_BRIDGE_SESSION'])
    report={'schema':1,'passed':False,'scope':'real-input corner selection/filter/native actor preservation','steps':[],'session':bridge.scope}
    started=time.monotonic()
    def observe():
        result=bridge.call('/camperturrets',entity=args.entity)
        need(result.get('presentation',{}).get('open') is True,'actual camper management must already be open')
        return result
    def click(id,field,value):
        need(time.monotonic()-started<35,'bounded workspace case exceeded 35 seconds')
        nodes=bridge.call('/ui/tree')['nodes'];matches=[n for n in nodes if n.get('id')==id and n.get('clickable') is True]
        need(len(matches)==1,'exactly one visible clickable '+id+' required')
        target=matches[0];need(target.get('enabled') is not False,'disabled target refused')
        reply=bridge.call('/ui/click','POST',path=target['path'])
        until=time.monotonic()+2
        while True:
            state=observe()
            if state['presentation'].get(field)==value:break
            need(time.monotonic()<until,'actual presentation did not change after '+id)
            time.sleep(.1)
        report['steps'].append({'input':target,'reply':reply,'observed':state['presentation']})
        return bridge.call('/ui/tree')['nodes']
    try:
        need(bridge.call('/ping')['state']=='ingame','game not ready')
        before=bridge.call('/state',sections='inventory');report['before']=before
        names={cell.get('name') for cell in before.get('backpack',[]) if cell.get('count',0)>0}
        need({'rebirthCamperTurret','ammo9mmBulletBall','ammoArrowStone','ammoRocketHE'}<=names,'parent must supply positive turret/ammo and negative arrow/rocket controls in backpack')
        initial=observe();report['initial']=initial
        observed=initial['presentation'].get('backpackInput') or {}
        frame=observed.get('frame',0)
        live_slots=[s for s in observed.get('slots',[]) if s.get('count',0)>0]
        need(live_slots and all(0<=frame-s.get('nativeUpdateFrame',-100)<4 for s in live_slots),
             'native backpack descendant update loop is not live')
        report['nativeUpdateObservation']=observed
        nodes=bridge.call('/ui/tree')['nodes']
        source=[n for n in nodes if n.get('ctrl')=='XUiC_RebirthCamperTurretBackpackSlot' and (n.get('item') or {}).get('name')=='rebirthCamperTurret']
        need(len(source)==1 and source[0].get('tooltip'),'one visible named turret source required for selected-item check')
        reply=bridge.call('/ui/click','POST',path=source[0]['path'])
        nodes=bridge.call('/ui/tree')['nodes']
        selected=[n for n in nodes if n.get('id')=='camperInspectName']
        need(len(selected)==1 and selected[0].get('text')==source[0]['tooltip'],'native selected-item name did not fill the upper panel')
        detail_rect=selected[0]['rect'];bag_rect=source[0]['rect']
        need(detail_rect[1]+detail_rect[3]<bag_rect[1],'selected-item details are not above the backpack')
        description=[n for n in nodes if n.get('id')=='camperInspectDescription' and n.get('text')]
        need(description,'selected turret description missing')
        report['selectedItem']={'input':source[0],'reply':reply,'name':selected[0],'description':description[0]}
        for corner in range(4):click('turretCorner'+str(corner),'selectedCorner',corner)
        nodes=click('filterTurrets','backpackFilter',1)
        slots=[n for n in nodes if n.get('ctrl')=='XUiC_RebirthCamperTurretBackpackSlot' and n.get('item')]
        need(slots,'turret filter did not expose the actual supplied turret')
        need(all(n['item']['name']=='rebirthCamperTurret' for n in slots),'turret filter exposes another item')
        nodes=click('filterAmmo','backpackFilter',2)
        slots=[n for n in nodes if n.get('ctrl')=='XUiC_RebirthCamperTurretBackpackSlot' and n.get('item')]
        need(any(n['item']['name']=='ammo9mmBulletBall' for n in slots),'ammo filter omitted supplied firearm control')
        need(all('arrow' not in n['item']['name'].lower() and 'rocket' not in n['item']['name'].lower() for n in slots),'ammo filter exposes arrows/rockets')
        click('filterAmmo','backpackFilter',0)
        after=bridge.call('/state',sections='inventory');report['after']=after
        for area in ('backpack','toolbelt'):
            need(area in before and area in after,'actual inventory area missing: '+area)
            need(before[area]==after[area],'presentation gestures changed native '+area)
        # Owner requested no further screenshots; native state and input receipts are retained.
        report['passed']=True
    except Exception as error:
        report['failure']=type(error).__name__+': '+str(error)
    finally:
        report['seconds']=round(time.monotonic()-started,3)
        out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(report,indent=2),encoding='utf-8')
    return 0 if report['passed'] else 1

if __name__=='__main__':raise SystemExit(main())


