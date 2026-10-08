"""Verify supplied masters and stage a reproducible single-bundle Unity project."""
from pathlib import Path
import hashlib, json, shutil, collections
root=Path(__file__).resolve().parents[2]
source=Path(__file__).parent/'Source/Trader_Voiceset_Implementation'
project=Path(__file__).parent/'UnityProject'
manifest=json.loads((source/'Production_Manifest.json').read_text())
for line in (source/'SHA256SUMS.txt').read_text().splitlines():
    digest,name=line.split(None,1)
    assert hashlib.sha256((source/name.strip().lstrip('*')).read_bytes()).hexdigest()==digest,name
assert len(manifest)==3600
assert len({r['id'] for r in manifest})==3600
bank={r['id']:r for r in json.loads((source/'Dialogue/Dialogue_Bank_3600.json').read_text())}
assets=[]
for row in manifest:
    assert row['spoken_text']==bank[row['id']]['text'],row['id']
    rel='Assets/TraderVoices/'+row['requested_game_export_path']
    dest=project/rel;dest.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(source/row['actual_audio_path'],dest)
    assets.append(rel)
(project/'Assets/Editor').mkdir(parents=True,exist_ok=True)
shutil.copyfile(Path(__file__).parent/'BuildTraderVoices.cs',project/'Assets/Editor/BuildTraderVoices.cs')
(project/'ProjectSettings').mkdir(exist_ok=True)
(project/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.62f2\n')
(project/'Assets/voice_assets.json').write_text(json.dumps({'paths':assets}))
print('Verified and staged',len(assets),'clips:',dict(collections.Counter(r['target_trader'] for r in manifest)))
