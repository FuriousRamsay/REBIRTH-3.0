"""Real-input, CodexTest-only camper presentation regression phases. Parent owns launch/fixture.
No rig/authority helpers are invoked. Screenshots require visual/audio review; state PASS is not visual PASS.
"""
import argparse,json,os,time
from pathlib import Path
from camper_consolidated_regression_live import Runner,need

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--phase',choices=['duke','duke-native','manual','wire'],required=True);p.add_argument('--entity',type=int);p.add_argument('--corner',type=int,default=0);p.add_argument('--relay',type=float,nargs=3);p.add_argument('--wire-slot',type=int);p.add_argument('--swap-slot',type=int);p.add_argument('--report',type=Path,required=True);a=p.parse_args()
 need(not a.report.exists(),'fresh report required');need(os.environ.get('REBIRTH_BRIDGE_SESSION') and os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'),'exclusive parent bridge session required')
 report={'schema':1,'phase':a.phase,'passed':False,'visualAudioReviewRequired':True,'coverageLimits':['No remote-peer qualification','No screenshot existence interpreted as visual quality','Manual current-aim shots do not prove target hit accuracy']};r=Runner(report,a.report);opened=False
 def obs():return r.call('/camperturrets',entity=a.entity)
 def wait(predicate,label,timeout=5):
  until=time.monotonic()+timeout
  while True:
   v=obs()
   if predicate(v):return v
   need(time.monotonic()<until,label);time.sleep(.1)
 def click(identifier):
  n=[x for x in r.call('/ui/tree')['nodes'] if x.get('id')==identifier and x.get('clickable') and x.get('enabled') is not False];need(len(n)==1,'one real clickable control required '+identifier);r.call('/ui/click','POST',path=n[0]['path'])
 def shotcount(v):return sum(x['count'] for x in v['corners'][a.corner]['ammo'])
 def capture(label):report.setdefault('captures',[]).append(r.call('/screenshot','POST',name=label))
 try:
  state=r.call('/state',sections='world,buffs');need(state['world']['gameName']=='CodexTest','human save refused');r.start=r.call('/ping')['lastLogSeq'];report['surroundings']=r.call('/surroundings');r.call('/cleararea','POST',radius=40);r.call('/restore','POST')
  if a.phase in ('duke','duke-native'):
   # Setup invokes the native buff, whose actual onSelfBuffStart tries to pin the note.
   r.call('/console','POST',cmd='buff buffDukeNote');time.sleep(.4)
   nodes=r.call('/ui/tree')['nodes'];report['ui']=nodes
   visible=any('THE DUKE LEFT YOU A NOTE TO READ' in (n.get('text') or '').upper() for n in nodes);need(visible==(a.phase=='duke-native'),'Duke pin does not match explicitly prepared native/REBIRTH progression mode')
   capture('duke_native_buff_pin_suppressed');r.call('/console','POST',cmd='debuff buffDukeNote')
   report['coverageLimits'].append('English text absence only; repeat native progression separately to verify retained native pin')
  elif a.phase=='manual':
   need(a.entity is not None and 0<=a.corner<4,'exact prepared entity/corner required');before=obs();need(before['presentation']['open'] and not before['presentation']['cameraActive'],'management must already be open');need(before['corners'][a.corner]['loadedSlot']>=0,'powered loaded test turret required');click('turretCctv'+str(a.corner));opened=True
   v=wait(lambda v:v['presentation']['cameraActive'],'CCTV did not open');need(v['presentation']['localPlayerFirstPerson'] is False,'complete native third-person view missing');capture('cctv_player_before_manual_shot');time.sleep(.2)
   samples=[]
   for i in range(3):
    baseline=obs();r.call('/press','POST',action='GUI.LeftClick',seconds=.08);after=wait(lambda x:shotcount(x)<shotcount(baseline),'real manual fire consumed no ammunition');need(after['corners'][a.corner]['shotsConsumed']>baseline['corners'][a.corner]['shotsConsumed'],'native shot counter did not advance');capture('cctv_manual_shot_'+str(i));samples.append(after);time.sleep(max(.15,after['corners'][a.corner].get('shotInterval') or 1))
   report['shots']=samples;r.call('/key','POST',name='E');opened=False;v=wait(lambda v:not v['presentation']['cameraActive'],'CCTV exit failed');need(v['presentation']['localPlayerFirstPerson']==before['presentation']['localPlayerFirstPerson'],'prior player view not restored');report['review']='Inspect captures for complete head when in frame, no near-camera muzzle cloud; listen for unchanged shot sounds. For precision, repeat with an independently positioned target and observe actual health, not these consumption checks.'
  else:
   need(a.entity is not None and a.relay and a.wire_slot and a.swap_slot and a.wire_slot!=a.swap_slot,'physical relay coordinates and existing wire/swap slots required');r.call('/select','POST',slot=a.wire_slot);r.call('/lookat','POST',x=a.relay[0],y=a.relay[1],z=a.relay[2]);r.call('/press','POST',action='Secondary',seconds=.15)
   v=wait(lambda v:v['wirePreview']['ready'] and v['wirePreview']['previewActive'],'native wire preview not created after authority ACK');need(v['wirePreview']['source']=='wire_mesh','preview not attached to native hand wire mesh');capture('relay_native_hanging_wire');report['selected']=v['wirePreview'];nodes=r.call('/ui/tree')['nodes'];need(not any('RELAY SELECTED.' in (n.get('text') or '').upper() for n in nodes),'invented selection toast remains')
   # Actual second click on the same relay cancels selection.
   r.call('/press','POST',action='Secondary',seconds=.15);wait(lambda v:not v['wirePreview']['selected'] and not v['wirePreview']['previewActive'],'relay cancellation leaked native wire')
   r.call('/press','POST',action='Secondary',seconds=.15);wait(lambda v:v['wirePreview']['previewActive'],'preview did not reopen');r.call('/select','POST',slot=a.swap_slot);wait(lambda v:not v['wirePreview']['previewActive'] and not v['wirePreview']['selected'],'tool swap leaked preview');r.call('/select','POST',slot=a.wire_slot);report['review']='Inspect hanging wire capture and listen for native live/dead connection sound. Actual external-device commit and moving-endpoint/world reload are separate prepared phases.'
  r.errors();report['passed']=True
 except Exception as e:report['failure']=type(e).__name__+': '+str(e)
 finally:
  if opened:
   try:r.call('/key','POST',name='E')
   except Exception as e:report['cleanupFailure']=str(e)
  if a.phase in ('duke','duke-native'):
   try:r.call('/console','POST',cmd='debuff buffDukeNote')
   except Exception as e:report['cleanupFailure']=str(e)
  if a.phase=='wire' and a.swap_slot:
   try:r.call('/select','POST',slot=a.swap_slot)
   except Exception as e:report['cleanupFailure']=str(e)
  r.save()
 return 0 if report['passed'] and 'cleanupFailure' not in report else 1
if __name__=='__main__':raise SystemExit(main())

