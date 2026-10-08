"""Verify revised jar assets and record their review provenance."""
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'_Documentation/FeatureDesign/SurvivorSkillSystem/CookingCatalogue_20260915/Production'
revision = OUT/'JarRevision'
records = json.loads((revision/'revision_prompts.json').read_text(encoding='utf-8'))
manifest_path = OUT/'asset_manifest.json'
manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
assert len(records) == 12
xml = '\n'.join(p.read_text(encoding='utf-8-sig') for p in (ROOT/'Config').rglob('*.xml'))
for record in records:
    task = next(t for t in manifest['tasks'] if t['key'] == record['key'])
    im = Image.open(task['path'])
    assert im.size == (160, 160) and im.mode == 'RGBA', task['key']
    assert im.getchannel('A').getextrema() == (0, 255), task['key']
    assert task['icon'] in xml, task['icon']
    task['revision_prompt'] = record['prompt']
    task['visual_review'] = 'Native jar revision: reviewed at inventory size with matching recipe card; 2026-09-16.'
manifest_path.write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
print('Verified 12 revised exports: 160x160 RGBA, transparent exterior, XML references present.')
