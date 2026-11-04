"""Apply owner-selected B/C profiles to approved wraps using retained RGBA masters."""
import concurrent.futures, json, re, subprocess, sys, shutil
from pathlib import Path
from build_comparisons import build, ROOT, sha
from install_approved import copy_with_retry
HERE=Path(__file__).resolve().parent
BASE=Path('F:/REBIRTH_Wrap_Optimization/2026-11-04/SelectedBC')
plan=json.loads((HERE/'backup-result.json').read_text())
assigned=set(re.findall(r'^\s*new Assignment\("EOS_[^"]+", "modVehicleWrap([^"]+)"\)',(ROOT/'Scripts/Purge/Runtime/RebirthPurgePlayerWrapAssignments.cs').read_text(),re.M))
ids=plan['approved_bundles']
profiles={id:75 if id in assigned else 50 for id in ids}
def digest(p):return sha(p.read_bytes())
def job(id):
    log=BASE/(id+'.log')
    with log.open('w') as f:
        p=subprocess.run([sys.executable,__file__,'--one',id],stdout=f,stderr=subprocess.STDOUT)
    if p.returncode:raise RuntimeError(str(log))
    return json.loads((BASE/'Runtime/Resources'/ids[id]).with_suffix('.report.json').read_text())
if __name__!='__main__':pass
elif '--one' in sys.argv:
    id=sys.argv[-1];build(profiles[id],id,BASE,ids[id])
elif '--install' in sys.argv:
    rows=json.loads((BASE/'built.json').read_text())
    receipt=json.loads((BASE/'gpu-plan.json.receipt.json').read_text());assert receipt['status']=='PASS'
    checked={r['name']:r['sha256'] for r in receipt['bundles']}
    before=json.loads((BASE/'before.json').read_text());assert set(checked)==set(ids.values())
    active=subprocess.check_output(['powershell','-NoProfile','-Command',"@(Get-Process -Name 7DaysToDie -ErrorAction SilentlyContinue).Count"],text=True).strip()
    assert active=='0','Game running: leave candidates staged'
    for row in rows:
        name=row['bundle'];assert digest(BASE/'Runtime/Resources'/name)==checked[name]==row['sha256']
        assert digest(ROOT/'Resources'/name)==before[name]['sha256']
    done=[]
    try:
        for row in rows:
            name=row['bundle'];done.append(name)
            copy_with_retry(BASE/'Runtime/Resources'/name,ROOT/'Resources'/name)
            assert digest(ROOT/'Resources'/name)==row['sha256']
        for name,old in before.items():
            if name not in ids.values():assert digest(ROOT/'Resources'/name)==old['sha256']
    except BaseException:
        for name in done:copy_with_retry(BASE/'BeforeInstallation'/name,ROOT/'Resources'/name)
        raise
    result=dict(status='INSTALLED',optionB=[i for i,p in profiles.items() if p==75],optionC=[i for i,p in profiles.items() if p==50],beforeBytes=sum(before[n]['bytes'] for n in ids.values()),afterBytes=sum(r['bytes'] for r in rows),legacyAndComparisonsUnchanged=True,bundles=rows)
    (BASE/'installation.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k!='bundles'}))
else:
    BASE.mkdir(parents=True,exist_ok=True)
    assert not (BASE/'before.json').exists(),'Existing run; inspect before retrying'
    manifest=json.loads((Path(plan['destination'])/'VERIFIED_MANIFEST.json').read_text())
    originals={r['relative']:r['sha256'] for r in manifest['files']}
    for name in ids.values():assert digest(Path(plan['destination'])/'CurrentBundles'/name)==originals['CurrentBundles/'+name]
    paths=set((ROOT/'Resources').glob('rebirth_wrap_*.unity3d'))|{ROOT/'Resources'/n for n in ids.values()}
    before={p.name:dict(sha256=digest(p),bytes=p.stat().st_size) for p in paths}
    (BASE/'BeforeInstallation').mkdir(exist_ok=True)
    for name in ids.values():
        dest=BASE/'BeforeInstallation'/name;shutil.copyfile(ROOT/'Resources'/name,dest);assert digest(dest)==before[name]['sha256']
    (BASE/'before.json').write_text(json.dumps(before,indent=2))
    (BASE/'profiles.json').write_text(json.dumps(dict(profiles=profiles,excludedAssigned=sorted(assigned-set(ids))),indent=2))
    print('B:',[i for i,p in profiles.items() if p==75], 'C:',sum(p==50 for p in profiles.values()),flush=True)
    rows=[]
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        for row in pool.map(job,ids):
            rows.append(row);print(len(rows),row['id'],row['bytes'],flush=True)
    (BASE/'built.json').write_text(json.dumps(rows,indent=2))
    (BASE/'gpu-plan.json').write_text(json.dumps(dict(directory=str(BASE/'Runtime/Resources'),bundles=list(ids.values()))))
    print('BUILT',sum(r['bytes'] for r in rows),flush=True)

