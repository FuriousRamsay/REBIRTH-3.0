"""Install reviewed audit corrections, retaining their prompts and provenance."""
import json
from pathlib import Path
import assets

revision = assets.OUT/'JarRevision'
m = assets.init()
for key, title in [('dried','Dried Meat'), ('potpie','Mushroom Pot Pie')]:
    record = json.loads((revision/f'audit-existing-{key}.json').read_text())
    task = next((t for t in m['tasks'] if t['key']=='existing-'+key), None)
    if task is None:
        icon = 'rebirthCookingFood'+key.title()
        task = dict(key='existing-'+key, category='food', title=title, icon=icon,
                    path=str(assets.ICONS/(icon+'.png')), master=str(assets.MASTERS/(icon+'.png')),
                    prompt=record['prompt'], refs=[], status='pending')
        m['tasks'].append(task)
    assets.accept(m, m['tasks'].index(task), Path(record['source']))
    card = next(t for t in m['tasks'] if t['key']=='C-'+key)
    card['refs'][1] = task['master']
for key in ['C-L03','C-L07','C-potpie','C-dried-transparent']:
    record = json.loads((revision/f'audit-{key}.json').read_text())
    task = next(t for t in m['tasks'] if t['key']==record['key'])
    assets.accept(m, m['tasks'].index(task), Path(record['source']))
for record_path in sorted(revision.glob('audit-*.json')):
    record = json.loads(record_path.read_text())
    task = next((t for t in m['tasks'] if t['key']==record['key']), None)
    if task:
        task.setdefault('audit_prompts',[])
        if record not in task['audit_prompts']:
            task['audit_prompts'].append(record)
        task['visual_review'] = '2026-09-16 ingredient and card consistency audit; see ART_CONSISTENCY_AUDIT.md.'
assets.write(m)
print('Audit corrections installed; sources and prompts recorded.')
