"""Build only the frozen approved set into F: staging; never install implicitly."""
import concurrent.futures, hashlib, json, subprocess, sys, time
from pathlib import Path
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
OUTPUT=Path('F:/REBIRTH_Wrap_Optimization/2026-11-04/Candidates')
PLAN=json.loads((HERE/'backup-result.json').read_text())
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def build(pair):
    id,name=pair;dest=OUTPUT/'BC7HQ2'/name;report=dest.with_suffix('.report.json')
    if dest.exists():
        receipt=json.loads(report.read_text())
        assert sha(dest)==receipt['candidateSha256']
        assert sha(ROOT/'Resources'/name)==receipt['sourceSha256']
        return dict(id=id,status='PASS',receipt=str(report),reused=True)
    log=OUTPUT/'Logs'/(id+'.log');log.parent.mkdir(parents=True,exist_ok=True)
    with log.open('w') as out:
        result=subprocess.run([sys.executable,str(HERE/'compress_candidate.py'),name,'--uber','2','--output-root',str(OUTPUT)],cwd=ROOT,stdout=out,stderr=subprocess.STDOUT)
    return dict(id=id,status='PASS' if result.returncode==0 else 'FAIL',receipt=str(report),log=str(log))

def main():
    assert PLAN['status']=='PASS'
    OUTPUT.mkdir(parents=True,exist_ok=True)
    started=time.time();rows=[]
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        futures=[pool.submit(build,pair) for pair in PLAN['approved_bundles'].items()]
        for future in concurrent.futures.as_completed(futures):
            row=future.result();rows.append(row)
            result=dict(status='RUNNING',completed=len(rows),total=len(futures),seconds=round(time.time()-started,1),results=rows)
            (OUTPUT/'batch.json').write_text(json.dumps(result,indent=2))
            print(json.dumps({k:v for k,v in row.items() if k!='receipt'}), 'completed',len(rows),'/',len(futures),flush=True)
    result['status']='PASS' if all(row['status']=='PASS' for row in rows) else 'FAIL'
    (OUTPUT/'batch.json').write_text(json.dumps(result,indent=2))
    print(result['status'],round(time.time()-started,1),'seconds',flush=True)
    if result['status']!='PASS':sys.exit(1)
if __name__=='__main__':main()
