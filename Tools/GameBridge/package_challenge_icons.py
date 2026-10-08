"""Package only the challenge icon changes, preserving mod-relative paths."""
from pathlib import Path
import hashlib,json,zipfile
ROOT=Path(__file__).resolve().parents[2]
out=ROOT/'Tools/GameBridge/out/delivery'
out.mkdir(parents=True,exist_ok=True)
files=[ROOT/'Config/_Rebirth/challenges.xml',ROOT/'_Documentation/Codex_Changes/CUMULATIVE_LOG.txt']
files.append(ROOT/'Tools/GameBridge/tests/challenge_icons.json')
files+=list((ROOT/'UIAtlases/UIAtlas').glob('rb_challenge_*.png'))
files+=list((ROOT/'_Documentation/ChallengeIcons').glob('*'))
files+=[ROOT/'Tools/GameBridge'/n for n in ['build_learning_challenges.py','challenge_icon_art.json','audit_challenge_icons.py','prepare_challenge_icons.py','package_challenge_icons.py']]
files=[p for p in files if p.is_file() and p.name!='package_manifest.json']
manifest={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(files))}
mp=ROOT/'_Documentation/ChallengeIcons/package_manifest.json'
mp.write_text(json.dumps(manifest,indent=2)+'\n');files.append(mp)
dest=out/'REBIRTH_20261002_unique_challenge_icons.zip'
with zipfile.ZipFile(dest,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(set(files)):z.write(p,p.relative_to(ROOT).as_posix())
with zipfile.ZipFile(dest) as z:
    assert z.testzip() is None
    for name,h in manifest.items():assert hashlib.sha256(z.read(name)).hexdigest()==h
digest=hashlib.sha256(dest.read_bytes()).hexdigest()
dest.with_suffix('.zip.sha256').write_text(digest+'  '+dest.name+'\n')
print(json.dumps({'archive':str(dest),'files':len(set(files)),'sha256':digest}))
