from pathlib import Path
import csv, io, json, hashlib, difflib, shutil, zipfile
r=Path.cwd(); d=r/'_Documentation/TunerRegression'
shared=Path('E:/_Haven/shared/REBIRTHTemp')
for name in ['TunerInputFixture','TunerModelFixture','TunerUiCallbackFixture','TunerApprovedUi/QA_CustomIcons']:
 src=shared/name; dst=d/'Reviews'/Path(name).name
 shutil.copytree(src,dst,dirs_exist_ok=True,ignore=shutil.ignore_patterns('bin','obj','__pycache__'))
rows=list(csv.reader(io.StringIO((r/'Config/Localization.csv').read_text(encoding='utf-8-sig'))))
with (d/'ACTIVE_LOCALIZATION_ADDITIONS.csv').open('w',encoding='utf-8',newline='') as f:
 w=csv.writer(f);w.writerow(rows[0]);w.writerows(x for x in rows[1:] if x and ('tuner' in x[0].lower() or 'vehicletuning' in x[0].lower()))
paths=list((r/'Scripts/Vehicles/Tuning').glob('*.cs'))+[r/'Config/_Vehicles/vehicle_tuning_windows.xml',r/'Config/_Vehicles/vehicle_tuning_xui.xml',r/'Config/Localization.csv',r/'Tools/GameBridge/tests/vehicle_tuning_modes.json']+list((r/'Resources/UI/VehicleTuningIcons').glob('*.svg'))+list((r/'UIAtlases/RebirthUiIcons').glob('rb_tuner_*.png'))
manifest=[];diff=[]
for p in paths:
 rel=p.relative_to(r).as_posix(); b=d/'baseline'/p.name
 old=b.read_bytes() if b.exists() else b'';new=p.read_bytes()
 manifest.append(dict(path=rel,baseline_sha256=hashlib.sha256(old).hexdigest() if b.exists() else None,proposed_sha256=hashlib.sha256(new).hexdigest(),bytes=len(new)))
 if p.suffix!='.png':
  diff.extend(difflib.unified_diff(old.decode('utf-8-sig').splitlines(True),new.decode('utf-8-sig').replace('\r\n','\n').splitlines(True),fromfile='a/'+rel,tofile='b/'+rel))
(d/'BASELINE_PROPOSED_MANIFEST.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(d/'approved-tuner-normalized.patch').write_text(''.join(diff).replace('\r\n','\n'),encoding='utf-8')
handoff=r/'_Documentation/AGENT_HANDOFF.md'
with handoff.open('a',encoding='utf-8') as f:f.write('\n2026-10-27 SUB2 tuner redesign delivered for lead review. Custom vehicle icons, centered button content, smaller fonts, native edit/preview modes and active CSV localization implemented. Isolated Debug 0 errors/18 warnings; Release 0 errors/52 warnings; input 24 groups/model 86 checks/callback 43 checks/static bounds PASS. No production install or game launch. Overall NOT COMPLETE: requested live storage resizing remains unmet; 1..44 storage source proposal is explicitly not an approved substitute. See TunerRegression/APPROVED_TUNER_REDESIGN_REVIEW.md and manifest. No game ownership held.\n')
excluded={'tuner-emergency-input-localization.patch','localization-active-csv-append.csv','source-hashes.json'}
docs=[p for p in d.rglob('*') if p.is_file() and 'buildobj' not in p.parts and 'bin' not in p.parts and 'obj' not in p.parts and p.name not in excluded]
out=r/'artifacts/rebirth-tuner-approved-ui-source-20261027.zip'
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
 for p in paths+docs:z.write(p,p.relative_to(r).as_posix())
with zipfile.ZipFile(out) as z:assert z.testzip() is None
print(str(out));print(hashlib.sha256(out.read_bytes()).hexdigest());print(len(paths),'owned source/assets; package verified')
