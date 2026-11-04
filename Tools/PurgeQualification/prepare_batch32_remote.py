import hashlib,json,os,sys,xml.etree.ElementTree as E,zipfile,subprocess
from pathlib import Path
R=Path(__file__).resolve().parents[2];batch=sys.argv[1] if len(sys.argv)>1 else 'purge-b32'
assert batch.startswith('purge-b') and batch[7:].isdigit()
stage=R/'Tools/Profiler/out'/batch;stage.mkdir(exist_ok=True)
p=subprocess.run(['powershell.exe','-NoProfile','-Command','if(Get-Process 7DaysToDie,7DaysToDieServer -ErrorAction SilentlyContinue){exit 2}'],capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW);assert p.returncode==0,'Game active'
remote=R/'Tools/Profiler/out/purge-remote-20261102';data=Path('E:/_Haven/shared/CodexPurgeRemote20261102');save=data/'Saves/Navezgane/CodexPurgeRemote20261102'
assert save.is_dir();manifest=[]
assert not (stage/'before.zip').exists(),'Backup already exists; resume instead of overwriting'
with zipfile.ZipFile(stage/'before.zip','w',compression=zipfile.ZIP_STORED) as z:
 for label,source in [('old-remote-evidence',remote),('isolated-userdata',data)]:
  for f in source.rglob('*'):
   assert not (getattr(f.lstat(),'st_file_attributes',0)&0x400),'Reparse source refused'
   if not f.is_file():continue
   name=(Path(label)/f.relative_to(source)).as_posix();digest=hashlib.sha256(f.read_bytes()).hexdigest();z.write(f,name);assert hashlib.sha256(f.read_bytes()).hexdigest()==digest
   manifest.append({'entry':name,'sha256':digest,'bytes':f.stat().st_size})
with zipfile.ZipFile(stage/'before.zip') as z:
 for f in manifest:assert hashlib.sha256(z.read(f['entry'])).hexdigest()==f['sha256']
(stage/'backup-manifest.json').write_text(json.dumps(manifest,indent=2))
admin=data/'Saves/serveradmin.xml';(stage/'serveradmin.before.xml').write_bytes(admin.read_bytes());(stage/'serverconfig.before.xml').write_bytes((remote/'serverconfig.xml').read_bytes())
world=R.parents[1]/'Data/Worlds/Navezgane/prefabs.xml';assert world.is_file()
records=set();m=E.parse(save/'RebirthData/Purge/Clearance/world.xml').getroot()
for shard in m.findall('shard'):
 b=(save/'RebirthData/Purge/Clearance'/shard.get('file')).read_bytes();assert hashlib.sha256(b).hexdigest()==shard.get('sha256')
 for q in E.fromstring(b).findall('poi'):records.add((q.get('prefab'),int(q.get('x')),int(q.get('z'))))
idx={p.stem:p for p in (R.parents[1]/'Data/Prefabs').rglob('*.xml')};rows=[]
for d in E.parse(world).getroot().findall('decoration'):
 name=d.get('name');f=idx.get(name)
 if not f:continue
 props={a.get('name'):a.get('value','') for a in E.parse(f).getroot().findall('property')};parts=props.get('SleeperVolumeGroup','').split(',');n=len(parts)//3
 if not 3<=n<=5 or any(k and k.startswith('SVS') and v.strip() for k,v in props.items()):continue
 if props.get('TraderArea')=='True' or 'rwgonly' in props.get('Tags','').lower() or 'streettile' in props.get('Tags','').lower():continue
 x,y,z=map(int,d.get('position').split(','))
 if (name,x,z) in records:continue
 total=sum(int(parts[i+2]) for i in range(0,n*3,3))
 if total>8:continue
 rows.append({'name':name,'origin':[x,y,z],'size':list(map(int,props['PrefabSize'].split(','))),'rotation':int(d.get('rotation')),'rooms':n,'maxActors':total,'distance':round(((x+1234)**2+(z-408)**2)**.5,1),'sourceHash':hashlib.sha256(f.read_bytes()).hexdigest()})
rows.sort(key=lambda r:r['distance']);(stage/'fresh-candidates.json').write_text(json.dumps(rows[:12],indent=2));print(json.dumps({'backupFiles':len(manifest),'bytes':sum(f['bytes'] for f in manifest),'recordCount':len(records),'candidates':rows[:5]},indent=2))
