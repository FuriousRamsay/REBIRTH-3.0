"""Parent-reviewed runtime test overlay; does not install or operate the game."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
BUILD=Path('E:/_Haven/shared/REBIRTHTemp/CamperTraceBuild')
snapshot=BUILD/'hud-pointer-locks-fixed-fence-20261014.json'
verification=BUILD/'hud-pointer-locks-fixed-verification-20261014.json'
fence=json.loads(snapshot.read_text(encoding='utf-8-sig'))
proof=json.loads(verification.read_text(encoding='utf-8-sig'))
assert proof['allowInstall'] and proof['buildExitCode']==0 and proof['fenceId']==fence['fenceId']
def sha(data):return hashlib.sha256(data).hexdigest()
rows=[]
for item in fence['files']:
    path=Path(item['path']);data=path.read_bytes()
    assert sha(data)==item['sha256'].lower(),str(path)
    relative=path.relative_to(ROOT).as_posix()
    if relative.startswith(('Scripts/','Config/')) or relative=='RebirthUtils.csproj' or (len(Path(relative).parts)==1 and path.suffix=='.cs'):
        rows.append({'path':relative,'bytes':len(data),'sha256':sha(data)})
rows.sort(key=lambda r:r['path'])
source_hash=sha(json.dumps(rows,sort_keys=True,separators=(',',':')).encode())
candidate=Path(proof['artifact']['path']);dll_hash=sha(candidate.read_bytes())
assert dll_hash==proof['artifact']['sha256'].lower()==sha((ROOT/'RebirthUtils.dll').read_bytes())
# Reviewed runtime files changed in the recent UI/cooking/HUD batches. This
# overlay requires the same existing cumulative REBIRTH baseline on both peers.
paths=['RebirthUtils.dll','Config/_Vehicles/camper_station_picker_windows.xml',
       'Config/_Vehicles/vehicle_editor_windows.xml','Config/_Cooking/runtime.xml',
       'Config/loot.xml','Config/XUi_InGame/windows.xml']
files=[]
for relative in paths:
    data=(ROOT/relative).read_bytes()
    files.append({'path':relative,'bytes':len(data),'sha256':sha(data)})
plan={'schema':1,'reviewed':True,'scope':'changed-files-overlay','files':files,
      'sourceFence':rows,'sourceFenceSha256':source_hash,
      'dllCandidate':{'path':str(candidate),'sha256':dll_hash,'sourceFenceSha256':source_hash},
      'buildVerification':str(verification),'buildVerificationSha256':sha(verification.read_bytes()),
      'qualification':'Compiled test candidate; source/simulation checks passed. Full live multiplayer and driving/reload qualification outstanding.',
      'baseline':'Same current cumulative REBIRTH installation and native game version required; not a full mod distribution.'}
output=BUILD/'runtime-test-overlay-reviewed-20261014.json'
with output.open('x',encoding='utf-8') as stream:json.dump(plan,stream,indent=2)
print(output)
