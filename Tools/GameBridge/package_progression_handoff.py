"""Changed files only, relative to the user's pre-work checkpoint. Never commits."""
from pathlib import Path
import hashlib
import json
import subprocess
import zipfile

root = Path(__file__).resolve().parents[2]
git = ['git', '-c', 'safe.directory=' + root.as_posix()]
def paths(*args):
    return subprocess.check_output(git + list(args), cwd=root).decode('utf-8').split('\0')

files = set(paths('diff', '--name-only', '--diff-filter=ACMRTUXB', '364392a', '-z'))
files.update(paths('ls-files', '--others', '--exclude-standard', '-z'))
files = {p for p in files if p and not p.startswith(('.claude/', '.codex/', '.git/')) and (root/p).is_file()}
# Repository's generic Debug/ ignore rule hides this modified source from ordinary status.
files.add('Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptanceVectorHarness.cs')
manifest_path = '_Documentation/Skill_Playtest/20261002_changed_files_manifest.json'
files.discard(manifest_path)
entries = []
for p in sorted(files):
    data = (root/p).read_bytes()
    entries.append({'path':p, 'bytes':len(data), 'sha256':hashlib.sha256(data).hexdigest()})
(root/manifest_path).write_text(json.dumps({'source_checkpoint':'364392a', 'full_project':False,
    'files':entries}, indent=2), encoding='utf-8')
files.add(manifest_path)
out = root/'Tools/GameBridge/out/delivery'
out.mkdir(parents=True, exist_ok=True)
archive = out/'REBIRTH_20261002_progression_changed_files.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
    for p in sorted(files):
        z.write(root/p, p)
with zipfile.ZipFile(archive) as z:
    assert set(z.namelist()) == files
    assert z.testzip() is None
    for entry in entries:
        assert hashlib.sha256(z.read(entry['path'])).hexdigest() == entry['sha256']
digest = hashlib.sha256(archive.read_bytes()).hexdigest()
archive.with_suffix('.zip.sha256').write_text(digest+'  '+archive.name+'\n', encoding='utf-8')
print(json.dumps({'archive':str(archive),'files':len(files),'sha256':digest,'verified':True}))
