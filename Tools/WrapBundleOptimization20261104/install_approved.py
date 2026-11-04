"""Install reviewed compression candidates only; restore originals on failure."""
import argparse, hashlib, json, shutil, subprocess, time
from pathlib import Path
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[1]
BASE=Path('F:/REBIRTH_Wrap_Optimization/2026-11-04')
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for b in iter(lambda:f.read(4*1024*1024),b''):h.update(b)
    return h.hexdigest()
def copy_with_retry(source,destination):
    for attempt in range(40):
        try:
            shutil.copyfile(source,destination)
            return
        except OSError as error:
            if (getattr(error,'winerror',None) not in (5,32,1224) and error.errno!=22) or attempt==39:raise
            if attempt%8==0:print('Waiting for transient Windows file mapping:',destination.name,flush=True)
            time.sleep(1)
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--install',action='store_true');ap.add_argument('--resume',action='store_true');args=ap.parse_args()
    plan=json.loads((HERE/'backup-result.json').read_text());assert plan['status']=='PASS'
    backup=Path(plan['destination']);manifest=json.loads((backup/'VERIFIED_MANIFEST.json').read_text())
    original={row['relative'].split('/')[-1]:row for row in manifest['files'] if row['relative'].startswith('CurrentBundles/')}
    names=set(plan['approved_bundles'].values());assert len(names)==64
    batch=json.loads((BASE/'Candidates/batch.json').read_text());assert batch['status']=='PASS' and batch['completed']==64
    quality=json.loads((BASE/'Candidates/quality.json').read_text());assert quality['status']=='PASS' and quality['textures']==832
    gpu=json.loads((BASE/'gpu-plan.json.receipt.json').read_text());assert gpu['status']=='PASS' and len(gpu['textures'])==832
    gpu_hashes={row['name']:row['sha256'] for row in gpu['bundles']};assert set(gpu_hashes)==names
    assert all(row['format']=='BC7' for row in gpu['textures'])
    gpu_textures={(row['bundle'],row['name']):row for row in gpu['textures']};assert len(gpu_textures)==832
    native_ids=[]
    for group,folder in [('first','CamperFirst'),('second','CamperSecond'),('third','CamperThird')]:
        ids=json.loads((BASE/('native-'+group+'-ids.json')).read_text(encoding='utf-8-sig'))
        if isinstance(ids,str):ids=[ids]
        native_ids.extend(ids)
        for id in ids:
            for view in ['quarter','side','right','rear','hood','rear-door-open']:
                assert (BASE/'NativeReview'/folder/(id+'-'+view+'.png')).is_file(),('Missing native view',id,view)
    assert len(native_ids)==64 and set(native_ids)==set(plan['approved_native_ids'])
    for id in native_ids:
        for view in ['quarter','side','right','rear','hood']:
            assert (BASE/'NativeReview/Pickup'/(id+'-'+view+'.png')).is_file(),('Missing pickup view',id,view)
    receipts={};already_installed=set()
    for name in sorted(names):
        candidate=BASE/'Candidates/BC7HQ2'/name
        receipt=json.loads(candidate.with_suffix('.report.json').read_text())
        assert receipt['offlineValidation']=='PASS'
        assert sha(candidate)==receipt['candidateSha256']==gpu_hashes[name]
        assert receipt['sourceSha256']==original[name]['sha256']
        current=sha(ROOT/'Resources'/name)
        if args.resume and current==receipt['candidateSha256']:already_installed.add(name)
        else:assert current==receipt['sourceSha256'],('Unknown installed bytes',name)
        assert sha(backup/'CurrentBundles'/name)==original[name]['sha256']
        for texture in receipt['textures']:
            actual=gpu_textures[(name,texture['name'])]
            assert [actual['width'],actual['height']]==texture['after'][:2] and actual['mips']==texture['mips']
        receipts[name]=receipt
    for name,row in original.items():
        if name not in names:assert sha(ROOT/'Resources'/name)==row['sha256'],('Legacy bundle changed',name)
    # User owns the running game: do not install against it or close/control it.
    game=subprocess.run(['powershell','-NoProfile','-Command',
        "@(Get-Process -ErrorAction SilentlyContinue | Where-Object {$_.ProcessName -like '7DaysToDie*'}).Count"],capture_output=True,text=True,check=True)
    assert game.stdout.strip()=='0','Game is running; candidates remain staged. Do not close it automatically.'
    print('Preinstall checks PASS; 64 approved candidates, 832 GPU/quality checks; legacy unchanged',flush=True)
    if not args.install:return
    prepared={};installed=[]
    try:
        for name in sorted(names):
            if name in already_installed:continue
            temp=ROOT/'Resources'/(name+'.compression-install.tmp')
            assert not temp.exists(),('Unexpected installation temporary file',temp)
            shutil.copyfile(BASE/'Candidates/BC7HQ2'/name,temp)
            assert sha(temp)==receipts[name]['candidateSha256'];prepared[name]=temp
        for name,temp in prepared.items():
            # Retain the existing Windows file/ACL: rename-overwrite was denied
            # here while copy-overwrite succeeded. The game is absent and every
            # original has a verified backup for rollback.
            installed.append(name)
            copy_with_retry(temp,ROOT/'Resources'/name)
            assert sha(ROOT/'Resources'/name)==receipts[name]['candidateSha256']
        for name in names:assert sha(ROOT/'Resources'/name)==receipts[name]['candidateSha256']
        for name,row in original.items():
            if name not in names:assert sha(ROOT/'Resources'/name)==row['sha256']
    except BaseException:
        for name in reversed(installed):
            copy_with_retry(backup/'CurrentBundles'/name,ROOT/'Resources'/name)
            assert sha(ROOT/'Resources'/name)==original[name]['sha256']
        raise
    finally:
        for temp in prepared.values():
            if temp.exists():temp.unlink()
    result=dict(status='PASS',installed=len(names),resumed=len(already_installed),beforeBytes=sum(r['sourceBytes'] for r in receipts.values()),
        afterBytes=sum(r['candidateBytes'] for r in receipts.values()),legacyUnchanged=True,gameLaunched=False,
        backup=str(backup),gpuReceipt=str(BASE/'gpu-plan.json.receipt.json'),qualityReceipt=str(BASE/'Candidates/quality.json'),bundles=receipts)
    (BASE/'installation.json').write_text(json.dumps(result,indent=2));(HERE/'installation-result.json').write_text(json.dumps({k:v for k,v in result.items() if k!='bundles'},indent=2))
    print(json.dumps({k:v for k,v in result.items() if k!='bundles'}),flush=True)
if __name__=='__main__':main()
