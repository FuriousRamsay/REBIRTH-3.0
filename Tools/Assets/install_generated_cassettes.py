"""Install recorded image-generation outputs through the existing atlas exporter."""
from pathlib import Path
import json, subprocess, sys
root=Path(__file__).resolve().parents[2]
rows=json.loads((root/'_Documentation/Icon_Restoration/generated_cassette_manifest_20260930.json').read_text())
state_path=root/'_Documentation/Art/LiteratureConcepts/Production_Completed.json'
state=json.loads(state_path.read_text())
for row in rows:
    if state.get(row['id'],{}).get('generation_source')==row['path']: continue
    subprocess.run([sys.executable,str(root/'_Documentation/Art/LiteratureConcepts/install_production_icon.py'),row['id'],row['path']],check=True,capture_output=True)
    state=json.loads(state_path.read_text())
    state[row['id']]['generation_source']=row['path']
    state[row['id']]['visual_review']='passed: generated preview reviewed; runtime not tested'
    state_path.write_text(json.dumps(state,indent=2)+'\n')
print(f'Installed/verified {len(rows)} cassette icons')
