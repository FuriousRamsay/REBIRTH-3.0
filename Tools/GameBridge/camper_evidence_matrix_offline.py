"""Offline sanitized camper evidence matrix. Reads only explicit evidence basenames; never session/auth/run files.
PASS means a bounded automated assertion, not overall requirement completion. No game/network calls.
"""
import argparse,json,math
from pathlib import Path
FILES=('duke-live.json','four-cards-live.json','manual-smoke-live.json','headshot-193.json','deploy-timing.json','bare-truck.json','body-native-installed.json','energy-installed.json','live-actions.json','wire-live.json','charging-live.json')
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
 row('Native progression retains Duke pin','NOT RUN','No native-mode evidence file','Cannot infer native-mode behavior from REBIRTH run')
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
 row('Shotgun spread and automatic muzzle effects preserved','NOT RUN','No completed comparison evidence','Source policy/offline fixture is not live qualification')
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
 out={'schema':1,'draft':True,'complete':False,'scope':'Current offline evidence snapshot, no game input; bounded claims only','evidenceFilesRead':sorted(data),'rows':rows,'readIssues':issues,'sanitization':'Whitelisted semantic summaries only; no request args, authentication, session files, player/world identities or raw replies exported'}
 (root/'requirement-matrix-draft.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
 lines=['# Camper overnight requirement matrix — DRAFT','', 'Incomplete qualification. Bounded automated assertions are distinct from visual/audio review and unperformed tests.','', '| Requirement | Status | Evidence | Limit |','|---|---|---|---|']
 for x in rows:lines.append('| '+' | '.join(str(x[k]).replace('|','/').replace('\n',' ') for k in ('requirement','status','evidence','limit'))+' |')
 if issues:lines+=['','Read issues:']+['- '+s for s in issues]
 (root/'requirement-matrix-draft.md').write_text('\n'.join(lines)+'\n',encoding='utf-8');print(json.dumps({'draft':True,'complete':False,'rows':len(rows),'readIssues':len(issues),'output':'requirement-matrix-draft.json and .md'}))
if __name__=='__main__':main()
