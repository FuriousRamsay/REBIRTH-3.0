"""Native remove/button-install/Shift-install roundtrips on one empty-equipped corner."""
import argparse
import os
from pathlib import Path
import time
from camper_consolidated_regression_live import Runner


def need(value, message):
    if not value:
        raise AssertionError(message)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--entity',type=int,required=True)
    p.add_argument('--corner',type=int,choices=range(4),required=True)
    p.add_argument('--report',type=Path,required=True)
    args=p.parse_args()
    need(not args.report.exists() and os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'),'fresh report and parent lease required')
    report={'passed':False,'steps':[],'scope':'native turret removal/button and Shift installation, backpack conservation'}
    b=Runner(report,args.report)
    started=time.monotonic()

    def state():
        need(time.monotonic()-started<35,'roundtrip exceeded35seconds')
        return b.call('/camperturrets',entity=args.entity)

    def wait(installed):
        end=time.monotonic()+2
        while True:
            v=state()
            if v['corners'][args.corner]['installed']==installed and not v['presentation']['waiting']:
                return v
            need(time.monotonic()<end,'native installation state did not settle')
            time.sleep(.06)

    def click(identifier,**kw):
        found=[n for n in b.call('/ui/tree')['nodes'] if n.get('id')==identifier and n.get('clickable')]
        need(len(found)==1,'one native control required: '+identifier)
        return b.call('/ui/click','POST',path=found[0]['path'],**kw)

    def count(actor):
        return sum(s.get('count',0) for s in actor['backpack'] if s.get('name')=='rebirthCamperTurret')

    try:
        before=state();corner=before['corners'][args.corner]
        need(before['presentation']['open'] and corner['installed'],'installed corner and open management required')
        need(not any(a['count'] for a in corner['ammo']) and not corner['silencer'] and not any(corner['scopes']),
             'roundtrip requires no ammunition or attachments on selected corner')
        actor=b.call('/state',sections='inventory');report['before']=before;report['actorBefore']=actor
        click('turretCorner'+str(args.corner))
        for method in ('button','shift'):
            click('turretInstall'+str(args.corner));removed=wait(False)
            returned=b.call('/state',sections='inventory')
            need(count(returned)==count(actor)+1,'removal did not return one turret to backpack')
            if method=='button':
                click('turretInstall'+str(args.corner))
            else:
                source=[n for n in b.call('/ui/tree')['nodes'] if n.get('ctrl')=='XUiC_RebirthCamperTurretBackpackSlot'
                        and (n.get('item') or {}).get('name')=='rebirthCamperTurret']
                need(len(source)>=1,'native turret source required')
                b.call('/ui/click','POST',path=source[0]['path'],shift=1)
            restored=wait(True)
            if restored['corners'][args.corner]['enabled']!=corner['enabled']:
                click('turretEnabled'+str(args.corner))
            after=b.call('/state',sections='inventory')
            need(count(after)==count(actor),'installation did not consume exactly one backpack turret')
            # Generic Install may consume either identical turret stack and leave
            # the returned one in another physical bag slot. Other items must
            # retain their exact cells; turret metadata/count must be conserved.
            for area in ('backpack','toolbelt'):
                old=[s for s in actor[area] if s.get('name')!='rebirthCamperTurret']
                new=[s for s in after[area] if s.get('name')!='rebirthCamperTurret']
                need(new==old,'roundtrip changed unrelated '+area+' cells')
                identity=lambda cells:sorted((s['name'],s.get('meta'),s['count']) for s in cells if s.get('name')=='rebirthCamperTurret')
                need(identity(after[area])==identity(actor[area]),'roundtrip changed turret identity/count in '+area)
            need(state()['presentation']['cursorCount']==0,'native cursor retained an item')
            report['steps'].append({'method':method,'removed':removed['corners'][args.corner],
                                    'restored':state()['corners'][args.corner]})
        report['passed']=True
    except Exception as error:
        report['failure']=type(error).__name__+': '+str(error)
    finally:
        report['seconds']=round(time.monotonic()-started,3);b.save()
    return 0 if report['passed'] else 1


if __name__=='__main__':
    raise SystemExit(main())
