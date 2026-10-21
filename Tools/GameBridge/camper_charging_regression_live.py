"""CodexTest real-input charge qualification. Parent owns launch and prepared real sources/cells.
Plan contains only observed UI/native input actions, never source/charge setters. NOT RUN.
"""
import argparse,json,math,os,time
from pathlib import Path
from camper_consolidated_regression_live import Runner,need
ALLOWED={'/activate','/key','/ui/click','/ui/drag','/lookat','/select','/press','/move'}
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',type=Path,required=True);p.add_argument('--report',type=Path,required=True);a=p.parse_args();plan=json.loads(a.plan.read_text(encoding='utf-8-sig'));need(plan['confirm']=='CodexTest','explicit disposable plan required');need(not a.report.exists(),'fresh report required');need(os.environ.get('REBIRTH_BRIDGE_SESSION') and os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'),'exclusive parent session required');entity=plan['entity'];seconds=plan.get('sampleSeconds',20);need(10<=seconds<=60,'bounded10..60second sample required');out={'schema':1,'passed':False,'samples':[],'limits':['Native battery UseTimes change measured, not bank buffer alone','No reload/remote durability claim','Driving test requires clear level track and slow actual forward input']};r=Runner(out,a.report);original=None
 def actions(name):
  need(name in plan and plan[name],'nonempty observed real-input plan required '+name)
  for x in plan[name]:need(x['endpoint'] in ALLOWED,'non-input setter refused');r.call(x['endpoint'],'POST',**x.get('args',{}));time.sleep(.15)
 def energy():
  v=r.call('/energy',entity=entity);need(v['authoritative'] and v['registered'] and not v['held'],'current authoritative unheld source state required');need(v['externalRelayLinkCount']==0,'external relay links must be absent for isolation');need(all(s['kind']!='Generator' or not s['enabled'] for s in v['sources']),'generator must stay OFF');return v
 def celluse(v):
  cells={(s['id'],c['slot']):(c['name'],c['count'],float(c['useTimes']),float(c['maxUseTimes'])) for s in v['sources'] if s['kind']=='Battery' for c in s['components'] if c['count']>0};need(cells and all(x[0]=='carBattery' and x[1]==1 and math.isfinite(x[2]) and x[3]>0 for x in cells.values()),'actual battery components and native UseTimes fields required');return cells
 def gain(before,after):
  a,b=celluse(before),celluse(after);need(a.keys()==b.keys(),'cell identity/slot set changed');need(all(a[k][:2]==b[k][:2] and a[k][3]==b[k][3] for k in a),'battery type/count/maxuse changed');return sum(a[k][2]-b[k][2] for k in a)
 def sample(label):
  values=[];until=time.monotonic()+seconds
  while True:
   v=energy();out['samples'].append({'phase':label,'realTime':time.monotonic(),'energy':v});r.save();values.append(v)
   if time.monotonic()>=until:return values
   time.sleep(1)
 try:
  original=r.call('/state',sections='world')['world'];need(original['gameName']=='CodexTest','human save refused');r.start=r.call('/ping')['lastLogSeq'];out['surroundings']=r.call('/surroundings');r.call('/cleararea','POST',radius=40);r.call('/restore','POST')
  # Parent-provided genuine UI actions select sources, turn generator off, battery/solar on,
  # enter Camp and close management; no bank state is manufactured.
  actions('prepareSolarCamp');v=energy();need(v['mode']=='Camp' and v['demandWatts']==0 and not v['mobility']['localPlayerIsDriver'],'Camp with no driver or active demand required');need(any(s['kind']=='Solar' and s['enabled'] and any(c['count'] for c in s['components']) for s in v['sources']),'real enabled populated solar source required');need(any(s['kind']=='Battery' and s['enabled'] for s in v['sources']),'enabled real battery bank required');need(any(c[2]>=5 for c in celluse(v).values()),'partly depleted actual battery required')
  day=max(1,original['day']);r.call('/console','POST',cmd=f'settime {day} 0 0');time.sleep(2);night=sample('night_engine_off');need(all(not v['mobility']['localPlayerIsDriver'] and v['demandWatts']==0 and all(s['availableWatts']==0 for s in v['sources'] if s['kind']=='Solar') for v in night),'night isolation missing');need(abs(gain(night[0],night[-1]))<.0001,'battery charge changed at night without engine or consumers')
  r.call('/console','POST',cmd=f'settime {day} 12 0');time.sleep(2);sun=sample('day_surplus');need(any(sum(s['availableWatts'] for s in v['sources'] if s['kind']=='Solar')>0 for v in sun),'no measured daytime solar production');need(all(not v['mobility']['localPlayerIsDriver'] and v['demandWatts']==0 for v in sun),'solar sample had unplanned consumer demand');out['solarChargeGainUseTimes']=gain(sun[0],sun[-1]);need(out['solarChargeGainUseTimes']>0,'no actual battery cell charge gain from surplus daytime solar')
  # Remove solar funding by night, transition Travel through real controls and mount normally.
  r.call('/console','POST',cmd=f'settime {day} 0 0');actions('toTravelAndMount');start=energy();need(start['mode']=='Travel' and start['mobility']['localPlayerIsDriver'],'native Travel driver attachment required');need(all(s['availableWatts']==0 for s in start['sources'] if s['kind']=='Solar'),'solar still producing in engine isolation');origin=start['position'];until=time.monotonic()+seconds;driven=[start]
  while time.monotonic()<until:
   move=r.call('/move','POST',forward=plan.get('slowForward',.15),seconds=1);need(move.get('movement')=='vehicle' and move.get('vehicleEntityId')==entity,'native vehicle driver input not routed');v=energy();need(v['mobility']['localPlayerIsDriver'],'driver detached');driven.append(v);out['samples'].append({'phase':'slow_drive_night','energy':v,'move':move});r.save()
  need(math.dist(origin,driven[-1]['position'])>1,'vehicle did not actually travel');out['engineChargeGainUseTimes']=gain(start,driven[-1]);need(out['engineChargeGainUseTimes']>0,'no actual battery cell charge gain while driving without solar');actions('stopAndDismount');parked=sample('engine_off_night');need(all(not x['mobility']['localPlayerIsDriver'] and x['demandWatts']==0 for x in parked),'engine-off isolation not prepared');need(abs(gain(parked[0],parked[-1]))<.0001,'unfunded parked night charge change')
  # Genuine mounted turret shot proves internal supply admission without any external cable.
  actions('openCctv');v=r.call('/camperturrets',entity=entity);corner=plan['corner'];need(v['presentation']['cameraActive'] and v['presentation']['cameraCorner']==corner,'current real CCTV required');need(energy()['externalRelayLinkCount']==0,'external cable appeared');before=v['corners'][corner]['shotsConsumed'];r.call('/press','POST',action='gui.LeftClick',seconds=.06);time.sleep(.5);after=r.call('/camperturrets',entity=entity);need(after['corners'][corner]['shotsConsumed']>before,'internally supplied turret did not fire');reportshot={'before':v,'after':after,'energy':energy()};out['internalTurretWithoutExternalWire']=reportshot;r.call('/key','POST',name='E');r.errors();out['passed']=True
 except Exception as e:out['failure']=type(e).__name__+': '+str(e)
 finally:
  try:
   r.call('/release','POST')
   if original:r.call('/console','POST',cmd=f"settime {original['day']} {original['hour']} {original['minute']}")
  except Exception as e:out['cleanupFailure']=str(e)
  r.save()
 return 0 if out['passed'] and 'cleanupFailure' not in out else 1
if __name__=='__main__':raise SystemExit(main())


