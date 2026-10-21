"""CodexTest actual CCTV head-aim/shot driver. Scoped GameBridge raw-mouse-axis input, no runtime aiming.
Parent must prepare current CCTV lease, clear backdrop, all automatic turrets disabled,
powered single-projectile ammo, one durable zombie >15m from player and inside loaded range.
Does not spawn/teleport targets, disable guard, launch/build, or claim hit-region proof.
"""
import argparse,json,math,os,time
from pathlib import Path
from camper_consolidated_regression_live import need
from camper_cctv_corner_sweep_live import SweepRunner

def angle(v):return [math.degrees(math.atan2(v[0],v[2])),math.degrees(math.atan2(v[1],math.hypot(v[0],v[2])))]
def delta(a,b):return (a-b+180)%360-180
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--entity',type=int,required=True);p.add_argument('--corner',type=int,required=True);p.add_argument('--target',type=int,required=True);p.add_argument('--shots',type=int,default=3);p.add_argument('--report',type=Path,required=True);a=p.parse_args();need(1<=a.shots<=10 and 0<=a.corner<4,'bounded shot/corner args');need(not a.report.exists(),'fresh report required');need(os.environ.get('REBIRTH_BRIDGE_SESSION') and os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'),'exclusive parent session required')
 out={'schema':1,'passed':False,'input':'Exclusive GameBridge raw mouse axes and native GUI.LeftClick only','shots':[],'limits':['Head-point aim and actual health loss do not identify native damage hit region','Target death ends further shots; use durable target for requested count','No remote-peer qualification']};r=SweepRunner(out,a.report);start=time.monotonic();identity=None
 def motion(dx,dy):
  need(time.monotonic()-start<90,'bounded90s driver timeout')
  r.call('/cctvmouse','POST',entity=a.entity,corner=a.corner,x=float(dx),y=float(dy));time.sleep(.08)
 def observe():
  v=r.call('/camperturrets',entity=a.entity);t=r.call('/entities',entity=a.target)['entities'][0];need(v['presentation']['cameraActive'] and v['presentation']['cameraCorner']==a.corner,'current exact CCTV lease required');need(t['entityInstance']==identity or identity is None,'target instance changed');return v,t
 def aim(v,t):
  h=t['headPosition'];pos=v['presentation']['cameraWorldPosition'];d=[h[k]-pos[i] for i,k in enumerate(('x','y','z'))];distance=math.sqrt(sum(x*x for x in d));need(distance<v['corners'][a.corner]['loadedRange'],'target beyond native ammo range');want=angle(d);current=angle(v['presentation']['cameraForward']);return [delta(want[0],current[0]),delta(want[1],current[1])],current
 try:
  need(r.call('/state',sections='world')['world']['gameName']=='CodexTest','human save refused');r.start=r.call('/ping')['lastLogSeq'];out['surroundings']=r.call('/surroundings');v,t=observe();identity=t['entityInstance'];need(t['alive'],'living target required');need(not any(c['enabled'] for c in v['corners']),'disable all automatic turrets via real controls first');need(v['corners'][a.corner]['loadedSlot']>=0,'loaded single-projectile ammo required');out['initial']={'turret':v,'target':t}
  nearby=r.call('/entities',radius=100,limit=200)['entities'];row=[x for x in nearby if x['id']==a.target];need(len(row)==1 and row[0]['distance']>15,'target must stay outside15m guard engagement region')
  # Calibrate actual mouse response in each direction; no guessed sensitivity/inversion.
  def calibrate(axis):
   for sample in (.05,-.05):
    before_v,before_t=observe();_,before=aim(before_v,before_t)
    motion(sample if axis==0 else 0,sample if axis else 0)
    after_v,after_t=observe();_,after=aim(after_v,after_t)
    response=(delta(after[axis],before[axis]) if axis==0 else after[axis]-before[axis])/sample
    if .0001<abs(response)<10000:
     # Undo only a sample that actually moved; the other direction may be clamped.
     motion(-sample if axis==0 else 0,-sample if axis else 0)
     return response
   raise AssertionError('no usable '+('horizontal','vertical')[axis]+' real mouse response in either direction')
  sx,sy=calibrate(0),calibrate(1);out['degreesPerRawAxisUnit']=[sx,sy]
  for shot in range(a.shots):
   for iteration in range(120):
    v,t=observe();need(t['alive'],'target died before requested count; prepare durable target');error,_=aim(v,t)
    if max(abs(x) for x in error)<=.12:break
    motion(max(-2,min(2,error[0]/sx)),max(-2,min(2,error[1]/sy)))
   else:raise AssertionError('native mouse servo did not converge on actual head')
   baseline=t['health'];count=v['corners'][a.corner]['shotsConsumed'];need(count is not None,'native shot observation unavailable');r.call('/screenshot','POST',name='cctv_head_aim_'+str(shot));r.call('/press','POST',action='gui.LeftClick',seconds=.055);deadline=time.monotonic()+3
   while True:
    after,target=observe()
    if after['corners'][a.corner]['shotsConsumed']>count:break
    need(time.monotonic()<deadline,'native firing consumed no ammunition');time.sleep(.05)
   time.sleep(.12);after,target=observe();entry={'index':shot,'aimErrorDegrees':error,'beforeHealth':baseline,'afterHealth':target['health'],'shotsConsumedDelta':after['corners'][a.corner]['shotsConsumed']-count,'ammoBefore':v['corners'][a.corner]['ammo'],'ammoAfter':after['corners'][a.corner]['ammo'],'target':target,'turret':after,'capture':r.call('/screenshot','POST',name='cctv_head_impact_'+str(shot))};out['shots'].append(entry);r.save();need(entry['shotsConsumedDelta']==1,'pulse fired multiple native rounds; result cannot claim per-shot accuracy');need(target['health']<baseline,'head-aligned native shot missed actual target health');time.sleep(max(.2,after['corners'][a.corner].get('shotInterval') or .5))
  r.errors();out['passed']=True
 except Exception as e:out['failure']=type(e).__name__+': '+str(e)
 finally:r.save()
 return 0 if out['passed'] else 1
if __name__=='__main__':raise SystemExit(main())





