from pathlib import Path
import hashlib,json,difflib,zipfile
r=Path.cwd();t=r/'Tools/PlayerReportsLbdEnergy20261029';prod=Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0')
paths=['Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs','Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs','Scripts/Metabolism/RebirthMetabolismService.cs','Scripts/Metabolism/RebirthMetabolismModels.cs']
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
rows=[];patch=[]
for p in paths:
 b=t/'baseline'/p;s=r/p
 assert sha(prod/p)==sha(b),f'production diverged before packaging {p}'
 rows.append(dict(path=p,baseline=sha(b),proposed=sha(s)))
 patch.extend(difflib.unified_diff(b.read_text(encoding='utf-8-sig').splitlines(True),s.read_text(encoding='utf-8-sig').splitlines(True),fromfile='a/'+p,tofile='b/'+p))
(t/'source-manifest.json').write_text(json.dumps(dict(source=str(prod),files=rows),indent=2))
(t/'patch.diff').write_text(''.join(patch),encoding='utf-8')
files=[r/p for p in paths]
for p in t.rglob('*'):
 if not p.is_file():continue
 rel=p.relative_to(t)
 if any(x in ['bin','obj','Fixture','negative','native'] for x in rel.parts):continue
 if p.name=='delivery-manifest.json':continue
 files.append(p)
files+=list((r/'_Documentation/PlayerReports_20261029/SUB1').glob('*'))
files+=[r/'Tools/GameBridge/tests'/p for p in ['player_reports_received_weapon_lbd_20261029.json','player_reports_energy_observation_20261029.json']]
for p in files:assert p.is_file(),str(p)
manifest=[dict(path=p.relative_to(r).as_posix(),sha256=sha(p)) for p in files]
(t/'delivery-manifest.json').write_text(json.dumps(manifest,indent=2))
files.append(t/'delivery-manifest.json')
archive=r/'player-reports-sub1-lbd-energy-source-20261029.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for p in files:z.write(p,p.relative_to(r).as_posix())
with zipfile.ZipFile(archive) as z:
 assert len(z.namelist())==len(files)
 for p in files:assert z.read(p.relative_to(r).as_posix())==p.read_bytes()
print(json.dumps(dict(archive=str(archive),sha256=sha(archive),files=len(files),runtime=rows),indent=2))
