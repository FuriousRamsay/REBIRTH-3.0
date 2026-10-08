"""Verified changed-files checkpoint. Not a full mod or release qualification."""
from pathlib import Path
import hashlib,json,zipfile
root=Path(__file__).resolve().parents[2]
paths=[
 'RebirthUtils.dll','Config/_Rebirth/challenges.xml','Config/gameevents.xml',
 'Config/Localization.csv','Config/XUi_InGame/windows.xml','Config/XUi_InGame/templates.xml',
 'Config/XUi_Menu/windows.xml','Config/_TraderVoices/catalogue.xml',
 'Resources/rebirth_trader_voices.unity3d','Scripts/UI/XUiC_RebirthMapLists.cs',
 'Scripts/Survivor/Definitions/RebirthSurvivorAuthoringValidator.cs',
 'Tools/GameBridge/build_learning_challenges.py','Tools/GameBridge/organize_learning_challenges.py',
 'Tools/GameBridge/layout_learning_challenges.py','Tools/GameBridge/learning_rewards.py',
 'Tools/GameBridge/audit_learning.py','Tools/GameBridge/tests/challenge_learning.json',
 '_Documentation/Codex_Changes/CUMULATIVE_LOG.txt',
]
for folder in ('Scripts/TraderVoices','_Documentation/Trader Voicesets','_Documentation/ChallengeLearning'):
    paths.extend(str(p.relative_to(root)).replace('\\','/') for p in (root/folder).rglob('*') if p.is_file() and p.name not in ('package_manifest.json',))
paths.extend(str(p.relative_to(root)).replace('\\','/') for p in (root/'Tools/TraderVoices').glob('*') if p.is_file())
paths=sorted(set(paths));assert all((root/p).is_file() for p in paths)
manifest={p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in paths}
doc=root/'_Documentation/Trader Voicesets/package_manifest.json'
doc.write_text(json.dumps({'type':'changed-files implementation checkpoint','fullProject':False,'sourceMasters':'Original seven ZIPs in Downloads and Tools/TraderVoices/Source remain unchanged; not duplicated in this runtime overlay','files':manifest},indent=2))
paths.append(str(doc.relative_to(root)).replace('\\','/'))
out=root/'Tools/TraderVoices/out';out.mkdir(exist_ok=True)
archive=out/'REBIRTH_20261003_voices_challenges_checkpoint.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=3) as z:
    for p in paths:z.write(root/p,'zzz_REBIRTH__3_0/'+p)
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    for p,digest in manifest.items():assert hashlib.sha256(z.read('zzz_REBIRTH__3_0/'+p)).hexdigest()==digest
digest=hashlib.sha256(archive.read_bytes()).hexdigest()
archive.with_suffix('.zip.sha256').write_text(digest+'  '+archive.name+'\n')
print(json.dumps({'archive':str(archive),'files':len(paths),'bytes':archive.stat().st_size,'sha256':digest,'verified':True}))
