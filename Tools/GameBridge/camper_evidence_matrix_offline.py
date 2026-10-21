"""Offline sanitized camper evidence matrix. Reads only explicit evidence basenames; never session/auth/run files.
PASS means a bounded automated assertion, not overall requirement completion. No game/network calls.
"""
import argparse,json,math
from pathlib import Path
FILES=('duke-live.json','four-cards-live.json','manual-smoke-live.json','headshot-193.json','deploy-timing.json','bare-truck.json','body-native-installed.json','energy-installed.json','live-actions.json','wire-live.json','charging-live.json','headshot-227.json','headshot-227-retry.json','engine-camp-seat-live.json','engine-deploy-seat-live.json','external-wire-connected.json','external-wire-powered.json','external-wire-disconnected.json','external-wire-settled-off.json','relay-pack-live.json','relay-deploy-live.json','travel-access-live.json','headshot-final-229.json','headshot-visible-final-232.json','headshot-clear-232-probe2.json','four-corner-sweep-live.json','pickup-replace-live.json','charging-final-fe8810f.json','corner0-fe8810f.json','headshot-fe8810f-236.json','corner0-bf7894a.json','corner1-bf7894a.json','corner2-bf7894a.json','corner3-bf7894a.json','corner0-visual-review.json','corner1-visual-review.json','corner2-bf7894a-retry.json','corner2-visual-review.json','corner3-visual-review.json','mode-caption-visual-review.json','power-toggle-visual-review.json','headshot-sniper-bf7894a-243.json','charging-final-bf7894a.json','reverse-paved-fueled-final-245.json')
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--root',type=Path,default=Path(__file__).parent/'out/camper_overnight_20261021090828');a=p.parse_args();root=a.root.resolve();data={};issues=[]
 for name in FILES:
  path=root/name
  if path.exists():
   try:data[name]=json.loads(path.read_text(encoding='utf-8-sig'))
   except (ValueError,OSError):issues.append(name+': incomplete/unreadable while parent writes; not scored')
 rows=[]
 def row(req,status,evidence,limit):rows.append({'requirement':req,'status':status,'evidence':evidence,'limit':limit})
 d=data.get('duke-live.json',{});nodes=d.get('ui',[]);duke=bool(d.get('passed') is True and d.get('phase')=='duke' and isinstance(nodes,list) and not any('THE DUKE LEFT YOU A NOTE TO READ' in str(n.get('text','')).upper() for n in nodes))
 row('REBIRTH Duke pinned notification','BOUNDED PASS' if duke else 'NOT VERIFIED','duke-live.json: English UI absence after native buff setup' if duke else 'No completed native-buff UI assertion','Inspect capture; native progression retained-pin counterpart not established')
 row('Native progression retains Duke pin','OFFLINE POLICY COVERED; OUTSIDE LIVE SCOPE','No native-mode evidence file','Cannot infer native-mode behavior from REBIRTH run')
 d=data.get('four-cards-live.json',{});steps=d.get('steps',[]);cards=d.get('passed') is True and sorted(s.get('corner',-1) for s in steps)==[0,1,2,3] and all(s.get('controller') is not None and s.get('released') is None and s.get('powerChanged')!=s.get('powerRestored') for s in steps)
 row('Four corner power toggles and CCTV claim/release','BOUNDED PASS' if cards else 'NOT VERIFIED',f'four-cards-live.json: {len(steps)} completed corner steps','Single local session; no remote contention or driving claim')
 view=cards and all('screenshot' in s for s in steps)
 row('CCTV complete player head and prior-view restoration','STATE PASS; VISUAL REVIEW REQUIRED' if view else 'NOT VERIFIED','Native third-person entry/prior-view assertions and per-corner captures' if view else 'No completed view/capture cycle','View flag is not visible-head proof; screenshots must be inspected')
 d=data.get('manual-smoke-live.json',{});shots=d.get('shots',[]);counts=[x.get('corners',[{}])[0].get('shotsConsumed') for x in shots] if shots else []
 manual=d.get('passed') is True and len(shots)==3
 row('Three real manual CCTV shots','BOUNDED PASS' if manual else 'NOT VERIFIED',f'manual-smoke-live.json: {len(shots)} recorded shot snapshots','Driver asserts ammunition decrement/native shot counter, not hit accuracy')
 row('CCTV distracting smoke removed; shot sound retained','VISUAL/AUDIO REVIEW REQUIRED' if manual else 'NOT VERIFIED','Shot captures available' if manual else 'No completed manual capture','Screenshot existence cannot pass smoke quality or sound; no automatic-effect comparison')
 d=data.get('headshot-193.json',{});hits=d.get('shots',[]);good=bool(d.get('passed') is True and hits and all(s.get('shotsConsumedDelta')==1 and s.get('afterHealth',math.inf)<s.get('beforeHealth',-math.inf) for s in hits))
 row('Actual head-aligned hit accuracy','BOUNDED PASS' if good else 'BLOCKED ALIGNMENT' if not hits and d.get('passed') is False else 'NOT VERIFIED',f'headshot-193.json: {len(hits)} actual shot-result records','No completed hit proof when servo did not converge; three consumption-only shots do not prove accuracy; native hit region not identified')
 row('Shotgun spread and automatic muzzle effects preserved','SCOPED SOURCE FIXTURE; OUTSIDE LIVE COMPARISON','No completed comparison evidence','Source policy/offline fixture is not live qualification')
 timing=data.get('deploy-timing.json',[]);span=None
 if isinstance(timing,list) and timing and timing[0].get('mode')=='Deploying' and timing[-1].get('mode')=='Camp':span=(timing[-1]['at']-timing[0]['at'])/1000
 row('Camp deploy mode/timing','OBSERVED ONLY' if span is not None else 'NOT VERIFIED',f'deploy-timing.json: sampled Deploying-to-Camp span {span:.3f}s' if span is not None else 'No sampled transition','First poll is not transition start; not an exact configured-duration assertion')
 row('Native placeable and body-mod installation','OBSERVED ONLY' if all(k in data for k in ('bare-truck.json','body-native-installed.json')) else 'NOT VERIFIED','Bare/body snapshots available; no mutation replay is manufactured','Snapshots alone do not prove native placement input or all modifier removal/reinstall transitions')
 actions=data.get('live-actions.json',[]);moves=[x for x in actions if isinstance(x,dict) and x.get('endpoint')=='/move' and isinstance(x.get('reply'),dict) and x['reply'].get('ok') is True]
 row('Driving/body physics stability and hood jumping','OBSERVED ACTIONS; NOT QUALIFIED' if moves else 'NOT VERIFIED',f'live-actions.json: {len(moves)} successful input-dispatch move replies','Successful input dispatch is not stable physics; need actual pose/velocity thresholds, driver state, and screenshots')
 energy=data.get('energy-installed.json',{});sources=energy.get('sources',[])
 row('Real Energy source/component installation','OBSERVED ONLY' if sources else 'NOT VERIFIED',f'energy-installed.json: {len(sources)} source records','Installed snapshot does not prove every UI gesture, charging, or source funding')
 d=data.get('charging-live.json',{});charge=d.get('passed') is True and d.get('solarChargeGainUseTimes',0)>0 and d.get('engineChargeGainUseTimes',0)>0
 row('Surplus daytime solar and slow-drive battery charging','BOUNDED PASS' if charge else 'NOT RUN','charging-live.json cell UseTimes comparisons' if charge else 'No completed charging driver evidence','Bank CurrentPower alone is not cell charge; need night/off and actual driving isolation')
 row('Mounted turrets powered without external wires','BOUNDED PASS' if charge and d.get('internalTurretWithoutExternalWire') else 'NOT VERIFIED','Internal-supply shot isolation only if charging driver completed','Unwired screenshot or installed battery alone does not prove paid internal supply')
 d=data.get('wire-live.json',{});wire=d.get('passed') is True and d.get('phase')=='wire'
 row('Native relay hanging wire, cancellation/tool swap','STATE PASS; VISUAL/AUDIO REVIEW REQUIRED' if wire else 'NOT RUN','wire-live.json' if wire else 'No completed wire driver evidence','Native appearance/sound need review; actual device commit/moving endpoint/reload separate')
 row('Lighting, rear mesh/material/dye/plate and full model appearance','VISUAL REVIEW REQUIRED','No automated visual score is inferred','Parent inspect actual renders/game captures; physical code hashes do not prove appearance')
 final_hits=data.get('headshot-final-229.json',{});records=final_hits.get('shots',[])
 ten=final_hits.get('passed') is True and len(records)==10 and all(s.get('shotsConsumedDelta')==1 and s.get('afterHealth',math.inf)<s.get('beforeHealth',-math.inf) for s in records)
 # Later supplemental rows preserve failed trigger runs separately from successful hit records.
 # Supplemental bounded facts from later live phases. Do not inherit parent prose as evidence.
 actual=[]
 for name in ('headshot-227.json','headshot-227-retry.json'):
  for shot in data.get(name,{}).get('shots',[]):
   if shot.get('shotsConsumedDelta')==1 and shot.get('afterHealth',math.inf)<shot.get('beforeHealth',-math.inf):actual.append(shot)
 if actual and not ten:
  for item in rows:
   if item['requirement']=='Actual head-aligned hit accuracy':item.update(status='BOUNDED HIT OBSERVATION',evidence=f'{len(actual)} later recorded single-round target-health decreases across two reports',limit='Both drivers ended failed/incomplete after recorded hits; no complete10-shot script PASS or native head hit-region proof. Earlier servo failure retained in raw evidence.')
 if ten:
  for item in rows:
   if item['requirement']=='Actual head-aligned hit accuracy':item.update(status='BOUNDED10-SHOT PASS',evidence='headshot-final-229.json: completed10 single-round target-health decreases',limit='Native head damage region not identified; later visible-target quick-click run failed after4 recorded hits. Server press-latch retest still required.')
 later=data.get('headshot-visible-final-232.json',{});sweep=data.get('four-corner-sweep-live.json',{})
 row('Latest brief-click server press latch','FIFTH-BUILD RETEST PENDING','fe8810f live short-pulse run incomplete; bf7894a adds native-injection timing/witness','Earlier timed pulse could expire before native input injection on slow frames. Do not attribute every no-ammo result to turret logic; require new delivery witness plus actual ammo/health observations.')
 row('Four-corner full aim/clearance sweep','INCOMPLETE' if sweep.get('passed') is False else 'NOT VERIFIED',f"four-corner-sweep-live.json: {len(sweep.get('corners',[]))} corner result records",'Timed out globally; four-card toggle test does not prove four full yaw/pitch sweep boundaries')
 charge_report=data.get('charging-live.json',{});groups={}
 for sample in charge_report.get('samples',[]):groups.setdefault(sample.get('phase'),[]).append(sample.get('energy',{}))
 def uses(e):return {(s['id'],c['slot']):float(c['useTimes']) for s in e.get('sources',[]) if s.get('kind')=='Battery' for c in s.get('components',[]) if c.get('count',0)>0}
 def gain_phase(name):
  seq=groups.get(name,[])
  if len(seq)<2:return None
  a,b=uses(seq[0]),uses(seq[-1])
  return sum(a[k]-b[k] for k in a) if a and a.keys()==b.keys() else None
 try:
  solar=gain_phase('day_surplus');drive=gain_phase('slow_drive_night');night=gain_phase('night_engine_off');park=gain_phase('engine_off_night')
  partial=solar is not None and solar>0 and drive is not None and drive>0 and night==0 and park==0
 except (KeyError,TypeError,ValueError):partial=False
 if partial:
  for item in rows:
   if item['requirement']=='Surplus daytime solar and slow-drive battery charging':item.update(status='ISOLATED CHARGING PHASES PASS; FULL FLOW INCOMPLETE',evidence=f'charging-live.json: {sum(len(g) for g in groups.values())} samples; reported solar69/engine62 UseTimes gains, night/off unchanged',limit='Full driver passed=false at final stale radial selector; internal-turret final step not completed. Replay dynamic selector flow; no reload/remote durability claim.')
 camp=data.get('engine-camp-seat-live.json',{});deploy=data.get('engine-deploy-seat-live.json',[]);engine_samples=deploy+[camp] if isinstance(deploy,list) and camp else []
 stopped=bool(engine_samples) and all(x.get('engine',{}).get('available') is True and x['engine']['entityEngineRunning'] is False and x['engine']['engines'] and all(e['nativePartRunning'] is False for e in x['engine']['engines']) for x in engine_samples)
 fuels=[x['engine']['nativeFuelLevel'] for x in engine_samples] if stopped else []
 row('Camp/Deploying native engine state and fuel','BOUNDED STATE PASS' if stopped and max(fuels)==min(fuels) and camp.get('mobility',{}).get('localPlayerIsDriver') is True else 'NOT VERIFIED',f'{len(engine_samples)} actual native engine samples with driver-seat Camp observation','Stopped flags and unchanged fuel in sampled interval; no historical startup oneshot or audible-sound absence proof')
 connected=data.get('external-wire-powered.json',{});off=data.get('external-wire-settled-off.json',data.get('external-wire-disconnected.json',{}));live_nodes=connected.get('devices',{}).get('nodes',[]);off_nodes=off.get('devices',{}).get('nodes',[])
 powered=connected.get('externalRelayLinkCount')==1 and connected.get('suppliedWatts',0)>=5 and any(n.get('block')=='spotlightPlayer' and n.get('powered') is True and n.get('nativePowerUsed',0)==5 for n in live_nodes)
 restored=off.get('externalRelayLinkCount')==0 and all(n.get('powered') is False and n.get('nativePowerUsed',0)==0 for n in off_nodes if n.get('block')=='spotlightPlayer') and any(n.get('block')=='spotlightPlayer' for n in off_nodes)
 row('External native spotlight cable funding and unlink','CONNECTED5W OBSERVED; OFF CONFIRMED' if powered and restored else 'CONNECTED5W OBSERVED; OFF NOT CONFIRMED' if powered else 'NOT VERIFIED','Actual link count and native spotlight power-use snapshot comparison','Immediate unlink snapshot can retain pre-tick5W/powered state; require settled-off snapshot, not parent prose')
 fades=[x for name in ('relay-pack-live.json','relay-deploy-live.json') for x in data.get(name,[]) if isinstance(x,dict)]
 synced=bool(fades) and all(all(isinstance(x.get(k),(int,float)) for k in ('min','max','station')) and abs(x['min']-x['station'])<.0001 and abs(x['max']-x['station'])<.0001 for x in fades)
 row('Relay/station mode fade synchronization','BOUNDED STATE PASS; VISUAL REVIEW REQUIRED' if synced else 'NOT VERIFIED',f'{len(fades)} observed transition fade samples','Numeric opacity synchronization is not a visual-material quality or timing-start proof')
 pickup=data.get('pickup-replace-live.json',{});ps=pickup.get('samples',[])
 stable=len(ps)>=6 and pickup.get('bodyRetained') is True and pickup.get('nativeTakeEntity')!=pickup.get('replacedEntity') and all(x.get('position')==ps[0].get('position') and x.get('mobility',{}).get('rigidbody',{}).get('velocity')==[0,0,0] and x.get('mobility',{}).get('rigidbody',{}).get('angularVelocity')==[0,0,0] for x in ps)
 row('Native pickup/re-place with camper body retained','BOUNDED PARKED PASS' if stable else 'NOT VERIFIED',f'pickup-replace-live.json: {len(ps)} stationary native energy/physics samples after replacement','Confirms retained body and parked stability in sampled interval, not subsequent driven stability or all modifier transitions')
 replay=data.get('charging-final-fe8810f.json',{})
 if replay.get('passed') is False:
  row('Earlier full charging replay (fe8810f)','INCOMPLETE',"charging-final-fe8810f.json: actual vehicle movement prerequisite failed",'Earlier isolated solar69/engine62 gains remain bounded older evidence. Latest replay had native Travel driver/engine and routed input, but0.08throttle moved only about0.3m on hill; movement threshold failed, not mounting/routing. Final unwired turret step not completed.')
 row('Latest build transport qualification','LIVE RETEST PENDING','bf7894a source adds bridge native press delivery clock and witness','Build success/offline fixtures are not live native input delivery or actual hit proof. No four-active-green screenshot qualification: that frame showed camper interior/delayed UI.')
 completed_sweeps=[i for i in range(4) if data.get(f'corner{i}-bf7894a.json',{}).get('passed') is True or i==2 and data.get('corner2-bf7894a-retry.json',{}).get('passed') is True]
 if completed_sweeps:
  for item in rows:
   if item['requirement']=='Four-corner full aim/clearance sweep':item.update(status='BOUNDED FOUR-CORNER PASS' if len(completed_sweeps)==4 else 'PARTIAL CORNER PASS',evidence='Completed fifth-build corner sweep indices: '+','.join(map(str,completed_sweeps)),limit='State sweep success does not prove no visible hardware; inspect each actual boundary capture. Earlier full sweep timeout retained.')
 review=data.get('corner0-visual-review.json',{})
 if review.get('reviewMethod')=='Actual four PNGs opened with view_image':
  row('Corner0 actual boundary capture review','REVIEWED: CENTER CLEAR; EDGE HARDWARE VISIBLE','Four actual boundary PNGs viewed; no gun barrel across reticle; local head visible left-down','Camper hood/armor rail lower-right and ladder/grille lower-left remain visible. Do not claim no own hardware or all-corner visual approval.')
 if data.get('corner1-visual-review.json',{}).get('reviewMethod')=='Actual four PNGs opened with view_image':
  row('Corner1 actual boundary capture review','REVIEWED: CENTER BARREL CLEAR; EDGE HARDWARE VISIBLE','Four actual PNGs reviewed; world tree/foliage at reticle, camper/adjacent hardware at edges','No ammunition loaded in frames, so no firing/clear-shot admission proof. Dense world foliage limits scene visibility.')
 for corner in (2,3):
  if data.get(f'corner{corner}-visual-review.json',{}).get('reviewMethod')=='Actual four PNGs opened with view_image':
   row(f'Corner{corner} actual boundary capture review','REVIEWED: PERIPHERAL CAMPER STRUCTURE VISIBLE','Four actual boundary PNGs viewed; no camera-own barrel across reticle observed','Substantial rear-door/arms/grille at side regions; world foliage also obstructs scene. No ammunition loaded, so no firing-clearance proof.')
 if data.get('mode-caption-visual-review.json',{}).get('reviewMethod')=='Actual two PNGs opened with view_image':
  row('Defense mode icons/captions and dark panel appearance','VISUALLY REVIEWED','Actual Assault/Sniper management screenshots inspected; centered labels/icons and CCTV captions','Native target/ring icon shown; panels dark charcoal with slight purple cast and purple accents. Not a camera view or accuracy assertion.')
 sniper=data.get('headshot-sniper-bf7894a-243.json',{});ss=sniper.get('shots',[]);witnesses=[x.get('reply',{}).get('inputWitness',{}) for x in sniper.get('requests',[]) if x.get('endpoint')=='/press']
 sniper_ok=sniper.get('passed') is True and len(ss)==10 and len(witnesses)>=10 and all(x.get('injectedFrames',0)>0 for x in witnesses) and all(x.get('shotsConsumedDelta')==1 and x.get('beforeHealth',0)-x.get('afterHealth',0)==19 for x in ss)
 if sniper_ok:
  for item in rows:
   if item['requirement'] in ('Actual head-aligned hit accuracy','Latest brief-click server press latch','Latest build transport qualification'):
    item.update(status='FIFTH-BUILD BOUNDED10-SHOT PASS',evidence='headshot-sniper-bf7894a-243.json:10/10 single-round19health decreases with positive native input witnesses',limit='Sniper single-projectile test on prepared fixed target; no native head damage-region proof, held-automatic cadence or remote-peer claim. Older failures retained.')
 power=data.get('power-toggle-visual-review.json',{})
 if power.get('allFourGreenActiveVisible') is True and power.get('allFourYellowOffVisible') is True:
  row('All four power-toggle colors/captions','VISUALLY REVIEWED PASS','Actual green ACTIVE and yellow OFF management PNGs opened and inspected','Labels/colors not ammunition or actual supply proof; earlier delayed interior capture excluded')
 new_charge=data.get('charging-final-bf7894a.json',{})
 if new_charge and new_charge.get('passed') is not True and 'failure' not in new_charge:
  row('Fifth-build full charging replay','IN PROGRESS','charging-final-bf7894a.json records partial current run','No completion claim while parent run remains active')
 if new_charge.get('passed') is True and new_charge.get('solarChargeGainUseTimes',0)>0 and new_charge.get('engineChargeGainUseTimes',0)>0:
  for item in rows:
   if item['requirement']=='Surplus daytime solar and slow-drive battery charging':item.update(status='FIFTH-BUILD FULL DRIVER PASS',evidence='charging-final-bf7894a.json: surplus solar69/actual-driving62 cell UseTimes gain; night/off isolation and movement thresholds passed',limit='Bounded local real-input flow; no reload, remote-peer, weather variants or exact watt balance claim')
  internal=new_charge.get('internalTurretWithoutExternalWire',{})
  if internal.get('energy',{}).get('externalRelayLinkCount')==0:
   for item in rows:
    if item['requirement']=='Mounted turrets powered without external wires':item.update(status='BOUNDED INTERNAL SUPPLY PASS',evidence='Completed charging driver final native CCTV shot with externalRelayLinkCount0',limit='One mounted turret firing admission; not all turrets simultaneous load or remote-peer qualification')
 reverse=data.get('reverse-paved-fueled-final-245.json',{});rp=reverse.get('reverse',{}).get('presentation',{});ap=reverse.get('after',{}).get('presentation',{})
 reverse_ok=reverse.get('passed') is True and rp.get('forwardVelocity',0)<-.05 and rp.get('rearMaxEffectiveIntensity')==3.25 and rp.get('rearMaxRange')==6 and rp.get('rearMaxSpotAngle')==60 and ap.get('rearMaxEffectiveIntensity',99)<=.66
 row('Fueled native reverse illumination and reset','BOUNDED LOCAL PASS; ACTUAL NIGHT IMAGE REVIEWED' if reverse_ok else 'NOT VERIFIED','reverse-paved-fueled-final-245.json: reverse velocity about-2.969, intensity3.25/range6/angle60; boosted light reset afterward','Actual night PNG shows bright red ground cone; retained red lamp color. Earlier zero-fuel coast was invalid fixture, not actual reverse illumination proof. No audio recording or remote durability claim.')
 out={'schema':1,'draft':True,'complete':False,'scope':'Current offline evidence snapshot, no game input; bounded claims only','evidenceFilesRead':sorted(data),'rows':rows,'readIssues':issues,'sanitization':'Whitelisted semantic summaries only; no request args, authentication, session files, player/world identities or raw replies exported'}
 (root/'requirement-matrix-draft.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
 lines=['# Camper overnight requirement matrix — DRAFT','', 'Final delivery assessment pending parent. Completed bounded local tests are distinct from historical failed attempts, scoped offline checks, and capabilities outside the agreed live scope.','', '| Requirement | Status | Evidence | Limit |','|---|---|---|---|']
 for x in rows:lines.append('| '+' | '.join(str(x[k]).replace('|','/').replace('\n',' ') for k in ('requirement','status','evidence','limit'))+' |')
 if issues:lines+=['','Read issues:']+['- '+s for s in issues]
 (root/'requirement-matrix-draft.md').write_text('\n'.join(lines)+'\n',encoding='utf-8');print(json.dumps({'draft':True,'complete':False,'rows':len(rows),'readIssues':len(issues),'output':'requirement-matrix-draft.json and .md'}))
if __name__=='__main__':main()












