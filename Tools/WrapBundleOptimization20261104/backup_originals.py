from pathlib import Path
import os,re,json,hashlib,shutil,time,argparse
ROOT=Path(__file__).resolve().parents[2]
HERE=Path(__file__).resolve().parent
p=argparse.ArgumentParser();p.add_argument('--copy',action='store_true');a=p.parse_args()
s=(ROOT/'Scripts/Vehicles/Camper/RebirthCamperWrapAppearance.cs').read_text(encoding='utf-8-sig');start=s.index('private static readonly HashSet<string> NativeBodyIds');ids=re.findall(r'"([A-Za-z0-9]+)"',s[start:s.index('};',start)])
special={'MidnightCartographer':'rebirth_midnight_cartographer.unity3d','CaptStress':'rebirth_captstress.unity3d','GenmaSaotome':'rebirth_genma_saotome.unity3d'}
rows={}
def add(src,dest):
 src=Path(src)
 if src.is_file():rows[str(src.resolve())]={'source':str(src.resolve()),'relative':str(dest).replace('\\','/'),'bytes':src.stat().st_size}
def tree(src,dest,selector=lambda p:True):
 src=Path(src)
 if not src.exists():return
 for d,dirs,files in os.walk(src):
  dirs[:]=[x for x in dirs if x not in {'.git','Library','Temp','Logs','obj','bin','__pycache__'}]
  for f in files:
   path=Path(d)/f
   if selector(path):add(path,Path(dest)/path.relative_to(src))
# Copy current wrap bundles including legacy into backup; only approved IDs may later be rebuilt.
for file in (ROOT/'Resources').glob('*.unity3d'):
 if 'wrap' in file.name or file.name in special.values() or file.name in {'rebirth_undead_chill.unity3d','rebirth_undead_chill_after_hours.unity3d'}:add(file,Path('CurrentBundles')/file.name)
for folder in ['Resources/CamperWraps','VehicleWrapReference','Tools/VehicleStudio/WrapCollection/_UnityProject/Assets/REBIRTHCamperWrapCollection','Tools/VehicleStudio/WrapCollection/Unity','Tools/VehicleStudio/WrapCollection/Templates']:
 tree(ROOT/folder,Path('ProductionSources')/folder)
for folder in ['Scripts/Vehicles/Camper','Config/_Vehicles']:
 tree(ROOT/folder,Path('ProductionSources')/folder,lambda p:p.suffix in {'.cs','.xml'})
work=Path('F:/Codex/VehicleWrapWork')
tree(work/'GeneratedImages','OriginalGeneratedImages')
for id in ids:
 folder=work/'StandardCollection/outputs'/id
 def selected(path):
  low=str(path).lower()
  return (path.suffix.lower()=='.png' and ('fullbleed' in low or 'sealed-artwork' in low or 'coverage3-current' in low)) or (path.suffix.lower() in {'.json','.txt','.py','.shader'} and path.stat().st_size<5_000_000)
 tree(folder,Path('ApprovedAuthoringSources')/id,selected)
plan={'destination':'F:/REBIRTH_Wrap_Backups/2026-11-04_PreOptimization','approved_native_ids':ids,'approved_bundles':{id:special.get(id,'rebirth_wrap_'+id.lower()+'.unity3d') for id in ids},'files':sorted(rows.values(),key=lambda r:r['relative'])}
plan['total_bytes']=sum(r['bytes'] for r in plan['files'])
(HERE/'backup-plan.json').write_text(json.dumps(plan,indent=2))
if not a.copy:
 print(json.dumps({'files':len(rows),'bytes':plan['total_bytes'],'approved_ids':len(ids),'bundle_files':sum(r['relative'].startswith('CurrentBundles/') for r in rows.values()),'F_free':shutil.disk_usage('F:/').free}));raise SystemExit()
destination=Path(plan['destination']);destination.mkdir(parents=True,exist_ok=True)
manifest=destination/'VERIFIED_MANIFEST.json';done=[];start=time.monotonic();last=start
def sha(path):
 h=hashlib.sha256()
 with Path(path).open('rb') as f:
  for b in iter(lambda:f.read(4*1024*1024),b''):h.update(b)
 return h.hexdigest()
for i,row in enumerate(plan['files']):
 src=Path(row['source']);dst=destination/row['relative'];dst.parent.mkdir(parents=True,exist_ok=True)
 before=src.stat();expected=sha(src)
 if not dst.exists():shutil.copy2(src,dst)
 elif sha(dst)!=expected:raise RuntimeError('Existing backup differs; refusing overwrite: '+str(dst))
 actual=sha(dst);after=src.stat()
 if actual!=expected or before.st_size!=after.st_size or before.st_mtime_ns!=after.st_mtime_ns:raise RuntimeError('Backup verification/source stability failed: '+str(src))
 done.append(dict(row,sha256=actual))
 now=time.monotonic()
 if now-last>15:
  print(json.dumps({'verified':i+1,'total':len(plan['files']),'copied_GB':round(sum(x['bytes'] for x in done)/1e9,2)}),flush=True);last=now
result={k:v for k,v in plan.items() if k!='files'};result.update(status='PASS',files=done,seconds=round(time.monotonic()-start,2),source_files_modified=False)
manifest.write_text(json.dumps(result,indent=2));(HERE/'backup-result.json').write_text(json.dumps({k:v for k,v in result.items() if k!='files'},indent=2))
print(json.dumps({'status':'PASS','manifest':str(manifest),'files':len(done),'bytes':plan['total_bytes'],'seconds':result['seconds']}),flush=True)
