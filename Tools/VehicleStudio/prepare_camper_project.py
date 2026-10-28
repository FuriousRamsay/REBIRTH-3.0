"""Prepare a fresh minimal E-drive Unity project; no Unity process or source changes."""
import argparse, hashlib, json, re, shutil
from pathlib import Path
GUID = re.compile(rb'guid:\s*([0-9a-f]{32})')
def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''): h.update(b)
    return h.hexdigest()
def plan(source, first):
    assets=source/'Assets'; index={}
    for meta in assets.rglob('*.meta'):
        matches=GUID.findall(meta.read_bytes())
        if matches: index[matches[0].decode()]=meta.with_suffix('')
    seeds=[first['prefabAsset'], 'Assets/REBIRTHCamperDonorPoseGenerated/4c31a760c8fd442b9e2d882e7e16712c/UNAPPROVED_Camper_DonorPose_Profile.asset', 'Assets/Perfectvertex/80sCamperTruck/Prefabs/assembled_vehicle.prefab', 'Assets/Perfectvertex/80sCamperTruck/Art/Materials/80sCamperTruck_body_blue.mat','Assets/Perfectvertex/80sCamperTruck/Art/Materials/80sCamperTruck_body_white.mat']
    selected=set(); pending=[source/s for s in seeds]
    pending += [p for p in (assets/'REBIRTHVehicleTools').rglob('*') if p.is_file()]
    missing=set()
    while pending:
        p=pending.pop()
        if p in selected: continue
        if not p.exists(): raise ValueError('Missing seed/dependency: '+str(p))
        if p.is_dir(): continue
        selected.add(p)
        meta=Path(str(p)+'.meta')
        if meta.exists(): pending.append(meta)
        if p.suffix.lower() in ('.prefab','.mat','.asset','.meta','.controller','.anim','.asmdef'):
            for g in GUID.findall(p.read_bytes()):
                key=g.decode()
                if key in index: pending.append(index[key])
                elif not key.startswith('0000000000000000'): missing.add(key)
    if missing: raise ValueError('Unresolved asset GUID dependencies: '+str(sorted(missing)))
    for p in tuple(selected):
        for parent in p.parents:
            if parent==source: break
            meta=Path(str(parent)+'.meta')
            if meta.exists(): selected.add(meta)
    for folder in ('Packages','ProjectSettings'):
        selected.update(p for p in (source/folder).rglob('*') if p.is_file())
    return sorted(selected)
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--source',required=True);ap.add_argument('--destination',required=True);ap.add_argument('--first-result',required=True);ap.add_argument('--receipt',required=True);ap.add_argument('--copy',action='store_true');a=ap.parse_args()
    source=Path(a.source).resolve();dest=Path(a.destination).resolve()
    if dest.drive.upper()!='E:' or source==dest or dest.exists(): raise ValueError('Fresh E-drive destination required.')
    files=plan(source,json.loads(Path(a.first_result).read_text(encoding='utf-8-sig')))
    rows=[dict(path=p.relative_to(source).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in files]
    receipt=dict(kind='REBIRTH_MINIMAL_UNITY_PROJECT_COPY',source=str(source),destination=str(dest),status='PLANNED',files=rows,totalBytes=sum(r['bytes'] for r in rows),excludes=['Library','Temp','Logs','UserSettings','old generated outputs','REBIRTHQuickSmoke'])
    if a.copy:
        dest.mkdir(parents=True)
        for p,row in zip(files,rows):
            target=dest/row['path'];target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
            if sha(p)!=row['sha256'] or sha(target)!=row['sha256']: raise ValueError('Copy/source preservation failed: '+row['path'])
        (dest/'Assets/REBIRTHQuickSmoke/Editor').mkdir(parents=True)
        receipt['status']='COPIED_SOURCE_HASHES_PRESERVED'
    Path(a.receipt).write_text(json.dumps(receipt,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in receipt.items() if k!='files'}));print('selectedFiles',len(rows))
if __name__=='__main__': main()
