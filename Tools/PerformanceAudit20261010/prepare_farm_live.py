import sys,json,time
sys.path.insert(0,'Tools/StorageGear')
from test_capacity_live import call,root
out=root/'_Documentation/PerformanceAudit_20261010'
terrain=json.loads((out/'LIVE_FARM_TERRAIN.json').read_text())
assert call('/state',sections='world')['world']['gameName']=='CodexTest'
results=[]
for x in range(20,28):
 for z in range(982,990):
  y=max(b['position'][1] for b in terrain['blocks'] if b['position'][0]==x and b['position'][2]==z)
  call('/setblock','POST',x=x,y=y+1,z=z,name='air')
  call('/setblock','POST',x=x,y=y,z=z,name='air')
  call('/setblock','POST',x=x,y=y,z=z,name='farmPlotBlockPlayer',playerplaced=1)
  call('/setblock','POST',x=x,y=y+1,z=z,name='plantedCorn1',playerplaced=1)
  results.append([x,y+1,z])
 (out/'LIVE_FARM_FIXTURE.json').write_text(json.dumps(results,indent=2))
 print('placed',len(results),flush=True)
report=call('/console','POST',cmd='rbfarming perfreport 24')
(out/'LIVE_FARM_ACTIVE_REPORT.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
assert not report.get('hadError'),report
assert 'plants=64 growing=64' in json.dumps(report),'Reject workload: expected 64 active immature crop tile entities'

