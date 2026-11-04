"""Copy only the configured disposable save and its exact external preference scopes."""
import hashlib, json, os, subprocess, sys, zipfile, urllib.request
from datetime import datetime, timezone
from pathlib import Path
R=Path(__file__).resolve().parents[2]
config=json.loads((R/'Tools/GameBridge/gamebridge.config.json').read_text(encoding='utf-8-sig'))
assert config['world']=='West Xuyofu Territory' and config['save']=='CodexTestPurgeFresh20261102'
def no_game():
    p=subprocess.run(['powershell.exe','-NoProfile','-NonInteractive','-Command',"if(Get-Process 7DaysToDie -ErrorAction SilentlyContinue){exit 2}else{exit 0}"],capture_output=True,text=True,creationflags=subprocess.CREATE_NO_WINDOW)
    assert p.returncode==0,'Could not prove game is closed'
def safe_snapshot_state():
    if '--menu' not in sys.argv:
        no_game()
        return
    control=json.loads((R/'Tools/Profiler/out'/sys.argv[1]/'control.private.json').read_text(encoding='utf-8-sig'))
    session=json.loads(Path(control['session']).read_text(encoding='utf-8-sig'))
    assert session['pid']==control['pid'], 'Session identity mismatch'
    headers={'X-Bridge-Token':session['token'],'X-Bridge-Run':control['runToken']}
    def read(route):
        req=urllib.request.Request(f"http://127.0.0.1:{session['port']}{route}",headers=headers)
        with urllib.request.urlopen(req,timeout=30) as response:return json.load(response)
    ping=read('/ping');life=read('/purgelifecycle')
    assert ping['ok'] and ping['pid']==control['pid'] and ping['state']=='menu'
    batch_name=sys.argv[1]
    assert batch_name.startswith('purge-b') and batch_name[7:].isdigit(), 'Unexpected batch stage'
    assert ping['batch']['run']=='purgebatch'+batch_name[7:] and ping['batch']['queued']==0
    assert life['ok'] and life['worldPresent'] is False and life['equipmentCachedWorld'] is False
safe_snapshot_state()
base=Path(os.environ['APPDATA'])/'7DaysToDie';save=base/'Saves'/config['world']/config['save']
evidence=json.loads((R/'Tools/Profiler/out/purge-b24/reload-fresh-crate-observe.json').read_text(encoding='utf-8-sig'))
world=json.loads(evidence['results'][0]['output'][0])['world']
assert len(world)==32 and save.is_dir()
norm=str(save.absolute()).replace('\\','/').rstrip('/').lower()
keys=[hashlib.sha256((norm+'|'+world).encode()).hexdigest(),hashlib.sha256(norm.encode()).hexdigest()]
stage=R/'Tools/Profiler/out'/sys.argv[1]/'save-backups'/('snapshot-'+datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S'))
assert stage.is_relative_to(R/'Tools/Profiler/out') and not stage.exists()
sources=[('native-save',save)]+[(f'external/{kind}/{key}',base/'RebirthData'/kind/key) for kind in ('HudTracking','Journal') for key in keys]
manifest={'disposable':config['save'],'world':world,'sources':[],'files':[]}
stage.mkdir(parents=True)
# Archive entries avoid Windows' legacy destination-path length limit. No
# source content is changed; failed earlier directory copies are retained.
with zipfile.ZipFile(stage/'save.zip','w',compression=zipfile.ZIP_STORED) as archive:
    for label,source in sources:
        manifest['sources'].append({'path':str(source),'copy':label,'exists':source.exists()})
        if not source.exists():continue
        for node in [source,*source.rglob('*')]:
            assert not (getattr(node.lstat(),'st_file_attributes',0)&0x400),f'Reparse source refused: {node}'
        for source_file in source.rglob('*'):
            if not source_file.is_file():continue
            name=(Path(label)/source_file.relative_to(source)).as_posix()
            before_stat=source_file.stat()
            digest=hashlib.sha256(source_file.read_bytes()).hexdigest()
            archive.write(source_file,arcname=name)
            after_stat=source_file.stat()
            assert (before_stat.st_size,before_stat.st_mtime_ns)==(after_stat.st_size,after_stat.st_mtime_ns),'Source changed during snapshot'
            assert hashlib.sha256(source_file.read_bytes()).hexdigest()==digest,'Source content changed during snapshot'
            manifest['files'].append({'copy':name,'bytes':source_file.stat().st_size,'sha256':digest})
with zipfile.ZipFile(stage/'save.zip') as archive:
    for row in manifest['files']:assert hashlib.sha256(archive.read(row['copy'])).hexdigest()==row['sha256']
safe_snapshot_state();(stage/'manifest.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps({'snapshot':str(stage),'verifiedFiles':len(manifest['files']),'bytes':sum(f['bytes'] for f in manifest['files'])}))
