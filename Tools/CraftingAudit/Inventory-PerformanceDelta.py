import csv,hashlib,pathlib,json
base={r['path'].replace('\\','/'):r for r in csv.DictReader(open('Tools/PerformanceAudit20261009/SOURCE_SCAN.csv',encoding='utf-8-sig'))}
rows=[];current=set()
for p in sorted(pathlib.Path('Scripts').rglob('*.cs')):
 key=p.as_posix();current.add(key);digest=hashlib.sha256(p.read_bytes()).hexdigest()
 if key not in base or base[key]['sha256']!=digest:rows.append(dict(path=key,status='new' if key not in base else 'changed',sha256=digest,lines=len(p.read_text(encoding='utf-8-sig').splitlines())))
for key in base.keys()-current:rows.append(dict(path=key,status='removed',sha256='',lines=0))
out=pathlib.Path('_Documentation/PerformanceAudit_20261010')
with (out/'CHANGED_SOURCE.csv').open('w',newline='',encoding='utf-8') as f:
 w=csv.DictWriter(f,fieldnames=['path','status','sha256','lines']);w.writeheader();w.writerows(rows)
print(json.dumps({'currentFiles':len(current),'baselineFiles':len(base),'changedFiles':len(rows)}))
for r in rows:print(r['status'],r['path'],r['lines'])
