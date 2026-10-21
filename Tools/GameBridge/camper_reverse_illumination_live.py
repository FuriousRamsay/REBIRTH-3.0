"""Parent-only night reverse test: real driving input, read-only light observations.

Prepare fitted camper in Travel, local native driver seated, lamps working,
gameplay UI closed and a flat clear road with room both behind and ahead.
Screenshots require human review; numeric light settings do not prove appearance.
"""
import argparse
import os
import time
from pathlib import Path
from camper_consolidated_regression_live import Runner, need


def reverse_profile(p):
    need(p['forwardVelocity'] < -.05, 'actual reverse velocity required')
    need(p['rearEmittingCount'] >= 2, 'both actual rear emitters required')
    need(abs(p['rearMaxEffectiveIntensity']-3.25) < .02, 'reverse beam must be 3.25')
    need(abs(p['rearMaxRange']-6) < .02, 'reverse beam range must be 6m')
    need(abs(p['rearMaxSpotAngle']-60) < .02, 'reverse beam angle must be 60 degrees')


def released_profile(p):
    need(abs(p['forwardVelocity']) < .5, 'vehicle must be below safe walking-coast speed before exit')
    need(p['rearMaxEffectiveIntensity'] <= .65+.01, 'boost stuck after reverse')
    for lamp in p['rearLights']:
        if lamp['effectiveIntensity'] > 0:
            need(lamp['range'] <= 4.01 and lamp['spotAngle'] <= 48.01, 'reverse beam geometry stuck')


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--entity',type=int,required=True)
    parser.add_argument('--report',type=Path,required=True)
    a=parser.parse_args()
    need(not a.report.exists(),'fresh report required')
    need(os.environ.get('REBIRTH_BRIDGE_SESSION') and os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'),'exclusive parent session required')
    report={'schema':1,'passed':False,'limits':['Actual native input and light observations only',
        'Screenshots need human review of ground brightness, width and native red color',
        'Original paired chrome cylinders are retained lamp bezels, not added geometry',
        'No broken-lamp, bare-pickup or remote-peer qualification']}
    r=Runner(report,a.report);start=time.monotonic();owned=False
    def observe():
        need(time.monotonic()-start<45,'45second test budget exceeded')
        value=r.call('/camper',entity=a.entity)
        need(value['entity']==a.entity,'exact camper required')
        need(value['presentation']['bodyInstalled'] and value['presentation']['mode']=='Travel','fitted Travel camper required')
        return value
    try:
        world=r.call('/state',sections='world')['world']
        need(world['gameName']=='CodexTest' and not world['isDaytime'],'disposable night fixture required')
        r.start=r.call('/ping')['lastLogSeq'];report['surroundings']=r.call('/surroundings')
        before=observe();report['before']=before
        need(before['vehicleDriverEntityId']==before['localPlayerEntityId'] and before['localPlayerEntityId']>=0,'local native driver required')
        need(abs(before['presentation']['forwardVelocity'])<.08,'stationary initial fixture required')
        owned=True
        r.call('/move','POST',forward=-1,seconds=1.25)
        reverse=observe();reverse_profile(reverse['presentation']);report['reverse']=reverse
        report['reverseCapture']=r.call('/screenshot','POST',name='camper_actual_night_reverse_beams')
        # Opposite native input brakes either direction, including a downhill
        # forward roll after reverse crosses zero. Never set native velocity.
        for _ in range(16):
            v=observe()
            velocity=v['presentation']['forwardVelocity']
            if -.05<=velocity<.5:break
            r.call('/move','POST',forward=1 if velocity<0 else -1,seconds=.2)
        else:raise AssertionError('native braking did not settle within bounded inputs')
        r.call('/stop','POST');time.sleep(.15)
        after=observe();released_profile(after['presentation']);report['after']=after
        report['afterCapture']=r.call('/screenshot','POST',name='camper_reverse_released_native_lamps')
        r.errors();report['passed']=True
    except Exception as error:
        report['failure']=type(error).__name__+': '+str(error)
    finally:
        if owned:
            try:
                r.call('/stop','POST')
                v=observe()
                if v['localPlayerAttachedEntityId']==a.entity:
                    need(abs(v['presentation']['forwardVelocity'])<.5,'refuse vehicle dismount above walking-coast speed')
                    r.call('/key','POST',name='E')
                    report['cleanup']=observe()
                    need(report['cleanup']['localPlayerAttachedEntityId']!=a.entity,'native exit did not release driver')
                    until=time.monotonic()+5
                    while abs(report['cleanup']['presentation']['forwardVelocity'])>=.08:
                        need(time.monotonic()<until,'native parked vehicle did not settle after exit')
                        time.sleep(.2);report['cleanup']=observe()
            except Exception as error:
                report['cleanupFailure']=str(error);report['passed']=False
        report['seconds']=round(time.monotonic()-start,3);r.save()
    return 0 if report['passed'] else 1


if __name__=='__main__':raise SystemExit(main())
