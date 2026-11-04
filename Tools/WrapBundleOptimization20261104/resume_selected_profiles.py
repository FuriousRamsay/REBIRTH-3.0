import json,concurrent.futures
from apply_selected_profiles import BASE,ids,job,digest
rows=[]
for id,name in ids.items():
    report=(BASE/'Runtime/Resources'/name).with_suffix('.report.json')
    if report.exists():
        r=json.loads(report.read_text());assert digest(BASE/'Runtime/Resources'/name)==r['sha256'];rows.append(r)
missing=[id for id in ids if id not in {r['id'] for r in rows}]
print('Resume',len(rows),'complete;',len(missing),'remaining',flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
    for row in pool.map(job,missing):
        rows.append(row);print(len(rows),row['id'],row['bytes'],flush=True)
(BASE/'built.json').write_text(json.dumps(rows,indent=2))
(BASE/'gpu-plan.json').write_text(json.dumps(dict(directory=str(BASE/'Runtime/Resources'),bundles=list(ids.values()))))
print('BUILT',sum(r['bytes'] for r in rows),flush=True)
