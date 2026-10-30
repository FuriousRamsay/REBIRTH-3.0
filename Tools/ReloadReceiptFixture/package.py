from pathlib import Path
import hashlib,json,zipfile,difflib
R=Path(__file__).resolve().parents[2];D=R/'_Documentation/ReloadReceipt_20261029';T=Path(__file__).parent
runtime=R/'Scripts/Survivor/Progression/RebirthWeaponSustainedDpsService.cs'
old=(D/'RebirthWeaponSustainedDpsService.cs.baseline').read_text(encoding='utf-8-sig');new=runtime.read_text(encoding='utf-8-sig')
(D/'runtime.diff').write_text(''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='production/ReloadService',tofile='proposal/ReloadService')),encoding='utf-8')
(D/'proposal.json').write_text(json.dumps(dict(path=str(runtime.relative_to(R)).replace('\\','/'),sha256=hashlib.sha256(runtime.read_bytes()).hexdigest().upper()),indent=2)+'\n')
files=[runtime]+sorted(p for p in T.iterdir() if p.is_file())+sorted(p for p in D.iterdir() if p.is_file() and p.name not in ('manifest.json','archive.json') and p.suffix!='.zip')
manifest={str(p.relative_to(R)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in files}
(D/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');files.append(D/'manifest.json')
archive=D/'ReloadReceiptOwnershipChanged.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for p in files:z.write(p,str(p.relative_to(R)).replace('\\','/'))
with zipfile.ZipFile(archive) as z:
 for name,sha in manifest.items():assert hashlib.sha256(z.read(name)).hexdigest().upper()==sha
receipt=dict(path=str(archive),sha256=hashlib.sha256(archive.read_bytes()).hexdigest().upper(),bytes=archive.stat().st_size,entries=len(files),verification='PASS every payload hash')
(D/'archive.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt))

