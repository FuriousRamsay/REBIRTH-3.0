"""Package only follow-up changes against the local checkpoint, with verified hashes."""
from pathlib import Path
import hashlib, json, subprocess, zipfile

root = Path(__file__).resolve().parents[2]
def git(*args):
    return subprocess.check_output(['git', *args], cwd=root).decode('utf-8')
assert git('rev-parse', '--short=7', 'HEAD').strip() == '46b7aef'
paths = set(git('diff', '--name-only', '-z', 'HEAD').split('\0'))
paths.update(git('ls-files', '--others', '--exclude-standard', '-z').split('\0'))
paths.discard('')
manifest_path = '_Documentation/ReleaseFollowup/changed_files_manifest.json'
paths.discard(manifest_path)
assert not any('/out/session' in p or p.startswith(('.git/', '.claude/')) for p in paths)
manifest = {'baseline': git('rev-parse', 'HEAD').strip(),
            'scope': 'Uncommitted follow-up only; not a full cumulative project',
            'files': [], 'deleted': []}
for relative in sorted(paths):
    path = root / relative
    if not path.exists():
        manifest['deleted'].append(relative)
        continue
    data = path.read_bytes()
    manifest['files'].append({'path': relative, 'bytes': len(data),
                              'sha256': hashlib.sha256(data).hexdigest()})
(root/manifest_path).write_text(json.dumps(manifest, indent=2), encoding='utf-8')
destination = Path('C:/Users/Etienne/Desktop/__0/_0/__Rebirth')
assert destination.is_dir()
archive = destination/'REBIRTH_3_0_FOLLOWUP_2026-10-03_CHANGED_FILES.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
    for row in manifest['files']:
        z.write(root/row['path'], row['path'])
    z.write(root/manifest_path, manifest_path)
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    assert set(z.namelist()) == {row['path'] for row in manifest['files']} | {manifest_path}
    for row in manifest['files']:
        assert hashlib.sha256(z.read(row['path'])).hexdigest() == row['sha256'], row['path']
print(json.dumps({'archive': str(archive), 'files': len(manifest['files'])+1,
                  'bytes': archive.stat().st_size, 'verified': True,
                  'sha256': hashlib.sha256(archive.read_bytes()).hexdigest()}))
