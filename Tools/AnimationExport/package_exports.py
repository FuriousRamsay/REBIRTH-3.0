"""Package only the requested exports, evidence and reproducible source files."""
import hashlib, json, zipfile
from pathlib import Path

root=Path(__file__).resolve().parents[2]
out=root/'Tools/AnimationExport/output'
files=[
 'Tools/AnimationExport/BakedToFbx.cs',
 'Tools/AnimationExport/FastAsciiFbx.cs',
 'Tools/AnimationExport/ExportAnimations.cs',
 'Tools/AnimationExport/validate_fbx.py',
 'Tools/AnimationExport/package_exports.py',
 'Tools/AnimationExport/UnityBake/Assets/Editor/BakeAnimations.cs',
 'Tools/AnimationExport/UnityBake/Packages/manifest.json',
 'Tools/AnimationExport/UnityBake/ProjectSettings/ProjectVersion.txt',
 '_Documentation/AnimationExport/VANILLA_ANIMATION_EXPORT_20261024.md',
 '_Documentation/AnimationExport/VALIDATION_AND_COMPLETION.md',
 '_Documentation/AGENT_HANDOFF.md',
]
for name in ('Vanilla_Player_Animations.fbx','Vanilla_Zombie_Animations.fbx',
             'Vanilla_Player_Animations.fbx.clips.tsv','Vanilla_Zombie_Animations.fbx.clips.tsv',
             'player.validation.json','zombie.validation.json','source-coverage.json','unity-import-validation.txt'):
 files.append('Tools/AnimationExport/output/'+name)
for name in ('CAB-a03629c3b057b44faed3b90e6b5d7d7d.clips.txt',
             'CAB-b3590fc1b8354ea688be811f12bff2b7.clips.txt',
             'CAB-fc8d2ad7b89a12f466c9724d84de3406.clips.txt'):
 files.append('Tools/AnimationExport/'+name)
def digest(path):
 with path.open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
hashes={name:digest(root/name) for name in files}
manifest=out/'SHA256SUMS.txt'
manifest.write_text(''.join(h+'  '+name+'\n' for name,h in hashes.items()),encoding='utf-8')
files.append(manifest.relative_to(root).as_posix())
archive=out/'Vanilla_Animation_FBX_20261024.zip'
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=5) as z:
 for name in files: z.write(root/name,name)
with zipfile.ZipFile(archive) as z:
 assert set(z.namelist())==set(files)
 for name,expected in hashes.items():
  with z.open(name) as f: assert hashlib.file_digest(f,'sha256').hexdigest()==expected,name
archive.with_suffix('.zip.sha256').write_text(digest(archive)+'  '+archive.name+'\n')
print(json.dumps(dict(archive=str(archive),files=len(files),bytes=archive.stat().st_size,entry_hashes='PASS'),indent=2))
