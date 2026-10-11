"""Measure an already verified disposable farm; no gameplay/policy overrides."""
import sys,json,time
from pathlib import Path
sys.path.insert(0,'Tools/StorageGear')
from test_capacity_live import call,root
out=root/'_Documentation/PerformanceAudit_20261010'
assert call('/state',sections='world')['world']['gameName']=='CodexTest'
assert call('/guard')['enabled']
def console(cmd):
 r=call('/console','POST',cmd=cmd)
 assert not r.get('hadError'),r
 return r
report=console('rbfarming perfreport 24')
assert 'plants=64 growing=64' in json.dumps(report),'Not the required workload'
results={'workload':report,'start':console('rbfarming perfsample start')}
try:
 print('Measuring 30 seconds of normal farm updates',flush=True)
 time.sleep(30)
 results['idle']=console('rbfarming perfsample report')
 results['stop']=console('rbfarming perfsample stop')
 results['endWorkload']=console('rbfarming perfreport 24')
 assert 'plants=64 growing=64' in json.dumps(results['endWorkload'])
finally:
 console('rbfarming perfsample stop')
 (out/'LIVE_FARM_IDLE_64.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results['idle']),flush=True)
