"""Parent-executed CodexTest CCTV corner sweeps through real raw mouse input only.
Requires native Defense UI open, four installed turrets, automatic mode OFF, and
an exclusive bridge lease. Captures need human review; stable aim is not collision proof.
"""
import argparse, math, os, time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need


def angle_delta(a,b): return (a-b+180)%360-180

def camera_angles(value):
    x,y,z=value['presentation']['cameraForward']
    return math.degrees(math.atan2(x,z)),math.degrees(math.atan2(y,math.hypot(x,z)))


class SweepRunner(Runner):
    def save(self):
        # Runner persists every request twice. Keep observations needed by this
        # sweep, rather than repeatedly serializing mesh/backpack debug dumps.
        for request in self.report.get('requests',[]):
            if request.get('endpoint')!='/camperturrets': continue
            value=request.get('reply')
            if not isinstance(value,dict): continue
            value.pop('lodGroups',None);value.pop('armorSurfaces',None)
            presentation=value.get('presentation')
            if isinstance(presentation,dict): presentation.pop('backpackInput',None)
        super().save()


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--entity',type=int,required=True)
    p.add_argument('--corner',type=int,choices=range(4),help='One bounded90second corner case; otherwise all four within240seconds')
    p.add_argument('--report',type=Path,required=True)
    a=p.parse_args()
    need(not a.report.exists(),'fresh report required')
    need(os.environ.get('REBIRTH_BRIDGE_SESSION') and os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'),'exclusive parent session required')
    report={'schema':1,'passed':False,'corners':[],'limits':['Mouse-only observed stable boundaries; no forced rig pose','Screenshots require visual review for truck intrusion','No remote-peer or exhaustive swept-volume collision claim','Original aim is not restored; CCTV is closed leaving a legal observed aim']}
    r=SweepRunner(report,a.report);start=time.monotonic();active=None;owned=None;initial=None;cleaning=False
    budget=90 if a.corner is not None else 240
    report['requestedCorners']=[a.corner] if a.corner is not None else list(range(4))

    def bounded(): need(cleaning or time.monotonic()-start<budget,'global'+str(budget)+'second sweep limit reached')
    def observe():
        bounded();v=r.call('/camperturrets',entity=a.entity)
        need(not any(c['enabled'] for c in v['corners']),'automatic turret enabled during sweep')
        for c in v['corners']:
            if c.get('runtimeBound'):
                need(all(isinstance(c[k],(int,float)) and math.isfinite(c[k]) for k in ('yaw','pitch','yawMin','yawMax','pitchMin','pitchMax')),'finite native angles/limits required')
                need(c['yawMin']-.05<=c['yaw']<=c['yawMax']+.05 and c['pitchMin']-.05<=c['pitch']<=c['pitchMax']+.05,'actual turret exceeded authored native limits')
        return v
    def wait(test,label):
        until=time.monotonic()+4
        while True:
            v=observe()
            if test(v): return v
            need(time.monotonic()<until,label);time.sleep(.04)
    def click(identifier):
        nodes=r.call('/ui/tree')['nodes'];found=[n for n in nodes if n.get('id')==identifier and n.get('clickable') and n.get('enabled') is not False]
        need(len(found)==1,'one enabled native control required: '+identifier)
        r.call('/ui/click','POST',path=found[0]['path'])
    def motion(x,y):
        bounded();need(active is not None and max(abs(x),abs(y))<=2,'bounded exact corner input required')
        r.call('/cctvmouse','POST',entity=a.entity,corner=active,x=float(x),y=float(y))
        # Raw samples finish after consumption, not necessarily after the .05s
        # authoritative control cadence and subsequent pose publication.
        time.sleep(.08)
    def corner(v):
        need(v['presentation']['cameraActive'] and v['presentation']['cameraCorner']==active,'exact CCTV lease changed')
        return v['corners'][active]
    def calibrate(axis):
        # Calibrate the rendered camera (same observation as the qualified
        # head-aim driver), not the potentially delayed server corner angles.
        for value in (.1,-.1,.25,-.25):
            v=observe();corner(v);before=camera_angles(v)[axis]
            motion(value if axis==0 else 0,value if axis else 0)
            until=time.monotonic()+.4
            while True:
                v=observe();corner(v);after=camera_angles(v)[axis]
                change=angle_delta(after,before) if axis==0 else after-before
                if abs(change)>.0001:
                    report.setdefault('calibration',[]).append({'corner':active,'axis':axis,'raw':value,'change':change})
                    # Native pitch increases downward; camera elevation increases
                    # upward. Preserve the sweep's down-positive convention.
                    response=change/value*(1 if axis==0 else -1)
                    return 1 if response>0 else -1
                if time.monotonic()>=until:break
                time.sleep(.04)
        raise AssertionError('axis has no observable mouse response at starting pose')
    def sweep(label,x,y):
        samples=[];stable=0;before=corner(observe())
        for i in range(100):
            motion(x,y);v=observe();c=corner(v)
            samples.append({'yaw':c['yaw'],'pitch':c['pitch']})
            movement=max(abs(angle_delta(c['yaw'],before['yaw'])),abs(c['pitch']-before['pitch']))
            stable=stable+1 if movement<.025 else 0;before=c
            # Each sample includes the native publication settling interval.
            if stable>=2: break
        else: raise AssertionError('no stable boundary within100axis samples: '+label)
        motion(0,0);settled=observe();c=corner(settled)
        need(max(abs(angle_delta(c['yaw'],before['yaw'])),abs(c['pitch']-before['pitch']))<.05,'boundary did not settle after neutral mouse')
        capture=r.call('/screenshot','POST',name='cctv_corner_'+str(active)+'_'+label)
        return {'label':label,'rawAxes':[x,y],'samples':samples,'stableSamples':stable,'settled':{k:c[k] for k in ('yaw','pitch','yawMin','yawMax','pitchMin','pitchMax')},'capture':capture,'visualReviewRequired':True}
    try:
        need(r.call('/state',sections='world')['world']['gameName']=='CodexTest','human save refused')
        r.start=r.call('/ping')['lastLogSeq'];report['surroundings']=r.call('/surroundings');initial=observe()
        need(initial['presentation']['open'] and not initial['presentation']['cameraActive'],'native Defense management UI must be open')
        need(all(c['installed'] and c['controllerEntity'] is None for c in initial['corners']),'four installed unreserved turrets required')
        report['initial']=initial
        for active in report['requestedCorners']:
            click('turretCctv'+str(active));owned=active;wait(lambda v:v['presentation']['cameraActive'] and v['presentation']['cameraCorner']==active,'CCTV failed to open')
            time.sleep(.12)
            sx,sy=calibrate(0),calibrate(1)
            entry={'corner':active,'axisSigns':[sx,sy],'boundaries':[]};report['corners'].append(entry)
            for label,x,y in [('left',-2*sx,0),('left_down',0,2*sy),('right_at_down',2*sx,0),('right_down',0,2*sy)]:
                entry['boundaries'].append(sweep(label,x,y));r.save()
            motion(0,0);r.call('/key','POST',name='E');wait(lambda v:not v['presentation']['cameraActive'] and v['corners'][active]['controllerEntity'] is None,'CCTV lease did not release');owned=None
        active=None;report['final']=observe()
        need([c['ammo'] for c in report['final']['corners']]==[c['ammo'] for c in initial['corners']],'sweep changed ammunition')
        need(report['final']['presentation']['localPlayerFirstPerson']==initial['presentation']['localPlayerFirstPerson'],'native player view not restored')
        r.errors();report['passed']=True
    except Exception as e: report['failure']=type(e).__name__+': '+str(e)
    finally:
        cleaning=True
        try:
            v=observe()
            if owned is not None and v['presentation']['cameraActive'] and v['presentation']['cameraCorner']==owned:
                active=v['presentation']['cameraCorner'];motion(0,0);r.call('/key','POST',name='E')
            report['cleanup']=observe()
        except Exception as e: report['cleanupFailure']=str(e);report['passed']=False
        report['seconds']=round(time.monotonic()-start,3);r.save()
    return 0 if report['passed'] else 1

if __name__=='__main__': raise SystemExit(main())
