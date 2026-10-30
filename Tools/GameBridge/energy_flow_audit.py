"""Offline Energy evidence audit. Never connects, launches, edits saves or adjusts balance."""
from pathlib import Path
import argparse,json,re,hashlib,math
TRACE='[REBIRTH Metabolism Trace]'
NUM=r'(-?\d+(?:\.\d+)?)'
ROW=re.compile(r'entity=(\d+) dt='+NUM+r's move=(\S+) activity='+NUM+r' \| energy '+NUM+'->'+NUM+r' ceiling='+NUM+r' use='+NUM+r'/m recover='+NUM+r'/m beverage='+NUM+r'/m')
CLEAVER='ItemsWeaponsCleaver001_FR'
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def require(value,message):
 if not value:raise ValueError(message)
def entries(value):
 if isinstance(value,dict):
  if 'seq' in value and 'msg' in value:yield value
  for child in value.values():yield from entries(child)
 elif isinstance(value,list):
  for child in value:yield from entries(child)
def ticks(logs):
 records={}
 for e in logs:
  if TRACE not in e['msg']:continue
  seq=int(e['seq']);require(seq not in records or records[seq]['msg']==e['msg'],'Conflicting native sequence')
  records[seq]=e
 ordered=[records[k] for k in sorted(records)]
 begins=[e for e in ordered if TRACE+' BEGIN ' in e['msg']]
 ends=[e for e in ordered if TRACE+' END ' in e['msg']]
 require(len(begins)==1 and len(ends)==1,'Need one complete BEGIN/END trace, without timeout/restart')
 require('reason=manual' in ends[0]['msg'],'Trace expired before explicit completion')
 entity=int(re.search(r'entity=(\d+)',begins[0]['msg'])[1]);update=float(re.search(r'update='+NUM+'s',begins[0]['msg'])[1])
 result=[]
 for e in ordered:
  match=ROW.search(e['msg'])
  if not match:continue
  a=match.groups();actor=int(a[0]);dt=float(a[1]);before,after,ceiling,use,recover,beverage=map(float,a[4:])
  require(begins[0]['seq']<e['seq']<ends[0]['seq'] and actor==entity,'Tick outside controlled trace/actor')
  require(all(math.isfinite(v) for v in [dt,before,after,ceiling,use,recover,beverage]) and 0<dt<=max(2,update*2.5)+.011,'Invalid processed tick duration/value')
  require(min(before,after,ceiling,use,recover,beverage)>=0,'Negative Energy/rate observation')
  # Rates are per real minute, dt is the actual processed seconds (not child
  # process wall time). Nominal cost can saturate at zero; never infer a loss
  # to a changing reserve ceiling from the unexplained difference itself.
  cost=use*dt/60;gained=(recover+beverage)*dt/60
  expected=max(0,before-cost)+gained
  tolerance=.011+(use+recover+beverage)*.005/60+dt*.015/60
  residual=after-expected
  balanced=abs(residual)<=tolerance
  hyd=re.search(r'\| hydration '+NUM+'->'+NUM,e['msg']);nut=re.search(r'\| nutrition '+NUM+'->'+NUM,e['msg'])
  require(hyd and nut,'Reserve observations missing from native tick')
  result.append(dict(seq=int(e['seq']),entity=actor,dt=dt,move=a[2],before=before,after=after,ceiling=ceiling,use=use,recover=recover,beverage=beverage,hydrationBefore=float(hyd[1]),hydrationAfter=float(hyd[2]),nutritionBefore=float(nut[1]),nutritionAfter=float(nut[2]),cost=min(before,cost),nominalCost=cost,zeroFloorAdjustment=max(0,cost-before),recovered=recover*dt/60,beverageAdded=beverage*dt/60,residual=residual,tolerance=tolerance,balanced=balanced,limit='NONE' if balanced else 'UNRESOLVED_CEILING_OR_OTHER_CHANGE' if before>ceiling or expected>ceiling else 'UNEXPLAINED'))
 require(result,'No actual tick rows')
 require(abs(result[0]['before']-77)<=.02,'Controlled trace did not start at requested 77')
 for previous,current in zip(result,result[1:]):require(abs(previous['after']-current['before'])<=.011,'Energy continuity gap: missing tick or external mutation')
 return result,begins[0],ends[0]
def audit(logs,actions,exit_receipt,trace_root):
 require(exit_receipt.get('exited') is True and int(exit_receipt.get('ownedPid',0))>0,'Owned process exit not witnessed')
 rows,begin,end=ticks(logs);actor=rows[0]['entity'];witnesses=[]
 require(sorted(a['kind'] for a in actions)==['ascent','descent','fight','fight','fight','sprint','sprint','sprint'],'Missing/extra measured action phases')
 intervals=[]
 for a in actions:
  b,z=a['before'],a['after'];lower=int(b['lastLogSeq']);upper=int(z['lastLogSeq'])
  require(begin['seq']<=lower<upper<=end['seq'],'Action bounds outside complete trace')
  for s in (b,z):require(s['guard']['enabled'] is True and not s['player']['dead'] and s['player']['entityId']==actor,'Guard/dead/actor witness changed')
  require(all(upper<=x or lower>=y for x,y in intervals),'Overlapping action intervals');intervals.append((lower,upper))
  observed=[r for r in rows if lower<r['seq']<=upper];require(observed,'Action lacks native ticks')
  kind=a['kind'];w=dict(name=a['name'],kind=kind,processedSeconds=sum(r['dt'] for r in observed),before=observed[0]['before'],after=observed[-1]['after'])
  if kind in ('sprint','ascent','descent'):
   tag='running' if kind=='sprint' else 'climbing';duration=sum(r['dt'] for r in observed if r['move']==tag)
   minimum=max(1,float(a['seconds'])-1.5) if kind=='sprint' else 2 if kind=='ascent' else .5
   require(duration>=minimum,a['name']+' lacks genuine '+tag+' tick coverage')
   w.update(movement=tag,movementSeconds=duration)
  else:
   c=a['result'];spawn=a['spawned'];target=spawn['id']
   require(spawn['class']=='zombieArlene' and target>0 and c['kills']==['zombieArlene'] and not c['incidents'] and c['hits']>0 and c['swingsRegistered']>0,'No exact one-enemy hit/death result')
   require(a['actualHandBefore']==CLEAVER and b['player']['holding']['name']==CLEAVER and z['player']['holding']['name']==CLEAVER,'Cleaver hand context not maintained at boundaries')
   kb=a['killBefore'];ka=a['killAfter']
   for k in (kb,ka):require(k['authority'] is True and k['player']==actor and k['domains']['soloCombat']['actor']==actor,'Native owner-kill audit identity mismatch')
   require(ka['worldId']==kb['worldId'] and ka['domains']['soloCombat']['nativeKills']-kb['domains']['soloCombat']['nativeKills']==1,'Exactly one native player-attributed kill not established')
   require(any(len(v.split(','))>=8 and v.split(',')[1]=='1' and v.split(',')[7]=='target' for v in c['rayProbe']),'No native melee ray target hit')
   p=Path(c['trace']).resolve();require(p.is_file() and Path(trace_root).resolve() in p.parents,'Current fight trace unavailable/outside bridge output')
   ft=read(p);require(isinstance(ft,list) and ft and all(v.get('target')=='zombieArlene#'+str(target) for v in ft),'Fight trace not pinned to exact spawned entity')
   w.update(nativeOwnerKills=1,spawnedEntity=target,nativeMeleeTargetHit=True,traceSha256=hashlib.sha256(p.read_bytes()).hexdigest().upper(),targetAndWeaponSpecificKill='UNPROVEN: completed native counter is actor-aggregated, not a per-target/per-weapon receipt')
  witnesses.append(w)
 unbalanced=[r for r in rows if not r['balanced']]
 return dict(status='INCOMPLETE',activityWitnesses='PASS',accounting='CONSISTENT_WITH_PRINTED_TICKS' if not unbalanced else 'UNRESOLVED',processedSeconds=sum(r['dt'] for r in rows),tickCount=len(rows),energyStart=rows[0]['before'],energyEnd=rows[-1]['after'],observedDelta=rows[-1]['after']-rows[0]['before'],integratedUse=sum(r['cost'] for r in rows),integratedNominalUse=sum(r['nominalCost'] for r in rows),zeroFloorAdjustment=sum(r['zeroFloorAdjustment'] for r in rows),integratedRecovery=sum(r['recovered'] for r in rows),integratedBeverage=sum(r['beverageAdded'] for r in rows),integratedResidual=sum(r['residual'] for r in rows),summedPrintedTickTolerance=sum(r['tolerance'] for r in rows),unbalancedTicks=unbalanced,witnesses=witnesses,reported77to8MagnitudeObserved=abs(rows[-1]['after']-8)<=.02,ownerReportReproduced='NOT ESTABLISHED: controlled reserves/traits/gear/course differ from unspecified owner session; weapon-specific killing item unavailable',groundEvidenceLimit='Four exact source-qualified native Terrain supports, startup source-hash checks and per-cell observations; physical native acceptance NOT RUN',ticks=rows)
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('evidence');a=p.parse_args();folder=Path(a.evidence).resolve();output=folder/'energy-audit.json'
 require(not any(v.lower() in ('saves','saveslocal') for v in folder.parts),'Never write audit evidence into game saves')
 require(not output.exists(),'Refuse audit overwrite')
 try:
  logs=[]
  for f in sorted(folder.glob('[0-9][0-9][0-9]-*.json')):logs.extend(entries(read(f)))
  actions=[read(f) for f in sorted(folder.glob('energy-action-*.json'))]
  report=audit(logs,actions,read(folder/'owned-process-exited.json'),Path(__file__).resolve().parent/'out')
 except (ValueError,KeyError,TypeError,OSError,IndexError) as e:report=dict(status='FAILED_CLOSED',reason=str(e),ownerReportReproduced='NOT ESTABLISHED')
 output.write_text(json.dumps(report,indent=2)+'\n');print(report['status']+'; see '+str(output))
 # Do not turn a consistent accounting ledger into full reproduction/exoneration.
 return 2
if __name__=='__main__':raise SystemExit(main())
